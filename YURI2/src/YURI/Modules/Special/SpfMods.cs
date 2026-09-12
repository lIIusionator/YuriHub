using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Yuri.Core;
using Yuri.Platform;
using Yuri.Shell.Hub;

namespace Yuri.Modules.Special;

/// <summary>
/// The content mods. Each kind names a sub-folder of the client's content
/// tree and the files it replaces there; the replacements come from
/// Bloxstrap's mod resources (the death sound from a file you pick) into
/// YURI\mods\kind. APPLY copies them into every install found — the
/// original saved beside it as .yuri-orig — and REVERT moves the originals
/// back (a strap's Modifications copy is simply deleted). A 15 s watch
/// re-applies after a client update replaced the files.
/// </summary>
public static class SpfMods
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_ACC = 0xFFFB7185;
    public sealed record ModFile(string N, string Src);
    public sealed record ModDef(string Sub, string Dir, ModFile[] Files);
    static readonly Dictionary<string, ModDef> Defs = new()
    {
        ["oldsnd"] = new(Path.Combine("content", "sounds"), "oldsnd", new[]
        {
            new ModFile("action_footsteps_plastic.mp3", "OldWalk.mp3"), new ModFile("action_jump.mp3", "OldJump.mp3"), new ModFile("action_get_up.mp3", "OldGetUp.mp3"),
            new ModFile("action_falling.mp3", "Empty.mp3"), new ModFile("action_jump_land.mp3", "Empty.mp3"), new ModFile("action_swim.mp3", "Empty.mp3"), new ModFile("impact_water.mp3", "Empty.mp3"),
        }),
        ["avbg"] = new(Path.Combine("ExtraContent", "places"), "avbg", new[] { new ModFile("Mobile.rbxl", "OldAvatarBackground.rbxl") }),
        ["death"] = new(Path.Combine("content", "sounds"), "death", new[] { new ModFile("ouch.ogg", "ouch.ogg") }),
        ["emoji"] = new(Path.Combine("content", "fonts"), "emoji", new[] { new ModFile("TwemojiMozilla.ttf", "emoji.ttf") }),
    };
    const string Base = "https://raw.githubusercontent.com/bloxstraplabs/bloxstrap/main/Bloxstrap/Resources/Mods/";
    static readonly Dictionary<string, int> ModN = new(); static readonly Dictionary<string, long> ModAt = new(), Fetching = new();
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    static DispatcherTimer? _auto;
    public static bool NoNetwork;
    static string ModsDir => Paths.Mods;

    public static ModDef? Def(string kind) => Defs.TryGetValue(kind, out var d) ? d : null;
    static List<(string src, string url)> Urls(string kind) => kind switch
    {
        "oldsnd" => new() { ("OldWalk.mp3", Base + "Sounds/OldWalk.mp3"), ("OldJump.mp3", Base + "Sounds/OldJump.mp3"), ("OldGetUp.mp3", Base + "Sounds/OldGetUp.mp3"), ("Empty.mp3", Base + "Sounds/Empty.mp3") },
        "avbg" => new() { ("OldAvatarBackground.rbxl", Base + "OldAvatarBackground.rbxl") },
        "emoji" => new() { ("emoji.ttf", "https://github.com/bloxstraplabs/rbxcustom-fontemojis/releases/download/my-phone-is-78-percent/" + Spf.Emoji[Math.Clamp(Spf.EmojiK, 1, Spf.Emoji.Length) - 1].f) },
        _ => new(),
    };
    public static bool Have(string kind)
    {
        var d = Def(kind);
        if (d is null) return false;
        foreach (var f in d.Files) if (!File.Exists(Path.Combine(ModsDir, d.Dir, f.Src))) return false;
        return true;
    }
    /// <summary>SPFModTargets(sub): every install's folder for the sub-path, and the straps' Modifications copies (mk).</summary>
    /// <summary>Set by the test harness: stands in for the install scan.</summary>
    public static Func<string, List<(string d, bool mk)>>? TargetsOverride;
    public static List<(string d, bool mk)> Targets(string sub)
    {
        if (TargetsOverride is not null) return TargetsOverride(sub);
        var res = new List<(string, bool)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string d, bool mk) { if (seen.Add(d)) res.Add((d, mk)); }
        try
        {
            if (Os.IsWin)
            {
                string lad = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "";
                if (lad == "") return res;
                string vers = Path.Combine(lad, "Roblox", "Versions");
                if (Directory.Exists(vers)) foreach (var v in Directory.GetDirectories(vers, "version-*")) if (File.Exists(Path.Combine(v, "RobloxPlayerBeta.exe"))) Add(Path.Combine(v, sub), false);
                foreach (var lc in new[] { "Bloxstrap", "Fishstrap", "Voidstrap", "Bubblestrap" })
                {
                    string root = Path.Combine(lad, lc);
                    if (!Directory.Exists(root)) continue;
                    string lv = Path.Combine(root, "Versions");
                    if (Directory.Exists(lv)) foreach (var v in Directory.GetDirectories(lv, "version-*")) if (File.Exists(Path.Combine(v, "RobloxPlayerBeta.exe"))) Add(Path.Combine(v, sub), false);
                    Add(Path.Combine(root, "Modifications", sub), true);
                }
                foreach (var p in Roblox.Processes())
                {
                    string exe = ""; try { exe = p.MainModule?.FileName ?? ""; } catch { }
                    if (exe == "") continue;
                    string? vdir = Path.GetDirectoryName(exe);
                    if (vdir is not null) Add(Path.Combine(vdir, sub), false);
                }
            }
            else if (Os.IsMac)
            {
                foreach (var app in new[] { "/Applications/Roblox.app", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications", "Roblox.app") })
                    if (Directory.Exists(app)) Add(Path.Combine(app, "Contents", "Resources", sub), false);
            }
        }
        catch { }
        return res;
    }
    public static string Name(string kind) => kind == "oldsnd" ? "old character sounds" : kind == "avbg" ? "old avatar background" : kind == "death" ? "death sound" : "emoji font";
    public static bool On(string kind) => kind == "oldsnd" ? Spf.Snd : kind == "avbg" ? Spf.AvBg : kind == "death" ? Spf.Death : Spf.EmojiOn;

    public static int Apply(string kind, bool say = true)
    {
        var d = Def(kind);
        if (d is null || !Have(kind)) return 0;
        int n = 0;
        foreach (var (td, mk) in Targets(d.Sub))
        {
            if (mk && !Directory.Exists(td)) { try { Directory.CreateDirectory(td); } catch { } }
            if (!Directory.Exists(td)) continue;
            foreach (var f in d.Files)
            {
                string dst = Path.Combine(td, f.N);
                if (!mk && File.Exists(dst) && !File.Exists(dst + ".yuri-orig")) { try { File.Copy(dst, dst + ".yuri-orig", false); } catch { } }
                try { File.Copy(Path.Combine(ModsDir, d.Dir, f.Src), dst, true); n++; } catch { }
            }
        }
        ModN[kind] = n; ModAt[kind] = Clock.Tick;
        if (say) Spf.Say(n > 0 ? Name(kind).ToUpperInvariant() + " APPLIED TO " + n + " FILE" + (n == 1 ? "" : "S") + " ACROSS YOUR INSTALLS" : Name(kind).ToUpperInvariant() + " - NO ROBLOX INSTALL FOUND TO WRITE INTO", n > 0 ? C_ON : AMBER);
        Poke();
        return n;
    }
    public static int Revert(string kind, bool say = true)
    {
        var d = Def(kind);
        if (d is null) return 0;
        int n = 0;
        foreach (var (td, mk) in Targets(d.Sub))
            foreach (var f in d.Files)
            {
                string dst = Path.Combine(td, f.N);
                if (mk) { if (File.Exists(dst)) { try { File.Delete(dst); n++; } catch { } } }
                else if (File.Exists(dst + ".yuri-orig")) { try { File.Move(dst + ".yuri-orig", dst, true); n++; } catch { } }
            }
        ModN[kind] = 0;
        if (say) Spf.Say(Name(kind).ToUpperInvariant() + " OFF - " + (n > 0 ? n + " ORIGINAL" + (n == 1 ? "" : "S") + " PUT BACK" : "NOTHING TO PUT BACK"), 0xFFC7CBE0);
        Poke();
        return n;
    }
    /// <summary>SPFModFetch(kind): the replacement files, downloaded to a .part first.</summary>
    public static void Fetch(string kind)
    {
        var d = Def(kind);
        if (d is null || Fetching.ContainsKey(kind)) return;
        string dir = Path.Combine(ModsDir, d.Dir);
        try { Directory.CreateDirectory(dir); } catch { }
        var list = Urls(kind);
        if (list.Count == 0) { Spf.Say("NOTHING TO FETCH FOR " + Name(kind).ToUpperInvariant(), C_ACC); return; }
        if (NoNetwork) { Spf.Say("COULD NOT FETCH " + Name(kind).ToUpperInvariant() + " - NO NETWORK", C_ACC); return; }
        Fetching[kind] = Clock.Tick;
        Spf.Say("FETCHING " + Name(kind).ToUpperInvariant() + "...", AMBER);
        _ = Task.Run(async () =>
        {
            string err = "";
            foreach (var (src, url) in list)
            {
                string dst = Path.Combine(dir, src);
                try
                {
                    var bytes = await Http.GetByteArrayAsync(url);
                    if (bytes.Length == 0) { err = "empty download for " + src; break; }
                    await File.WriteAllBytesAsync(dst + ".part", bytes);
                    File.Move(dst + ".part", dst, true);
                }
                catch (Exception e) { err = "download failed for " + src + " - " + e.Message; break; }
            }
            Dispatcher.UIThread.Post(() =>
            {
                Fetching.Remove(kind);
                if (err == "") { if (On(kind)) Apply(kind, true); }
                else Spf.Say("COULD NOT FETCH " + Name(kind).ToUpperInvariant() + " - " + err.ToUpperInvariant(), C_ACC);
                Poke();
            });
        });
    }
    /// <summary>SPFModArm(kind): apply what is on disk, fetch what is not, and start the watch.</summary>
    public static void Arm(string kind)
    {
        if (Have(kind)) Apply(kind, true);
        else if (kind == "death") Spf.Say("PICK AN .OGG WITH THE CHIP - THE DEATH SOUND IS YOURS TO CHOOSE", AMBER);
        else Fetch(kind);
        AutoArm();
    }
    public static void AutoArm()
    {
        bool any = Spf.Snd || Spf.AvBg || Spf.Death || Spf.EmojiOn;
        _auto ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15000) };
        _auto.Tick -= OnAuto; _auto.Tick += OnAuto;
        if (any) _auto.Start(); else _auto.Stop();
    }
    static void OnAuto(object? s, EventArgs e) { try { AutoTick(); } catch { } }
    /// <summary>SPFModAutoTick: a target file without our .yuri-orig beside it is a fresh client build — re-apply.</summary>
    public static void AutoTick()
    {
        foreach (var kind in new[] { "oldsnd", "avbg", "death", "emoji" })
        {
            if (!On(kind) || !Have(kind)) continue;
            var d = Def(kind)!;
            bool fresh = false;
            foreach (var (td, mk) in Targets(d.Sub))
            {
                if (mk || !Directory.Exists(td)) continue;
                foreach (var f in d.Files) if (File.Exists(Path.Combine(td, f.N)) && !File.Exists(Path.Combine(td, f.N) + ".yuri-orig")) fresh = true;
            }
            if (fresh) { Apply(kind, false); Spf.Say(Name(kind).ToUpperInvariant() + " RE-APPLIED TO A NEW ROBLOX BUILD", C_ON); }
        }
    }
    public static string HintShort(string kind)
    {
        if (Fetching.ContainsKey(kind)) return "fetching...";
        if (!On(kind)) return "";
        if (!Have(kind)) return kind == "death" ? "no file picked yet" : "not fetched yet";
        int n = ModN.TryGetValue(kind, out var v) ? v : 0;
        return n > 0 ? n + " file" + (n == 1 ? "" : "s") + " in place  \u00B7  watching for updates" : "no install found yet";
    }
    /// <summary>SPFDeathPick: any .ogg you choose becomes the oof.</summary>
    public static async Task DeathPickAsync(HubSurface hub)
    {
        string f = "";
        try
        {
            var top = TopLevel.GetTopLevel(hub);
            if (top?.StorageProvider is { CanOpen: true } sp)
            {
                var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Pick the death sound", AllowMultiple = false, FileTypeFilter = new[] { new FilePickerFileType("Sound (*.ogg)") { Patterns = new[] { "*.ogg" } } } });
                if (files.Count > 0) f = files[0].TryGetLocalPath() ?? "";
            }
        }
        catch { }
        if (f == "") return;
        SetDeathFile(f);
        hub.Tim(Pace.TICK_A);
    }
    public static void SetDeathFile(string f)
    {
        var d = Def("death")!;
        string dir = Path.Combine(ModsDir, d.Dir);
        try { Directory.CreateDirectory(dir); File.Copy(f, Path.Combine(dir, "ouch.ogg"), true); } catch { }
        if (!File.Exists(Path.Combine(dir, "ouch.ogg"))) { Spf.Say("COULD NOT COPY THAT FILE INTO YURI\\mods", C_ACC); return; }
        Spf.Say("DEATH SOUND: " + Path.GetFileName(f).ToUpperInvariant(), C_ON);
        if (Spf.Death) Apply("death", true);
    }
    /// <summary>The EMOJI chip: the next set; the fetched font goes so the new one is fetched.</summary>
    public static void EmojiNext()
    {
        Spf.EmojiK = Spf.EmojiK % Spf.Emoji.Length + 1; Spf.Save("emojik", Spf.EmojiK);
        try { File.Delete(Path.Combine(ModsDir, "emoji", "emoji.ttf")); } catch { }
        if (Spf.EmojiOn) Fetch("emoji");
        else Spf.Say("EMOJI FONT: " + Spf.EmojiLabel() + " - TURN THE ROW ON TO APPLY IT", AMBER);
    }
    /// <summary>The engine hook for rows 14-17: on arms, off reverts.</summary>
    public static void Toggle(int row, bool on)
    {
        string kind = row == 14 ? "oldsnd" : row == 15 ? "avbg" : row == 16 ? "death" : "emoji";
        if (on) Arm(kind); else { Revert(kind, true); AutoArm(); }
    }
    public static void Register()
    {
        Spf.ModHave = Have;
        Spf.ModHintShort = HintShort;
        Spf.ModToggle = Toggle;
        AutoArm();
    }
    static void Poke() => HubSurface.Live?.Tim(Pace.TICK_A);
}
