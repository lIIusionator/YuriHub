using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using Yuri.Core;
using Yuri.Gfx;

namespace Yuri.Platform;

/// <summary>One place's public game details, as the gameinfo child returned them.</summary>
public sealed class GameRec
{
    public string Place = "", Universe = "", Nm = "", Desc = "";
    public long Playing, Visits, Favs, Up, Down;
    public List<string> Thumbs = new();                             // up to ten 768x432 shots, files on disk
    public string Icon = "";                                        // the 256x256 icon, a file on disk
    public long At;                                                 // when it was fetched (ticks); 0 while pending
    public bool Failed;
}

/// <summary>
/// SPF.stats and its prefetch: place -> details, fetched once per place in
/// the background over Roblox's public APIs (universe, games, votes,
/// thumbnails, icons), the pictures kept under YURI\cache for twelve hours
/// as the child did in the temp folder, the details kept beside them so a
/// second launch shows a card at once. Anyone can Fetch; the hub is poked
/// when a record lands.
/// </summary>
public static class GameInfo
{
    static readonly Dictionary<string, GameRec> Stats = new();
    static readonly HashSet<string> InFlight = new();
    static readonly SemaphoreSlim Gate = new(2);
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    static string CacheDir => Path.Combine(Paths.Root, "cache");
    static readonly JsonSerializerOptions JsonOpts = new() { IncludeFields = true };
    static readonly Regex UniRx = new("\"universeId\"\\s*:\\s*(\\d+)", RegexOptions.Compiled);
    static readonly Regex ImgRx = new("\"imageUrl\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.Compiled);
    public static Action? Changed;
    /// <summary>Set by a host that must not touch the network (the test harness).</summary>
    public static bool NoNetwork;

    static GameInfo() { Http.DefaultRequestHeaders.UserAgent.ParseAdd("YURI"); }

    /// <summary>SPF.stats[pl] when it is known, else null (Fetch has been queued by the caller).</summary>
    public static GameRec? Get(string place) => Stats.TryGetValue(place, out var r) && r.At != 0 ? r : null;
    public static bool Known(string place) => Stats.TryGetValue(place, out var r) && r.At != 0;

    /// <summary>SPFThumbN / SPFThumbAt / SPFIco.</summary>
    public static int ThumbN(string place) => Get(place)?.Thumbs.Count ?? 0;
    public static Bitmap? ThumbAt(string place, int i)
    {
        var r = Get(place);
        return r is null || i < 1 || i > r.Thumbs.Count ? null : Img.File(r.Thumbs[i - 1]);
    }
    public static Bitmap? Ico(string place) { var r = Get(place); return r is null || r.Icon == "" ? null : Img.File(r.Icon); }

    /// <summary>SPFStatsFetch(pl): queue the lookup once; cached details arrive without the network.</summary>
    public static void Fetch(string place)
    {
        if (string.IsNullOrEmpty(place) || !place.All(char.IsDigit)) return;
        // A failure got `At` stamped like a success, so the guard below treated
        // it as an answer and the place kept its raw id as a name for the rest of
        // the session. A failed record is worth keeping - it stops a dead id
        // being asked for on every frame - but it has to expire.
        const long RETRY_MS = 20000;
        if (InFlight.Contains(place)) return;
        if (Stats.TryGetValue(place, out var have) && have.At != 0 && !(have.Failed && Clock.Tick - have.At > RETRY_MS)) return;
        if (LoadCached(place) is { } cached) { Stats[place] = cached; Changed?.Invoke(); return; }
        if (NoNetwork) return;
        Stats[place] = new GameRec { Place = place };
        InFlight.Add(place);
        _ = Task.Run(async () =>
        {
            await Gate.WaitAsync();
            GameRec rec;
            try { rec = await FetchAsync(place); }
            catch { rec = new GameRec { Place = place, Failed = true }; }
            finally { Gate.Release(); }
            rec.At = Math.Max(1, Clock.Tick);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Stats[place] = rec;
                InFlight.Remove(place);
                if (!rec.Failed) SaveCached(rec);
                Changed?.Invoke();
            });
        });
    }

    static async Task<string> GetJ(string url)
    {
        try
        {
            using var res = await Http.GetAsync(url);
            return res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync() : "";
        }
        catch { return ""; }
    }
    static async Task<bool> GetFile(string url, string dst)
    {
        try
        {
            if (File.Exists(dst) && new FileInfo(dst).Length > 512 && (DateTime.UtcNow - File.GetLastWriteTimeUtc(dst)).TotalHours < 12) return true;
            using var res = await Http.GetAsync(url);
            if (!res.IsSuccessStatusCode) return false;
            var bytes = await res.Content.ReadAsByteArrayAsync();
            if (bytes.Length < 64) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            await File.WriteAllBytesAsync(dst, bytes);
            Img.Forget(dst);
            return true;
        }
        catch { return false; }
    }
    static string Str(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    static long Num(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;

    static async Task<GameRec> FetchAsync(string place)
    {
        var rec = new GameRec { Place = place };
        string j = await GetJ("https://apis.roblox.com/universes/v1/places/" + place + "/universe");
        var um = UniRx.Match(j);
        if (!um.Success) { rec.Failed = true; return rec; }
        rec.Universe = um.Groups[1].Value;
        j = await GetJ("https://games.roblox.com/v1/games?universeIds=" + rec.Universe);
        try
        {
            using var doc = JsonDocument.Parse(j);
            var d = doc.RootElement.GetProperty("data")[0];
            rec.Nm = Str(d, "name"); rec.Desc = Str(d, "description");
            rec.Playing = Num(d, "playing"); rec.Visits = Num(d, "visits"); rec.Favs = Num(d, "favoritedCount");
        }
        catch { }
        j = await GetJ("https://games.roblox.com/v1/games/votes?universeIds=" + rec.Universe);
        try
        {
            using var doc = JsonDocument.Parse(j);
            var d = doc.RootElement.GetProperty("data")[0];
            rec.Up = Num(d, "upVotes"); rec.Down = Num(d, "downVotes");
        }
        catch { }
        j = await GetJ("https://thumbnails.roblox.com/v1/games/multiget/thumbnails?universeIds=" + rec.Universe + "&size=768x432&format=Png&isCircular=false&countPerUniverse=10");
        int n = 0;
        foreach (Match m in ImgRx.Matches(j))
        {
            if (n >= 10) break;
            string u2 = m.Groups[1].Value.Replace("\\/", "/");
            if (u2 == "") continue;
            string dst = Path.Combine(CacheDir, "thumb_" + place + "_" + (n + 1) + ".png");
            if (await GetFile(u2, dst)) { rec.Thumbs.Add(dst); n++; }
        }
        j = await GetJ("https://thumbnails.roblox.com/v1/games/icons?universeIds=" + rec.Universe + "&size=256x256&format=Png&isCircular=false");
        var im = ImgRx.Match(j);
        if (im.Success)
        {
            string dst2 = Path.Combine(CacheDir, "icon_" + place + ".png");
            if (await GetFile(im.Groups[1].Value.Replace("\\/", "/"), dst2)) rec.Icon = dst2;
        }
        if (rec.Nm == "" && rec.Thumbs.Count == 0) rec.Failed = true;
        return rec;
    }

    // ---- the on-disk record, so a second launch shows the card at once ----
    static string RecPath(string place) => Path.Combine(CacheDir, "game_" + place + ".json");
    static GameRec? LoadCached(string place)
    {
        try
        {
            var p = RecPath(place);
            if (!File.Exists(p) || (DateTime.UtcNow - File.GetLastWriteTimeUtc(p)).TotalHours > 12) return null;
            var rec = JsonSerializer.Deserialize<GameRec>(File.ReadAllText(p), JsonOpts);
            if (rec is null) return null;
            rec.Thumbs.RemoveAll(t => !File.Exists(t));
            if (rec.Icon != "" && !File.Exists(rec.Icon)) rec.Icon = "";
            rec.At = Math.Max(1, Clock.Tick);
            return rec;
        }
        catch { return null; }
    }
    static void SaveCached(GameRec rec)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            File.WriteAllText(RecPath(rec.Place), JsonSerializer.Serialize(rec, JsonOpts));
        }
        catch { }
    }
}
