using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Yuri.Core;
using Yuri.Platform;
using Yuri.Gfx;
using Yuri.Shell.Hub;

namespace Yuri.Modules.Special;

public sealed class Account { public string Nm = "", Id = "", Ck = ""; }
/// <summary>A row in the GROUPS or CREATED list: name, a sub-line, an icon id, a link.</summary>
public sealed record AcctRow(string N, string S, string Ic, string Lk);
public sealed class Profile
{
    public string Desc = "", Disp = "", Nm = "", Fr = "", Fol = "", Fwg = "", Loc = ""; public int Pres;
    public List<AcctRow> Groups = new(), Made = new(); public string GroupsCode = "200", MadeCode = "200"; public long At;
}

/// <summary>
/// ACCOUNTS (Acct* and SPFAcctView's data): saved .ROBLOSECURITY cookies,
/// each sealed with the Windows user's key (CryptProtectData) so the file is
/// useless to anyone else, kept in YURI\config\accounts.dat. Add pastes a
/// cookie from the clipboard; the name, id, avatar and full profile (about,
/// friend counts, groups, created games) are fetched with the cookie set as
/// a header. LAUNCH mints an authentication ticket from the cookie and hands
/// it to a fresh client through the roblox-player protocol, so a client can
/// be opened already signed into any account. Windows only for the sealed
/// store and the launch; on macOS the store is plain and the launch is
/// disabled. AcctLoad / Store / AddFromClip / Del / NameFetch / ProfFetch /
/// ParseTarget / Launch.
/// </summary>
public static class SpfAcct
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_ACC = 0xFFFB7185;
    public static readonly List<Account> Acct = new();
    public static int Sel = 1; public static string Tgt = "";
    public static int Tab = 1; public static double TabT = 1, PaneT; public static int Pane; public static long TabAt;
    public static long At, AcctPre;
    static readonly Dictionary<string, Profile> Prof = new();
    static readonly Dictionary<string, long> Try = new();
    static readonly Dictionary<string, long> KillAt = new();
    public static double AvYaw, AvPitch; public static bool AvAuto = true, AvDrag;
    static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false, AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(15) };
    static SpfAcct() { Http.DefaultRequestHeaders.UserAgent.ParseAdd("Roblox/WinInet"); }
    public static string File => Path.Combine(Paths.Cfg, "accounts.dat");
    static string CacheDir => Path.Combine(Paths.Root, "cache", "acct");
    public static bool NoNetwork;                                        // set by the test harness
    public const int ACCT_MAX = 8;

    // ---- DPAPI ----
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool CryptProtectData(ref DATA_BLOB pIn, string? name, IntPtr optE, IntPtr res, IntPtr prompt, uint flags, ref DATA_BLOB pOut);
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool CryptUnprotectData(ref DATA_BLOB pIn, IntPtr desc, IntPtr optE, IntPtr res, IntPtr prompt, uint flags, ref DATA_BLOB pOut);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct DATA_BLOB { public uint cb; public IntPtr pb; }
    [SupportedOSPlatform("windows")]
    static string Protect(string plain)
    {
        if (plain == "") return "";
        var raw = Encoding.UTF8.GetBytes(plain);
        var h = GCHandle.Alloc(raw, GCHandleType.Pinned);
        try
        {
            var pin = new DATA_BLOB { cb = (uint)raw.Length, pb = h.AddrOfPinnedObject() };
            var pout = new DATA_BLOB();
            if (!CryptProtectData(ref pin, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref pout)) return "";
            var outBytes = new byte[pout.cb];
            Marshal.Copy(pout.pb, outBytes, 0, (int)pout.cb);
            LocalFree(pout.pb);
            return Convert.ToBase64String(outBytes);
        }
        finally { h.Free(); }
    }
    [SupportedOSPlatform("windows")]
    static string Unprotect(string b64)
    {
        byte[] raw; try { raw = Convert.FromBase64String(b64); } catch { return ""; }
        if (raw.Length == 0) return "";
        var h = GCHandle.Alloc(raw, GCHandleType.Pinned);
        try
        {
            var pin = new DATA_BLOB { cb = (uint)raw.Length, pb = h.AddrOfPinnedObject() };
            var pout = new DATA_BLOB();
            if (!CryptUnprotectData(ref pin, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref pout)) return "";
            var outBytes = new byte[pout.cb];
            Marshal.Copy(pout.pb, outBytes, 0, (int)pout.cb);
            LocalFree(pout.pb);
            return Encoding.UTF8.GetString(outBytes);
        }
        finally { h.Free(); }
    }
    // On macOS there is no per-user DPAPI; the cookie is stored base64 in a 0600 file the user owns.
    static string Seal(string plain) => Os.IsWin ? Protect(plain) : "b64:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(plain));
    static string Unseal(string blob)
    {
        if (blob.StartsWith("b64:")) { try { return Encoding.UTF8.GetString(Convert.FromBase64String(blob[4..])); } catch { return ""; } }
        return Os.IsWin ? Unprotect(blob) : "";
    }

    // ---- AcctLoad / AcctStore ----
    public static void Load()
    {
        Acct.Clear();
        if (!System.IO.File.Exists(File)) return;
        try
        {
            foreach (var raw in System.IO.File.ReadAllText(File, Encoding.UTF8).Split('\n'))
            {
                var ln = raw.TrimEnd('\r');
                if (ln.Trim() == "") continue;
                var p = ln.Split('|', 3);
                if (p.Length < 2) continue;
                string uid, blob;
                if (p.Length >= 3) { uid = p[1]; blob = p[2]; } else { uid = ""; blob = p[1]; }
                string ck = Unseal(blob);
                if (ck == "") continue;                                        // a different user, or tampered
                Acct.Add(new Account { Nm = p[0], Id = uid, Ck = ck });
                if (Acct.Count >= ACCT_MAX) break;
            }
        }
        catch { }
    }
    public static void Store()
    {
        var sb = new StringBuilder();
        foreach (var a in Acct)
        {
            string e = Seal(a.Ck);
            if (e != "") sb.Append(a.Nm.Replace("|", " ")).Append('|').Append(a.Id).Append('|').Append(e).Append('\n');
        }
        try { Directory.CreateDirectory(Paths.Cfg); System.IO.File.WriteAllText(File, sb.ToString(), new UTF8Encoding(false)); } catch { }
    }
    /// <summary>AcctAddFromClip(text): the .ROBLOSECURITY cookie out of a pasted string.</summary>
    public static bool AddFromClip(string c)
    {
        c = c.Trim();
        var m = Regex.Match(c, "\\.ROBLOSECURITY\\s*=\\s*([^\\s;]+)", RegexOptions.IgnoreCase);
        if (m.Success) c = m.Groups[1].Value;
        if (c.Length < 100 || !c.Contains("WARNING")) { Spf.Say("CLIPBOARD DOES NOT LOOK LIKE A .ROBLOSECURITY COOKIE", C_ACC); return false; }
        if (Acct.Any(a => a.Ck == c)) { Spf.Say("THAT ACCOUNT IS ALREADY SAVED", AMBER); return false; }
        if (Acct.Count >= ACCT_MAX) { Spf.Say("ACCOUNT LIST IS FULL (" + ACCT_MAX + ")", AMBER); return false; }
        Acct.Add(new Account { Nm = "account " + (Acct.Count + 1), Id = "", Ck = c });
        Sel = Acct.Count;
        Store();
        Spf.Say("ACCOUNT SAVED - NOW CLEAR YOUR CLIPBOARD", C_ON);
        NameFetch(Acct.Count);
        return true;
    }
    public static void Del(int i)
    {
        if (i < 1 || i > Acct.Count) return;
        KillAt[Acct[i - 1].Ck] = Clock.Tick;
        Acct.RemoveAt(i - 1);
        Store();
        Sel = Math.Clamp(i, 1, Math.Max(1, Acct.Count));
        Spf.Say("ACCOUNT REMOVED", 0xFFC7CBE0);
    }
    public static double KillT(int i, long now) => 0.0;                  // the fade is cosmetic; the row is gone from the list at once here

    static HttpRequestMessage Req(string url, string? cookie = null)
    {
        var r = new HttpRequestMessage(HttpMethod.Get, url);
        if (cookie is not null) r.Headers.TryAddWithoutValidation("Cookie", ".ROBLOSECURITY=" + cookie);
        return r;
    }
    /// <summary>How many accounts have a username resolved - the loader waits on this rather than on a timer.</summary>
    public static int ResolvedN() { int n = 0; foreach (var a in Acct) if (a.Nm != "") n++; return n; }
    /// <summary>AcctNameFetch(i): resolve the account's username and id from its cookie, then its profile.</summary>
    public static void NameFetch(int i)
    {
        if (i < 1 || i > Acct.Count || NoNetwork) return;
        string k = "n" + i;
        if (Try.TryGetValue(k, out var at) && Clock.Tick - at < 15000) return;
        Try[k] = Clock.Tick;
        string ck = Acct[i - 1].Ck;
        _ = Task.Run(async () =>
        {
            string nm = "", uid = "";
            try
            {
                using var res = await Http.SendAsync(Req("https://users.roblox.com/v1/users/authenticated", ck));
                if (res.IsSuccessStatusCode)
                {
                    string j = await res.Content.ReadAsStringAsync();
                    var mn = Regex.Match(j, "\"name\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""); if (mn.Success) nm = Unesc(mn.Groups[1].Value);
                    var mi = Regex.Match(j, "\"id\"\\s*:\\s*(\\d+)"); if (mi.Success) uid = mi.Groups[1].Value;
                }
            }
            catch { }
            Dispatcher.UIThread.Post(() =>
            {
                if (i < 1 || i > Acct.Count) return;
                bool ch = false;
                if (nm != "") { Acct[i - 1].Nm = nm.Replace("|", " "); ch = true; }
                if (uid != "") { Acct[i - 1].Id = uid; ch = true; }
                if (ch) Store();
                ProfFetch(i);
                Poke();
            });
        });
    }
    static readonly string[] Grades = { "1000" };
    public static Profile? GetProf(string id) => id != "" && Prof.TryGetValue(id, out var p) ? p : null;
    /// <summary>AcctProfFetch(i): the full profile — description, counts, groups, created games, avatar images.</summary>
    public static void ProfFetch(int i, bool force = false)
    {
        if (i < 1 || i > Acct.Count || NoNetwork) return;
        var a = Acct[i - 1];
        if (a.Id == "") { NameFetch(i); return; }
        if (!force && Prof.TryGetValue(a.Id, out var have) && Clock.Tick - have.At < 45000) return;
        string k = "p" + a.Id;
        if (!force && Try.TryGetValue(k, out var at) && Clock.Tick - at < 15000) return;
        Try[k] = Clock.Tick;
        string ck = a.Ck, uid = a.Id;
        _ = Task.Run(async () =>
        {
            var pr = new Profile { At = Clock.Tick };
            try
            {
                string j = await GetT("https://users.roblox.com/v1/users/" + uid);
                var m = Regex.Match(j, "\"description\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""); if (m.Success) pr.Desc = Unesc(m.Groups[1].Value);
                m = Regex.Match(j, "\"displayName\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""); if (m.Success) pr.Disp = Unesc(m.Groups[1].Value);
                m = Regex.Match(j, "\"name\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""); if (m.Success) pr.Nm = Unesc(m.Groups[1].Value);
                pr.Fr = await Count("https://friends.roblox.com/v1/users/" + uid + "/friends/count");
                pr.Fol = await Count("https://friends.roblox.com/v1/users/" + uid + "/followers/count");
                pr.Fwg = await Count("https://friends.roblox.com/v1/users/" + uid + "/followings/count");
                string pj = await GetT("https://presence.roblox.com/v1/presence/users", null, "{\"userIds\":[" + uid + "]}");
                var pm = Regex.Match(pj, "\"userPresenceType\"\\s*:\\s*(\\d+)"); if (pm.Success) pr.Pres = int.Parse(pm.Groups[1].Value);
                await Groups(pr, uid);
                await Made(pr, uid, ck);
                await AvatarImages(uid);
            }
            catch { }
            Dispatcher.UIThread.Post(() => { Prof[uid] = pr; Poke(); });
        });
    }
    static async Task<string> GetT(string url, string? ck = null, string? postJson = null)
    {
        try
        {
            HttpResponseMessage res;
            if (postJson is not null)
            {
                var rq = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(postJson, Encoding.UTF8, "application/json") };
                if (ck is not null) rq.Headers.TryAddWithoutValidation("Cookie", ".ROBLOSECURITY=" + ck);
                res = await Http.SendAsync(rq);
            }
            else res = await Http.SendAsync(Req(url, ck));
            return res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync() : "";
        }
        catch { return ""; }
    }
    static async Task<string> Count(string url) { var m = Regex.Match(await GetT(url), "\"count\"\\s*:\\s*(\\d+)"); return m.Success ? m.Groups[1].Value : ""; }
    static async Task Groups(Profile pr, string uid)
    {
        string j = await GetT("https://groups.roblox.com/v1/users/" + uid + "/groups/roles");
        if (j == "") { pr.GroupsCode = "0"; return; }
        foreach (Match m in Regex.Matches(j, "\"group\"\\s*:\\s*\\{[^}]*?\"id\"\\s*:\\s*(\\d+)[^}]*?\"name\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"[^}]*?\"memberCount\"\\s*:\\s*(\\d+)"))
        {
            var role = Regex.Match(j[m.Index..], "\"role\"\\s*:\\s*\\{[^}]*?\"name\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            pr.Groups.Add(new AcctRow(Unesc(m.Groups[2].Value), role.Success ? Unesc(role.Groups[1].Value) : "", m.Groups[1].Value, ""));
            if (pr.Groups.Count >= 40) break;
        }
    }
    static async Task Made(Profile pr, string uid, string ck)
    {
        string j = await GetT("https://games.roblox.com/v2/users/" + uid + "/games?accessFilter=Public&limit=50&sortOrder=Asc", ck);
        if (j == "") { pr.MadeCode = "0"; return; }
        foreach (Match m in Regex.Matches(j, "\\{\"id\"\\s*:\\s*(\\d+)\\s*,\\s*\"name\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""))
        {
            var vis = Regex.Match(j[m.Index..], "\"placeVisits\"\\s*:\\s*(\\d+)");
            pr.Made.Add(new AcctRow(Unesc(m.Groups[2].Value), vis.Success ? vis.Groups[1].Value : "", m.Groups[1].Value, ""));
            if (pr.Made.Count >= 40) break;
        }
    }
    static async Task AvatarImages(string uid)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            await Grab("https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds=" + uid + "&size=150x150&format=Png&isCircular=false", HeadPath(uid));
            await Grab("https://thumbnails.roblox.com/v1/users/avatar?userIds=" + uid + "&size=420x420&format=Png&isCircular=false", BodyPath(uid));
        }
        catch { }
    }
    static async Task Grab(string listUrl, string dst)
    {
        if (System.IO.File.Exists(dst) && (DateTime.UtcNow - System.IO.File.GetLastWriteTimeUtc(dst)).TotalHours < 24) return;
        string j = await GetT(listUrl);
        var m = Regex.Match(j, "\"imageUrl\"\\s*:\\s*\"([^\"]+)\"");
        if (!m.Success) return;
        try { var bytes = await Http.GetByteArrayAsync(m.Groups[1].Value.Replace("\\/", "/")); await System.IO.File.WriteAllBytesAsync(dst, bytes); } catch { }
    }
    static string HeadPath(string id) => Path.Combine(CacheDir, id + "_h.png");
    static string BodyPath(string id) => Path.Combine(CacheDir, id + "_f.png");
    public static Bitmap? Head(string id) { if (id == "") return null; try { return Img.File(HeadPath(id)); } catch { return null; } }
    public static Bitmap? Body(string id) { if (id == "") return null; try { return Img.File(BodyPath(id)); } catch { return null; } }
    public static Bitmap? ListIcon(string kind, string id)
    {
        if (id == "") return null;
        string p = Path.Combine(CacheDir, "ic_" + kind + "_" + id + ".png");
        try { return Img.File(p); } catch { return null; }
    }

    static string Unesc(string s) => FastFlags.Ffm.Unesc(s);
    public static string PresName(int t) => t == 2 ? "in game" : t == 3 ? "in studio" : t == 1 ? "online" : "offline";
    public static uint PresCol(int t) => t == 2 ? C_ON : t == 3 ? 0xFFA78BFAu : t == 1 ? 0xFF38BDF8u : 0xFF6E7590u;

    // ---- the launch target ----
    public static bool ParseTarget(string src, out string pl, out string job, out string code, out string shr)
    {
        pl = ""; job = ""; code = ""; shr = "";
        src = src.Trim();
        if (src == "") return false;
        string s2 = SpfMatch.ShareOf(src);
        if (s2 != "") { shr = s2; return true; }
        var m = Regex.Match(src, "^\\s*(\\d{5,})[\\s/|,]+([0-9a-fA-F][0-9a-fA-F\\-]{15,})\\s*$");
        if (m.Success) { pl = m.Groups[1].Value; job = m.Groups[2].Value; return true; }
        m = Regex.Match(src, "gameInstanceId=([0-9a-fA-F\\-]{8,})", RegexOptions.IgnoreCase); if (m.Success) job = m.Groups[1].Value;
        m = Regex.Match(src, "(?:privateServerLinkCode|linkCode|accessCode)=([A-Za-z0-9_\\-]+)", RegexOptions.IgnoreCase); if (m.Success) code = m.Groups[1].Value;
        m = Regex.Match(src, "placeId=(\\d+)", RegexOptions.IgnoreCase);
        if (m.Success) pl = m.Groups[1].Value;
        else { m = Regex.Match(src, "(?:places|games)/(\\d+)", RegexOptions.IgnoreCase); if (m.Success) pl = m.Groups[1].Value; else { m = Regex.Match(src, "^\\s*(\\d{5,})\\s*$"); if (m.Success) pl = m.Groups[1].Value; } }
        return pl != "";
    }
    public static string TargetDesc(string src)
    {
        if (src.Trim() == "") return "opens roblox on its home screen, signed in";
        if (!ParseTarget(src, out var pl, out var job, out var code, out var shr))
            return shr != "" ? "share link - not usable per account" : "not recognised as a place or server";
        if (code != "") return "private server on place " + pl;
        if (job != "") return "specific server on place " + pl;
        return "place " + pl + "  \u00B7  any public server";
    }
    /// <summary>AcctLaunch(i): mint a ticket from the cookie and open a client already signed in as this account.</summary>
    public static void Launch(int i)
    {
        if (i < 1 || i > Acct.Count) { Spf.Say("NO ACCOUNT SELECTED", AMBER); return; }
        if (!Os.IsWin) { Spf.Say("LAUNCH-AS-ACCOUNT IS A WINDOWS FEATURE - THE roblox-player PROTOCOL AND THE SINGLETON ARE WINDOWS-ONLY", AMBER); return; }
        if (!Spf.Multi && SpfEngineCount() > 0) { Spf.Say("ROBLOX IS ALREADY RUNNING - TURN MULTI-ROBLOX INSTANCES ON", C_ACC); return; }
        string src = Tgt.Trim();
        string pl = "", job = "", code = "", shr = "";
        if (src != "")
        {
            if (!ParseTarget(src, out pl, out job, out code, out shr)) { Spf.Say("COULD NOT READ THAT TARGET - EMPTY THE BOX TO JUST OPEN THE APP", AMBER); return; }
            if (shr != "") { Spf.Say("SHARE LINKS CANNOT BE AIMED AT AN ACCOUNT - USE THE PLACE ID AND CODE", AMBER); return; }
        }
        if (At != 0 && Clock.Tick - At < 20000) { Spf.Say("A LAUNCH IS ALREADY IN FLIGHT", AMBER); return; }
        AcctPre = SpfEngineCount();
        At = Clock.Tick;
        Spf.Say("LAUNCHING " + Acct[i - 1].Nm + "...", C_ON);
        string ck = Acct[i - 1].Ck;
        _ = Task.Run(async () =>
        {
            string err = await DoLaunch(ck, pl, job, code);
            Dispatcher.UIThread.Post(() =>
            {
                At = 0;
                if (err == "") { Spf.Say("TICKET ACCEPTED - CLIENT STARTING", C_ON); Later(9000, Verify); }
                else Spf.Say("LAUNCH FAILED - " + err.ToUpperInvariant(), C_ACC);
                Poke();
            });
        });
    }
    static async Task<string> DoLaunch(string ck, string pl, string job, string code)
    {
        string csrf;
        try
        {
            var rq = new HttpRequestMessage(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket/") { Content = new StringContent("", Encoding.UTF8, "application/json") };
            rq.Headers.TryAddWithoutValidation("Referer", "https://www.roblox.com/");
            rq.Headers.TryAddWithoutValidation("Cookie", ".ROBLOSECURITY=" + ck);
            using var r1 = await Http.SendAsync(rq);
            csrf = r1.Headers.TryGetValues("x-csrf-token", out var v) ? v.First() : "";
        }
        catch (Exception e) { return e.Message.Replace("|", " "); }
        if (csrf == "") return "no csrf token - cookie is probably expired";
        string ticket;
        try
        {
            var rq = new HttpRequestMessage(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket/") { Content = new StringContent("", Encoding.UTF8, "application/json") };
            rq.Headers.TryAddWithoutValidation("Referer", "https://www.roblox.com/");
            rq.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
            rq.Headers.TryAddWithoutValidation("Cookie", ".ROBLOSECURITY=" + ck);
            using var r2 = await Http.SendAsync(rq);
            ticket = r2.Headers.TryGetValues("rbx-authentication-ticket", out var v) ? v.First() : "";
            if (ticket == "") return "roblox refused a ticket (status " + (int)r2.StatusCode + ")";
        }
        catch (Exception e) { return e.Message.Replace("|", " "); }
        long btid = Random.Shared.NextInt64(100000000, 999999999);
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string uri;
        if (pl == "")
            uri = "roblox-player:1+launchmode:app+gameinfo:" + ticket + "+launchtime:" + ts + "+browsertrackerid:" + btid + "+robloxLocale:en_us+gameLocale:en_us+channel:";
        else
        {
            string req = code != "" ? "RequestPrivateGame" : job != "" ? "RequestGameJob" : "RequestGame";
            string launcher = "https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=" + req + "&browserTrackerId=" + btid + "&placeId=" + pl
                + (job != "" ? "&gameId=" + job : "") + (code != "" ? "&accessCode=" + code : "") + "&isPartyLeader=false&isTeleport=false&isPlayTogetherGame=false";
            var enc = new StringBuilder();
            foreach (char c in launcher) enc.Append(Regex.IsMatch(c.ToString(), "[A-Za-z0-9._~-]") ? c.ToString() : "%" + ((int)c).ToString("X2"));
            uri = "roblox-player:1+launchmode:play+gameinfo:" + ticket + "+launchtime:" + ts + "+placelauncherurl:" + enc + "+browsertrackerid:" + btid + "+robloxLocale:en_us+gameLocale:en_us+channel:";
        }
        try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); return ""; }
        catch (Exception e) { return "windows refused roblox-player: " + e.Message.Replace("|", " "); }
    }
    static void Verify()
    {
        int n = SpfEngineCount();
        if (n > AcctPre) Spf.Say("CLIENT RUNNING - " + n + " OPEN", C_ON);
        else Spf.Say("NO NEW CLIENT APPEARED - ROBLOX MAY HAVE REFUSED THE LAUNCH", C_ACC);
        Poke();
    }
    static int SpfEngineCount() { try { return Process.GetProcessesByName(Os.RobloxProcess).Length; } catch { return 0; } }
    static void Later(int ms, Action a)
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        t.Tick += (s, e) => { t.Stop(); a(); };
        t.Start();
    }
    static void Poke() => HubSurface.Live?.Tim(Pace.TICK_A);
    // ---- the field editor for the launch box (mode "at") ----
    public static string BufFor() => Tgt;
    public static void SyncBuf(string buf) => Tgt = buf;
}
