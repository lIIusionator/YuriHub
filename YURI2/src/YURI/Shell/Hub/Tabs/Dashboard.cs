using Yuri.Core;
using Yuri.Gfx;
using Yuri.Platform;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Shell.Hub.Tabs;

/// <summary>
/// Tab 1. Three stat tiles (SESSION / TOTAL TIME / DAILY AVG), the SCREEN TIME
/// graph (24 hourly bars, "system on" in amber), WHAT'S NEW with the version
/// chip and CHECK FOR UPDATES, and the SYSTEMS / SCRIPTS tiles on the right.
/// The counts the tiles show come from the modules that own them
/// (SysArmedN, the script hub's list) through the hooks at the top.
/// </summary>
public static class Dashboard
{
    const uint AMBER = 0xFFFBBF24;
    // filled by their modules: how many systems are armed, what the tiles say
    public static Func<int> SysArmedN = () => 0;
    public static Func<int> ScriptsPlaced = () => 0;
    public static Func<int> ScriptsRunning = () => 0;
    public static Func<string?> SystemsSub = () => null;
    public static Func<bool> BlockActive = () => false;    // `enabled`
    public static Func<bool> BlockArmed = () => false;     // abOn

    public static void Draw(HubSurface hub, double x0, double y0, double dx, double dy2, double f, long now, uint hubCur)
    {
        var HL = hub.HL;
        double emb2G = (Math.Sin(DecT(now) * 0.004) + 1) / 2;
        long scrA = hub.Tab == 1 ? HL.scrAt : now - 99999;
        string[] labs = { "SESSION", "TOTAL TIME", "DAILY AVG" };
        string[] vals = { Usage.FmtUp(now - hub.StatStart), Usage.FmtHrs(Usage.AllTot), Usage.FmtHrs(Usage.Days != 0 ? Usage.AllTot / Usage.Days : 0) };
        string[] subs = { "this run", "all sessions", "screen time / day" };
        for (int k = 1; k <= 3; k++)
        {
            double sx_ = x0 + (k - 1) * (HL.stw + 12), sy_ = HL.sty + dy2;
            FillRR(sx_, sy_, HL.stw, HL.sth, 10, VBrush(sx_, sy_, HL.stw, HL.sth, FA(0xFF171A30, f), FA(0xFF12141F, f)));
            MiniBackdrop(sx_, sy_, HL.stw, HL.sth, 10, hubCur, f, now, 0.8);
            StrokeRR(sx_, sy_, HL.stw, HL.sth, 10, Pen(FA(0x22FFFFFF, f), 1));
            FillRR(sx_ + 12, sy_ + 10, 16, 2.4, 1.2, SBrush(FA(Alpha(hubCur, 170), f)));
            Txt(labs[k - 1], sx_ + 12, sy_ + 15, HL.stw - 24, 12, Fonts.fBadge, FA(0x76C7CBE0, f), Fmt.L);
            Txt(vals[k - 1], sx_ + 12, sy_ + 28, HL.stw - 24, 24, HL.fV, FA(Alpha(Mix(0xFFE8EAF6, hubCur, 0.18), 240), f), Fmt.L);
            Txt(subs[k - 1], sx_ + 12, sy_ + 49, HL.stw - 24, 12, HL.fXs, FA(0x4EC7CBE0, f), Fmt.L);
            if (!HubState.LowPerf)
                for (int j = 1; j <= 4; j++)
                {
                    double bh2 = 3 + 8 * Math.Abs(Math.Sin(DecT(now) * 0.0042 + j * 0.9 + k * 2.1));
                    FillRR(sx_ + HL.stw - 34 + (j - 1) * 6, sy_ + HL.sth - 12 - bh2, 3, bh2, 1.2, SBrush(FA(Alpha(hubCur, 55 + 45 * Math.Abs(Math.Sin(DecT(now) * 0.003 + j + k))), f)));
                }
            var pnC = Pen(FA(Alpha(hubCur, 60), f), 1);
            Line(sx_ + HL.stw - 12, sy_ + 8, sx_ + HL.stw - 6, sy_ + 8, pnC);
            Line(sx_ + HL.stw - 6, sy_ + 8, sx_ + HL.stw - 6, sy_ + 14, pnC);
        }
        // ---- SCREEN TIME ----
        double gx = x0, gy_ = HL.gy + dy2;
        FillRR(gx, gy_, HL.ctw, HL.gh, 10, VBrush(gx, gy_, HL.ctw, HL.gh, FA(0xFF161930, f), FA(0xFF10121C, f)));
        MiniBackdrop(gx, gy_, HL.ctw, HL.gh, 10, hubCur, f, now, 0.85);
        StrokeRR(gx, gy_, HL.ctw, HL.gh, 10, Pen(FA(0x1EFFFFFF, f), 1));
        FillRR(gx + 12, gy_ + 10, 16, 2.4, 1.2, SBrush(FA(Alpha(hubCur, 170), f)));
        Txt("SCREEN TIME", gx + 12, gy_ + 15, 120, 12, Fonts.fBadge, FA(0x76C7CBE0, f), Fmt.L);
        FillEll(gx + HL.ctw - 226, gy_ + 18, 6, 6, SBrush(FA(Alpha(hubCur, 210), f)));
        Txt("active", gx + HL.ctw - 216, gy_ + 14, 44, 14, Fonts.fHint, FA(0x5CC7CBE0, f), Fmt.L);
        FillEll(gx + HL.ctw - 166, gy_ + 18, 6, 6, SBrush(FA(Alpha(AMBER, 220), f)));
        Txt("system on", gx + HL.ctw - 156, gy_ + 14, 70, 14, Fonts.fHint, FA(0x5CC7CBE0, f), Fmt.L);
        Txt("today", gx + HL.ctw - 62, gy_ + 14, 50, 14, Fonts.fHint, FA(0x4EC7CBE0, f), Fmt.R);
        int curHr = Clock.CurHour();
        double maxu = 900;
        for (int j = 0; j < 24; j++) maxu = Math.Max(maxu, Usage.Tot[j]);
        double bx0 = gx + 16, slot = (HL.ctw - 32) / 24, baseY = gy_ + HL.gh - 22;
        var pnG = Pen(FA(0x14FFFFFF, f), 1);
        Line(bx0, gy_ + 40, bx0 + HL.ctw - 32, gy_ + 40, pnG);
        Line(bx0, baseY + 1.5, bx0 + HL.ctw - 32, baseY + 1.5, pnG);
        for (int j = 1; j <= 24; j++)
        {
            int vt = Usage.Tot[j - 1], vs = Usage.Sys[j - 1];
            double bh2 = (HL.gh - 68) * Math.Min(vt / maxu, 1.0);
            double bs2 = (HL.gh - 68) * Math.Min(vs / maxu, 1.0);
            bool cur3 = j == curHr;
            double bxj = bx0 + (j - 1) * slot;
            if (cur3) FillRR(bxj - 1, gy_ + 38, slot - 2, HL.gh - 58, 3, SBrush(FA(Alpha(hubCur, 16), f)));
            FillRR(bxj, baseY - Math.Max(bh2, 2), slot - 4.5, Math.Max(bh2, 2), 1.5, SBrush(FA(Alpha(cur3 ? AccHi(hubCur, 0.3) : hubCur, vt != 0 ? 100 + 130 * Math.Min(vt / maxu, 1.0) : 30), f)));
            if (bs2 > 0.5) FillRR(bxj, baseY - bs2, slot - 4.5, bs2, 1.5, SBrush(FA(Alpha(AMBER, 220), f)));
            if ((j - 1) % 6 == 0) Txt((j - 1).ToString(), bxj - 4, baseY + 4, 26, 12, HL.fXs, FA(0x46C7CBE0, f), Fmt.L);
        }
        Txt(Usage.FmtHrs(Usage.Tot[curHr - 1]) + " this hour", gx + HL.ctw - 176, baseY + 4, 160, 12, HL.fXs, FA(Alpha(hubCur, 130), f), Fmt.R);
        // ---- WHAT'S NEW ----
        double ly = HL.ly + dy2;
        double dbot = HubLayout.pd + HubLayout.ch - 34 + dy2;
        double colw = HL.ctw - 192;
        FillRR(x0, ly, colw, dbot - ly, 10, VBrush(x0, ly, colw, dbot - ly, FA(0xFF161930, f), FA(0xFF10121C, f)));
        MiniBackdrop(x0, ly, colw, dbot - ly, 10, hubCur, f, now, 0.85);
        StrokeRR(x0, ly, colw, dbot - ly, 10, Pen(FA(0x1CFFFFFF, f), 1));
        FillRR(x0 + 12, ly + 10, 16, 2.4, 1.2, SBrush(FA(Alpha(hubCur, 170), f)));
        Txt("WHAT'S NEW", x0 + 12, ly + 15, 130, 12, Fonts.fBadge, FA(0x76C7CBE0, f), Fmt.L);
        double zvx = x0 + colw - 12 - HL.zvw;
        FillRR(zvx, ly + 12, HL.zvw, 16, 5, SBrush(FA(Alpha(hubCur, 26), f)));
        Txt(AppInfo.ZVer, zvx, ly + 11, HL.zvw, 16, HL.fS, FA(Alpha(AccHi(hubCur, 0.4), 230), f), Fmt.C);
        FFMBtn(2001, zvx - 10 - HL.ubw, ly + 12, HL.ubw, 16, "CHECK FOR UPDATES", hubCur, f, 0, HL.fS);
        FadeLine(x0 + 12, x0 + colw - 12, ly + 33, 0x16FFFFFF, f);
        var log = Changelog.Entries;
        int nrows = Math.Min(7, log.Length);
        for (int i5 = 1; i5 <= nrows; i5++)
        {
            var en = log[log.Length - nrows + i5 - 1];
            double tdel = Math.Min(Math.Max((now - scrA - 140 - i5 * 40) / 240.0, 0.0), 1.0);
            double ta5 = 1 - Math.Pow(1 - tdel, 3);
            double eyd = ly + 40 + (i5 - 1) * 19 + (1 - ta5) * 5;
            uint dcol = en.kind == "+" ? hubCur : AMBER;
            FillEll(x0 + 13, eyd + 3, 9, 9, SBrush(FA(Alpha(dcol, 60 * ta5), f)));
            FillEll(x0 + 15.5, eyd + 5.5, 4, 4, SBrush(FA(Alpha(dcol, 220 * ta5), f)));
            Txt(en.text, x0 + 30, eyd - 1, colw - 44, 15, Fonts.fHint, FA(Alpha(0xC7CBE0, 70 + 90 * ta5), f), Fmt.L);
        }
        Txt("full history under UPDATE LOGS", x0 + 12, dbot - 22, colw - 24, 13, HL.fXs, FA(0x46C7CBE0, f), Fmt.L);
        FillEll(x0 + colw - 22, dbot - 19, 5, 5, SBrush(FA(Alpha(hubCur, 130 + 50 * emb2G), f)));
        // ---- SYSTEMS / SCRIPTS ----
        double rcx = x0 + colw + 14, rcw = HL.ctw - colw - 14;
        int sysN = SysArmedN();
        int placed = ScriptsPlaced(), runN = ScriptsRunning();
        for (int k = 1; k <= 2; k++)
        {
            double thd = (dbot - ly - 12) / 2;
            double tyd = ly + (k - 1) * (thd + 12);
            int valT = k == 1 ? sysN : placed;
            uint tcl2 = k == 1 ? (BlockActive() ? HubState.C_ON : BlockArmed() ? AMBER : sysN != 0 ? HubState.C_ON : hubCur) : (runN != 0 ? HubState.C_ON : hubCur);
            string subT = k == 1 ? (SystemsSub() ?? (runN != 0 ? "scripts live" : "all systems idle"))
                                 : $"{runN} running - {Math.Max(placed - runN, 0)} idle";
            FillRR(rcx, tyd, rcw, thd, 10, VBrush(rcx, tyd, rcw, thd, FA(0xFF171A30, f), FA(0xFF12141F, f)));
            MiniBackdrop(rcx, tyd, rcw, thd, 10, tcl2, f, now, 0.85);
            StrokeRR(rcx, tyd, rcw, thd, 10, Pen(FA(Alpha(tcl2, 40 + 30 * emb2G), f), 1));
            FillRR(rcx + 12, tyd + 10, 16, 2.4, 1.2, SBrush(FA(Alpha(tcl2, 170), f)));
            Txt(k == 1 ? "SYSTEMS" : "SCRIPTS", rcx + 12, tyd + 15, rcw - 40, 12, Fonts.fBadge, FA(0x76C7CBE0, f), Fmt.L);
            FillEll(rcx + rcw - 22, tyd + 14, 6, 6, SBrush(FA(Alpha(tcl2, 160 + 70 * Math.Abs(Math.Sin(DecT(now) * 0.0032 + k))), f)));
            Txt(valT.ToString(), rcx + 12, tyd + 28, 60, 30, HL.fV, FA(Alpha(Mix(0xFFE8EAF6, tcl2, 0.3), 245), f), Fmt.L);
            Txt(k == 1 ? "enabled" : "placed", rcx + 16 + Fonts.MeasureW(valT.ToString(), HL.fV), tyd + 38, 70, 14, HL.fXs, FA(0x66C7CBE0, f), Fmt.L);
            Txt(subT, rcx + 12, tyd + thd - 22, rcw - 60, 13, HL.fXs, FA(Alpha(tcl2, 140), f), Fmt.L);
            if (!HubState.LowPerf)
                for (int j = 1; j <= 4; j++)
                {
                    int live = k == 1 ? sysN : runN;
                    double bh2 = 3 + 9 * Math.Abs(Math.Sin(DecT(now) * 0.0042 + j * 0.9 + k * 2.1)) * (live != 0 ? 1 : 0.35);
                    FillRR(rcx + rcw - 40 + (j - 1) * 7, tyd + thd - 12 - bh2, 3, bh2, 1.2, SBrush(FA(Alpha(tcl2, 55 + 55 * Math.Abs(Math.Sin(DecT(now) * 0.003 + j + k))), f)));
                }
        }
    }

