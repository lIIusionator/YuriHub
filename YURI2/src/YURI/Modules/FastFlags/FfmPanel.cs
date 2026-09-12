using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Yuri.Core;
using Yuri.Gfx;
using Yuri.Shell.Hub;
using Yuri.Shell.Hub.Tabs;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Modules.FastFlags;

/// <summary>
/// FFMSystems: module 1's SYSTEMS panel. Nine rows — INJECT LIVE, SINGLETON,
/// RE-APPLY, AUTO INJECT, CLEAN, UPDATE FLAGS, then under PRESETS: INSERT
/// HITBOX, INSERT 30HZ HITBOX, COMMUNITY FLAGS — the DATABASE / IMPORT /
/// EXPORT / CLEAR / LOGS bar, the filter-or-add field, the staged list and
/// the status line. Zone ids are the .ahk's (415-425, 436, 442, 45x-48x,
/// 50x-54x). The injector rows read the PROCESS MEMORY ENGINE, which is
/// Windows-only and a later phase: without it the client is never attached,
/// INJECT reads NO RBX and the toggles only keep their settings. The
/// DATABASE, LOGS and COMMUNITY views are the next phase; their buttons
/// register a press and nothing else yet.
/// </summary>
public static class FfmPanel
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_ACC = 0xFFFB7185;
    public const int SYSN = 9, SEPAT = 7, SEPH = 26;
    static readonly string[] Names = { "INJECT LIVE", "SINGLETON", "RE-APPLY", "AUTO INJECT", "CLEAN", "UPDATE FLAGS", "INSERT HITBOX", "INSERT 30HZ HITBOX", "COMMUNITY FLAGS" };
    static readonly string[] Hints = { "write flags into Roblox memory", "find the flag table by code signature", "hold what you injected - rewrite it every 2 s",
        "inject 4 s after Roblox is detected", "export your flags without the failed ones", "rename outdated flags to their current names",
        "replace the staged set with HITBOX", "replace the staged set with 30HZ HITBOX", "browse flag sets shared by other players" };
    static readonly bool[] IsBtn = { true, false, false, false, true, true, true, true, true };

    // ---- the injector's settings ([ffm] in zeal.ini) and their eases ----
    public static bool UseSingleton = true, AutoInject, ReApply;     // ffUseSingleton / ffAutoInject / ffReApply
    public static double SglT, RepT, AijT;                          // FFM.sglT / repT / aijT
    public static int Pid => FfmEngine.Pid;                        // FFM.pid: the attached client
    public static int InjectSucc, InjectFail; public static long WrAt;
    public static int FailedCount => FfmEngine.Failed.Count; public static int OrigCount => FfmEngine.Orig.Count;
    public static bool AnyInjected => FfmEngine.AnyInjected();
    public static double Scr, ScrT, SysScr, SysScrT;                // FFM.scr / scrT / sysScr / sysScrT
    static bool _busyPick;

    public static void Register()
    {
        UseSingleton = Ini.ReadInt(Paths.IniFile, "ffm", "singleton", 1) != 0;
        AutoInject = Ini.ReadInt(Paths.IniFile, "ffm", "autoinject", 0) != 0;
        ReApply = Ini.ReadInt(Paths.IniFile, "ffm", "reapply", 0) != 0;
        SglT = UseSingleton ? 1 : 0; AijT = AutoInject ? 1 : 0; RepT = ReApply ? 1 : 0;
        FfmEngine.ManageReApply();                                          // the watchdog follows the saved setting
        Integrations.Panels[1] = Draw;
        Integrations.PanelZones[1] = Zone;
        Integrations.PanelClicks[1] = Click;
        Integrations.PanelWheels[1] = Wheel;
        Integrations.PanelOverlays[1] = (hub, x0, y0, dx, dy2, f, now, acc) =>
        {
            if (FfmViews.View == "" && now - FfmViews.ViewAt >= 300) return;
            FfmViews.DbView(hub, f, now, acc, dx, dy2);
            FfmCommView.Draw(hub, f, now, acc, dx, dy2);
            FfmViews.LogView(hub, f, now, acc, dx, dy2);
        };
        Platform.GameInfo.Changed = () => HubSurface.Live?.Tim(Pace.TICK_A);
        Integrations.FfmStaged = () => Ffm.Flags.Count;
        Integrations.FfmPid = () => Pid;
        Ffm.SelReset = () => { FfmField.Blur(); ScrT = 0.0; Scr = 0.0; };
        Integrations.FfmInjectFail = () => InjectFail;
        FfmField.TextX = mode => mode == "q" ? HubSurface.Live!.HL.abx + 26 : mode == "db" ? HubSurface.Live!.HL.ctx + 46
            : (mode == "gl" || mode == "pl") ? HubSurface.Live!.HL.abx + 64
            : mode is "sa" or "sl" or "rn" ? Special.SpfSaved.FieldX(HubSurface.Live!.HL, mode)
            : mode == "at" ? HubSurface.Live!.HL.ctx + 240
            : mode is "rfn" or "rfv" or "rfe" or "rfd" ? ClientSettings.RSetFx.TextX(HubSurface.Live!.HL, mode)
            : mode is "pn" or "pb" ? Shell.Hub.EditProfile.TextX(HubSurface.Live!.HL, mode) : HubSurface.Live!.HL.abx + HubSurface.Live!.HL.abw - 164;
        FfmField.OnSync = mode => { if (mode == "q") ScrT = 0.0; else if (mode == "db") FfmViews.DbSync(); else if (mode == "gl" || mode == "pl") Special.SpfPanel.FieldSync(mode); else if (mode == "rfd") ClientSettings.RSetFx.DbSync(); };
        FfmField.OnEnter = mode => { if (mode == "db") FfmViews.DbEnter(); else if (mode == "gl" || mode == "pl" || mode == "at") FfmField.End(true); else if (mode is "sa" or "sl" or "rn") Special.SpfSaved.Enter(mode); else if (mode is "rfn" or "rfv" or "rfe" or "rfd") ClientSettings.RSetFx.Enter(mode); else if (mode == "sn") { FfmField.End(true); FfmField.Begin("sd", 0); } else if (mode == "pn") { FfmField.End(true); FfmField.Begin("pb", 0); } else if (mode == "sd") { FfmField.End(true); if (ScriptHub.Scr.CeIdx != 0) ScriptHub.Scr.FocusSet(1); } else FfmField.End(true); };
    }

    // ---- geometry (FFMSysRowY, FFMSysTotal, FFMMaxScr) ----
    public static double SysRowY(HubLayout HL, int i) => 12 + (i - 1) * HL.ffrg + (i >= SEPAT ? SEPH : 0);
    public static double SysTotal(HubLayout HL) => SYSN * HL.ffrg + SEPH;
    public static double MaxScr(HubLayout HL, int count, int vis) => Math.Max(0.0, count * HL.ffrh - vis * HL.ffrh);
    static string Q() => FfmField.Edit == "q" ? FfmField.Buf.Trim() : Ffm.Q2;
    static List<int> FlagList() => Ffm.FlagList(Q());

    /// <summary>The per-frame eases: the scrolls and the three toggles.</summary>
    public static void Tick(HubLayout HL)
    {
        ScrT = Clamp(ScrT, 0.0, MaxScr(HL, FlagList().Count, HL.ffrows));
        Scr += (ScrT - Scr) * EK(0.3); if (Math.Abs(Scr - ScrT) < 0.3) Scr = ScrT;
        SysScrT = Clamp(SysScrT, 0.0, Math.Max(0.0, SysTotal(HL) - (HL.ffh - 20)));
        SysScr += (SysScrT - SysScr) * EK(0.3); if (Math.Abs(SysScr - SysScrT) < 0.3) SysScr = SysScrT;
        SglT += ((UseSingleton ? 1.0 : 0.0) - SglT) * EK(0.22);
        RepT += ((ReApply ? 1.0 : 0.0) - RepT) * EK(0.22);
        AijT += ((AutoInject ? 1.0 : 0.0) - AijT) * EK(0.22);
        FfmViews.Tick(HL);
    }
    public static bool Animating => Math.Abs(Scr - ScrT) > 0.3 || Math.Abs(SysScr - SysScrT) > 0.3 || FfmField.Edit != "" || Ffm.Flash.Count > 0
        || (Ffm.MsgAt != 0 && Clock.Tick - Ffm.MsgAt < 5300) || FfmViews.Animating || FfmViews.Loading;

    // ---- FFMSystems ----
    public static void Draw(HubSurface hub, double ax, double ay, double x0, double y0, double dx, double dy2, double ff, long now, uint acc)
    {
        var HL = hub.HL;
        Tick(HL);
        double mt = HL.modT;
        Txt("SYSTEMS", ax, y0 + 36, 120, 14, Fonts.fBadge, FA(0x62C7CBE0, ff), Fmt.L);
        Txt(mt > 0.5 ? SYSN + " listed" : "0 listed", ax + 66, y0 + 36, 80, 14, HL.fS, FA(Alpha(acc, 60 + 120 * mt), ff), Fmt.L);
        bool rbx = Platform.Roblox.IsRunning();
        if (mt > 0.4)
        {
            double fh = ff * Clamp((mt - 0.4) / 0.4, 0.0, 1.0);
            FFMBtn(421, ax + HL.abw - 96, y0 + 30, 96, 22, rbx ? "CLOSE RBX" : "RBX OFFLINE", acc, fh, rbx ? 2 : 0);
        }
        double pah = 26 + (HL.ffh - 26) * mt;
        FillRR(ax, ay, HL.abw, pah, 12, VBrush(ax, ay, HL.abw, pah, FA(Mix(0xFF141728, 0xFF181C34, mt), ff), FA(0xFF12141F, ff)));
        MiniBackdrop(ax, ay, HL.abw, pah, 12, acc, ff, now, 0.9);
        Integrations.HubEmptyGhost(hub, ax, ay, pah, ff);                // the collapsed state, fading back in
        StrokeRR(ax, ay, HL.abw, pah, 12, Pen(FA(Alpha(acc, 70 + 90 * mt), ff), 1.2));
        double gl = 40 * mt;
        var pnG = Pen(FA(Alpha(AccHi(acc, 0.4), 200 * mt), ff), 1.6);
        Line(ax + 14, ay + 0.6, ax + 14 + gl, ay + 0.6, pnG);
        Line(ax + HL.abw - 14 - gl, ay + pah - 0.6, ax + HL.abw - 14, ay + pah - 0.6, pnG);
        ModRing(ax, ay, HL.abw, pah, 12, acc, ff, HL.modAt, now, 700, 18, hub.HubMod != 0);
        if (mt < 0.02) return;
        int stFS = PushG();
        ClipRR(ax, ay, HL.abw, pah, 12);
        double[] ons = { 0, SglT, RepT, AijT, 0, 0, 0, 0, 0 };
        string[] subs = { "", UseSingleton ? "ON" : "OFF", ReApply ? "ON" : "OFF", AutoInject ? "ON" : "OFF", "", "", "", "", "" };
        SysDivider(hub, ax, ay, ff, mt, now, acc);
        InstTabs(hub, ax, ay, ff, mt, now, acc);
        for (int i = 1; i <= SYSN; i++)
        {
            double cg = Clamp((mt - 0.30 - Math.Min(i, 4) * 0.07) / 0.38, 0.0, 1.0);
            if (cg <= 0.01) continue;
            double fb = ff * cg;
            double ry = ay + SysRowY(HL, i) - SysScr;
            if (ry + 36 < ay + 4 || ry > ay + pah - 4) continue;
            double hv = Math.Max(hub.Hv(450 + i), hub.Hv(470 + i));
            double sld = (1 - cg) * 10;
            double axs = ax + sld;                                     // row CONTENT rides the entrance slide; panel chrome does not
            bool live = i == 1 ? Ffm.Flags.Count > 0 : i == 5 ? FailedCount > 0 : i == 6 ? Ffm.Flags.Count > 0 : i >= 7 || ons[i - 1] > 0.5;
            if (hv > 0.01) FillRR(ax + 6, ry, HL.abw - 12, 36, 8, HBrush(ax + 6, ry, HL.abw - 12, 36, FA(Alpha(0xFFFFFF, 15 * hv), fb), FA(Alpha(0xFFFFFF, 3 * hv), fb)));
            FillRR(axs + 14, ry + 10, 3, 16, 1.5, VBrush(axs + 14, ry + 10, 3, 16, FA(Alpha(live ? AccHi(acc, 0.35) : 0xFFC7CBE0, live ? 235 : 60), fb), FA(Alpha(live ? acc : 0xFFC7CBE0, live ? 150 : 40), fb)));
            SysIcon(i, axs + 36, ry + 18, FA(Alpha(live ? acc : 0xFF9AA8C0, live ? 220 : 110), fb), live, now);
            double tw = (i == 1 ? HL.abw - 184 : IsBtn[i - 1] ? HL.abw - 118 : HL.abw - 158) - 54;
            Txt(Names[i - 1], axs + 54, ry + 4, tw, 16, Fonts.fBadge, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.18), 235), fb), Fmt.L);
            if (i == 2)
            {
                bool rOn = UseSingleton;
                string rLbl = "RECOMMENDED";
                double rtw = Fonts.MeasureW(rLbl, HL.fS);
                double rbx2 = axs + 54 + Fonts.MeasureW(Names[1], Fonts.fBadge) + 10;
                double rbw2 = 22 + rtw;
                if (rbx2 + rbw2 < axs + 54 + tw)
                {
                    double gl2 = rOn ? (0.55 + 0.45 * (Math.Sin(DecT(now) * 0.0024) + 1) / 2) : 0.0;
                    FillRR(rbx2, ry + 4, rbw2, 15, 5, SBrush(FA(Alpha(acc, R(rOn ? 30 + 22 * gl2 : 12)), fb)));
                    StrokeRR(rbx2, ry + 4, rbw2, 15, 5, Pen(FA(Alpha(acc, R(rOn ? 120 + 70 * gl2 : 60)), fb), 1));
                    FillEll(rbx2 + 6, ry + 9.5, 4, 4, SBrush(FA(Alpha(rOn ? C_ON : 0xFF9AA8C0, rOn ? 210 : 90), fb)));
                    Txt(rLbl, rbx2 + 14, ry + 3, rtw + 4, 15, HL.fS, FA(Alpha(AccHi(acc, 0.35), rOn ? 235 : 120), fb), Fmt.L);
                }
            }
            // Off Windows there is no memory engine, so INJECT LIVE writes
            // ClientAppSettings.json instead - and rows 2 to 4 drive the engine
            // and nothing else. Saying "write flags into Roblox memory" there
            // describes a thing the build cannot do.
            bool eng = !Yuri.Platform.Os.IsWin && i >= 2 && i <= 4;
            string hint = !Yuri.Platform.Os.IsWin && i == 1 ? "write flags into the client's settings file"
                        : eng ? "needs the process memory engine - windows only"
                        : i == 1 && Pid != 0 ? $"write flags into CLIENT  -  pid {Pid}" : Hints[i - 1];
            Txt(FFMElide(hint, Fonts.fHint, tw), axs + 54, ry + 20, tw, 13, Fonts.fHint, FA(eng ? 0x4EC7CBE0u : 0x7EC7CBE0u, fb), Fmt.L);
            if (IsBtn[i - 1])
            {
                if (i == 1)
                {
                    double wp = (WrAt != 0 && now - WrAt < 700) ? 1 - (now - WrAt) / 700.0 : 0;
                    if (wp > 0.01) StrokeRR(axs + HL.abw - 174 - 8 * wp, ry + 5 - 4 * wp, 76 + 16 * wp, 26 + 8 * wp, 6 + 2 * wp, Pen(FA(Alpha(C_ON, 170 * wp), fb), 2 * (1 - wp) + 0.5));
                    bool file = !Yuri.Platform.Os.IsWin;
                    FFMBtn(461, axs + HL.abw - 174, ry + 5, 76, 26, file ? "WRITE" : Pid != 0 ? "INJECT" : "NO RBX", acc, fb, file || Pid != 0 ? 1 : 0);
                    FFMBtn(469, axs + HL.abw - 92, ry + 5, 76, 26, file ? "CLEAR" : "UNINJECT", acc, fb, file || AnyInjected ? 1 : 0);
                }
                else if (i == 5)
                {
                    if (FailedCount != 0) Txt(FailedCount + " failed", axs + HL.abw - 176, ry + 11, 62, 14, HL.fXs, FA(Alpha(acc, 200), fb), Fmt.R);
                    FFMBtn(485, axs + HL.abw - 108, ry + 5, 92, 26, "CLEAN", acc, fb, (FailedCount != 0 || OrigCount != 0) ? 1 : 0);
                }
                else if (i == 6) FFMBtn(486, axs + HL.abw - 108, ry + 5, 92, 26, FfmUpdate.Updating ? "UPDATING" : "UPDATE", acc, fb, Ffm.Flags.Count != 0 ? 1 : 0);
                else if (i == 7 || i == 8) FFMBtn(480 + i, axs + HL.abw - 108, ry + 5, 92, 26, "INSERT", acc, fb, 0);
                else FFMBtn(480 + i, axs + HL.abw - 108, ry + 5, 92, 26, "BROWSE", acc, fb, 1);
            }
            else
            {
                Txt(subs[i - 1], axs + HL.abw - 148, ry + 11, 70, 14, HL.fXs, FA(Alpha(ons[i - 1] > 0.5 ? C_ON : 0xFF9AA8C0, ons[i - 1] > 0.5 ? 200 : 95), fb), Fmt.R);
                FFMTogDraw(axs + HL.abw - 62, ry + 7, 44, 22, ons[i - 1], acc, hv, fb);
            }
        }
        double sysTot = SysTotal(HL), sysVis = HL.ffh - 20;
        if (sysTot > sysVis)
        {
            double sbS = Math.Max(hub.Hv(442), HL.drag == 12 ? 1.0 : 0.0);
            double sw2 = 3 + 3 * sbS;
            double sxs = ax + HL.abw - 6 - 3 * sbS;
            FillRR(sxs, ay + 10, sw2, sysVis, sw2 / 2, SBrush(FA(Alpha(0xFFFFFF, 18 + 26 * sbS), ff * mt)));
            double th2 = Math.Max(22, sysVis * (sysVis / sysTot));
            double ty2 = ay + 10 + (sysVis - th2) * (SysScr / Math.Max(1, sysTot - sysVis));
            FillRR(sxs, ty2, sw2, th2, sw2 / 2, VBrush(sxs, ty2, sw2, th2, FA(Alpha(AccHi(acc, 0.35), 190 + 65 * sbS), ff * mt), FA(Alpha(acc, 170 + 60 * sbS), ff * mt)));
        }
        Pop(stFS);
        double ca = Clamp((mt - 0.62) / 0.30, 0.0, 1.0);
        if (ca <= 0.01) return;
        double fAct = ff * ca;
        double by = ay + HL.ffh + 12 + (1 - ca) * 8;
        string[] labs = { "DATABASE", "IMPORT", "EXPORT", "CLEAR", "LOGS" };
        for (int i = 1; i <= 5; i++) FFMBtn(i == 5 ? 425 : 415 + i, ax + (i - 1) * 82, by, 74, 26, labs[i - 1], acc, fAct, i == 4 ? 3 : 0);
        double cq = Clamp((mt - 0.70) / 0.28, 0.0, 1.0);
        if (cq <= 0.01) return;
        double fq = ff * cq;
        double qy = ay + HL.ffh + 46 + (1 - cq) * 6;
        bool editing = FfmField.Edit == "q";
        string qTxt = editing ? FfmField.Buf : Ffm.Q2;
        double hvq = hub.Hv(422);
        FillRR(ax, qy, HL.abw - 34, 24, 6, SBrush(FA(0xFF0C0E17, fq)));
        MicroBackdrop(ax, qy, HL.abw - 34, 24, 6, acc, fq, now, 0.75);
        StrokeRR(ax, qy, HL.abw - 34, 24, 6, Pen(FA(editing ? Alpha(acc, 200) : Alpha(0xFFFFFF, 22 + 26 * hvq), fq), editing ? 1.3 : 1));
        var pnQ = Pen(FA(Alpha(editing ? acc : 0xFF9AA8C0, editing ? 210 : 110 + 60 * hvq), fq), 1.3);
        Ell(ax + 9, qy + 7, 9, 9, pnQ);
        Line(ax + 17, qy + 15, ax + 20, qy + 18, pnQ);
        double qMax = HL.abw - 172;
        string qVis = qTxt; int qOff = 0;
        while (Fonts.MeasureW(qVis, HL.fM) > qMax && qVis.Length > 1) { qVis = qVis[1..]; qOff++; }
        if (qTxt == "") Txt("filter staged  \u00B7  or type a flag name to add", ax + 26, qy, qMax, 24, Fonts.fHint, FA(0x58C7CBE0, fq), Fmt.L);
        if (editing) FfmField.Paint(ax + 26, qy, 24, qVis, qOff, acc, fq, now);
        if (qTxt != "") Txt(qVis, ax + 26, qy, qMax, 24, HL.fM, FA(Alpha(0xFFE8EAF6, 235), fq), Fmt.L);
        var lst = FlagList();
        if (qTxt != "") Txt(lst.Count + " match" + (lst.Count == 1 ? "" : "es"), ax + HL.abw - 128, qy, 90, 24, HL.fS, FA(0x66C7CBE0, fq), Fmt.R);
        double hva = hub.Hv(423);
        FillRR(ax + HL.abw - 30, qy, 30, 24, 6, VBrush(ax + HL.abw - 30, qy, 30, 24, FA(Mix(0xFF171A30, 0xFF232744, hva), fq), FA(0xFF12141F, fq)));
        MicroBackdrop(ax + HL.abw - 30, qy, 30, 24, 6, acc, fq, now, 0.9);
        StrokeRR(ax + HL.abw - 30, qy, 30, 24, 6, Pen(FA(Alpha(acc, 90 + 120 * hva), fq), 1));
        var pnP = Pen(FA(Alpha(AccHi(acc, 0.3), 200 + 55 * hva), fq), 1.7);
        Line(ax + HL.abw - 21, qy + 12, ax + HL.abw - 9, qy + 12, pnP);
        Line(ax + HL.abw - 15, qy + 6, ax + HL.abw - 15, qy + 18, pnP);
        double cl = Clamp((mt - 0.76) / 0.24, 0.0, 1.0);
        if (cl <= 0.01) return;
        double fl2 = ff * cl;
        double ly = ay + HL.ffh + 76, lh = HL.fflh;
        FillRR(ax, ly, HL.abw, lh, 10, VBrush(ax, ly, HL.abw, lh, FA(0xFF10131F, fl2), FA(0xFF0C0E17, fl2)));
        MiniBackdrop(ax, ly, HL.abw, lh, 10, acc, fl2, now, 0.7);
        StrokeRR(ax, ly, HL.abw, lh, 10, Pen(FA(0x18FFFFFF, fl2), 1));
        if (lst.Count == 0)
        {
            string msg = Ffm.Flags.Count != 0 ? "no flags match that filter" : "nothing staged yet";
            string sub = Ffm.Flags.Count != 0 ? "clear the filter to see all " + Ffm.Flags.Count : "open DATABASE, IMPORT a file, or type a name above";
            Txt(msg, ax, ly + lh / 2 - 16, HL.abw, 16, Fonts.fHint, FA(0x74C7CBE0, fl2), Fmt.C);
            Txt(sub, ax, ly + lh / 2 + 2, HL.abw, 15, HL.fS, FA(0x4EC7CBE0, fl2), Fmt.C);
            Status(hub, ax, ly + lh + 10, fl2, now, acc);
            return;
        }
        int stFL = PushG();
        ClipRR(ax + 1, ly + 4, HL.abw - 2, lh - 8, 9);
        int first = (int)Math.Floor(Scr / HL.ffrh) + 1;
        for (int k = 1; k <= HL.ffrows + 1; k++)
        {
            int vi = first + k - 1;
            if (vi < 1 || vi > lst.Count) continue;
            int idx = lst[vi - 1];
            var fg = Ffm.Flags[idx];
            double ry = ly + 6 + (vi - 1) * HL.ffrh - Scr;
            double hvR = hub.Hv(520 + k), hvV = hub.Hv(540 + k), hvX = hub.Hv(500 + k);
            double hAny = Math.Max(hvR, Math.Max(hvV, hvX));
            double fpu = 0.0;
            if (Ffm.Flash.TryGetValue(idx, out var flAt))
            {
                double e = (now - flAt) / 620.0;
                if (e >= 1) Ffm.Flash.Remove(idx); else fpu = 1 - e;
            }
            if (hAny > 0.01) FillRR(ax + 3, ry, HL.abw - 16, HL.ffrh - 2, 6, HBrush(ax + 3, ry, HL.abw - 16, HL.ffrh - 2, FA(Alpha(0xFFFFFF, 16 * hAny), fl2), FA(Alpha(0xFFFFFF, 3 * hAny), fl2)));
            if (fpu > 0.01) FillRR(ax + 3, ry, HL.abw - 16, HL.ffrh - 2, 6, SBrush(FA(Alpha(acc, 70 * fpu), fl2)));
            uint bcol = fg.Type == "bool" ? (fg.Value.Equals("true", StringComparison.OrdinalIgnoreCase) ? C_ON : 0xFF6E7590) : AMBER;
            bool onF = fg.On;
            double hvP = hub.Hv(510 + k);
            if (hvP > 0.02) FillEll(ax + 4, ry + 3, HL.ffrh - 8, HL.ffrh - 8, SBrush(FA(Alpha(0xFFFFFF, R(14 * hvP)), fl2)));
            double pcx = ax + 4 + (HL.ffrh - 8) / 2, pcy = ry + 3 + (HL.ffrh - 8) / 2;
            if (onF) FillEll(pcx - 3.6, pcy - 3.6, 7.2, 7.2, SBrush(FA(Alpha(bcol, 225), fl2)));
            else Ell(pcx - 3.4, pcy - 3.4, 6.8, 6.8, Pen(FA(Alpha(0xFFC7CBE0, 120 + 90 * hvP), fl2), 1.3));
            double fr = fl2 * (onF ? 1.0 : 0.55);                        // the rest of the row, dimmed when off
            string pfx = Ffm.PfxOf(fg.Name);
            string bare = pfx != "" ? Ffm.BareOf(fg.Name) : fg.Name;
            double nx = ax + 20;
            if (pfx != "")
            {
                Txt(pfx, nx, ry, 60, HL.ffrh - 2, HL.fM, FA(Alpha(acc, 150), fr), Fmt.L);
                nx += Fonts.MeasureW(pfx, HL.fM) + 1;
            }
            Txt(FFMElide(bare, HL.fM, (ax + HL.abw - 176) - nx), nx, ry, (ax + HL.abw - 176) - nx, HL.ffrh - 2, HL.fM, FA(Alpha(0xFFE8EAF6, 200 + 45 * hAny), fr), Fmt.L);
            bool vEdit = FfmField.Edit == "v" && FfmField.EditRow == idx;
            string vTxt = vEdit ? FfmField.Buf : fg.Value;
            if (vEdit)
            {
                FillRR(ax + HL.abw - 170, ry + 2, 82, HL.ffrh - 6, 5, SBrush(FA(Alpha(acc, 30), fr)));
                MicroBackdrop(ax + HL.abw - 170, ry + 2, 82, HL.ffrh - 6, 5, acc, fr, now, 0.75);
                StrokeRR(ax + HL.abw - 170, ry + 2, 82, HL.ffrh - 6, 5, Pen(FA(Alpha(acc, 190), fr), 1.1));
            }
            else if (hvV > 0.02) StrokeRR(ax + HL.abw - 170, ry + 2, 82, HL.ffrh - 6, 5, Pen(FA(Alpha(0xFFFFFF, 44 * hvV), fr), 1));
            string vVis = vTxt; int vOff = 0;
            while (Fonts.MeasureW(vVis, HL.fM) > 74 && vVis.Length > 1) { vVis = vVis[1..]; vOff++; }
            if (vEdit) FfmField.Paint(ax + HL.abw - 164, ry, HL.ffrh - 2, vVis, vOff, acc, fr, now);
            Txt(vEdit ? vVis : FFMElide(vTxt, HL.fM, 74), ax + HL.abw - 164, ry, 74, HL.ffrh - 2, HL.fM, FA(Alpha(AccHi(bcol, 0.25), 230), fr), Fmt.L);
            TypePill(hub, ax + HL.abw - 84, ry + 5, fg.Type, fr);
            var pnX = Pen(FA(Alpha(hvX > 0.2 ? acc : 0xFFC7CBE0, 90 + 150 * hvX), fr), 1.4);
            double cxx = ax + HL.abw - 26, cyy = ry + (HL.ffrh - 2) / 2;
            Line(cxx - 4, cyy - 4, cxx + 4, cyy + 4, pnX);
            Line(cxx + 4, cyy - 4, cxx - 4, cyy + 4, pnX);
        }
        Pop(stFL);
        double tot = lst.Count * HL.ffrh, view = HL.ffrows * HL.ffrh;
        if (tot > view)
        {
            double sbA = Math.Max(hub.Hv(436), HL.drag == 6 ? 1.0 : 0.0);
            double sw = 3 + 3 * sbA;
            double sx2 = ax + HL.abw - 5 - 3 * sbA;
            FillRR(sx2, ly + 6, sw, lh - 12, sw / 2, SBrush(FA(Alpha(0xFFFFFF, 20 + 26 * sbA), fl2)));
            double thmb = Math.Max(22, (lh - 12) * (view / tot));
            double ty = ly + 6 + (lh - 12 - thmb) * (Scr / Math.Max(1, tot - view));
            FillRR(sx2, ty, sw, thmb, sw / 2, VBrush(sx2, ty, sw, thmb, FA(Alpha(AccHi(acc, 0.35), 190 + 65 * sbA), fl2), FA(Alpha(acc, 170 + 60 * sbA), fl2)));
        }
        Status(hub, ax, ly + lh + 10, fl2, now, acc);
    }

    /// <summary>FFMStatus: the status dot and line, fading after four seconds; the injection badge while fresh.</summary>
    public static void Status(HubSurface hub, double ax, double sy, double ff, long now, uint acc)
    {
        var HL = hub.HL;
        uint col = Ffm.MsgCol == C_ACC ? acc : (Ffm.MsgCol != 0 ? Ffm.MsgCol : acc);
        double fade = Ffm.MsgAt != 0 ? Clamp(1 - (now - Ffm.MsgAt - 4000) / 1200.0, 0.15, 1.0) : 0.35;
        double pu = Math.Pow(Math.Max(0.0, Math.Sin((DecT(now) % 1700) / 1700.0 * 6.283)), 4);
        FillEll(ax + 1, sy + 1, 12, 12, SBrush(FA(Alpha(col, R((40 + 60 * pu) * fade)), ff)));
        FillEll(ax + 4, sy + 4, 6, 6, SBrush(FA(Alpha(col, R(235 * fade)), ff)));
        double msgW = FfmViews.Loading ? HL.abw - 164 : HL.abw - 34;
        Txt(FFMElide(Ffm.Msg, Fonts.fBadge, msgW - 4), ax + 20, sy, msgW, 16, Fonts.fBadge, FA(Alpha(AccHi(col, 0.25), R(230 * fade)), ff), Fmt.L);
        if (FfmViews.Loading)
        {
            var pnL = Pen(FA(Alpha(acc, 200), ff), 1.6);
            PenDash(pnL, 1); PenDashOff(pnL, (DecT(now) * 0.06) % 1000);
            Ell(ax + HL.abw - 16, sy - 1, 14, 14, pnL);
            Txt("loading database", ax + HL.abw - 136, sy, 112, 15, HL.fS, FA(0x60C7CBE0, ff), Fmt.R);
        }
        if (WrAt != 0 && now - WrAt < 8000)
        {
            double rf = Clamp(1 - (now - WrAt - 5000) / 2000.0, 0.0, 1.0);
            if (rf > 0.01)
            {
                int total = InjectSucc + InjectFail;
                uint rc = InjectFail == 0 ? C_ON : (InjectSucc == 0 ? acc : AMBER);
                string badge = $"{InjectSucc}/{total} OK";
                double bw = Fonts.MeasureW(badge, HL.fS) + 12;
                double bx = ax + HL.abw - bw - 6;
                FillRR(bx, sy, bw, 15, 4, SBrush(FA(Alpha(rc, R(40 * rf)), ff)));
                Txt(badge, bx, sy - 1, bw, 16, HL.fS, FA(Alpha(rc, R(210 * rf)), ff), Fmt.C);
            }
        }
    }

    /// <summary>FFMTypePill: BOOL green, INT sky, FLOAT violet, STRING amber.</summary>
    public static void TypePill(HubSurface hub, double px, double py, string ty, double ff)
    {
        uint col = ty == "bool" ? C_ON : ty == "int" ? 0xFF38BDF8 : ty == "float" ? 0xFFA78BFA : AMBER;
        FillRR(px, py, 42, 14, 4, SBrush(FA(Alpha(col, 34), ff)));
        Txt(ty.ToUpperInvariant(), px, py - 1, 42, 15, hub.HL.fS, FA(Alpha(AccHi(col, 0.3), 230), ff), Fmt.C);
    }

    /// <summary>FFMInstTabs: one tab per client above the panel when several run, plus ALL.</summary>
    static void InstTabs(HubSurface hub, double ax, double ay, double ff, double mt, long now, uint acc)
    {
        var HL = hub.HL;
        int n = FfmEngine.InstList.Count;
        if (n < 2) return;
        double cg = Clamp((mt - 0.30) / 0.36, 0.0, 1.0);
        if (cg <= 0.01) return;
        double fb = ff * cg;
        double ty = ay - 24 + (1 - cg) * 6, th = 20, rlim = ax + HL.abw - 104, aw = 40;
        double hvA = hub.Hv(490);
        FillRR(ax, ty, aw, th, 6, VBrush(ax, ty, aw, th, FA(Mix(0xFF1A1E38, 0xFF232A50, hvA), fb), FA(0xFF11131F, fb)));
        StrokeRR(ax, ty, aw, th, 6, Pen(FA(Alpha(acc, 70 + 80 * hvA), fb), 1));
        Txt("ALL", ax, ty - 1, aw, th, HL.fXs, FA(Alpha(AccHi(acc, 0.35), 225), fb), Fmt.C);
        double x0t = ax + aw + 8, avail = rlim - x0t;
        double tw = Math.Min(112, Math.Max(44, (avail - (n - 1) * 6) / n));
        int fit = 0;
        for (int k = 1; k <= Math.Min(n, 8); k++) { if (x0t + k * (tw + 6) - 6 > rlim) break; fit = k; }
        int more = n - fit;
        if (more > 0 && fit > 0) { fit--; more = n - fit; }
        for (int i = 1; i <= fit; i++)
        {
            int pid = FfmEngine.InstList[i - 1];
            double tx = x0t + (i - 1) * (tw + 6);
            bool on = pid == FfmEngine.Pid;
            double hv = hub.Hv(490 + i);
            FillRR(tx, ty, tw, th, 6, VBrush(tx, ty, tw, th, FA(Mix(on ? 0xFF232A50 : 0xFF171A30, 0xFF2A3260, hv), fb), FA(0xFF11131F, fb)));
            StrokeRR(tx, ty, tw, th, 6, Pen(FA(Alpha(on ? acc : 0xFFFFFF, on ? 150 + 60 * hv : 26 + 40 * hv), fb), 1));
            if (on) FillRR(tx + 6, ty + th - 2, tw - 12, 2, 1, SBrush(FA(Alpha(AccHi(acc, 0.4), 210), fb)));
            FillEll(tx + 7, ty + th / 2 - 2.5, 5, 5, SBrush(FA(Alpha(on ? C_ON : 0xFF9AA8C0, on ? 200 + 45 * Math.Abs(Math.Sin(DecT(now) * 0.0026)) : 90), fb)));
            int nSt = Ffm.Flags.Count;
            string cntS = nSt.ToString();
            double cntW = tw >= 62 ? Fonts.MeasureW(cntS, HL.fXs) + 6 : 0;
            if (cntW > 0) Txt(cntS, tx + tw - cntW - 5, ty - 1, cntW, th, HL.fXs, FA(Alpha(nSt > 0 ? AccHi(acc, 0.35) : 0xFF8A93AD, on ? 225 : 140), fb), Fmt.R);
            Txt(FFMElide(FfmEngine.InstLabel(pid, tw - cntW), HL.fS, tw - 22 - cntW), tx + 16, ty - 1, tw - 20 - cntW, th, HL.fS, FA(Alpha(on ? 0xFFE8EAF6 : 0xFFC7CBE0, on ? 235 : 150), fb), Fmt.L);
        }
        if (more > 0)
        {
            double tx = x0t + fit * (tw + 6);
            if (tx + tw <= rlim) { FillRR(tx, ty, tw, th, 6, SBrush(FA(0x12FFFFFF, fb))); Txt("+" + more, tx, ty - 1, tw, th, HL.fS, FA(0x8EC7CBE0, fb), Fmt.C); }
        }
    }
    /// <summary>FFMSysDivider: the PRESETS rule with its count and spark.</summary>
    static void SysDivider(HubSurface hub, double ax, double ay, double ff, double mt, long now, uint acc)
    {
        var HL = hub.HL;
        double cg = Clamp((mt - 0.58) / 0.34, 0.0, 1.0);
        if (cg <= 0.01) return;
        double fd = ff * cg;
        double dy = ay + SysRowY(HL, SEPAT) - SEPH / 2.0 - 3 - SysScr;
        if (dy < ay - 10 || dy > ay + HL.ffh + 10) return;
        double sld = (1 - cg) * 14;
        double lx = ax + 14 + sld;
        string lbl = "PRESETS";
        double lw = Fonts.MeasureW(lbl, HL.fXs);
        var pn = Pen(FA(Alpha(acc, 150 + 70 * cg), fd), 1.4);
        Line(lx, dy - 3, lx + 3.5, dy + 0.5, pn);
        Line(lx + 3.5, dy + 0.5, lx, dy + 4, pn);
        Txt(lbl, lx + 10, dy - 7, lw + 8, 14, HL.fXs, FA(Alpha(Mix(0xFFC7CBE0, acc, 0.42), 150 + 70 * cg), fd), Fmt.L);
        Txt((SYSN - SEPAT + 1).ToString(), lx + 16 + lw, dy - 7, 14, 14, HL.fXs, FA(Alpha(acc, 120), fd), Fmt.L);
        double tx1 = lx + 32 + lw, tx2 = ax + HL.abw - 16;
        if (tx2 - tx1 > 24)
        {
            FadeLine(tx1, tx2, dy, Alpha(0xFFFFFF, R(40 + 26 * cg)), fd);
            double ph = (DecT(now) % 3400) / 3400.0;
            if (ph < 0.42)
            {
                double t2 = ph / 0.42;
                double hx = tx1 + (tx2 - tx1) * Ease3(t2);
                double ha = Math.Sin(3.14159 * t2);
                FillRR(hx - 22, dy, 22, 1, 0.5, HBrush(hx - 22, dy - 1, 44, 2, FA(Alpha(acc, 0), fd), FA(Alpha(acc, R(165 * ha)), fd)));
                FillRR(hx, dy, 22, 1, 0.5, HBrush(hx, dy - 1, 44, 2, FA(Alpha(acc, R(165 * ha)), fd), FA(Alpha(acc, 0), fd)));
                FillEll(hx - 1.6, dy - 1.6, 3.2, 3.2, SBrush(FA(Alpha(AccHi(acc, 0.4), R(210 * ha)), fd)));
            }
        }
        FillEll(tx2 - 2, dy - 2, 4, 4, SBrush(FA(Alpha(acc, 90 + 60 * Math.Abs(Math.Sin(DecT(now) * 0.0022))), fd)));
    }

    /// <summary>FFMSysIcon: the nine rows' glyphs.</summary>
    static void SysIcon(int i, double cx, double cy, uint col, bool live, long now)
    {
        if (HubState.LowPerf) return;
        var pn = PenP(col, 1.5);
        var b2 = SBrushP(col);
        if (i == 1)
        {
            StrokeRR(cx - 6, cy - 7, 12, 14, 2, pn);
            if (live)
            {
                double ay = cy + 2 + Math.Sin(DecT(now) * 0.005) * 1.2;
                Line(cx, cy - 3, cx, ay, pn); Line(cx - 3, ay - 3, cx, ay, pn); Line(cx + 3, ay - 3, cx, ay, pn);
            }
            else { Line(cx - 3, cy - 2, cx + 3, cy - 2, pn); Line(cx - 3, cy + 2, cx + 1, cy + 2, pn); }
        }
        else if (i == 2)
        {
            for (int k = 1; k <= 3; k++) { Line(cx - 7, cy - 5 + (k - 1) * 5, cx, cy - 8 + (k - 1) * 5, pn); Line(cx, cy - 8 + (k - 1) * 5, cx + 7, cy - 5 + (k - 1) * 5, pn); }
        }
        else if (i == 3)
        {
            double sp = live ? (DecT(now) * 0.14) % 360 : 20;
            Arc(cx - 7, cy - 7, 14, 14, sp, 280, pn);
            double a_ = (sp + 280) * 0.0174533;
            FillEll(cx + 7 * Math.Cos(a_) - 2, cy + 7 * Math.Sin(a_) - 2, 4, 4, b2);
        }
        else if (i == 4)
        {
            StrokeRR(cx - 7, cy - 7, 14, 14, 3, pn);
            Line(cx - 3, cy, cx - 1, cy + 3, pn); Line(cx - 1, cy + 3, cx + 4, cy - 3, pn);
        }
        else if (i == 5)
        {
            Line(cx - 7, cy - 6, cx + 7, cy - 6, pn); Line(cx - 7, cy - 6, cx - 1.6, cy + 1, pn); Line(cx + 7, cy - 6, cx + 1.6, cy + 1, pn);
            Line(cx - 1.6, cy + 1, cx - 1.6, cy + 5, pn); Line(cx + 1.6, cy + 1, cx + 1.6, cy + 5, pn);
            if (live) { double dy = cy + 6 + (DecT(now) * 0.006) % 3; FillEll(cx - 1.4, dy, 2.8, 2.8, b2); }
        }
        else if (i == 6)
        {
            double sp = live ? (DecT(now) * 0.13) % 360 : 90;
            for (int k = 0; k < 2; k++)
            {
                double bas = sp + k * 180;
                Arc(cx - 7, cy - 7, 14, 14, bas, 150, pn);
                double aa = (bas + 150) * 0.0174533;
                FillEll(cx + 7 * Math.Cos(aa) - 1.8, cy + 7 * Math.Sin(aa) - 1.8, 3.6, 3.6, b2);
            }
        }
        else if (i == 7)
        {
            Ell(cx - 6, cy - 6, 12, 12, pn);
            Line(cx - 10, cy, cx - 7, cy, pn); Line(cx + 7, cy, cx + 10, cy, pn); Line(cx, cy - 10, cx, cy - 7, pn); Line(cx, cy + 7, cx, cy + 10, pn);
            FillEll(cx - 1.8, cy - 1.8, 3.6, 3.6, b2);
        }
        else if (i == 8)
        {
            Ell(cx - 7, cy - 7, 14, 14, pn);
            Line(cx - 4.5, cy + 2, cx - 2, cy + 2, pn); Line(cx - 2, cy + 2, cx - 2, cy - 2, pn); Line(cx - 2, cy - 2, cx + 1, cy - 2, pn);
            Line(cx + 1, cy - 2, cx + 1, cy + 2, pn); Line(cx + 1, cy + 2, cx + 4.5, cy + 2, pn);
        }
        else
        {
            double op = live ? (0.82 + 0.18 * (Math.Sin(DecT(now) * 0.0022) + 1) / 2) : 0.72;
            double lw = 7.4 * op;
            Line(cx, cy - 6.4, cx, cy + 5.4, pn);
            for (int k = 1; k <= 2; k++)
            {
                double sx = k == 1 ? -1 : 1;
                Line(cx, cy - 6.4, cx + sx * lw, cy - 4.4, pn); Line(cx + sx * lw, cy - 4.4, cx + sx * lw, cy + 5.0, pn); Line(cx, cy + 5.4, cx + sx * lw, cy + 5.0, pn);
                for (int q = 0; q < 2; q++) { double ly2 = cy - 1.6 + q * 3.0; Line(cx + sx * 1.8, ly2, cx + sx * (lw - 1.4), ly2, pn); }
            }
        }
    }

    // ---- FFMZone ----
    public static int Zone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        if (FfmViews.View == "db" && FfmViews.ViewT > 0.5) return FfmViews.DbZone(hub, ux, uy);
        if (FfmViews.View == "log" && FfmViews.ViewT > 0.5) return FfmViews.LogZone(hub, ux, uy);
        if (FfmViews.View == "comm" && FfmViews.ViewT > 0.5) return FfmCommView.Zone(hub, ux, uy);
        double ax = HL.abx, ay = HL.aby;
        if (uy >= HL.cty + 30 && uy <= HL.cty + 52 && ux >= ax + HL.abw - 96 && ux <= ax + HL.abw) return 421;
        if (FfmEngine.InstList.Count >= 2 && uy >= ay - 24 && uy <= ay - 4)
        {
            if (ux >= ax && ux <= ax + 40) return 490;
            int n2 = FfmEngine.InstList.Count; double aw2 = 40, rlm2 = ax + HL.abw - 104, x02 = ax + aw2 + 8;
            double tw2 = Math.Min(112, Math.Max(44, ((rlm2 - x02) - (n2 - 1) * 6) / n2));
            int fit2 = 0;
            for (int k = 1; k <= Math.Min(n2, 8); k++) { if (x02 + k * (tw2 + 6) - 6 > rlm2) break; fit2 = k; }
            if (n2 - fit2 > 0 && fit2 > 0) fit2--;
            for (int k = 1; k <= fit2; k++) { double tx2 = x02 + (k - 1) * (tw2 + 6); if (ux >= tx2 && ux <= tx2 + tw2) return 490 + k; }
        }
        if (SysTotal(HL) > HL.ffh - 20 && ux >= ax + HL.abw - 12 && ux <= ax + HL.abw && uy >= ay + 10 && uy <= ay + HL.ffh - 10) return 442;
        if (uy >= ay + 6 && uy <= ay + HL.ffh - 6 && ux >= ax && ux <= ax + HL.abw)
            for (int i = 1; i <= SYSN; i++)
            {
                double ry = ay + SysRowY(HL, i) - SysScr;
                if (uy >= ry && uy <= ry + 36)
                {
                    if (i == 1)
                    {
                        if (ux >= ax + HL.abw - 174 && ux <= ax + HL.abw - 98) return 461;
                        if (ux >= ax + HL.abw - 92 && ux <= ax + HL.abw - 16) return 469;
                    }
                    else if (i >= 5 && ux >= ax + HL.abw - 108 && ux <= ax + HL.abw - 16) return 480 + i;
                    if (i >= 2 && i <= 4 && ux >= ax + HL.abw - 64 && ux <= ax + HL.abw - 16) return 470 + i;
                    return 450 + i;
                }
            }
        if (uy >= HL.ffby && uy <= HL.ffby + 26)
            for (int i = 1; i <= 5; i++)
            {
                double bx = ax + (i - 1) * 82;
                if (ux >= bx && ux <= bx + 74) return i == 5 ? 425 : 415 + i;
            }
        if (uy >= HL.ffqy && uy <= HL.ffqy + 24)
        {
            if (ux >= ax + HL.abw - 30 && ux <= ax + HL.abw) return 423;
            if (ux >= ax && ux <= ax + HL.abw - 34) return 422;
        }
        var lst = FlagList();
        if (lst.Count * HL.ffrh > HL.ffrows * HL.ffrh && ux >= ax + HL.abw - 14 && ux <= ax + HL.abw && uy >= HL.ffly && uy <= HL.ffly + HL.fflh) return 436;
        if (uy >= HL.ffly && uy <= HL.ffly + HL.fflh && ux >= ax + 3 && ux <= ax + HL.abw - 8)
        {
            int r = (int)Math.Floor((uy - HL.ffly - 6 + Scr) / HL.ffrh) + 1;
            int vis = r - (int)Math.Floor(Scr / HL.ffrh);
            if (r >= 1 && r <= lst.Count && vis >= 1 && vis <= HL.ffrows)
            {
                if (ux >= ax + 3 && ux < ax + 20) return 510 + vis;               // the ON/OFF pip
                if (ux >= ax + HL.abw - 38 && ux < ax + HL.abw - 14) return 500 + vis;
                if (ux >= ax + HL.abw - 170) return 540 + vis;
                return 520 + vis;
            }
        }
        return 0;
    }

    /// <summary>FFMRealRow(vis): the staged index behind the vis-th visible row, -1 for none.</summary>
    static int RealRow(HubSurface hub, int vis)
    {
        var lst = FlagList();
        int idx = (int)Math.Floor(Scr / hub.HL.ffrh) + vis;
        return (idx >= 1 && idx <= lst.Count) ? lst[idx - 1] : -1;
    }

    // ---- FFMClick ----
    public static bool Click(HubSurface hub, int z)
    {
        long now = Clock.Tick;
        if (FfmViews.Click(hub, z)) { hub.ClickAt[z] = now; hub.Tim(Pace.TICK_A); return true; }
        // a click anywhere but the live field commits or cancels it, as HubClick's FFMBlur did
        if (FfmField.Edit == "q" && z != 422 && z != 423) FfmField.Blur();
        else if (FfmField.Edit == "v" && !(z >= 541 && z <= 545 && RealRow(hub, z - 540) == FfmField.EditRow)) FfmField.Blur();
        bool hit = true;
        switch (z)
        {
            // On macOS there is no live engine, so these do the file route -
            // which is the only route that exists there, and a persistent one:
            // it survives a client restart, which the memory patch never does.
            case 461: if (Yuri.Platform.Os.IsWin) FfmEngine.ApplyLive(); else Ffm.FileApply(); break;   // FFMApplyLive
            case 469: if (Yuri.Platform.Os.IsWin) FfmEngine.UninjectAll(); else Ffm.FileClear(); break; // FFMUninjectAll
            case 490: if (Yuri.Platform.Os.IsWin) FfmEngine.InjectAll(); else Ffm.FileApply(); break;
            case >= 491 and <= 498:
            {
                int ii = z - 490;
                if (ii <= FfmEngine.InstList.Count)
                {
                    int pid = FfmEngine.InstList[ii - 1];
                    if (FfmEngine.Busy) Ffm.Say("INJECTION IN PROGRESS - TRY AGAIN IN A MOMENT", AMBER);
                    else if (pid != FfmEngine.Pid)
                    {
                        FfmEngine.InstAt = Clock.Tick;
                        Ffm.Say(FfmEngine.UseInstance(pid) ? "TARGETING CLIENT " + FfmEngine.InstIdx(pid) + "  PID=" + pid : "COULD NOT ATTACH TO PID " + pid, FfmEngine.Pid == pid ? 0xFF34D399 : C_ACC);
                    }
                }
                break;
            }
            case 472:
                UseSingleton = !UseSingleton; Ini.Write(Paths.IniFile, "ffm", "singleton", UseSingleton ? 1 : 0);
                Ffm.Say(UseSingleton ? "SIGNATURE LOOKUP - FAST" : "STRUCTURAL SCAN - SLOWER, SURVIVES UPDATES", 0); break;
            case 473:
                ReApply = !ReApply; Ini.Write(Paths.IniFile, "ffm", "reapply", ReApply ? 1 : 0);
                FfmEngine.ManageReApply();
                Ffm.Say(ReApply ? "RE-APPLY ON" : "RE-APPLY OFF", 0); break;
            case 474:
                AutoInject = !AutoInject; Ini.Write(Paths.IniFile, "ffm", "autoinject", AutoInject ? 1 : 0);
                // The engine's client poll is what fires this on Windows. There is
                // no poll on macOS, so the switch means "keep the file in step
                // with the staged list" and writes it the moment it goes on.
                if (!Yuri.Platform.Os.IsWin && AutoInject) Ffm.FileApply();
                else Ffm.Say(AutoInject ? "AUTO INJECT ON" : "AUTO INJECT OFF", 0);
                break;
            case 485: Clean(hub); break;
            case 486: FfmUpdate.UpdateFlags(); break;
            case 487: InsertPreset("HITBOX", FfmPresets.Hitbox); break;
            case 488: InsertPreset("30HZ HITBOX", FfmPresets.Hitbox30); break;
            case 489: FfmViews.OpenView("comm"); break;                          // FFMCommOpen
            case 416: FfmViews.OpenView("db"); if (FfmViews.Db.Count == 0 && !FfmViews.Loading) _ = FfmViews.FetchDb(); break;
            case 417: _ = ImportAsync(hub); break;
            case 418: _ = ExportAsync(hub); break;
            case 419: Ffm.ClearAll(); ScrT = 0; Scr = 0; break;
            case 425: FfmViews.OpenView("log"); break;
            case 421: FfmEngine.CloseRoblox(); break;
            case 422:
                if (FfmField.Edit != "q") FfmField.Begin("q", 0);
                FfmField.Mouse(hub.PtrX); break;
            case 423:
            {
                string nm = (FfmField.Edit == "q" ? FfmField.Buf : Ffm.Q2).Trim();
                if (nm == "") FfmField.Begin("q", 0);
                else
                {
                    int r = Ffm.Add(nm, "");
                    FfmField.Buf = ""; Ffm.Q2 = ""; FfmField.Car = 0; FfmField.Sel = -1;
                    if (r != 0) Ffm.Say(Ffm.StagedMsg(r, nm), 0xFF34D399);
                }
                break;
            }
            default:
                if (z >= 451 && z <= 450 + SYSN) Detail.FfmDetail(z - 450);
                else if (z >= 501 && z <= 505) { int i = RealRow(hub, z - 500); if (i >= 0) { Ffm.Del(i); ScrT = Clamp(ScrT, 0.0, MaxScr(hub.HL, FlagList().Count, hub.HL.ffrows)); } }
                else if (z >= 511 && z <= 515) { int i = RealRow(hub, z - 510); if (i >= 0) Ffm.ToggleOn(i); }
                else if (z >= 521 && z <= 525) { int i = RealRow(hub, z - 520); if (i >= 0) Cycle(i); }
                else if (z >= 541 && z <= 545)
                {
                    int i = RealRow(hub, z - 540);
                    if (i >= 0) { FfmField.Begin("v", i); FfmField.Mouse(hub.PtrX); }
                }
                else hit = false;
                break;
        }
        if (z > 0) hub.ClickAt[z] = now;
        hub.Tim(Pace.TICK_A);
        return hit;
    }

    /// <summary>The scrollbar presses the module owns (the community view's three kinds).</summary>
    /// <summary>The press half of the panel: a field takes the caret (and starts a mouse selection) on the button going down, as the .ahk's HubClick does; the community lists start their drags.</summary>
    public static bool Press(HubSurface hub, int z, double uy)
    {
        if (FfmViews.View == "comm") return FfmCommView.Press(hub, z, uy);
        if (FfmViews.View == "db" && z == 437) { if (FfmField.Edit != "db") FfmField.Begin("db", 0); FfmField.Mouse(hub.PtrX); return true; }
        if (FfmViews.View != "") return false;
        if (z == 422) { if (FfmField.Edit != "q") FfmField.Begin("q", 0); FfmField.Mouse(hub.PtrX); return true; }
        if (z >= 541 && z <= 545) { int i = RealRow(hub, z - 540); if (i >= 0) { FfmField.Begin("v", i); FfmField.Mouse(hub.PtrX); } return true; }
        return false;
    }
    /// <summary>FFMDragScroll: drag 5 the database list, 6 the staged list, 11 the log / updates / history, 12 the systems column.</summary>
    public static void DragScroll(HubSurface hub, double uy)
    {
        var HL = hub.HL;
        if (HL.drag == 5)
        {
            double lh = 11 * HL.ffrh, tot = FfmViews.DbList().Count * HL.ffrh, ly = HL.cty + 30 + 74;
            double thmb = Math.Max(22, (lh - 4) * (lh / Math.Max(1, tot)));
            FfmViews.DbScrT = HubUI.ScrollFromY(HL, uy, ly + 2, lh - 4, tot, lh, thmb, FfmViews.DbScr); FfmViews.DbScr = FfmViews.DbScrT;
        }
        else if (HL.drag == 12)
        {
            double vis = HL.ffh - 20, tot = SysTotal(HL), th2 = Math.Max(22, vis * (vis / Math.Max(1, tot)));
            SysScrT = HubUI.ScrollFromY(HL, uy, HL.aby + 10, vis, tot, vis, th2, SysScr); SysScr = SysScrT;
        }
        else if (HL.drag == 11) FfmViews.LogDragScroll(hub, uy);
        else
        {
            var lst = FlagList();
            double lh = HL.fflh, view = HL.ffrows * HL.ffrh, tot = lst.Count * HL.ffrh, ly = HL.ffly;
            double thmb = Math.Max(22, (lh - 12) * (view / Math.Max(1, tot)));
            ScrT = HubUI.ScrollFromY(HL, uy, ly + 6, lh - 12, tot, view, thmb, Scr); Scr = ScrT;
        }
        hub.Tim(Pace.TICK_A);
    }
    public static void Drag(HubSurface hub, double uy) => FfmCommView.Drag(hub, uy);

    /// <summary>The wheel: the staged list two rows a notch, the systems column one row.</summary>
    public static bool Wheel(HubSurface hub, double ux, double uy, double delta)
    {
        var HL = hub.HL;
        if (FfmViews.Wheel(hub, ux, uy, delta)) return true;
        if (ux >= HL.abx && ux <= HL.abx + HL.abw && uy >= HL.aby && uy <= HL.aby + HL.ffh)
        {
            SysScrT = Clamp(SysScrT - delta * HL.ffrg, 0.0, Math.Max(0.0, SysTotal(HL) - (HL.ffh - 20)));
            return true;
        }
        if (ux >= HL.abx && ux <= HL.abx + HL.abw && uy >= HL.ffly - 6 && uy <= HL.ffly + HL.fflh + 6)
        {
            ScrT = Clamp(ScrT - delta * 2 * HL.ffrh, 0.0, MaxScr(HL, FlagList().Count, HL.ffrows));
            return true;
        }
        return false;
    }

    /// <summary>FFMCycle(i): a bool flips; anything else opens its value for editing.</summary>
    static void Cycle(int i)
    {
        var fl = Ffm.Flags[i];
        if (fl.Type == "bool")
        {
            fl.Value = fl.Value.Equals("true", StringComparison.OrdinalIgnoreCase) ? "false" : "true";
            Ffm.Flash[i] = Clock.Tick;
            Ffm.SaveFlags();
            Ffm.Say("TOGGLED " + fl.Name, 0);
        }
        else FfmField.Begin("v", i);
    }

    /// <summary>FFMInsertPreset(label, set): replaces the staged set, after a snapshot.</summary>
    static void InsertPreset(string label, IReadOnlyList<KeyValuePair<string, string>> set)
    {
        Ffm.Snap("preset");
        Ffm.Flags.Clear(); Ffm.Flash.Clear();
        Ffm.Reindex();
        ScrT = 0.0; Scr = 0.0;
        Ffm.Bulk = true;
        foreach (var kv in set) Ffm.Add(kv.Key, kv.Value);
        Ffm.Bulk = false;
        Ffm.Reindex();
        Ffm.SaveFlags();
        Ffm.Say($"{label} PRESET LOADED - {Ffm.Flags.Count} FLAG(S)", 0xFF34D399);
    }

    /// <summary>FFMCleanScan: what CLEAN keeps (injected cleanly), drops (refused, never injected), and repairs (a bare name onto its live prefixed form).</summary>
    sealed class CleanScan { public Dictionary<string, bool> Drop = new(StringComparer.OrdinalIgnoreCase); public Dictionary<string, string> Fix = new(StringComparer.OrdinalIgnoreCase), Why = new(StringComparer.OrdinalIgnoreCase); public int Kept; }
    static CleanScan CleanScanRun(bool forFile)
    {
        var scan = new CleanScan();
        bool haveLive = FfmUpdate.LiveAt != 0 && FfmUpdate.Live.Count > 0;
        foreach (var fl in Ffm.Flags)
        {
            string nm = fl.Name;
            if (!fl.On) continue;
            if (FfmEngine.Failed.Contains(nm)) { scan.Drop[nm] = true; scan.Why[nm] = "injection refused - see the INJECTION log"; continue; }
            if (!FfmEngine.Orig.ContainsKey(nm)) { scan.Drop[nm] = true; scan.Why[nm] = "not injected - added since the last INJECT, or off when it ran"; continue; }
            scan.Kept++;
            if (forFile && haveLive && Ffm.PfxOf(nm) == "")
            {
                string bk = Ffm.BareOf(nm);
                if (FfmUpdate.BareAll.TryGetValue(bk, out var cands))
                    foreach (var c in cands) if (FfmUpdate.SigilFits(Ffm.PfxOf(c), fl.Type)) { scan.Fix[nm] = c; break; }
            }
        }
        return scan;
    }
    static string JsonClean(CleanScan scan)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sb = new System.Text.StringBuilder("{");
        int n = 0;
        foreach (var fl in Ffm.Flags)
        {
            if (scan.Drop.ContainsKey(fl.Name) || !fl.On) continue;
            string outNm = scan.Fix.TryGetValue(fl.Name, out var fx) ? fx : fl.Name;
            if (!seen.Add(outNm)) continue;
            sb.Append(n > 0 ? "," : "").Append("\n    \"").Append(Ffm.Esc(outNm)).Append("\": \"").Append(Ffm.Esc(Ffm.Norm(fl.Value, fl.Type))).Append('"');
            n++;
        }
        return sb.Append("\n}").ToString();
    }
    /// <summary>FFMClean: export only what injected cleanly, bare names repaired to their live prefixed forms.</summary>
    static void Clean(HubSurface hub)
    {
        if (Ffm.Flags.Count == 0) { Ffm.Say("NOTHING STAGED", C_ACC); return; }
        if (OrigCount == 0 && FailedCount == 0) { Ffm.Say("INJECT FIRST - CLEAN KEEPS THE FLAGS THAT INJECTED", C_ACC); return; }
        var scan = CleanScanRun(true);
        if (scan.Kept == 0) { Ffm.Say("NOTHING INJECTED CLEANLY - NOTHING TO EXPORT", C_ACC); return; }
        if (_busyPick) return;
        foreach (var (k, why) in scan.Why) FfmViews.Log("Clean", k, why);
        Ffm.Say("CHOOSE A PATH  " + scan.Kept + " KEPT  " + scan.Drop.Count + " DROPPED" + (scan.Fix.Count > 0 ? "  " + scan.Fix.Count + " PREFIXED" : "") + " - CANCEL COPIES TO CLIPBOARD", AMBER);
        _ = CleanExportAsync(hub, JsonClean(scan), scan.Kept);
    }
    static async Task CleanExportAsync(HubSurface hub, string json, int kept)
    {
        _busyPick = true;
        try
        {
            var top = TopLevel.GetTopLevel(hub);
            IStorageFile? file = null;
            if (top?.StorageProvider is { CanSave: true } sp)
                file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Export the flags that injected cleanly", SuggestedFileName = "ClientAppSettings.json", DefaultExtension = "json",
                    FileTypeChoices = new[] { new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } } } });
            if (file is null)
            {
                try { if (top?.Clipboard is { } cb) await cb.SetTextAsync(json); } catch { }
                Ffm.Say("CLEAN SET COPIED - " + kept + " FLAG(S)", 0xFF34D399);
                return;
            }
            try
            {
                await using var s = await file.OpenWriteAsync();
                await using var wr = new StreamWriter(s, new System.Text.UTF8Encoding(false));
                await wr.WriteAsync(json);
                Ffm.Say("CLEAN SET EXPORTED - " + kept + " FLAG(S)", 0xFF34D399);
            }
            catch { Ffm.Say("EXPORT FAILED", C_ACC); }
        }
        finally { _busyPick = false; hub.Tim(Pace.TICK_A); }
    }

    // ---- IMPORT / EXPORT: the platform's file dialogs; cancelling falls back to the clipboard, as before ----
    static async Task ImportAsync(HubSurface hub)
    {
        if (_busyPick) return;
        _busyPick = true;
        try
        {
            var top = TopLevel.GetTopLevel(hub);
            Ffm.Say("SELECT FILE(S) - CANCEL USES CLIPBOARD", AMBER);
            IReadOnlyList<IStorageFile> files = Array.Empty<IStorageFile>();
            if (top?.StorageProvider is { CanOpen: true } sp)
                files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Import fast flags", AllowMultiple = true,
                    FileTypeFilter = new[] { new FilePickerFileType("JSON") { Patterns = new[] { "*.json", "*.txt" } }, FilePickerFileTypes.All } });
            if (files.Count == 0)
            {
                string clip = "";
                try { clip = await (top?.Clipboard?.GetTextAsync() ?? Task.FromResult<string?>("")) ?? ""; } catch { }
                if (clip.Trim() == "") { Ffm.Say("CLIPBOARD EMPTY - PICK A FILE OR COPY JSON", C_ACC); return; }
                int n = Ffm.ParseInto(clip);
                Ffm.Say((n != 0 ? $"IMPORTED {n} FROM CLIPBOARD" : "NO FLAGS IN CLIPBOARD") + (Ffm.BadN != 0 ? $"  -  {Ffm.BadN} REFUSED, VALUE DOES NOT FIT THE NAME" : ""),
                    n != 0 ? (Ffm.BadN != 0 ? AMBER : 0xFF34D399) : C_ACC);
                return;
            }
            int total = 0;
            foreach (var f in files)
            {
                try
                {
                    await using var s = await f.OpenReadAsync();
                    using var rd = new StreamReader(s);
                    total += Ffm.ParseInto(await rd.ReadToEndAsync());
                }
                catch { }
            }
            Ffm.Say((total != 0 ? $"IMPORTED {total} FROM {files.Count} FILE(S)" : "NO FLAGS IN THE FILE(S)") + (Ffm.BadN != 0 ? $"  -  {Ffm.BadN} REFUSED, VALUE DOES NOT FIT THE NAME" : ""),
                total != 0 ? (Ffm.BadN != 0 ? AMBER : 0xFF34D399) : C_ACC);
        }
        finally { _busyPick = false; hub.Tim(Pace.TICK_A); }
    }

    static async Task ExportAsync(HubSurface hub)
    {
        if (Ffm.Flags.Count == 0) { Ffm.Say("NOTHING STAGED", C_ACC); return; }
        if (_busyPick) return;
        _busyPick = true;
        try
        {
            var top = TopLevel.GetTopLevel(hub);
            Ffm.Say("CHOOSE A PATH - CANCEL COPIES TO CLIPBOARD", AMBER);
            IStorageFile? file = null;
            if (top?.StorageProvider is { CanSave: true } sp)
                file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Export fast flags", SuggestedFileName = "ClientAppSettings.json", DefaultExtension = "json",
                    FileTypeChoices = new[] { new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } } } });
            if (file is null)
            {
                try { if (top?.Clipboard is { } cb) await cb.SetTextAsync(Ffm.Json()); } catch { }
                Ffm.Say($"COPIED {Ffm.Flags.Count} TO CLIPBOARD", 0xFF34D399);
                return;
            }
            try
            {
                await using var s = await file.OpenWriteAsync();
                await using var wr = new StreamWriter(s, new System.Text.UTF8Encoding(false));
                await wr.WriteAsync(Ffm.Json());
                Ffm.Say($"EXPORTED {Ffm.Flags.Count} TO FILE", 0xFF34D399);
            }
            catch { Ffm.Say("EXPORT FAILED", C_ACC); }
        }
        finally { _busyPick = false; hub.Tim(Pace.TICK_A); }
    }
}
