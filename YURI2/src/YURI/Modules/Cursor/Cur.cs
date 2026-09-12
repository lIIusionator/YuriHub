using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Yuri.Core;
using Yuri.Platform;
using Yuri.Shell.Hub;

namespace Yuri.Modules.Cursor;

public sealed record CurSlotDef(string Key, string Label, string[] Names, string? Prev = null);
/// <summary>One cursor file the client reads: where it lives, which slot feeds it, whether a strap's Modifications folder is meant to hold it (mk).</summary>
public sealed record CurTarget(string Path, string Dir, string Name, int Slot, bool Mk);
public sealed class CurSlot
{
    public string Src = ""; public Bitmap? Bmp; public int Bw, Bh; public bool Has;
    public Bitmap? Dbmp; public int Dbw, Dbh; public string Dpath = "\f"; public long PickAt; public int Dsrc;   // dsrc: 0 live, 1 a strap's mod, 2 our backup
}

/// <summary>
/// CURSOR - module 3. Four slots — ARROW (with the far pointer), SHIFT LOCK,
/// CAMERA, TEXT — each a picture fitted into 64x64 and kept as a working
/// copy under YURI\cursor. APPLY copies the working copies over every
/// cursor file the installed clients (and the straps' Modifications
/// folders) read, backing each original up first under a key of its path;
/// RESTORE puts the originals back and clears the slots. AUTO RE-APPLY
/// watches for a client update every 15 s. CurScan / Targets / Apply /
/// Restore / SetImage / ClearSlot / SyncDefaults / AutoTick / Boot.
/// </summary>
public static class Cur
{
    public const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_OFF = 0xFFFB7185, C_ACC = 0xFFFB7185;
    public const string FAROPT = "ArrowFarCursor.png";
    public const int SIZE = 64, SYSN = 8, PV = 68, CH = 194;
    public static readonly CurSlotDef[] Slots =
    {
        new("arrow", "ARROW", new[] { "ArrowCursor.png", FAROPT }, FAROPT),
        new("lock", "SHIFT LOCK", new[] { "MouseLockedCursor.png" }),
        new("camera", "CAMERA", new[] { "CrossMouseIcon.png" }),
        new("text", "TEXT", new[] { "IBeamCursor.png" }),
    };
    public static readonly CurSlot[] Slot = { new(), new(), new(), new() };
    public static string Msg = "NO IMAGE"; public static uint MsgCol = 0xFFC7CBE0; public static long MsgAt;
    public static List<CurTarget> Targets = new(); public static long TgtAt, DefAt = -1;
    public static int BakN; public static long BakAt;
    public static int Applied; public static long ApplyAt;
    public static double Scr, ScrT, FarT, AutoT;
    public static bool Busy, Far = true, Auto;
    static DispatcherTimer? _autoTimer;
    public static string Dir => Paths.Cur;
    public static string BakDir => Path.Combine(Paths.Cur, "backup");
    public static string SlotImg(int si) => Path.Combine(Dir, Slots[si - 1].Key + ".png");
    public static string SlotSrc(int si) => Path.Combine(Dir, Slots[si - 1].Key + "_src.png");

    public static void Say(string msg, uint col = 0)
    {
        Msg = msg; MsgCol = col != 0 ? col : C_ON; MsgAt = Clock.Tick;
        HubSurface.Live?.Tim(Pace.TICK_A);
    }

