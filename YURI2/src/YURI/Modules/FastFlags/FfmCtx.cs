using Avalonia.Controls;
using Yuri.Core;
using Yuri.Gfx;
using Yuri.Shell.Hub;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;

namespace Yuri.Modules.FastFlags;

/// <summary>
/// The right-click menu over any text field and over the script editor
/// (FFMCtx*): Cut / Copy / Paste with their accelerators, greyed when they
/// cannot act. Zones 701-703 are the rows, 700 the rest of the menu; a
/// click anywhere else closes it. The clipboard's contents are read when the
/// menu opens, so Paste knows whether it has anything to offer.
/// </summary>
public static class FfmCtx
{
    public const double CTXW = 138, CTXH = 90, CTXIH = 26;
    public static bool On; public static long At; public static double X, Y;
    static string _clip = "";
    public static bool Live => On || Clock.Tick - At <= 200;
    /// <summary>FFMActFld: 3 for the script editor, 1 for a hub field, 0 for none.</summary>
    static int ActFld() => ScriptHub.Scr.Focus == 1 ? 3 : FfmField.Edit != "" ? 1 : 0;
    static bool HasSel() => ActFld() == 3 ? ScriptHub.Scr.SelLen() > 0 : FfmField.Sel >= 0 && FfmField.Sel != FfmField.Car;
    public static (string t, bool on)[] Items() => new[] { ("Cut", HasSel()), ("Copy", HasSel()), ("Paste", _clip.Trim() != "") };
    public static void Open(HubSurface hub, double ux, double uy)
    {
        On = true; At = Clock.Tick;
        X = Math.Min(ux, HubLayout.pd + HubLayout.cw - CTXW - 6);
        Y = Math.Min(uy, HubLayout.pd + HubLayout.ch - CTXH - 6);
        _ = ReadClip(hub);
        hub.Tim(Pace.TICK_A);
    }
    static async Task ReadClip(HubSurface hub)
    {
        try { var cb = TopLevel.GetTopLevel(hub)?.Clipboard; _clip = cb is null ? "" : await cb.GetTextAsync() ?? ""; } catch { _clip = ""; }
        hub.Tim(Pace.TICK_A);
    }
    public static void Close(HubSurface hub) { if (!On) return; On = false; At = Clock.Tick; hub.Tim(Pace.TICK_A); }
    /// <summary>FFMCtxRun: the row's action against whichever field is live.</summary>
    public static void Run(HubSurface hub, int i)
    {
        var items = Items();
        if (i < 1 || i > 3 || !items[i - 1].on) { Close(hub); return; }
        var cb = TopLevel.GetTopLevel(hub)?.Clipboard;
        if (ActFld() == 3)
        {
            if (i == 1) { string t = ScriptHub.Scr.SelText(); if (t != "") { try { cb?.SetTextAsync(t); } catch { } ScriptHub.Scr.DelSel(); } }
            else if (i == 2) { string t = ScriptHub.Scr.SelText(); if (t != "") try { cb?.SetTextAsync(t); } catch { } }
            else ScriptHub.Scr.Paste(_clip);
        }
        else
        {
            if (i == 1) { string t = FfmField.SelText(); if (t != "") { try { cb?.SetTextAsync(t); } catch { } FfmField.DelSel(); } }
            else if (i == 2) { string t = FfmField.SelText(); if (t != "") try { cb?.SetTextAsync(t); } catch { } }
            else foreach (char c in _clip.Replace("\r", "").Replace("\n", " ")) FfmField.Char(c.ToString());
        }
        Close(hub);
    }
    public static int Zone(double ux, double uy)
    {
        if (!On) return 0;
        if (ux >= X && ux <= X + CTXW && uy >= Y + 6 && uy <= Y + 6 + 3 * CTXIH)
        {
            int it = (int)Math.Floor((uy - Y - 6) / CTXIH) + 1;
            if (it >= 1 && it <= 3) return 700 + it;
        }
        return 700;
    }
    public static bool Click(HubSurface hub, int z)
    {
        if (!On) return false;
        if (z >= 701 && z <= 703) { Run(hub, z - 700); return true; }
        if (z == 700) { Close(hub); return true; }
        Close(hub);
        return false;
    }
    public static void Draw(HubSurface hub, long now, uint acc)
    {
        if (!On && now - At > 200) return;
        double t = Ease3(Clamp((now - At) / 170.0, 0.0, 1.0));
        if (!On) t = 1 - t;
        if (t <= 0.01) return;
        var items = Items();
        double mx4 = X, my4 = Y;
        int st = PushXform(mx4 + 10, my4 + 10, 0.94 + 0.06 * t, 0);
        for (int k = 1; k <= 5; k++) FillRR(mx4 - k, my4 - k + 3, CTXW + k * 2, CTXH + k * 2, 11 + k, SBrush(Alpha(0x000000, R(26 * (1 - k / 6.0) * t))));
        FillRR(mx4, my4, CTXW, CTXH, 10, VBrush(mx4, my4, CTXW, CTXH, Alpha(AccHi(0xFF1B2038, 0.03), R(252 * t)), Alpha(0xFF141728, R(252 * t))));
        MiniBackdrop(mx4, my4, CTXW, CTXH, 10, acc, t, now, 0.8);
        StrokeRR(mx4, my4, CTXW, CTXH, 10, Pen(Alpha(acc, R(120 * t)), 1.1));
        for (int i4 = 1; i4 <= 3; i4++)
        {
            double iy = my4 + 6 + (i4 - 1) * CTXIH, hv = hub.Hv(700 + i4);
            bool on = items[i4 - 1].on;
            if (on && hv > 0.01)
            {
                FillRR(mx4 + 4, iy, CTXW - 8, CTXIH - 2, 6, SBrush(Alpha(acc, R(46 * hv * t))));
                FillRR(mx4 + 7, iy + 6, 2.5, CTXIH - 14, 1.2, SBrush(Alpha(acc, R(210 * hv * t))));
            }
            Txt(items[i4 - 1].t, mx4 + 18, iy - 1, CTXW - 56, CTXIH, Fonts.fHint, Alpha(on ? AccHi(0xFFE8EAF6, hv * 0.5) : 0xFF6E7590, R((on ? 235 : 105) * t)), Fmt.L);
            string acc2 = i4 == 1 ? "Ctrl+X" : i4 == 2 ? "Ctrl+C" : "Ctrl+V";
            Txt(acc2, mx4 + CTXW - 62, iy - 1, 50, CTXIH, hub.HL.fXs, Alpha(0xFF9AA8C0, R((on ? 130 : 70) * t)), Fmt.R);
        }
        Pop(st);
    }
}
