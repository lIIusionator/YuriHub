using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using Yuri.Core;
using Yuri.Gfx;
using Yuri.Platform;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;

namespace Yuri.Shell.Hub;

/// <summary>
/// The updater. The .ahk probed the repository copy of YURI.ahk for its
/// APP_VERSION and downloaded YURI.exe from the same place; YURI 2.0 is a
/// different program, so it reads YURI2.version (one line: the version)
/// and fetches the build for this platform - YURI2-win-x64.exe, or the
/// macOS zip - from the same branch. The card (HubGate kind 3), its copy,
/// glyph, buttons and phases are the .ahk's: 1 checking, 2 newer found,
/// 3 downloading, 4 up to date, 5 could not, 6 restarting. Nothing on disk
/// changes on any failure; the previous build stays beside the new one
/// as .old until the next launch removes it.
/// </summary>
public static class Upd
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399;
    public const string RepoBase = "https://raw.githubusercontent.com/lIIusionator/YuriHub/main/";
    public static string VersionUrl = RepoBase + "YURI2.version";
    /// <summary>
    /// The .ahk asks the repository for YURI.ahk and reads APP_VERSION out of it,
    /// which is why its check works: that file is the release. This build looked
    /// for a YURI2.version that nobody publishes, so every check answered 404.
    /// The dedicated file stays the first choice - it is one line and costs
    /// nothing - and the script itself is the fallback, so a repository that has
    /// only ever held the .ahk still answers.
    /// </summary>
    public static string FallbackUrl = RepoBase + "YURI.ahk";
    static readonly Regex AhkVer = new("^\\s*(?:global\\s+)?APP_VERSION\\s*:=\\s*\"([^\"]+)\"", RegexOptions.Multiline);
    public static string BuildUrl => RepoBase + (Os.IsWin ? "YURI2-win-x64.exe" : RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "YURI2-macOS-AppleSilicon.zip" : "YURI2-macOS-Intel.zip");
    public static int Phase; public static string Ver = "", Msg = "", Ttl = ""; public static long At; public static bool Auto, Pend;
    public static long GateAt, GateOut; public static int GateKind = 1;
    public static string RestartCmd = "";
    static string _tmp = "";
    static CancellationTokenSource? _cts;
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    public static Func<Task<string>>? ProbeOverride;                                   // tests: the version text the repository would answer
    public static Func<string, Task<bool>>? DownloadOverride;                            // tests: (path) -> wrote?
    public static bool NoNetwork;
    static readonly Func<HubSurface?> Hub = () => HubSurface.Live;

    public static bool GateUp => GateAt != 0;
    public static bool Animating => GateUp || Phase is 1 or 3 or 6;
    static void Poke() => Hub()?.Tim(Pace.TICK_A);

    // ---- the probe ----
    static int VerCompare(string a, string b)
    {
        var pa = a.Split('.').Select(p => int.TryParse(new string(p.TakeWhile(char.IsDigit).ToArray()), out var n) ? n : 0).ToArray();
        var pb = b.Split('.').Select(p => int.TryParse(new string(p.TakeWhile(char.IsDigit).ToArray()), out var n) ? n : 0).ToArray();
        for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++) { int x = i < pa.Length ? pa[i] : 0, y = i < pb.Length ? pb[i] : 0; if (x != y) return x < y ? -1 : 1; }
        return 0;
    }
    public static async Task<(bool ok, bool newer, string ver, string msg)> ProbeAsync(CancellationToken ct)
    {
        string txt;
        try
        {
            if (ProbeOverride is not null) txt = await ProbeOverride();
            else if (NoNetwork) return (false, false, "", "could not reach the repository");
            else
            {
                var a1 = await Ask(VersionUrl, ct);
                txt = a1.body;
                if (txt == "")
                {
                    var a2 = await Ask(FallbackUrl, ct);                          // the script itself, as the .ahk does
                    txt = a2.body;
                    // Name the status so a failure can be told apart: a 404 on
                    // both is a repository that carries neither file, a network
                    // error is something else entirely, and "could not check"
                    // alone never distinguished them.
                    if (txt == "") return (false, false, "", a2.status != 0 ? "the repository answered " + a2.status
                                                          : a1.status != 0 ? "the repository answered " + a1.status
                                                          : "could not reach the repository");
                }
            }
        }
        catch (OperationCanceledException) { return (false, false, "", "the repository did not answer"); }
        catch { return (false, false, "", "could not reach the repository"); }
        // A one-line version file, or APP_VERSION lifted out of the script.
        var am = AhkVer.Match(txt);
        string ver = am.Success ? am.Groups[1].Value.Trim() : txt.Split('\n')[0].Trim().TrimStart('v', 'V');
        if (ver == "" || !char.IsDigit(ver[0])) return (false, false, "", "the repository copy carries no version");
        return (true, VerCompare(ver, AppInfo.Version) > 0, ver, "");
    }
    /// <summary>One GET. "" when the repository will not serve it, so the caller can try the next source.</summary>
    static async Task<(string body, int status)> Ask(string url, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url + (url.Contains('?') ? "&" : "?") + "t=" + Clock.Tick);
            req.Headers.TryAddWithoutValidation("User-Agent", "YURI");
            req.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
            using var resp = await Http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return ("", (int)resp.StatusCode);
            return (await resp.Content.ReadAsStringAsync(ct), 0);
        }
        catch (OperationCanceledException) { throw; }
        catch { return ("", 0); }
    }
    static async Task<string> DownloadAsync(string file, CancellationToken ct)
    {
        try { File.Delete(file); } catch { }
        try
        {
            if (DownloadOverride is not null) { if (!await DownloadOverride(file)) return "could not download the build"; }
            else
            {
                if (NoNetwork) return "could not download the build";
                using var req = new HttpRequestMessage(HttpMethod.Get, BuildUrl + "?t=" + Clock.Tick);
                req.Headers.TryAddWithoutValidation("User-Agent", "YURI");
                using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode) return "could not download the build: the repository answered " + (int)resp.StatusCode;
                await using var fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None);
                await resp.Content.CopyToAsync(fs, ct);
            }
        }
        catch (OperationCanceledException) { return "the download was cancelled"; }
        catch (Exception e) { return "could not download the build: " + e.Message; }
        if (!File.Exists(file)) return "the download produced no file";
        return "";
    }
    /// <summary>UpdVerify: a real build of the right shape, and larger than a stub.</summary>
    public static string Verify(string file, string ver)
    {
        try
        {
            var fi = new FileInfo(file);
            if (!fi.Exists || fi.Length < 1024 * 1024) return "the download is not a YURI build";
            using var fs = File.OpenRead(file);
            var head = new byte[4]; int n = fs.Read(head, 0, 4);
            if (Os.IsWin) { if (n < 2 || head[0] != (byte)'M' || head[1] != (byte)'Z') return "the download is not a YURI.exe build"; }
            else if (n < 4 || head[0] != 0x50 || head[1] != 0x4B) return "the download is not a YURI.app archive";
        }
        catch (Exception e) { return "could not read the download: " + e.Message; }
        return "";
    }
    /// <summary>UpdReplace: the running build moves aside as .old, the download takes its place, and the hub closes to restart.</summary>
    public static string Replace(string file, string ver)
    {
        string exe = Environment.ProcessPath ?? "";
        if (exe == "" || !File.Exists(exe)) return "could not find the running build to replace";
        if (!Os.IsWin) return "on macOS, unzip the downloaded YURI.app over the one in Applications - " + file;
        string oldExe = exe + ".old";
        try
        {
            try { File.Delete(oldExe); } catch { }
            File.Move(exe, oldExe, true);
            try { File.Copy(file, exe, true); }
            catch { try { File.Move(oldExe, exe, true); } catch { } throw; }
        }
        catch (Exception e) { return "could not replace the file: " + e.Message; }
        try { File.Delete(file); } catch { }
        RestartCmd = exe;
        Hub()?.HubClose();
        return "";
    }
    /// <summary>The previous launch's build, left beside this one by the swap.</summary>
    public static void SweepOld() { try { string exe = Environment.ProcessPath ?? ""; if (exe != "" && File.Exists(exe + ".old")) File.Delete(exe + ".old"); } catch { } }

    // ---- the phases ----
    public static void Set(int ph, string msg = "", string ttl = "COULD NOT UPDATE") { Phase = ph; Msg = msg; Ttl = ttl; At = Clock.Tick; Poke(); }
    public static void Reset() { KillChild(); Phase = 0; Ver = ""; Msg = ""; Ttl = ""; At = 0; Auto = false; Pend = false; }
    static void KillChild() { try { _cts?.Cancel(); } catch { } _cts = null; if (_tmp != "") { try { File.Delete(_tmp); } catch { } _tmp = ""; } }
    static bool ProbeStart()
    {
        Reset();
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = _cts.Token;
        Set(1);
        long stamp = At;
        _ = Task.Run(async () =>
        {
            var r = await ProbeAsync(ct);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (Phase != 1 || At != stamp) return;
                if (r.ok && r.newer) { Ver = r.ver; Set(2); if (Auto) RaiseWhenClear(); }
                else if (Auto) Reset();
                else if (r.ok) Set(4);
                else Set(5, r.msg != "" ? r.msg : "could not check", "COULD NOT CHECK");
            });
        });
        return true;
    }
    public static void Raise() { Pend = false; GateKind = 3; GateAt = Clock.Tick; GateOut = 0; ClearBelow(); Poke(); }
    static void RaiseWhenClear() { if (GateUp) Pend = true; else Raise(); }
    static void ClearBelow()
    {
        Modules.ScriptHub.Scr.Blur();
        Modules.FastFlags.FfmField.Blur();
        try { Modules.FastFlags.FfmViews.CloseView(); } catch { }
        Modules.ClientSettings.RSet.DdClose();
        if (Hub() is { } h) { h.HL.drag = 0; h.HL.dragZone = 0; }
    }
    /// <summary>UpdCheckStart: CHECK FOR UPDATES on the dashboard.</summary>
    public static void CheckStart()
    {
        if (Hub() is not { } h || h.HL.closeAt != 0 || GateUp) return;
        ProbeStart();
        Auto = false;
        Raise();
    }
    /// <summary>UpdAutoStart: the launch check - silent unless it finds something.</summary>
    public static void AutoStart()
    {
        if (Phase != 0) return;
        if (!ProbeStart()) { Reset(); return; }
        Auto = true;
    }
    public static void InstallBegin()
    {
        if (Phase != 2) return;
        _tmp = Path.Combine(Path.GetTempPath(), "yuri_upd_" + Clock.Tick + (Os.IsWin ? ".exe" : ".zip"));
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var ct = _cts.Token; string tmp = _tmp;
        Set(3);
        long stamp = At;
        _ = Task.Run(async () =>
        {
            string err = await DownloadAsync(tmp, ct);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (Phase != 3 || At != stamp) return;
                if (err != "") { Set(5, err); return; }
                Set(6);
                Avalonia.Threading.DispatcherTimer.RunOnce(Finish, TimeSpan.FromMilliseconds(80));
            });
        });
    }
    static void Finish()
    {
        if (Phase != 6) return;
        string err = Verify(_tmp, Ver);
        if (err == "") err = Replace(_tmp, Ver);
        if (err == "") return;
        try { File.Delete(_tmp); } catch { }
        _tmp = "";
        Set(5, err);
    }
    public static void Cancel() { KillChild(); Dismiss(); }
    public static void Dismiss() { if (!GateUp || GateOut != 0) return; GateOut = Clock.Tick; Poke(); }
    static void CardCopy(out string ttl, out string l1, out string l2)
    {
        switch (Phase)
        {
            case 1: ttl = "CHECKING FOR UPDATES"; l1 = "Asking the repository for its newest version."; l2 = "You are on v" + AppInfo.Version + "."; break;
            case 2: ttl = "NEWER VERSION FOUND"; l1 = "v" + Ver + " is available - you are on v" + AppInfo.Version + "."; l2 = Auto ? "Found at launch. UPDATE installs it, then restarts YURI." : "UPDATE fetches it, checks it loads, then restarts YURI."; break;
            case 3: ttl = "DOWNLOADING v" + Ver; l1 = "Fetching the new build from the repository."; l2 = "This one keeps running until the new one has been checked."; break;
            case 4: ttl = "NO NEWER VERSION"; l1 = "The repository has nothing newer than v" + AppInfo.Version + "."; l2 = "You are up to date."; break;
            case 5: ttl = Ttl != "" ? Ttl : "COULD NOT UPDATE"; l1 = Msg; l2 = "Nothing was changed."; break;
            default: ttl = "RESTARTING"; l1 = "Checking that v" + Ver + " loads, then putting it in place."; l2 = "YURI will close and reopen on its own."; break;
        }
    }

    // ---- the gate card ----
    public static void CardGeom(out double kx, out double ky, out double kw, out double kh) { kw = 486; kh = 232; kx = HubLayout.pd + (HubLayout.cw - kw) / 2; ky = HubLayout.pd + (HubLayout.ch - kh) / 2 - 4; }
    static void Btns(out double by, out double bw, out double bh, out double x1, out int z1, out double x2, out int z2, out int n)
    {
        CardGeom(out double kx, out double ky, out double kw, out double kh);
        bw = 150; bh = 34; by = ky + kh - bh - 24; x2 = 0; z2 = 0;
        if (Phase == 2) { n = 2; x1 = kx + (kw - bw * 2 - 14) / 2; z1 = 2002; x2 = x1 + bw + 14; z2 = 2003; }
        else if (Phase == 6) { n = 0; x1 = kx + (kw - bw) / 2; z1 = 0; }
        else { n = 1; x1 = kx + (kw - bw) / 2; z1 = Phase is 4 or 5 ? 2002 : 2003; }
    }
    public static int Zone(double ux, double uy)
    {
        if (!GateUp) return 0;
        if (GateKind == 3)
        {
            Btns(out double by, out double bw, out double bh, out double x1, out int z1, out double x2, out int z2, out int n);
            if (uy >= by && uy <= by + bh) { if (n >= 1 && ux >= x1 && ux <= x1 + bw) return z1; if (n == 2 && ux >= x2 && ux <= x2 + bw) return z2; }
        }
        else
        {
            CardGeom(out double kx, out double ky, out double kw, out double kh);
            double bw = 150, bh = 34, bx = kx + (kw - bw) / 2, by = ky + kh - bh - 24;
            if (ux >= bx && ux <= bx + bw && uy >= by && uy <= by + bh) return 1210;
        }
        return 1209;                                                        // the veil: swallows the click
    }
    public static bool Click(HubSurface hub, int z)
    {
        long now = Clock.Tick;
        switch (z)
        {
            case 1210: hub.ClickAt[1210] = now; Dismiss(); return true;
            case 2001: hub.ClickAt[2001] = now; CheckStart(); return true;
            case 2002: hub.ClickAt[2002] = now; if (Phase == 2) InstallBegin(); else Dismiss(); return true;
            case 2003: hub.ClickAt[2003] = now; Cancel(); return true;
            case 1209: return true;
        }
        return false;
    }
    public static void Draw(HubSurface hub, long now)
    {
        if (!GateUp) return;
        var HL = hub.HL;
        int gKind = GateKind;
        double f;
        if (GateOut != 0)
        {
            double e = Clamp((now - GateOut) / 300.0, 0.0, 1.0);
            f = 1 - Ease3(e);
            if (e >= 1.0)
            {
                GateAt = 0; GateOut = 0;
                if (gKind == 3) Reset();
                else if (Pend) Raise();
                else if (gKind == 1 && !Tut.Seen) Tut.Start(true);                   // the first run: the tour, once the welcome card has gone
                return;
            }
        }
        else f = Ease3(Clamp((now - GateAt) / 420.0, 0.0, 1.0));
        if (f <= 0.004) return;
        long el = now - (GateOut != 0 ? GateOut : GateAt);
        double pd = HubLayout.pd, cw = HubLayout.cw, ch = HubLayout.ch;
        uint acc = HubState.Accent;
        FillRR(pd - 2, pd - 2, cw + 4, ch + 4, 22, SBrush(FA(0xD8060710, f)));
        FillRR(pd - 2, pd - 2, cw + 4, ch / 2, 22, VBrush(pd - 2, pd - 2, cw + 4, ch / 2, FA(0x66000000, f), FA(0x00000000, f)));
        FillRR(pd - 2, pd + ch / 2, cw + 4, ch / 2 + 2, 22, VBrush(pd - 2, pd + ch / 2, cw + 4, ch / 2 + 2, FA(0x00000000, f), FA(0x66000000, f)));
        CardGeom(out double kx, out double ky, out double kw, out double kh);
        ky += (1 - f) * 20;
        ShadowDraw(kx, ky + 6, kw, kh, 20, 8, 26, 26, f);
        FillRR(kx, ky, kw, kh, 20, VBrush(kx, ky, kw, kh, FA(0xFF191D33, f), FA(0xFF0D0F1D, f)));
        MicroBackdrop(kx, ky, kw, kh, 20, acc, f, now, 0.95);
        StrokeRR(kx, ky, kw, kh, 20, Pen(FA(Alpha(acc, 165), f), 1.2));
        FillRR(kx + 1, ky + 16, 2, kh - 32, 1, VBrush(kx + 1, ky + 16, 2, kh - 32, FA(Alpha(AccHi(acc, 0.5), 215), f), FA(Alpha(acc, 35), f)));
        double sw = (kw - 160) * f;
        FillRR(kx + kw / 2 - sw / 2, ky + 0.8, sw, 2, 1, HBrush(kx + kw / 2 - sw / 2, ky + 0.8, sw, 2, FA(Alpha(AccHi(acc, 0.45), 30), f), FA(Alpha(AccHi(acc, 0.45), 230), f)));
        Txt(AppInfo.AppName, kx + 28, ky + 20, 90, 18, Fonts.fBrand, FA(0xC8E8EAF6, f), Fmt.L);
        double bwz = Fonts.MeasureW(AppInfo.AppName, Fonts.fBrand);
        FillEll(kx + 32 + bwz, ky + 27, 5, 5, SBrush(FA(Alpha(acc, R(150 + 70 * (Math.Sin(DecT(now) * 0.004) + 1) / 2)), f)));
        Txt("CONTROL SUITE", kx + 29, ky + 36, 120, 10, HL.fS, FA(Alpha(acc, 155), f), Fmt.L);
        string rs = gKind == 3 ? AppInfo.ZVer : HubLayout.bw + " x " + HubLayout.bh;
        double rw = Fonts.MeasureW(rs, HL.fXs) + 22;
        FillRR(kx + kw - 28 - rw, ky + 22, rw, 18, 9, SBrush(FA(Alpha(0xFFFFFF, 16), f)));
        StrokeRR(kx + kw - 28 - rw, ky + 22, rw, 18, 9, Pen(FA(Alpha(acc, 90), f), 1));
        Txt(rs, kx + kw - 28 - rw, ky + 25, rw, 13, HL.fXs, FA(Alpha(0xFFC7CBE0, 215), f), Fmt.C);
        FadeLine(kx + 26, kx + kw - 26, ky + 54, 0x1EFFFFFF, f);
        double gx = kx + 60, gy = ky + 112;
        if (gKind == 3) Glyph(gx, gy, acc, f, now);
        else
        {
            double rr = 26 + 30 * (1 - Ease3(Clamp(el / 620.0, 0.0, 1.0))), ra = GateOut != 0 ? 0 : 1 - Clamp(el / 620.0, 0.0, 1.0);
            if (ra > 0.01) Ell(gx - rr, gy - rr, rr * 2, rr * 2, Pen(FA(Alpha(AccHi(acc, 0.4), R(170 * ra)), f), 1.4));
            FillEll(gx - 27, gy - 27, 54, 54, SBrush(FA(Alpha(acc, 26), f)));
            StrokeRR(gx - 19, gy - 15, 30, 22, 4, Pen(FA(Alpha(acc, R(95 * f)), f), 1.4));
            var pnM = Pen(FA(Alpha(AccHi(acc, 0.42), 240), f), 1.9);
            StrokeRR(gx - 14, gy - 10, 30, 22, 4, pnM); Line(gx - 6, gy + 17, gx + 8, gy + 17, pnM); Line(gx + 1, gy + 12, gx + 1, gy + 17, pnM);
        }
        double tx = kx + 108, tw = kw - 136;
        long elc = gKind == 3 && GateOut == 0 ? now - At : el;
        double l1 = Ease3(Clamp((elc - 60) / 380.0, 0.0, 1.0)) * (GateOut != 0 ? f : 1), l2 = Ease3(Clamp((elc - 140) / 380.0, 0.0, 1.0)) * (GateOut != 0 ? f : 1), l3 = Ease3(Clamp((elc - 210) / 380.0, 0.0, 1.0)) * (GateOut != 0 ? f : 1);
        string gTtl, gL1, gL2;
        if (gKind == 3) CardCopy(out gTtl, out gL1, out gL2);
        else if (gKind == 2) { gTtl = "RESOLUTION CHANGE DETECTED"; gL1 = "The hub builds its layout for the screen it opened on,"; gL2 = "so it has closed. Restart YURI to use it here."; }
        else { gTtl = "BEFORE YOU START"; gL1 = "The hub builds its layout for the screen it opens on."; gL2 = "Change resolution and it will put itself away and say so."; }
        Txt(gTtl, tx + (1 - l1) * 10, ky + 78, tw, 24, HL.fV, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.22), 248), f * l1), Fmt.L);
        Txt(HubUI.FFMElide(gL1, Fonts.fHint, tw), tx + (1 - l2) * 10, ky + 108, tw, 18, Fonts.fHint, FA(0xBEC7CBE0, f * l2), Fmt.L);
        Txt(HubUI.FFMElide(gL2, Fonts.fHint, tw), tx + (1 - l3) * 10, ky + 128, tw, 18, Fonts.fHint, FA(0xBEC7CBE0, f * l3), Fmt.L);
        if (gKind == 3)
        {
            Btns(out double by, out double bw, out double bh, out double x1, out int z1, out double x2, out int z2, out int n);
            by += (1 - f) * 20;
            FadeLine(kx + 26, kx + kw - 26, by - 18, 0x1EFFFFFF, f);
            double bf = GateOut != 0 ? f : f * Ease3(Clamp((elc - 300) / 320.0, 0.0, 1.0));
            if (n >= 1) BtnDraw(hub, z1, x1, by, bw, bh, z1 == 2002 ? (Phase == 2 ? "UPDATE" : "CLOSE") : "CANCEL", acc, bf, now, z1 == 2002);
            if (n == 2) BtnDraw(hub, z2, x2, by, bw, bh, "CANCEL", acc, bf, now, false);
            return;
        }
        double bw0 = 150, bh0 = 34, bx0 = kx + (kw - bw0) / 2, by0 = ky + kh - bh0 - 24;
        FadeLine(kx + 26, kx + kw - 26, by0 - 18, 0x1EFFFFFF, f);
        BtnDraw(hub, 1210, bx0, by0, bw0, bh0, gKind == 2 ? "CLOSE HUB" : "GOT IT", acc, f, now, true);
    }
    static void Glyph(double gx, double gy, uint acc, double f, long now)
    {
        int ph = Phase; long el = now - At;
        uint col = ph == 4 ? C_ON : ph == 5 ? AMBER : acc;
        double rr = 26 + 30 * (1 - Ease3(Clamp(el / 620.0, 0.0, 1.0))), ra = GateOut != 0 ? 0 : 1 - Clamp(el / 620.0, 0.0, 1.0);
        if (ra > 0.01) Ell(gx - rr, gy - rr, rr * 2, rr * 2, Pen(FA(Alpha(AccHi(col, 0.4), R(170 * ra)), f), 1.4));
        FillEll(gx - 27, gy - 27, 54, 54, SBrush(FA(Alpha(col, 26), f)));
        if (ph is 1 or 3 or 6)
        {
            double a1 = DecT(now) * 0.34 % 360, a2 = -DecT(now) * 0.52 % 360;
            Arc(gx - 17, gy - 17, 34, 34, a1, 100, Pen(FA(Alpha(AccHi(acc, 0.42), 240), f), 2.2));
            Arc(gx - 11, gy - 11, 22, 22, a2, 150, Pen(FA(Alpha(acc, 150), f), 1.6));
            if (ph == 3)
            {
                double dy = DecT(now) % 900 / 900.0, da = Math.Sin(Math.PI * dy), ay = gy - 5 + 6 * dy;
                var pnD = Pen(FA(Alpha(AccHi(acc, 0.5), R(230 * da)), f), 1.6);
                Line(gx, ay - 3, gx, ay + 3, pnD); Line(gx - 2.6, ay + 0.6, gx, ay + 3, pnD); Line(gx + 2.6, ay + 0.6, gx, ay + 3, pnD);
            }
        }
        else if (ph == 2)
        {
            double bob = HubState.LowPerf ? 0 : 1.6 * Math.Sin(DecT(now) * 0.005);
            var pnA = Pen(FA(Alpha(AccHi(acc, 0.42), 240), f), 1.9);
            Line(gx, gy - 14 + bob, gx, gy + 4 + bob, pnA); Line(gx - 6, gy - 2 + bob, gx, gy + 4 + bob, pnA); Line(gx + 6, gy - 2 + bob, gx, gy + 4 + bob, pnA);
            var pnT = Pen(FA(Alpha(acc, R(110 * f)), f), 1.6);
            Line(gx - 13, gy + 7, gx - 13, gy + 13, pnT); Line(gx - 13, gy + 13, gx + 13, gy + 13, pnT); Line(gx + 13, gy + 13, gx + 13, gy + 7, pnT);
        }
        else if (ph == 4)
        {
            double t = Ease3(Clamp((el - 120) / 420.0, 0.0, 1.0));
            Ell(gx - 19, gy - 19, 38, 38, Pen(FA(Alpha(C_ON, R(70 + 60 * t)), f), 1.4));
            double s1 = Math.Min(t * 2, 1.0), s2 = Clamp(t * 2 - 1, 0.0, 1.0);
            var pnK = Pen(FA(Alpha(AccHi(C_ON, 0.2), 245), f), 2.3);
            Line(gx - 11, gy, gx - 11 + 8 * s1, gy + 8 * s1, pnK);
            if (s2 > 0) Line(gx - 3, gy + 8, gx - 3 + 15 * s2, gy + 8 - 16 * s2, pnK);
        }
        else if (ph == 5)
        {
            Ell(gx - 19, gy - 19, 38, 38, Pen(FA(Alpha(AMBER, 110), f), 1.4));
            Line(gx, gy - 12, gx, gy + 3, Pen(FA(Alpha(AccHi(AMBER, 0.2), 245), f), 2.4));
            FillEll(gx - 2.2, gy + 7.5, 4.4, 4.4, SBrush(FA(Alpha(AMBER, 245), f)));
        }
    }
    /// <summary>HubGateBtnDraw: the primary (filled) and secondary (outlined) card buttons.</summary>
    public static void BtnDraw(HubSurface hub, int z, double bx, double by, double bw, double bh, string label, uint acc, double f, long now, bool primary)
    {
        double hv = hub.Hv(z);
        long pAt = hub.ClickAt.TryGetValue(z, out var ca) ? now - ca : 99999;
        double pr = pAt < 150 ? 1 - pAt / 150.0 : 0.0, lift = -2.0 * hv + 2.4 * pr;
        int st = PushXform(bx + bw / 2, by + bh / 2 + lift, 1 - 0.025 * pr, 0);
        double x0 = bx, y0 = by + lift;
        if (pAt < 420) { double rp = pAt / 420.0, gr = 6 + 26 * Ease3(rp); StrokeRR(x0 - gr, y0 - gr, bw + gr * 2, bh + gr * 2, 10 + gr, Pen(FA(Alpha(AccHi(acc, 0.45), R(150 * (1 - rp))), f), 1.6 * (1 - rp) + 0.4)); }
        uint tc;
        if (primary)
        {
            if (hv > 0.01) FillRR(x0 - 6, y0 - 5, bw + 12, bh + 10, 14, SBrush(FA(Alpha(AccHi(acc, 0.4), R(34 * hv)), f)));
            ShadowDraw(x0, y0 + 2, bw, bh, 10, 4, 26, 14, f * (0.55 + 0.45 * hv));
            FillRR(x0, y0, bw, bh, 10, VBrush(x0, y0, bw, bh, FA(Alpha(AccHi(acc, 0.5), R(198 + 40 * hv)), f), FA(Alpha(acc, R(140 + 40 * hv)), f)));
            FillRR(x0 + 6, y0 + 1, bw - 12, bh * 0.42, 8, VBrush(x0 + 6, y0 + 1, bw - 12, bh * 0.42, FA(Alpha(0xFFFFFF, R(52 + 26 * hv)), f), FA(Alpha(0xFFFFFF, 0), f)));
            if (hv > 0.02 && hv < 0.995)
            {
                double sx2 = x0 - 30 + (bw + 60) * hv;
                int stS = PushG(); ClipRR(x0, y0, bw, bh, 10);
                FillRR(sx2 - 16, y0, 32, bh, 0, HBrush(sx2 - 16, y0, 32, bh, FA(Alpha(0xFFFFFF, 0), f), FA(Alpha(0xFFFFFF, R(46 * Math.Sin(hv * Math.PI))), f)));
                Pop(stS);
            }
            StrokeRR(x0, y0, bw, bh, 10, Pen(FA(Alpha(AccHi(acc, 0.6), R(215 + 40 * hv)), f), 1.3));
            tc = FA(0xFFFFFFFF, f);
        }
        else
        {
            if (hv > 0.01) FillRR(x0 - 4, y0 - 3, bw + 8, bh + 6, 12, SBrush(FA(Alpha(0xFFFFFF, R(10 * hv)), f)));
            FillRR(x0, y0, bw, bh, 10, VBrush(x0, y0, bw, bh, FA(Alpha(0xFFFFFF, R(14 + 10 * hv)), f), FA(Alpha(0xFFFFFF, R(6 + 6 * hv)), f)));
            StrokeRR(x0, y0, bw, bh, 10, Pen(FA(Alpha(acc, R(90 + 110 * hv)), f), 1.1));
            if (hv > 0.02 && !HubState.LowPerf) { double uw = (bw - 24) * Ease3(hv); FillRR(x0 + bw / 2 - uw / 2, y0 + bh - 4, uw, 2, 1, SBrush(FA(Alpha(acc, R(160 * hv)), f))); }
            tc = FA(Alpha(THMix(0xFFC7CBE0, 0xFFFFFFFF, hv), R(200 + 55 * hv)), f);
        }
        TxtP(label, x0, y0 - 1 - (HubState.LowPerf ? 0 : 0.6 * hv - 0.8 * pr), bw, bh, Fonts.fBadge, tc, Fmt.C);
        Pop(st);
    }
}
