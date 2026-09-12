using System.Diagnostics;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Yuri.Core;
using Yuri.Platform;

namespace Yuri.Modules.Special;

/// <summary>
/// DISCORD ACTIVITY over Discord's local IPC — discord-ipc-0..9 as a named
/// pipe on Windows, as a unix socket under $TMPDIR (and the Flatpak / Snap
/// paths) on macOS. Frames are op (int32) + length (int32) + JSON. The
/// handshake sends the application id; READY opens the link; a CLOSE or
/// "Invalid client ID" marks the id bad for the session. The presence is
/// rebuilt from the session and sent only when its key changed, at most
/// every three seconds; leaving a game clears it. SPFDcOpen / Frame / Read
/// / Msg / Close / Push, with the pipe read made non-blocking.
/// </summary>
public static class SpfDiscord
{
    public const string APP = "1538773253810683994";                   // SPF_APP
    public static string AppId = APP;                                   // spfDiscordApp
    const int FOC_HOLD = 6000;                                          // SPF_FOC_HOLD
    static Stream? _pipe; static Socket? _sock;
    static string _sent = "", _badId = ""; static long _at, _pushAt;
    static readonly byte[] _rbuf = new byte[262144 + 8]; static int _rlen;
    static int _focRaw = -1; static long _focAt;
    public static bool Open => _pipe is not null;
    public static string Thumb = "";                                    // SPF.thumb: the large image (the game's icon url)

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    /// <summary>SPFDcOpen(): connect and hand over the application id.</summary>
    static bool OpenLink()
    {
        if (_pipe is not null) return true;
        if (AppId == "") { Fail("no application id"); return false; }
        if (AppId.Length < 17) { Fail("id looks incomplete"); return false; }
        if (AppId == _badId) { Fail("not an application id - use the Application ID from discord.com/developers"); return false; }
        for (int i = 0; i < 10; i++)
        {
            try
            {
                if (Os.IsWin)
                {
                    var p = new NamedPipeClientStream(".", "discord-ipc-" + i, PipeDirection.InOut, PipeOptions.Asynchronous);
                    p.Connect(120);
                    _pipe = p;
                }
                else
                {
                    string? path = null;
                    string tmp = Environment.GetEnvironmentVariable("TMPDIR") ?? Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? "/tmp";
                    foreach (var cand in new[] { Path.Combine(tmp, "discord-ipc-" + i), Path.Combine(tmp, "app", "com.discordapp.Discord", "discord-ipc-" + i), Path.Combine(tmp, "snap.discord", "discord-ipc-" + i) })
                        if (File.Exists(cand)) { path = cand; break; }
                    if (path is null) continue;
                    var s = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    s.Connect(new UnixDomainSocketEndPoint(path));
                    _sock = s;
                    _pipe = new NetworkStream(s, true);
                }
            }
            catch { continue; }
            if (Frame(0, "{\"v\":1,\"client_id\":\"" + AppId + "\"}")) { Spf.Dc = 1; Spf.DcErr = ""; return true; }
            Close();
            Fail("handshake refused");
            return false;
        }
        Fail("discord not running");
        return false;
    }
    static void Fail(string why) { Spf.Dc = 3; Spf.DcErr = why; _at = Clock.Tick; }

    /// <summary>SPFDcFrame(op, json).</summary>
    static bool Frame(int op, string json)
    {
        if (_pipe is null) return false;
        var body = Encoding.UTF8.GetBytes(json);
        var buf = new byte[8 + body.Length];
        BitConverter.GetBytes(op).CopyTo(buf, 0); BitConverter.GetBytes(body.Length).CopyTo(buf, 4); body.CopyTo(buf, 8);
        try { _pipe.Write(buf, 0, buf.Length); _pipe.Flush(); return true; }
        catch (Exception ex) { Close(); Fail(PipeErr(ex, "write")); return false; }
    }
    static string PipeErr(Exception ex, string where)
    {
        string w = " [" + where + "]";
        if (ex is IOException) return "discord hung up - check the application id" + w;
        return "pipe error " + ex.GetType().Name + w;
    }

