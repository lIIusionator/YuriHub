using Yuri.Core;
using Yuri.Gfx;
using Yuri.Platform;
using Yuri.Shell.Hub;

namespace Yuri.Modules.Forsaken;

/// <summary>
/// The three bindable keys (block / solve / markers) as the .ahk kept them in
/// [keys]: AutoHotkey key names, so a zeal.ini written by YURI.ahk still
/// reads. KeyLabel shortens them for the chips. Rebinding listens for the
/// next key or side / middle mouse button; ESC cancels; a key that would
/// clash with the other system is refused with the clash card's message.
/// </summary>
public static class Keys
{
    public static string BindKey = "", PuzKey = "", MkKey = "";
    public static bool RebindOn; public static int RebindTgt; public static long RebindAt, BindFlashAt, PuzFlashAt, MkFlashAt;
    static readonly Dictionary<string, string> Labels = new() { ["XButton1"] = "XB1", ["XButton2"] = "XB2", ["MButton"] = "M3", ["Space"] = "SPC", ["Tab"] = "TAB", ["CapsLock"] = "CAPS", ["Backspace"] = "BKSP", ["Enter"] = "ENT", ["Delete"] = "DEL", ["Insert"] = "INS", ["Home"] = "HOME", ["End"] = "END", ["PgUp"] = "PGUP", ["PgDn"] = "PGDN", ["Left"] = "LEFT", ["Right"] = "RGT", ["Up"] = "UP", ["Down"] = "DOWN", ["LShift"] = "LSFT", ["RShift"] = "RSFT", ["LControl"] = "LCTL", ["RControl"] = "RCTL", ["LAlt"] = "LALT", ["RAlt"] = "RALT", ["LWin"] = "LWIN", ["RWin"] = "RWIN", ["PrintScreen"] = "PRT", ["ScrollLock"] = "SCRL", ["Pause"] = "PAUS", ["AppsKey"] = "MENU", ["NumLock"] = "NLCK" };
    static readonly Dictionary<int, string> VkNames = new() { [8] = "Backspace", [9] = "Tab", [13] = "Enter", [19] = "Pause", [20] = "CapsLock", [27] = "Escape", [32] = "Space", [33] = "PgUp", [34] = "PgDn", [35] = "End", [36] = "Home", [37] = "Left", [38] = "Up", [39] = "Right", [40] = "Down", [44] = "PrintScreen", [45] = "Insert", [46] = "Delete", [91] = "LWin", [92] = "RWin", [93] = "AppsKey", [144] = "NumLock", [145] = "ScrollLock", [160] = "LShift", [161] = "RShift", [162] = "LControl", [163] = "RControl", [164] = "LAlt", [165] = "RAlt", [186] = ";", [187] = "=", [188] = ",", [189] = "-", [190] = ".", [191] = "/", [192] = "`", [219] = "[", [220] = "\\", [221] = "]", [222] = "'" };
    public static string Label(string k)
    {
        if (k == "") return "???";
        if (Labels.TryGetValue(k, out var l)) return l;
        return k.Replace("Numpad", "N").ToUpperInvariant();
    }
    public static string VkName(int vk)
    {
        if (VkNames.TryGetValue(vk, out var n)) return n;
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
        if (vk >= 0x41 && vk <= 0x5A) return ((char)(vk + 32)).ToString();
        if (vk >= 0x60 && vk <= 0x69) return "Numpad" + (vk - 0x60);
        if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);
        if (vk == 0x6A) return "NumpadMult"; if (vk == 0x6B) return "NumpadAdd"; if (vk == 0x6D) return "NumpadSub"; if (vk == 0x6E) return "NumpadDot"; if (vk == 0x6F) return "NumpadDiv";
        return "vk" + vk.ToString("X2");
    }
    public static int VkOf(string name)
    {
        if (name == "") return 0;
        foreach (var (vk, n) in VkNames) if (n.Equals(name, StringComparison.OrdinalIgnoreCase)) return vk;
        if (name.Length == 1) { char c = char.ToUpperInvariant(name[0]); if (c >= '0' && c <= '9' || c >= 'A' && c <= 'Z') return c; }
        if (name.StartsWith("Numpad", StringComparison.OrdinalIgnoreCase) && int.TryParse(name[6..], out var np)) return 0x60 + np;
        if (name.StartsWith("F", StringComparison.OrdinalIgnoreCase) && int.TryParse(name[1..], out var fn) && fn >= 1 && fn <= 24) return 0x6F + fn;
        if (name.StartsWith("vk", StringComparison.OrdinalIgnoreCase) && int.TryParse(name[2..], System.Globalization.NumberStyles.HexNumber, null, out var raw)) return raw;
        return 0;
    }
    public static void Load() { BindKey = Ini.Read(Paths.IniFile, "keys", "block", ""); PuzKey = Ini.Read(Paths.IniFile, "keys", "solve", ""); MkKey = Ini.Read(Paths.IniFile, "keys", "markers", ""); }
    public static void Save() { Ini.Write(Paths.IniFile, "keys", "block", BindKey); Ini.Write(Paths.IniFile, "keys", "solve", PuzKey); Ini.Write(Paths.IniFile, "keys", "markers", MkKey); }
    public static double ChipW(string label) => Math.Max(38, Fonts.MeasureW(label, Fonts.fBadge) + 16);
    public static void RebindClick(int tgt) { if (RebindOn) Cancel(); else Start(tgt); }
    public static void Start(int tgt)
    {
        RebindOn = true; RebindAt = Clock.Tick; RebindTgt = tgt;
        if (Os.IsWin && OperatingSystem.IsWindows()) WinHooks.Ensure();
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    public static void Cancel() { if (!RebindOn) return; RebindOn = false; RebindTgt = 0; HubSurface.Live?.Tim(Pace.TICK_A); }
    /// <summary>A key or mouse button arrived while a chip is listening. Returns true when it was consumed.</summary>
    public static bool Feed(string keyName)
    {
        if (!RebindOn) return false;
        if (keyName == "Escape") { Cancel(); return true; }
        End(keyName);
        return true;
    }
    static void End(string key)
    {
        int tgt = RebindTgt;
        bool taken = (tgt == 2 && key == MkKey) || (tgt == 3 && key == PuzKey);
        string clash = tgt == 1 && Puz.On && (key == PuzKey || key == MkKey) ? "PUZZLE AI" : (tgt == 2 || tgt == 3) && Ab.On && key == BindKey ? "EXTERNAL BLOCK REBIND" : "";
        if (taken || clash != "") { if (clash != "") Puz.ClashRaise(tgt == 1 ? "EXTERNAL BLOCK REBIND" : "PUZZLE AI", clash, Label(key)); Cancel(); return; }
        RebindOn = false; RebindTgt = 0;
        if (tgt == 2) { PuzKey = key; PuzFlashAt = Clock.Tick; }
        else if (tgt == 3) { MkKey = key; MkFlashAt = Clock.Tick; }
        else { BindKey = key; BindFlashAt = Clock.Tick; }
        Save();
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    public static void Clear(int tgt)
    {
        if (RebindOn && RebindTgt == tgt) Cancel();
        if (tgt == 1) { if (BindKey == "") return; BindKey = ""; BindFlashAt = Clock.Tick; }
        else if (tgt == 2) { if (PuzKey == "") return; PuzKey = ""; PuzFlashAt = Clock.Tick; }
        else { if (MkKey == "") return; MkKey = ""; MkFlashAt = Clock.Tick; }
        Save();
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    public static bool Bound(int kr) => kr == 1 ? BindKey != "" : kr == 2 ? PuzKey != "" : MkKey != "";

    /// <summary>
    /// The AutoHotkey name of a side or middle mouse button, or "" for anything
    /// else. Hotkey("*" bindKey) takes these names exactly as it takes a key's,
    /// so a bind set to XButton2 has to reach the same dispatch a keyboard bind
    /// does - VkOf() cannot name them and returns 0. Returns interned constants,
    /// so the mouse hook allocates nothing on a move.
    /// </summary>
    public static string BtnName(int msg, int xb) => msg switch
    {
        0x207 or 0x208 => "MButton",
        0x20B or 0x20C => xb == 2 ? "XButton2" : "XButton1",
        _ => "",
    };
    /// <summary>True for the DOWN half of the three bindable buttons.</summary>
    public static bool BtnDown(int msg) => msg is 0x207 or 0x20B;
}
