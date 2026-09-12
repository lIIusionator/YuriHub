using Avalonia.Input;
using Yuri.Core;
using Yuri.Gfx;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.G;

namespace Yuri.Modules.FastFlags;

/// <summary>
/// The hub's single-line field (FFM.edit / FFM.buf / FFM.car / FFM.sel).
/// One field is live at a time: "q" the flag panel's filter/add box, "v" a
/// staged flag's value, "db" the database search — later modules add their
/// own modes. Keys are the .ahk's: Backspace, Delete, arrows with Shift and
/// Ctrl, Home, End, Ctrl+A/C/X/V, Enter commits, Escape cancels, Tab
/// commits. The buffer is capped at 96 characters as before.
/// </summary>
public static class FfmField
{
    public static string Edit = "";                                  // FFM.edit: the mode, "" when nothing is live
    public static int EditRow = -1;                                  // FFM.editRow (0-based)
    public static string Buf = "";                                   // FFM.buf
    public static int Car;                                           // FFM.car
    public static int Sel = -1;                                      // FFM.sel: the anchor, -1 for none
    public static bool MSel;                                         // FFM.msel: a mouse selection in progress
    public static long CaretAt, DblAt;
    /// <summary>The field's text origin, per mode (FFMFldTextX).</summary>
    public static Func<string, double> TextX = _ => 0;
    /// <summary>Called on Enter in a mode the field does not handle itself (db, rf*).</summary>
    public static Action<string>? OnEnter;
    /// <summary>Called after the buffer changed (FFMSyncBuf): the panel resets its scroll and caches.</summary>
    public static Action<string>? OnSync;

    // FFMFldFont. EDIT PROFILE's two fields and the script hub's name and
    // description draw in fHint and were measured in the monospace face, so the
    // selection and the caret were laid out in a font the text was not drawn in -
    // over wide letters like w the highlight ended well short of the word.
    public static Font Font => Edit is "db" or "gl" or "pl" or "sa" or "sl" or "rn" or "at" or "pn" or "pb" or "sn" or "sd"
        ? Fonts.fHint : Yuri.Shell.Hub.HubSurface.Live?.HL.fM ?? Fonts.fHint;
    /// <summary>The face the field was last PAINTED with - the drawing site knows, so it tells us rather than us inferring it.</summary>
    static Font? LastFont;
    static Font Face => LastFont ?? Font;
    /// <summary>The face this field would lay its caret out in, for the harness.</summary>
    public static Font FontPublic => Face;

    /// <summary>FFMBeginEdit(mode, row): one field live; the previous commits.</summary>
    public static void Begin(string mode, int row)
    {
        if (Edit != "" && (Edit != mode || (mode == "v" && EditRow != row))) End(true);
        Edit = mode; EditRow = row; CaretAt = Clock.Tick;
        // DblAt is what makes a second click inside 420 ms select everything.
        // Left set, a click on ONE field followed by a click on ANOTHER inside
        // that window selected the whole of the second one - which read as the
        // first field's highlight never going away.
        Sel = -1; MSel = false; DblAt = 0; LastFont = null; LastOff = 0;
        Buf = mode == "db" ? Ffm.Q
            : mode == "q" ? Ffm.Q2
            : mode == "v" && row >= 0 && row < Ffm.Flags.Count ? Ffm.Flags[row].Value
            : mode == "gl" ? Special.Spf.MmLink
            : mode == "pl" ? Special.Spf.MmPriv
            : mode is "sa" or "sl" or "rn" ? Special.SpfSaved.BufFor(mode)
            : mode == "at" ? Special.SpfAcct.BufFor()
            : mode is "rfn" or "rfv" or "rfe" or "rfd" ? ClientSettings.RSetFx.BufFor(mode)
            : mode == "sn" ? ScriptHub.Scr.Name : mode == "sd" ? ScriptHub.Scr.Desc
            : mode is "pn" or "pb" ? Shell.Hub.EditProfile.BufFor(mode)
            : "";
        Car = Buf.Length;
        if (mode is "rfn" or "rfv" or "rfd") ClientSettings.RSetFx.FocSet(mode);
        else if (mode == "rfe") ClientSettings.RSetFx.FocSet(ClientSettings.RSetFx.FxSel);
        Poke();
    }