    /// <summary>SPFDcRead(): whatever Discord has answered so far, without blocking.</summary>
    static void Read()
    {
        for (int k = 0; k < 8; k++)
        {
            if (_pipe is null) return;
            try
            {
                int avail = Available();
                if (avail <= 0) { if (_rlen < 8) return; }
                else
                {
                    int room = _rbuf.Length - _rlen;
                    if (room <= 0) { Close(); Fail("bad frame"); return; }
                    int n = _pipe.Read(_rbuf, _rlen, Math.Min(room, avail));
                    if (n <= 0) return;
                    _rlen += n;
                }
                while (_rlen >= 8)
                {
                    int op = BitConverter.ToInt32(_rbuf, 0), ln = BitConverter.ToInt32(_rbuf, 4);
                    if (ln < 0 || ln > 262144) { Close(); Fail("bad frame"); return; }
                    if (_rlen < 8 + ln) break;
                    string pay = ln > 0 ? Encoding.UTF8.GetString(_rbuf, 8, ln) : "";
                    Array.Copy(_rbuf, 8 + ln, _rbuf, 0, _rlen - 8 - ln); _rlen -= 8 + ln;
                    Msg(op, pay);
                    if (_pipe is null) return;
                }
                if (avail <= 0) return;
            }
            catch (Exception ex) { Close(); Fail(PipeErr(ex, "read")); return; }
        }
    }
    static int Available()
    {
        if (_sock is not null) return _sock.Available;
        if (_pipe is NamedPipeClientStream np)
        {
            // PeekNamedPipe tells how much waits; a blocking Read would stall the tick
            var h = np.SafePipeHandle.DangerousGetHandle();
            return PeekNamedPipe(h, IntPtr.Zero, 0, IntPtr.Zero, out uint avail, IntPtr.Zero) ? (int)avail : 0;
        }
        return 0;
    }
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool PeekNamedPipe(IntPtr h, IntPtr buf, uint size, IntPtr read, out uint avail, IntPtr left);

    static void Msg(int op, string pay)
    {
        if (op == 2)
        {
            var m = Regex.Match(pay, "\"message\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            string msg = m.Success ? "discord: " + Ffm_Unesc(m.Groups[1].Value) : "discord closed the link - application id not recognised";
            if (msg.Contains("lient ID") || msg.Contains("lient id")) _badId = AppId;
            Close(); Fail(msg);
            return;
        }
        if (Regex.IsMatch(pay, "\"evt\"\\s*:\\s*\"READY\"")) { Spf.Dc = 2; Spf.DcErr = ""; _sent = ""; return; }
        if (pay.Contains("Invalid client ID") || pay.Contains("invalid client id")) { _badId = AppId; Close(); Fail("discord: invalid application id - see zeal.ini note"); return; }
        if (Regex.IsMatch(pay, "\"evt\"\\s*:\\s*\"ERROR\""))
        {
            var m = Regex.Match(pay, "\"message\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            Spf.DcErr = m.Success ? Ffm_Unesc(m.Groups[1].Value) : "discord rejected the update";
            Spf.Dc = 3; _at = Clock.Tick; _sent = "";
        }
    }
    static string Ffm_Unesc(string s) => FastFlags.Ffm.Unesc(s);

    /// <summary>SPFDcClose(): a polite CLOSE frame, then the handle.</summary>
    public static void Close()
    {
        var p = _pipe; var s = _sock;
        _pipe = null; _sock = null; _sent = ""; _rlen = 0;
        if (p is not null)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes("{}");
                var buf = new byte[8 + body.Length];
                BitConverter.GetBytes(2).CopyTo(buf, 0); BitConverter.GetBytes(body.Length).CopyTo(buf, 4); body.CopyTo(buf, 8);
                p.Write(buf, 0, buf.Length); p.Flush();
            }
            catch { }
            try { p.Dispose(); } catch { }
            try { s?.Dispose(); } catch { }
        }
        if (Spf.Dc == 2) Spf.Dc = 0;
    }

    /// <summary>SPFFocusTick(): whether the client is the window in front, held six seconds before it changes.</summary>
    public static void FocusTick()
    {
        if (!Spf.Discord || !Spf.DcFocus) { _focRaw = -1; _focAt = 0; return; }
        int f = 0;
        if (Os.IsWin)
        {
            try
            {
                var hw = GetForegroundWindow();
                if (hw != IntPtr.Zero && GetWindowThreadProcessId(hw, out uint pid) != 0)
                    try { f = Process.GetProcessById((int)pid).ProcessName.Equals(Os.RobloxProcess, StringComparison.OrdinalIgnoreCase) ? 1 : 0; } catch { }
            }
            catch { }
        }
        if (f != _focRaw) { _focRaw = f; _focAt = Clock.Tick; }
        if (_focAt != 0 && Clock.Tick - _focAt >= FOC_HOLD && (Spf.FocOn ? 1 : 0) != f) { Spf.FocOn = f == 1; Spf.Pulse = Clock.Tick; }
    }

