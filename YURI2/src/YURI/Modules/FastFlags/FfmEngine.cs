using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using Yuri.Core;
using Yuri.Platform;
using Yuri.Shell.Hub;

namespace Yuri.Modules.FastFlags;

/// <summary>MemIO: the raw ReadProcessMemory / WriteProcessMemory wrapper.</summary>
public static class MemIO
{
    public static IntPtr HProcess; public static long ModuleBase; public const long ModuleSize = 200L * 1024 * 1024;
    public static string Why = "";
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, nuint size, out nuint read);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool WriteProcessMemory(IntPtr h, IntPtr addr, byte[] buf, nuint size, out nuint written);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool VirtualProtectEx(IntPtr h, IntPtr addr, nuint size, uint prot, out uint old);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern nuint VirtualQueryEx(IntPtr h, IntPtr addr, out MBI mbi, nuint len);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetExitCodeProcess(IntPtr h, out uint code);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool Module32FirstW(IntPtr snap, ref MODULEENTRY32W me);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool Module32NextW(IntPtr snap, ref MODULEENTRY32W me);
    [StructLayout(LayoutKind.Sequential)] public struct MBI { public IntPtr BaseAddress, AllocationBase; public uint AllocationProtect, __align; public nuint RegionSize; public uint State, Protect, Type, __align2; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MODULEENTRY32W { public uint dwSize, th32ModuleID, th32ProcessID, GlblcntUsage, ProccntUsage; public IntPtr modBaseAddr; public uint modBaseSize; public IntPtr hModule; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szModule; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExePath; }

    public static bool Attach(int pid)
    {
        if (HProcess != IntPtr.Zero) { CloseHandle(HProcess); HProcess = IntPtr.Zero; }
        HProcess = OpenProcess(0x1F0FFF, false, (uint)pid);
        if (HProcess == IntPtr.Zero) return false;
        ModuleBase = GetModuleBase(pid, Os.RobloxProcess + ".exe");
        return ModuleBase != 0;
    }
    public static void Detach()
    {
        if (HProcess != IntPtr.Zero) { CloseHandle(HProcess); HProcess = IntPtr.Zero; }
        ModuleBase = 0;
    }
    static long GetModuleBase(int pid, string name)
    {
        var snap = CreateToolhelp32Snapshot(0x08 | 0x10, (uint)pid);
        if (snap != IntPtr.Zero && snap != new IntPtr(-1))
        {
            try
            {
                var me = new MODULEENTRY32W { dwSize = (uint)Marshal.SizeOf<MODULEENTRY32W>() };
                if (Module32FirstW(snap, ref me))
                    do { if (string.Equals(me.szModule, name, StringComparison.OrdinalIgnoreCase)) return me.modBaseAddr.ToInt64(); } while (Module32NextW(snap, ref me));
            }
            finally { CloseHandle(snap); }
        }
        try { return Process.GetProcessById(pid).MainModule?.BaseAddress.ToInt64() ?? 0; } catch { return 0; }
    }
    public static byte[]? Read(long addr, int size)
    {
        if (size <= 0 || size > 1048576 || HProcess == IntPtr.Zero || addr <= 0) return null;
        try
        {
            var buf = new byte[size];
            return ReadProcessMemory(HProcess, (IntPtr)addr, buf, (nuint)size, out _) ? buf : null;
        }
        catch { return null; }
    }
    public static int ReadI32(long addr) { var b = Read(addr, 4); return b is null ? 0 : BitConverter.ToInt32(b, 0); }
    public static long ReadU64(long addr) { var b = Read(addr, 8); return b is null ? 0 : BitConverter.ToInt64(b, 0); }
    /// <summary>The same reads, null when the read FAILED rather than 0 — the pre-injection snapshot needs the difference.</summary>
    public static long? ReadI32Chk(long addr) { var b = Read(addr, 4); return b is null ? null : BitConverter.ToInt32(b, 0); }
    public static long? ReadU8Chk(long addr) { var b = Read(addr, 1); return b is null ? null : b[0]; }
    public static long? ReadChk(long addr, int w) => w == 1 ? ReadU8Chk(addr) : ReadI32Chk(addr);
    public static string ReadStr(long addr, int sz)
    {
        sz = Math.Min(sz, 8192);
        if (sz <= 0) return "";
        var b = Read(addr, sz);
        return b is null ? "" : Encoding.UTF8.GetString(b);
    }
    /// <summary>A plain write, then once more with the page's protection lifted; a verdict in Why when both refuse.</summary>
    public static bool Write(long addr, byte[] buf, int sz)
    {
        if (HProcess == IntPtr.Zero || addr == 0) return false;
        Why = "";
        if (WriteProcessMemory(HProcess, (IntPtr)addr, buf, (nuint)sz, out _)) return true;
        lock (typeof(MemIO))
        {
            foreach (uint np in new uint[] { 0x04, 0x40 })
            {
                if (!VirtualProtectEx(HProcess, (IntPtr)addr, (nuint)sz, np, out uint old)) continue;
                bool ok = WriteProcessMemory(HProcess, (IntPtr)addr, buf, (nuint)sz, out _);
                VirtualProtectEx(HProcess, (IntPtr)addr, (nuint)sz, old, out _);
                if (ok) return true;
            }
        }
        Why = Classify(addr);
        return false;
    }
    public static string Classify(long addr)
    {
        if (VirtualQueryEx(HProcess, (IntPtr)addr, out var mbi, (nuint)Marshal.SizeOf<MBI>()) == 0) return "address unknown to the process";
        if (mbi.State != 0x1000 || mbi.Protect == 0x01) return "stale address - the page is gone";
        if ((mbi.Protect & 0xCC) == 0) return mbi.Type == 0x1000000 ? "read-only image page - only the client file can set it" : mbi.Type == 0x40000 ? "locked by the anti-cheat - only the client file can set it" : "read-only page";
        return "write refused";
    }
    public static string WhyStr() => Why != "" ? "  -  " + Why : "";
    public static bool IsAlive()
    {
        if (HProcess == IntPtr.Zero) return false;
        return GetExitCodeProcess(HProcess, out uint code) && code == 0x103;
    }
}

/// <summary>The flag table's header, as FlagSingleton reads it: the master list's sentinel, the bucket array, the bucket mask.</summary>
public sealed record FlagHdr(long End, long List, long Mask) { public string Key => List + "|" + Mask + "|" + End; }

