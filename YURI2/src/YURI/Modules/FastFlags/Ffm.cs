using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Yuri.Core;
using Yuri.Platform;

namespace Yuri.Modules.FastFlags;

/// <summary>One staged flag: name (fully prefixed), value, type, and whether it is written.</summary>
public sealed class Flag
{
    public string Name = "", Value = "", Type = "bool";
    public bool On = true;
    public Flag Clone() => new() { Name = Name, Value = Value, Type = Type, On = On };
}

/// <summary>A snapshot of the staged list, taken before every destructive act.</summary>
public sealed record Snapshot(string At, string T, string Act, List<Flag> Flags);

/// <summary>
/// FAST FLAG MANAGER - built-in module 1: the data. Names are stored FULLY
/// PREFIXED end to end (FFlagFoo, not Foo); that is what ClientAppSettings
/// requires and it removes the FFlagX/DFFlagX collision. The staged list
/// lives in YURI\flags\zeal_flags.json and its history in zeal_hist.json,
/// both in the .ahk's own line formats so a user's staged set carries over.
/// Every mutation snapshots first (FFMSnap), then saves.
///
/// Delivery is ClientSettings\ClientAppSettings.json in every installed
/// client (and the straps' Modifications folder). The live injector — the
/// PROCESS MEMORY ENGINE — is Windows-only and a separate phase.
/// </summary>
public static class Ffm
{
    public const int HSMAX = 30;                                    // FFM_HSMAX: snapshots kept
    public static readonly Regex Pfx = new("^(DynamicFastFlag|DynamicFastInt|DynamicFastString|DynamicFastLog|DFString|SFString|FString|DFFlag|SFFlag|DFInt|DFLog|SFLog|SFInt|FFlag|FInt|FLog)", RegexOptions.Compiled);
    // The .ahk read its own file back with `"value":"([^"]*)"`, which drops an
    // entry whose value holds an escaped quote - FString flags carrying JSON
    // (FStringPartTexturePackTable2022 is one) went missing on the next
    // launch. Escapes are honoured here.
    static readonly Regex Entry = new("\\{\"name\":\"((?:[^\"\\\\]|\\\\.)+)\",\"value\":\"((?:[^\"\\\\]|\\\\.)*)\",\"type\":\"([^\"]+)\"(?:,\"on\":\"([01])\")?\\}", RegexOptions.Compiled);
    static readonly Regex HistLine = new("^\\{\"at\":\"([^\"]*)\",\"t\":\"([^\"]*)\",\"act\":\"([^\"]*)\",\"flags\":\\[(.*)\\]\\}$", RegexOptions.Compiled);
    static readonly Regex Pair = new("\"(?<K>[^\"]+)\"\\s*:\\s*(?:\"(?<V>[^\"]*)\"|(?<N>[^,\\}\\s]+))", RegexOptions.Compiled);

    public static readonly List<Flag> Flags = new();                  // FFM.flags
    static readonly Dictionary<string, int> Map = new(StringComparer.OrdinalIgnoreCase);   // FFM.map: lower name -> index (0-based)
    /// <summary>Set by the panel: reset the selection and scroll when the engine switches client.</summary>
    public static Action? SelReset;
    /// <summary>FFMUseInstance hands each client its own staged list; this makes that list the working one.</summary>
    public static void SwapList(List<Flag> list)
    {
        if (ReferenceEquals(list, Flags)) return;
        Flags.Clear(); Flags.AddRange(list);
        Reindex();
    }
    public static readonly List<Snapshot> Hist = new();               // FFM.hist, newest first
    public static readonly Dictionary<int, long> Flash = new();       // FFM.flash: row -> tick
    public static int BadN;                                           // FFM.badN: values refused on the way in
    public static bool Bulk;                                          // FFM.bulk: one disk write at the end
    public static string Msg = "IDLE"; public static uint MsgCol; public static long MsgAt;   // FFMSay
    public static string Q = "", Q2 = "";                             // the database and the staged list's search
    static string _flCacheQ = "\f"; static List<int> _flCache = new();
    public static Action? Changed;                                    // the panel's cue
    /// <summary>
    /// Called after a change to the staged list. On macOS with AUTO INJECT on
    /// this rewrites the file, so the switch keeps its promise there: the client
    /// picks the flags up on its next launch without anyone pressing anything.
    /// </summary>
    static bool _inAfter;
    public static void AfterChange()
    {
        if (Yuri.Platform.Os.IsWin || !FfmPanel.AutoInject || Bulk || _inAfter) return;
        // Re-entrant by construction otherwise: WriteAppSettings can Say, and Say
        // used to come back through here.
        _inAfter = true;
        try { WriteAppSettings(); } finally { _inAfter = false; }
    }

