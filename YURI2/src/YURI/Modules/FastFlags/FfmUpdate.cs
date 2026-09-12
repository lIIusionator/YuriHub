using System.Text.RegularExpressions;
using Yuri.Core;
using Yuri.Shell.Hub;

namespace Yuri.Modules.FastFlags;

/// <summary>
/// UPDATE FLAGS. Fetches the live flag list (the same three sources as the
/// database), builds a token index over it — each name split into its
/// CamelCase words, each word weighted by how rare it is (ln n/df), words
/// in more than 8% of names dropped from the postings — and, for every
/// staged name the live list does not carry, looks for the closest live
/// name by weighted Dice over the words. A match under 0.70 is too far, a
/// runner-up within 0.05 is ambiguous, a swapped word (not a dropped one)
/// is applied but flagged REVIEW, a name whose bare form exists under a
/// different prefix is PREFIX-fixed. The names as they were go to HISTORY
/// and to UNDO. This is FFMUpdateFlags/FFMUpdateGot line for line, with the
/// child process's fetch and tokenising folded in.
/// </summary>
public static class FfmUpdate
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_ACC = 0xFFFB7185, SKY = 0xFF38BDF8;
    const double MATCH_MIN = 0.70, MATCH_GAP = 0.05, MATCH_REV = 0.18, DF_CAP = 0.08;
    const int POOL_TOK = 2, POOL_MAX = 1500;
    static readonly Regex TokRx = new("[A-Z]+(?![a-z])|[A-Z][a-z]*|[0-9]+", RegexOptions.Compiled);
    static readonly Regex JsonKey = new("[{,]\\s*\"([A-Za-z0-9_]+)\"\\s*:", RegexOptions.Compiled);
    static readonly Regex FVar = new("^\\[.*?\\]\\s*(\\w+)", RegexOptions.Compiled | RegexOptions.Multiline);

    public static long UpdAt;                                          // FFM.updAt: the pass in flight
    public static List<string>? Undo;                                  // FFM.updUndo: the names before the last pass
    public static HashSet<string> Live = new(StringComparer.Ordinal); // FFM.live
    public static Dictionary<string, List<string>> BareAll = new(StringComparer.OrdinalIgnoreCase);   // FFM.bareAll: bare (lower) -> live forms
    public static long LiveAt;                                         // FFM.liveAt
    public static bool LivePartial;                                    // FFM.livePartial

    public static bool Updating => UpdAt != 0 && Clock.Tick - UpdAt < 60000;
    public static bool CanUndo => Undo is { } u && u.Count == Ffm.Flags.Count;

    /// <summary>FFMTok(s): the CamelCase words and numbers of a name.</summary>
    public static List<string> Tok(string s)
    {
        var res = new List<string>();
        foreach (Match m in TokRx.Matches(s)) res.Add(m.Value);
        return res;
    }
    /// <summary>FFMSigil(pfx): what a prefix says about the value — bool, int, string, or "" for none.</summary>
    public static string Sigil(string pfx)
    {
        if (pfx == "") return "";
        if (pfx.Contains("Flag")) return "bool";
        if (pfx.Contains("Int") || pfx.Contains("Log")) return "int";
        return "string";
    }
    public static bool SigilFits(string pfx, string ty)
    {
        string s = Sigil(pfx);
        return s == "" || s == "string" || s == ty;
    }

    /// <summary>FFMUpdateFlags(): fetch, then judge on the UI thread.</summary>
    public static void UpdateFlags()
    {
        if (Updating) { Ffm.Say("ALREADY UPDATING - PLEASE WAIT", SKY); return; }
        if (Ffm.Flags.Count == 0) { Ffm.Say("NO STAGED FLAGS TO UPDATE", AMBER); return; }
        UpdAt = Clock.Tick;
        Ffm.Say("FETCHING LATEST FLAG NAMES...", SKY);
        Task.Run(async () =>
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var order = new List<string>();
            int srcOk = 0;
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Velostrap");
            async Task<string> Fetch(string url)
            {
                try
                {
                    using var res = await http.GetAsync(url);
                    return res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync() : "";
                }
                catch { return ""; }
            }
            foreach (var src in new[] { "https://raw.githubusercontent.com/MaximumADHD/Roblox-FFlag-Tracker/main/PCDesktopClient.json",
                                        "https://raw.githubusercontent.com/MaximumADHD/Roblox-FFlag-Tracker/main/PCClientBootstrapper.json" })
            {
                var body = await Fetch(src);
                if (body == "") continue;
                srcOk++;
                foreach (Match m in JsonKey.Matches(body)) if (seen.Add(m.Groups[1].Value)) order.Add(m.Groups[1].Value);
            }
            var fv = await Fetch("https://raw.githubusercontent.com/MaximumADHD/Roblox-Client-Tracker/roblox/FVariables.txt");
            if (fv != "")
            {
                srcOk++;
                foreach (Match m in FVar.Matches(fv)) if (seen.Add(m.Groups[1].Value)) order.Add(m.Groups[1].Value);
            }
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                UpdAt = 0;
                if (order.Count == 0) { Ffm.Say("UPDATE FAILED - CHECK CONNECTION", C_ACC); return; }
                UpdateGot(order, srcOk, 3);
                HubSurface.Live?.Tim(Pace.TICK_A);
            });
        });
    }

    /// <summary>FFMUpdateGot: the index and the pass over the staged list. Public so it can be judged on a list given by hand.</summary>
    public static void UpdateGot(IReadOnlyList<string> names, int srcOk, int srcN)
    {
        Ffm.Say("BUILDING INDEX...", SKY);
        var live = new HashSet<string>(StringComparer.Ordinal);
        var bareAll = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var tokOf = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var df = new Dictionary<string, int>(StringComparer.Ordinal);
        int n = 0;
        foreach (var nm in names)
        {
            if (nm == "" || !live.Add(nm)) continue;
            string bn = Ffm.BareOf(nm);
            if (!bareAll.TryGetValue(bn, out var lst)) bareAll[bn] = lst = new List<string>();
            lst.Add(nm);
            var toks = Tok(bn).ToArray();
            tokOf[nm] = toks;
            var st = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in toks) if (st.Add(t)) df[t] = df.GetValueOrDefault(t) + 1;
            n++;
        }
        if (n == 0) { Ffm.Say("UPDATE FAILED - EMPTY FLAG LIST", C_ACC); return; }
        LivePartial = srcOk < srcN || n < 15000;
        var w = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (t, d) in df) w[t] = Math.Log(n / (double)d);
        double lnN = Math.Log(n);
        int cap = Math.Max(2, (int)(n * DF_CAP));
        var post = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (nm, toks) in tokOf)
        {
            var st = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in toks)
            {
                if (t == "" || !st.Add(t) || !df.TryGetValue(t, out var d) || d > cap) continue;
                if (!post.TryGetValue(t, out var pl)) post[t] = pl = new List<string>();
                pl.Add(nm);
            }
        }
        Live = live; BareAll = bareAll; LiveAt = Clock.Tick;
        var undo = new List<string>();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var remap = new Dictionary<string, string>(StringComparer.Ordinal);
        int hN = Ffm.Hist.Count;
        Ffm.Snap("update");                                           // the names as they were, for HISTORY
        foreach (var fl in Ffm.Flags) { undo.Add(fl.Name); taken.Add(fl.Name); }
        int ren = 0, rev = 0, unk = 0, okCnt = 0, bareN = 0;
        FfmViews.Upd.Clear(); FfmViews.UpdScr = 0; FfmViews.UpdScrT = 0;
        double W(string t) => w.TryGetValue(t, out var v) ? v : lnN;
        foreach (var fl in Ffm.Flags)
        {
            string old = fl.Name, ty = fl.Type;
            if (live.Contains(old)) { okCnt++; continue; }
            string clean = Ffm.BareOf(old);
            bool hadPfx = Ffm.HasPfx(old);
            if (bareAll.TryGetValue(clean, out var forms))
            {
                string cand = ""; int fits = 0;
                foreach (var c in forms)
                    if (SigilFits(Ffm.PfxOf(c), ty)) { if (cand == "") cand = c; fits++; }
                if (cand == "") cand = forms[0];
                if (!hadPfx) { okCnt++; bareN++; continue; }
                if (cand == old || taken.Contains(cand)) { okCnt++; continue; }
                taken.Remove(old); taken.Add(cand);
                fl.Name = cand; remap[old] = cand; ren++;
                FfmViews.UpdLog(old, cand, "PREFIX", 1.0, fits > 1 ? fits + " live forms fit this value - took the first" : "");
                continue;
            }
            if (LivePartial) { unk++; FfmViews.UpdLog(old, "", "SKIPPED", 0.0, "live list incomplete - a source did not download; not judged"); continue; }
            var q = Tok(clean);
            if (q.Count == 0) { unk++; FfmViews.UpdLog(old, "", "SKIPPED", 0.0, "nothing in the name to match on"); continue; }
            var qs = new HashSet<string>(StringComparer.Ordinal); double qw = 0.0;
            var ord = new List<string>();
            foreach (var t in q) if (qs.Add(t)) { qw += W(t); ord.Add(t); }
            ord = ord.OrderByDescending(W).ToList();                   // rarest first; stable, as the .ahk's insertion sort was
            var pool = new HashSet<string>(StringComparer.Ordinal); int used = 0;
            foreach (var t in ord)
            {
                if (!post.TryGetValue(t, out var pl)) continue;
                foreach (var c in pl) pool.Add(c);
                if (++used >= POOL_TOK || pool.Count >= POOL_MAX) break;
            }
            if (pool.Count == 0) { unk++; FfmViews.UpdLog(old, "", "SKIPPED", 0.0, "every word in this name is too common to search on"); continue; }
            string b1 = ""; double s1 = -1.0, sw1 = 0.0, s2 = -1.0;
            foreach (var c in pool)
            {
                if (!SigilFits(Ffm.PfxOf(c), ty)) continue;
                double iw = 0.0, cw = 0.0, ce = 0.0;
                var cs = new HashSet<string>(StringComparer.Ordinal);
                foreach (var t in tokOf[c])
                {
                    if (t == "" || !cs.Add(t)) continue;
                    double tw = W(t);
                    cw += tw;
                    if (qs.Contains(t)) iw += tw; else ce += tw;
                }
                if (iw <= 0.0 || qw + cw <= 0.0) continue;
                double d = 2 * iw / (qw + cw);
                if (d > s1) { s2 = s1; s1 = d; b1 = c; sw1 = qw != 0 ? Math.Min(qw - iw, ce) / qw : 0.0; }
                else if (d > s2) s2 = d;
            }
            if (b1 == "") { unk++; FfmViews.UpdLog(old, "", "SKIPPED", 0.0, "nothing in the live list can hold a " + ty + " value"); continue; }
            if (s1 < MATCH_MIN) { unk++; FfmViews.UpdLog(old, b1, "SKIPPED", s1, "closest was " + b1 + ", too far to trust"); continue; }
            if (s2 > s1 - MATCH_GAP) { unk++; FfmViews.UpdLog(old, b1, "SKIPPED", s1, "two candidates score within " + MATCH_GAP + " - refusing to guess"); continue; }
            string best = hadPfx ? b1 : Ffm.BareOf(b1);                // a set written bare stays bare
            if (taken.Contains(best)) { unk++; FfmViews.UpdLog(old, best, "SKIPPED", s1, best + " is already in the staged set"); continue; }
            taken.Remove(old); taken.Add(best);
            fl.Name = best; remap[old] = best;
            if (sw1 > MATCH_REV) { rev++; FfmViews.UpdLog(old, best, "REVIEW", s1, "a word was swapped, not dropped - check this is the same flag"); }
            else { ren++; FfmViews.UpdLog(old, best, "RENAMED", s1); }
        }
        if (ren + rev > 0)
        {
            Undo = undo;
            Ffm.Reindex();
            Ffm.SaveFlags();
            Ffm.Say($"UPDATED {ren + rev} FLAG(S) - {okCnt} CURRENT" + (rev != 0 ? $" - {rev} NEED REVIEW" : "") + (unk != 0 ? $" - {unk} UNKNOWN" : ""), rev != 0 ? AMBER : C_ON);
        }
        else
        {
            Undo = null;
            if (Ffm.Hist.Count > hN) { Ffm.Hist.RemoveAt(0); Ffm.HistSave(); }   // nothing changed: the snapshot was for nothing
            Ffm.Say((unk != 0 ? $"{okCnt} CURRENT - {unk} UNKNOWN FLAG(S)" : $"ALL {okCnt} FLAG(S) ALREADY CURRENT") + (LivePartial ? "  -  LIST INCOMPLETE, NOTHING JUDGED RETIRED" : ""),
                LivePartial ? AMBER : C_ON);
        }
        if (bareN != 0) FfmViews.UpdLog("-", "", "NOTE", 0.0, bareN + " staged flag(s) are bare: correct for INJECT, dead in an exported settings file - use PREFIX to rewrite them");
        Ffm.Changed?.Invoke();
    }

    /// <summary>FFMUpdateUndo(): the names back as they were before the last pass.</summary>
    public static void UpdateUndo()
    {
        if (!CanUndo) { Ffm.Say("NOTHING TO UNDO", AMBER); return; }
        int n = 0;
        for (int i = 0; i < Undo!.Count; i++)
            if (Ffm.Flags[i].Name != Undo[i]) { Ffm.Flags[i].Name = Undo[i]; n++; }
        Undo = null;
        Ffm.Reindex();
        Ffm.SaveFlags();
        Ffm.Say(n != 0 ? $"UNDID {n} RENAME(S)" : "NOTHING CHANGED", C_ON);
        Ffm.Changed?.Invoke();
    }

    /// <summary>FFMNormalizePrefixes(): give every bare staged name its live prefix, when exactly one live form fits its value.</summary>
    public static void NormalizePrefixes()
    {
        if (!(LiveAt != 0 && Live.Count > 0)) { Ffm.Say("RUN UPDATE FIRST", AMBER); return; }
        int n = 0, amb = 0;
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fl in Ffm.Flags) taken.Add(fl.Name);
        foreach (var fl in Ffm.Flags)
        {
            if (Ffm.HasPfx(fl.Name)) continue;
            if (!BareAll.TryGetValue(fl.Name, out var forms)) continue;
            string cand = ""; int fits = 0;
            foreach (var c in forms) if (SigilFits(Ffm.PfxOf(c), fl.Type)) { if (cand == "") cand = c; fits++; }
            if (cand == "" || taken.Contains(cand)) continue;
            if (fits > 1) { amb++; FfmViews.Log("Prefix", fl.Name, "ambiguous - " + fits + " live forms fit this value"); continue; }
            taken.Remove(fl.Name); taken.Add(cand);
            FfmViews.Log("Prefix", fl.Name, "-> " + cand);
            fl.Name = cand;
            n++;
        }
        if (n != 0) { Ffm.Reindex(); Ffm.SaveFlags(); }
        Ffm.Say(n != 0 ? $"PREFIXED {n} FLAG(S)" + (amb != 0 ? $" - {amb} AMBIGUOUS" : "") : "NO BARE FLAGS TO PREFIX", n != 0 ? C_ON : AMBER);
        Ffm.Changed?.Invoke();
    }
}