/// <summary>
/// FlagSingleton: finds the client's flag table. Fast route: a signature on
/// the accessor's prologue ("sub rsp,38h; mov rcx,[rip+x]; lea r8,[rip+y]"),
/// every hit checked for plausibility (readable header, sane pointers, a
/// 2^n-1 bucket mask, entries whose names read as flag names). Slow route:
/// a structural scan of every pointer-aligned qword in the data pages for
/// something shaped like the header. Addresses are cached per pid.
/// </summary>
public static class FlagSingleton
{
    const string PATTERN = "48 83 EC 38 48 8B 0D ?? ?? ?? ?? 4C 8D 05";
    static byte[]? _patBytes; static bool[]? _patMask; static int _patLen; static int _anchorAt; static byte _anchorB;
    static long _cached; static Dictionary<string, long>? _index; static string _hdrKey = "";
    static long _failAt; static bool _scanning; static long _shapeAt;
    public static int StatRegions, StatPass; public static long StatBytes; public static bool ShapeGaveUp;
    const int SCAN_BLOCK = 1048576, SCAN_RETRY = 1500, SHAPE_MS = 7000;
    sealed record PidCache(long Cached, Dictionary<string, long>? Index, string HdrKey);
    static readonly Dictionary<int, PidCache> _pidCache = new();
    static readonly Regex NameRx = new("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);

    static void BuildPattern()
    {
        if (_patLen != 0) return;
        var parts = PATTERN.Split(' ');
        _patBytes = new byte[parts.Length]; _patMask = new bool[parts.Length]; _patLen = parts.Length;
        for (int i = 0; i < parts.Length; i++) { if (parts[i] == "??") { _patBytes[i] = 0; _patMask[i] = false; } else { _patBytes[i] = Convert.ToByte(parts[i], 16); _patMask[i] = true; } }
        var freq = new Dictionary<byte, int> { [0x00] = 90, [0x48] = 61, [0x8B] = 28, [0xFF] = 20, [0x89] = 18, [0x83] = 16, [0x4C] = 16, [0x8D] = 13, [0x0F] = 12, [0x24] = 10, [0xE8] = 10, [0x44] = 8, [0x40] = 8, [0x10] = 7, [0x20] = 7, [0xC0] = 7, [0x01] = 6, [0x08] = 6, [0xCC] = 6 };
        int pick = -1, best = 0;
        for (int i = 0; i < _patLen; i++) { if (!_patMask[i]) continue; int sc = freq.TryGetValue(_patBytes[i], out var f) ? f : 5; if (pick < 0 || sc < best) { pick = i; best = sc; } }
        _anchorAt = Math.Max(pick, 0); _anchorB = _patBytes[_anchorAt];
    }
    public static void Invalidate() { _cached = 0; _index = null; _hdrKey = ""; _failAt = 0; }
    public static void Stash(int pid) { if (pid != 0) _pidCache[pid] = new PidCache(_cached, _index, _hdrKey); }
    public static void Adopt(int pid)
    {
        Invalidate();
        if (pid == 0 || !_pidCache.TryGetValue(pid, out var e)) return;
        _cached = e.Cached; _index = e.Index; _hdrKey = e.HdrKey;
    }
    public static void Forget(int pid) { if (pid != 0) _pidCache.Remove(pid); }
    public static void ClearCooldown() => _failAt = 0;

    /// <summary>Every flag once, by lowercase name → the entry's flag-object pointer (vpr); cached for the life of the table.</summary>
    public static Dictionary<string, long> Index(FlagHdr? hdr)
    {
        if (_index is not null) return _index;
        var idx = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (hdr is null || hdr.End == 0) return _index = idx;
        long node = MemIO.ReadU64(hdr.End + 8);
        int n = 0;
        while (node != 0 && node != hdr.End && n < 200000)
        {
            var entry = MemIO.Read(node, 64);
            if (entry is null) break;
            long sz = BitConverter.ToInt64(entry, 32), alc = BitConverter.ToInt64(entry, 40);
            if (sz > 0 && sz <= 8192)
            {
                string nm = EntryName(entry, sz, alc);
                long vpr = BitConverter.ToInt64(entry, 48);
                if (nm != "" && vpr != 0 && !idx.ContainsKey(nm)) idx[nm] = vpr;
            }
            node = BitConverter.ToInt64(entry, 8);
            n++;
        }
        return _index = idx;
    }
    public static string EntryName(byte[] entry, long sz, long alc)
    {
        if (sz <= 0) return "";
        if (alc > 15) return sz <= 8192 ? MemIO.ReadStr(BitConverter.ToInt64(entry, 16), (int)sz) : "";
        return sz <= 15 ? Encoding.UTF8.GetString(entry, 16, (int)sz) : "";
    }
    static bool PtrOk(long p) => p >= 0x10000 && p <= 0x7FFFFFFFFFFF;
    static bool Plausible(long sgl)
    {
        if (!PtrOk(sgl)) return false;
        var h = MemIO.Read(sgl, 56);
        if (h is null) return false;
        long end = BitConverter.ToInt64(h, 8), list = BitConverter.ToInt64(h, 24), mask = BitConverter.ToInt64(h, 48);
        if (!PtrOk(end) || !PtrOk(list)) return false;
        if (mask < 0x1F || mask > 0x1FFFFF || ((mask + 1) & mask) != 0) return false;
        long node = MemIO.ReadU64(end + 8);
        int good = 0;
        for (int k = 0; k < 6; k++)
        {
            if (node == 0 || node == end) break;
            var e = MemIO.Read(node, 64);
            if (e is null) break;
            long sz = BitConverter.ToInt64(e, 32), alc = BitConverter.ToInt64(e, 40), vpr = BitConverter.ToInt64(e, 48);
            if (sz < 2 || sz > 256 || alc < sz || vpr == 0) return false;
            if (!NameRx.IsMatch(EntryName(e, sz, alc))) return false;
            good++;
            node = BitConverter.ToInt64(e, 8);
        }
        return good >= 2;
    }
    /// <summary>The signature scan: executable pages first, then everything committed and readable.</summary>
    public static long Get()
    {
        if (_cached != 0) return _cached;
        if (_scanning) return 0;
        if (_failAt != 0 && Clock.Tick - _failAt < SCAN_RETRY) return 0;
        BuildPattern();
        if (MemIO.HProcess == IntPtr.Zero || MemIO.ModuleBase == 0 || _patLen < 1) return 0;
        _scanning = true;
        try
        {
            var hp = MemIO.HProcess;
            StatRegions = 0; StatBytes = 0; StatPass = 0;
            for (int pass = 1; pass <= 2; pass++)
            {
                bool execOnly = pass == 1;
                StatPass = pass;
                long addr = MemIO.ModuleBase, stopAt = addr + MemIO.ModuleSize;
                while (addr < stopAt && MemIO.VirtualQueryEx(hp, (IntPtr)addr, out var mbi, (nuint)Marshal.SizeOf<MemIO.MBI>()) != 0)
                {
                    long vBase = mbi.BaseAddress.ToInt64(), vSize = (long)mbi.RegionSize;
                    if (vSize == 0) break;
                    bool ok = mbi.State == 0x1000 && (mbi.Protect & 0x101) == 0;
                    if (ok && execOnly) ok = (mbi.Protect & 0xF0) != 0;
                    if (ok)
                    {
                        StatRegions++;
                        long bOff = 0;
                        while (bOff < vSize)
                        {
                            int bLen = (int)Math.Min(SCAN_BLOCK, vSize - bOff);
                            if (bLen < _patLen) break;
                            var data = MemIO.Read(vBase + bOff, bLen);
                            if (data is not null)
                            {
                                StatBytes += bLen;
                                long hit = ScanBlock(data, bLen, vBase + bOff);
                                if (hit != 0) return hit;
                            }
                            if (bOff + bLen >= vSize) break;
                            bOff += bLen - (_patLen - 1);
                            if (MemIO.HProcess != hp) return 0;
                        }
                    }
                    addr = vBase + vSize;
                }
            }
            if (MemIO.HProcess == hp) _failAt = Clock.Tick;
            return 0;
        }
        finally { _scanning = false; }
    }
    static long ScanBlock(byte[] data, int len, long baseAddr)
    {
        int lim = len - _patLen;
        if (lim < 0) return 0;
        var span = data.AsSpan(0, len);
        int pos = _anchorAt;
        while (pos <= lim + _anchorAt)
        {
            int found = span[pos..].IndexOf(_anchorB);
            if (found < 0) return 0;
            int at = pos + found;
            pos = at + 1;
            int off = at - _anchorAt;
            if (off < 0 || off > lim) continue;
            bool ok = true;
            for (int i = 0; i < _patLen; i++) if (_patMask![i] && data[off + i] != _patBytes![i]) { ok = false; break; }
            if (!ok) continue;
            long fa = baseAddr + off;
            int rel = MemIO.ReadI32(fa + 7);
            long sgl = MemIO.ReadU64(fa + 11 + rel);
            if (sgl != 0 && Plausible(sgl)) { _cached = sgl; return sgl; }
        }
        return 0;
    }
    public static FlagHdr? PeekHeader() => _cached != 0 ? ReadHeader() : null;

    /// <summary>The structural scan: every pointer-aligned qword in the data pages, checked for the header's shape, seven seconds at most.</summary>
    static long FindByShape()
    {
        if (MemIO.HProcess == IntPtr.Zero || MemIO.ModuleBase == 0 || _scanning) return 0;
        _scanning = true; ShapeGaveUp = false; StatRegions = 0; StatBytes = 0; StatPass = 0;
        try
        {
            var hp = MemIO.HProcess;
            long t0 = Clock.Tick;
            for (int pass = 1; pass <= 2; pass++)
            {
                long algn = pass == 1 ? 0xF : 0x7;
                StatPass = pass;
                var seen = new HashSet<long>();
                long addr = MemIO.ModuleBase, stop = addr + MemIO.ModuleSize;
                while (addr < stop && MemIO.VirtualQueryEx(hp, (IntPtr)addr, out var mbi, (nuint)Marshal.SizeOf<MemIO.MBI>()) != 0)
                {
                    long vBase = mbi.BaseAddress.ToInt64(), vSize = (long)mbi.RegionSize;
                    if (vSize == 0) break;
                    if (mbi.State == 0x1000 && (mbi.Protect & 0x101) == 0 && (mbi.Protect & 0xF0) == 0)
                    {
                        StatRegions++;
                        long bOff = 0;
                        while (bOff < vSize)
                        {
                            int bLen = (int)Math.Min(SCAN_BLOCK, vSize - bOff);
                            if (bLen < 8) break;
                            var data = MemIO.Read(vBase + bOff, bLen);
                            if (data is not null)
                            {
                                StatBytes += bLen;
                                for (int off = 0; off + 8 <= bLen; off += 8)
                                {
                                    long p = BitConverter.ToInt64(data, off);
                                    if (!PtrOk(p) || (p & algn) != 0) continue;
                                    if (!seen.Add(p)) continue;
                                    if (ShapeOK(p)) return p;
                                }
                            }
                            if (Clock.Tick - t0 > SHAPE_MS) { ShapeGaveUp = true; return 0; }
                            if (bOff + bLen >= vSize) break;
                            bOff += bLen;
                            if (MemIO.HProcess != hp) return 0;
                        }
                    }
                    addr = vBase + vSize;
                }
            }
            return 0;
        }
        finally { _scanning = false; }
    }
    static bool ShapeOK(long p)
    {
        var hdr = MemIO.Read(p, 56);
        if (hdr is null) return false;
        long end = BitConverter.ToInt64(hdr, 8), list = BitConverter.ToInt64(hdr, 24), mask = BitConverter.ToInt64(hdr, 48);
        if (end == 0 || list == 0 || mask < 0x1F || mask > 0x1FFFFF || ((mask + 1) & mask) != 0) return false;
        if (!PtrOk(end) || !PtrOk(list)) return false;
        long node = MemIO.ReadU64(end + 8);
        int good = 0;
        for (int k = 0; k < 12; k++)
        {
            if (node == 0 || node == end) break;
            var entry = MemIO.Read(node, 64);
            if (entry is null) break;
            long sz = BitConverter.ToInt64(entry, 32), alc = BitConverter.ToInt64(entry, 40), vpr = BitConverter.ToInt64(entry, 48);
            if (sz > 2 && sz <= 200 && alc >= sz && PtrOk(vpr)) { string nm = EntryName(entry, sz, alc); if (nm != "" && NameRx.IsMatch(nm)) good++; }
            node = BitConverter.ToInt64(entry, 8);
        }
        return good >= 4;
    }
    public static FlagHdr? AdoptShape()
    {
        long sgl = FindByShape();
        if (sgl == 0) return null;
        _cached = sgl; _index = null; _hdrKey = "";
        return ReadHeader();
    }
    public static FlagHdr? Header(bool allowScan)
    {
        var h = ReadHeader();
        if (h is not null && h.List != 0 && h.Mask != 0) return h;
        if (!allowScan) return null;
        if (_shapeAt != 0 && Clock.Tick - _shapeAt < 5000) return null;
        _shapeAt = Clock.Tick;
        return AdoptShape();
    }
    public static FlagHdr? ReadHeader()
    {
        long sgl = Get();
        if (sgl == 0) return null;
        var hdr = MemIO.Read(sgl, 56);
        if (hdr is null) { _cached = 0; return null; }
        var h = new FlagHdr(BitConverter.ToInt64(hdr, 8), BitConverter.ToInt64(hdr, 24), BitConverter.ToInt64(hdr, 48));
        if (h.End == 0 || h.List == 0 || h.Mask < 0x1F || h.Mask > 0x1FFFFF || ((h.Mask + 1) & h.Mask) != 0) { _cached = 0; _index = null; _hdrKey = ""; return null; }
        if (h.Key != _hdrKey) { _hdrKey = h.Key; _index = null; }
        return h;
    }
}

/// <summary>FFlags: the per-flag hash-walk, typed reads and writes.</summary>
public static class FFlags
{
    /// <summary>FNV-1a over the bare name.</summary>
    public static ulong Hash(string name)
    {
        ulong h = 0xcbf29ce484222325;
        foreach (char c in name) { h ^= c; h *= 0x100000001b3; }
        return h;
    }
    public static bool Ptr(long p) => p >= 0x10000 && p <= 0x7FFFFFFFFFFF;
    /// <summary>The value's address: the bucket for the name's hash, walked for an exact match, then the flag object's value at +0xC0.</summary>
    public static long FindValuePtr(string name, FlagHdr hdr)
    {
        long bucket = (long)(Hash(name) & (ulong)hdr.Mask);
        long node = MemIO.ReadU64(hdr.List + bucket * 16 + 8);
        for (int k = 0; k < 300; k++)
        {
            if (node == 0 || node == hdr.End) break;
            var entry = MemIO.Read(node, 64);
            if (entry is null) break;
            long sz = BitConverter.ToInt64(entry, 32), alc = BitConverter.ToInt64(entry, 40);
            string ename = FlagSingleton.EntryName(entry, sz, alc);
            if (ename != "" && ename == name)
            {
                long vpr = BitConverter.ToInt64(entry, 48);
                return vpr == 0 ? 0 : MemIO.ReadU64(vpr + 0xC0);
            }
            node = BitConverter.ToInt64(entry, 8);
        }
        return 0;
    }
    public static long? Coerce(string value)
    {
        string v = value.Trim().ToLowerInvariant();
        if (v == "true" || v == "1") return 1;
        if (v == "false" || v == "0") return 0;
        if (long.TryParse(v, out var n)) return Fit(n);
        if (double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)) return Fit((long)Math.Round(d));
        return null;
    }
    static long? Fit(long n) => n >= int.MinValue && n <= int.MaxValue ? n : null;
    public static bool SetAtW(long vp, string value, int w)
    {
        var rv = Coerce(value);
        if (rv is null || !Ptr(vp)) return false;
        if (w == 1)
        {
            if (rv < 0 || rv > 255) return false;
            return MemIO.Write(vp, new[] { (byte)rv.Value }, 1);
        }
        return MemIO.Write(vp, BitConverter.GetBytes((int)rv.Value), 4);
    }
    public sealed record StrObjR(string Hex, string Text);
    /// <summary>The 32-byte std::string object at vp: in-place when its capacity is 15, on the heap otherwise.</summary>
    public static StrObjR? StrObj(long vp)
    {
        var b = Ptr(vp) ? MemIO.Read(vp, 32) : null;
        if (b is null) return null;
        long sz = BitConverter.ToInt64(b, 16), cap = BitConverter.ToInt64(b, 24);
        if (sz < 0 || sz > 8192 || cap < 15 || cap > 0x7FFFFFFF) return null;
        string txt = cap <= 15 ? (sz <= 15 ? Encoding.UTF8.GetString(b, 0, (int)sz) : "") : MemIO.ReadStr(BitConverter.ToInt64(b, 0), (int)sz);
        return new StrObjR(Convert.ToHexString(b), txt);
    }
    /// <summary>1 written, 0 refused, -1 too long for the in-place form.</summary>
    public static int SetStr(long vp, string value)
    {
        if (!Ptr(vp)) return 0;
        var sb = Encoding.UTF8.GetBytes(value);
        if (sb.Length > 15) return -1;
        var o = new byte[32];
        Array.Copy(sb, o, sb.Length);
        BitConverter.GetBytes((long)sb.Length).CopyTo(o, 16);
        BitConverter.GetBytes(15L).CopyTo(o, 24);
        return MemIO.Write(vp, o, 32) ? 1 : 0;
    }
    public static bool PutRaw(long vp, string hex)
    {
        if (!Ptr(vp) || hex.Length != 64) return false;
        return MemIO.Write(vp, Convert.FromHexString(hex), 32);
    }
}