    /// <summary>The head's two pills: OPEN ROBLOX (205) and TUTORIAL (2207), drawn with the title.</summary>
    public static void Head(HubSurface hub, double x0, double y0, double f, long now, uint hubCur)
    {
        var HL = hub.HL;
        HubLaunchBtn(x0 + HL.ctw - 182, y0 - 3, 142, 24, f, now, hubCur, Roblox.IsRunning());
        HubTourBtn(x0 + HL.ctw - 182 - 8 - 96, y0 - 3, 96, 24, f, now, hubCur);
    }

    public static int Zone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        double x0 = HL.ctx;
        if (ux >= x0 + HL.ctw - 182 && ux <= x0 + HL.ctw - 40 && uy >= HL.cty - 3 && uy <= HL.cty + 21) return 205;
        if (ux >= x0 + HL.ctw - 286 && ux <= x0 + HL.ctw - 190 && uy >= HL.cty - 3 && uy <= HL.cty + 21) return 2207;
        if (ux >= x0 + (HL.ctw - 192) - 22 - HL.zvw - HL.ubw && ux <= x0 + (HL.ctw - 192) - 22 - HL.zvw && uy >= HL.ly + 12 && uy <= HL.ly + 28) return 2001;
        return 0;
    }

    public static bool Click(HubSurface hub, int z)
    {
        long now = Clock.Tick;
        if (z == 205)
        {
            hub.ClickAt[205] = now;
            Roblox.Launch();                      // SPFSay lands with the SPECIAL FEATURES module (phase 3)
            return true;
        }
        if (z == 2207) { hub.ClickAt[2207] = now; Tut.Start(false); return true; }    // THE TOUR
        if (z == 2001) { Upd.CheckStart(); return true; }             // UpdCheckStart: the update card
        return false;
    }
}
