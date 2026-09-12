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

namespace Yuri.Modules.Special;

/// <summary>
/// SPFSystems: module 5's panel. Seventeen rows in a column that scrolls
/// three at a time (SPFSysVis), each with its glyph, name, hint, and its
/// switch; rows 1, 6, 8 and several from 9 up grow a chip row when on
/// (Discord's ACCOUNT / JOINABLE / FOCUS, FPS BOOST's GPU / QUIET / SYSTEM,
/// the trimmer's interval and limit, the cleaner's age and CLEAN NOW, the
/// theme's DARK / LIGHT, the death sound's file, the emoji set). Under the
/// column: ACCOUNTS and SAVED PLACES, the GAME LINK and PRIVATE SERVER LINK
/// fields with JOIN / HUNT, and the LIVE card. Zone ids are the .ahk's:
/// SPF_SWZ 3100 + row, SPF_BDZ 3140 + row, chips 974+, 1070+, 1074+,
/// 3200 + (row-9)*4 + k, 971 the scrollbar, 966-970, 1029, 1011.
/// </summary>
public static class SpfPanel
{
    const uint AMBER = Spf.AMBER, C_ON = Spf.C_ON, C_ACC = Spf.C_ACC;
    const int SWZ = 3100, BDZ = 3140, SUBN = 3, SUBW = 88, SUBH = 16, SUB6Z = 1070, SUB6N = 3, SUB6W = 66, SUB8Z = 1074, SUB8N = 2, SUB8W = 84;
    public const double spfrh = 32, spfrg = 36, spfh = 276;
    static readonly string[] Names = { "DISCORD ACTIVITY", "SHOW SERVER REGION", "AUTO-REGION FINDER", "BETTER MATCHMAKING", "MULTI-ROBLOX INSTANCES", "FPS BOOST",
        "DISABLE CRASH HANDLER", "MEMORY TRIMMER", "SERVER DETAILS", "NO DESKTOP APP", "ROBLOX CLEANER", "LAUNCH ON STARTUP", "ROBLOX APP THEME",
        "OLD CHARACTER SOUNDS", "OLD AVATAR BACKGROUND", "CUSTOM DEATH SOUND", "EMOJI FONT" };

    /// <summary>SPFViewOpen("acct"): set when the ACCOUNTS view lands.</summary>
    public static Action? OpenAcct;
    public static void Register()
    {
        Spf.Load();
        Integrations.Panels[5] = Draw;
        Integrations.PanelOverlays[5] = (hub, x0, y0, dx, dy2, f, now, acc) => { if (Spf.View == "" && now - Spf.ViewAt >= 300) return; SpfSaved.Draw(hub, f, now, acc, dx, dy2); SpfAcctView.Draw(hub, f, now, acc, dx, dy2); };
        SpfSaved.Load();
        SpfAcct.Load();
        SpfMods.Register();
        SpfWin.Register();
        SpfFps.Register();
        Shell.Boot.SavedAccounts = SpfAcct.Acct.Count;
        Shell.Boot.SavedPlaces = SpfSaved.Saved.Count + SpfSaved.SavedP.Count;
        Integrations.PanelZones[5] = Zone;
        Integrations.PanelClicks[5] = Click;
        Integrations.PanelWheels[5] = Wheel;
        Integrations.SpecialOn = Spf.OnCount;
        // SysArmedN(): the .ahk counts the whole SPECIAL FEATURES block as ONE
        // armed system, not one per row, and does not count FPS BOOST at all.
        // Chained, so every module that registers after this one adds its own.
        var prevArmed = Dashboard.SysArmedN;
        Dashboard.SysArmedN = () => prevArmed() + (Spf.Discord || Spf.Region || Spf.Match || Spf.Odds || Spf.Multi ? 1 : 0);
    }

    static double SysRowY(int i) => 12 + (i - 1) * spfrg;
    static double SysTotal() => Spf.SYSN * spfrg;
    static double SysVis() => spfrg * 3 + 10;
    static double SubX(double ax, int k) => ax + 54 + (k - 1) * 93;
    static double SubY(double ry) => ry + 16;
    static double Sub6X(double ax, int k) => ax + 54 + (k - 1) * 70;
    static double Sub8X(double ax, int k) => ax + 54 + (k - 1) * 88;
    static int ChipZ(int row, int k) => 3200 + (row - 9) * 4 + k;
    sealed record Chips(int N, double W, double Step, string[] Labs, double[] Ons);
    /// <summary>SPFChipRow(row): the chips a row from 9 up carries, or null.</summary>
    static Chips? ChipRow(int row) => row switch
    {
        9 => new(3, 66, 70, new[] { "REJOIN LAST", "COPY LINK", "COPY ID" }, new[] { Spf.Hist.Count > 0 ? 1.0 : 0.0, Spf.InGame && Spf.Job != "" ? 1.0 : 0.0, Spf.Job != "" ? 1.0 : 0.0 }),
        11 => new(2, 84, 88, new[] { Spf.CleanAgeLabel().ToUpperInvariant(), "CLEAN NOW" }, new[] { 1.0, 1.0 }),
        13 => new(1, 84, 88, new[] { Spf.ThemeName().ToUpperInvariant() }, new[] { 1.0 }),
        16 => new(1, 84, 88, new[] { Spf.ModHave?.Invoke("death") == true ? "CHANGE FILE" : "PICK FILE" }, new[] { Spf.ModHave?.Invoke("death") == true ? 1.0 : 0.0 }),
        17 => new(1, 84, 88, new[] { Spf.EmojiLabel() }, new[] { 1.0 }),
        _ => null,
    };
    static double ChipOff(int row) { var c = ChipRow(row); return c is null ? 0 : c.N * c.Step; }

    static string Hint(int i)
    {
        switch (i)
        {
            case 1: return "show the roblox game you are in on your discord profile";
            case 2: return "show which region your current server is hosted in";
            case 3: return "rejoin until you land on a server in your own region";
            case 4: return "picks a server this session already measured as close to you";
            case 5:
                if (!Spf.Multi) return "run more than one roblox client at once";
                int stale = Spf.MultiStale?.Invoke() ?? 0;
                if (stale > 0) return $"{stale} client{(stale == 1 ? "" : "s")} started before this was on  \u00B7  close and relaunch from the website";
                int rbxN = Spf.RbxCount?.Invoke() ?? 0;
                return rbxN > 0 ? $"{rbxN} client{(rbxN == 1 ? "" : "s")} running  \u00B7  press play on the website to add one" : "ready  \u00B7  launch from the website, not the shortcut";
            case 6: return Spf.Fps ? (Spf.FpsHintShort?.Invoke() ?? "engine arrives with the next build") : "more frames without touching a single graphics setting";
            case 7: return Spf.Crash ? (Spf.CrashN > 0 ? $"closed {Spf.CrashN} time{(Spf.CrashN == 1 ? "" : "s")} this session  \u00B7  still watching" : "watching  \u00B7  closes it within seconds of each launch") : "closes RobloxCrashHandler.exe after each launch to free its memory";
            case 8: return Spf.Mem ? (Spf.MemHintShort?.Invoke() ?? "engine arrives with the next build") : "evicts the client's memory on a schedule  \u00B7  can cause stutter";
            case 9: return Spf.Srv ? (Spf.SrvHintShort?.Invoke() ?? "the log tail arrives with the next build") : "type, uptime, id and a rejoinable history of every server";
            case 10: return Spf.NoApp ? (Spf.NoAppN > 0 ? $"closed the client {Spf.NoAppN} time{(Spf.NoAppN == 1 ? "" : "s")} this session" : "watching  \u00B7  acts the moment a game is left") : "leaving a game closes the client instead of the home app";
            case 11: return Spf.Clean ? Spf.CleanHintShort() : "roblox logs and cache older than an age you pick";
            case 12: return Spf.Start ? (Spf.StartupHas() ? (Os.IsMac ? "in your login items" : "in the windows startup list") : "startup entry missing - turn it off and on") : (Os.IsMac ? "opens YURI when you log in" : "opens YURI when windows starts");
            case 13: return Spf.Theme ? (Spf.ThemeState != "" ? Spf.ThemeState : "will set it at the next launch") : "forces the roblox app to light or dark";
            case 14: return Spf.Snd ? (Spf.ModHintShort?.Invoke("oldsnd") ?? "") : "the 2014 walk, jump and get-up sounds";
            case 15: return Spf.AvBg ? (Spf.ModHintShort?.Invoke("avbg") ?? "") : "the 2020 avatar editor scene";
            case 16: return Spf.Death ? (Spf.ModHintShort?.Invoke("death") ?? "") : "any .ogg you pick replaces the oof";
            default: return Spf.EmojiOn ? (Spf.ModHintShort?.Invoke("emoji") ?? "") : "catmoji, or the windows 11, 10 or 8 set";
        }
    }

