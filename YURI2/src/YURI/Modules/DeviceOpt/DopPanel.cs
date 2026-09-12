using Yuri.Core;
using Yuri.Gfx;
using Yuri.Modules.FastFlags;
using Yuri.Platform;
using Yuri.Shell.Hub;
using Yuri.Shell.Hub.Tabs;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Modules.DeviceOpt;

/// <summary>DOPSystems / DOPFooter / DOPZone / DOPClick / DOPDetail / DOPIcon: module 6's panel. Zone ids are the .ahk's (1250-1274).</summary>
public static class DopPanel
{
    const uint AMBER = Dop.AMBER, C_ON = Dop.C_ON, C_BAD = Dop.C_BAD, C_ACC = Dop.C_ACC;
    const int RH = Dop.RH, RG = Dop.RG;
    const double DOP_FRAME = 0.62, DOP_ROW0 = 0.63, DOP_ROWD = 0.045, DOP_ROWS = 0.13;
    public static double RowY(int i) => 12 + (i - 1) * RG;
    public static double Total() => Dop.SYSN * RG;
    public static double Vis() => Dop.SYSN * RG;
    public static double Doph => Dop.SYSN * RG + 24;
    static double Dopby(HubLayout HL) => HL.aby + Doph + 12;
    const double Dopbh = 72;

    public static void Register()
    {
        Integrations.Panels[6] = (hub, ax, ay, x0, y0, dx, dy2, ff, now, acc) =>
        {
            Draw(hub, ax, ay, x0, y0, dx, dy2, ff, now, acc);
            if (!Yuri.Platform.Os.IsWin)
                HubUI.WinOnlyVeil(hub.HL.abx - 10, hub.HL.aby - 10, hub.HL.abw + 20,
                    HubLayout.pd + HubLayout.ch - 34 - (hub.HL.aby - 10), acc, ff, now,
                    "EVERY GROUP IN THIS MODULE",
                    "these are registry values, bcdedit and powercfg - macOS has no equivalent to set");
        };
        Integrations.PanelZones[6] = (hub, ux, uy) => Yuri.Platform.Os.IsWin ? Zone(hub, ux, uy) : 0;
        Integrations.PanelClicks[6] = Click;
        Integrations.PanelWheels[6] = Wheel;
        Integrations.DevOptOn = Dop.OnCount;
        Dop.Boot();
    }
    public static bool Animating => Dop.Busy != "" || Math.Abs(Dop.Scr - Dop.ScrT) > 0.4 || (Dop.MsgAt != 0 && Clock.Tick - Dop.MsgAt < 6300) || Dop.FlashAt.Count > 0
        || (Dop.FlashLast != 0 && Clock.Tick - Dop.FlashLast < 700) || Dop.T.Any(kv => Math.Abs(kv.Value - (Dop.IsOn(Dop.Groups[kv.Key - 1].Id) ? 1.0 : 0.0)) > 0.004);

    static void Icon(int i, double cx, double cy, uint col, bool live, long now)
    {
        var pn = Pen(col, 1.6);
        if (i == 1) { Line(cx - 5, cy - 7, cx - 5, cy + 6, pn); Line(cx - 5, cy - 7, cx + 4, cy + 2, pn); Line(cx - 5, cy + 6, cx + 4, cy + 2, pn); }
        else if (i == 2)
        {
            Ell(cx - 7, cy - 7, 14, 14, pn);
            double a = live ? (DecT(now) * 0.06 % 360) * 0.0174533 : 5.6;
            Line(cx, cy, cx + 5 * Math.Cos(a), cy + 5 * Math.Sin(a), pn); Line(cx, cy, cx, cy - 4, pn);
        }
        else if (i == 3) { for (int k = 1; k <= 4; k++) { double bh = 3 + k * 2.6; Line(cx - 7 + (k - 1) * 4.6, cy + 6, cx - 7 + (k - 1) * 4.6, cy + 6 - bh, pn); } }
        else if (i == 4) { Line(cx + 2, cy - 8, cx - 4, cy + 1, pn); Line(cx - 4, cy + 1, cx + 1, cy + 1, pn); Line(cx + 1, cy + 1, cx - 2, cy + 8, pn); }
        else
        {
            StrokeRR(cx - 6, cy - 6, 12, 12, 2, pn);
            for (int k = 1; k <= 3; k++) { Line(cx - 9, cy - 4 + (k - 1) * 4, cx - 6, cy - 4 + (k - 1) * 4, pn); Line(cx + 6, cy - 4 + (k - 1) * 4, cx + 9, cy - 4 + (k - 1) * 4, pn); }
        }
    }

