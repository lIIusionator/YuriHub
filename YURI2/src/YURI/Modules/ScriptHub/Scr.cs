using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Input;
using Yuri.Core;
using Yuri.Modules.FastFlags;
using Yuri.Platform;
using Yuri.Shell.Hub;

namespace Yuri.Modules.ScriptHub;

public sealed class ScrTabState { public List<string> Lines = new() { "" }; public int Cl = 1, Cc, Top = 1, EditIdx; public string Name = "", Desc = "", File = ""; public long Born; }
public sealed class ScrItem { public string Name = "", Desc = "", File = "", Ver = "2"; public int Pid, Av; }
public sealed class ScrLib { public string Name = "", Path = "", Tms = "", Disp = ""; public DateTime Tm; }
public sealed class ScrTok { public int T, C; public string S = ""; }

/// <summary>
/// SCRIPT HUB (module 5): a small AutoHotkey editor with placed cards. The
/// editor is a line buffer with a caret, an anchor and a selection; the
/// tokenizer colours comments, strings, numbers, keywords and calls per
/// line. PLACE writes the script into YURI\scripts as zs_&lt;tick&gt;.ahk and
/// lists it in zeal.ini; ENABLE runs it under the AutoHotkey it needs and
/// STOP closes that process; CHECK validates it with `/validate`. Up to
/// four tabs; SAVE keeps a copy under YURI\scripts\saved for the library.
/// </summary>
public static class Scr
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399;
    public static List<string> Lines = new() { "" };
    public static int Cl = 1, Cc, Top = 1, Sl = 1, Sc; public static bool SelOn, MSel;
    public static List<List<ScrTok>> Toks = new(); public static bool Dirty = true;
    public static int Focus;                                            // 1 the editor
    public static string Name = "", Desc = "", File = "";
    public static int EditIdx, CeIdx, Cur = 1;
    public static long CaretAt, FlashAt, PlaceAt, WarnAt, RunAt, EdAt, BackAt, CeAt, CeBtnAt, DelAt, LibDelAt, DblAt; public static int CeBtnZ;
    public static string WarnMsg = "", OutMsg = "", OutVer = "", ChkVer = ""; public static int OutOk = -1; public static long OutAt;
    public static readonly List<ScrTabState> Tabs = new();
    public static bool ExpOpen, LibOpen; public static readonly List<ScrLib> LibList = new();
    public static double LsSc, LsScT, LsMax, Hsc, HscT, HscMax;
    public static readonly Dictionary<int, long> CardAt = new(), TogAt = new();
    public static double DelY, LibDelY; public static string DelName = "", LibDelName = "";
    public static int LastCl, LastCc = -1; public static bool Follow = true;
    public static readonly List<ScrItem> List = new();
    public static string Dir => Path.Combine(Paths.Root, "scripts");
    public static string SavedDir => Path.Combine(Dir, "saved");
    public static Func<string, string>? AhkExeOverride;                  // tests: major -> exe path
    static string? _v1, _v2;

    /// <summary>
    /// The dashboard reads the placed and running counts through these. Both
    /// were left at their `() => 0` defaults, so the SCRIPTS tile reported an
    /// empty hub however many scripts were placed or running. Separate from
    /// Register, which resets the editor's tabs and is not safe to run twice -
    /// this is, and boot calls it before the script tab has ever been opened.
    /// </summary>
    public static void Hooks()
    {
        Yuri.Shell.Hub.Tabs.Dashboard.ScriptsPlaced = () => List.Count;
        Yuri.Shell.Hub.Tabs.Dashboard.ScriptsRunning = () => { int n = 0; for (int i = 1; i <= List.Count; i++) if (Running(i)) n++; return n; };
    }
    /// <summary>The placed-script list without touching the editor - the dashboard needs the count at boot, not a live editor.</summary>
    public static void LoadListOnly() { if (List.Count == 0) LoadList(); }
    public static void Register()
    {
        Hooks();
        Tabs.Clear(); Tabs.Add(new ScrTabState { Lines = Lines });
        LoadList();
    }
    static void Poke() => HubSurface.Live?.Tim(Pace.TICK_A);

    // ---- tabs ----
    public static void TabSync() { var t = Tabs[Cur - 1]; t.Lines = Lines; t.Cl = Cl; t.Cc = Cc; t.Top = Top; t.Name = Name; t.Desc = Desc; t.EditIdx = EditIdx; t.File = File; }
    public static void TabLoad(int i)
    {
        Cur = i; var t = Tabs[i - 1];
        Lines = t.Lines; Cl = t.Cl; Cc = t.Cc; Top = t.Top; Name = t.Name; Desc = t.Desc; EditIdx = t.EditIdx; File = t.File;
        Sl = Cl; Sc = Cc; SelOn = false; MSel = false; Dirty = true; FlashAt = Clock.Tick;
        Poke();
    }
    public static void TabSwitch(int i) { if (i == Cur || i < 1 || i > Tabs.Count) return; TabSync(); TabLoad(i); }
    public static bool TabAdd()
    {
        if (Tabs.Count >= 4) { Warn("tab limit reached - close a tab first"); return false; }
        TabSync();
        Tabs.Add(new ScrTabState { Born = Clock.Tick });
        TabLoad(Tabs.Count);
        return true;
    }
    public static void TabClose(int i)
    {
        if (i < 1 || i > Tabs.Count) return;
        if (Tabs.Count == 1) { Clear(); Name = ""; Desc = ""; EditIdx = 0; File = ""; return; }
        if (i == Cur) { Tabs.RemoveAt(i - 1); TabLoad(Math.Min(i, Tabs.Count)); }
        else { int cur = Cur - (i < Cur ? 1 : 0); Tabs.RemoveAt(i - 1); Cur = cur; }
        Poke();
    }
    public static void InMain()
    {
        if (Cur == 1) return;
        TabSync();
        var src = Tabs[Cur - 1]; var t1 = Tabs[0];
        t1.Lines = new List<string>(src.Lines); t1.Cl = 1; t1.Cc = 0; t1.Top = 1; t1.Name = src.Name; t1.Desc = src.Desc; t1.EditIdx = 0; t1.File = "";
        TabLoad(1);
    }
    static bool Empty => Lines.Count == 1 && Lines[0] == "";

    // ---- the text ----
    public static string Text() => string.Join("\n", Lines);
    public static void SetText(string txt)
    {
        txt = txt.Replace("\r\n", "\n").Replace("\r", "\n");
        Lines = txt.Split('\n').ToList();
        if (Lines.Count == 0) Lines.Add("");
        Cl = Lines.Count; Cc = Lines[Cl - 1].Length; Top = 1;
        Sl = Cl; Sc = Cc; SelOn = false; MSel = false; Dirty = true;
    }
    public static void FocusSet(int field)
    {
        if (field != 0 && FfmField.Edit != "") FfmField.Blur();
        if (Focus == field) return;
        Focus = field; CaretAt = Clock.Tick; DblAt = 0;
        Poke();
    }
    /// <summary>
    /// Blur: the selection goes with the focus. Leaving SelOn set drew the
    /// editor's highlight over text nobody was editing any more - a click into a
    /// field left the previous box looking live.
    /// </summary>
    public static void Blur() { Focus = 0; MSel = false; SelOn = false; Sl = Cl; Sc = Cc; DblAt = 0; Poke(); }
    /// <summary>Keep the caret and the anchor inside the buffer - every read below indexes Lines with them.</summary>
    public static void ClampCaret()
    {
        if (Lines.Count == 0) Lines.Add("");
        Cl = Math.Clamp(Cl, 1, Lines.Count); Cc = Math.Clamp(Cc, 0, Lines[Cl - 1].Length);
        Sl = Math.Clamp(Sl, 1, Lines.Count); Sc = Math.Clamp(Sc, 0, Lines[Sl - 1].Length);
    }
    public static bool SelGet(out int l1, out int c1, out int l2, out int c2)
    {
        l1 = c1 = l2 = c2 = 0;
        ClampCaret();
        if (!SelOn || (Sl == Cl && Sc == Cc)) return false;
        if (Sl < Cl || (Sl == Cl && Sc < Cc)) { l1 = Sl; c1 = Sc; l2 = Cl; c2 = Cc; } else { l1 = Cl; c1 = Cc; l2 = Sl; c2 = Sc; }
        return true;
    }
    public static int SelLen()
    {
        if (!SelGet(out int l1, out int c1, out int l2, out int c2)) return 0;
        if (l1 == l2) return c2 - c1;
        int n = Lines[l1 - 1].Length - c1 + c2 + (l2 - l1);
        for (int i = l1 + 1; i < l2; i++) n += Lines[i - 1].Length;
        return n;
    }
    public static string SelText()
    {
        if (!SelGet(out int l1, out int c1, out int l2, out int c2)) return "";
        if (l1 == l2) return Lines[l1 - 1].Substring(c1, c2 - c1);
        var sb = new StringBuilder(Lines[l1 - 1][c1..]);
        for (int i = l1 + 1; i < l2; i++) sb.Append('\n').Append(Lines[i - 1]);
        return sb.Append('\n').Append(Lines[l2 - 1][..c2]).ToString();
    }
    public static bool DelSel()
    {
        if (!SelGet(out int l1, out int c1, out int l2, out int c2)) return false;
        Lines[l1 - 1] = Lines[l1 - 1][..c1] + Lines[l2 - 1][c2..];
        for (int i = 0; i < l2 - l1; i++) Lines.RemoveAt(l1);
        Cl = l1; Cc = c1; SelOn = false; Sl = l1; Sc = c1; Dirty = true;
        return true;
    }
    public static void Char(string ch)
    {
        if (Focus != 1 || ch == "" || ch[0] < 32) return;
        DelSel();
        string ln = Lines[Cl - 1];
        Lines[Cl - 1] = ln[..Cc] + ch + ln[Cc..];
        Cc += ch.Length; Dirty = true; CaretAt = Clock.Tick;
        Poke();
    }
    /// <summary>ScrKey: the editor's keys. Returns true when the key was the editor's.</summary>
    public static bool HandleKey(KeyEventArgs e, Func<Task<string?>> clipGet, Action<string> clipSet)
    {
        if (Focus != 1) return false;
        CaretAt = Clock.Tick;
        bool ctl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta), shf = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (e.Key == Key.Escape) { Blur(); return true; }
        if (ctl)
        {
            switch (e.Key)
            {
                case Key.A: Sl = 1; Sc = 0; Cl = Lines.Count; Cc = Lines[Cl - 1].Length; SelOn = true; break;
                case Key.C: { string t = SelText(); if (t != "") clipSet(t); break; }
                case Key.X: { string t = SelText(); if (t != "") { clipSet(t); DelSel(); } break; }
                case Key.V: _ = PasteAsync(clipGet); break;
                default: return false;
            }
            Poke(); return true;
        }
        bool mv = e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown;
        if (mv && shf && !SelOn) { Sl = Cl; Sc = Cc; SelOn = true; }
        string ln = Lines[Cl - 1];
        switch (e.Key)
        {
            case Key.Back:
                if (!DelSel())
                {
                    if (Cc > 0) { Lines[Cl - 1] = ln[..(Cc - 1)] + ln[Cc..]; Cc--; }
                    else if (Cl > 1) { string prev = Lines[Cl - 2]; Cc = prev.Length; Lines[Cl - 2] = prev + ln; Lines.RemoveAt(Cl - 1); Cl--; }
                }
                Dirty = true; break;
            case Key.Delete:
                if (!DelSel())
                {
                    if (Cc < ln.Length) Lines[Cl - 1] = ln[..Cc] + ln[(Cc + 1)..];
                    else if (Cl < Lines.Count) { Lines[Cl - 1] = ln + Lines[Cl]; Lines.RemoveAt(Cl); }
                }
                Dirty = true; break;
            case Key.Enter:
            {
                DelSel(); ln = Lines[Cl - 1];
                string rest = ln[Cc..]; Lines[Cl - 1] = ln[..Cc];
                string ind = Regex.Match(Lines[Cl - 1], "^(\\s*)").Groups[1].Value;
                Lines.Insert(Cl, ind + rest);
                Cl++; Cc = ind.Length; Dirty = true; break;
            }
            case Key.Tab: DelSel(); ln = Lines[Cl - 1]; Lines[Cl - 1] = ln[..Cc] + "    " + ln[Cc..]; Cc += 4; Dirty = true; break;
            case Key.Left: if (Cc > 0) Cc--; else if (Cl > 1) { Cl--; Cc = Lines[Cl - 1].Length; } break;
            case Key.Right: if (Cc < ln.Length) Cc++; else if (Cl < Lines.Count) { Cl++; Cc = 0; } break;
            case Key.Up: if (Cl > 1) { Cl--; Cc = Math.Min(Cc, Lines[Cl - 1].Length); } break;
            case Key.Down: if (Cl < Lines.Count) { Cl++; Cc = Math.Min(Cc, Lines[Cl - 1].Length); } break;
            case Key.Home: Cc = 0; break;
            case Key.End: Cc = ln.Length; break;
            case Key.PageUp: Cl = Math.Max(1, Cl - 8); Cc = Math.Min(Cc, Lines[Cl - 1].Length); break;
            case Key.PageDown: Cl = Math.Min(Lines.Count, Cl + 8); Cc = Math.Min(Cc, Lines[Cl - 1].Length); break;
            default: return false;
        }
        if (mv && !shf) { SelOn = false; Sl = Cl; Sc = Cc; }
        Poke();
        return true;
    }
    static async Task PasteAsync(Func<Task<string?>> clipGet) { string? txt; try { txt = await clipGet(); } catch { return; } Paste(txt ?? ""); }
    public static void Paste(string txt)
    {
        if (txt == "") return;
        txt = txt.Replace("\r\n", "\n").Replace("\r", "\n");
        var parts = txt.Split('\n');
        DelSel();
        string ln = Lines[Cl - 1], head = ln[..Cc], tail = ln[Cc..];
        if (parts.Length == 1) { Lines[Cl - 1] = head + parts[0] + tail; Cc += parts[0].Length; }
        else
        {
            Lines[Cl - 1] = head + parts[0];
            for (int i = 1; i < parts.Length; i++) Lines.Insert(Cl + i - 1, i == parts.Length - 1 ? parts[i] + tail : parts[i]);
            Cl += parts.Length - 1; Cc = parts[^1].Length;
        }
        Dirty = true; FlashAt = Clock.Tick;
        Poke();
    }
    public static void CaretFromMouse(int li, int co)
    {
        li = Math.Clamp(li, 1, Lines.Count);
        Cl = li; Cc = Math.Clamp(co, 0, Lines[li - 1].Length); CaretAt = Clock.Tick;
    }
    public static void WordSelect()
    {
        string ln2 = Lines[Cl - 1];
        if (ln2 == "") return;
        int a = Cc, b = Cc;
        while (a > 0 && IsWordCh(ln2[a - 1])) a--;
        while (b < ln2.Length && IsWordCh(ln2[b])) b++;
        Sl = Cl; Sc = a; Cc = b; SelOn = a != b; CaretAt = Clock.Tick;
        Poke();
    }
    static bool IsWordCh(char c) => char.IsLetterOrDigit(c) || c == '_';

    // ---- the tokenizer ----
    static readonly HashSet<string> Kw = new(StringComparer.OrdinalIgnoreCase) { "if", "else", "loop", "while", "for", "return", "break", "continue", "global", "local", "static", "try", "catch", "finally", "throw", "class", "until", "switch", "case", "default", "goto", "and", "or", "not", "in", "is", "true", "false", "unset", "super", "this", "new", "extends" };
    /// <summary>True when the cached tokens no longer describe the lines - a line count that has changed under them renders text that is not there any more.</summary>
    public static bool TokStale => Dirty || Toks.Count != Lines.Count;
    public static void Retok()
    {
        Toks = new List<List<ScrTok>>(Lines.Count);
        const string hx = "0123456789ABCDEFabcdefxX.";
        foreach (var line in Lines)
        {
            var toks = new List<ScrTok>(); int n = line.Length, i = 0;
            while (i < n)
            {
                char c = line[i];
                if (c == ';' && (i == 0 || line[i - 1] == ' ' || line[i - 1] == '\t')) { toks.Add(new ScrTok { T = 4, S = line[i..], C = i }); break; }
                if (c == '"' || c == '\'')
                {
                    int j = i + 1; while (j < n && line[j] != c) j++;
                    toks.Add(new ScrTok { T = 3, S = line.Substring(i, Math.Min(j, n - 1) - i + 1), C = i }); i = j + 1; continue;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    int j = i; while (j < n && (char.IsLetterOrDigit(line[j]) || line[j] == '_')) j++;
                    string w = line[i..j];
                    toks.Add(new ScrTok { T = Kw.Contains(w) ? 1 : j < n && line[j] == '(' ? 5 : 0, S = w, C = i }); i = j; continue;
                }
                if (char.IsDigit(c))
                {
                    int j = i; while (j < n && hx.Contains(line[j])) j++;
                    toks.Add(new ScrTok { T = 2, S = line[i..j], C = i }); i = j; continue;
                }
                int k = i;
                while (k < n) { char cc = line[k]; if (cc == ';' || cc == '"' || cc == '\'' || char.IsLetter(cc) || cc == '_' || char.IsDigit(cc)) break; k++; }
                if (k == i) k = i + 1;
                toks.Add(new ScrTok { T = 0, S = line[i..k], C = i }); i = k;
            }
            Toks.Add(toks);
        }
        Dirty = false;
    }

    // ---- the placed list ----
    public static void LoadList()
    {
        List.Clear();
        int n = (int)Ini.ReadInt(Paths.IniFile, "scripts", "count", 0);
        for (int i = 1; i <= n; i++)
        {
            string nm = Ini.Read(Paths.IniFile, "script" + i, "name", ""), ds = Ini.Read(Paths.IniFile, "script" + i, "desc", ""), fl = Ini.Read(Paths.IniFile, "script" + i, "file", "");
            int avi = (int)Ini.ReadInt(Paths.IniFile, "script" + i, "av", 0); if (avi == 1) avi = 0;
            string vr = Ini.Read(Paths.IniFile, "script" + i, "ver", "2");
            if (nm == "" || fl == "" || !System.IO.File.Exists(fl)) continue;
            List.Add(new ScrItem { Name = nm, Desc = ds, File = fl, Av = avi, Ver = vr == "1" ? "1" : "2" });
            MarkPlaced(List.Count);
        }
    }
    public static void SaveList()
    {
        Ini.Write(Paths.IniFile, "scripts", "count", (long)List.Count);
        for (int i = 1; i <= List.Count; i++)
        {
            var it = List[i - 1];
            Ini.Write(Paths.IniFile, "script" + i, "name", it.Name); Ini.Write(Paths.IniFile, "script" + i, "desc", it.Desc); Ini.Write(Paths.IniFile, "script" + i, "file", it.File);
            Ini.Write(Paths.IniFile, "script" + i, "av", (long)it.Av); Ini.Write(Paths.IniFile, "script" + i, "ver", it.Ver);
        }
        Ini.Write(Paths.IniFile, "script" + (List.Count + 1), "name", "");
    }
    public static void MarkPlaced(int i) { CardAt[i] = Clock.Tick; Poke(); }
    public static void New()
    {
        Lines = new List<string> { "" }; Cl = 1; Cc = 0; Top = 1; Sl = 1; Sc = 0; SelOn = false; MSel = false;
        Name = ""; Desc = ""; EditIdx = 0; Dirty = true; FlashAt = Clock.Tick;
        FocusSet(1);
    }
    public static void Clear()
    {
        Lines = new List<string> { "" }; Cl = 1; Cc = 0; Top = 1; Dirty = true; Sl = 1; Sc = 0; SelOn = false; MSel = false;
        Name = ""; Desc = ""; OutOk = -1; OutMsg = ""; OutVer = ""; ChkVer = ""; EditIdx = 0; FlashAt = Clock.Tick;
        Poke();
    }
    public static void Warn(string msg)
    {
        ExpOpen = false;
        WarnAt = WarnAt != 0 && Clock.Tick - WarnAt < 2340 ? Clock.Tick - 220 : Clock.Tick;
        WarnMsg = msg;
        Poke();
    }
    public static void Place()
    {
        string body = Text();
        if (body.Trim() == "") { Warn("nothing to place - write or paste a script first"); return; }
        if ((AhkExe("2") != "" || AhkExe("1") != "") && !Check(true)) { Warn("script has errors - see the output line below"); return; }
        string ver = ChkVer != "" ? ChkVer : "2";
        try { Directory.CreateDirectory(Dir); } catch { }
        string nm = Name.Trim() == "" ? "SCRIPT " + (List.Count + 1) : Name.Trim();
        string ds = Desc.Trim() == "" ? "placed from the script hub" : Desc.Trim();
        if (EditIdx >= 1 && EditIdx <= List.Count)
        {
            var it = List[EditIdx - 1];
            Stop(EditIdx);
            try { System.IO.File.WriteAllText(it.File, body, new UTF8Encoding(false)); } catch { }
            it.Name = nm; it.Desc = ds; it.Ver = ver;
        }
        else
        {
            string fl = Path.Combine(Dir, "zs_" + Clock.Tick + ".ahk");
            try { System.IO.File.WriteAllText(fl, body, new UTF8Encoding(false)); } catch { }
            if (!System.IO.File.Exists(fl)) { Warn("could not write the script file - check folder permissions"); return; }
            List.Add(new ScrItem { Name = nm, Desc = ds, File = fl, Ver = ver });
            MarkPlaced(List.Count);
        }
        SaveList();
        EditIdx = 0; Dirty = false; PlaceAt = Clock.Tick;
        Blur();
    }
    public static void Edit(int i)
    {
        if (i < 1 || i > List.Count) return;
        for (int j = 1; j <= Tabs.Count; j++) { int ei = j == Cur ? EditIdx : Tabs[j - 1].EditIdx; if (ei == i) { TabSwitch(j); return; } }
        if (!((Empty && Name == "") || TabAdd())) return;
        var it = List[i - 1];
        string body = ""; try { body = System.IO.File.ReadAllText(it.File); } catch { }
        SetText(body);
        Name = it.Name; Desc = it.Desc; EditIdx = i; File = ""; Dirty = true; FlashAt = Clock.Tick;
        Poke();
    }
    public static void Delete(int i, double lsy, double pitch, int visCards)
    {
        if (i < 1 || i > List.Count) return;
        int vis = i - (int)Math.Floor(LsSc / pitch);
        DelY = lsy + (i - 1) * pitch - LsSc; DelName = List[i - 1].Name;
        DelAt = vis >= 1 && vis <= visCards ? Clock.Tick : 0;
        CardAt.Clear(); TogAt.Clear();
        Stop(i);
        try { System.IO.File.Delete(List[i - 1].File); } catch { }
        List.RemoveAt(i - 1);
        if (EditIdx == i) EditIdx = 0; else if (EditIdx > i) EditIdx--;
        SaveList();
        Poke();
    }
    public static bool Running(int i)
    {
        if (i < 1 || i > List.Count) return false;
        int p = List[i - 1].Pid;
        if (p != 0 && Exists(p)) return true;
        List[i - 1].Pid = 0;
        return false;
    }
    static bool Exists(int pid) { try { return !Process.GetProcessById(pid).HasExited; } catch { return false; } }
    public static void Toggle(int i) { if (Running(i)) Stop(i); else Start(i); }
    public static void Start(int i)
    {
        if (i < 1 || i > List.Count) return;
        string vr = List[i - 1].Ver, exe = AhkExe(vr);
        if (exe == "") { Warn(Os.IsWin ? "AutoHotkey v" + vr + " was not found - this script cannot run" : "AutoHotkey is a Windows program - scripts cannot run here"); return; }
        try { var p = Process.Start(new ProcessStartInfo(exe, "\"" + List[i - 1].File + "\"") { UseShellExecute = false }); List[i - 1].Pid = p?.Id ?? 0; } catch { List[i - 1].Pid = 0; }
        RunAt = Clock.Tick;
        Poke();
    }
    public static void Stop(int i)
    {
        if (i < 1 || i > List.Count) return;
        int p = List[i - 1].Pid;
        if (p != 0 && Exists(p)) { try { Process.GetProcessById(p).Kill(); } catch { } }
        List[i - 1].Pid = 0;
        Poke();
    }
    public static void StopAll() { foreach (var it in List) if (it.Pid != 0 && Exists(it.Pid)) { try { Process.GetProcessById(it.Pid).Kill(); } catch { } } }

    // ---- AutoHotkey ----
    static string ExeMajor(string p) { try { var v = FileVersionInfo.GetVersionInfo(p); return v.FileMajorPart.ToString(); } catch { return ""; } }
    static IEnumerable<string> AhkRoots()
    {
        var res = new List<string>();
        foreach (var r in new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AutoHotkey"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "AutoHotkey"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "AutoHotkey"), Path.Combine(AppContext.BaseDirectory, "AutoHotkey") })
            if (r != "" && Directory.Exists(r)) res.Add(r);
        return res;
    }
    static string FindExe(string major)
    {
        if (!Os.IsWin) return "";
        string[] names = { "AutoHotkey64.exe", "AutoHotkey32.exe", "AutoHotkeyU64.exe", "AutoHotkeyU32.exe", "AutoHotkeyA32.exe", "AutoHotkey.exe" };
        var roots = AhkRoots().ToList();
        foreach (var r in roots) foreach (var n in names) { string c = Path.Combine(r, n); if (System.IO.File.Exists(c) && ExeMajor(c) == major) return c; }
        foreach (var r in roots) { try { foreach (var d in Directory.GetDirectories(r)) foreach (var n in names) { string c = Path.Combine(d, n); if (System.IO.File.Exists(c) && ExeMajor(c) == major) return c; } } catch { } }
        return "";
    }
    public static string AhkExe(string major)
    {
        if (AhkExeOverride is not null) return AhkExeOverride(major);
        if (major == "1") return _v1 ??= FindExe("1");
        return _v2 ??= FindExe("2");
    }
    public static string ReqVer(string body) { var m = Regex.Match(body, "^\\s*#Requires\\s+AutoHotkey\\s+v?([12])", RegexOptions.IgnoreCase | RegexOptions.Multiline); return m.Success ? m.Groups[1].Value : ""; }
    static readonly string[] V1Pats = { "^\\s*#NoEnv\\b", "^\\s*SetBatchLines\\b", "^\\s*SetFormat\\b", "^\\s*AutoTrim\\b", "^\\s*StringCaseSense\\b", "^\\s*#MaxThreadsPerHotkey\\b", "^\\s*SetEnv\\b",
        "^\\s*(?:Sleep|Send|SendInput|MsgBox|Gui|Menu|StringReplace|StringSplit|StringTrimLeft|FileRead|FileAppend|FileDelete|WinActivate|WinWait|SetTimer|Run|RunWait|Random|SoundBeep|ControlSend|MouseMove|MouseClick|Process|Sort|Transform|EnvSet|EnvGet|SetTitleMatchMode|SetKeyDelay|SetMouseDelay|DetectHiddenWindows|CoordMode|Loop)\\s*,",
        "^\\s*If(?:WinActive|WinExist|Equal|NotEqual|InString|Greater|Less)\\b" };
    static readonly string[] V2Pats = { "^\\s*#Requires\\b", "=>", "\\bMap\\(", "\\bArray\\(", "\\bInputHook\\(", "\\bA_Args\\b", "\\bObjBindMethod\\(", ":=\\s*\\{", "\\.Push\\(", "\\bStrSplit\\(" };
    public static string GuessVer(string body)
    {
        string r = ReqVer(body);
        if (r != "") return r;
        int v1 = V1Pats.Count(p => Regex.IsMatch(body, p, RegexOptions.IgnoreCase | RegexOptions.Multiline)), v2 = V2Pats.Count(p => Regex.IsMatch(body, p, RegexOptions.IgnoreCase | RegexOptions.Multiline));
        if (v1 > 0 && v1 >= v2) return "1";
        if (v2 > 0 && v2 > v1) return "2";
        return "";
    }
    public static string ErrPretty(string raw, string v)
    {
        raw = raw.Trim('\r', '\n', ' ');
        string msg = "", lno = "";
        var m = Regex.Match(raw, "\\((\\d+)\\)\\s*:\\s*==>\\s*([^\\r\\n]+)");
        if (m.Success) { lno = m.Groups[1].Value; msg = m.Groups[2].Value.Trim(); }
        if (msg == "") foreach (var l in raw.Split('\n')) if (l.Trim() != "") { msg = l.Trim(); break; }
        var m2 = Regex.Match(raw, "Specifically:\\s*([^\\r\\n]+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        string spec = m2.Success ? m2.Groups[1].Value.Trim() : "";
        string res = "v" + v + (lno != "" ? " line " + lno : "") + " - " + (msg == "" ? "script failed to parse" : msg);
        if (spec != "" && res.Length + spec.Length < 100) res += "  [" + spec + "]";
        return res.Length > 108 ? res[..108] + "..." : res;
    }
    public static Func<string, string, (int rc, string output)>? ValidateOverride;                 // tests: (exe, file) -> result
    static (int rc, string output) Validate(string exe, string file)
    {
        if (ValidateOverride is not null) return ValidateOverride(exe, file);
        try
        {
            var psi = new ProcessStartInfo(exe, "/ErrorStdOut /validate \"" + file + "\"") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using var p = Process.Start(psi)!;
            string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit(15000);
            return (p.HasExited ? p.ExitCode : 1, o);
        }
        catch { return (1, ""); }
    }
    public static bool Check(bool silent = false)
    {
        string body = Text();
        if (body.Trim() == "")
        {
            OutOk = -1; OutVer = ""; ChkVer = ""; OutMsg = ""; OutAt = Clock.Tick;
            if (!silent) Warn("nothing to check - write or paste a script first");
            return false;
        }
        string e2 = AhkExe("2"), e1 = AhkExe("1");
        if (e2 == "" && e1 == "") { OutOk = 0; OutVer = ""; OutMsg = Os.IsWin ? "AutoHotkey not found - cannot validate" : "AutoHotkey is a Windows program - cannot validate here"; OutAt = Clock.Tick; Poke(); return false; }
        try { Directory.CreateDirectory(Dir); } catch { }
        string tmp = Path.Combine(Dir, "_check.ahk");
        try { System.IO.File.WriteAllText(tmp, body, new UTF8Encoding(false)); } catch { }
        string req = GuessVer(body);
        var order = req == "1" ? new[] { ("1", e1) } : req == "2" ? new[] { ("2", e2) } : new[] { ("2", e2), ("1", e1) };
        bool ok = false; string usedV = "", firstErr = "", firstV = "";
        foreach (var (v, exe) in order)
        {
            if (exe == "") continue;
            if (v == "1") { ok = true; usedV = "1"; OutMsg = firstErr != "" ? "v2 rejected it - will run under AutoHotkey v1 (not pre-validated)" : "detected as AutoHotkey v1 - will run under v1 (v1 has no validate switch)"; break; }
            var (rc, r) = Validate(exe, tmp);
            if (rc == 0) { ok = true; usedV = v; OutMsg = "no errors - parses clean as AutoHotkey v" + usedV; break; }
            if (firstErr == "") { firstErr = r; firstV = v; }
        }
        try { System.IO.File.Delete(tmp); } catch { }
        if (ok) { OutOk = 1; OutVer = usedV; ChkVer = usedV; }
        else { OutOk = 0; OutVer = firstV; ChkVer = ""; OutMsg = firstErr == "" ? "AutoHotkey v" + (req != "" ? req : "?") + " not found - install it to validate, the script itself was not checked" : ErrPretty(firstErr, firstV); }
        OutAt = Clock.Tick;
        if (!silent) Poke();
        return ok;
    }

    // ---- save, export, the library ----
    static string SafeName() => Name.Trim() == "" ? "script" : Regex.Replace(Name.Trim(), "[\\\\/:*?\"<>|]", "_");
    public static void Save()
    {
        string body = Text();
        if (body.Trim() == "") { Warn("nothing to save - write a script first"); return; }
        if (File != "")
        {
            try { System.IO.File.WriteAllText(File, body, new UTF8Encoding(false)); } catch { }
            OutOk = 1; OutMsg = "saved - " + File; OutAt = Clock.Tick; Poke(); return;
        }
        try { Directory.CreateDirectory(SavedDir); } catch { }
        string nm = SafeName(), fl = Path.Combine(SavedDir, nm + ".ahk"); int n = 2;
        while (System.IO.File.Exists(fl)) { fl = Path.Combine(SavedDir, nm + "_" + n + ".ahk"); n++; }
        try { System.IO.File.WriteAllText(fl, body, new UTF8Encoding(false)); } catch { }
        File = fl; OutOk = 1; OutMsg = "saved - " + fl; OutAt = Clock.Tick;
        Poke();
    }
    public static void ExportTo(string sel, string ext)
    {
        string body = Text();
        if (!Regex.IsMatch(sel, "\\." + ext + "$", RegexOptions.IgnoreCase)) sel += "." + ext;
        try { System.IO.File.WriteAllText(sel, body, new UTF8Encoding(false)); } catch { }
        OutOk = 1; OutMsg = "exported - " + sel; OutAt = Clock.Tick;
        Poke();
    }
    public static void LibRefresh()
    {
        LibList.Clear();
        if (!Directory.Exists(SavedDir)) return;
        try
        {
            foreach (var pat in new[] { "*.ahk", "*.txt" })
                foreach (var f in Directory.GetFiles(SavedDir, pat))
                {
                    if (LibList.Count >= 24) break;
                    var tm = System.IO.File.GetLastWriteTime(f);
                    LibList.Add(new ScrLib { Name = Path.GetFileName(f), Path = f, Tm = tm, Tms = tm.ToString("MMM d  HH:mm"), Disp = Path.GetFileNameWithoutExtension(f) });
                }
        }
        catch { }
    }
    public static void LibOpenAt(int i)
    {
        if (i < 1 || i > LibList.Count) return;
        string p = LibList[i - 1].Path, body;
        try { body = System.IO.File.ReadAllText(p); } catch { return; }
        if (!((Empty && Name == "") || TabAdd())) return;
        SetText(body);
        Name = Path.GetFileNameWithoutExtension(LibList[i - 1].Name).ToUpperInvariant();
        File = p; EditIdx = 0; LibOpen = false;
        Poke();
    }
    public static void LibDel(int i)
    {
        if (i < 1 || i > LibList.Count) return;
        try { System.IO.File.Delete(LibList[i - 1].Path); } catch { }
        LibRefresh();
        Poke();
    }
    public static void LoadFile(string path)
    {
        string txt; try { txt = System.IO.File.ReadAllText(path); } catch { return; }
        SetText(txt);
        if (Name == "") Name = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
        FlashAt = Clock.Tick;
        Poke();
    }

    // ---- the inline card edit ----
    public static void CardEdit(int i)
    {
        if (i < 1 || i > List.Count || CeIdx == i) return;
        if (CeIdx == 0) TabSync();
        var it = List[i - 1];
        string body = ""; try { body = System.IO.File.ReadAllText(it.File); } catch { }
        SetText(body);
        Name = it.Name; Desc = it.Desc; CeIdx = i; Dirty = true; LibOpen = false; ExpOpen = false; CeAt = Clock.Tick;
        FocusSet(1);
        Poke();
    }
    public static void CardCancel() { CeIdx = 0; Blur(); TabLoad(Cur); }
    public static void CardSave()
    {
        int i = CeIdx;
        if (i < 1 || i > List.Count) { CardCancel(); return; }
        string body = Text();
        if (body.Trim() == "") { Warn("nothing to save - the script is empty"); return; }
        if ((AhkExe("2") != "" || AhkExe("1") != "") && !Check(true)) { Warn("script has errors - fix them or CANCEL"); return; }
        string ver = ChkVer != "" ? ChkVer : "2";
        var it = List[i - 1];
        bool wasOn = it.Pid != 0 && Exists(it.Pid);
        Stop(i);
        try { System.IO.File.WriteAllText(it.File, body, new UTF8Encoding(false)); } catch { }
        it.Name = Name.Trim() == "" ? it.Name : Name.Trim(); it.Desc = Desc.Trim() == "" ? it.Desc : Desc.Trim(); it.Ver = ver;
        SaveList();
        if (wasOn) Start(i);
        PlaceAt = Clock.Tick;
        CardCancel();
    }
    public static void CardMain()
    {
        int i = CeIdx;
        if (i < 1 || i > List.Count) { CardCancel(); return; }
        string nm = Name, ds = Desc, body = Text();
        CeIdx = 0;
        var t = Tabs[Cur - 1];
        bool keep = !(t.Lines.Count == 1 && t.Lines[0] == "" && t.Name == "");
        if (keep)
        {
            TabLoad(Cur);
            if (!TabAdd())
            {
                CeIdx = i;
                string b2 = ""; try { b2 = System.IO.File.ReadAllText(List[i - 1].File); } catch { }
                SetText(body != "" ? body : b2); Name = nm; Desc = ds;
                Warn("tab limit reached - SAVE or CANCEL here, or close a tab first");
                Poke(); return;
            }
            SetText(body); Name = nm; Desc = ds;
        }
        EditIdx = i; Dirty = true; FlashAt = Clock.Tick;
        FocusSet(1);
        Poke();
    }
    public static double Shift()
    {
        if (WarnAt == 0) return 0.0;
        long t = Clock.Tick - WarnAt;
        if (t >= 2600) return 0.0;
        if (t < 220) return 34 * Gfx.Ease.Ease3(t / 220.0);
        if (t > 2340) return 34 * (1 - Gfx.Ease.Ease3((t - 2340) / 260.0));
        return 34.0;
    }
    public static void Exiting() { StopAll(); }
}