    /// <summary>FFMEndEdit(commit): a value is checked against its prefix; the filter is kept.</summary>
    public static void End(bool commit = true)
    {
        if (Edit == "") return;
        if (commit)
        {
            if (Edit == "v" && EditRow >= 0 && EditRow < Ffm.Flags.Count)
            {
                string v = Buf.Trim();
                if (v != "")
                {
                    var fl = Ffm.Flags[EditRow];
                    string pfxE = Ffm.PfxOf(fl.Name);
                    string fault = pfxE != "" ? Ffm.TypeFault(pfxE, v) : "";
                    if (fault != "") Ffm.Say(fault.ToUpperInvariant(), 0xFFFB7185);
                    else
                    {
                        fl.Value = v; fl.Type = Ffm.TypeOf(v);
                        Ffm.Flash[EditRow] = Clock.Tick;
                        Ffm.SaveFlags();
                        Ffm.Changed?.Invoke();
                    }
                }
            }
            else if (Edit == "q") Ffm.Q2 = Buf.Trim();
            else if (Edit == "gl" || Edit == "pl") OnSync?.Invoke(Edit);
            else if (Edit is "sa" or "sl" or "rn") Special.SpfSaved.Sync(Edit, Buf);
            else if (Edit == "at") Special.SpfAcct.SyncBuf(Buf);
            else if (Edit is "rfn" or "rfv" or "rfe" or "rfd") ClientSettings.RSetFx.Commit(Edit, Buf);
            else if (Edit == "sn") ScriptHub.Scr.Name = Buf.Trim();
            else if (Edit == "sd") ScriptHub.Scr.Desc = Buf.Trim();
            else if (Edit is "pn" or "pb") Shell.Hub.EditProfile.Commit(Edit, Buf);
        }
        if (Edit is "rfn" or "rfv" or "rfe" or "rfd") ClientSettings.RSetFx.FocSet("");
        Edit = ""; EditRow = -1;
        Sel = -1; MSel = false;
        OnSync?.Invoke("");
        Poke();
    }

    /// <summary>FFMKeepEdit(z): the zones a click may land on without closing the live field - the field itself and the controls that act on it.</summary>
    public static bool KeepEdit(int z, int tab, int hubMod)
    {
        if (z >= 700 && z <= 703) return true;
        switch (Edit)
        {
            case "db": return z == 437 || z == 435 || (z >= 601 && z <= 611);
            case "q": return z == 422 || z == 423;
            case "v": return z >= 541 && z <= 545;
            case "rfn": case "rfv": return z is 1601 or 1602 or 1603;
            case "rfe": return z > ClientSettings.RSetFx.FXZVAL && z <= ClientSettings.RSetFx.FXZDEL + ClientSettings.RSetFx.FXZMAX;
            case "rfd": return z is 1606 or 1607 or 1604 || (z > ClientSettings.RSetFx.FXZDB && z <= ClientSettings.RSetFx.FXZDB + ClientSettings.RSetFx.DBSLOTS);
            case "sn": case "sd": return z is 66 or 67 or 240 or 241;
            case "pn": case "pb": return z is 136 or 137;
            default: return tab == 2 && hubMod == 5 && z >= 400;                 // the SPECIAL fields: anything on their own panel
        }
    }
    /// <summary>FFMBlur(): a click elsewhere commits the filter and a value, cancels the rest.</summary>
    /// <summary>
    /// Blur: COMMIT. The .ahk only had two fields when FFMBlur was written and
    /// committed both of them; every field added since is a type-and-it-is-yours
    /// box, and discarding on blur meant a click anywhere outside - including on
    /// the next text box - silently reverted what had just been typed. Escape is
    /// the way to abandon an edit, and it still calls End(false).
    /// </summary>
    public static void Blur() { if (Edit != "") End(true); }

    // ---- selection ----
    public static string SelText() => (Sel < 0 || Sel == Car) ? "" : Buf.Substring(Math.Min(Sel, Car), Math.Abs(Car - Sel));
    public static bool DelSel()
    {
        if (Sel < 0 || Sel == Car) return false;
        int a = Math.Min(Sel, Car), b2 = Math.Max(Sel, Car);
        Buf = Buf[..a] + Buf[b2..];
        Car = a; Sel = -1;
        return true;
    }
    static void SyncBuf()
    {
        Sel = -1;
        Car = Math.Max(0, Math.Min(Car, Buf.Length));
        if (Edit == "db") Ffm.Q = Buf;
        else if (Edit is "sa" or "sl" or "rn") Special.SpfSaved.Sync(Edit, Buf);
        else if (Edit == "at") Special.SpfAcct.SyncBuf(Buf);
        CaretAt = Clock.Tick;
        OnSync?.Invoke(Edit);
        Poke();
    }

