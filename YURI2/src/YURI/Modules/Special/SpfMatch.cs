using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using Yuri.Core;
using Yuri.Platform;
using Yuri.Shell.Hub;

namespace Yuri.Modules.Special;

/// <summary>
/// AUTO-REGION FINDER and BETTER MATCHMAKING. After a join the region lookup
/// measures the server's distance; too far and the client is closed and the
/// place relaunched by its roblox:// link — a named public instance this
/// session already measured as close when BETTER MATCHMAKING has one, else
/// the next public server from the list — up to fifteen times, only within
/// ninety seconds of the join, 2.5 s between attempts. A hunt started from
/// the GAME or PRIVATE SERVER field does the same until it lands close.
/// SPFTarget / SPFServersFetch / SPFPickServer / SPFLaunch / SPFJoinOnce /
/// SPFHuntStart / SPFHuntStop / SPFMatchTick / SPFMatchReroll.
/// </summary>
public static class SpfMatch
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_ACC = 0xFFFB7185;
    public const int MM_MAX = 15, MM_WINDOW = 90000, MM_COOL = 2500, MM_IDLE = 60000;
    public static int MM_KM = 3500;                                       // [special] radiuskm
    static string _huntPl = "", _huntCode = "", _huntShare = "", _tgtShare = "";
    static long _mmAt;
    static string _srvFor = ""; static readonly List<string> _srvList = new(); static readonly HashSet<string> _srvTried = new();
    static bool _srvBusy;
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    static SpfMatch() { Http.DefaultRequestHeaders.UserAgent.ParseAdd("YURI"); }

    public static bool MatchOK() => SpfEngine.SrvKm >= 0 && SpfEngine.SrvKm <= MM_KM;
    public static string ShareOf(string src)
    {
        if (!Regex.IsMatch(src, "roblox\\.com/share", RegexOptions.IgnoreCase)) return "";
        var m = Regex.Match(src, "[?&]code=([A-Za-z0-9_\\-]+)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : "";
    }

    /// <summary>SPFTarget: the place (and private code) the reroll goes back to.</summary>
    static bool Target(out string place, out string code)
    {
        place = ""; code = ""; _tgtShare = "";
        if (Spf.Hunting && (_huntPl != "" || _huntShare != "")) { place = _huntPl; code = _huntCode; _tgtShare = _huntShare; return true; }
        if (Spf.MmPriv != "")
        {
            string shr = ShareOf(Spf.MmPriv);
            if (shr != "") { _tgtShare = shr; return true; }
            var m = Regex.Match(Spf.MmPriv, "(?:places|games)/(\\d+)", RegexOptions.IgnoreCase); if (m.Success) place = m.Groups[1].Value;
            var c = Regex.Match(Spf.MmPriv, "(?:privateServerLinkCode|linkCode)=([A-Za-z0-9_\\-]+)", RegexOptions.IgnoreCase); if (c.Success) code = c.Groups[1].Value;
            if (place != "" && code != "") return true;
        }
        if (Spf.MmLink != "")
        {
            var m = Regex.Match(Spf.MmLink, "(?:places|games)/(\\d+)", RegexOptions.IgnoreCase);
            if (m.Success) place = m.Groups[1].Value;
            else { var b = Regex.Match(Spf.MmLink, "^\\s*(\\d{5,})\\s*$"); if (b.Success) place = b.Groups[1].Value; }
            if (place != "") return true;
        }
        place = Spf.Place;
        return place != "";
    }

    /// <summary>SPFServersFetch(place): the public server list, once per place.</summary>
    static void ServersFetch(string place)
    {
        if (place == "" || place == _srvFor || _srvBusy || SpfEngine.NoNetwork) return;
        _srvFor = place; _srvList.Clear(); _srvTried.Clear(); _srvBusy = true;
        _ = Task.Run(async () =>
        {
            var ids = new List<string>();
            try
            {
                using var res = await Http.GetAsync("https://games.roblox.com/v1/games/" + place + "/servers/Public?limit=100&sortOrder=Desc");
                if (res.IsSuccessStatusCode)
                {
                    string body = await res.Content.ReadAsStringAsync();
                    foreach (Match m in Regex.Matches(body, "\"id\"\\s*:\\s*\"([0-9a-fA-F\\-]{30,})\"")) { ids.Add(m.Groups[1].Value); if (ids.Count >= 60) break; }
                }
            }
            catch { }
            Dispatcher.UIThread.Post(() => { _srvBusy = false; if (_srvFor == place) { _srvList.Clear(); _srvList.AddRange(ids); } HubSurface.Live?.Tim(Pace.TICK_A); });
        });
    }
    static string NextServer()
    {
        foreach (var id in _srvList) if (id != "" && _srvTried.Add(id)) return id;
        return "";
    }
    /// <summary>SPFPickServer(pl): a server this session measured as close, else one not yet measured.</summary>
    static string PickServer(string pl)
    {
        if (!Spf.Odds || pl == "") return "";
        string best = ""; int bestKm = 999999;
        foreach (var id in _srvList)
        {
            if (id == "") continue;
            int km = SpfEngine.SrvSeenKm(pl, id);
            if (km < 0) continue;
            if (km <= MM_KM && km < bestKm) { best = id; bestKm = km; }
        }
        if (best != "") return best;
        foreach (var id in _srvList) if (id != "" && SpfEngine.SrvSeenKm(pl, id) < 0) return id;
        return "";
    }

    /// <summary>SPFLaunch: close the client, relaunch by link.</summary>
    static bool Launch(string pl, string code, string shr, string job = "")
    {
        if (SpfEngine.RbxCount(true) > 1)
        {
            Spf.MmNote = "several clients open - a reroll would close them all";
            Spf.Say("REROLL SKIPPED - IT WOULD CLOSE YOUR OTHER ROBLOX CLIENTS", AMBER);
            return false;
        }
        Roblox.Kill(300);
        if (Roblox.IsRunningNow()) { Spf.MmNote = "could not close roblox - try running as admin"; return false; }
        SpfEngine.PurgeSessions();
        SpfEngine.ClearGame();
        string uri;
        if (shr != "") uri = "roblox://navigation/share_links?code=" + shr + "&type=Server";
        else
        {
            uri = "roblox://experiences/start?placeId=" + pl;
            if (code != "") uri += "&linkCode=" + code;
            else if (job != "") uri += "&gameInstanceId=" + job;
        }
        Roblox.OpenUrl(uri);
        return true;
    }

    /// <summary>SPFJoinOnce: JOIN with the finder off — one launch, a picked server when BETTER MATCHMAKING knows one.</summary>
    static void JoinOnce(string pl, string cd, string shr)
    {
        Spf.Hunting = false; _huntPl = ""; _huntCode = ""; _huntShare = ""; Spf.HuntWhich = 0;
        Spf.MmTries = 0; _mmAt = 0; _srvTried.Clear();
        string job = cd == "" && shr == "" ? PickServer(pl) : "";
        if (!Launch(pl, cd, shr, job))
        {
            Spf.Say("COULD NOT CLOSE ROBLOX - TRY RUNNING AS ADMIN", C_ACC);
            SpfEngine.Sync();
            return;
        }
        Spf.MmNote = job != "" ? "joined a picked server - no reroll" : "joined - auto-region finder off, no reroll";
        Spf.Say("JOINING" + (shr != "" ? " PRIVATE SERVER" : pl != "" ? " PLACE " + pl : "") + (job != "" ? "  -  PICKED SERVER" : ""), C_ON);
        SpfEngine.Sync();
    }

    /// <summary>SPFHuntStart(which): 1 the GAME link, 2 the PRIVATE SERVER link.</summary>
    public static void HuntStart(int which)
    {
        string src = which == 2 ? Spf.MmPriv : Spf.MmLink;
        if (src == "") { Spf.Say((which == 2 ? "PRIVATE SERVER" : "GAME") + " LINK IS EMPTY", AMBER); return; }
        string pl = "", cd = "", shr = "";
        if (which == 2) shr = ShareOf(src);
        if (shr == "")
        {
            var m = Regex.Match(src, "(?:places|games)/(\\d+)", RegexOptions.IgnoreCase);
            if (m.Success) pl = m.Groups[1].Value;
            else { var b = Regex.Match(src, "^\\s*(\\d{5,})\\s*$"); if (b.Success) pl = b.Groups[1].Value; }
            if (which == 2) { var c = Regex.Match(src, "(?:privateServerLinkCode|linkCode)=([A-Za-z0-9_\\-]+)", RegexOptions.IgnoreCase); if (c.Success) cd = c.Groups[1].Value; }
            if (pl == "") { Spf.Say("COULD NOT READ A PLACE ID FROM THAT LINK", C_ACC); return; }
            if (which == 2 && cd == "") { Spf.Say("THAT LINK HAS NO PRIVATE SERVER CODE", C_ACC); return; }
        }
        if (!Spf.Match) { JoinOnce(pl, cd, shr); return; }
        Spf.Hunting = true; _huntPl = pl; _huntCode = cd; _huntShare = shr; Spf.HuntWhich = which;
        Spf.MmTries = 0; _mmAt = 0; Spf.MmNote = "starting";
        _srvFor = ""; _srvList.Clear(); _srvTried.Clear();
        if (pl != "") ServersFetch(pl);
        Spf.Say("HUNTING A " + (Spf.HomeRegion != "" ? Spf.HomeRegion.ToUpperInvariant() : "NEARBY") + " SERVER", C_ON);
        SpfEngine.Sync();
        Reroll();
    }
    public static void HuntStop(string msg = "")
    {
        Spf.Hunting = false; _huntPl = ""; _huntCode = ""; _huntShare = ""; Spf.HuntWhich = 0;
        if (msg != "") Spf.MmNote = msg;
        SpfEngine.Sync();
    }

    /// <summary>SPFMatchTick(): called from the engine's tick.</summary>
    public static void Tick()
    {
        if (Spf.Odds && Target(out var tp0, out _) && tp0 != "") ServersFetch(tp0);
        if (!Spf.Match && !Spf.Hunting) return;
        if (Spf.Hunting && _mmAt != 0 && Clock.Tick - _mmAt > MM_IDLE) { HuntStop("timed out waiting for roblox"); return; }
        if (Target(out var tp, out _) && tp != "") ServersFetch(tp);
        if (!Spf.InGame || SpfEngine.SrvKm < 0) return;
        if (MatchOK())
        {
            if (Spf.MmTries != 0 || Spf.MmNote == "" || Spf.MmNote.Contains("reroll"))
            {
                Spf.Say("IN REGION - " + Spf.RegionText + "  (" + SpfEngine.SrvKm + " km)", C_ON);
                Spf.MmTries = 0;
            }
            if (Spf.Hunting) HuntStop("landed in " + Spf.RegionText + " - " + SpfEngine.SrvKm + " km");
            else Spf.MmNote = "matched - " + SpfEngine.SrvKm + " km away";
            return;
        }
        if (Spf.MmTries >= MM_MAX)
        {
            if (Spf.Hunting) HuntStop("gave up after " + MM_MAX + " tries");
            else Spf.MmNote = "gave up after " + MM_MAX + " tries";
            return;
        }
        if (!Spf.Hunting && (SpfEngine.JoinAt == 0 || Clock.Tick - SpfEngine.JoinAt > MM_WINDOW)) { Spf.MmNote = SpfEngine.SrvKm + " km away, but you are settled in"; return; }
        if (_mmAt != 0 && Clock.Tick - _mmAt < MM_COOL) return;
        Reroll();
    }
    static void Reroll()
    {
        if (!Target(out var pl, out var code) && _tgtShare == "") { Spf.MmNote = "no place to rejoin - paste a game link"; return; }
        _mmAt = Clock.Tick; Spf.MmTries += 1;
        Spf.MmNote = "rerolling " + Spf.MmTries + "/" + MM_MAX + (SpfEngine.SrvKm >= 0 ? " - " + SpfEngine.SrvKm + " km away" : "");
        Spf.Say("REROLLING SERVER " + Spf.MmTries + "/" + MM_MAX, AMBER);
        string job = "";
        if (code == "") { job = PickServer(pl); if (job == "") job = NextServer(); }
        if (job != "") _srvTried.Add(job);
        Launch(pl, code, _tgtShare, job);
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
}
