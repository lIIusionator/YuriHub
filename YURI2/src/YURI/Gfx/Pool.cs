using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Yuri.Core;



namespace Yuri.Gfx;

/// <summary>
/// The selfie pool (LoadSelfiePool): avatar.* first, then avatar_1.*, then
/// selfie* / galimg_* — from YURI\avatars when the user has them, else the
/// embedded set. The gate shows entries past Base (avatar.* is overlay-only);
/// RotN is how many of those are in the gallery's rotation. Pictures are
/// kept whole; the square and tall crops the .ahk pre-rendered are cover-fits
/// at draw time here (SelAt / SelAtTall).
/// </summary>
public static class Pool
{
    public sealed record Pic(string Name, Bitmap? Bmp, string Path, bool Embedded);
    public static readonly List<Pic> Pics = new();
    public static int Base, RotN, Av1Idx, CustomIdx;
    public static int N => Pics.Count;
    public static int DispN => N - (CustomIdx != 0 ? 1 : 0);
    static bool _loaded;
    static readonly string[] EmbNames = { "avatar.jpg", "avatar_1.jpg", "selfie3.jpg", "selfie4.png", "selfie5.jpg", "selfie6.jpg" };

    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        Pics.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var avv = new List<(string name, string path)>(); var av1 = new List<(string name, string path)>(); var sel = new List<(string name, string path)>();
        string dir = Paths.Av;
        try
        {
            if (Directory.Exists(dir))
            {
                foreach (var pat in new[] { "avatar.jpg", "avatar.jpeg", "avatar.png" }) { if (avv.Count > 0) break; foreach (var f in Directory.GetFiles(dir, pat)) { if (seen.Add(System.IO.Path.GetFileName(f))) avv.Add((System.IO.Path.GetFileName(f), f)); break; } }
                foreach (var pat in new[] { "avatar_1.jpg", "avatar_1.jpeg", "avatar_1.png" }) { if (av1.Count > 0) break; foreach (var f in Directory.GetFiles(dir, pat)) { if (seen.Add(System.IO.Path.GetFileName(f))) av1.Add((System.IO.Path.GetFileName(f), f)); break; } }
                foreach (var pat in new[] { "selfie*.png", "selfie*.jpg", "selfie*.jpeg", "galimg_*.png", "galimg_*.jpg", "galimg_*.jpeg" })
                    foreach (var f in Directory.GetFiles(dir, pat)) if (seen.Add(System.IO.Path.GetFileName(f))) sel.Add((System.IO.Path.GetFileName(f), f));
            }
        }
        catch { }
        foreach (var e in EmbNames)
        {
            if (!seen.Add(e)) continue;
            string k = e.ToLowerInvariant();
            if (k.StartsWith("avatar.")) { if (avv.Count == 0) avv.Add((e, "")); }
            else if (k.StartsWith("avatar_1.")) { if (av1.Count == 0) av1.Add((e, "")); }
            else if (k.StartsWith("selfie")) sel.Add((e, ""));
        }
        sel.Sort((a, b) => Logical(a.name, b.name));
        int n1 = 0;
        foreach (var it in avv) if (Add(it.name, it.path)) n1++;
        var hidden = Hidden();
        var rotL = new List<(string name, string path)>(); var hidL = new List<(string name, string path)>();
        foreach (var it in av1) (hidden.Contains(it.name) ? hidL : rotL).Add(it);
        foreach (var it in sel) (hidden.Contains(it.name) ? hidL : rotL).Add(it);
        int r = 0;
        foreach (var it in rotL) if (Add(it.name, it.path)) { r++; if (it.name.ToLowerInvariant().Contains("avatar_1")) Av1Idx = Pics.Count; }
        RotN = r;
        foreach (var it in hidL) if (Add(it.name, it.path) && it.name.ToLowerInvariant().Contains("avatar_1")) Av1Idx = Pics.Count;
        string cst = System.IO.Path.Combine(dir, "profile_crop.png");
        if (File.Exists(cst) && Add("profile_crop.png", cst)) CustomIdx = Pics.Count;
        Base = n1;
    }
    static HashSet<string> Hidden()
    {
        var h = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try { foreach (var s in Ini.Read(Paths.IniFile, "gallery", "hidden", "").Split('|', StringSplitOptions.RemoveEmptyEntries)) h.Add(s); } catch { }
        return h;
    }
    static int Logical(string a, string b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    static bool Add(string name, string path)
    {
        Bitmap? bmp = path != "" ? Img.File(path) : Img.Asset(name);
        if (bmp is null) return false;
        Pics.Add(new Pic(name, bmp, path, path == ""));
        return true;
    }
    /// <summary>SelAt(idx): 1-based, wrapping.</summary>
    public static Bitmap? SelAt(int idx) => N < 1 ? null : Pics[((idx - 1) % N + N) % N].Bmp;
    public static bool HasRot => RotN >= 1;
    public static int GalLo => Base + 1;
    public static int GalHi => Base + Math.Max(1, RotN);
    public static string SelKey(int idx) => idx >= 1 && idx <= N ? Pics[idx - 1].Name : "";
    public static int SelFindKey(string key) { for (int i = 0; i < N; i++) if (Pics[i].Name.Equals(key, StringComparison.OrdinalIgnoreCase)) return i + 1; return 0; }
    public static bool IsGalOnly(int idx) => SelKey(idx).StartsWith("galimg_", StringComparison.OrdinalIgnoreCase);

    // ---- the profile picture (profPicSet / selCur / selNext / selFadeAt) ----
    public static bool ProfPicSet; public static int SelCur = 1, SelNext = 1; public static long SelFadeAt; public const double SEL_FADE = 520;
    public static void ProfPicLoad()
    {
        string key = Ini.Read(Paths.IniFile, "profile", "pic", "");
        int pi = key != "" ? SelFindKey(key) : 0;
        if (pi != 0 && !IsGalOnly(pi)) { SelCur = SelNext = pi; SelFadeAt = 0; ProfPicSet = true; }
    }
    public static void ProfPicSave() => Ini.Write(Paths.IniFile, "profile", "pic", ProfPicSet ? SelKey(SelFadeAt != 0 ? SelNext : SelCur) : "");
    /// <summary>SelSwitchG(idx): the portrait fades to idx.</summary>
    public static void SwitchG(int idx)
    {
        if (N < 1) return;
        idx = ((idx - 1) % N + N) % N + 1;
        if (SelFadeAt != 0) SelCur = SelNext;
        if (idx == SelCur) { SelNext = idx; SelFadeAt = 0; return; }
        SelNext = idx; SelFadeAt = Clock.Tick;
    }
    public static void SelTick(long now) { if (SelFadeAt != 0 && now - SelFadeAt >= SEL_FADE) { SelCur = SelNext; SelFadeAt = 0; } }

    // ---- the gallery's edits ----
    public static void HideSet(string name, bool hide)
    {
        var parts = Ini.Read(Paths.IniFile, "gallery", "hidden", "").Split('|', StringSplitOptions.RemoveEmptyEntries).Where(p => !p.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (hide) parts.Add(name.ToLowerInvariant());
        Ini.Write(Paths.IniFile, "gallery", "hidden", string.Join("|", parts));
    }
    public static int HiddenN() => Hidden().Count(h => Pics.Any(p => p.Name.Equals(h, StringComparison.OrdinalIgnoreCase)));
    public static int AddedN() { try { return Directory.Exists(Paths.Av) ? Directory.GetFiles(Paths.Av, "galimg_*.*").Count(f => System.Text.RegularExpressions.Regex.IsMatch(f, "\\.(png|jpe?g)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) : 0; } catch { return 0; } }
    /// <summary>GalReload(): the pool from disk again, keeping the profile picture by its name.</summary>
    public static void Reload()
    {
        string pk = N >= 1 && SelCur >= 1 && SelCur <= N ? SelKey(SelFadeAt != 0 ? SelNext : SelCur) : "";
        _loaded = false; Base = 0; RotN = 0; Av1Idx = 0; CustomIdx = 0;
        Load();
        SelFadeAt = 0;
        int pi = pk != "" ? SelFindKey(pk) : 0;
        if (pi != 0 && IsGalOnly(pi)) pi = 0;
        if (pi != 0) { SelCur = SelNext = pi; }
        else { SelCur = SelNext = 1; if (pk != "") ProfPicSet = false; }
    }
    public static bool RemovePool(int idx) { string k = SelKey(idx); if (k == "") return false; HideSet(k, true); Reload(); return true; }
    public static void ResetGallery()
    {
        try
        {
            string arc = System.IO.Path.Combine(Paths.Av, "archive"); Directory.CreateDirectory(arc);
            if (Directory.Exists(Paths.Av))
                foreach (var src in Directory.GetFiles(Paths.Av, "galimg_*.*"))
                {
                    string nm = System.IO.Path.GetFileName(src), dst = System.IO.Path.Combine(arc, nm); int k = 1;
                    while (File.Exists(dst) && k < 500) dst = System.IO.Path.Combine(arc, System.IO.Path.GetFileNameWithoutExtension(nm) + "_" + k++ + System.IO.Path.GetExtension(nm));
                    File.Move(src, dst);
                }
        }
        catch { }
        Ini.Write(Paths.IniFile, "gallery", "hidden", "");
        Reload();
    }
    /// <summary>GalAddGot: copies picked files in as galimg_&lt;stamp&gt;_&lt;n&gt;.</summary>
    public static int AddFiles(IEnumerable<string> files)
    {
        int n = 0, seq = 0; long stamp = Clock.Tick;
        try { Directory.CreateDirectory(Paths.Av); } catch { }
        foreach (var fp in files)
        {
            if (!File.Exists(fp)) continue;
            string ext = System.IO.Path.GetExtension(fp).TrimStart('.').ToLowerInvariant();
            if (ext is not ("png" or "jpg" or "jpeg")) continue;
            seq++;
            string dst = System.IO.Path.Combine(Paths.Av, "galimg_" + stamp + "_" + seq + "." + ext);
            try { File.Copy(fp, dst, false); HideSet(System.IO.Path.GetFileName(dst), false); n++; } catch { }
        }
        if (n > 0) Reload();
        return n;
    }
    /// <summary>CropCommit: a square of `side` around (cx, cy) of the source, scaled to 256, saved as profile_crop.png and made the custom entry.</summary>
    public static int CropCommit(Bitmap src, double cx, double cy, double side)
    {
        const int SELW = 256;
        try
        {
            Directory.CreateDirectory(Paths.Av);
            using var rt = new RenderTargetBitmap(new PixelSize(SELW, SELW), new Vector(96, 96));
            using (var dc = rt.CreateDrawingContext())
            using (dc.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
                dc.DrawImage(src, new Rect(cx - side / 2, cy - side / 2, side, side), new Rect(0, 0, SELW, SELW));
            string dst = System.IO.Path.Combine(Paths.Av, "profile_crop.png");
            rt.Save(dst);
            Reload();
            return CustomIdx;
        }
        catch { return 0; }
    }
    /// <summary>CropSquare's centre with the .ahk's bias: a square of the short side, biased upward for a tall picture.</summary>
    public static (double cx, double cy, double side) AutoCrop(Bitmap src, double bias = 0.32)
    {
        double w = src.PixelSize.Width, h = src.PixelSize.Height, side = Math.Min(w, h);
        return (w / 2, h > w ? side / 2 + (h - side) * bias : h / 2, side);
    }
}
