using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Yuri.Core;
using Yuri.Shell.Hub;

namespace Yuri.Modules.FastFlags;

/// <summary>A community folder: GENERAL or one game (its place id), with its sets.</summary>
public sealed class CommEntry
{
    public string Kind = "game", Folder = "", Place = "", Dir = "";
    public readonly List<CommSet> Sets = new();
}
public sealed class CommSet
{
    public string File = "", Author = "UNKNOWN", Disk = "", Sha = "";
    public int N;
    public bool Have;
}

/// <summary>
/// The community repository (lIIusionator/Community-Fflags), cached under
/// YURI\community so the browser opens instantly and works offline:
/// index.txt lists the folders and their sets (the .ahk's Chr(1)-separated
/// H / C / S lines), each set's JSON body kept beside it. A sync reads the
/// repository's HEAD from its atom feed, walks the git tree, fetches only
/// the sets whose blob SHA changed, and rewrites the index; when the API is
/// rate-limited it refreshes what it already knows. This is the `commsync`
/// child folded into a Task.
/// </summary>
public static class FfmComm
{
    const char SEP = '\u0001';
    const string Repo = "lIIusionator/Community-Fflags";
    const int FRESH_MS = 180000;                                        // FFM_COMM_FRESH_MS: three minutes - an automatic sync younger than this is not repeated
    public static readonly List<CommEntry> Entries = new();           // FFM_COMM
    public static long IndexAt, SyncAt, OkAt;                          // FFM_COMM_AT / SYNCAT / OKAT
    public static bool Syncing, Forced;                                // FFM_COMM_SYNC / FORCED
    public static string Msg = "";                                      // FFM_COMM_MSG: why it is offline, "" when fine
    public static string Root => Paths.Comm;
    static readonly Regex Unsafe = new("[<>:\"/\\\\|?*]", RegexOptions.Compiled);
    static readonly Regex Ctrl = new("[\\x00-\\x1F]", RegexOptions.Compiled);
    static readonly Regex Head = new("Grit::Commit/([0-9a-f]{40})", RegexOptions.Compiled);
    static readonly Regex Obj = new("\\{[^{}]*\\}", RegexOptions.Compiled);
    static readonly Regex Pair = new("\"[^\"]+\"\\s*:\\s*(?:\"[^\"]*\"|[^,\\}\\s]+)", RegexOptions.Compiled);
    static readonly Regex Author = new("^[ \\t]*//[ \\t]*#[ \\t]*(\\S.*?)[ \\t]*$", RegexOptions.Compiled | RegexOptions.Multiline);
    static readonly Regex Comment = new("^[ \\t]*//.*$", RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>CommSafe(n): a name the file system takes.</summary>
    public static string Safe(string n)
    {
        n = Unsafe.Replace(n, "_");
        n = Ctrl.Replace(n, "");
        n = n.TrimEnd(' ', '.');
        return n == "" ? "_" : n;
    }
    /// <summary>CommBlobSha(txt): git's blob SHA-1 of the text.</summary>
    public static string BlobSha(string txt)
    {
        var body = Encoding.UTF8.GetBytes(txt);
        var hdr = Encoding.UTF8.GetBytes("blob " + body.Length + "\0");
        var all = new byte[hdr.Length + body.Length];
        hdr.CopyTo(all, 0); body.CopyTo(all, hdr.Length);
        return Convert.ToHexString(SHA1.HashData(all)).ToLowerInvariant();
    }
    /// <summary>How many "name": value pairs a set body holds, after its // comment lines are dropped; 0 when it is not a { } object.</summary>
    public static int CountPairs(string body, out string txt, out string who)
    {
        who = "UNKNOWN";
        var mc = Author.Match(body);
        if (mc.Success) who = mc.Groups[1].Value;
        txt = Comment.Replace(body, "");
        if (!txt.Trim(' ', '\t', '\r', '\n').StartsWith('{')) return 0;
        return Pair.Matches(txt).Count;
    }

    // ---- FFMCommIndexLoad ----
    public static int IndexLoad()
    {
        Entries.Clear();
        string txt = "";
        try { if (File.Exists(Path.Combine(Root, "index.txt"))) txt = File.ReadAllText(Path.Combine(Root, "index.txt"), Encoding.UTF8); } catch { }
        CommEntry? cur = null;
        foreach (var raw in txt.Split('\n'))
        {
            var f = raw.TrimEnd('\r').Split(SEP);
            if (f.Length >= 3 && f[0] == "C")
            {
                cur = new CommEntry { Kind = f[1].Equals("general", StringComparison.OrdinalIgnoreCase) ? "general" : "game", Folder = f[1], Place = f[2], Dir = f.Length >= 4 && f[3] != "" ? f[3] : f[1] };
                Entries.Add(cur);
            }
            else if (f.Length >= 5 && f[0] == "S" && cur is not null)
            {
                int.TryParse(f[4], out int cnt);
                string disk = f.Length >= 6 && f[5] != "" ? f[5] : f[2];
                cur.Sets.Add(new CommSet { File = f[2], Author = f[3] == "" ? "UNKNOWN" : f[3], N = cnt, Disk = disk,
                    Have = File.Exists(Path.Combine(Root, cur.Dir, disk)), Sha = f.Length >= 7 ? f[6] : "" });
            }
        }
        IndexAt = Clock.Tick;
        return Entries.Count;
    }

    // ---- FFMCommInsert(ci, si): a cached set replaces the staged list ----
    public static void Insert(int ci, int si)
    {
        if (ci < 0 || ci >= Entries.Count) return;
        var ent = Entries[ci];
        if (si < 0 || si >= ent.Sets.Count) return;
        var st = ent.Sets[si];
        Ffm.Snap("community set");
        string body = "";
        try { body = File.ReadAllText(Path.Combine(Root, ent.Dir, st.Disk), Encoding.UTF8); } catch { }
        if (body.Trim() == "")
        {
            st.Have = false;
            Ffm.Say(st.File.ToUpperInvariant() + " IS MISSING FROM THE CACHE - REFRESHING", 0xFFFB7185);
            _ = Sync(true);
            return;
        }
        Ffm.Flags.Clear(); Ffm.Flash.Clear(); Ffm.Reindex();
        FfmPanel.ScrT = 0; FfmPanel.Scr = 0;
        int n = Ffm.ParseInto(body);
        if (n == 0) { Ffm.Say("NO FLAGS FOUND IN THAT SET", 0xFFFB7185); return; }
        FfmViews.CloseView();
        Ffm.Say($"{st.Author.ToUpperInvariant()} / {st.File.Replace(".json", "").ToUpperInvariant()} LOADED - {n} FLAG(S)", 0xFF34D399);
    }

    // ---- FFMCommSync(force): the repository walk, off the UI thread ----
    sealed record OldSet(string Who, string Cnt, string Disk, string Sha, string Dir);
    sealed class OldCat { public string Place = "", Msha = "", Dir = ""; public readonly List<string> Files = new(); }
    sealed class Folder { public bool Marked; public string Msha = ""; public readonly List<(string name, string sha)> Files = new(); }
    static Task? _sync;

    /// <summary>Set by a host that must not touch the network (the test harness).</summary>
    public static bool NoNetwork;

    public static Task Sync(bool force = false)
    {
        if (NoNetwork) return Task.CompletedTask;
        if (Syncing && _sync is { IsCompleted: false }) return _sync;
        if (!force && OkAt != 0 && Clock.Tick - OkAt < FRESH_MS) return Task.CompletedTask;
        try { Directory.CreateDirectory(Root); } catch { }
        Syncing = true; SyncAt = Clock.Tick; Forced = force;
        string root = Root;
        // The loader waits on this Task, and it used to complete when the FETCH
        // had finished - a step before Synced() had run IndexLoad on the UI
        // thread. So the loader could count the sync done, open the hub, and
        // draw a frame with the entries list still empty. It completes when the
        // result has actually landed now.
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _sync = done.Task;
        _ = Task.Run(async () =>
        {
            string reply;
            try { reply = await SyncCore(root, force); }
            catch (Exception ex) { reply = "ERR" + SEP + "sync failed - " + ex.Message; }
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { try { Synced(reply); } finally { done.TrySetResult(true); } });
        });
        return _sync;
    }