    public static string File => Path.Combine(Paths.Flags, "zeal_flags.json");    // ffFile
    public static string HistFile => Path.Combine(Paths.Flags, "zeal_hist.json"); // ffHist

    // ---- FFMBoot ----
    public static void Boot()
    {
        Flags.Clear();
        LoadFlags();
        Reindex();
        HistLoad();
    }

    /// <summary>FFMSay(msg, col): the panel's status line; col 0 keeps the accent.</summary>
    public static void Say(string msg, uint col = 0)
    {
        Msg = msg; MsgCol = col; MsgAt = Clock.Tick;
        Changed?.Invoke();                                            // NOT AfterChange: a status line is not a change to the list
    }

    // ---- names ----
    public static bool HasPfx(string n) => Pfx.IsMatch(n);
    public static string PfxOf(string n) { var m = Pfx.Match(n); return m.Success ? m.Value : ""; }
    public static string BareOf(string n) => Pfx.Replace(n, "", 1);
    /// <summary>FFMKindOf: what the prefix says the value must be — string, bool or int.</summary>
    public static string KindOf(Flag fl)
    {
        string p = PfxOf(fl.Name);
        if (p != "") return p.Contains("String") ? "string" : p.Contains("Flag") ? "bool" : "int";
        return fl.Type == "string" ? "string" : fl.Type == "bool" ? "bool" : "int";
    }
    /// <summary>FFMDefaultFor(name): true for flags, 0 for ints and logs, "" for strings.</summary>
    public static string DefaultFor(string name)
    {
        string p = PfxOf(name);
        if (p == "" || p.Contains("Flag")) return "true";
        return p.Contains("String") ? "" : "0";
    }
    /// <summary>FFMType(v): bool / int / float / string.</summary>
    public static string TypeOf(string v)
    {
        v = v.Trim().ToLowerInvariant();
        if (v == "true" || v == "false") return "bool";
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return v.Contains('.') ? "float" : "int";
        return "string";
    }
    /// <summary>FFMNorm(v, ty): bools as True/False, everything else as written.</summary>
    public static string Norm(string v, string ty)
    {
        v = v.Trim();
        if (ty == "bool") return (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1") ? "True" : "False";
        return v;
    }
    /// <summary>FFMTypeFault(pfx, val): "" when the value fits the prefix, else why not.</summary>
    public static string TypeFault(string pfx, string val)
    {
        string lv = val.ToLowerInvariant();
        if (pfx.Contains("Flag"))
            return (lv != "true" && lv != "false" && val != "1" && val != "0") ? pfx + " holds " + val + " - expects true/false" : "";
        if (pfx.Contains("Int") || pfx.Contains("Log"))
        {
            if (!long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return pfx + " holds " + val + " - expects an integer";
            if (n > 2147483647 || n < -2147483648) return pfx + " holds " + val + " - outside 32-bit range";
        }
        return "";                                                    // String: anything is legal
    }
    /// <summary>FFMStagedMsg(r, nm): what the status line says after Add.</summary>
    public static string StagedMsg(int r, string nm)
    {
        if (r == 2) return "UPDATED " + nm;
        return KindOf(new Flag { Name = nm, Type = "int" }) == "bool" ? "STAGED " + nm : "STAGED " + nm + "  -  CLICK ITS VALUE TO SET IT";
    }
    public static string Esc(string v) => v.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    /// <summary>The inverse of Esc, one pass so "\\\"" is a backslash and a quote, not a quote.</summary>
    public static string Unesc(string s)
    {
        if (s.IndexOf('\\') < 0) return s;
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length)
            {
                char n = s[++i];
                sb.Append(n switch { 'n' => '\n', 'r' => '\r', 't' => '\t', _ => n });
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    // ---- the list ----
    public static bool On(Flag fl) => fl.On;
    public static int OnCount() { int n = 0; foreach (var fl in Flags) if (fl.On) n++; return n; }
    public static bool Staged(string name) => Map.ContainsKey(name);
    public static int IndexOf(string name) => Map.TryGetValue(name, out var i) ? i : -1;

    /// <summary>FFMAdd(name, value): 1 added, 2 updated, 0 refused. An empty value takes the prefix's default.</summary>
    public static int Add(string name, string value)
    {
        name = name.Trim();
        if (name == "") return 0;
        if (Regex.IsMatch(name, "^DFlag(?=[A-Z])")) name = name[5..];
        if (value == "") value = DefaultFor(name);
        string pfxV = PfxOf(name);
        if (pfxV != "")
        {
            string fault = TypeFault(pfxV, value.Trim());
            if (fault != "")
            {
                BadN++;
                if (!Bulk) Say(fault.ToUpperInvariant(), 0xFFFB7185);
                return 0;
            }
        }
        if (Map.TryGetValue(name, out var idx))
        {
            var fl = Flags[idx];
            fl.Value = value; fl.Type = TypeOf(value);
            if (!Bulk) { Flash[idx] = Clock.Tick; SaveFlags(); }
            _flCacheQ = "\f";
            Changed?.Invoke(); AfterChange();
            return 2;
        }
        Flags.Add(new Flag { Name = name, Value = value, Type = TypeOf(value), On = true });
        Map[name] = Flags.Count - 1;
        if (!Bulk) { Flash[Flags.Count - 1] = Clock.Tick; SaveFlags(); }
        _flCacheQ = "\f";
        Changed?.Invoke(); AfterChange();
        return 1;
    }

    /// <summary>FFMDel(i): remove row i (0-based here), after a snapshot.</summary>
    public static void Del(int i)
    {
        if (i < 0 || i >= Flags.Count) return;
        Snap("delete");
        Flags.RemoveAt(i);
        Flash.Clear();
        Reindex();
        SaveFlags();
        Changed?.Invoke(); AfterChange();
    }

    /// <summary>FFMClearAll(): everything out, into the history.</summary>
    public static void ClearAll()
    {
        int n = Flags.Count;
        if (n == 0) { Say("NOTHING STAGED", 0xFFFBBF24); return; }
        Snap("clear");
        Flags.Clear(); Flash.Clear();
        Reindex();
        SaveFlags();
        Say($"CLEARED {n} FLAG(S)  -  HISTORY HAS THEM", 0xFFFB7185);
    }

    /// <summary>FFMToggleOn(i): a switched-off flag is kept, not written.</summary>
    public static void ToggleOn(int i)
    {
        if (i < 0 || i >= Flags.Count) return;
        var fl = Flags[i];
        fl.On = !fl.On;
        Flash[i] = Clock.Tick;
        SaveFlags();
        Say(fl.On ? fl.Name + " ON" : fl.Name + " OFF - KEPT, NOT APPLIED", fl.On ? 0xFF34D399 : 0);
    }

    /// <summary>FFMFlagList(): the staged rows that match the search, cached per query.</summary>
    public static List<int> FlagList(string q)
    {
        if (_flCacheQ == q) return _flCache;
        var res = new List<int>();
        for (int i = 0; i < Flags.Count; i++)
            if (q == "" || Flags[i].Name.Contains(q, StringComparison.OrdinalIgnoreCase)) res.Add(i);
        _flCache = res; _flCacheQ = q;
        return res;
    }

    /// <summary>FFMParseInto(text): every "name": value pair in a JSON-ish text, one write at the end. Returns how many were taken.</summary>
    public static int ParseInto(string text)
    {
        int n = 0;
        BadN = 0;
        Snap("import");
        Bulk = true;
        try
        {
            foreach (Match m in Pair.Matches(text))
            {
                string v = m.Groups["V"].Success && m.Groups["V"].Value != "" ? m.Groups["V"].Value : m.Groups["N"].Value;
                if (Add(m.Groups["K"].Value, v) != 0) n++;           // a refused value is not an import
            }
        }
        finally { Bulk = false; }
        if (n > 0)
        {
            Flash.Clear();
            Reindex();
            SaveFlags();
            Changed?.Invoke(); AfterChange();
        }
        return n;
    }

    /// <summary>FFMJson(): the staged, switched-on flags as ClientAppSettings.json text.</summary>
    public static string Json()
    {
        var seen = new HashSet<string>();
        var sb = new StringBuilder("{");
        int n = 0;
        foreach (var fl in Flags)
        {
            if (!seen.Add(fl.Name)) continue;
            if (!fl.On) continue;                                     // switched off: kept, not written
            sb.Append(n > 0 ? "," : "").Append("\n    \"").Append(Esc(fl.Name)).Append("\": \"").Append(Esc(Norm(fl.Value, fl.Type))).Append('"');
            n++;
        }
        return sb.Append("\n}").ToString();
    }

    /// <summary>FFMExportFile(path): the list to a .json; false when the write failed.</summary>
    public static bool ExportFile(string path)
    {
        if (!Regex.IsMatch(path, "\\.json$", RegexOptions.IgnoreCase)) path += ".json";
        try
        {
            System.IO.File.WriteAllText(path, Json(), new UTF8Encoding(false));
            Say($"EXPORTED {Flags.Count} TO FILE", 0xFF34D399);
            return true;
        }
        catch { Say("EXPORT FAILED", 0xFFFB7185); return false; }
    }

    // ---- persistence: zeal_flags.json ----
    public static void LoadFlags()
    {
        if (!System.IO.File.Exists(File)) return;
        try
        {
            var c = System.IO.File.ReadAllText(File, Encoding.UTF8);
            foreach (Match m in Entry.Matches(c))
                Flags.Add(new Flag { Name = Unesc(m.Groups[1].Value), Value = Unesc(m.Groups[2].Value), Type = m.Groups[3].Value, On = m.Groups[4].Value != "0" });
        }
        catch { }
    }
    public static void SaveFlags()
    {
        var sb = new StringBuilder("[");
        int n = 0;
        foreach (var fl in Flags)
        {
            sb.Append(n > 0 ? "," : "").Append("{\"name\":\"").Append(Esc(fl.Name)).Append("\",\"value\":\"").Append(Esc(fl.Value)).Append("\",\"type\":\"").Append(fl.Type).Append('"')
              .Append(fl.On ? "" : ",\"on\":\"0\"").Append('}');
            n++;
        }
        sb.Append(']');
        try
        {
            Directory.CreateDirectory(Paths.Flags);
            System.IO.File.WriteAllText(File, sb.ToString(), new UTF8Encoding(false));
        }
        catch { }
    }
    /// <summary>FFMReindex(): the name map after any change to the rows.</summary>
    public static void Reindex()
    {
        Map.Clear();
        for (int i = 0; i < Flags.Count; i++) Map[Flags[i].Name] = i;
        _flCacheQ = "\f";
    }

    // ---- history: zeal_hist.json ----
    /// <summary>FFMSnap(act): the list as it is, first, so the act can be undone.</summary>
    public static void Snap(string act)
    {
        if (Flags.Count == 0) return;
        var lst = new List<Flag>(Flags.Count);
        foreach (var fl in Flags) lst.Add(fl.Clone());
        var now = DateTime.Now;
        Hist.Insert(0, new Snapshot(now.ToString("yyyyMMddHHmmss"), now.ToString("HH:mm:ss"), act, lst));
        while (Hist.Count > HSMAX) Hist.RemoveAt(Hist.Count - 1);
        HistSave();
    }
    public static void HistSave()
    {
        var sb = new StringBuilder();
        foreach (var sn in Hist)
        {
            var rec = new StringBuilder();
            foreach (var fl in sn.Flags)
                rec.Append(rec.Length > 0 ? "," : "").Append("{\"name\":\"").Append(Esc(fl.Name)).Append("\",\"value\":\"").Append(Esc(fl.Value)).Append("\",\"type\":\"").Append(fl.Type).Append('"').Append(fl.On ? "" : ",\"on\":\"0\"").Append('}');
            sb.Append("{\"at\":\"").Append(sn.At).Append("\",\"t\":\"").Append(sn.T).Append("\",\"act\":\"").Append(Esc(sn.Act)).Append("\",\"flags\":[").Append(rec).Append("]}\n");
        }
        try
        {
            Directory.CreateDirectory(Paths.Flags);
            System.IO.File.WriteAllText(HistFile, sb.ToString(), new UTF8Encoding(false));
        }
        catch { }
    }
    public static void HistLoad()
    {
        Hist.Clear();
        if (!System.IO.File.Exists(HistFile)) return;
        string body;
        try { body = System.IO.File.ReadAllText(HistFile, Encoding.UTF8); } catch { return; }
        foreach (var raw in body.Split('\n'))
        {
            var ln = raw.TrimEnd('\r');
            var h = HistLine.Match(ln);
            if (!h.Success) continue;
            var lst = new List<Flag>();
            foreach (Match m in Entry.Matches(h.Groups[4].Value))
                lst.Add(new Flag { Name = Unesc(m.Groups[1].Value), Value = Unesc(m.Groups[2].Value), Type = m.Groups[3].Value, On = m.Groups[4].Value != "0" });
            if (lst.Count > 0) Hist.Add(new Snapshot(h.Groups[1].Value, h.Groups[2].Value, h.Groups[3].Value, lst));
            if (Hist.Count >= HSMAX) break;
        }
    }
    /// <summary>FFMHistRestore(i): the list as it was before snapshot i's act; the current list is snapshotted first.</summary>
    public static void HistRestore(int i)
    {
        if (i < 0 || i >= Hist.Count) return;
        var sn = Hist[i];
        Snap("restore");                                              // the way back from the way back
        Flags.Clear(); Flash.Clear();
        foreach (var fl in sn.Flags) Flags.Add(fl.Clone());
        Reindex();
        SaveFlags();
        Say($"RESTORED {Flags.Count} FLAG(S) FROM {sn.T}  -  BEFORE {sn.Act.ToUpperInvariant()}", 0xFF34D399);
    }

    // ---- delivery: every ClientSettings folder a client reads ----
    /// <summary>RSetAppSettingsPaths(): each installed client's ClientSettings folder, plus the straps' Modifications folder.</summary>
    public static List<string> AppSettingsPaths()
    {
        var res = new List<string>();
        void Add(string d) { if (!res.Contains(d, StringComparer.OrdinalIgnoreCase)) res.Add(d); }
        if (Os.IsWin)
        {
            string lad = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "";
            if (lad == "") return res;
            var versions = Path.Combine(lad, "Roblox", "Versions");
            if (Directory.Exists(versions))
                foreach (var d in Directory.EnumerateDirectories(versions, "version-*"))
                    if (System.IO.File.Exists(Path.Combine(d, "RobloxPlayerBeta.exe"))) Add(Path.Combine(d, "ClientSettings"));
            foreach (var b in new[] { "Fishstrap", "Bloxstrap", "Voidstrap" })
            {
                var root = Path.Combine(lad, b);
                if (!Directory.Exists(root)) continue;
                Add(Path.Combine(root, "Modifications", "ClientSettings"));
                var bv = Path.Combine(root, "Versions");
                if (Directory.Exists(bv))
                    foreach (var d in Directory.EnumerateDirectories(bv, "version-*"))
                        if (System.IO.File.Exists(Path.Combine(d, "RobloxPlayerBeta.exe"))) Add(Path.Combine(d, "ClientSettings"));
            }
        }
        else if (Os.IsMac)
        {
            // the macOS client reads its own bundle's ClientSettings
            // The bundle the RUNNING client came out of first - that is the one
            // that will read the file, wherever it was installed.
            string live = Platform.Roblox.MacBundle();
            if (live != "") Add(Path.Combine(live, "Contents", "MacOS", "ClientSettings"));
            foreach (var app in new[] { "/Applications/Roblox.app", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications", "Roblox.app") })
                if (Directory.Exists(app)) Add(Path.Combine(app, "Contents", "MacOS", "ClientSettings"));
        }
        return res;
    }

    /// <summary>Write the staged flags into every delivery folder; returns how many folders took it.</summary>
    /// <summary>
    /// The file route. The PROCESS MEMORY ENGINE patches a client that is already
    /// running and needs OpenProcess and WriteProcessMemory, so it is Windows
    /// only - but the client reads ClientAppSettings.json on every launch on both
    /// platforms, and that route needs nothing but a file write. It was written
    /// and never wired to anything, which left macOS with no way to apply a flag
    /// at all.
    /// </summary>
    /// <summary>The body the file route writes, for the harness.</summary>
    public static string JsonPublic() => Json();
    /// <summary>Why the last write did not land, or "" - a swallowed exception here reads as "no client found", which is a different problem with a different fix.</summary>
    public static string WriteFault = "";
    public static int WriteAppSettings()
    {
        string body = Json();
        int n = 0; WriteFault = "";
        var paths = AppSettingsPaths();
        foreach (var d in paths)
        {
            string f = Path.Combine(d, "ClientAppSettings.json");
            try
            {
                Directory.CreateDirectory(d);
                System.IO.File.WriteAllText(f, body, new UTF8Encoding(false));
                // read it back: a write that "succeeds" into a place the client
                // does not read is the failure that looks like success
                if (System.IO.File.ReadAllText(f) == body) n++;
                else WriteFault = "the file did not keep what was written";
            }
            catch (UnauthorizedAccessException) { WriteFault = "no permission to write into " + d; }
            catch (IOException e) { WriteFault = e.Message; }
            catch (Exception e) { WriteFault = e.Message; }
        }
        if (paths.Count == 0) WriteFault = "no Roblox install found";
        return n;
    }
    /// <summary>Take the file back out. An empty object rather than a delete: the client is happier reading `{}` than finding the file gone mid-launch.</summary>
    public static int ClearAppSettings()
    {
        int n = 0;
        foreach (var d in AppSettingsPaths())
        {
            string f = Path.Combine(d, "ClientAppSettings.json");
            if (!System.IO.File.Exists(f)) continue;
            try { System.IO.File.WriteAllText(f, "{}", new UTF8Encoding(false)); n++; } catch { }
        }
        return n;
    }
    /// <summary>How many delivery folders this machine has - 0 means no client was found.</summary>
    public static int AppSettingsN() => AppSettingsPaths().Count;

    // ---- the apply the module actually performs ----
    // On Windows the live engine is the point of the module and stays the
    // default. Everywhere else the file IS the apply, so the same button does
    // the thing that platform can do rather than reporting that it cannot.
    public static void FileApply()
    {
        if (Flags.Count == 0) { Say("NOTHING STAGED", 0xFFFBBF24); return; }
        int n = WriteAppSettings();
        if (n > 0) { Say("WROTE " + OnCount() + " FLAG(S) TO " + n + " INSTALL(S)  -  RESTART ROBLOX", 0xFF34D399); return; }
        // Name the reason. "Nothing written" with no cause is the message that
        // sends someone looking for a bug in the flags.
        Say((WriteFault == "" ? "NOTHING WRITTEN" : WriteFault).ToUpperInvariant(), 0xFFFB7185);
    }
    public static void FileClear()
    {
        int n = ClearAppSettings();
        Say(n == 0 ? "NOTHING TO CLEAR" : "CLEARED " + n + " INSTALL(S)  -  RESTART ROBLOX", n == 0 ? 0xFFFBBF24 : 0xFF34D399);
    }
}
