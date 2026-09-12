using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Yuri.Platform;

namespace Yuri.Modules.Forsaken;

/// <summary>
/// The low-level keyboard and mouse hooks the .ahk got from AutoHotkey's
/// Hotkey / InputHook: installed on the UI thread (whose message loop
/// delivers them), one shared pair for every Forsaken feature. A handler
/// returns true to swallow the event, as a bare `*key::` hotkey does.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WinHooks
{
    delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookExW(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)] static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandleW(string? lpModuleName);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] static extern uint MapVirtualKeyW(uint uCode, uint uMapType);
    [StructLayout(LayoutKind.Sequential)] struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct MSLLHOOKSTRUCT { public int x, y; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public INPUTUNION u; }
    [StructLayout(LayoutKind.Explicit)] struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    static readonly IntPtr OurTag = new(0x59555249);                       // "YURI": our own synthetic input, so the hooks let it through
    static IntPtr _kb, _ms; static HookProc? _kbProc, _msProc;
    /// <summary>(vk, down, injected) -> swallow?</summary>
    public static Func<int, bool, bool, bool>? OnKey;
    /// <summary>(msg: 0x201 LBUTTONDOWN 0x202 LBUTTONUP 0x207/0x208 middle 0x20B/0x20C x-buttons, x, y, xbutton (1/2), injected) -> swallow?</summary>
    public static Func<int, int, int, int, bool, bool>? OnMouse;
    public static void Ensure()
    {
        if (_kb != IntPtr.Zero) return;
        _kbProc = KbHook; _msProc = MsHook;
        var h = GetModuleHandleW(null);
        _kb = SetWindowsHookExW(13, _kbProc, h, 0);
        _ms = SetWindowsHookExW(14, _msProc, h, 0);
    }
    public static void Release()
    {
        if (_kb != IntPtr.Zero) { UnhookWindowsHookEx(_kb); _kb = IntPtr.Zero; }
        if (_ms != IntPtr.Zero) { UnhookWindowsHookEx(_ms); _ms = IntPtr.Zero; }
    }
    static IntPtr KbHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && OnKey is not null)
        {
            var k = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            bool down = wParam == (IntPtr)0x100 || wParam == (IntPtr)0x104, injected = (k.flags & 0x10) != 0 || k.dwExtraInfo == OurTag;
            bool swallow = false;
            try { swallow = OnKey((int)k.vkCode, down, injected); } catch { }
            if (swallow) return (IntPtr)1;
        }
        return CallNextHookEx(_kb, nCode, wParam, lParam);
    }
    static IntPtr MsHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && OnMouse is not null)
        {
            var m = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            int msg = (int)wParam; bool injected = (m.flags & 1) != 0 || m.dwExtraInfo == OurTag;
            int xb = (int)(m.mouseData >> 16);
            bool swallow = false;
            try { swallow = OnMouse(msg, m.x, m.y, xb, injected); } catch { }
            if (swallow) return (IntPtr)1;
        }
        return CallNextHookEx(_ms, nCode, wParam, lParam);
    }
    public static void KeyDown(int vk) => Key(vk, true);
    public static void KeyUp(int vk) => Key(vk, false);
    /// <summary>
    /// One synthetic key, the way AutoHotkey's SendInput sends it: the virtual
    /// key AND its scan code, plus the extended-key flag where the key has one.
    /// A vk with wScan left at 0 arrives with an empty scan code in lParam,
    /// which anything reading raw input - a game engine, notably - ignores.
    /// That is why Q never landed in the client.
    /// </summary>
    static void Key(int vk, bool down)
    {
        ushort sc = (ushort)MapVirtualKeyW((uint)vk, 0);                    // MAPVK_VK_TO_VSC
        uint flags = (down ? 0u : 2u) | (Extended(vk) ? 1u : 0u);           // KEYEVENTF_KEYUP | KEYEVENTF_EXTENDEDKEY
        var inp = new INPUT[] { new() { type = 1, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = (ushort)vk, wScan = sc, dwFlags = flags, dwExtraInfo = OurTag } } } };
        SendInput(1, inp, Marshal.SizeOf<INPUT>());
    }
    static bool Extended(int vk) => vk is 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28
                                       or 0x2D or 0x2E or 0x2C or 0x90 or 0x6F or 0x5B or 0x5C or 0x5D or 0xA3 or 0xA5;

    public static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
    public static bool RobloxInFront()
    {
        try
        {
            var hw = GetForegroundWindow();
            if (hw == IntPtr.Zero || GetWindowThreadProcessId(hw, out uint pid) == 0) return false;
            return Process.GetProcessById((int)pid).ProcessName.Equals(Os.RobloxProcess, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