    /// <summary>FFMCommSynced(payload).</summary>
    static void Synced(string payload)
    {
        Syncing = false;
        bool forced = Forced; Forced = false;
        var f = payload.Split(SEP);
        if (f.Length >= 1 && f[0] == "OK")
        {
            IndexLoad();
            Msg = "";
            OkAt = Clock.Tick;
            if (forced)
            {
                string tot = f.Length >= 2 ? f[1] : "?", nNew = f.Length >= 3 ? f[2] : "?", note = f.Length >= 5 ? f[4] : "";
                Ffm.Say($"COMMUNITY REFRESHED - {tot} SET{(tot == "1" ? "" : "S")}  \u00B7  {(nNew == "0" ? "NOTHING CHANGED" : nNew + " UPDATED")}" + (note != "" ? "  \u00B7  " + note.ToUpperInvariant() : ""),
                    note != "" ? 0xFFFBBF24 : 0xFF34D399);
            }
        }
        else
        {
            Msg = f.Length >= 2 && f[1] != "" ? f[1] : "sync failed";
            if (forced) Ffm.Say("COMMUNITY REFRESH FAILED - " + Msg.ToUpperInvariant(), 0xFFFB7185);
        }
        HubSurface.Live?.Tim(Pace.TICK_A);
    }

    static async Task<string> SyncCore(string root, bool forced)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("YURI");
        int lastStatus = 0; long lastReset = 0;
        async Task<string> Get(string url)
        {
            lastStatus = 0; lastReset = 0;
            try
            {
                using var res = await http.GetAsync(url);
                lastStatus = (int)res.StatusCode;
                if (res.Headers.TryGetValues("X-RateLimit-Reset", out var rv) && long.TryParse(rv.FirstOrDefault(), out var r)) lastReset = r;
                return res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync() : "";
            }
            catch { return ""; }
        }
        // ---- what the cache already holds ----
        var oldS = new Dictionary<string, OldSet>(StringComparer.Ordinal);
        var oldC = new Dictionary<string, OldCat>(StringComparer.Ordinal);
        var oldOrder = new List<string>();
        string oldHead = ""; bool oldFull = false, oldAllHere = true;
        string oldTxt = "";
        try { var ip = Path.Combine(root, "index.txt"); if (File.Exists(ip)) oldTxt = File.ReadAllText(ip, Encoding.UTF8); } catch { }
        string oldFold = "", oldDir = "";
        foreach (var raw in oldTxt.Split('\n'))
        {
            var fl = raw.TrimEnd('\r').Split(SEP);
            if (fl.Length >= 2 && fl[0] == "H") { oldHead = fl[1]; oldFull = fl.Length >= 3 && fl[2] == "1"; }
            else if (fl.Length >= 3 && fl[0] == "C")
            {
                oldFold = fl[1]; oldDir = fl.Length >= 4 && fl[3] != "" ? fl[3] : fl[1];
                oldC[oldFold] = new OldCat { Place = fl[2], Msha = fl.Length >= 5 ? fl[4] : "", Dir = oldDir };
                oldOrder.Add(oldFold);
            }
            else if (fl.Length >= 5 && fl[0] == "S" && oldFold != "")
            {
                string disk = fl.Length >= 6 && fl[5] != "" ? fl[5] : fl[2];
                oldS[oldFold + "/" + fl[2]] = new OldSet(fl[3], fl[4], disk, fl.Length >= 7 ? fl[6] : "", oldDir);
                oldC[oldFold].Files.Add(fl[2]);
                if (!File.Exists(Path.Combine(root, oldDir, disk))) oldAllHere = false;
            }
        }
        // ---- HEAD: nothing to do when the cache is at it and complete ----
        var hm = Head.Match(await Get($"https://github.com/{Repo}/commits/HEAD.atom"));
        string head = hm.Success ? hm.Groups[1].Value : "";
        if (!forced && head != "" && head == oldHead && oldFull && oldAllHere && oldS.Count > 0)
            return "OK" + SEP + oldS.Count + SEP + "0" + SEP + oldS.Count;
        string reff = head != "" ? head : "HEAD";
        // ---- the whole repository in ONE request, and not through the API ----
        // The tree used to come from api.github.com, which allows sixty
        // unauthenticated calls an hour per address. A boot and a few opens ran
        // through that, and once limited the sync fell back to refreshing the
        // folders it already knew - so a set added since then simply never
        // appeared until the hour turned, with nothing to say so except a note
        // in the refresh message. The archive is served by github.com itself,
        // is not metered, and carries every file's body as well, so the per-file
        // raw fetches that followed are gone too.
        var bodies = new Dictionary<string, string>(StringComparer.Ordinal);      // "folder/file" -> text
        bool limited = false; string note = "";
        {
            byte[]? zip = null;
            try
            {
                using var res = await http.GetAsync($"https://github.com/{Repo}/archive/{(head != "" ? head : "refs/heads/main")}.zip");
                if (res.IsSuccessStatusCode) zip = await res.Content.ReadAsByteArrayAsync();
                else lastStatus = (int)res.StatusCode;
            }
            catch { zip = null; }
            if (zip is null || zip.Length == 0)
            {
                if (oldOrder.Count > 0) { limited = true; note = "could not fetch the repository - showing the cached sets"; }
                else return "ERR" + SEP + "could not reach the repository";
            }
            else
            {
                try
                {
                    using var za = new System.IO.Compression.ZipArchive(new MemoryStream(zip), System.IO.Compression.ZipArchiveMode.Read);
                    foreach (var e in za.Entries)
                    {
                        if (e.FullName.EndsWith('/')) continue;
                        // strip the archive's top folder ("Community-Fflags-<ref>/")
                        int top = e.FullName.IndexOf('/');
                        if (top < 0) continue;
                        string rel = e.FullName[(top + 1)..];
                        int sl = rel.IndexOf('/');
                        if (sl < 0 || rel.IndexOf('/', sl + 1) >= 0) continue;         // one deep only
                        using var sr = new StreamReader(e.Open(), Encoding.UTF8);
                        bodies[rel] = sr.ReadToEnd();
                    }
                }
                catch { if (oldOrder.Count > 0) { limited = true; note = "the repository archive could not be read - showing the cached sets"; } else return "ERR" + SEP + "the repository archive could not be read"; }
            }
        }
        var folders = new Dictionary<string, Folder>(StringComparer.Ordinal);
        var order = new List<string>();
        if (!limited)
        {
            foreach (var kv in bodies)
            {
                string path = kv.Key;
                int sl = path.IndexOf('/');
                string fold = path[..sl], name = path[(sl + 1)..];
                string sha = BlobSha(kv.Value);
                if (!folders.TryGetValue(fold, out var rec)) { folders[fold] = rec = new Folder(); order.Add(fold); }
                if (name == "place-id.ini") { rec.Marked = true; rec.Msha = sha; }
                else if (Regex.IsMatch(name, "\\.json$", RegexOptions.IgnoreCase)) rec.Files.Add((name, sha));
            }
            // the .github folder and the like carry no place-id and no sets; the
            // loop below skips anything unmarked that is not `general`
        }
        else
        {
            foreach (var fold in oldOrder)
            {
                var rec = new Folder { Marked = true };
                foreach (var f in oldC[fold].Files) rec.Files.Add((f, ""));
                folders[fold] = rec; order.Add(fold);
            }
        }
        var gen = new StringBuilder(); var game = new StringBuilder();
        int n = 0, nNew = 0, same = 0;
        foreach (var fold in order)
        {
            var rec = folders[fold];
            bool isGen = fold.Equals("general", StringComparison.OrdinalIgnoreCase);
            if (!isGen && !rec.Marked) continue;
            string place = "";
            if (!isGen)
            {
                oldC.TryGetValue(fold, out var prevC);
                if (prevC is not null && prevC.Place != "" && (limited || (rec.Msha != "" && prevC.Msha == rec.Msha)))
                { place = prevC.Place; if (limited) rec.Msha = prevC.Msha; }
                else
                {
                    string ini = bodies.TryGetValue(fold + "/place-id.ini", out var pb) ? pb
                               : await Get($"https://raw.githubusercontent.com/{Repo}/{reff}/{fold}/place-id.ini");
                    var mi = Regex.Match(ini, "(\\d{6,})");
                    if (mi.Success) place = mi.Groups[1].Value;
                }
                if (place == "") continue;
            }
            string dirN = Safe(fold);
            string dir = Path.Combine(root, dirN);
            try { Directory.CreateDirectory(dir); } catch { }
            if (!Directory.Exists(dir)) continue;
            var kept = new StringBuilder();
            foreach (var (fname, shaIn) in rec.Files)
            {
                string sha = shaIn;
                string key = fold + "/" + fname;
                oldS.TryGetValue(key, out var prev);
                if (prev is not null && sha != "" && prev.Sha == sha && File.Exists(Path.Combine(dir, prev.Disk)))
                {
                    kept.Append("S").Append(SEP).Append(fold).Append(SEP).Append(fname).Append(SEP).Append(prev.Who).Append(SEP).Append(prev.Cnt).Append(SEP).Append(prev.Disk).Append(SEP).Append(sha).Append('\n');
                    n++; same++;
                    continue;
                }
                string body = bodies.TryGetValue(key, out var fb) ? fb : await Get($"https://raw.githubusercontent.com/{Repo}/{reff}/{fold}/{fname}");
                if (body == "")
                {
                    if (lastStatus != 404 && prev is not null && File.Exists(Path.Combine(dir, prev.Disk)))
                    {
                        kept.Append("S").Append(SEP).Append(fold).Append(SEP).Append(fname).Append(SEP).Append(prev.Who).Append(SEP).Append(prev.Cnt).Append(SEP).Append(prev.Disk).Append(SEP).Append(prev.Sha).Append('\n');
                        n++;
                    }
                    continue;
                }
                if (sha == "") sha = BlobSha(body);
                int cnt = CountPairs(body, out string txt, out string who);
                if (cnt == 0) continue;
                string safe = Safe(fname);
                bool wasSame = prev is not null && prev.Sha != "" && prev.Sha == sha && prev.Who == who && File.Exists(Path.Combine(dir, safe));
                try { File.WriteAllText(Path.Combine(dir, safe), txt, new UTF8Encoding(false)); } catch { continue; }
                kept.Append("S").Append(SEP).Append(fold).Append(SEP).Append(fname).Append(SEP).Append(who).Append(SEP).Append(cnt).Append(SEP).Append(safe).Append(SEP).Append(sha).Append('\n');
                n++;
                if (wasSame) same++; else nNew++;
            }
            if (kept.Length == 0) continue;
            var target = isGen ? gen : game;
            target.Append("C").Append(SEP).Append(fold).Append(SEP).Append(place).Append(SEP).Append(dirN).Append(SEP).Append(rec.Msha).Append('\n').Append(kept);
        }
        if (n == 0) return "ERR" + SEP + (limited ? "github api rate limit - nothing cached to refresh" : "the repository has no usable sets");
        try { File.WriteAllText(Path.Combine(root, "index.txt"), "H" + SEP + reff + SEP + (limited ? "0" : "1") + "\n" + gen + game, new UTF8Encoding(false)); } catch { }
        return "OK" + SEP + n + SEP + nNew + SEP + same + SEP + note;
    }
}
