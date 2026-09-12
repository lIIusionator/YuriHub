namespace Yuri.Core;

/// <summary>
/// DATA LAYOUT. Everything YURI writes lives under one folder, split by purpose:
///   YURI\config\zeal.ini            settings
///   YURI\flags\zeal_flags.json      staged fast flags
///   YURI\avatars\                   custom + user profile pictures
///   YURI\scripts\                   script hub library
///   YURI\exports\                   default target for exports
/// The .ahk kept it beside the script. The .exe keeps it beside itself; a macOS
/// .app bundle keeps it beside the bundle (the bundle's own folder is not a
/// place to write), so `YURI.app` and `YURI/` sit together the way
/// `YURI.exe` and `YURI/` do.
/// </summary>
public static class Paths
{
    public static readonly string ScriptDir = ResolveScriptDir();        // A_ScriptDir
    public static readonly string Root   = ResolveRoot();
    public static readonly string Cfg    = Path.Combine(Root, "config");
    public static readonly string Flags  = Path.Combine(Root, "flags");
    public static readonly string Av     = Path.Combine(Root, "avatars");
    public static readonly string Scr    = Path.Combine(Root, "scripts");
    public static readonly string Exp    = Path.Combine(Root, "exports");
    public static readonly string Cur    = Path.Combine(Root, "cursor");
    public static readonly string Rsb    = Path.Combine(Root, "settings");
    public static readonly string DevDir = Path.Combine(Root, "devopt");
    public static readonly string Comm   = Path.Combine(Root, "community");   // the offline copy of the community sets
    public static readonly string Mods   = Path.Combine(Root, "mods");
    public static readonly string IniFile = Path.Combine(Cfg, "zeal.ini");
    public static readonly string PerfLog = Path.Combine(Root, "perf.log");

    public const string KeysUrl = "https://raw.githubusercontent.com/lIIusionator/YuriHub/main/keys.json";   // YURI_KEYS_URL

    static string ResolveScriptDir()
    {
        var exeDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        if (OperatingSystem.IsMacOS())
        {
            // .../YURI.app/Contents/MacOS  ->  the folder holding YURI.app
            var macos = exeDir;
            var contents = Path.GetDirectoryName(macos);
            var bundle = contents is null ? null : Path.GetDirectoryName(contents);
            if (bundle is not null && bundle.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(macos) == "MacOS" && Path.GetFileName(contents) == "Contents")
                return Path.GetDirectoryName(bundle) ?? bundle;
        }
        return exeDir;
    }

    /// <summary>
    /// `YURI` beside the program, as the .ahk had it. On macOS and Linux an
    /// unbundled build's executable is itself named `YURI`, which leaves no
    /// room for a folder of that name beside it; the data then goes to the
    /// user's application-data folder instead (a .app bundle is the shipped
    /// case, and its data sits beside the bundle).
    /// </summary>
    static string ResolveRoot()
    {
        var beside = Path.Combine(ScriptDir, "YURI");
        // Gatekeeper may run a downloaded, unsigned .app from a read-only
        // translocated copy; Program Files may not be writable. The data
        // folder then goes where the user can write.
        bool translocated = OperatingSystem.IsMacOS() && ScriptDir.Contains("/AppTranslocation/", StringComparison.Ordinal);
        if (!translocated && !File.Exists(beside) && Writable(beside)) return beside;
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string fallback = OperatingSystem.IsMacOS() ? Path.Combine(home, "Library", "Application Support", "YURI")
                        : OperatingSystem.IsWindows() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YURI")
                        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YURI");
        return fallback;
    }
    static bool Writable(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, ".write-probe");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }

    /// <summary>YuriInitDirs(): create the tree, move loose legacy files in from the old flat layout.</summary>
    public static void InitDirs()
    {
        foreach (var d in new[] { Root, Cfg, Flags, Av, Scr, Exp, Cur, Path.Combine(Cur, "backup"),
                                  Rsb, Path.Combine(Rsb, "fonts"), Comm })
            try { Directory.CreateDirectory(d); } catch { }

        MoveIn(Path.Combine(ScriptDir, "zeal.ini"),            Path.Combine(Cfg, "zeal.ini"));
        MoveIn(Path.Combine(ScriptDir, "zeal_flags.json"),     Path.Combine(Flags, "zeal_flags.json"));
        MoveIn(Path.Combine(ScriptDir, "selfie_zz_custom.png"), Path.Combine(Av, "selfie_zz_custom.png"));

        var legacyScripts = Path.Combine(ScriptDir, "yuri_scripts");
        if (Directory.Exists(legacyScripts) && !Directory.Exists(Path.Combine(Scr, ".moved")))
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(legacyScripts))
                {
                    var dst = Path.Combine(Scr, Path.GetFileName(f));
                    if (!File.Exists(dst)) File.Move(f, dst);
                }
                Directory.Delete(legacyScripts, false);
            }
            catch { }
        }
        foreach (var pat in new[] { "avatar.jpg", "avatar.jpeg", "avatar.png", "selfie*.png", "selfie*.jpg", "selfie*.jpeg" })
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(ScriptDir, pat))
                {
                    var dst = Path.Combine(Av, Path.GetFileName(f));
                    if (!File.Exists(dst)) File.Move(f, dst);
                }
            }
            catch { }
        }
    }

    static void MoveIn(string src, string dst)
    {
        if (File.Exists(src) && !File.Exists(dst))
            try { File.Move(src, dst); } catch { }
    }
}
