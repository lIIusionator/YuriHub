using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Yuri.Core;
using Yuri.Gfx;
using Yuri.Modules.FastFlags;
using Yuri.Platform;
using Yuri.Shell.Hub;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.G;

namespace Yuri.Modules.ClientSettings;

/// <summary>
/// The parts of CLIENT SETTINGS that replace files: ROBLOX FONT, ROBLOX LOGO,
/// ROBLOX STUDIO LOGO, RESTORE LOGOS, and the live framerate cap. Every file
/// overwritten is copied aside first (YURI\settings\fonts, YURI\settings\logo)
/// and listed in backups.dat with the path it came from, so REVERT and RESTORE
/// put back what was actually there; every shortcut repointed has its previous
/// IconLocation recorded in icons.dat. The client's texture folder is scanned
/// for the logo art rather than assuming a filename - Roblox has moved it
/// between builds - and the user can point at the exact file instead.
/// </summary>
public static class RSetLogo
{
    const uint AMBER = RSet.AMBER, C_ON = RSet.C_ON, C_OFF = RSet.C_OFF, C_ACC = RSet.C_ACC;
    static readonly Regex LogoPat = new("logo.*\\.png$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    public sealed record Tgt(string Path, string Dir, string Name, string Sub, bool Mk);
    public sealed record Lnk(string Path, string Name);
    public static string LogoSub = "", LogoSubS = "";
    public static readonly Dictionary<string, string> IcoOrig = new();                       // shortcut path -> IconLocation it had
    public static readonly Dictionary<string, (string kind, string orig)> BakLog = new();    // backup path -> what it came from
    static List<Tgt>? _tg, _tgS; static long _tgAt;
    static List<Lnk>[] _lnk = { new(), new() }; static long _lnkAt;
    static string[] _appExe = { "", "" }; static long _appExeAt;
    static Bitmap?[] _logoBmp = { null, null }, _logoDef = { null, null }; static string[] _logoDefP = { "", "" };
    public static readonly Dictionary<string, string> FpsWas = new(); public static int FpsWasP;
    static bool _picking;
    public static Func<string, string, List<Tgt>>? TargetsOverride;                            // tests: (kind, sub) -> targets
    public static Func<List<Tgt>>? FontTargetsOverride;
    static string LogoDir => Path.Combine(RSet.Dir, "logo");
    static string FontDir => Path.Combine(RSet.Dir, "fonts");
    static string IcoLog => Path.Combine(LogoDir, "icons.dat");
    static string BakLogPath => Path.Combine(RSet.Dir, "backups.dat");
    public static string IcoPath(int kind) => Path.Combine(LogoDir, "appicon" + (kind == 2 ? "_studio" : "") + ".ico");
    public static string LogoOf(int kind) => kind == 2 ? RSet.LogoS : RSet.Logo;
    public static string LogoSubOf(int kind) => kind == 2 ? LogoSubS : LogoSub;
    public static string LogoExe(int kind) => kind == 2 ? "RobloxStudioBeta.exe" : "RobloxPlayerBeta.exe";
    public static string LogoName(int kind) => kind == 2 ? "STUDIO" : "PLAYER";
    static string Lad => Os.IsWin ? Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "" : "";
    static string Key(string s) => Cursor.Cur.Key(s);

    public static void Register()
    {
        LogoSub = Ini.Read(Paths.IniFile, "rset", "logosub", "");
        LogoSubS = Ini.Read(Paths.IniFile, "rset", "logosubS", "");
        IcoLoad(); BakLoad();
        RSet.FontApplyHook = FontApply; RSet.FontRevertHook = FontRevert;
        RSet.LogoApplyHook = () => (RSet.By("logo") is { } lg1 && RSet.Idx(lg1) > 1 ? LogoApply(1) : 0) + (RSet.By("logos") is { } ls1 && RSet.Idx(ls1) > 1 ? LogoApply(2) : 0); RSet.LogoRevertHook = () => LogoRevert(0);
        RSet.IconApplyHook = () => (RSet.By("logo") is { } lg && RSet.Idx(lg) > 1 ? IconApply(1) : 0) + (RSet.By("logos") is { } ls && RSet.Idx(ls) > 1 ? IconApply(2) : 0);
        RSet.IconRevertHook = IconRevert;
        RSet.FpsLiveHook = FpsLive; RSet.FpsLiveRevertHook = FpsLiveRevert;
        RSet.IcoOrigCount = () => IcoOrig.Count;
        RSet.LogoClearHook = () => { LogoSub = ""; LogoSubS = ""; Ini.Write(Paths.IniFile, "rset", "logosub", ""); Ini.Write(Paths.IniFile, "rset", "logosubS", ""); LogoLoad(0); LogoScan(1, true); };
        RSetPanel.PickFont = PickFontAsync; RSetPanel.PickLogo = h => PickLogoAsync(h, 1); RSetPanel.PickLogoS = h => PickLogoAsync(h, 2);
        RSetPanel.LogoApplyOne = LogoApplyOne; RSetPanel.LogoRestore = LogoRestore; RSetPanel.LogoTile = Tile;
        RSetPanel.LogoHint = Hint;
        LogoLoad(0);
    }

    // ---- targets ----
    public static List<Tgt> FontTargets()
    {
        if (FontTargetsOverride is not null) return FontTargetsOverride();
        var res = new List<Tgt>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string lad = Lad; if (lad == "") return res;
        try
        {
            string vers = Path.Combine(lad, "Roblox", "Versions");
            if (Directory.Exists(vers))
                foreach (var v in Directory.GetDirectories(vers, "version-*"))
                {
                    string d = Path.Combine(v, "content", "fonts");
                    if (!Directory.Exists(d)) continue;
                    foreach (var f in Directory.GetFiles(d)) { if (!Regex.IsMatch(f, "\\.(ttf|otf)$", RegexOptions.IgnoreCase)) continue; string n = Path.GetFileName(f); names.Add(n); res.Add(new Tgt(f, d, n, "", false)); }
                }
            if (names.Count > 0 && Directory.Exists(Path.Combine(lad, "Bloxstrap")))
            {
                string bd = Path.Combine(lad, "Bloxstrap", "Modifications", "content", "fonts");
                foreach (var nm in names) res.Add(new Tgt(Path.Combine(bd, nm), bd, nm, "", true));
            }
        }
        catch { }
        return res;
    }
    public static List<Tgt> LogoTargets(int kind)
    {
        if (TargetsOverride is not null) return TargetsOverride(kind.ToString(), LogoSubOf(kind));
        var res = new List<Tgt>(); var rel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string lad = Lad; if (lad == "") return res;
        try
        {
            string exe = LogoExe(kind), vers = Path.Combine(lad, "Roblox", "Versions");
            if (Directory.Exists(vers))
                foreach (var v in Directory.GetDirectories(vers, "version-*"))
                {
                    if (!File.Exists(Path.Combine(v, exe))) continue;
                    string root = Path.Combine(v, "content", "textures");
                    if (!Directory.Exists(root)) continue;
                    string sub0 = LogoSubOf(kind);
                    if (sub0 != "")
                    {
                        string fp = Path.Combine(root, sub0);
                        if (File.Exists(fp)) { rel.Add(sub0); res.Add(new Tgt(fp, Path.GetDirectoryName(fp) ?? root, Path.GetFileName(fp), sub0, false)); }
                        continue;
                    }
                    foreach (var f in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories))
                    {
                        if (!LogoPat.IsMatch(Path.GetFileName(f))) continue;
                        string sub = f[(root.Length + 1)..];
                        rel.Add(sub);
                        res.Add(new Tgt(f, Path.GetDirectoryName(f) ?? root, Path.GetFileName(f), sub, false));
                    }
                }
            if (kind == 1 && rel.Count > 0 && Directory.Exists(Path.Combine(lad, "Bloxstrap")))
            {
                string bd = Path.Combine(lad, "Bloxstrap", "Modifications", "content", "textures");
                foreach (var sub in rel) { string fp = Path.Combine(bd, sub); res.Add(new Tgt(fp, Path.GetDirectoryName(fp) ?? bd, Path.GetFileName(fp), sub, true)); }
            }
        }
        catch { }
        return res;
    }
    public static List<Tgt> LogoScan(int kind, bool force = false)
    {
        if (force || _tg is null || _tgS is null || Clock.Tick - _tgAt > 20000) { _tg = LogoTargets(1); _tgS = LogoTargets(2); _tgAt = Clock.Tick; }
        return kind == 2 ? _tgS! : _tg!;
    }
    public static int LogoCount(int kind, bool force = false) => LogoScan(kind, force).Count;
    public static string AppExe(int kind)
    {
        if (_appExeAt == 0 || Clock.Tick - _appExeAt > 30000)
        {
            _appExeAt = Clock.Tick;
            string lad = Lad;
            for (int k = 1; k <= 2; k++)
            {
                string best = "";
                try
                {
                    string vers = Path.Combine(lad, "Roblox", "Versions");
                    if (lad != "" && Directory.Exists(vers)) foreach (var v in Directory.GetDirectories(vers, "version-*")) { string fp = Path.Combine(v, LogoExe(k)); if (File.Exists(fp)) best = fp; }
                }
                catch { }
                _appExe[k - 1] = best;
            }
        }
        return _appExe[kind - 1];
    }
    static string TexRoot(int kind)
    {
        string lad = Lad, best = "";
        if (lad == "") return "";
        try
        {
            string vers = Path.Combine(lad, "Roblox", "Versions");
            if (Directory.Exists(vers)) foreach (var v in Directory.GetDirectories(vers, "version-*")) { if (!File.Exists(Path.Combine(v, LogoExe(kind)))) continue; string d = Path.Combine(v, "content", "textures"); if (Directory.Exists(d)) best = d; }
        }
        catch { }
        return best;
    }
    public static List<Lnk> LnkScan(int kind, bool force = false)
    {
        if (force || _lnkAt == 0 || Clock.Tick - _lnkAt > 30000) { _lnk[0] = LnkTargets(1); _lnk[1] = LnkTargets(2); _lnkAt = Clock.Tick; }
        return _lnk[kind - 1];
    }
    public static Func<int, List<Lnk>>? LnkOverride;
    static List<Lnk> LnkTargets(int kind)
    {
        if (LnkOverride is not null) return LnkOverride(kind);
        var res = new List<Lnk>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Os.IsWin) return res;
        string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        foreach (var r in new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.Programs), Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), Path.Combine(appdata, "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar") })
        {
            if (r == "" || !Directory.Exists(r)) continue;
            try
            {
                foreach (var f in Directory.GetFiles(r, "*.lnk", SearchOption.AllDirectories))
                {
                    string n = Path.GetFileName(f);
                    if (!Regex.IsMatch(n, "^(roblox|bloxstrap|fishstrap)", RegexOptions.IgnoreCase)) continue;
                    bool isS = Regex.IsMatch(n, "studio", RegexOptions.IgnoreCase);
                    if (isS != (kind == 2)) continue;
                    if (!seen.Add(f)) continue;
                    res.Add(new Lnk(f, n));
                }
            }
            catch { }
        }
        return res;
    }

    // ---- the journals ----
    static void IcoSave()
    {
        try { Directory.CreateDirectory(LogoDir); } catch { }
        try { File.Delete(IcoLog); } catch { }
        if (IcoOrig.Count > 0) { try { File.WriteAllText(IcoLog, string.Join("\n", IcoOrig.Select(kv => kv.Key + "|" + kv.Value)) + "\n", new UTF8Encoding(false)); } catch { } }
    }
    static void IcoLoad()
    {
        IcoOrig.Clear();
        if (!File.Exists(IcoLog)) return;
        try { foreach (var ln in File.ReadAllLines(IcoLog)) { int q = ln.IndexOf('|'); if (q > 0) IcoOrig[ln[..q]] = ln[(q + 1)..]; } } catch { }
    }
    static void BakLoad()
    {
        BakLog.Clear();
        if (!File.Exists(BakLogPath)) return;
        try { foreach (var ln in File.ReadAllLines(BakLogPath)) { var p = ln.Split('|', 3); if (p.Length == 3 && p[1] != "" && p[2] != "") BakLog[p[1]] = (p[0], p[2]); } } catch { }
    }
    static void BakSave()
    {
        RSet.EnsureDirs();
        try { File.Delete(BakLogPath); } catch { }
        if (BakLog.Count > 0) { try { File.WriteAllText(BakLogPath, string.Join("\n", BakLog.Select(kv => kv.Value.kind + "|" + kv.Key + "|" + kv.Value.orig)) + "\n", new UTF8Encoding(false)); } catch { } }
    }
    static bool BakNote(string kind, string bak, string orig) { if (BakLog.ContainsKey(bak)) return false; BakLog[bak] = (kind, orig); return true; }
    /// <summary>RSetBakSweep(kinds): backups whose target the scan no longer lists (an install that was updated away) still go back where they came from.</summary>
    static int BakSweep(HashSet<string> kinds)
    {
        int n = 0; bool dirty = false;
        foreach (var (bak, e) in BakLog.ToList())
        {
            if (!kinds.Contains(e.kind)) continue;
            if (!File.Exists(bak)) { BakLog.Remove(bak); dirty = true; continue; }
            string od = Path.GetDirectoryName(e.orig) ?? "";
            if (od == "" || !Directory.Exists(od)) { try { File.Delete(bak); } catch { } BakLog.Remove(bak); dirty = true; continue; }
            bool ok = false;
            try { File.Copy(bak, e.orig, true); File.Delete(bak); ok = true; } catch { }
            if (ok) { BakLog.Remove(bak); dirty = true; n++; }
        }
        if (dirty) BakSave();
        return n;
    }

    // ---- the font ----
    static string FontBak(Tgt t) => Path.Combine(FontDir, Key(t.Path) + "_" + t.Name);
    public static int FontApply()
    {
        if (RSet.Font == "" || !File.Exists(RSet.Font)) return 0;
        var tg = FontTargets();
        if (tg.Count == 0) return 0;
        try { Directory.CreateDirectory(FontDir); } catch { }
        int ok = 0; bool dirty = false;
        foreach (var t in tg)
        {
            if (t.Mk) { try { Directory.CreateDirectory(t.Dir); } catch { } }
            string bak = FontBak(t);
            if (!File.Exists(bak) && File.Exists(t.Path)) { try { File.Copy(t.Path, bak, false); } catch { } }
            try { File.Copy(RSet.Font, t.Path, true); ok++; if (File.Exists(bak)) dirty |= BakNote("font", bak, t.Path); } catch { }
        }
        if (dirty) BakSave();
        return ok;
    }
    public static int FontRevert()
    {
        int n = 0; bool dirty = false;
        foreach (var t in FontTargets())
        {
            string bak = FontBak(t);
            if (!File.Exists(bak)) continue;
            try { File.Copy(bak, t.Path, true); File.Delete(bak); n++; if (BakLog.Remove(bak)) dirty = true; } catch { }
        }
        if (dirty) BakSave();
        return n + BakSweep(new HashSet<string> { "font" });
    }

    // ---- the logo art ----
    static string LogoBak(Tgt t) => Path.Combine(LogoDir, Key(t.Path) + "_" + t.Name);
    public static int LogoApply(int kind)
    {
        string src = LogoOf(kind);
        if (src == "" || !File.Exists(src)) return 0;
        var tg = LogoTargets(kind);
        if (tg.Count == 0) return 0;
        try { Directory.CreateDirectory(LogoDir); } catch { }
        int ok = 0; bool dirty = false;
        foreach (var t in tg)
        {
            if (t.Mk) { try { Directory.CreateDirectory(t.Dir); } catch { } }
            string bak = LogoBak(t);
            if (!File.Exists(bak) && File.Exists(t.Path)) { try { File.Copy(t.Path, bak, false); } catch { } }
            try { File.Copy(src, t.Path, true); ok++; if (File.Exists(bak)) dirty |= BakNote("logo" + kind, bak, t.Path); } catch { }
        }
        if (dirty) BakSave();
        return ok;
    }
    public static int LogoRevert(int kind)
    {
        if (kind == 0) return LogoRevert(1) + LogoRevert(2);
        int n = 0; bool dirty = false;
        foreach (var t in LogoTargets(kind))
        {
            string bak = LogoBak(t);
            if (!File.Exists(bak)) continue;
            try { File.Copy(bak, t.Path, true); File.Delete(bak); n++; if (BakLog.Remove(bak)) dirty = true; } catch { }
        }
        if (dirty) BakSave();
        return n + BakSweep(new HashSet<string> { "logo" + kind });
    }

    // ---- the shortcut icons ----
    /// <summary>RSetIcoWrite(src, out): the image fitted into a 256x256 PNG, wrapped in the one-entry ICO header Windows accepts for PNG icons.</summary>
    public static bool IcoWrite(string srcPath, string outPath)
    {
        if (srcPath == "" || !File.Exists(srcPath)) return false;
        Bitmap? src; try { src = new Bitmap(srcPath); } catch { return false; }
        using (src)
        {
            int w = src.PixelSize.Width, h = src.PixelSize.Height;
            if (w == 0 || h == 0) return false;
            const int SZ = 256;
            double sc = Math.Min(SZ / (double)w, SZ / (double)h);
            double dw = Math.Max(1, Math.Round(w * sc)), dh = Math.Max(1, Math.Round(h * sc));
            byte[] png;
            try
            {
                using var rt = new RenderTargetBitmap(new PixelSize(SZ, SZ), new Vector(96, 96));
                using (var dc = rt.CreateDrawingContext())
                using (dc.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
                    dc.DrawImage(src, new Rect(0, 0, w, h), new Rect((SZ - dw) / 2, (SZ - dh) / 2, dw, dh));
                using var ms = new MemoryStream();
                rt.Save(ms);
                png = ms.ToArray();
            }
            catch { return false; }
            var ico = new byte[22 + png.Length];
            ico[2] = 1; ico[4] = 1;                                              // type 1 = icon, one image
            ico[10] = 1; ico[12] = 32;                                          // colour planes, bits per pixel
            BitConverter.GetBytes(png.Length).CopyTo(ico, 14);
            BitConverter.GetBytes(22).CopyTo(ico, 18);
            png.CopyTo(ico, 22);
            try { File.Delete(outPath); File.WriteAllBytes(outPath, ico); } catch { return false; }
            return File.Exists(outPath);
        }
    }
    public static Func<string, string?>? LnkIconGet; public static Func<string, string, bool>? LnkIconSet;   // the shortcut IconLocation, WScript.Shell on Windows
    static string? ShortcutIcon(string path)
    {
        if (LnkIconGet is not null) return LnkIconGet(path);
        if (!Os.IsWin) return null;
        try { dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!; dynamic lnk = sh.CreateShortcut(path); return (string)lnk.IconLocation; } catch { return null; }
    }
    static bool ShortcutIconSet(string path, string ico)
    {
        if (LnkIconSet is not null) return LnkIconSet(path, ico);
        if (!Os.IsWin) return false;
        try { dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!; dynamic lnk = sh.CreateShortcut(path); lnk.IconLocation = ico; lnk.Save(); return true; } catch { return false; }
    }
    public static int IconApply(int kind)
    {
        string src = LogoOf(kind);
        if (src == "" || !File.Exists(src)) return 0;
        try { Directory.CreateDirectory(LogoDir); } catch { }
        string ico = IcoPath(kind);
        if (!IcoWrite(src, ico)) return 0;
        var lst = LnkTargets(kind);
        if (lst.Count == 0) return 0;
        int n = 0;
        foreach (var t in lst)
        {
            if (!IcoOrig.ContainsKey(t.Path)) { var was = ShortcutIcon(t.Path); if (was is null) continue; IcoOrig[t.Path] = was; }
            if (ShortcutIconSet(t.Path, ico + ",0")) n++;
        }
        if (n > 0) { IcoSave(); IconRefresh(); }
        return n;
    }
    public static int IconRevert()
    {
        var mine = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { IcoPath(1), IcoPath(2) };
        int n = 0;
        for (int kind = 1; kind <= 2; kind++)
        {
            string exe = AppExe(kind);
            foreach (var t in LnkScan(kind, true))
            {
                if (!File.Exists(t.Path)) continue;
                string want = "";
                if (IcoOrig.TryGetValue(t.Path, out var w0)) want = w0;
                else
                {
                    string cur = ShortcutIcon(t.Path) ?? "";
                    string cp = Regex.Replace(cur, ",\\s*-?\\d+\\s*$", "").Trim();
                    if (cp != "" && mine.Contains(cp) && exe != "") want = exe + ",0";
                }
                if (want == "") continue;
                if (ShortcutIconSet(t.Path, want)) n++;
            }
        }
        IcoOrig.Clear();
        try { File.Delete(IcoLog); } catch { }
        if (n > 0) IconRefresh();
        return n;
    }
    [System.Runtime.InteropServices.DllImport("shell32.dll")] static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
    static void IconRefresh() { if (Os.IsWin) { try { SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); } catch { } } }

    // ---- the actions ----
    public static void LogoApplyOne(int kind)
    {
        string src = LogoOf(kind);
        if (src == "" || !File.Exists(src)) { RSet.Say("PICK AN IMAGE FIRST - PRESS BROWSE", AMBER); return; }
        int nt = LogoApply(kind), ni = IconApply(kind);
        string key = kind == 2 ? "logos" : "logo";
        RSet.Pick[key] = 2; RSet.FlashAt[key] = Clock.Tick;
        LogoScan(1, true);
        string nm = LogoName(kind);
        if (nt > 0 || ni > 0) RSet.Say(nm + " LOGO APPLIED  -  " + nt + " FILE(S), " + ni + " SHORTCUT(S)" + (ProcRunning(LogoExe(kind)) ? "  -  RESTART IT TO SEE IT" : ""), C_ON);
        else RSet.Say(nm + " LOGO MATCHED NOTHING - BROWSE AGAIN TO PICK THE TARGET", C_ACC);
    }
    static bool ProcRunning(string exe) { try { return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)).Length > 0; } catch { return false; } }
    public static void LogoRestore()
    {
        int nt = LogoRevert(0), ni = IconRevert();
        RSet.Logo = ""; RSet.LogoS = "";
        RSet.Pick["logo"] = 1; RSet.Pick["logos"] = 1; RSet.FlashAt["logo"] = Clock.Tick; RSet.FlashAt["logos"] = Clock.Tick;
        Ini.Write(Paths.IniFile, "rset", "logofile", ""); Ini.Write(Paths.IniFile, "rset", "logofileS", ""); Ini.Write(Paths.IniFile, "rset", "logo", 1L); Ini.Write(Paths.IniFile, "rset", "logos", 1L);
        LogoLoad(0); LogoScan(1, true);
        if (nt > 0 || ni > 0) RSet.Say("DEFAULT LOGOS RESTORED  -  " + nt + " FILE(S), " + ni + " SHORTCUT(S)" + (ProcRunning("RobloxPlayerBeta.exe") ? "  -  RESTART ROBLOX" : ""), C_ON);
        else RSet.Say("NOTHING TO RESTORE - ALREADY ON THE ROBLOX DEFAULTS", AMBER);
    }
    public static void SetLogo(int kind, string path)
    {
        if (path == "" || !File.Exists(path)) { RSet.Say("LOGO FILE NOT FOUND", C_OFF); return; }
        string key = kind == 2 ? "logos" : "logo";
        if (kind == 2) RSet.LogoS = path; else RSet.Logo = path;
        LogoLoad(kind);
        RSet.Pick[key] = 2; RSet.FlashAt[key] = Clock.Tick;
        Ini.Write(Paths.IniFile, "rset", kind == 2 ? "logofileS" : "logofile", path); Ini.Write(Paths.IniFile, "rset", key, 2L);
        int n = LogoCount(kind, true) + LnkScan(kind, true).Count;
        if (n > 0) { RSet.Say(LogoName(kind) + " LOGO READY - " + Path.GetFileName(path) + "  -  " + n + " TARGET(S)  -  PRESS APPLY", C_ON); return; }
        RSet.Say("NO " + LogoName(kind) + " FILE MATCHED - PICK THE FILE TO REPLACE", AMBER);
        _ = PickLogoTargetAsync(kind);
    }
    public static void SetLogoTarget(int kind, string path)
    {
        if (path == "" || !File.Exists(path)) { RSet.Say("FILE NOT FOUND", C_OFF); return; }
        string mk = Path.DirectorySeparatorChar + "content" + Path.DirectorySeparatorChar + "textures" + Path.DirectorySeparatorChar;
        int ipos = path.LastIndexOf(mk, StringComparison.OrdinalIgnoreCase);
        if (ipos < 0) { RSet.Say("THAT FILE IS NOT INSIDE THE ROBLOX TEXTURES FOLDER", C_OFF); return; }
        string sub = path[(ipos + mk.Length)..];
        if (kind == 2) LogoSubS = sub; else LogoSub = sub;
        Ini.Write(Paths.IniFile, "rset", kind == 2 ? "logosubS" : "logosub", sub);
        int n = LogoCount(kind, true);
        RSet.Say(LogoName(kind) + " TARGET SET - " + sub + "  \u00B7  " + n + " FILE(S)  -  PRESS APPLY", n > 0 ? C_ON : C_ACC);
    }
    public static void SetFont(string path)
    {
        if (path == "" || !File.Exists(path)) { RSet.Say("FONT FILE NOT FOUND", C_OFF); return; }
        RSet.Font = path; RSet.Pick["font"] = 2; RSet.FlashAt["font"] = Clock.Tick;
        Ini.Write(Paths.IniFile, "rset", "fontfile", path); Ini.Write(Paths.IniFile, "rset", "font", 2L);
        RSet.Say("FONT READY - " + Path.GetFileName(path), C_ON);
    }
    static async Task<string?> PickAsync(HubSurface hub, string title, FilePickerFileType type, string? start = null)
    {
        if (_picking) return null;
        _picking = true;
        try
        {
            var top = TopLevel.GetTopLevel(hub);
            if (top?.StorageProvider is not { CanOpen: true } sp) return null;
            var opt = new FilePickerOpenOptions { Title = title, AllowMultiple = false, FileTypeFilter = new[] { type, FilePickerFileTypes.All } };
            if (start is not null) { try { opt.SuggestedStartLocation = await sp.TryGetFolderFromPathAsync(start); } catch { } }
            var files = await sp.OpenFilePickerAsync(opt);
            return files.Count > 0 ? files[0].TryGetLocalPath() : null;
        }
        catch { return null; }
        finally { _picking = false; hub.Tim(Pace.TICK_A); }
    }
    public static async Task PickFontAsync(HubSurface hub)
    {
        var sel = await PickAsync(hub, "Pick the font", new FilePickerFileType("Fonts") { Patterns = new[] { "*.ttf", "*.otf" } });
        if (sel is null) return;
        SetFont(sel);
    }
    public static async Task PickLogoAsync(HubSurface hub, int kind)
    {
        var sel = await PickAsync(hub, "Pick the " + LogoName(kind).ToLowerInvariant() + " logo image", new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" } });
        if (sel is null) return;
        SetLogo(kind, sel);
    }
    static async Task PickLogoTargetAsync(int kind)
    {
        var hub = HubSurface.Live; if (hub is null) return;
        string rt = TexRoot(kind);
        if (rt == "") { RSet.Say("NO ROBLOX INSTALL FOUND", C_OFF); return; }
        var sel = await PickAsync(hub, "Pick the texture to replace", new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } }, rt);
        if (sel is null) return;
        SetLogoTarget(kind, sel);
    }

    // ---- previews ----
    public static void LogoLoad(int kind)
    {
        if (kind == 0) { LogoLoad(1); LogoLoad(2); return; }
        _logoBmp[kind - 1]?.Dispose(); _logoBmp[kind - 1] = null;
        string src = LogoOf(kind);
        if (src == "" || !File.Exists(src)) return;
        try { _logoBmp[kind - 1] = new Bitmap(src); } catch { }
    }
    static void LogoDefLoad(int kind)
    {
        string pth = AppExe(kind);
        if (pth == _logoDefP[kind - 1]) return;
        _logoDefP[kind - 1] = pth;
        _logoDef[kind - 1]?.Dispose(); _logoDef[kind - 1] = null;
        if (pth == "") return;
        if (Os.IsWin) { try { _logoDef[kind - 1] = LoginItems.Lgi.WinIcon(pth); } catch { } }
    }
    /// <summary>RSetLogoTile: the staged image, or the app's own icon dimmed when nothing is staged, in a small hatched frame.</summary>
    public static void Tile(double tx, double ty, double sz, double fb, uint acc, long now, int kind)
    {
        var bmp = _logoBmp[kind - 1];
        bool own = bmp is not null;
        if (!own) { LogoDefLoad(kind); bmp = _logoDef[kind - 1]; }
        FillRR(tx, ty, sz, sz, 5, SBrush(FA(0xFF0B0D16, fb)));
        int sv = PushG(); ClipRR(tx, ty, sz, sz, 5);
        var pn = Pen(FA(Alpha(0xFFFFFF, 10), fb), 1);
        for (double k = -sz; k < sz; k += 7) Line(tx + k, ty + sz, tx + k + sz, ty, pn);
        if (bmp is not null && bmp.PixelSize.Width > 0 && bmp.PixelSize.Height > 0)
        {
            double bw = bmp.PixelSize.Width, bh = bmp.PixelSize.Height, fit = Math.Min((sz - 6) / bw, (sz - 6) / bh), iw = bw * fit, ih = bh * fit;
            DrawImage(bmp, new Rect(tx + (sz - iw) / 2, ty + (sz - ih) / 2, iw, ih), new Rect(0, 0, bw, bh), fb * (own ? 1.0 : 0.6));
        }
        else Line(tx + sz / 2 - 4, ty + sz / 2, tx + sz / 2 + 4, ty + sz / 2, Pen(FA(Alpha(0xFFC7CBE0, 60), fb), 1.2));
        Pop(sv);
        int n = LogoCount(kind) + LnkScan(kind).Count;
        StrokeRR(tx, ty, sz, sz, 5, Pen(FA(Alpha(own ? (n > 0 ? acc : AMBER) : 0xFF6E7590, own ? 150 : 90), fb), 1.2));
    }
    /// <summary>The logo rows' hint: the staged file and where it will land.</summary>
    public static string Hint(int kind, string dflt)
    {
        string srcH = LogoOf(kind);
        if (srcH == "") return dflt;
        int nlg2 = LogoCount(kind), nsc2 = LnkScan(kind).Count;
        string subH = LogoSubOf(kind);
        string dest = (nlg2 > 0 ? (subH != "" ? subH : nlg2 + " file(s)") : "") + (nlg2 > 0 && nsc2 > 0 ? "  \u00B7  " : "") + (nsc2 > 0 ? nsc2 + " shortcut(s)" : "");
        return dest != "" ? Path.GetFileName(srcH) + "  ->  " + dest : Path.GetFileName(srcH) + "  \u00B7  NOTHING MATCHED - BROWSE AGAIN TO PICK THE TARGET";
    }

    // ---- the live framerate cap ----
    static Dictionary<string, string>? FpsSet() { var o = RSet.By("fps"); if (o?.F is null) return null; int i = RSet.Idx(o); return i <= 1 || i > o.F.Count ? null : o.F[i - 1]; }
    public static string FpsTarget() { var m = FpsSet(); return m is not null && m.TryGetValue("DFIntTaskSchedulerTargetFps", out var v) ? v : ""; }
    static string FlagPeek(Dictionary<string, long> idx, string name)
    {
        string bare = Ffm.BareOf(name);
        if (!idx.TryGetValue(bare, out var vpr) || vpr == 0) return "";
        long vp = MemIO.ReadU64(vpr + 0xC0);
        if (!FFlags.Ptr(vp)) return "";
        return MemIO.ReadChk(vp, FfmEngine.WidthOf(name))?.ToString() ?? "";
    }
    static void FpsProbe(Dictionary<string, long> idx, string tag)
    {
        var sb = new StringBuilder();
        foreach (var nm in new[] { "DFIntTaskSchedulerTargetFps", "FFlagTaskSchedulerLimitTargetFpsTo2402", "FFlagGameBasicSettingsFramerateCap5" }) { string v = FlagPeek(idx, nm); sb.Append(sb.Length > 0 ? "  " : "").Append(Ffm.BareOf(nm)).Append('=').Append(v == "" ? "?" : v); }
        FfmViews.Log("FpsProbe", tag, sb.ToString());
    }
    static bool Attached() => FfmEngine.Pid != 0 && MemIO.HProcess != IntPtr.Zero && MemIO.IsAlive();
    static bool WriteOne(Dictionary<string, long> idx, string nm, string val)
    {
        string bare = Ffm.BareOf(nm);
        if (!idx.TryGetValue(bare, out var vpr) || vpr == 0) return false;
        long vp = MemIO.ReadU64(vpr + 0xC0);
        int w = FfmEngine.WidthOf(nm);
        if (!FFlags.Ptr(vp) || !FFlags.SetAtW(vp, val, w)) return false;
        var back = MemIO.ReadChk(vp, w);
        return back is not null && back == FFlags.Coerce(val);
    }
    /// <summary>RSetFpsLive(): 1 live, 2 / 3 above 240 (saved, needs a restart; 2 with the clamp flag written too), -1 the write did not take, -2 no client to write into, 0 nothing to force.</summary>
    public static int FpsLive()
    {
        string tgt = FpsTarget();
        if (tgt == "") return 0;
        if (!Os.IsWin || !Attached() || FfmEngine.Busy) return -2;
        FfmEngine.Busy = true;
        try
        {
            var hdr = FlagSingleton.Header(!FfmPanel.UseSingleton);
            if (hdr is null || hdr.List == 0 || hdr.Mask == 0) return -1;
            var idx = FlagSingleton.Index(hdr);
            FpsProbe(idx, "before");
            FpsSnap(idx, FfmEngine.Pid);
            bool okT = false, okClamp = false;
            foreach (var (nm, val) in FpsSet()!)
            {
                if (!WriteOne(idx, nm, val)) continue;
                if (!Attached()) return -1;
                FfmViews.Log("Inject", nm, "live -> " + val);
                if (nm == "DFIntTaskSchedulerTargetFps") okT = true; else if (nm == "FFlagTaskSchedulerLimitTargetFpsTo2402") okClamp = true;
            }
            FpsProbe(idx, "after");
            if (!okT) return -1;
            if (long.TryParse(tgt, out var t) && t > 240) return okClamp ? 2 : 3;
            return 1;
        }
        catch { return -1; }
        finally { FfmEngine.Busy = false; }
    }
    static void FpsSnap(Dictionary<string, long> idx, int pid)
    {
        if (FpsWasP == pid && FpsWas.Count > 0) return;
        FpsWas.Clear(); FpsWasP = 0;
        foreach (var nm in FpsSet()!.Keys) { string v = FlagPeek(idx, nm); if (v != "") FpsWas[nm] = v; }
        if (FpsWas.Count > 0) FpsWasP = pid;
    }
    public static int FpsLiveRevert()
    {
        if (FpsWas.Count == 0) return 0;
        if (!Os.IsWin || !Attached() || FpsWasP != FfmEngine.Pid) { FpsWas.Clear(); FpsWasP = 0; return 0; }
        if (FfmEngine.Busy) return -2;
        FfmEngine.Busy = true;
        try
        {
            var hdr = FlagSingleton.Header(!FfmPanel.UseSingleton);
            if (hdr is null || hdr.List == 0 || hdr.Mask == 0) return -1;
            var idx = FlagSingleton.Index(hdr);
            FpsProbe(idx, "revert-before");
            int n = 0;
            foreach (var (nm, val) in FpsWas.ToList())
            {
                if (!WriteOne(idx, nm, val)) continue;
                if (!Attached()) return -1;
                FfmViews.Log("Revert", nm, "live -> " + val);
                n++;
            }
            FpsProbe(idx, "revert-after");
            if (n == 0) return -1;
            FpsWas.Clear(); FpsWasP = 0;
            return n;
        }
        catch { return -1; }
        finally { FfmEngine.Busy = false; }
    }
}