    static string JsonEsc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "").Replace("\t", "\\t");
    static string Clamp(string v, int n)
    {
        v = v.Trim();
        if (v == "") return "--";
        if (v.Length < 2) return v + ".";
        return v.Length > n ? v[..(n - 1)] + "\u2026" : v;
    }
    static string JoinUrl() => Spf.Place == "" || Spf.Job == "" ? "" : "https://www.roblox.com/games/start?placeId=" + Spf.Place + "&gameInstanceId=" + Spf.Job;

    /// <summary>SPFDcPush(): the presence, when something about it changed.</summary>
    public static void Push()
    {
        if (!Spf.Discord) return;
        if (Spf.MultiBlock?.Invoke() == true) { Close(); return; }
        if (Spf.Dc == 3 && Clock.Tick - _at < 10000) return;
        if (_pipe is null && !OpenLink()) return;
        Read();
        if (_pipe is null || Spf.Dc != 2) return;
        if (!Spf.InGame)
        {
            if (_sent != "clear")
            {
                Frame(1, "{\"cmd\":\"SET_ACTIVITY\",\"args\":{\"pid\":" + Environment.ProcessId + ",\"activity\":null},\"nonce\":\"" + Clock.Tick + "\"}");
                _sent = "clear";
            }
            return;
        }
        const string sPin = "\U0001F4CD", sWho = "\U0001F464", sPad = "\U0001F3AE", sDot = "  \u2022  ", sHunt = "\U0001F501", sZzz = "\U0001F4A4", sDoor = "\U0001F6AA";
        var r = GameInfo.Get(Spf.Place);
        string creator = SpfEngine.Creator;
        if (r is not null && r.Icon != "" && Thumb == "") Thumb = r.Icon;
        string det = Spf.GameNm != "" ? Spf.GameNm : "Place " + Spf.Place;
        bool haveR = Spf.RegionText != "";
        string st;
        if (Spf.Match && Spf.MmTries != 0 && !SpfMatch.MatchOK()) st = sHunt + " Looking for a closer server (" + Spf.MmTries + "/" + SpfMatch.MM_MAX + ")";
        else if (haveR) st = sPin + " " + Spf.RegionText + (SpfEngine.SrvKm >= 0 ? sDot + SpfEngine.SrvKm + " km" : "") + (creator != "" ? sDot + "by " + creator : "");
        else if (creator != "") st = sWho + " by " + creator;
        else st = sPad + " In a Roblox server";
        string tip = det + (creator != "" ? sDot + "by " + creator : "") + (haveR ? sDot + Spf.RegionText : "");
        string joinUrl = Spf.DcJoin ? JoinUrl() : "";
        string acct = Spf.DcAcct && Spf.UserNm != "" ? sWho + " @" + Spf.UserNm : "";
        if (acct != "") { st += sDot + acct; tip += sDot + acct; }
        if (joinUrl != "") tip += sDot + sDoor + " open to join";
        if (Spf.DcFocus) { st += sDot + (Spf.FocOn ? sPad + " In Roblox" : sZzz + " Tabbed out"); tip += sDot + (Spf.FocOn ? "focused" : "not focused"); }
        int nCli = SpfEngine.RbxCount();
        if (nCli > 1) { st += sDot + sPad + " " + nCli + " instances"; tip += sDot + nCli + " roblox instances open"; }
        det = Clamp(det, 128); st = Clamp(st, 128); tip = Clamp(tip, 128);
        string flag = Spf.Region && Spf.RegionCC != "" ? "https://flagcdn.com/w80/" + Spf.RegionCC.ToLowerInvariant() + ".png" : "";
        string thumbUrl = r is not null && r.Universe != "" ? "https://thumbnails.roblox.com/v1/games/icons?universeIds=" + r.Universe + "&size=256x256&format=Png" : "";
        string key = det + "|" + st + "|" + tip + "|" + flag + "|" + SpfEngine.CreatorId + "|" + Spf.MmTries + "|" + joinUrl + "|" + nCli;
        if (_sent == key) return;
        if (_pushAt != 0 && Clock.Tick - _pushAt < 3000) return;
        long ts = 0;
        if (SpfEngine.JoinAt != 0) ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (long)Math.Round((Clock.Tick - SpfEngine.JoinAt) / 1000.0);
        var assets = new StringBuilder();
        if (flag != "")
        {
            assets.Append(",\"assets\":{\"small_image\":\"").Append(JsonEsc(flag)).Append("\",\"small_text\":\"").Append(JsonEsc(Clamp(Spf.RegionText, 128))).Append("\"}");
        }
        var bl = new List<string>();
        if (joinUrl != "") bl.Add("{\"label\":\"Join my server\",\"url\":\"" + JsonEsc(joinUrl) + "\"}");
        if (Spf.Place != "") bl.Add("{\"label\":\"See game page\",\"url\":\"https://www.roblox.com/games/" + Spf.Place + "\"}");
        if (bl.Count > 2) bl.RemoveRange(2, bl.Count - 2);
        string btn = bl.Count > 0 ? ",\"buttons\":[" + string.Join(",", bl) + "]" : "";
        string json = "{\"cmd\":\"SET_ACTIVITY\",\"args\":{\"pid\":" + Environment.ProcessId + ",\"activity\":{\"details\":\"" + JsonEsc(det) + "\",\"state\":\"" + JsonEsc(st) + "\""
                    + (ts != 0 ? ",\"timestamps\":{\"start\":" + ts + "}" : "") + assets + btn + "}},\"nonce\":\"" + Clock.Tick + "\"}";
        if (Frame(1, json)) { _sent = key; Spf.Pulse = Clock.Tick; _pushAt = Clock.Tick; }
    }
}