    // ---- CurScan: every cursor file a client reads ----
    static void PushDirs(List<(string d, bool mk, int only)> dirs, string vdir)
    {
        dirs.Add((Path.Combine(vdir, "content", "textures", "Cursors", "KeyboardMouse"), false, 0));
        dirs.Add((Path.Combine(vdir, "content", "textures", "Cursors"), false, 0));
        dirs.Add((Path.Combine(vdir, "content", "textures"), false, 0));
    }
    public static List<CurTarget> Scan()
    {
        var res = new List<CurTarget>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dirs = new List<(string d, bool mk, int only)>();
        try
        {
            if (Os.IsWin)
            {
                string lad = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "";
                if (lad == "") return res;
                string vers = Path.Combine(lad, "Roblox", "Versions");
                if (Directory.Exists(vers)) foreach (var v in Directory.GetDirectories(vers, "version-*")) PushDirs(dirs, v);
                foreach (var lc in new[] { "Bloxstrap", "Fishstrap", "Voidstrap" })
                {
                    string root = Path.Combine(lad, lc);
                    if (!Directory.Exists(root)) continue;
                    string lv = Path.Combine(root, "Versions");
                    if (Directory.Exists(lv)) foreach (var v in Directory.GetDirectories(lv, "version-*")) if (File.Exists(Path.Combine(v, "RobloxPlayerBeta.exe"))) PushDirs(dirs, v);
                    string md = Path.Combine(root, "Modifications", "content", "textures");
                    dirs.Add((Path.Combine(md, "Cursors", "KeyboardMouse"), true, 1));
                    dirs.Add((Path.Combine(md, "Cursors", "KeyboardMouse"), true, 4));
                    dirs.Add((Path.Combine(md, "Cursors"), true, 3));
                    dirs.Add((md, true, 2));
                }
                foreach (var p in Roblox.Processes())
                {
                    string exe = ""; try { exe = p.MainModule?.FileName ?? ""; } catch { }
                    if (exe == "") continue;
                    string? vdir = Path.GetDirectoryName(exe);
                    if (vdir is not null) PushDirs(dirs, vdir);
                }
            }
            else if (Os.IsMac)
            {
                foreach (var app in new[] { "/Applications/Roblox.app", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications", "Roblox.app") })
                    if (Directory.Exists(app)) PushDirs(dirs, Path.Combine(app, "Contents", "Resources"));
            }
        }
        catch { }
        foreach (var e in dirs)
        {
            for (int si = 1; si <= Slots.Length; si++)
            {
                if (e.only != 0 && e.only != si) continue;
                foreach (var fn in Slots[si - 1].Names)
                {
                    string pth = Path.Combine(e.d, fn);
                    if (!seen.Add(pth)) continue;
                    if (!e.mk && !File.Exists(pth)) { seen.Remove(pth); continue; }
                    res.Add(new CurTarget(pth, e.d, fn, si, e.mk));
                }
            }
        }
        return res;
    }
    public static List<CurTarget> GetTargets(bool force = false)
    {
        if (force || TgtAt == 0 || Clock.Tick - TgtAt > 4000) { Targets = Scan(); TgtAt = Clock.Tick; }
        return Targets;
    }
    public static bool Writable(CurTarget t) => !(!Far && t.Name == FAROPT) && Slot[t.Slot - 1].Has;
    public static List<CurTarget> Active() => GetTargets().Where(Writable).ToList();
    public static int ActiveN() => GetTargets().Count(Writable);
    public static int FoundN() => GetTargets().Count(t => Far || t.Name != FAROPT);
    /// <summary>CurKey(s): FNV-1a 32 of the lowercase path, eight hex digits.</summary>
    public static string Key(string s)
    {
        uint h = 2166136261;
        foreach (char c in s.ToLowerInvariant()) h = (h ^ c) * 16777619;
        return h.ToString("X8");
    }
    public static string BakPath(CurTarget t) => Path.Combine(BakDir, Key(t.Path) + "_" + t.Name);

    // ---- which Bloxstrap Modifications files are OURS ----
    // "A mod file with no backup is one we created" is true only until a restore
    // has consumed the backup. Afterwards the same test calls a file we had just
    // put back - Bloxstrap's own cursor - ours to delete, so RESTORE took two
    // presses: the first returned the original into the mod folder and the
    // second removed the folder's file. Creation is RECORDED at apply time now,
    // so one press finishes and a second cannot destroy a mod we never made.
    static HashSet<string>? _made;
    static HashSet<string> Made()
    {
        if (_made is not null) return _made;
        _made = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in Ini.Read(Paths.IniFile, "cursor", "made", "").Split(',', StringSplitOptions.RemoveEmptyEntries)) _made.Add(k.Trim());
        return _made;
    }
    // "No record" and "a record that is now empty" are different answers. The
    // legacy fallback below treats an absent record as "unknown, use the old
    // rule" - so once a restore had dropped every key, the third press would
    // have fallen back to it and deleted a Bloxstrap file again. This flag is
    // written the first time anything is recorded and never cleared.
    static void MadeSave()
    {
        Ini.Write(Paths.IniFile, "cursor", "made", string.Join(",", Made()));
        Ini.Write(Paths.IniFile, "cursor", "madeset", 1L);
    }
    static bool MadeKept => Ini.ReadInt(Paths.IniFile, "cursor", "madeset", 0) != 0;
    static void MadeAdd(CurTarget t) { if (Made().Add(Key(t.Path))) MadeSave(); }
    static bool MadeHas(CurTarget t) => Made().Contains(Key(t.Path));
    static void MadeDrop(CurTarget t) { if (Made().Remove(Key(t.Path))) MadeSave(); }
    public static int BakCount(bool force = false)
    {
        if (force || BakAt == 0 || Clock.Tick - BakAt > 4000)
        {
            int n = 0;
            try { if (Directory.Exists(BakDir)) n = Directory.GetFiles(BakDir, "*.png").Length; } catch { }
            BakN = n; BakAt = Clock.Tick;
        }
        return BakN;
    }
    static string PrevName(int si) => Slots[si - 1].Prev ?? Slots[si - 1].Names[0];
    /// <summary>CurDefaultPath(si): the picture the slot shows when nothing is staged — our backup, a strap's mod, else the live file.</summary>
    static string DefaultPath(int si)
    {
        string prim = PrevName(si), live = "", mod = "";
        Slot[si - 1].Dsrc = 0;
        foreach (var t in GetTargets())
        {
            if (t.Slot != si || t.Name != prim) continue;
            if (t.Mk) { if (mod == "" && File.Exists(t.Path)) mod = t.Path; continue; }
            string bak = BakPath(t);
            if (File.Exists(bak)) { Slot[si - 1].Dsrc = 2; return bak; }
            if (live == "" && File.Exists(t.Path)) live = t.Path;
        }
        if (mod != "") { Slot[si - 1].Dsrc = 1; return mod; }
        return live;
    }
    public static Bitmap? BitmapFromFile(string pth)
    {
        if (pth == "" || !File.Exists(pth)) return null;
        try { if (new FileInfo(pth).Length > 8388608) return null; using var fs = File.OpenRead(pth); return new Bitmap(fs); } catch { return null; }
    }
    static void SyncDefault(int si)
    {
        var s = Slot[si - 1];
        string p = DefaultPath(si);
        if (p == s.Dpath) return;
        s.Dpath = p; s.Dbmp = null; s.Dbw = 0; s.Dbh = 0;
        if (p == "") return;
        var b = BitmapFromFile(p);
        if (b is not null) { s.Dbmp = b; s.Dbw = b.PixelSize.Width; s.Dbh = b.PixelSize.Height; }
    }
    public static void SyncDefaults(bool force = false)
    {
        if (Busy && !force) return;
        if (force) DefAt = -1;
        GetTargets();
        if (DefAt == TgtAt) return;
        DefAt = TgtAt;
        for (int si = 1; si <= Slots.Length; si++) SyncDefault(si);
    }
    static void DropDefaults()
    {
        foreach (var s in Slot) { s.Dbmp = null; s.Dbw = 0; s.Dbh = 0; s.Dpath = "\f"; }
        DefAt = -1;
    }
    public static void EnsureDirs() { try { Directory.CreateDirectory(Dir); Directory.CreateDirectory(BakDir); } catch { } }

    /// <summary>CurSetImage(si, path): fit the picture into 64x64 as the working copy, keep the source, remember the path.</summary>
    public static void SetImage(int si, string path)
    {
        if (si < 1 || si > Slots.Length) return;
        if (path == "" || !File.Exists(path)) { Say("FILE NOT FOUND", C_OFF); return; }
        var src = BitmapFromFile(path);
        if (src is null) { Say("NOT A READABLE IMAGE", C_OFF); return; }
        int w = src.PixelSize.Width, h = src.PixelSize.Height;
        if (w == 0 || h == 0) { Say("EMPTY IMAGE", C_OFF); return; }
        EnsureDirs();
        double sc = Math.Min(SIZE / (double)w, SIZE / (double)h);
        double dw = Math.Max(1, Math.Round(w * sc)), dh = Math.Max(1, Math.Round(h * sc));
        double ox = (SIZE - dw) / 2, oy = (SIZE - dh) / 2;
        try
        {
            using var rt = new RenderTargetBitmap(new PixelSize(SIZE, SIZE), new Vector(96, 96));
            using (var dc = rt.CreateDrawingContext())
            {
                using (dc.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
                    dc.DrawImage(src, new Rect(0, 0, w, h), new Rect(ox, oy, dw, dh));
            }
            rt.Save(SlotImg(si));
            src.Save(SlotSrc(si));
        }
        catch { Say("COULD NOT WRITE THE WORKING COPY", C_OFF); return; }
        var s = Slot[si - 1];
        s.Bmp = src; s.Bw = w; s.Bh = h; s.Src = path;
        s.Has = File.Exists(SlotImg(si));
        s.PickAt = Clock.Tick;
        Applied = 0; ApplyAt = 0;
        Ini.Write(Paths.IniFile, "cursor", Slots[si - 1].Key, path);
        if (!s.Has) { Say("COULD NOT WRITE THE WORKING COPY", C_OFF); return; }
        Say(Slots[si - 1].Label + " READY  " + w + "x" + h + " -> " + SIZE + "x" + SIZE, C_ON);
    }
    static bool TryDelete(string pth)
    {
        for (int k = 0; k < 3; k++)
        {
            try { File.Delete(pth); return true; } catch { }
            if (!File.Exists(pth)) return true;
            Thread.Sleep(40);
        }
        return !File.Exists(pth);
    }
    public static bool ClearSlot(int si)
    {
        if (si < 1 || si > Slots.Length) return false;
        var s = Slot[si - 1];
        s.Bmp = null; s.Bw = 0; s.Bh = 0; s.Src = ""; s.Has = false; s.PickAt = 0;
        bool okImg = TryDelete(SlotImg(si)), okSrc = TryDelete(SlotSrc(si));
        Ini.Delete(Paths.IniFile, "cursor", Slots[si - 1].Key);
        if (!okImg || !okSrc) { Say(Slots[si - 1].Label + " CLEARED, BUT ITS WORKING COPY IS LOCKED", AMBER); return false; }
        return true;
    }
    public static void OpenFolder()
    {
        EnsureDirs();
        try
        {
            if (Os.IsWin) Process.Start(new ProcessStartInfo("explorer.exe", "\"" + Dir + "\"") { UseShellExecute = true });
            else if (Os.IsMac) Process.Start("open", new[] { Dir });
        }
        catch { }
        Say("OPENED THE CURSOR FOLDER", 0);
    }

    /// <summary>CurApply: every staged slot over every target, the originals backed up first.</summary>
    public static int Apply(bool silent = false)
    {
        int staged = Slot.Count(s => s.Has);
        if (staged == 0) { if (!silent) Say("PICK AN IMAGE FIRST", AMBER); return 0; }
        var tg = Active();
        if (tg.Count == 0) { if (!silent) Say("NO MATCHING ROBLOX CURSOR FILES FOUND", C_OFF); return 0; }
        EnsureDirs();
        if (Busy) return 0;
        Busy = true;
        DropDefaults();
        int ok = 0, bad = 0;
        try
        {
            foreach (var t in tg)
            {
                if (t.Mk && !Directory.Exists(t.Dir)) { try { Directory.CreateDirectory(t.Dir); } catch { } }
                if (t.Mk && !File.Exists(t.Path)) MadeAdd(t);    // there was no mod file here before us
                string bak = BakPath(t);
                if (!File.Exists(bak) && File.Exists(t.Path)) { try { File.Copy(t.Path, bak, false); } catch { } }
                try { File.Copy(SlotImg(t.Slot), t.Path, true); ok++; } catch { bad++; }
            }
        }
        finally { Busy = false; }
        Applied = ok; ApplyAt = Clock.Tick;
        GetTargets(true); BakCount(true); SyncDefaults(true);
        if (!silent)
        {
            string rbx = Roblox.IsRunning() ? "  -  RESTART ROBLOX" : "";
            if (ok > 0 && bad == 0) Say("APPLIED TO " + ok + " FILE(S)" + rbx, C_ON);
            else if (ok > 0) Say("APPLIED " + ok + ", " + bad + " LOCKED - CLOSE ROBLOX", AMBER);
            else Say("WRITE FAILED - CLOSE ROBLOX OR RUN AS ADMIN", C_OFF);
        }
        return ok;
    }
    /// <summary>CurRestore: the backed-up originals back, the straps' overrides removed, the slots cleared.</summary>
    public static void Restore()
    {
        if (Busy) return;
        Busy = true;
        DropDefaults();
        int n = 0, bad = 0, rm = 0, stale = 0, seen = 0, mkSeen = 0;
        var slotBad = new HashSet<int>();
        try
        {
            foreach (var t in GetTargets(true))
            {
                seen++;
                if (t.Mk) mkSeen++;
                string bak = BakPath(t);
                if (!File.Exists(bak))
                {
                    // Ours to remove only if we brought it into existence. Kept
                    // as the fallback for a hub that applied before this record
                    // existed: no record at all means the old rule still stands.
                    bool ours = MadeHas(t) || (!MadeKept && Made().Count == 0);
                    if (t.Mk && ours && File.Exists(t.Path))
                    {
                        if (TryDelete(t.Path)) { rm++; MadeDrop(t); }
                        else { bad++; slotBad.Add(t.Slot); }
                    }
                    continue;
                }
                bool done = false;
                try { File.Copy(bak, t.Path, true); done = true; } catch { bad++; slotBad.Add(t.Slot); }
                if (!done) continue;
                n++;
                MadeDrop(t);                                     // whatever is there now is theirs, not ours
                if (!TryDelete(bak)) stale++;
            }
        }
        finally { Busy = false; }
        Applied = 0; ApplyAt = 0;
        for (int si = 1; si <= Slots.Length; si++) if (!slotBad.Contains(si)) ClearSlot(si);
        // Rescan first, as Apply does. Without it SyncDefaults reads the target
        // list captured before the loop ran, so the preview describes the files
        // as they were rather than as the restore has just left them.
        GetTargets(true); BakCount(true); SyncDefaults(true);
        if (n > 0 || rm > 0)
        {
            string rbx = Roblox.IsRunning() ? "  -  RESTART ROBLOX" : "";
            string msg = n > 0 ? "RESTORED " + n + " FILE(S)" : "";
            if (rm > 0) msg += (msg == "" ? "" : "  -  ") + "REMOVED " + rm + " OVERRIDE(S)";
            if (bad > 0) msg += "  -  " + bad + " LOCKED";
            if (stale > 0) msg += "  -  " + stale + " BACKUP(S) COULD NOT BE REMOVED";
            Say(msg + rbx, (bad > 0 || stale > 0) ? AMBER : C_ON);
        }
        else if (bad > 0) Say("RESTORE FAILED - CLOSE ROBLOX OR RUN AS ADMIN", C_OFF);
        else if (seen == 0) Say("NOTHING TO RESTORE - NO CURSOR FILES FOUND", AMBER);
        else Say("NOTHING OF OURS TO RESTORE - " + seen + " FILE(S) CHECKED" + (mkSeen > 0 ? ", " + mkSeen + " MOD SLOT(S)" : "") + "  -  ANY CUSTOM CURSOR LEFT IS NOT ONE WE APPLIED", AMBER);
    }
    public static void ManageAuto()
    {
        _autoTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15000) };
        _autoTimer.Tick -= OnAuto; _autoTimer.Tick += OnAuto;
        if (Auto) _autoTimer.Start(); else _autoTimer.Stop();
    }
    static void OnAuto(object? s, EventArgs e) { try { AutoTick(); } catch { } }
    /// <summary>CurAutoTick: a target that was not there last time means a new client build — re-apply.</summary>
    public static void AutoTick()
    {
        if (!Auto || Busy) return;
        if (!Slot.Any(s => s.Has)) return;
        var before = new HashSet<string>(Targets.Select(t => t.Path), StringComparer.OrdinalIgnoreCase);
        int fresh = GetTargets(true).Count(t => !before.Contains(t.Path));
        if (fresh == 0 || before.Count == 0) return;
        if (Apply(true) > 0) Say("AUTO RE-APPLIED TO A NEW ROBLOX BUILD", C_ON);
    }
    public static void Boot()
    {
        Far = Ini.ReadInt(Paths.IniFile, "cursor", "far", 1) != 0;
        Auto = Ini.ReadInt(Paths.IniFile, "cursor", "auto", 0) != 0;
        FarT = Far ? 1 : 0; AutoT = Auto ? 1 : 0;
        EnsureDirs();
        for (int si = 1; si <= Slots.Length; si++)
        {
            var s = Slot[si - 1];
            s.Src = Ini.Read(Paths.IniFile, "cursor", Slots[si - 1].Key, "");
            s.Has = File.Exists(SlotImg(si));
            string p = SlotSrc(si);
            if (!File.Exists(p)) continue;
            var b = BitmapFromFile(p);
            if (b is not null) { s.Bmp = b; s.Bw = b.PixelSize.Width; s.Bh = b.PixelSize.Height; }
        }
        int any = Slot.Count(s => s.Has);
        Msg = any > 0 ? any + " OF 4 IMAGE(S) LOADED" : "NO IMAGE";
        MsgCol = any > 0 ? C_ON : 0xFFC7CBE0;
        GetTargets(true);
        SyncDefaults(true);
        ManageAuto();
    }
    public static string DimText(int si)
    {
        var s = Slot[si - 1];
        if (s.Bw != 0 && s.Bh != 0 && s.Has) return s.Bw + " x " + s.Bh + "  \u00B7  fitted";
        return s.Dbmp is not null ? "not set  \u00B7  " + SrcName(si) : "not set";
    }
    public static string SrcName(int si) => Slot[si - 1].Dsrc == 1 ? "bloxstrap mod" : "roblox default";
}