    // ---- SPFSystems ----
    public static void Draw(HubSurface hub, double ax, double ay, double x0, double y0, double dx, double dy2, double ff, long now, uint acc)
    {
        var HL = hub.HL;
        Spf.Tick();
        double mt = HL.modT;
        Txt("SYSTEMS", ax, y0 + 36, 120, 14, Fonts.fBadge, FA(0x62C7CBE0, ff), Fmt.L);
        Txt(mt > 0.5 ? Spf.SYSN + " listed" : "0 listed", ax + 66, y0 + 36, 80, 14, HL.fS, FA(Alpha(acc, 60 + 120 * mt), ff), Fmt.L);
        if (mt > 0.4)
        {
            double fh = ff * Clamp((mt - 0.4) / 0.4, 0.0, 1.0);
            Txt(Spf.Status(), ax + HL.abw - 280, y0 + 30, 280, 22, Fonts.fHint, FA(Alpha(Spf.InGame ? 0xFFC7CBE0 : 0xFF9AA8C0, 145), fh), Fmt.R);
        }
        double pah = 26 + (spfh - 26) * mt;
        FillRR(ax, ay, HL.abw, pah, 12, VBrush(ax, ay, HL.abw, pah, FA(Mix(0xFF141728, 0xFF181C34, mt), ff), FA(0xFF12141F, ff)));
        MiniBackdrop(ax, ay, HL.abw, pah, 12, acc, ff, now, 0.9);
        Integrations.HubEmptyGhost(hub, ax, ay, pah, ff);
        StrokeRR(ax, ay, HL.abw, pah, 12, Pen(FA(Alpha(acc, 70 + 90 * mt), ff), 1.2));
        double gl = 40 * mt;
        var pnG = Pen(FA(Alpha(AccHi(acc, 0.4), 200 * mt), ff), 1.6);
        Line(ax + 14, ay + 0.6, ax + 14 + gl, ay + 0.6, pnG);
        Line(ax + HL.abw - 14 - gl, ay + pah - 0.6, ax + HL.abw - 14, ay + pah - 0.6, pnG);
        ModRing(ax, ay, HL.abw, pah, 12, acc, ff, HL.modAt, now, 700, 18, hub.HubMod != 0);
        if (mt < 0.02) return;
        int pClip = PushG();
        ClipRR(ax, ay, HL.abw, pah, 12);
        double sysVis = SysVis();
        double scr = Spf.SysScr;
        int svRows = PushG();
        ClipRR(ax + 2, ay + 8, HL.abw - 4, sysVis + 2, 8);
        for (int i = 1; i <= Spf.SYSN; i++)
        {
            double cg = Clamp((mt - 0.30 - Math.Min(i, 3) * 0.07) / 0.38, 0.0, 1.0);
            if (cg <= 0.01) continue;
            double fb = ff * cg;
            double ry = ay + SysRowY(i) - scr;
            if (ry + spfrh < ay + 8 || ry > ay + 10 + sysVis) continue;
            double hv = Math.Max(hub.Hv(BDZ + i), hub.Hv(SWZ + i));
            double sld = (1 - cg) * 10;
            double axs = ax + sld;
            double on = Spf.T[i];
            bool live = on > 0.5;
            double fl = Spf.FlashAt.TryGetValue(i, out var fa) && now - fa < 420 ? 1 - Clamp((now - fa) / 420.0, 0.0, 1.0) : 0.0;
            if (hv > 0.01 || fl > 0.01) FillRR(ax + 6, ry, HL.abw - 12, spfrh, 8, HBrush(ax + 6, ry, HL.abw - 12, spfrh, FA(Alpha(0xFFFFFF, 15 * hv + 20 * fl), fb), FA(Alpha(0xFFFFFF, 3 * hv), fb)));
            FillRR(axs + 14, ry + 8, 3, 16, 1.5, VBrush(axs + 14, ry + 8, 3, 16, FA(Alpha(live ? AccHi(acc, 0.35) : 0xFFC7CBE0, live ? 235 : 60), fb), FA(Alpha(live ? acc : 0xFFC7CBE0, live ? 150 : 40), fb)));
            Icon(i, axs + 36, ry + 16, FA(Alpha(live ? acc : 0xFF9AA8C0, live ? 220 : 110), fb), live, now);
            double tw = HL.abw - 126;
            Txt(Names[i - 1], axs + 54, ry + 2, tw, 16, Fonts.fBadge, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.18), 235), fb), Fmt.L);
            string ht = Hint(i);
            double subE = i == 1 ? Ease3(Clamp(Spf.T[1] * 1.6 - 0.15, 0.0, 1.0)) : 0.0;
            double sub6 = i == 6 ? Ease3(Clamp(Spf.T[6] * 1.6 - 0.15, 0.0, 1.0)) : 0.0;
            double sub8 = i == 8 ? Ease3(Clamp(Spf.T[8] * 1.6 - 0.15, 0.0, 1.0)) : 0.0;
            double subN = i >= 9 && ChipRow(i) is not null ? Ease3(Clamp(on * 1.6 - 0.15, 0.0, 1.0)) : 0.0;
            double hOff = Math.Round(SUB6N * 70 * sub6 + SUB8N * 88 * sub8 + ChipOff(i) * subN);
            string bLbl = i == 4 || i == 5 ? BadgeLabel(i) : "";
            double expW = bLbl != "" ? BadgeW(hub, bLbl) + 4 : 0;
            if (subE < 0.999) Txt(FFMElide(ht, Fonts.fHint, tw - expW - hOff), axs + 54 + hOff, ry + 17, tw - expW - hOff, 13, Fonts.fHint, FA(0x7EC7CBE0, fb * (1 - subE)), Fmt.L);
            if (subE > 0.001) SubRow(hub, 1, axs, ry, acc, fb * subE, now, subE);
            if (sub6 > 0.001) SubRow(hub, 6, axs, ry, acc, fb * sub6, now, sub6);
            if (sub8 > 0.001) SubRow(hub, 8, axs, ry, acc, fb * sub8, now, sub8);
            if (subN > 0.001) SubRow(hub, i, axs, ry, acc, fb * subN, now, subN);
            if (bLbl != "") ExpBadge(hub, axs + 54 + tw - expW + 2, ry + 16, fb, now, bLbl);
            FFMTogDraw(axs + HL.abw - 62, ry + 5, 44, 22, on, acc, hv, fb);
        }
        Pop(svRows);
        double sysTot = SysTotal();
        if (sysTot > sysVis)
        {
            double sbS = Math.Max(hub.Hv(971), HL.drag == 16 ? 1.0 : 0.0);
            double sw2 = 3 + 3 * sbS;
            double sxs = ax + HL.abw - 6 - 3 * sbS;
            FillRR(sxs, ay + 10, sw2, sysVis, sw2 / 2, SBrush(FA(Alpha(0xFFFFFF, 18 + 26 * sbS), ff * mt)));
            double th2 = Math.Max(22, sysVis * (sysVis / sysTot));
            double ty2 = ay + 10 + (sysVis - th2) * (scr / Math.Max(1, sysTot - sysVis));
            FillRR(sxs, ty2, sw2, th2, sw2 / 2, VBrush(sxs, ty2, sw2, th2, FA(Alpha(AccHi(acc, 0.35), 190 + 65 * sbS), ff * mt), FA(Alpha(acc, 170 + 60 * sbS), ff * mt)));
        }
        if (mt > 0.55)
        {
            double fg = ff * Clamp((mt - 0.55) / 0.35, 0.0, 1.0);
            GroupLine(ax + 14, ax + HL.abw - 14, ay + 134, acc, fg, now);
            double lbTot = HL.abw - 70, lbW = (lbTot - 8) / 2;
            AcctButton(hub, ax + 54, ay + 140, lbW, fg, acc, now);
            SavedButton(hub, ax + 54 + lbW + 8, ay + 140, lbW, fg, acc, now);
            Field(hub, ax, ay + 188, HL.abw, "gl", 966, "GAME LINK", "paste a roblox game link or place id", Spf.MmLink, acc, fg);
            Field(hub, ax, ay + 236, HL.abw, "pl", 967, "PRIVATE SERVER LINK", "paste a private server link", Spf.MmPriv, acc, fg);
            bool hg = Spf.Hunting && Spf.HuntWhich == 1, hp = Spf.Hunting && Spf.HuntWhich == 2;
            string jl = Spf.Match ? "HUNT" : "JOIN";
            FFMBtn(968, ax + HL.abw - 88, ay + 188, 72, 26, hg ? "STOP" : jl, acc, fg, hg ? 3 : Spf.MmLink != "" ? 1 : 0);
            FFMBtn(969, ax + HL.abw - 88, ay + 236, 72, 26, hp ? "STOP" : jl, acc, fg, hp ? 3 : Spf.MmPriv != "" ? 1 : 0);
        }
        Pop(pClip);
        Live(hub, ax, ay + spfh + 12, HL.abw, ff, now, acc, mt);
    }

    static string BadgeLabel(int i) => i == 5 && Spf.Multi && (Spf.MultiStale?.Invoke() ?? 0) > 0 ? "RESTART ROBLOX" : "EXPERIMENTAL";
    static double BadgeW(HubSurface hub, string lbl) => Math.Max(88, Fonts.MeasureW(lbl, hub.HL.fXs) + 30);
    static void ExpBadge(HubSurface hub, double bx, double by, double f, long now, string lbl)
    {
        double bw = BadgeW(hub, lbl), bh = 14;
        double pu = (Math.Sin(DecT(now) * 0.0024) + 1) / 2;
        FillRR(bx, by, bw, bh, 7, SBrush(FA(Alpha(AMBER, R(20 + 14 * pu)), f)));
        StrokeRR(bx, by, bw, bh, 7, Pen(FA(Alpha(AMBER, R(80 + 40 * pu)), f), 1));
        double tx = bx + 11, ty = by + bh / 2;
        var pn = Pen(FA(Alpha(AccHi(AMBER, 0.35), R(180 + 60 * pu)), f), 1.2);
        Line(tx, ty - 4.2, tx + 4.2, ty + 3.4, pn); Line(tx + 4.2, ty + 3.4, tx - 4.2, ty + 3.4, pn); Line(tx - 4.2, ty + 3.4, tx, ty - 4.2, pn);
        var b = SBrush(FA(Alpha(AccHi(AMBER, 0.5), R(200 + 55 * pu)), f));
        FillRR(tx - 0.5, ty - 2.2, 1.2, 3.4, 0.6, b); FillRR(tx - 0.5, ty + 2.0, 1.2, 1.2, 0.6, b);
        Txt(lbl, bx + 20, by - 1, bw - 24, bh, hub.HL.fXs, FA(Alpha(AccHi(AMBER, 0.45), R(210 + 45 * pu)), f), Fmt.L);
    }

    /// <summary>SPFSubRow: the chips a row shows once it is on.</summary>
    static void SubRow(HubSurface hub, int row, double ax, double ry, uint acc, double f, long now, double e)
    {
        var HL = hub.HL;
        string a1 = Spf.DcAcct && Spf.UserNm != "" ? "@" + Spf.UserNm : "ACCOUNT";
        string[] labs; double[] ons; int nSub, zBase, flBase; double wSub; double step = 0;
        Chips? c = null;
        if (row == 6) { labs = new[] { Spf.FpsGpu ? "GPU ON" : "GPU", "QUIET", "SYSTEM" }; ons = new[] { Spf.Q1, Spf.Q2, Spf.Q3 }; nSub = SUB6N; wSub = SUB6W; zBase = SUB6Z; flBase = 20; }
        else if (row == 8) { labs = new[] { Spf.MemInt + "S", Spf.MemThr == 0 ? "ANY SIZE" : "OVER " + Spf.MemFmt(Spf.MemThr) }; ons = new[] { Spf.M1, Spf.M2 }; nSub = SUB8N; wSub = SUB8W; zBase = SUB8Z; flBase = 30; }
        else if (row >= 9) { c = ChipRow(row); if (c is null) return; labs = c.Labs; ons = c.Ons; nSub = c.N; wSub = c.W; step = c.Step; zBase = ChipZ(row, 0); flBase = 100 + row * 4; }
        else { labs = new[] { a1, "JOINABLE", "FOCUS" }; ons = new[] { Spf.U1, Spf.U2, Spf.U3 }; nSub = SUBN; wSub = SUBW; zBase = 974; flBase = 10; }
        double sy = SubY(ry) - (1 - e) * 4;
        for (int k = 1; k <= nSub; k++)
        {
            double on = ons[k - 1];
            double sx = row == 6 ? Sub6X(ax, k) : row == 8 ? Sub8X(ax, k) : row >= 9 ? ax + 54 + (k - 1) * step : SubX(ax, k);
            int z = zBase + k;
            double hv = Ease3(hub.Hv(z));
            double fl = Spf.FlashAt.TryGetValue(flBase + k, out var fa) && now - fa < 420 ? 1 - Clamp((now - fa) / 420.0, 0.0, 1.0) : 0.0;
            int st = PushXform(sx + wSub / 2, sy + SUBH / 2.0, 1 + 0.05 * hv + 0.06 * fl, 0);
            FillRR(sx, sy, wSub, SUBH, 8, SBrush(FA(Alpha(on > 0.02 ? acc : 0xFFFFFF, R(on > 0.02 ? 30 + 26 * on + 26 * hv + 40 * fl : 8 + 14 * hv)), f)));
            MicroBackdrop(sx, sy, wSub, SUBH, 8, acc, f, now, 0.8);
            StrokeRR(sx, sy, wSub, SUBH, 8, Pen(FA(Alpha(on > 0.02 ? acc : 0xFF9AA8C0, R(on > 0.02 ? 120 + 80 * on + 50 * hv : 34 + 46 * hv)), f), 1));
            uint dc = on <= 0.02 ? 0xFF6E7590
                : row == 8 ? AccHi(acc, 0.35)
                : row >= 9 ? AccHi(acc, 0.35)
                : row == 6 ? ((k == 1 && !Spf.FpsGpu) || k == 2 || k == 3 ? AMBER : AccHi(acc, 0.35))
                : (k == 3 && !Spf.FocOn) ? AMBER : (k == 2 && Spf.JoinUrl() == "") ? AMBER : AccHi(acc, 0.35);
            double dr = 2.4 + 0.6 * on;
            FillEll(sx + 9 - dr, sy + SUBH / 2.0 - dr, dr * 2, dr * 2, SBrush(FA(Alpha(dc, R(on > 0.02 ? 200 + 55 * Math.Abs(Math.Sin(DecT(now) * 0.0035)) : 90)), f)));
            Txt(FFMElide(labs[k - 1], HL.fXs, wSub - 22), sx + 17, sy + 1, wSub - 22, 14, HL.fXs, FA(Alpha(on > 0.02 ? AccHi(acc, 0.45) : 0xFFC7CBE0, R(on > 0.02 ? 235 : 140 + 60 * hv)), f), Fmt.L);
            if (fl > 0.01)
            {
                double ex = (1 - fl) * 10;
                StrokeRR(sx - ex, sy - ex * 0.6, wSub + ex * 2, SUBH + ex * 1.2, 8 + ex * 0.3, Pen(FA(Alpha(acc, R(190 * fl)), f), 1.6 * fl + 0.3));
            }
            Pop(st);
        }
    }

    /// <summary>SPFField: the GAME LINK / PRIVATE SERVER LINK boxes, edited through the hub's field.</summary>
    static void Field(HubSurface hub, double ax, double fy, double aw, string mode, int zid, string label, string ph, string val, uint acc, double fg, double resv = 80)
    {
        var HL = hub.HL;
        double fdx = ax + 54, fdw = aw - 70 - resv;
        bool on = FfmField.Edit == mode;
        double hv = hub.Hv(zid);
        string shown = on ? FfmField.Buf : val;
        bool ok = val != "";
        Txt(label, fdx, fy - 16, 220, 13, HL.fXs, FA(Alpha(on ? acc : 0xC7CBE0, on ? 200 : 120), fg), Fmt.L);
        if (ok) Txt("set", fdx + fdw - 60, fy - 16, 60, 13, HL.fXs, FA(Alpha(C_ON, 175), fg), Fmt.R);
        FillRR(fdx, fy, fdw, 26, 7, SBrush(FA(Mix(0xFF141728, 0xFF1B2038, Math.Max(hv, on ? 1.0 : 0.0)), fg)));
        MicroBackdrop(fdx, fy, fdw, 26, 7, acc, fg, Clock.Tick, 0.75);
        StrokeRR(fdx, fy, fdw, 26, 7, Pen(FA(Alpha(on ? acc : ok ? C_ON : 0xFFFFFF, on ? 190 : ok ? 80 : 28 + 50 * hv), fg), 1));
        if (on) FillRR(fdx, fy + 6, 2.6, 14, 1.3, SBrush(FA(Alpha(acc, 210), fg)));
        string vv = shown; int vo = 0;
        while (Fonts.MeasureW(vv, Fonts.fHint) > fdw - 20 && vv.Length > 1) { vv = vv[1..]; vo++; }
        if (on) FfmField.Paint(fdx + 10, fy + 5, 16, vv, vo, acc, fg, Clock.Tick, Fonts.fHint);
        if (vv == "") Txt(ph, fdx + 10, fy + 5, fdw - 20, 16, Fonts.fHint, FA(0x58C7CBE0, fg), Fmt.L);
        else Txt(vv, fdx + 10, fy + 5, fdw - 20, 16, Fonts.fHint, FA(Alpha(0xFFE8EAF6, 235), fg), Fmt.L);
    }

    static void SavedButton(HubSurface hub, double bx, double by, double bw, double fg, uint acc, long now)
    {
        var HL = hub.HL;
        double bh = 26, hv = hub.Hv(970), hvE = Ease3(hv);
        bool op = false;
        if (hvE > 0.01 || op) FillRR(bx - 3, by - 3, bw + 6, bh + 6, 11, SBrush(FA(Alpha(acc, R(20 * hvE + (op ? 22 : 0))), fg)));
        FillRR(bx, by, bw, bh, 8, VBrush(bx, by, bw, bh, FA(Mix(0xFF1B2038, 0xFF26305A, Math.Max(hvE, op ? 1.0 : 0.0)), fg), FA(0xFF12141F, fg)));
        MicroBackdrop(bx, by, bw, bh, 8, acc, fg, now, 0.9);
        StrokeRR(bx, by, bw, bh, 8, Pen(FA(Alpha(acc, R(70 + 90 * hvE + (op ? 80 : 0))), fg), 1.2));
        double gx = bx + 16, gy = by + bh / 2, lift = 2 * hvE;
        var pn = Pen(FA(Alpha(AccHi(acc, 0.35), 210 + 45 * hvE), fg), 1.5);
        Line(gx - 5, gy - 7 - lift, gx + 5, gy - 7 - lift, pn); Line(gx - 5, gy - 7 - lift, gx - 5, gy + 7 - lift, pn); Line(gx + 5, gy - 7 - lift, gx + 5, gy + 7 - lift, pn);
        Line(gx - 5, gy + 7 - lift, gx, gy + 2 - lift, pn); Line(gx + 5, gy + 7 - lift, gx, gy + 2 - lift, pn);
        Txt("SAVED PLACES", bx + 32, by + 7, bw - 96, 16, Fonts.fBadge, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.2 * hvE), 215 + 40 * hvE), fg), Fmt.L);
        double cw2 = 30;
        FillRR(bx + bw - cw2 - 24, by + 6, cw2, 16, 8, SBrush(FA(Alpha(Spf.SavedCount > 0 ? acc : 0xFFFFFF, Spf.SavedCount > 0 ? 46 : 14), fg)));
        Txt(Spf.SavedCount + "/" + Spf.SAV_MAX, bx + bw - cw2 - 24, by + 7, cw2, 14, HL.fXs, FA(Alpha(Spf.SavedCount > 0 ? AccHi(acc, 0.5) : 0xFFC7CBE0, 225), fg), Fmt.C);
        var pnC = Pen(FA(Alpha(0xFFC7CBE0, 120 + 110 * hvE), fg), 1.5);
        double cvx = bx + bw - 18 + 2 * hvE;
        Line(cvx - 3, by + bh / 2 - 4, cvx + 2, by + bh / 2, pnC); Line(cvx + 2, by + bh / 2, cvx - 3, by + bh / 2 + 4, pnC);
    }
    static void AcctButton(HubSurface hub, double bx, double by, double bw, double fg, uint acc, long now)
    {
        var HL = hub.HL;
        double bh = 26, hv = hub.Hv(1029), hvE = Ease3(hv);
        bool op = false, arm = Spf.Multi;
        if (hvE > 0.01 || op) FillRR(bx - 3, by - 3, bw + 6, bh + 6, 11, SBrush(FA(Alpha(acc, R(20 * hvE + (op ? 22 : 0))), fg)));
        FillRR(bx, by, bw, bh, 8, VBrush(bx, by, bw, bh, FA(Mix(0xFF1B2038, 0xFF26305A, Math.Max(hvE, op ? 1.0 : 0.0)), fg), FA(0xFF12141F, fg)));
        MicroBackdrop(bx, by, bw, bh, 8, acc, fg, now, 0.9);
        if (arm) FillRR(bx, by, bw, bh, 8, SBrush(FA(Alpha(acc, R(26 + 16 * hvE)), fg)));
        StrokeRR(bx, by, bw, bh, 8, Pen(FA(Alpha(arm ? acc : 0xFF6E7590, R(70 + 90 * hvE + (op ? 80 : 0))), fg), 1.2));
        double gx = bx + 16, gy = by + bh / 2;
        var pn = Pen(FA(Alpha(AccHi(acc, 0.35), arm ? 150 : 90), fg), 1.2);
        Ell(gx - 6.5, gy - 6.5, 7, 7, pn); Arc(gx - 9, gy - 0.5, 13, 12, 200, 140, pn);
        var pn2 = Pen(FA(Alpha(AccHi(acc, 0.35), 210 + 45 * hvE), fg), 1.5);
        double dx2 = 3 + 0.8 * hvE;
        Ell(gx - 2.6 + dx2, gy - 7, 7.4, 7.4, pn2); Arc(gx - 5.4 + dx2, gy - 1, 13.6, 12.6, 200, 140, pn2);
        Txt("ACCOUNTS", bx + 32, by + 7, bw - 96, 16, Fonts.fBadge, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.2 * hvE), 215 + 40 * hvE), fg), Fmt.L);
        double cw2 = 30;
        int acN = Spf.AcctCount;
        FillRR(bx + bw - cw2 - 24, by + 6, cw2, 16, 8, SBrush(FA(Alpha(acN > 0 ? acc : 0xFFFFFF, acN > 0 ? 46 : 14), fg)));
        Txt(acN + "/" + Spf.ACCT_MAX, bx + bw - cw2 - 24, by + 7, cw2, 14, HL.fXs, FA(Alpha(acN > 0 ? AccHi(acc, 0.5) : 0xFFC7CBE0, 225), fg), Fmt.C);
        var pnC = Pen(FA(Alpha(0xFFC7CBE0, 120 + 110 * hvE), fg), 1.5);
        double cvx = bx + bw - 16 + 2 * hvE;
        Line(cvx - 3, by + bh / 2 - 4, cvx + 2, by + bh / 2, pnC); Line(cvx + 2, by + bh / 2, cvx - 3, by + bh / 2 + 4, pnC);
    }

    /// <summary>SPFLive: the LIVE card — GAME, REGION, HOME, DISCORD and the module's status line.</summary>
    static void Live(HubSurface hub, double px, double py, double pw, double ff, long now, uint acc, double mt)
    {
        var HL = hub.HL;
        double cg = Clamp((mt - 0.52) / 0.4, 0.0, 1.0);
        if (cg <= 0.01) return;
        double fb = ff * cg;
        double ph = 96, sld = (1 - cg) * 12;
        FillRR(px, py, pw, ph, 12, VBrush(px, py, pw, ph, FA(Mix(0xFF141728, 0xFF1B2038, 0.5), fb), FA(0xFF10121C, fb)));
        MiniBackdrop(px, py, pw, ph, 12, acc, fb, now, 0.85);
        StrokeRR(px, py, pw, ph, 12, Pen(FA(Alpha(acc, 60 + 40 * mt), fb), 1.2));
        int cl = PushG();
        ClipRR(px, py, pw, ph, 12);
        bool live = Spf.Discord || Spf.Region || Spf.Odds;
        if (live)
        {
            double by = py + (DecT(now) * 0.035) % (ph + 40) - 20;
            FillRect(px, by, pw, 16, VBrush(px, by, pw, 16, Alpha(0xFFFFFF, 0), FA(Alpha(acc, 20), fb)));
        }
        if (Spf.Pulse != 0 && now - Spf.Pulse < 900)
        {
            double e = (now - Spf.Pulse) / 900.0, ex = 2 + e * 26;
            StrokeRR(px - ex, py - ex, pw + ex * 2, ph + ex * 2, 12 + ex * 0.4, Pen(FA(Alpha(AccHi(acc, 0.4), R(150 * (1 - e))), fb), 2.0 * (1 - e) + 0.3));
        }
        Txt("LIVE", px + 16 + sld, py + 8, 80, 14, Fonts.fBadge, FA(0x72C7CBE0, fb), Fmt.L);
        bool multi = Spf.MultiBlock?.Invoke() == true;
        uint dCol = !live || multi ? 0xFF6E7590 : Spf.InGame ? C_ON : AMBER;
        double dPh = (Math.Sin(DecT(now) * 0.0038) + 1) / 2;
        FillEll(px + 52 + sld, py + 13, 6, 6, SBrush(FA(Alpha(dCol, R(120 + 110 * dPh)), fb)));
        string gTxt = multi ? "off - several roblox clients open" : Spf.InGame ? (Spf.GameNm != "" ? Spf.GameNm : Spf.Place != "" ? "place " + Spf.Place : "in a game - resolving...") : live ? "no roblox game detected" : "features are off";
        Txt("GAME", px + 16 + sld, py + 26, 70, 13, HL.fXs, FA(0x62C7CBE0, fb), Fmt.L);
        double gw2 = pw - 180;
        Txt(FFMElide(gTxt, Fonts.fHint, gw2 - 96), px + 84 + sld, py + 26, gw2 - 96, 14, Fonts.fHint, FA(Alpha(Spf.InGame ? 0xFFE8EAF6 : 0xFF9AA8C0, 215), fb), Fmt.L);
        if (Spf.Place != "") Txt("id " + Spf.Place, px + 84 + sld + gw2 - 92, py + 26, 92, 14, HL.fXs, FA(Alpha(0xFFC7CBE0, 150), fb), Fmt.R);
        string rTxt = Spf.RegionWhy();
        Txt("REGION", px + 16 + sld, py + 42, 70, 13, HL.fXs, FA(0x62C7CBE0, fb), Fmt.L);
        bool known = Spf.Region && Spf.RegionText != "";
        Txt(FFMElide(rTxt, Fonts.fHint, pw - 180), px + 84 + sld, py + 42, pw - 180, 14, Fonts.fHint, FA(Alpha(known ? AccHi(acc, 0.45) : 0xFF9AA8C0, known ? 235 : 190), fb), Fmt.L);
        if (Spf.Region && Spf.InGame && Spf.RegionText == "")
            for (int k = 1; k <= 3; k++)
            {
                double a3 = Math.Max(0.0, Math.Sin(DecT(now) * 0.006 - k * 0.7));
                FillEll(px + 152 + sld + k * 8, py + 48, 3.4, 3.4, SBrush(FA(Alpha(acc, R(60 + 150 * a3)), fb)));
            }
        string dTxt = !Spf.Discord ? "off" : Spf.Dc == 2 ? "linked" : Spf.Dc == 3 ? (Spf.DcErr != "" ? Spf.DcErr : "unavailable") : "connecting...";
        uint dcCol = !Spf.Discord ? 0xFF9AA8C0 : Spf.Dc == 2 ? C_ON : Spf.Dc == 3 ? AMBER : 0xFFC7CBE0;
        string hTxt = !Spf.Match && !Spf.Odds ? "off" : Spf.HomeRegion == "" ? "locating you..." : Spf.MmNote != "" ? Spf.HomeRegion + "  -  " + Spf.MmNote : Spf.HomeRegion;
        Txt("HOME", px + 16 + sld, py + 58, 70, 13, HL.fXs, FA(0x62C7CBE0, fb), Fmt.L);
        uint mmc = !Spf.Match && !Spf.Odds ? 0xFF9AA8C0 : Spf.MmTries > 0 ? AMBER : 0xFFC7CBE0;
        Txt(FFMElide(hTxt, Fonts.fHint, pw - 180), px + 84 + sld, py + 58, pw - 180, 14, Fonts.fHint, FA(Alpha(mmc, 215), fb), Fmt.L);
        Txt("DISCORD", px + 16 + sld, py + 74, 70, 13, HL.fXs, FA(0x62C7CBE0, fb), Fmt.L);
        double dcw = Spf.Dc == 2 || !Spf.Discord ? pw - 180 : pw - 108;
        Txt(FFMElide(dTxt, Fonts.fHint, dcw), px + 84 + sld, py + 74, dcw, 14, Fonts.fHint, FA(Alpha(dcCol, 220), fb), Fmt.L);
        double lx = px + pw - 92 + sld, ly = py + 69;
        double lf = Spf.Dc == 2 ? 1.0 : Spf.Dc == 3 ? 0.0 : 0.5;
        FillRR(lx, ly, 76, 18, 9, SBrush(FA(Alpha(0xFFFFFF, 12), fb)));
        MicroBackdrop(lx, ly, 76, 18, 9, acc, fb, now, 0.8);
        if (lf > 0.01) FillRR(lx, ly, 76 * lf, 18, 9, VBrush(lx, ly, 76 * lf, 18, FA(Alpha(AccHi(dcCol, 0.3), 130), fb), FA(Alpha(dcCol, 90), fb)));
        StrokeRR(lx, ly, 76, 18, 9, Pen(FA(Alpha(dcCol, 120), fb), 1));
        Txt(Spf.Discord ? (Spf.Dc == 2 ? "RPC LINKED" : Spf.Dc == 3 ? "NO LINK" : "LINKING") : "IDLE", lx, ly + 2, 76, 14, HL.fXs, FA(Alpha(dcCol, 225), fb), Fmt.C);
        if (Spf.MsgAt != 0 && now - Spf.MsgAt < 4200)
        {
            double mf = 1 - Clamp((now - Spf.MsgAt - 3200) / 1000.0, 0.0, 1.0);
            Txt(Spf.Msg, px + pw - 300 + sld, py + 8, 288, 16, HL.fXs, FA(Alpha(Spf.MsgCol == C_ACC ? acc : Spf.MsgCol, 215), fb * mf), Fmt.R);
        }
        Pop(cl);
    }

    /// <summary>SPFIcon: the seventeen glyphs.</summary>
    static void Icon(int i, double cx, double cy, uint col, bool live, long now)
    {
        if (HubState.LowPerf) return;
        var pn = PenP(col, 1.4);
        var b = SBrushP(col);
        switch (i)
        {
            case 1:
                StrokeRR(cx - 8, cy - 7, 16, 12, 4, pn);
                Line(cx - 3, cy + 5, cx - 5, cy + 9, pn); Line(cx - 5, cy + 9, cx + 1, cy + 5, pn);
                for (int k = 1; k <= 2; k++)
                {
                    double dr = 1.3 + (live ? 0.35 * Math.Abs(Math.Sin(DecT(now) * 0.005 + k)) : 0);
                    FillEll(cx - 2.3 + (k - 1) * 5.2 - dr, cy - 0.9 - dr, dr * 2, dr * 2, b);
                }
                break;
            case 2:
            {
                Ell(cx - 8, cy - 8, 16, 16, pn);
                Line(cx - 6.9, cy - 3.4, cx + 6.9, cy - 3.4, pn); Line(cx - 8, cy, cx + 8, cy, pn); Line(cx - 6.9, cy + 3.4, cx + 6.9, cy + 3.4, pn);
                double mw = live ? 2.8 + Math.Abs(Math.Cos(DecT(now) * 0.0016)) * 5.2 : 5;
                Ell(cx - mw, cy - 8, mw * 2, 16, pn);
                break;
            }
            case 3:
            {
                Ell(cx - 8, cy - 8, 16, 16, pn); Ell(cx - 3.6, cy - 3.6, 7.2, 7.2, pn);
                double aa = live ? now * 0.0038 : 0.9;
                Line(cx, cy, cx + 7.4 * Math.Cos(aa), cy + 7.4 * Math.Sin(aa), PenP(col, 1.6));
                FillEll(cx - 1.5, cy - 1.5, 3, 3, b);
                if (live)
                {
                    double pk = (DecT(now) % 1400) / 1400.0, rr = 2 + pk * 7;
                    Ell(cx - rr, cy - rr, rr * 2, rr * 2, PenP(FA(col, 1 - pk), 1.2));
                }
                break;
            }
            case 4:
            {
                var pn3 = PenP(col, 1.3);
                for (int k = 1; k <= 3; k++)
                {
                    double bxk = cx - 7 + (k - 1) * 7, hk = k == 2 ? 11 : 6;
                    if (k == 2 && live) { double lift = 1.4 * (Math.Sin(DecT(now) * 0.0032) + 1) / 2; FillRR(bxk - 2.2, cy + 5 - hk - lift, 4.4, hk + lift, 1.4, b); }
                    else if (k == 2) FillRR(bxk - 2.2, cy + 5 - hk, 4.4, hk, 1.4, b);
                    else StrokeRR(bxk - 2.2, cy + 5 - hk, 4.4, hk, 1.4, pn3);
                }
                Line(cx - 9, cy + 6, cx + 9, cy + 6, pn3);
                if (live) { var p9 = PenP(FA(col, 0.9), 1.5); Line(cx - 1.2, cy - 7.4, cx + 0.4, cy - 5.8, p9); Line(cx + 0.4, cy - 5.8, cx + 3.4, cy - 9.6, p9); }
                break;
            }
            case 5:
            {
                double sp = live ? 2.6 + 0.7 * (Math.Sin(DecT(now) * 0.0026) + 1) / 2 : 1.5;
                for (int q = 1; q <= 3; q++)
                {
                    int k = 4 - q;
                    double ox = cx - 6.5 + (k - 1) * sp, oy = cy - 5.5 + (k - 1) * sp;
                    double dim = k == 1 ? 1.0 : live ? 0.62 : 0.4;
                    FillRR(ox, oy, 13, 11, 2.2, SBrushP(0xEB0B0D16));
                    var pk = PenP(FA(col, dim), 1.3);
                    StrokeRR(ox, oy, 13, 11, 2.2, pk); Line(ox, oy + 3.4, ox + 13, oy + 3.4, pk);
                    if (k == 1) FillEll(ox + 2, oy + 1.1, 2.2, 2.2, SBrushP(FA(col, live ? 0.85 : 0.45)));
                }
                break;
            }
            case 6:
            {
                for (int k = 1; k <= 5; k++)
                {
                    double bx = cx - 8.5 + (k - 1) * 4.2;
                    double amp = live ? 0.5 + 0.5 * Math.Sin(DecT(now) * 0.0042 + k * 0.9) : 0.18;
                    Line(bx, cy + 7, bx, cy + 7 - (2.5 + amp * 8.5), pn);
                }
                if (live)
                {
                    double phz = (DecT(now) * 0.0011) % 1.0, nx = cx - 8.5 + phz * 16.8;
                    int k2 = Math.Clamp((int)Math.Floor(phz * 5) + 1, 1, 5);
                    double am = 0.5 + 0.5 * Math.Sin(DecT(now) * 0.0042 + k2 * 0.9);
                    FillEll(nx - 1.7, cy + 7 - (2.5 + am * 8.5) - 1.7, 3.4, 3.4, b);
                }
                Line(cx - 10, cy + 8.2, cx + 10, cy + 8.2, PenP(FA(col, live ? 0.75 : 0.4), 1.2));
                break;
            }
            case 7:
                StrokeRR(cx - 8, cy - 7, 16, 13, 2.2, pn); Line(cx - 8, cy - 3.6, cx + 8, cy - 3.6, pn);
                { var px2 = PenP(FA(col, live ? 0.5 : 0.9), 1.3); Line(cx - 2.6, cy - 0.4, cx + 2.6, cy + 4.6, px2); Line(cx + 2.6, cy - 0.4, cx - 2.6, cy + 4.6, px2); }
                if (live) Line(cx - 10, cy + 9, cx + 10, cy - 9, PenP(col, 1.8));
                break;
            case 8:
                StrokeRR(cx - 10, cy - 6, 20, 11, 2, pn);
                for (int k = 1; k <= 4; k++)
                {
                    double bx = cx - 8 + (k - 1) * 4.4;
                    if (live && k == 4) StrokeRR(bx, cy - 3.6, 3, 5.6, 0.8, PenP(FA(col, 0.6), 1));
                    else FillRR(bx, cy - 3.6, 3, 5.6, 0.8, SBrushP(FA(col, live ? 0.85 : 0.6)));
                }
                { var p5 = PenP(FA(col, live ? 0.75 : 0.45), 1.2); for (int k = 0; k < 5; k++) { double px3 = cx - 8 + k * 4; Line(px3, cy + 5, px3, cy + 8, p5); } }
                break;
            case 9:
                StrokeRR(cx - 9, cy - 8, 18, 6.5, 1.6, pn); StrokeRR(cx - 9, cy + 1.5, 18, 6.5, 1.6, pn);
                { var bk = SBrushP(FA(col, live ? 1.0 : 0.5)); FillEll(cx + 4.5, cy - 6, 3, 3, bk); FillEll(cx + 4.5, cy + 3.5, 3, 3, bk); }
                break;
            case 10:
                StrokeRR(cx - 9, cy - 7, 13, 14, 2, pn); Line(cx - 9, cy - 3.5, cx + 4, cy - 3.5, pn); Line(cx - 2, cy + 2, cx + 9, cy + 2, pn);
                Line(cx + 5, cy - 2, cx + 9, cy + 2, pn); Line(cx + 5, cy + 6, cx + 9, cy + 2, pn);
                break;
            case 11:
                Line(cx - 7, cy - 9, cx + 2, cy + 1, PenP(col, 1.6));
                { var p3 = PenP(col, 1.3); Line(cx - 1, cy - 1, cx + 6, cy + 5, p3); Line(cx + 1, cy + 3, cx + 4, cy + 9, p3); Line(cx + 4, cy + 1, cx + 8, cy + 6, p3); Line(cx + 2.5, cy + 2, cx + 6, cy + 8, p3); }
                break;
            case 12:
                Ell(cx - 8, cy - 8, 16, 16, PenP(col, 1.5));
                { var p6 = PenP(FA(col, live ? 1.0 : 0.6), 1.6); Line(cx - 2.5, cy - 4.5, cx + 4.5, cy, p6); Line(cx + 4.5, cy, cx - 2.5, cy + 4.5, p6); Line(cx - 2.5, cy + 4.5, cx - 2.5, cy - 4.5, p6); }
                break;
            case 13:
                Ell(cx - 8, cy - 8, 16, 16, PenP(col, 1.5));
                FillPie(cx - 8, cy - 8, 16, 16, Spf.ThemeDark ? 90 : 270, 180, SBrushP(FA(col, 0.9)));
                break;
            case 14:
                FillRR(cx - 9, cy - 3, 5, 6, 1, SBrushP(FA(col, 0.9)));
                { var p5 = PenP(col, 1.5); Line(cx - 4, cy - 3, cx + 1, cy - 8, p5); Line(cx + 1, cy - 8, cx + 1, cy + 8, p5); Line(cx + 1, cy + 8, cx - 4, cy + 3, p5); }
                { var p3 = PenP(FA(col, live ? 1.0 : 0.45), 1.3); Arc(cx - 1, cy - 5, 10, 10, -40, 80, p3); Arc(cx - 2, cy - 9, 18, 18, -40, 80, p3); }
                break;
            case 15:
                StrokeRR(cx - 9, cy - 7, 18, 14, 2, pn);
                Line(cx - 8, cy + 5, cx - 2, cy - 1, pn); Line(cx - 2, cy - 1, cx + 3, cy + 4, pn); Line(cx + 3, cy + 4, cx + 8, cy, pn);
                FillEll(cx + 2.5, cy - 5, 3.5, 3.5, SBrushP(FA(col, live ? 1.0 : 0.5)));
                break;
            case 16:
            {
                var bk = SBrushP(FA(col, 0.9));
                FillEll(cx - 7, cy - 8, 14, 13, bk); FillRR(cx - 4, cy + 2, 8, 6, 1.5, bk);
                var bd = SBrushP(FA(0xFF12141F, 0.95));
                FillEll(cx - 4.5, cy - 4, 3.5, 3.5, bd); FillEll(cx + 1, cy - 4, 3.5, 3.5, bd);
                break;
            }
            default:
                Ell(cx - 8, cy - 8, 16, 16, PenP(col, 1.5));
                { var bk = SBrushP(FA(col, 0.95)); FillEll(cx - 4.5, cy - 4, 2.6, 2.6, bk); FillEll(cx + 2, cy - 4, 2.6, 2.6, bk); }
                Arc(cx - 4.5, cy - 3, 9, 8, 20, 140, PenP(FA(col, live ? 1.0 : 0.6), 1.4));
                break;
        }
    }

    // ---- SPFZone ----
    public static int Zone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        if (Spf.View == "sav" && Clock.Tick - Spf.ViewAt > 150) return SpfSaved.Zone(hub, ux, uy);
        if (Spf.View == "acct" && Clock.Tick - Spf.ViewAt > 150) return SpfAcctView.Zone(hub, ux, uy);
        double ax = HL.abx, ay = HL.aby, sysVis = SysVis();
        if (Spf.Discord && HL.modT > 0.5)
        {
            double sy = SubY(ay + SysRowY(1) - Spf.SysScr);
            if (uy >= sy && uy <= sy + SUBH && uy <= ay + sysVis + 6)
                for (int k = 1; k <= SUBN; k++) { double sx = SubX(ax, k); if (ux >= sx && ux <= sx + SUBW) return 974 + k; }
        }
        if (Spf.Fps && HL.modT > 0.5)
        {
            double sy6 = SubY(ay + SysRowY(6) - Spf.SysScr);
            if (uy >= sy6 && uy <= sy6 + SUBH && uy <= ay + sysVis + 6)
                for (int k = 1; k <= SUB6N; k++) { double sx6 = Sub6X(ax, k); if (ux >= sx6 && ux <= sx6 + SUB6W) return SUB6Z + k; }
        }
        if (HL.modT > 0.5)
            for (int rw = 9; rw <= Spf.SYSN; rw++)
            {
                var c = ChipRow(rw);
                if (c is null || !Spf.RowOn(rw)) continue;
                double syN = SubY(ay + SysRowY(rw) - Spf.SysScr);
                if (uy >= syN && uy <= syN + SUBH && uy >= ay + 10 && uy <= ay + sysVis + 6)
                    for (int k = 1; k <= c.N; k++) { double sxN = ax + 54 + (k - 1) * c.Step; if (ux >= sxN && ux <= sxN + c.W) return ChipZ(rw, k); }
            }
        if (Spf.Mem && HL.modT > 0.5)
        {
            double sy8 = SubY(ay + SysRowY(8) - Spf.SysScr);
            if (uy >= sy8 && uy <= sy8 + SUBH && uy >= ay + 10 && uy <= ay + sysVis + 6)
                for (int k = 1; k <= SUB8N; k++) { double sx8 = Sub8X(ax, k); if (ux >= sx8 && ux <= sx8 + SUB8W) return SUB8Z + k; }
        }
        if (SysTotal() > sysVis && ux >= ax + HL.abw - 12 && ux <= ax + HL.abw && uy >= ay + 10 && uy <= ay + 10 + sysVis) return 971;
        if (uy >= ay + 6 && uy <= ay + sysVis + 6 && ux >= ax && ux <= ax + HL.abw)
            for (int i = 1; i <= Spf.SYSN; i++)
            {
                double ry = ay + SysRowY(i) - Spf.SysScr;
                double top = Math.Max(ry, ay + 10), bot = Math.Min(ry + spfrh, ay + 10 + sysVis);
                if (uy >= top && uy <= bot) return ux >= ax + HL.abw - 64 && ux <= ax + HL.abw - 16 ? SWZ + i : BDZ + i;
            }
        if (uy >= ay + 140 && uy <= ay + 166)
        {
            double lbTot = HL.abw - 70, lbW = (lbTot - 8) / 2;
            if (ux >= ax + 54 && ux <= ax + 54 + lbW) return 1029;
            if (ux >= ax + 54 + lbW + 8 && ux <= ax + 54 + lbW * 2 + 8) return 970;
        }
        if (ux >= ax + HL.abw - 88 && ux <= ax + HL.abw - 16)
        {
            if (uy >= ay + 188 && uy <= ay + 214) return 968;
            if (uy >= ay + 236 && uy <= ay + 262) return 969;
        }
        if (uy >= ay + 188 && uy <= ay + 214 && ux >= ax + 54 && ux <= ax + HL.abw - 96) return 966;
        if (uy >= ay + 236 && uy <= ay + 262 && ux >= ax + 54 && ux <= ax + HL.abw - 96) return 967;
        double py = ay + spfh + 12;
        if (ux >= ax && ux <= ax + HL.abw && uy >= py && uy <= py + 96) return 1011;
        return 0;
    }

    // ---- SPFClick ----
    public static bool Click(HubSurface hub, int z)
    {
        long now = Clock.Tick;
        if (Spf.View == "sav") { SpfSaved.Clip ??= t => { try { var cb = Avalonia.Controls.TopLevel.GetTopLevel(hub)?.Clipboard; if (cb is not null) _ = cb.SetTextAsync(t); } catch { } }; return SpfSaved.Click(hub, z); }
        if (Spf.View == "acct") return SpfAcctView.Click(hub, z);
        if (FfmField.Edit == "gl" && z != 966) FfmField.End(true);
        else if (FfmField.Edit == "pl" && z != 967) FfmField.End(true);
        if (z > SWZ && z <= SWZ + Spf.SYSN) { Spf.Toggle(z - SWZ); return true; }
        if (z > BDZ && z <= BDZ + Spf.SYSN) { SpfDetail(z - BDZ); return true; }
        if (z >= 975 && z <= 977)                                          // Discord's chips
        {
            int k = z - 974;
            if (k == 1) Spf.DcAcct = !Spf.DcAcct; else if (k == 2) Spf.DcJoin = !Spf.DcJoin; else Spf.DcFocus = !Spf.DcFocus;
            Spf.Save(k == 1 ? "dcacct" : k == 2 ? "dcjoin" : "dcfocus", k == 1 ? Spf.DcAcct : k == 2 ? Spf.DcJoin : Spf.DcFocus);
            Spf.Say(k == 1 ? (Spf.DcAcct ? "DISCORD SHOWS YOUR ROBLOX ACCOUNT" : "DISCORD KEEPS YOUR ACCOUNT PRIVATE")
                  : k == 2 ? (Spf.DcJoin ? "DISCORD OFFERS A JOIN LINK TO YOUR SERVER" : "NO JOIN LINK ON DISCORD")
                  : (Spf.DcFocus ? "DISCORD SHOWS WHETHER ROBLOX IS IN FRONT" : "FOCUS OFF DISCORD"), C_ON);
            Spf.FlashAt[10 + k] = now; return true;
        }
        if (z >= 1071 && z <= 1073) { SpfFpsChips.SubToggle(z - 1070); return true; }
        if (z == 1075 || z == 1076) { SpfWin.MemSubToggle(z - 1074); return true; }
        if (z >= 3200 && z < 3200 + (Spf.SYSN - 8) * 4)
        {
            int row = 9 + (z - 3200 - 1) / 4, k = (z - 3200 - 1) % 4 + 1;
            ChipClick(hub, row, k); return true;
        }
        switch (z)
        {
            case 966: if (FfmField.Edit != "gl") FfmField.Begin("gl", 0); FfmField.Mouse(hub.PtrX); return true;
            case 967: if (FfmField.Edit != "pl") FfmField.Begin("pl", 0); FfmField.Mouse(hub.PtrX); return true;
            case 968: Join(1); hub.ClickAt[z] = now; return true;
            case 969: Join(2); hub.ClickAt[z] = now; return true;
            case 970: Spf.ViewOpen("sav"); return true;                                          // SPFViewOpen("sav")
            case 1029: Spf.ViewOpen("acct"); return true;                                        // SPFViewOpen("acct")
            case 1011: Spf.Pulse = now; return true;
        }
        return false;
    }
    static void ChipClick(HubSurface hub, int row, int k)
    {
        long now = Clock.Tick;
        if (row == 9)
        {
            Spf.Clip = t => { try { var cb = Avalonia.Controls.TopLevel.GetTopLevel(hub)?.Clipboard; if (cb is not null) _ = cb.SetTextAsync(t); } catch { } };
            if (Spf.SrvChip is not null) Spf.SrvChip(k);
            else Spf.Say("SERVER DETAILS ARRIVES WITH THE LOG TAIL", AMBER);
        }
        else if (row == 11)
        {
            if (k == 1) { Spf.CleanAge = Spf.Next(Spf.CleanAge, Spf.CleanAges); Spf.Save("cleanage", Spf.CleanAge); Spf.Say("CLEAN FILES " + Spf.CleanAgeLabel().ToUpperInvariant() + " OLD", C_ON); }
            else Task.Run(() => Spf.CleanRun(true));
        }
        else if (row == 13)
        {
            Spf.ThemeDark = !Spf.ThemeDark; Spf.Save("themedark", Spf.ThemeDark);
            if (Spf.Theme) Spf.ThemeApply(true); else Spf.Say("ROBLOX APP THEME: " + Spf.ThemeName().ToUpperInvariant() + " - TURN THE ROW ON TO APPLY IT", AMBER);
        }
        else if (row == 16) _ = SpfMods.DeathPickAsync(hub);
        else if (row == 17) SpfMods.EmojiNext();
        Spf.FlashAt[100 + row * 4 + k] = now;
    }
    /// <summary>JOIN: the GAME LINK (1) or the PRIVATE SERVER LINK (2) through the client's protocol.</summary>
    static void Join(int which)
    {
        if (Spf.Hunting && Spf.HuntWhich == which) { SpfMatch.HuntStop("stopped"); Spf.Say("HUNT STOPPED", AMBER); return; }
        SpfMatch.HuntStart(which);                                        // SPFHuntStart: JOIN once, or HUNT with the finder on
    }
    public static void FieldSync(string mode)
    {
        if (mode == "gl") { Spf.MmLink = FfmField.Buf.Trim(); Spf.Save("gamelink", Spf.MmLink); }
        else if (mode == "pl") { Spf.MmPriv = FfmField.Buf.Trim(); Spf.Save("privlink", Spf.MmPriv); }
    }
    /// <summary>The press half: the link fields and the sheets' fields take the caret on the button going down.</summary>
    public static bool Press(HubSurface hub, int z)
    {
        string mode = z == 966 ? "gl" : z == 967 ? "pl" : z == 1006 ? "sa" : z == 1009 ? "sl" : z == 1013 ? "rn" : z == 1035 ? "at" : "";
        if (mode == "") return false;
        if (FfmField.Edit != mode) FfmField.Begin(mode, 0);
        FfmField.Mouse(hub.PtrX);
        return true;
    }
    /// <summary>SPFDragScroll: the systems column's scrollbar, zone 971 / drag 16.</summary>
    public static void DragScroll(HubSurface hub, double uy)
    {
        var HL = hub.HL;
        double sysVis = SysVis(), sysTot = SysTotal();
        double th2 = Math.Max(22, sysVis * (sysVis / Math.Max(1, sysTot)));
        Spf.SysScrT = HubUI.ScrollFromY(HL, uy, HL.aby + 10, sysVis, sysTot, sysVis, th2, Spf.SysScr); Spf.SysScr = Spf.SysScrT;
        hub.Tim(Pace.TICK_A);
    }
    public static bool Wheel(HubSurface hub, double ux, double uy, double delta)
    {
        var HL = hub.HL;
        if (Spf.View == "sav") return SpfSaved.Wheel(hub, ux, uy, delta);
        if (Spf.View == "acct") return SpfAcctView.Wheel(hub, ux, uy, delta);
        if (ux >= HL.abx && ux <= HL.abx + HL.abw && uy >= HL.aby && uy <= HL.aby + SysVis() + 10)
        {
            Spf.SysScrT = Clamp(Spf.SysScrT - delta * spfrg, 0.0, Math.Max(0.0, SysTotal() - SysVis()));
            return true;
        }
        return false;
    }

    // ---- SPFDetail(i): the seventeen explainers ----
    static readonly string[] DetHints = { "show the roblox game you are in on your discord profile", "show which region your current server is hosted in", "rejoin until you land on a server in your own region",
        "picks a server this session already measured as close to you", "run more than one roblox client at once, on different accounts", "more frames without changing a single graphics setting",
        "closes RobloxCrashHandler.exe after each launch to free its memory", "evicts the client's memory on a schedule - can cause stutter", "type, uptime, id and a rejoinable history of every server",
        "leaving a game closes the client instead of the home app", "roblox logs and cache older than an age you pick", "opens YURI when windows starts", "forces the roblox app to light or dark",
        "the 2014 walk, jump and get-up sounds", "the 2020 avatar editor scene", "any .ogg you pick replaces the oof", "catmoji, or the windows 11, 10 or 8 set" };
    static readonly string[] DetBodies =
    {
        "Publishes what you are playing to Discord over its local pipe - the game's name, its icon, how long you have been in, and a link to the game page. The three chips on this row add your Roblox account, a join link to this exact server, and whether Roblox is the window you are actually looking at.",
        "Reads the server's address out of the client log and looks up where it is hosted, then shows the region and how far away it is. The lookup runs in a separate process, so a slow reply never stalls the interface.",
        "After a join it checks the distance, and if the server is too far it closes the client and rejoins - up to fifteen times, asking for a different instance each go. It stops the moment it lands close enough. This is the one that restarts Roblox on you; JOIN only rerolls while it is on.",
        "Experimental, and it never rejoins anybody. Every join teaches it the distance of one specific server of one specific place; a later join then asks for a server it already measured as close instead of letting the matchmaker draw blind. It cannot help on a first visit, and it improves the odds rather than guaranteeing anything.",
        "Roblox allows one client per machine by holding a named system object; a second launch finds it, hands the join to the client already running and quits. With this on, YURI holds that object instead, so no client owns it and every launch starts its own. Turn it on BEFORE launching the clients you want.  LAUNCH THEM FROM THE WEBSITE, not from the desktop or Start Menu shortcut. The client has no login screen of its own - it is handed a one-time ticket by the browser at launch, and starting the executable directly gives it no ticket, which is why a shortcut launch opens a logged-out client that can do nothing. So: sign in as the first account, press Play on any game, then sign in as the second account and press Play again. Each client keeps the account whose ticket started it, so signing out afterwards is fine.  A client that was ALREADY RUNNING when you turned this on is not covered by it - its singleton was resolved when it launched, and this only changes what happens to launches after it. The row shows a RESTART ROBLOX mark whenever that is the case, and says how many clients it applies to: close those and start them again from the website. The mark goes as soon as nothing older than the switch is left running.  Switching this off releases the object; clients already open are unaffected. Two things worth knowing - Roblox works against this deliberately and its anti-cheat may close or crash the extra clients, and the fast flag manager attaches to one client at a time, so flags land in whichever it picked up rather than in all.",
        "Raises the frame rate by getting Windows out of the client's way, and by changing nothing else. No fast flag is written, no client file is edited, and nothing under CLIENT SETTINGS is read or altered - your graphics quality, texture detail, lighting and framerate cap are exactly where you left them. What it changes is how the operating system treats the process:  The client is raised in priority so its render and scheduler threads stop giving up their slice to background work. How far it is raised depends on how many logical processors you have, and that is deliberate: the compositor, the audio graph and csrss all run at High, and on a machine without room to spare a client at High can crowd them out. Starving the compositor does not make frames appear - it makes them arrive late and unevenly, which plays worse than a lower average. So High only where there is headroom for it, and Above Normal below that, which still outranks every piece of ordinary background work on the machine. Never Realtime.  Windows power throttling is switched off for the client you are actually looking at, and only that one. Letting a background client off the leash runs it at full clocks for a window nobody is watching, and on a laptop that heat comes straight out of the boost headroom of the client that IS being watched. This is the one that usually matters most: on modern laptops and on desktops with efficiency cores, Windows parks a process it reads as idle onto the slow cores and holds its clocks down, and a windowed game that is not the foreground window qualifies far more often than you would expect.  On a CPU with two kinds of core - Intel 12th generation and later - the client MAY be told to prefer the performance cores, and this is the one lever here whose benefit is not certain. It sets the default for every thread in the process, not just the one that matters, so on a chip with a lot of efficiency cores it asks the whole worker pool to crowd onto the performance half - and what the workers lose can cost more than the render thread gains. Windows own scheduler is built for that decision and is usually good at it.  So it is applied only when the row has MEASURED the client to be bottlenecked on a single thread, which is the one case where the render thread placement is the whole story. Otherwise Windows is left alone, and the row says why rather than counting it as a failure. On any CPU whose cores are all alike it does nothing at all and is skipped.  The client is allowed to ask for a finer system timer. The default tick is 15.6 ms, so a frame that finished early could sit waiting on the scheduler for longer than it took to draw. Note this is done TO THE CLIENT: since Windows 10 2004 a program raising the timer raises it only for itself, so a booster that calls timeBeginPeriod in its own process - as this one used to - does nothing for the game at all.  The client's memory priority is held at normal. Windows demotes processes it reads as background, and a demoted client is the first thing trimmed when memory gets tight - then faults its pages back in mid-frame. That is a hitch, and a hitch is worse to play through than a slightly lower average.  A client launched later is picked up on its own within a few seconds, and the client you tab to takes the top slot the moment you tab to it. Turning the switch off puts it all back: normal priority, throttling handed back to Windows, core preference cleared.  THE ROW TELLS YOU WHETHER ANY OF IT CAN HELP. It measures the client's busiest single thread, because a game runs out of road on its render thread long before it does on total CPU. If that thread is sitting near 100% of one core you are CPU-bound and this feature has something to work with; if it is not, the row says so plainly rather than letting a switch that is working look like a switch that is broken. It also names any lever that reported nothing - the core preference does nothing on a CPU whose cores are all alike, and the timer bit is Windows 11 only, so a count below the full six is normal and not a fault.  ONE RULE RUNS THROUGH ALL OF IT: nothing here is ever set lower than it was found, and everything is put back to what was found rather than to what the default happens to be. A client you had already raised in Task Manager is not demoted to make room for the one in front, and it comes back on High rather than on Normal when the switch goes off. A value you have already tuned past the target is left at your number, not overwritten with ours. A graphics preference you set yourself is neither claimed nor deleted. The guiding assumption is that a machine already configured better than this feature would configure it is a machine to leave alone.  THREE OPT-IN CHIPS sit on this row, and none is on by default because all three reach further than the rest.  GPU PREF is the largest single thing here on a laptop with two graphics chips. If the client came up on the integrated one it is not a few percent short, it is running on a different and far smaller GPU, and no amount of scheduling recovers that. This writes the same per-application preference the Windows Settings graphics page writes, asking for the high-performance card. Unlike everything else here it is a registry value: it survives a reboot, and Windows reads it when the process STARTS, so it does nothing for a client already open - restart Roblox for it to take. It refuses to turn on at all where there is only one GPU, and if Windows already has a preference set for Roblox it leaves that alone rather than overruling a choice you made - including when that preference already says high performance, which it neither claims nor removes. Turning the chip off deletes only a value YURI itself wrote, and it is recorded in the same restore file as everything else, so a crash cannot strand it either.  While the game has focus YURI also drops its own priority from high to normal. The suite raises itself at startup so its overlays stay smooth, but with the boost on that put the hub in the same class as the client it was boosting - and QUIET was demoting your browser to clear a path YURI was still standing in. It goes back the moment you tab away from the game. Normal rather than lower on purpose: this process holds the keyboard and mouse hooks for AUTOBLOCK and PUZZLE AI, and Windows drops a hook whose owner is too slow to answer.  QUIET drops known background CPU hogs - browsers, launchers, sync clients, the search indexer - to below-normal priority while the boost is on. Raising the client is only half of a contention problem; this is the other half, and on a CPU-bound machine it is worth as much as everything above put together, because below-normal costs those programs nothing at all until the moment they actually collide with the game. It works from a fixed list and never guesses: Discord, Spotify and any capture or recording tool are deliberately not on it, because those are exactly the programs that are cheap on average and terrible to starve for the one millisecond that matters. Neither is the Start menu and search UI, which was on the list and was taken off it: demoting that costs nothing while you are in the game and then makes Start feel slow the moment you tab out, which is a slowdown you would actually notice bought for a gain you would not. The rule the list follows is that it may only contain things whose slowdown cannot be felt. It only ever demotes something it finds at normal, it remembers what it found and puts that back, and it names what it touched when you turn it on. It is released when the chip goes off, when the switch goes off, and when YURI exits.  SYSTEM is the only control in YURI that changes anything outside a process, and it is the only one whose undo is written to disk before the change is made. It switches Windows to the High performance power plan, turns off Game DVR and its background capture, turns Game Mode on, and - if YURI is running as administrator - lowers the CPU share Windows reserves for low-priority work and lifts the network throttle. The power plan is the big one, especially on a laptop: Balanced caps the processor's minimum state and holds boost back, and no amount of scheduling priority argues with a clock that is not being allowed to rise. The rest are modest, and are described that way rather than sold as more than they are. If your plan already lets the processor run unrestricted - Ultimate Performance, High performance, a duplicate of either, or a custom plan you built - it is left exactly as it is and the row says so. That test reads what the plan actually does rather than what it is called, because Ultimate Performance is normally created by duplicating the built-in one and a duplicate carries a different identifier: matching on the name or the identifier would take the better plan away from the one person who had gone to the trouble of setting it up.  HOW THE UNDO WORKS, because a machine-wide change deserves better than a promise. Before any value is written, the value that was there is read and recorded in YURI\\config\\fpsrestore.ini. A value that did not exist is recorded as absent, so putting it back means deleting it rather than writing a zero and calling that the same thing. The record is written FIRST and the change second: a record with no change is harmless, a change with no record is the thing this is built to prevent. Restoring an entry clears it and the file is deleted once it is empty, so the file existing at all means something is still outstanding - and every launch restores whatever it finds there before re-applying anything. Turn the chip off, turn the switch off, close YURI, or have it killed outright and lose power: the settings come back either immediately or at the next launch.  Two things it will not do. It never touches hardware-accelerated GPU scheduling - that genuinely helps on some GPU and driver combinations and hurts on others, and it needs a reboot to try either way, so it is read and reported and left to you. And it writes nothing whose effect cannot be explained: the scheduler quantum value every tweak guide recommends is contradicted by half the guides recommending it, and a number written into the scheduler that nobody can account for is a guess with a reboot attached rather than a tweak.  One thing no switch here can reach: if the machine is on battery, or Battery Saver is on, Windows holds the clocks down harder than any of this can lift them. The row says so when it sees it. It also reads your framerate cap from CLIENT SETTINGS and says so if one is set - nothing on this row can push a client past a cap it was told to respect, and letting you chase that with these switches while the answer sits one panel away would be a worse silence than admitting it.  How much you gain depends entirely on what was holding you back. If the GPU is the limit this does very little; if the client was being throttled or fighting for CPU, it can be a large difference.",
        "Roblox starts a second program beside every client: RobloxCrashHandler.exe. It is a watchdog. It sits idle for the whole session, and only does anything if the client dies - then it shows the crash box and sends the crash report to Roblox. While the client is running it contributes nothing, so with this on it is closed as soon as it is seen, which frees the memory it holds - a few tens of megabytes, and one process fewer on the list. Bloxstrap, Fishstrap and Bubblestrap all do the same, on by default.  HOW IT WORKS. The same process list the fast flag manager already walks to find clients now also notes the crash handler, so this row costs no enumeration of its own; whatever it finds is closed on the same tick. That poll runs every 1.2 seconds while no client is attached and every 4 while one is, so a handler lives at most a few seconds past the launch that started it. Turning the switch on also sweeps once immediately, so a handler already sitting beside a running client goes at the click. Studio starts the same executable and it is closed too; Studio does not need it. Nothing is written to any Roblox file and nothing is done to the client itself.  THE COST, plainly: if the client crashes while this is on, it simply closes. No 'Roblox has crashed' box, no crash report, and nothing is sent to Roblox - which is why it is off by default. If a crash is something you would want to read about or report, leave this off. Turning it off puts nothing back: a handler already closed stays closed, and Roblox starts a fresh one with the next client. The row counts what it closed this session; a count that never moves with clients running is the sign to run YURI as administrator, since a process it cannot open it cannot close.",
        "On a schedule - the first chip - this empties the working set of every Roblox client: the pages the client has in physical memory are handed back to Windows, and the number beside it in Task Manager drops, usually by a lot. This is the same call Bloxstrap, Fishstrap and Bubblestrap make for their Memory Trimmer.  BE CLEAR ABOUT WHAT IT DOES. The client has not stopped needing those pages. It faults them back in as it touches them, which is the stutter you feel just after each trim, and within a minute or so its working set is back to roughly where it was. Nothing is freed for good; what changes is WHEN the memory is given up. That is worth something on a machine with too little RAM, where the alternative is Windows paging out whatever it likes - your browser, Discord, the game itself - at a moment of its choosing. On a machine with enough RAM it is a hitch bought for a smaller number, which is exactly why FPS BOOST does not do it: that row holds the client's memory priority at normal so Windows will NOT trim it, and with both switches on they are pulling in opposite directions. Use one or the other.  THE SECOND CHIP is what makes this a tool rather than a tic. With a limit set, a client is left alone until its working set is over that size, so the trim happens only once the client has actually grown into a problem - and the chip goes amber while it is holding the trimmer back. NO LIMIT trims every client every time, which is Bubblestrap's default and the one setting here that is hard to recommend. Both chips cycle through presets; zeal.ini takes any number under [special] memint (seconds) and memthr (MB).  Each tick opens every client with the two rights the call needs and no others, reads the size, trims if allowed, reads it again for the tally, and closes the handle at once - never held between ticks, because the anti-cheat is known to object to a foreign handle kept open for long. Nothing is written to any file and no setting of the client's is touched. Turning it off puts nothing back, because there is nothing to put back: the client has already reclaimed what it needs. The row shows the biggest client's working set and how many times the trimmer has fired; a size that never appears means the handle was refused, and running YURI as administrator is the fix.",
        "What the client's log says about the server you are in, kept instead of skimmed past. TYPE: private if you came in by a private server link, reserved if a game teleported you into one it made, public otherwise - read from the join call the client names. UPTIME: the client prints a 'Server Prefix' line on join with the server's start time in its name; the age counts from that. Some servers do not print it, and the row then says so rather than guessing. ID: the instance id, which is what a rejoin needs.  HISTORY: every server this session joined, newest first, up to forty, with the time and the type. REJOIN LAST opens a roblox:// link to the most recent one, so it goes through whatever launcher owns the protocol - Roblox's own or a strapper - with no cookie of YURI's involved; if that server has since emptied and closed, Roblox says so. COPY LINK copies the same link for the server you are in now, which anyone with Roblox installed can click; COPY ID copies the bare instance id. Turning the row off clears the history.",
        "When you leave a game, the client stays open and shows the Roblox home app. With this on, the moment the log says the client is going back to the app, YURI asks that client's window to close - the same thing Bloxstrap's 'don't exit to desktop app' does, and with the same message Windows sends when you press the X. It is not a kill: a client that refuses the close is left alone. It acts on exactly one line, the one Roblox prints for a return to the app, so a teleport between places, a kick or a disconnect dialog does not trigger it; and only on a line read live, never on a log being replayed from before the switch was on. The row counts what it closed this session.",
        "Roblox never clears its own logs (%LOCALAPPDATA%\\Roblox\\logs) or its temp cache (%TEMP%\\Roblox); on a machine that has played for months they run to gigabytes. With this on, files there older than the chip's age are deleted once at every launch of the hub, and CLEAN NOW does it on the spot. Two hundred files per folder per run at most, so a folder with ten thousand files cannot stall the hub - it catches up over a few launches. Nothing else of Roblox's is touched: the install, its versions, your settings, your login all stay. The log a running client is writing is never old enough to qualify. The row shows the last run's tally.",
        "Puts YURI in the Windows startup list: one value under HKCU\\...\\CurrentVersion\\Run, pointing at the exact path YURI is running from right now. It is rewritten at every launch while the switch is on, so moving or renaming the script does not leave Windows pointing at where it used to be. Off deletes the value. Nothing under HKLM and nothing that needs administrator. The row says whether the entry is actually there, read back from the registry, not from the switch.",
        "The Roblox app remembers light or dark per account in %LOCALAPPDATA%\\Roblox\\appStorage.json. With this on, every entry there is set to the chip's theme when the switch or the chip changes, and again each time the hub starts; the app reads it when it starts, so the next Roblox launch picks it up. If Roblox has never written that file (a fresh install that has not opened the app) there is nothing to rewrite, and the row says so instead of inventing the file. Off changes nothing back: Roblox keeps whatever it has at that moment.",
        "Roblox's character sounds from 2014: the walk, the jump and the get-up as they were, and silence for the falling, landing, swimming and water-impact sounds that did not exist then. Seven files under content\\sounds. Note that a game which supplies its own sounds is ",
        "The avatar editor scene as it was in 2020 - the place file the app loads behind the avatar, ExtraContent\\places\\Mobile.rbxl, ",
        "The sound the character makes on death, content\\sounds\\ouch.ogg, replaced by an .ogg of yours - PICK FILE opens the chooser, and the chip then shows CHANGE FILE. It must be an .ogg; Roblox will not play anything else from that slot, and YURI does not convert. A ",
        "The emoji Roblox draws in chat and text come from one font file, content\\fonts\\TwemojiMozilla.ttf. The chip picks which set replaces it: Catmoji, or the emoji of Windows 11, Windows 10 or Windows 8.1 - the same four Bloxstrap offers, fetched from its repository. Changing the chip while the row is on fetches and ",
    };
    static void SpfDetail(int i)
    {
        if (i < 1 || i > Spf.SYSN) return;
        string body = DetBodies[i - 1];
        string held = i == 6 ? Spf.FpsHeld?.Invoke() ?? "" : i == 8 ? Spf.MemHeld?.Invoke() ?? "" : i == 9 ? Spf.SrvHeld?.Invoke() ?? "" : "";
        Detail.OpenSheet(Names[i - 1], DetHints[i - 1], body + held, "SPECIAL FEATURES");
    }
}