    /// <summary>FFMChar(ch): typed text into the buffer, replacing a selection.</summary>
    public static void Char(string text)
    {
        if (Edit == "" || string.IsNullOrEmpty(text)) return;
        foreach (char ch in text)
        {
            if (ch < 32) continue;
            if (DelSel()) SyncBuf();
            if (Buf.Length >= (Edit == "pn" ? 16 : Edit == "pb" ? 64 : 96)) { Sel = -1; return; }
            Buf = Buf[..Car] + ch + Buf[Car..];
            Car += 1;
            Sel = -1;                                             // one event may carry several characters
        }
        SyncBuf();
    }

    /// <summary>FFMKey: navigation, editing and clipboard keys. Returns true when the key was the field's.</summary>
    public static bool HandleKey(KeyEventArgs e, Avalonia.Controls.TopLevel? top)
    {
        if (Edit == "") return false;
        CaretAt = Clock.Tick;
        bool ctl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        bool shf = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (ctl)
        {
            if (e.Key == Key.A) { Sel = 0; Car = Buf.Length; }
            else if (e.Key == Key.C) { var t = SelText(); if (t != "") _ = top?.Clipboard?.SetTextAsync(t); }
            else if (e.Key == Key.X) { var t = SelText(); if (t != "") { _ = top?.Clipboard?.SetTextAsync(t); DelSel(); SyncBuf(); } }
            else if (e.Key == Key.V) { _ = PasteAsync(top); }
            else if (e.Key == Key.Left || e.Key == Key.Right)
            {
                if (shf && Sel < 0) Sel = Car;
                if (!shf) Sel = -1;
                Car = e.Key == Key.Left ? WordLeft(Car) : WordRight(Car);
            }
            else return false;
            Poke();
            return true;
        }
        switch (e.Key)
        {
            case Key.Back:
                if (!DelSel() && Car > 0) { Buf = Buf[..(Car - 1)] + Buf[Car..]; Car -= 1; }
                SyncBuf(); return true;
            case Key.Delete:
                if (!DelSel() && Car < Buf.Length) Buf = Buf[..Car] + Buf[(Car + 1)..];
                SyncBuf(); return true;
            case Key.Left: case Key.Right: case Key.Home: case Key.End:
                if (shf && Sel < 0) Sel = Car;
                if (!shf) Sel = -1;
                Car = e.Key == Key.Left ? Math.Max(0, Car - 1) : e.Key == Key.Right ? Math.Min(Buf.Length, Car + 1) : e.Key == Key.Home ? 0 : Buf.Length;
                Poke(); return true;
            case Key.Escape:
                if (Edit == "db") FfmViews.CloseView();
                else if (Edit is "sa" or "sl" or "rn") { string m0 = Edit; End(false); Special.SpfSaved.Escape(m0); }
                else End(false);
                return true;
            case Key.Return:
                if (Edit == "q")
                {
                    string nm = Buf.Trim();
                    if (nm != "")
                    {
                        int r = Ffm.Add(nm, "");
                        Buf = ""; Ffm.Q2 = ""; Car = 0; Sel = -1;
                        OnSync?.Invoke("q");
                        if (r != 0) Ffm.Say(Ffm.StagedMsg(r, nm), 0xFF34D399);
                    }
                }
                else if (Edit == "db")
                {
                    var lst = FfmViews.DbList();
                    if (lst.Count > 0)
                    {
                        string nm = FfmViews.Db[lst[0]];
                        int r = Ffm.Add(nm, "");
                        FfmViews.DbIns[nm] = Clock.Tick;
                        if (r != 0) Ffm.Say(Ffm.StagedMsg(r, nm), 0xFF34D399);
                    }
                }
                else if (Edit == "v") End(true);
                else OnEnter?.Invoke(Edit);
                Poke(); return true;
            case Key.Tab:
                if (Edit == "rfn") { End(true); Begin("rfv", 0); }
                else if (Edit == "rfv") { End(true); Begin("rfn", 0); }
                else if (Edit == "sn") { End(true); Begin("sd", 0); }
                else if (Edit == "pn") { End(true); Begin("pb", 0); }
                else End(true);
                return true;
        }
        return false;
    }

    static async Task PasteAsync(Avalonia.Controls.TopLevel? top)
    {
        string p2 = "";
        try { p2 = await (top?.Clipboard?.GetTextAsync() ?? Task.FromResult<string?>("")) ?? ""; } catch { }
        if (Edit == "") return;
        p2 = p2.Replace("\r", "").Replace('\n', ' ').Replace('\t', ' ').Trim();
        DelSel();
        int room = Math.Max(0, 96 - Buf.Length);
        if (p2.Length > room) p2 = p2[..room];
        if (p2 != "") { Buf = Buf[..Car] + p2 + Buf[Car..]; Car += p2.Length; }
        SyncBuf();
    }