    public static void Draw(HubSurface hub, double ax, double ay, double x0, double y0, double dx, double dy2, double ff, long now, uint acc)
    {
        var HL = hub.HL;
        double mt = HL.modT;
        Txt("SYSTEMS", ax, y0 + 36, 120, 14, Fonts.fBadge, FA(0x62C7CBE0, ff), Fmt.L);
        Txt(mt > 0.5 ? Dop.SYSN + " listed" : "0 listed", ax + 66, y0 + 36, 80, 14, HL.fS, FA(Alpha(acc, 60 + 120 * mt), ff), Fmt.L);
        double fm = Clamp(mt / DOP_FRAME, 0.0, 1.0);
        double pah = 26 + (Doph - 26) * fm;
        FillRR(ax, ay, HL.abw, pah, 12, VBrush(ax, ay, HL.abw, pah, FA(Mix(0xFF141728, 0xFF181C34, fm), ff), FA(0xFF12141F, ff)));
        MiniBackdrop(ax, ay, HL.abw, pah, 12, acc, ff, now, 0.9);
        Integrations.HubEmptyGhost(hub, ax, ay, pah, ff);
        StrokeRR(ax, ay, HL.abw, pah, 12, Pen(FA(Alpha(acc, 70 + 90 * fm), ff), 1.2));
        double gl = 40 * fm;
        var pnG = Pen(FA(Alpha(AccHi(acc, 0.4), 200 * fm), ff), 1.6);
        Line(ax + 14, ay + 0.6, ax + 14 + gl, ay + 0.6, pnG); Line(ax + HL.abw - 14 - gl, ay + pah - 0.6, ax + HL.abw - 14, ay + pah - 0.6, pnG);
        ModRing(ax, ay, HL.abw, pah, 12, acc, ff, HL.modAt, now, 700, 18, hub.HubMod != 0);
        if (mt < 0.02) return;
        int st = PushG();
        ClipRR(ax, ay, HL.abw, pah, 12);
        double vis = Vis();
        Dop.ScrT = Clamp(Dop.ScrT, 0.0, Math.Max(0.0, Total() - vis));
        Dop.Scr += (Dop.ScrT - Dop.Scr) * EK(0.26);
        if (Math.Abs(Dop.ScrT - Dop.Scr) < 0.4) Dop.Scr = Dop.ScrT;
        double scr = Dop.Scr;
        for (int i = 1; i <= Dop.Groups.Length; i++)
        {
            var grp = Dop.Groups[i - 1];
            double ry = ay + RowY(i) - scr;
            double cg = Clamp((mt - DOP_ROW0 - (i - 1) * DOP_ROWD) / DOP_ROWS, 0.0, 1.0);
            if (cg <= 0.01) continue;
            double fb = ff * cg;
            double hv = Math.Max(hub.Hv(1250 + i), hub.Hv(1260 + i));
            double sld = (1 - cg) * 10;
            double axs = ax + sld;
            bool on = Dop.IsOn(grp.Id);
            double cur = Dop.T.TryGetValue(i, out var tv) ? tv : (on ? 1.0 : 0.0);
            cur += ((on ? 1.0 : 0.0) - cur) * EK(0.22);
            Dop.T[i] = cur;
            bool run = Dop.Busy == grp.Id;
            double fl = 0.0;
            if (Dop.FlashAt.TryGetValue(i, out var fat)) { double fe = (now - fat) / 520.0; if (fe >= 1) Dop.FlashAt.Remove(i); else fl = 1 - fe; }
            if (hv > 0.01 || fl > 0.01) FillRR(ax + 6, ry, HL.abw - 12, RH, 8, HBrush(ax + 6, ry, HL.abw - 12, RH, FA(Alpha(0xFFFFFF, 15 * hv + 20 * fl), fb), FA(Alpha(0xFFFFFF, 3 * hv), fb)));
            FillRR(axs + 14, ry + 10, 3, 20, 1.5, VBrush(axs + 14, ry + 10, 3, 20, FA(Alpha(on ? AccHi(acc, 0.35) : 0xFFC7CBE0, on ? 235 : 60), fb), FA(Alpha(on ? acc : 0xFFC7CBE0, on ? 150 : 40), fb)));
            Icon(i, axs + 36, ry + RH / 2.0, FA(Alpha(on ? acc : 0xFF9AA8C0, on ? 220 : 110), fb), on, now);
            double tw = HL.abw - 132;
            Txt(grp.N, axs + 54, ry + 5, tw, 16, Fonts.fBadge, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.18), 235), fb), Fmt.L);
            int dr = on ? Dop.DriftOf(grp.Id) : 0;
            string ht = !Os.IsWin ? "windows only - " + grp.H
                : run ? "working - approve the elevation prompt"
                : on && dr > 0 ? dr + " setting" + (dr == 1 ? "" : "s") + " no longer set - press RE-APPLY"
                : on && dr < 0 && grp.Rb ? "applied - restart to take effect; boot values cannot be verified"
                : on && dr < 0 ? "applied - this group cannot be verified without elevation"
                : on && grp.Rb ? "applied - takes effect after a restart"
                : on ? "applied - revert restores what was captured at hub open"
                : grp.H;
            uint hc = run ? Alpha(AMBER, 220) : on && dr > 0 ? Alpha(C_BAD, 215) : on ? Alpha(C_ON, 190) : 0x8AC7CBE0;
            Txt(FFMElide(ht, HL.fS, tw), axs + 54, ry + 24, tw, 15, HL.fS, FA(hc, fb), Fmt.L);
            if (grp.Rb && !on) Txt("RESTART", ax + HL.abw - 152, ry + 6, 62, 12, HL.fXs, FA(Alpha(AMBER, 165), fb), Fmt.R);
            double hvs = hub.Hv(1260 + i);
            FFMTogDraw(ax + HL.abw - 64, ry + RH / 2.0 - 10, 48, 20, cur, acc, hvs, fb * (Os.IsWin ? 1.0 : 0.5));
            if (run)
            {
                double e = (DecT(now) % 900) / 900.0;
                double ex = e * 10;
                StrokeRR(ax + HL.abw - 64 - ex, ry + RH / 2.0 - 10 - ex * 0.6, 48 + ex * 2, 20 + ex * 1.2, 10 + ex * 0.3, Pen(FA(Alpha(AMBER, R(190 * (1 - e))), fb), 1.6 * (1 - e) + 0.2));
            }
        }
        if (Total() > vis)
        {
            double sbG = Clamp((mt - DOP_ROW0) / DOP_ROWS, 0.0, 1.0);
            double sbA = hub.Hv(1272);
            double sw = 3 + 4 * sbA;
            double sx2 = ax + HL.abw - 12 - 3 * sbA;
            FillRR(sx2, ay + 10, sw, vis, sw / 2, SBrush(FA(Alpha(0xFFFFFF, 20 + 28 * sbA), ff * sbG)));
            double thmb = Math.Max(22, vis * (vis / Total()));
            double ty = ay + 10 + (vis - thmb) * (scr / Math.Max(1, Total() - vis));
            FillRR(sx2, ty, sw, thmb, sw / 2, VBrush(sx2, ty, sw, thmb, FA(Alpha(AccHi(acc, 0.35), 200), ff * sbG), FA(Alpha(acc, 180), ff * sbG)));
        }
        Pop(st);
        Footer(hub, ax, Dopby(HL) + dy2, HL.abw, ff, now, acc, mt);
    }

    static void Footer(HubSurface hub, double px, double py, double pw, double ff, long now, uint acc, double mt)
    {
        var HL = hub.HL;
        double cg = Clamp((mt - 0.84) / 0.12, 0.0, 1.0);
        if (cg <= 0.01) return;
        double fb = ff * cg;
        double ph = Dopbh;
        py -= (1 - cg) * 12;
        FillRR(px, py, pw, ph, 12, VBrush(px, py, pw, ph, FA(Mix(0xFF141728, 0xFF1B2038, 0.5), fb), FA(0xFF10121C, fb)));
        MiniBackdrop(px, py, pw, ph, 12, acc, fb, now, 0.85);
        StrokeRR(px, py, pw, ph, 12, Pen(FA(Alpha(acc, 60 + 40 * mt), fb), 1.2));
        double gl = 30 * cg;
        var pnG = Pen(FA(Alpha(AccHi(acc, 0.4), 170 * cg), fb), 1.5);
        Line(px + 12, py + 0.6, px + 12 + gl, py + 0.6, pnG); Line(px + pw - 12 - gl, py + ph - 0.6, px + pw - 12, py + ph - 0.6, pnG);
        int stF = PushG();
        ClipRR(px, py, pw, ph, 12);
        int n = Dop.OnCount();
        bool busy = Dop.Busy != "";
        if (busy && !HubState.LowPerf)
        {
            double bx = px + (DecT(now) * 0.10 % (pw + 80)) - 40;
            FillRect(bx, py, 46, ph, VBrush(bx, py, 46, ph, Alpha(0xFFFFFF, 0), FA(Alpha(AMBER, 26), fb)));
        }
        uint dot = busy ? AMBER : n > 0 ? C_ON : 0xFF8A90A6;
        int da = busy ? R(150 + 90 * Math.Abs(Math.Sin(DecT(now) * 0.005))) : (n > 0 ? 225 : 90);
        FillRR(px + 16, py + 13, 3, 14, 1.5, SBrush(FA(Alpha(dot, da), fb)));
        long mAge = Dop.MsgAt != 0 ? now - Dop.MsgAt : 999999;
        string shown = mAge < 6000 ? Dop.Msg : Dop.Status();
        uint scol = mAge < 6000 ? (Dop.MsgCol == C_ACC ? acc : Dop.MsgCol) : 0x8AC7CBE0;
        Txt(FFMElide(shown, Fonts.fHint, pw - 220), px + 28, py + 11, pw - 220, 18, Fonts.fHint, FA(scol, fb), Fmt.L);
        int segN = Dop.Groups.Length;
        double segW = 20, segG = 4;
        double tw = segN * segW + (segN - 1) * segG;
        double tx = px + pw - 16 - tw;
        Txt(n + " / " + segN, tx - 52, py + 10, 46, 14, HL.fS, FA(Alpha(n > 0 ? AccHi(acc, 0.4) : 0xFF8A90A6, n > 0 ? 235 : 150), fb), Fmt.R);
        Txt("APPLIED", tx, py + 10, tw, 14, HL.fXs, FA(0x62C7CBE0, fb), Fmt.R);
        for (int i = 1; i <= segN; i++)
        {
            var grp = Dop.Groups[i - 1];
            double sg = Clamp((cg - 0.30 - i * 0.06) / 0.40, 0.0, 1.0);
            if (sg <= 0.01) continue;
            double sx = tx + (i - 1) * (segW + segG), sy2 = py + 30;
            double lit = Dop.T.TryGetValue(i, out var tv) ? tv : (Dop.IsOn(grp.Id) ? 1.0 : 0.0);
            bool run = Dop.Busy == grp.Id;
            FillRR(sx, sy2, segW, 5, 2.5, SBrush(FA(Alpha(0xFFFFFF, R(16 * sg)), fb)));
            if (lit > 0.01) FillRR(sx, sy2, segW * lit, 5, 2.5, HBrush(sx, sy2, segW * lit + 0.5, 5, FA(Alpha(AccHi(acc, 0.35), R(230 * sg)), fb), FA(Alpha(C_ON, R(230 * sg)), fb)));
            if (run) { double pl = (Math.Sin(DecT(now) * 0.006 - i) + 1) / 2; FillRR(sx, sy2, segW, 5, 2.5, SBrush(FA(Alpha(AMBER, R((90 + 120 * pl) * sg)), fb))); }
            if (lit > 0.5 && grp.Rb) FillEll(sx + segW / 2 - 1.6, sy2 + 9, 3.2, 3.2, SBrush(FA(Alpha(AMBER, R(220 * sg)), fb)));
        }
        double bg = Clamp((cg - 0.22) / 0.5, 0.0, 1.0);
        if (bg > 0.01)
        {
            double by2 = py + ph - 32 + (1 - bg) * 6;
            FFMBtn(1270, px + 16, by2, 104, 24, "REVERT ALL", acc, ff * bg, n > 0 ? 3 : 4);
            FFMBtn(1271, px + 128, by2, 104, 24, "SNAPSHOTS", acc, ff * bg, 0);
            FFMBtn(1274, px + 240, by2, 104, 24, "RE-APPLY", acc, ff * bg, Dop.DriftN() > 0 ? 3 : 4);
        }
        Pop(stF);
    }

    public static int Zone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        double ax = HL.abx, ay = HL.aby;
        double vis = Vis();
        if (Total() > vis && ux >= ax + HL.abw - 12 && ux <= ax + HL.abw && uy >= ay + 10 && uy <= ay + 10 + vis) return 1272;
        if (uy >= ay + 6 && uy <= ay + vis + 6 && ux >= ax && ux <= ax + HL.abw)
            for (int i = 1; i <= Dop.SYSN; i++)
            {
                double ry = ay + RowY(i) - Dop.Scr;
                double top = Math.Max(ry, ay + 10), bot = Math.Min(ry + RH, ay + 10 + vis);
                if (uy >= top && uy <= bot) return ux >= ax + HL.abw - 64 && ux <= ax + HL.abw - 16 ? 1260 + i : 1250 + i;
            }
        double fby = Dopby(HL);
        double fy = fby + Dopbh - 32;
        if (uy >= fy && uy <= fy + 24)
        {
            if (ux >= ax + 16 && ux <= ax + 120) return 1270;
            if (ux >= ax + 128 && ux <= ax + 232) return 1271;
            if (ux >= ax + 240 && ux <= ax + 344) return 1274;
        }
        if (ux >= ax && ux <= ax + HL.abw && uy >= fby && uy <= fby + Dopbh) return 1273;
        if (ux >= ax && ux <= ax + HL.abw && uy >= ay && uy <= ay + Doph) return 1273;
        return 0;
    }

    public static bool Click(HubSurface hub, int z)
    {
        if (z >= 1251 && z <= 1250 + Dop.SYSN) DetailOpen(z - 1250);
        else if (z >= 1261 && z <= 1260 + Dop.SYSN) Dop.Toggle(z - 1260);
        else if (z == 1270) Dop.RevertAll();
        else if (z == 1271)
        {
            try { Directory.CreateDirectory(Dop.Dir); } catch { }
            try
            {
                if (Os.IsWin) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + Dop.Dir + "\"") { UseShellExecute = true });
                else if (Os.IsMac) System.Diagnostics.Process.Start("open", new[] { Dop.Dir });
            }
            catch { }
        }
        else if (z == 1274) Dop.Reapply();
        else if (z == 1273) { }
        else return false;
        if (z > 0) hub.ClickAt[z] = Clock.Tick;
        hub.Tim(Pace.TICK_A);
        return true;
    }
    /// <summary>
    /// The wheel over THIS panel's list. It used to consume every wheel event
    /// wherever the pointer was and whether or not there was anything left to
    /// scroll, which left the module rail unscrollable for as long as the panel
    /// was open.
    /// </summary>
    public static bool Wheel(HubSurface hub, double ux, double uy, double delta)
    {
        var HL = hub.HL;
        if (ux < HL.abx - 8 || ux > HL.abx + HL.abw + 8 || uy < HL.aby || uy > HL.aby + Vis()) return false;
        double max = Math.Max(0.0, Total() - Vis());
        if (max <= 0.5) return false;
        Dop.ScrT = Clamp(Dop.ScrT - delta * RG, 0.0, max);
        return true;
    }

    /// <summary>DOPDetail(i): the five explainers, in the Detail sheet.</summary>
    static void DetailOpen(int i)
    {
        if (i < 1 || i > Dop.Groups.Length) return;
        var g = Dop.Groups[i - 1];
        int n = g.It.Length;
        string b = g.Id switch
        {
            "input" => "Sets the pointer to move 1:1 with the mouse and shortens the queues the mouse and keyboard classes buffer into.  Enhance Pointer Precision is Windows applying an acceleration curve to your movement, so the same physical distance means different things depending on how fast you moved. Off, plus a flat curve, means the pointer travels the distance the sensor reported and nothing else.  The queue sizes are how many input packets the driver holds before the system reads them. Smaller is not automatically faster - it is a buffer, not a delay - so treat this one as worth trying rather than a certainty.",
            "timers" => "Boot configuration, so nothing here changes until you restart.  Dynamic tick lets Windows skip timer interrupts while idle to save power; off, the tick is steady. The platform clock forces everything onto HPET, which is usually the slower source to read - off means Windows picks, which is normally the TSC.  These are the entries most likely to differ from machine to machine, and the only group here whose original state cannot be read without elevation - so this one falls back to capturing at the moment you switch it on rather than at hub open.",
            "network" => "Two separate things.  In the registry: the multimedia throttle that caps network throughput while media plays, and Nagle's algorithm, which holds small packets back to combine them. Games send small packets constantly, so combining them is exactly wrong for them and right for almost everything else.  On the adapter: energy-saving features that let the NIC sleep or downshift, interrupt moderation, and wake-on-LAN. Every one is captured PER ADAPTER, so a machine with two NICs cannot end up with one adapter's old value written onto the other.  The adapter half is written with -NoRestart, so the NIC keeps running on its loaded settings until it is restarted - disable and re-enable the adapter, or reboot, if you want those now. The registry half is live for new connections immediately. Dropping the link on your behalf mid-session was not a trade worth making silently.  Checksum and large-send offload are deliberately NOT included. Those move work off the CPU; turning them off raises CPU load and usually costs latency rather than saving it.",
            "power" => "Stops Windows parking this machine's performance.  Power throttling is the scheduler putting background threads on efficiency cores or lower clocks. USB selective suspend lets a port sleep, which a mouse then has to wake.  The power plan switches to Windows' own High Performance scheme. The GUID of whatever plan you were on is recorded first, so revert puts you back on YOUR plan - not on Balanced, and not on a plan this tool invented.  On a laptop this will cost battery life. That is the trade, not a side effect.",
            _ => "Scheduling priority and how the GPU is driven. Two of these need a restart.  Hardware-accelerated GPU scheduling hands frame queueing to the GPU itself. Win32PrioritySeparation biases the scheduler towards whatever window is in front. The MMCSS Games task is the block Windows reads to decide what a game is allowed to ask for.  DisablePreemption is the one to watch on this row. It stops the GPU interrupting long draw calls, and on some drivers that shows up as resets or stutter rather than as smoothness. If something goes wrong after a restart, this group is the first thing to switch back off.",
        };
        b += "  ----  " + n + " setting" + (n == 1 ? "" : "s") + " in this group. ";
        if (Dop.IsOn(g.Id)) b += "Switching it off restores every one of them to the state captured when the hub first opened - the values that were there, or removal for anything that did not exist at all. Nothing is set to a guessed default.";
        else b += "Switching it on reads and saves the current value of every one of them before it writes anything, so switching it back off is a restore rather than a guess.";
        if (g.Rb) b += "  ----  Takes effect after a restart.";
        if (!Os.IsWin) b += "  ----  A Windows feature: on this system the switches are shown but do nothing.";
        Detail.OpenSheet(g.N, g.H, b, "DEVICE OPTIMIZATIONS");
    }
}
