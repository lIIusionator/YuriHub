using System.Globalization;
using System.Text;

namespace Yuri.Core;

/// <summary>
/// IniRead / IniWrite / IniDelete with AutoHotkey's semantics, so an existing
/// zeal.ini carries over untouched: sections and keys case-insensitive, values
/// stored as written (no quoting), a missing key returns the default. Encoding
/// follows the file (UTF-16 LE / UTF-8 / ANSI by BOM); a new file is written
/// UTF-16 LE with a BOM, which is what AutoHotkey v2 creates.
/// </summary>
public static class Ini
{
    sealed class Doc
    {
        public readonly List<string> Lines = new();
        public Encoding Enc = new UnicodeEncoding(false, true);
        public DateTime Stamp;
    }
    static readonly Dictionary<string, Doc> Cache = new(StringComparer.OrdinalIgnoreCase);
    static readonly object Gate = new();

    public static string Read(string file, string section, string key, string def = "")
    {
        lock (Gate)
        {
            var d = Load(file);
            if (d is null) return def;
            int s = FindSection(d, section);
            if (s < 0) return def;
            for (int i = s + 1; i < d.Lines.Count; i++)
            {
                var ln = d.Lines[i];
                if (IsSectionLine(ln)) break;
                if (Split(ln, out var k, out var v) && string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                    return v;
            }
            return def;
        }
    }

    /// <summary>Integer(IniRead(...)): decimal or 0x-hex; the default on anything else.</summary>
    public static long ReadInt(string file, string section, string key, long def)
    {
        var s = Read(file, section, key, "").Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && long.TryParse(s[2..], NumberStyles.HexNumber, null, out var h)) return h;
        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return v;
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) return (long)f;
        return def;
    }

    /// <summary>An ARGB colour the .ahk wrote as "0xFFFB7185".</summary>
    public static uint ReadArgb(string file, string section, string key, uint def)
    {
        var v = ReadInt(file, section, key, long.MinValue);
        return v == long.MinValue ? def : unchecked((uint)v);
    }

    /// <summary>Number(IniRead(...)).</summary>
    public static double ReadNum(string file, string section, string key, double def)
        => double.TryParse(Read(file, section, key, "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;

    /// <summary>The whole section as key/value pairs, in file order (IniRead with no key).</summary>
    public static List<KeyValuePair<string, string>> ReadSection(string file, string section)
    {
        var res = new List<KeyValuePair<string, string>>();
        lock (Gate)
        {
            var d = Load(file);
            if (d is null) return res;
            int s = FindSection(d, section);
            if (s < 0) return res;
            for (int i = s + 1; i < d.Lines.Count; i++)
            {
                var ln = d.Lines[i];
                if (IsSectionLine(ln)) break;
                if (Split(ln, out var k, out var v)) res.Add(new(k, v));
            }
        }
        return res;
    }

    public static bool Write(string file, string section, string key, string value)
    {
        lock (Gate)
        {
            var d = Load(file) ?? new Doc();
            int s = FindSection(d, section);
            if (s < 0)
            {
                if (d.Lines.Count > 0 && d.Lines[^1].Trim() != "") d.Lines.Add("");
                d.Lines.Add("[" + section + "]");
                d.Lines.Add(key + "=" + value);
                return Save(file, d);
            }
            int end = d.Lines.Count;
            for (int i = s + 1; i < d.Lines.Count; i++)
            {
                var ln = d.Lines[i];
                if (IsSectionLine(ln)) { end = i; break; }
                if (Split(ln, out var k, out _) && string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                {
                    d.Lines[i] = key + "=" + value;
                    return Save(file, d);
                }
            }
            int ins = end;
            while (ins > s + 1 && d.Lines[ins - 1].Trim() == "") ins--;
            d.Lines.Insert(ins, key + "=" + value);
            return Save(file, d);
        }
    }

    public static bool Write(string file, string section, string key, double value)
        => Write(file, section, key, value.ToString("0.###", CultureInfo.InvariantCulture));
    public static bool Write(string file, string section, string key, long value)
        => Write(file, section, key, value.ToString(CultureInfo.InvariantCulture));
    public static bool WriteArgb(string file, string section, string key, uint argb)
        => Write(file, section, key, "0x" + argb.ToString("X8"));

    public static bool Delete(string file, string section, string? key = null)
    {
        lock (Gate)
        {
            var d = Load(file);
            if (d is null) return true;
            int s = FindSection(d, section);
            if (s < 0) return true;
            int end = d.Lines.Count;
            for (int i = s + 1; i < d.Lines.Count; i++)
                if (IsSectionLine(d.Lines[i])) { end = i; break; }
            if (key is null) { d.Lines.RemoveRange(s, end - s); return Save(file, d); }
            for (int i = s + 1; i < end; i++)
                if (Split(d.Lines[i], out var k, out _) && string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                { d.Lines.RemoveAt(i); return Save(file, d); }
            return true;
        }
    }

    // ---- file ----
    static Doc? Load(string file)
    {
        if (!File.Exists(file)) { Cache.Remove(file); return null; }
        var stamp = File.GetLastWriteTimeUtc(file);
        if (Cache.TryGetValue(file, out var d) && d.Stamp == stamp) return d;
        try
        {
            var bytes = File.ReadAllBytes(file);
            var doc = new Doc { Stamp = stamp };
            Encoding enc; int skip;
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) { enc = new UnicodeEncoding(false, true); skip = 2; }
            else if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) { enc = new UTF8Encoding(true); skip = 3; }
            else { enc = LooksUtf8(bytes) ? new UTF8Encoding(false) : Encoding.Latin1; skip = 0; }
            doc.Enc = enc;
            var text = enc.GetString(bytes, skip, bytes.Length - skip);
            foreach (var ln in text.Split('\n')) doc.Lines.Add(ln.TrimEnd('\r'));
            if (doc.Lines.Count > 0 && doc.Lines[^1] == "") doc.Lines.RemoveAt(doc.Lines.Count - 1);
            Cache[file] = doc;
            return doc;
        }
        catch { return null; }
    }

    static bool Save(string file, Doc d)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var text = string.Join("\r\n", d.Lines) + "\r\n";
            var pre = d.Enc.GetPreamble();
            var body = d.Enc.GetBytes(text);
            var all = new byte[pre.Length + body.Length];
            pre.CopyTo(all, 0); body.CopyTo(all, pre.Length);
            var tmp = file + ".tmp";
            File.WriteAllBytes(tmp, all);
            File.Move(tmp, file, true);
            d.Stamp = File.GetLastWriteTimeUtc(file);
            Cache[file] = d;
            return true;
        }
        catch { return false; }
    }

    static bool LooksUtf8(byte[] b)
    {
        try { new UTF8Encoding(false, true).GetString(b); return true; } catch { return false; }
    }
    static bool IsSectionLine(string ln) { var t = ln.Trim(); return t.Length >= 2 && t[0] == '[' && t[^1] == ']'; }
    static int FindSection(Doc d, string section)
    {
        for (int i = 0; i < d.Lines.Count; i++)
        {
            var t = d.Lines[i].Trim();
            if (t.Length >= 2 && t[0] == '[' && t[^1] == ']' && string.Equals(t[1..^1], section, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
    static bool Split(string ln, out string key, out string val)
    {
        key = val = "";
        var t = ln.TrimStart();
        if (t.Length == 0 || t[0] == ';' || t[0] == '[') return false;
        int eq = t.IndexOf('=');
        if (eq < 0) return false;
        key = t[..eq].Trim();
        val = t[(eq + 1)..];
        return key.Length > 0;
    }
}