    static bool IsWordCh(char c) => char.IsLetterOrDigit(c) || c == '_';
    static int WordLeft(int pos)
    {
        int i = Math.Min(pos, Buf.Length);
        while (i > 0 && !IsWordCh(Buf[i - 1])) i--;
        while (i > 0 && IsWordCh(Buf[i - 1])) i--;
        return i;
    }
    static int WordRight(int pos)
    {
        int i = Math.Max(0, pos), n = Buf.Length;
        while (i < n && !IsWordCh(Buf[i])) i++;
        while (i < n && IsWordCh(Buf[i])) i++;
        return i;
    }

    // ---- the mouse ----
    /// <summary>FFMCaretX(v, relx): the caret index nearest a point inside the text.</summary>
    public static int CaretX(string v, double relx)
    {
        var fnt = Face;
        relx -= fnt.Pad;                                              // the glyphs start one pad in from the field's origin
        if (relx <= 0) return 0;
        var xs = Fonts.Run(v, fnt);                                   // the same table the caret is drawn from, so a click lands where it looks
        for (int i = 1; i <= v.Length; i++)
            if (xs[i] >= relx) return relx - xs[i - 1] < xs[i] - relx ? i - 1 : i;
        return v.Length;
    }
    static int LastOff;                                               // the scroll the field was last painted with, so a click lands on the character under it
    /// <summary>FFMFldMouse(ux): place the caret; a quick second click selects everything.</summary>
    public static void Mouse(double ux)
    {
        if (Edit == "") return;
        long now = Clock.Tick;
        if (DblAt != 0 && now - DblAt < 420)
        {
            Sel = 0; Car = Buf.Length; DblAt = 0; MSel = false; CaretAt = now;
            Poke();
            return;
        }
        DblAt = now;
        int off0 = Math.Min(LastOff, Buf.Length);
        Car = off0 + CaretX(Buf[off0..], ux - TextX(Edit));
        Sel = Car; MSel = true; CaretAt = now;
        Poke();
    }
    /// <summary>FFMFldDrag(ux): extend the mouse selection.</summary>
    public static void Drag(double ux)
    {
        if (Edit == "" || !MSel) return;
        int off0 = Math.Min(LastOff, Buf.Length);
        Car = off0 + CaretX(Buf[off0..], ux - TextX(Edit));
        CaretAt = Clock.Tick;
        Poke();
    }
    public static void MouseUp() => MSel = false;

    // ---- painting ----
    /// <summary>FFMFldPaint(tx, ty, th, vis, off, acc, ff, now): the selection and the caret over the visible slice.</summary>
    public static void Paint(double tx, double ty, double th, string vis, int off, uint acc, double ff, long now, Font? face = null)
    {
        var fnt = face ?? Font;
        LastFont = fnt; LastOff = off;
        double gx = tx + fnt.Pad;                                     // where the first glyph is drawn - Txt draws at x + Pad
        if (Sel >= 0 && Sel != Car)
        {
            int a = Math.Min(Sel, Car), b2 = Math.Max(Sel, Car);
            a = Math.Max(a - off, 0); b2 = Math.Max(b2 - off, 0);
            a = Math.Min(a, vis.Length); b2 = Math.Min(b2, vis.Length);
            if (b2 > a)
            {
                double x1 = Fonts.AdvAt(vis, fnt, a), x2 = Fonts.AdvAt(vis, fnt, b2);
                FillRR(gx + x1 - 1, ty + 2, Math.Max(x2 - x1 + 2, 3), th - 4, 3, SBrush(FA(Alpha(acc, 70), ff)));
            }
        }
        int cIdx = Math.Min(Math.Max(Car - off, 0), vis.Length);
        double cw2 = Fonts.AdvAt(vis, fnt, cIdx);
        Caret(gx + cw2, ty + 3, th - 6, acc, ff, now);
    }
    public static void Caret(double cx, double cy, double h, uint acc, double ff, long now)
    {
        if ((now - CaretAt) % 1060 >= 560) return;
        FillRect(cx, cy, 1.6, h, SBrush(FA(Alpha(AccHi(acc, 0.5), 235), ff)));
    }

    static void Poke() => Yuri.Shell.Hub.HubSurface.Live?.Tim(Pace.TICK_A);
}