/// <summary>One client's injection state (FFM.ps[pid]): what was written, where, what refused, its own staged list.</summary>
public sealed class Inst
{
    public Dictionary<string, string> Orig = new(); public Dictionary<string, long> OrigAt = new(); public HashSet<string> Failed = new();
    public int Succ, Fail; public string HdrKey = "", Label = ""; public List<Flag>? Flags; public Dictionary<string, int>? Map; public long Stamp;
    public Dictionary<string, long> RaVP = new(); public Dictionary<string, int> RaSkip = new(), RaSkipN = new(), RaStuck = new(), RaDrift = new(); public string RaKey = "";
}

/// <summary>
/// The engine's driver: the poll (attach to a client, notice one leaving,
/// notice the table being rebuilt), INJECT (two passes: the bucket walk with
/// the original captured first, then the master index for what missed —
/// strings only in the 15-byte in-place form), UNINJECT (put the originals
/// back and verify), the re-apply watchdog (rewrite what the client resets,
/// bursting after a rebuild, backing off on what will not hold) and CLOSE
/// RBX. Windows only; elsewhere Attached is always false.
/// </summary>
public static class FfmEngine
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_ACC = 0xFFFB7185;
    const int RA_IDLE = 2000, RA_BURST = 350, RA_BURSTN = 14;
    public static int Pid; public static long Stamp; public static bool Busy;
    public static readonly List<int> InstList = new();                 // FFM.inst
    static readonly Dictionary<int, Inst> Ps = new();
    public static Dictionary<string, string> Orig = new(); public static Dictionary<string, long> OrigAt = new(); public static HashSet<string> Failed = new();
    public static int InjectSucc, InjectFail; public static string LastHdrKey = ""; public static long WrAt, AttFailAt, InstAt;
    public static bool Rbx; public static long RbxAt;
    static Dictionary<string, long> RaVP = new(); static Dictionary<string, int> RaSkip = new(), RaSkipN = new(), RaStuck = new(), RaDrift = new(); static string RaKey = "";
    static int _raBurst, _raQuiet, _raRot, _raHeld; static long _raSaidAt;
    static readonly HashSet<int> AiDone = new(); static int _autoInjGen;
    static DispatcherTimer? _poll, _ra; static int _pollMs;
    public static bool Attached => Pid != 0 && MemIO.HProcess != IntPtr.Zero;
    public static Action? Changed;
    static void Poke() { Changed?.Invoke(); HubSurface.Live?.Tim(Pace.TICK_A); }
    static void Log(string m, string f, string s) => FfmViews.Log(m, f, s);

    public static void Boot()
    {
        if (!Os.IsWin) return;
        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) }; _pollMs = 1200;
        _poll.Tick += (s, e) => { try { Poll(); } catch (Exception ex) { Log("Poll", "-", "fault: " + ex.Message); } };
        _poll.Start();
        ManageReApply();
    }

    // ---- processes and instances ----
    public static List<int> ProcList(out List<int> crash)
    {
        var res = new List<int>(); crash = new List<int>();
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.ProcessName.Equals(Os.RobloxProcess, StringComparison.OrdinalIgnoreCase)) res.Add(p.Id);
                    else if (p.ProcessName.Equals("RobloxCrashHandler", StringComparison.OrdinalIgnoreCase)) crash.Add(p.Id);
                }
                catch { }
            }
        }
        catch { }
        res.Sort();
        return res;
    }
    static long ProcStamp(int pid) { try { return Process.GetProcessById(pid).StartTime.ToFileTimeUtc(); } catch { return 0; } }
    static Inst PS(int pid)
    {
        long st = ProcStamp(pid);
        if (Ps.TryGetValue(pid, out var old) && st != 0 && old.Stamp != 0 && old.Stamp != st) { Ps.Remove(pid); FlagSingleton.Forget(pid); }
        if (!Ps.TryGetValue(pid, out var ps)) Ps[pid] = ps = new Inst { Stamp = st };
        else if (st != 0 && ps.Stamp == 0) ps.Stamp = st;
        return ps;
    }
    public static int InstIdx(int pid) { int i = InstList.IndexOf(pid); return i < 0 ? 0 : i + 1; }
    public static string InstLabel(int pid, double tw) { int i = InstIdx(pid); return tw >= 96 ? "CLIENT " + i + "  " + pid : tw >= 62 ? "#" + i + "  " + pid : "#" + i; }
    static bool SameProc(IntPtr hp, int pid) => hp != IntPtr.Zero && pid != 0 && MemIO.HProcess == hp && Pid == pid;
    static bool Exists(int pid) { try { return !Process.GetProcessById(pid).HasExited; } catch { return false; } }

    /// <summary>FFMUseInstance(pid): hand the current client's maps back, attach to this one, adopt its cached addresses.</summary>
    public static bool UseInstance(int pid, bool quiet = false)
    {
        if (pid == Pid && MemIO.HProcess != IntPtr.Zero && MemIO.IsAlive()) return true;
        if (Pid != 0)
        {
            var cur = PS(Pid);
            if (cur.Stamp == 0 || Stamp == 0 || cur.Stamp == Stamp)
            {
                cur.Orig = Orig; cur.OrigAt = OrigAt; cur.Failed = Failed; cur.Succ = InjectSucc; cur.Fail = InjectFail; cur.HdrKey = LastHdrKey;
                cur.Flags = Ffm.Flags.ToList();
                cur.RaVP = RaVP; cur.RaSkip = RaSkip; cur.RaSkipN = RaSkipN; cur.RaStuck = RaStuck; cur.RaDrift = RaDrift; cur.RaKey = RaKey;
            }
        }
        FlagSingleton.Stash(Pid);
        MemIO.Detach();
        FlagSingleton.Invalidate();
        var ps = PS(pid);
        ps.Flags ??= Ffm.Flags.Select(f => f.Clone()).ToList();
        Orig = ps.Orig; Failed = ps.Failed; OrigAt = ps.OrigAt; InjectSucc = ps.Succ; InjectFail = ps.Fail; LastHdrKey = ps.HdrKey;
        Ffm.SwapList(ps.Flags);
        RaVP = ps.RaVP; RaSkip = ps.RaSkip; RaSkipN = ps.RaSkipN; RaStuck = ps.RaStuck; RaDrift = ps.RaDrift; RaKey = ps.RaKey;
        if (!quiet) Ffm.SelReset?.Invoke();
        if (!MemIO.Attach(pid)) { Pid = 0; Stamp = 0; return false; }
        Pid = pid; Stamp = ps.Stamp;
        FlagSingleton.Adopt(pid);
        return true;
    }

    /// <summary>FFMPoll(): the client list, attach / detach, auto-inject, the rebuilt-table notice.</summary>
    public static void Poll()
    {
        var lst = ProcList(out var crashPids);
        InstList.Clear(); InstList.AddRange(lst);
        Modules.Special.Spf.CrashSweep?.Invoke(crashPids);
        foreach (var dead in Ps.Keys.Where(k => !lst.Contains(k)).ToList()) { FlagSingleton.Forget(dead); Ps.Remove(dead); AiDone.Remove(dead); }
        bool live = lst.Count > 0;
        if (Rbx != live) { Rbx = live; RbxAt = Clock.Tick; Poke(); }
        int wasPid = Pid;
        if (Pid != 0 && !lst.Contains(Pid)) Pid = 0;
        int pid = Pid != 0 ? Pid : live ? lst[0] : 0;
        if (!live)
        {
            if (wasPid != 0 || MemIO.HProcess != IntPtr.Zero)
            {
                FlagSingleton.Forget(wasPid);
                Pid = 0; MemIO.Detach(); FlagSingleton.Invalidate();
                Orig = new(); OrigAt = new(); Failed = new(); LastHdrKey = ""; AttFailAt = 0;
                Ffm.Say("ROBLOX CLOSED", C_ACC);
                Poke();
            }
            SetPollMs(1200);
            return;
        }
        if (pid != Pid && !Busy)
        {
            int gen = ++_autoInjGen;
            if (UseInstance(pid))
            {
                AttFailAt = 0;
                Ffm.Say("ROBLOX ATTACHED  PID=" + pid, C_ON);
                if (FfmPanel.AutoInject && Ffm.Flags.Count > 0) { AiDone.Add(pid); Later(4000, () => AutoInjectFire(gen, null)); }
            }
            else if (AttFailAt == 0 || Clock.Tick - AttFailAt > 15000)
            {
                AttFailAt = Clock.Tick;
                Ffm.Say("ROBLOX FOUND BUT COULD NOT ATTACH - TRY RUNNING AS ADMIN", C_ACC);
            }
            Poke();
        }
        if (FfmPanel.AutoInject && Ffm.Flags.Count > 0 && !Busy)
        {
            var fresh = lst.Where(p2 => !AiDone.Contains(p2)).ToList();
            if (fresh.Count > 0) { foreach (var p2 in fresh) AiDone.Add(p2); int gen2 = ++_autoInjGen; Later(4000, () => AutoInjectFire(gen2, fresh)); }
        }
        if (pid != 0 && Ffm.Flags.Count > 0 && !Busy && MemIO.HProcess != IntPtr.Zero && MemIO.IsAlive())
        {
            var hdr = FlagSingleton.PeekHeader();
            if (hdr is not null && hdr.List != 0 && hdr.Mask != 0)
            {
                string key = hdr.Key;
                if (LastHdrKey != "" && key != LastHdrKey)
                {
                    int gen = ++_autoInjGen;
                    if (FfmPanel.AutoInject) { Ffm.Say("FLAG TABLE REBUILT - RE-INJECTING", AMBER); Later(4000, () => AutoInjectFire(gen, null)); }
                    else if (FfmPanel.ReApply && Orig.Count > 0) Ffm.Say("FLAG TABLE REBUILT - RE-APPLY IS HOLDING", AMBER);
                    else Ffm.Say("FLAG TABLE REBUILT - INJECT AGAIN IF NEEDED", AMBER);
                    if (FfmPanel.ReApply) ReApplyBurst();
                    Poke();
                }
                LastHdrKey = key;
            }
        }
        SetPollMs(Pid != 0 && MemIO.HProcess != IntPtr.Zero ? 4000 : 1200);
    }
    static void SetPollMs(int ms) { if (_poll is not null && ms != _pollMs) { _pollMs = ms; _poll.Interval = TimeSpan.FromMilliseconds(ms); } }
    static void Later(int ms, Action a)
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        t.Tick += (s, e) => { t.Stop(); try { a(); } catch (Exception ex) { Log("Timer", "-", "fault: " + ex.Message); } };
        t.Start();
    }
    static void AutoInjectFire(int gen, List<int>? pids)
    {
        if (gen != _autoInjGen) return;
        if (pids is null || pids.Count == 0) { ApplyLive(true); return; }
        if (Busy) return;
        int was = Pid, done = 0;
        foreach (var pid in pids)
        {
            if (!Exists(pid) || !UseInstance(pid)) continue;
            if (Ffm.Flags.Count > 0 && ApplyLive(true, true) > 0) done++;
        }
        if (was != 0 && was != Pid && Exists(was)) UseInstance(was);
        if (done > 0) Ffm.Say("AUTO INJECTED " + done + " CLIENT(S)", C_ON);
        Poke();
    }

    // ---- INJECT ----
    public static void InjectAll()
    {
        var lst = ProcList(out _);
        if (lst.Count == 0) { Ffm.Say("NO ROBLOX INSTANCES", C_ACC); return; }
        int was = Pid, done = 0, tot = 0, empty = 0;
        foreach (var pid in lst)
        {
            if (!UseInstance(pid)) continue;
            if (Ffm.Flags.Count == 0) { empty++; continue; }
            int n = ApplyLive(false, true);
            if (n > 0) { done++; tot += n; }
        }
        if (was != 0 && was != Pid && lst.Contains(was)) UseInstance(was);
        Ffm.Say(done > 0 ? "APPLIED TO " + done + " INSTANCE(S) - " + tot + " WRITE(S)" + (empty > 0 ? "  -  " + empty + " EMPTY" : "")
                         : empty > 0 ? "NOTHING STAGED IN " + empty + " INSTANCE(S)" : "APPLIED TO NO INSTANCES", done > 0 ? C_ON : C_ACC);
        Poke();
    }
    public static int WidthOf(string nm)
    {
        string p = Ffm.PfxOf(nm);
        if (p != "") return p.Contains("Flag") ? 1 : 4;
        int i = Ffm.IndexOf(nm);
        return i >= 0 && i < Ffm.Flags.Count && Ffm.Flags[i].Type == "bool" ? 1 : 4;
    }
    /// <summary>FFMApplyLive(isAuto, silent): the staged list into the attached client's memory. Returns how many wrote.</summary>
    public static int ApplyLive(bool isAuto = false, bool silent = false)
    {
        if (!Os.IsWin) { Ffm.Say("LIVE INJECTION IS A WINDOWS FEATURE - THE CLIENT FILE CARRIES THE FLAGS HERE", AMBER); return 0; }
        if (Busy) { if (!isAuto) Ffm.Say("INJECTION IN PROGRESS - TRY AGAIN IN A MOMENT", AMBER); return 0; }
        if (Pid == 0 || MemIO.HProcess == IntPtr.Zero) { Ffm.Say("ROBLOX NOT ATTACHED", C_ACC); return 0; }
        if (!MemIO.IsAlive()) { Poll(); Ffm.Say("ROBLOX NOT ATTACHED", C_ACC); return 0; }
        if (Ffm.Flags.Count == 0) { Ffm.Say("NOTHING STAGED", C_ACC); return 0; }
        Busy = true;
        try
        {
            int succ = 0, fail = 0;
            var hpA = MemIO.HProcess; int pidA = Pid;
            FlagHdr? hdr = null;
            if (FfmPanel.UseSingleton)
            {
                int maxTries = isAuto ? 15 : 1;
                if (!isAuto) FlagSingleton.ClearCooldown();
                for (int t = 1; t <= maxTries; t++)
                {
                    hdr = FlagSingleton.ReadHeader();
                    if (hdr is not null && hdr.List != 0 && hdr.Mask != 0) break;
                    if (isAuto)
                    {
                        if (!SameProc(hpA, pidA) || !MemIO.IsAlive()) { Log("Inject", "-", "auto wait abandoned: target gone after " + t + " of " + maxTries); hdr = null; break; }
                        Ffm.Say("WAITING FOR SINGLETON " + t + "/" + maxTries, AMBER);
                        Thread.Sleep(500);
                    }
                }
            }
            else
            {
                if (!isAuto) Ffm.Say("SCANNING FOR THE FLAG TABLE...", AMBER);
                hdr = FlagSingleton.AdoptShape();
            }
            if (hdr is null || hdr.List == 0 || hdr.Mask == 0)
            {
                int rg = FlagSingleton.StatRegions; double mb = Math.Round(FlagSingleton.StatBytes / 1048576.0, 1);
                if (FfmPanel.UseSingleton)
                {
                    Ffm.Say(rg > 0 ? "SINGLETON NOT FOUND - SCANNED " + mb + " MB  -  TRY SINGLETON OFF" : "SINGLETON NOT FOUND - NO READABLE MEMORY", C_ACC);
                    Log("Inject", "singleton", rg > 0 ? "no match in " + rg + " region(s), " + mb + " MB, pass " + FlagSingleton.StatPass : "scanned nothing - 0 regions passed the filter");
                }
                else
                {
                    if (FlagSingleton.ShapeGaveUp) Ffm.Say("SCAN TIMED OUT AFTER " + mb + " MB  -  PRESS INJECT AGAIN", C_ACC);
                    else if (rg == 0) Ffm.Say("FLAG TABLE NOT FOUND - NO READABLE MEMORY", C_ACC);
                    else Ffm.Say("FLAG TABLE NOT FOUND - SCANNED " + mb + " MB  -  TRY SINGLETON ON", C_ACC);
                    Log("Inject", "table", FlagSingleton.ShapeGaveUp ? "structural scan hit its time budget after " + rg + " region(s), " + mb + " MB, pass " + FlagSingleton.StatPass
                                                                   : "structural scan found no matching header in " + rg + " region(s), " + mb + " MB, pass " + FlagSingleton.StatPass);
                }
                return 0;
            }
            if (!SameProc(hpA, pidA)) { if (!silent) Ffm.Say("INJECT ABORTED - TARGET CHANGED MID-SCAN", C_ACC); Log("Inject", "-", "aborted: instance changed while locating the table"); return 0; }
            Failed = new();
            var miss = new List<Flag>();
            var hp0 = MemIO.HProcess; int pid0 = Pid;
            bool swapped = false;
            foreach (var fl in Ffm.Flags.ToList())
            {
                if (!SameProc(hp0, pid0)) { swapped = true; break; }
                if (!fl.On) continue;
                string bare = Ffm.BareOf(fl.Name);
                if (Ffm.KindOf(fl) == "string") { miss.Add(fl); continue; }
                bool had = Orig.ContainsKey(fl.Name);
                long vp1 = had && OrigAt.TryGetValue(fl.Name, out var oa) ? oa : FFlags.FindValuePtr(bare, hdr);
                if (!FFlags.Ptr(vp1)) { miss.Add(fl); continue; }
                int w1 = WidthOf(fl.Name);
                long? cur = null;
                if (!had) { cur = MemIO.ReadChk(vp1, w1); if (cur is null) { miss.Add(fl); continue; } }
                if (FFlags.SetAtW(vp1, fl.Value, w1))
                {
                    if (!had) { Orig[fl.Name] = cur!.Value.ToString(); OrigAt[fl.Name] = vp1; }
                    succ++;
                    Log("Inject", fl.Name, "OK \u2192 " + fl.Value);
                }
                else miss.Add(fl);
            }
            if (swapped) { Ffm.Say("INJECT ABORTED - ROBLOX RESTARTED MID-WRITE", C_ACC); Log("Inject", "-", "aborted after " + succ + " flag(s): process changed"); return 0; }
            if (miss.Count > 0)
            {
                Ffm.Say("RESOLVING " + miss.Count + " FLAG" + (miss.Count == 1 ? "" : "S") + "...", AMBER);
                var idx = FlagSingleton.Index(hdr);
                foreach (var fl in miss)
                {
                    if (!SameProc(hp0, pid0)) { Ffm.Say("INJECT ABORTED - ROBLOX RESTARTED MID-WRITE", C_ACC); Log("Inject", "-", "aborted after " + succ + " flag(s): process changed"); return 0; }
                    string nm = fl.Name, bare = Ffm.BareOf(nm);
                    if (Ffm.KindOf(fl) == "string")
                    {
                        if (!idx.TryGetValue(bare, out var vprS)) { fail++; Failed.Add(nm); Log("Inject", nm, idx.Count > 0 ? "NOT IN THIS ROBLOX BUILD" : "table unreadable - could not verify"); continue; }
                        long vps = OrigAt.TryGetValue(nm, out var oas) ? oas : MemIO.ReadU64(vprS + 0xC0);
                        var so = vps != 0 ? FFlags.StrObj(vps) : null;
                        if (so is null) { fail++; Failed.Add(nm); Log("Inject", nm, "original unreadable - NOT written (could not be undone)"); continue; }
                        int rs = FFlags.SetStr(vps, fl.Value);
                        if (rs == -1) { fail++; Failed.Add(nm); Log("Inject", nm, "SKIPPED - " + Encoding.UTF8.GetByteCount(fl.Value) + " bytes; a string over 15 cannot be written live - the client file sets it at launch"); continue; }
                        if (rs == 1)
                        {
                            if (!Orig.ContainsKey(nm)) { Orig[nm] = "str:" + so.Hex; OrigAt[nm] = vps; }
                            succ++;
                            Log("Inject", nm, "OK (string) \u2192 " + fl.Value + " (was " + so.Text + ")");
                        }
                        else { fail++; Failed.Add(nm); Log("Inject", nm, "found but write refused" + MemIO.WhyStr()); }
                        continue;
                    }
                    if (FFlags.Coerce(fl.Value) is null)
                    {
                        fail++; Failed.Add(nm);
                        Log("Inject", nm, fl.Type == "string" ? "SKIPPED - string flags cannot be written to memory"
                            : double.TryParse(fl.Value.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) ? "SKIPPED - outside 32-bit range: " + fl.Value : "SKIPPED - value not numeric: " + fl.Value);
                        continue;
                    }
                    if (!idx.TryGetValue(bare, out var vpr)) { fail++; Failed.Add(nm); Log("Inject", nm, idx.Count > 0 ? "NOT IN THIS ROBLOX BUILD" : "table unreadable - could not verify"); continue; }
                    bool had0 = Orig.ContainsKey(nm);
                    long vp0 = had0 && OrigAt.TryGetValue(nm, out var oa0) ? oa0 : MemIO.ReadU64(vpr + 0xC0);
                    int w0 = WidthOf(nm);
                    long? cur0 = null;
                    if (!had0) { cur0 = vp0 != 0 ? MemIO.ReadChk(vp0, w0) : null; if (cur0 is null) { fail++; Failed.Add(nm); Log("Inject", nm, "original unreadable - NOT written (could not be undone)"); continue; } }
                    if (FFlags.SetAtW(vp0, fl.Value, w0))
                    {
                        if (!had0) { Orig[nm] = cur0!.Value.ToString(); OrigAt[nm] = vp0; }
                        succ++;
                        Log("Inject", nm, "OK (resolved) \u2192 " + fl.Value);
                    }
                    else { fail++; Failed.Add(nm); Log("Inject", nm, "found but write refused" + MemIO.WhyStr()); }
                }
            }
            WrAt = Clock.Tick; InjectSucc = succ; InjectFail = fail;
            if (FfmPanel.ReApply && succ > 0) ReApplyBurst();
            int total = Ffm.OnCount();
            if (!silent)
            {
                if (succ == 0) Ffm.Say("INJECT FAILED  " + fail + " ERRORS", C_ACC);
                else if (fail > 0) Ffm.Say("INJECTED " + succ + "/" + total + "  (" + fail + " FAILED)", AMBER);
                else Ffm.Say("INJECTED " + succ + "/" + total + " FLAGS", C_ON);
                Poke();
            }
            return succ;
        }
        finally { Busy = false; }
    }

    // ---- UNINJECT ----
    public static int Uninject(bool silent = false)
    {
        if (Busy) { if (!silent) Ffm.Say("INJECTION IN PROGRESS - TRY AGAIN IN A MOMENT", AMBER); return 0; }
        if (Pid == 0 || MemIO.HProcess == IntPtr.Zero) { if (!silent) Ffm.Say("ROBLOX NOT ATTACHED", C_ACC); return 0; }
        if (!MemIO.IsAlive()) { Poll(); if (!silent) Ffm.Say("ROBLOX NOT ATTACHED", C_ACC); return 0; }
        if (Orig.Count == 0) { if (!silent) Ffm.Say("NOTHING TO UNINJECT", AMBER); return 0; }
        Busy = true;
        try
        {
            var hdr = FlagSingleton.Header(!FfmPanel.UseSingleton);
            if (hdr is null || hdr.List == 0 || hdr.Mask == 0) { Ffm.Say(FlagSingleton.ShapeGaveUp ? "FLAG TABLE SCAN TIMED OUT" : "FLAG TABLE NOT FOUND", C_ACC); return 0; }
            _autoInjGen++;
            bool reOff = false;
            if (FfmPanel.ReApply) { FfmPanel.ReApply = false; Ini.Write(Paths.IniFile, "ffm", "reapply", 0); ManageReApply(); reOff = true; }
            var idx = FlagSingleton.Index(hdr);
            var hp0 = MemIO.HProcess; int pid0 = Pid;
            int ok = 0, bad = 0, held = 0;
            var done = new List<string>();
            foreach (var (nm, val) in Orig.ToList())
            {
                if (!SameProc(hp0, pid0))
                {
                    Ffm.Say("UNINJECT ABORTED - ROBLOX RESTARTED MID-WRITE" + (reOff ? "  -  RE-APPLY TURNED OFF" : ""), C_ACC);
                    Log("Uninject", "-", "aborted after " + ok + " flag(s): process changed");
                    foreach (var nm2 in done) Orig.Remove(nm2);
                    return 0;
                }
                string bn = Ffm.BareOf(nm);
                long vp = OrigAt.TryGetValue(nm, out var oa) ? oa : 0;
                if (!FFlags.Ptr(vp)) { vp = FFlags.FindValuePtr(bn, hdr); if (vp == 0 && idx.TryGetValue(bn, out var vpr)) vp = MemIO.ReadU64(vpr + 0xC0); }
                bool wrote; string back;
                if (val.StartsWith("str:"))
                {
                    wrote = FFlags.Ptr(vp) && FFlags.PutRaw(vp, val[4..]);
                    var so = wrote ? FFlags.StrObj(vp) : null;
                    back = so is not null ? "str:" + so.Hex : "";
                }
                else
                {
                    int wu = WidthOf(nm);
                    wrote = FFlags.Ptr(vp) && FFlags.SetAtW(vp, val, wu);
                    back = wrote ? (MemIO.ReadChk(vp, wu)?.ToString() ?? "") : "";
                }
                if (!wrote) { bad++; Log("Uninject", nm, "WRITE FAILED" + MemIO.WhyStr()); continue; }
                if (back == "") { bad++; Log("Uninject", nm, "wrote " + val + " but could not verify"); continue; }
                if (back == val) { ok++; done.Add(nm); Log("Uninject", nm, "restored \u2192 " + val); }
                else { held++; Log("Uninject", nm, "held at " + back + " by Roblox (wanted " + val + ")"); }
            }
            foreach (var nm in done) Orig.Remove(nm);
            Failed = new();
            if (Orig.Count == 0) { InjectSucc = 0; InjectFail = 0; } else { InjectSucc = Orig.Count; InjectFail = 0; }
            WrAt = Clock.Tick;
            int others = Ps.Count(kv => kv.Key != Pid && kv.Value.Orig.Count > 0 && Exists(kv.Key));
            string note = (reOff ? "  -  RE-APPLY TURNED OFF" : "") + (others > 0 ? "  -  " + others + " OTHER CLIENT(S) STILL INJECTED" : "");
            if (silent) { }
            else if (ok == 0 && held == 0) Ffm.Say("UNINJECT FAILED  " + bad + " ERRORS" + note, C_ACC);
            else if (held > 0) Ffm.Say("UNINJECTED " + ok + " - " + held + " HELD BY ROBLOX, RESTART TO CLEAR" + note, AMBER);
            else if (bad > 0) Ffm.Say("UNINJECTED " + ok + "  (" + bad + " FAILED)" + note, AMBER);
            else Ffm.Say("UNINJECTED " + ok + " - RESTART RBX FOR GRAPHICS FLAGS" + note, C_ON);
            Poke();
            return ok;
        }
        finally { Busy = false; }
    }
    public static void UninjectAll()
    {
        if (Busy) { Ffm.Say("INJECTION IN PROGRESS - TRY AGAIN IN A MOMENT", AMBER); return; }
        var lst = ProcList(out _);
        if (lst.Count == 0) { Ffm.Say("NO ROBLOX INSTANCES", C_ACC); return; }
        var todo = lst.Where(pid => (pid == Pid ? Orig.Count : Ps.TryGetValue(pid, out var ps) ? ps.Orig.Count : 0) > 0).ToList();
        if (todo.Count == 0) { Ffm.Say("NOTHING TO UNINJECT", AMBER); return; }
        int was = Pid, done = 0, tot = 0;
        foreach (var pid in todo)
        {
            if (!UseInstance(pid)) continue;
            int n = Uninject(true);
            if (n > 0) { done++; tot += n; }
        }
        if (was != 0 && was != Pid && lst.Contains(was)) UseInstance(was);
        Ffm.Say(done > 0 ? "UNINJECTED " + done + " INSTANCE(S) - " + tot + " FLAG(S) PUT BACK  -  RESTART RBX FOR GRAPHICS FLAGS" : "NOTHING WAS PUT BACK", done > 0 ? C_ON : C_ACC);
        Poke();
    }
    public static bool AnyInjected() => Orig.Count > 0 || Ps.Any(kv => kv.Value.Orig.Count > 0 && Exists(kv.Key));

    // ---- CLOSE RBX ----
    public static void CloseRoblox()
    {
        if (!Roblox.IsRunning()) { Ffm.Say("ROBLOX IS NOT RUNNING", AMBER); return; }
        MemIO.Detach(); FlagSingleton.Invalidate();
        int killed = Roblox.Kill(500);
        Rbx = false; RbxAt = Clock.Tick; Pid = 0; Orig = new(); OrigAt = new();
        try { Modules.Special.SpfEngine.PurgeSessions(); } catch { }
        // a FRESH look, not the cached one - see Roblox.Kill
        Ffm.Say(Roblox.IsRunningNow() ? "COULD NOT CLOSE ROBLOX - TRY RUNNING AS ADMIN" : "ROBLOX CLOSED" + (killed > 1 ? "  (" + killed + " CLIENTS)" : ""), C_ACC);
        Poke();
    }

    // ---- the re-apply watchdog ----
    public static void ManageReApply()
    {
        if (!FfmPanel.ReApply) { _ra?.Stop(); _raBurst = 0; return; }
        ReApplyArm(RA_IDLE);
    }
    static void ReApplyArm(int ms)
    {
        if (!FfmPanel.ReApply) return;
        _ra ??= new DispatcherTimer();
        _ra.Stop();
        _ra.Interval = TimeSpan.FromMilliseconds(ms);
        _ra.Tick -= OnRa; _ra.Tick += OnRa;
        _ra.Start();
    }
    static void OnRa(object? s, EventArgs e) { _ra?.Stop(); try { ReApplyTick(); } catch (Exception ex) { Log("ReApply", "-", "fault: " + ex.Message); ReApplyArm(RA_IDLE); } }
    public static void ReApplyBurst() { _raBurst = RA_BURSTN; _raQuiet = 0; ReApplyArm(RA_BURST); }
    static int ReApplyIdle() => _raQuiet < 5 ? RA_IDLE : _raQuiet < 15 ? 3000 : 5000;
    static List<int> ReApplyTargets() => InstList.Where(pid => pid == Pid ? Orig.Count > 0 : Ps.TryGetValue(pid, out var ps) && ps.Orig.Count > 0).ToList();
    static void ReApplyTick()
    {
        if (!FfmPanel.ReApply) return;
        var tg = ReApplyTargets();
        if (tg.Count > 0)
        {
            _raRot = _raRot % tg.Count + 1;
            int want = tg[_raRot - 1];
            if (want != Pid)
            {
                int back = Pid;
                try { if (UseInstance(want, true)) ReApplyRun(); }
                finally { if (back != 0 && Exists(back)) UseInstance(back, true); ReApplyArm(_raBurst > 0 ? RA_BURST : RA_IDLE); }
                return;
            }
        }
        ReApplyRun();
    }
    static void ReApplyRun()
    {
        if (!FfmPanel.ReApply) return;
        if (Pid == 0 || MemIO.HProcess == IntPtr.Zero || Ffm.Flags.Count == 0 || Orig.Count == 0) { ReApplyArm(RA_IDLE); return; }
        if (Busy) { ReApplyArm(_raBurst > 0 ? RA_BURST : RA_IDLE); return; }
        if (!MemIO.IsAlive()) { Poll(); ReApplyArm(RA_IDLE); return; }
        var hdr = FlagSingleton.Header(!FfmPanel.UseSingleton);
        if (hdr is null || hdr.List == 0 || hdr.Mask == 0) { ReApplyArm(RA_IDLE); return; }
        string key = hdr.List + "|" + hdr.Mask;
        if (key != RaKey)
        {
            if (RaKey != "") { Log("ReApply", "-", "flag table rebuilt - holding at " + RA_BURST + "ms"); _raBurst = RA_BURSTN; }
            RaKey = key; _raQuiet = 0; RaDrift = new(); RaStuck = new(); RaSkip = new(); RaSkipN = new(); RaVP = new();
        }
        Busy = true;
        try
        {
            var hp0 = MemIO.HProcess; int pid0 = Pid;
            var idx = FlagSingleton.Index(hdr);
            int restored = 0, held = 0, stuck = 0, worstN = 0; string worst = "";
            foreach (var fl in Ffm.Flags.ToList())
            {
                if (!SameProc(hp0, pid0)) break;
                string nm = fl.Name;
                if (!fl.On || !Orig.ContainsKey(nm)) continue;
                if (RaSkip.TryGetValue(nm, out var sk) && sk > 0) { RaSkip[nm] = sk - 1; continue; }
                string bare = Ffm.BareOf(nm);
                var dv = FFlags.Coerce(fl.Value);
                if (dv is null) continue;
                long vp;
                if (OrigAt.TryGetValue(nm, out var oa)) vp = oa;
                else if (RaVP.TryGetValue(nm, out var rv)) vp = rv;
                else { long vpr = idx.TryGetValue(bare, out var v2) ? v2 : 0; vp = vpr != 0 ? MemIO.ReadU64(vpr + 0xC0) : 0; if (vp != 0) RaVP[nm] = vp; }
                if (vp == 0) continue;
                int w = WidthOf(nm);
                var cur = MemIO.ReadChk(vp, w);
                if (cur is null) continue;
                if (cur == dv) { held++; RaStuck.Remove(nm); continue; }
                if (FFlags.SetAtW(vp, fl.Value, w))
                {
                    var back = MemIO.ReadChk(vp, w);
                    if (back is null || back != dv)
                    {
                        int n = (RaStuck.TryGetValue(nm, out var st) ? st : 0) + 1;
                        RaStuck[nm] = n; stuck++;
                        RaVP.Remove(nm);
                        if (n >= 3) { int bo = RaSkipN.TryGetValue(nm, out var sn) ? Math.Min(sn * 2, 32) : 2; RaSkipN[nm] = bo; RaSkip[nm] = bo; }
                        if (n <= 3) Log("ReApply", nm, "write did not take (read back " + (back is null ? "unreadable" : back.ToString()) + ")");
                    }
                    else
                    {
                        restored++;
                        RaStuck.Remove(nm); RaSkip.Remove(nm); RaSkipN.Remove(nm);
                        int d = (RaDrift.TryGetValue(nm, out var dd) ? dd : 0) + 1;
                        RaDrift[nm] = d;
                        if (d > worstN) { worstN = d; worst = nm; }
                        Log("ReApply", nm, "restored -> " + fl.Value + (d > 1 ? "  (x" + d + ")" : ""));
                    }
                }
            }
            _raHeld = held;
            if (restored > 0 || stuck > 0) { _raBurst = RA_BURSTN; _raQuiet = 0; }
            else { if (_raBurst > 0) _raBurst--; _raQuiet++; }
            long now = Clock.Tick;
            if ((restored > 0 || stuck > 0) && now - _raSaidAt > 4000)
            {
                _raSaidAt = now;
                if (stuck > 0 && restored == 0) Ffm.Say("CANNOT HOLD " + stuck + " FLAG" + (stuck == 1 ? "" : "S") + " - ROBLOX IS REWRITING THEM", C_ACC);
                else if (worstN > 2) Ffm.Say("HOLDING " + Ffm.BareOf(worst) + " - PUT BACK " + worstN + " TIME" + (worstN == 1 ? "" : "S"), AMBER);
                else Ffm.Say("RE-APPLY RESTORED " + restored + " FLAG" + (restored == 1 ? "" : "S"), AMBER);
                Poke();
            }
        }
        finally { Busy = false; ReApplyArm(_raBurst > 0 ? RA_BURST : ReApplyIdle()); }
    }
}
