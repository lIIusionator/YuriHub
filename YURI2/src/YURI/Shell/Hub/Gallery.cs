using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Yuri.Core;
using Yuri.Gfx;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;

namespace Yuri.Shell.Hub;

public static class Gallery
{
    const double TH = 64, GP = 14, TY0 = 58, PD3 = 24;
    public static bool Open; public static long IntroAt, CloseAt, PickAt; public static bool Manage; public static int Target;   // Target: the script card, 0 for the profile
    static readonly List<int> Idx = new(); static int _pend, _rmIdx, _hidN, _hovIdx; static double _hovE, _clE, _brE;
    static double _cw, _ch, _footY, _fbX, _fbW; static int _show, _rows;
    // the cropper
    public static bool Crop; static Bitmap? _src; static string _srcPath = ""; static double _zoom = 1, _panX, _panY, _dragX0, _dragY0, _panX0, _panY0; static bool _dragging; static long _cropAt, _cropOut;
    const double CW_C = 380, CH_C = 380;
    public static bool Animating => Open || Crop || (CloseAt != 0 && Clock.Tick - CloseAt < 300);
    static bool _picking;
    public static Action<int, int>? CardPicked;                        // (target, pool index) for the script cards

    /// <summary>
    /// The hooks the rest of the hub reads the pool through. They were declared
    /// and never assigned, so SETTINGS said "0 in rotation" with five pictures
    /// in it, and UPDATE LOGS believed there was no rotation at all.
    /// </summary>
    public static void Register()
    {
        Tabs.SettingsTab.GalRotN = () => Gfx.Pool.RotN;
        Tabs.SettingsTab.GalHidN = () => Gfx.Pool.HiddenN();
        Tabs.UpdateLogs.GalHasRot = () => Gfx.Pool.HasRot;
    }
    public static void Show(int target = 0, bool manage = false)
    {
        Manage = manage;
        if (Open || Crop) return;
        Pool.Load();
        if (Pool.N < 1) { _ = BrowseAsync(target); return; }
        Target = target;
        Idx.Clear();
        if (Manage) { if (Pool.HasRot) for (int i = Pool.GalLo; i <= Pool.GalHi; i++) Idx.Add(i); }
        else for (int i = 1; i <= Pool.N; i++) { if (i == Pool.Av1Idx || i == Pool.CustomIdx || Pool.IsGalOnly(i)) continue; Idx.Add(i); }
        if (Idx.Count == 0 && !Manage) for (int i = 1; i <= Pool.N; i++) Idx.Add(i);
        _show = Math.Min(Idx.Count, Manage ? 12 : 8);
        int cols = Math.Min(Math.Max(_show, 1), 4);
        _rows = Math.Max(1, (_show + 3) / 4);
        _cw = Math.Max(Manage ? 360 : 300, 48 + cols * TH + (cols - 1) * GP);
        _footY = TY0 + _rows * (TH + GP) - GP + 16;
        _ch = _footY + 30 + 18;
        _fbX = Math.Max(24 + 134 + 12, _cw - 172); _fbW = _cw - 24 - _fbX;
        _hidN = Manage ? Pool.HiddenN() + Pool.AddedN() : 0;
        Open = true; IntroAt = Clock.Tick; CloseAt = 0; PickAt = 0; _pend = 0; _rmIdx = 0; _hovIdx = 0; _hovE = _clE = _brE = 0;
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    static void Geom(out double cx, out double cy) { cx = HubLayout.pd + (HubLayout.cw - _cw) / 2; cy = HubLayout.pd + (HubLayout.ch - _ch) / 2; }
    static void Close() { if (CloseAt != 0) return; CloseAt = Clock.Tick; }
    static void Finish()
    {
        Open = false;
        int pend = _pend, rm = _rmIdx, target = Target; bool manage = Manage;
        CloseAt = 0;
        if (manage)
        {
            if (pend == -2) _ = AddFilesAsync();
            else if (pend == -3 && rm >= 1) { Pool.RemovePool(rm); Avalonia.Threading.DispatcherTimer.RunOnce(() => Show(0, true), TimeSpan.FromMilliseconds(120)); }
            else if (pend == -4) { Pool.ResetGallery(); Avalonia.Threading.DispatcherTimer.RunOnce(() => Show(0, true), TimeSpan.FromMilliseconds(120)); }
        }
        else if (pend >= 1) CropFromPool(pend, target);
        else if (pend == -1) _ = BrowseAsync(target);
        Manage = false; Target = 0;
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    static void CropFromPool(int idx, int target)
    {
        if (idx < 1 || idx > Pool.N) return;
        var pic = Pool.Pics[idx - 1];
        Bitmap? full = pic.Path != "" && File.Exists(pic.Path) ? Img.File(pic.Path) : Img.Asset(pic.Name);
        if (full is null) return;
        CropOpen(full, pic.Path, target);
    }
    static async Task BrowseAsync(int target)
    {
        if (_picking || HubSurface.Live is not { } hub) return;
        _picking = true;
        try
        {
            var top = TopLevel.GetTopLevel(hub);
            if (top?.StorageProvider is not { CanOpen: true } sp) return;
            var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Pick a picture", AllowMultiple = false, FileTypeFilter = new[] { new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } } } });
            if (files.Count > 0 && files[0].TryGetLocalPath() is { } p && Img.File(p) is { } bmp) CropOpen(bmp, p, target);
        }
        catch { }
        finally { _picking = false; hub.Tim(Pace.TICK_A); }
    }
    static async Task AddFilesAsync()
    {
        if (_picking || HubSurface.Live is not { } hub) return;
        _picking = true;
        try
        {
            var top = TopLevel.GetTopLevel(hub);
            if (top?.StorageProvider is not { CanOpen: true } sp) return;
            var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Add images to the gallery", AllowMultiple = true, FileTypeFilter = new[] { new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } } } });
            var paths = files.Select(f => f.TryGetLocalPath()).Where(p => p is not null).Select(p => p!).ToList();
            if (paths.Count > 0 && Pool.AddFiles(paths) > 0) Show(0, true);
        }
        catch { }
        finally { _picking = false; hub.Tim(Pace.TICK_A); }
    }

    // ---- the cropper ----
    public static void CropOpen(Bitmap src, string path, int target)
    {
        _src = src; _srcPath = path; Target = target; Crop = true; _cropAt = Clock.Tick; _cropOut = 0; _dragging = false;
        var (cx, cy, side) = Pool.AutoCrop(src);
        _zoom = 1; _panX = cx; _panY = cy;                                      // the window's centre in source pixels
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    static double CropSide() { if (_src is null) return 1; double w = _src.PixelSize.Width, h = _src.PixelSize.Height; return Math.Min(w, h) / _zoom; }
    static void CropClamp()
    {
        if (_src is null) return;
        double w = _src.PixelSize.Width, h = _src.PixelSize.Height, side = CropSide();
        _panX = Clamp(_panX, side / 2, w - side / 2); _panY = Clamp(_panY, side / 2, h - side / 2);
    }
    static void CropGeom(out double kx, out double ky, out double fx, out double fy, out double fs)
    {
        kx = HubLayout.pd + (HubLayout.cw - CW_C) / 2; ky = HubLayout.pd + (HubLayout.ch - CH_C) / 2;
        fs = 224; fx = kx + (CW_C - fs) / 2; fy = ky + 58;   // room under it for the zoom row, which used to be laid across the buttons
    }
    static void CropCommitNow()
    {
        if (_src is null) return;
        CropClamp();
        int idx = Pool.CropCommit(_src, _panX, _panY, CropSide());
        if (idx != 0)
        {
            if (Target >= 1) CardPicked?.Invoke(Target, idx);
            else { Pool.ProfPicSet = true; Pool.SwitchG(idx); Pool.ProfPicSave(); }
        }
        CropCancel();
    }
    /// <summary>
    /// The card arrived on a 420 ms scale-in and used to vanish between two
    /// frames. This starts the exit; the draw finishes it and CropDone tears the
    /// state down, so USE and CANCEL both leave the same way.
    /// </summary>
    static void CropCancel() { if (_cropOut == 0) { _cropOut = Clock.Tick; HubSurface.Live?.Tim(Pace.TICK_A); } }
    static void CropDone() { Crop = false; _cropOut = 0; _src = null; Target = 0; HubSurface.Live?.Tim(Pace.TICK_A); }
    /// <summary>Closing: the card is on its way out and must not answer a click.</summary>
    public static bool CropClosing => _cropOut != 0;

    // ---- zones / clicks ----
    public static int Zone(double ux, double uy)
    {
        if (Crop)
        {
            CropGeom(out double kx, out double ky, out double fx, out double fy, out double fs);
            if (ux >= fx && ux <= fx + fs && uy >= fy && uy <= fy + fs) return 1225;                 // the frame: drag to pan
            double by = ky + CH_C - 46;
            if (uy >= by && uy <= by + 30) { if (ux >= kx + CW_C / 2 - 130 && ux <= kx + CW_C / 2 - 10) return 1226; if (ux >= kx + CW_C / 2 + 10 && ux <= kx + CW_C / 2 + 130) return 1227; }
            if (ux >= kx && ux <= kx + CW_C && uy >= ky && uy <= ky + CH_C) return 1224;
            return 1223;                                                                          // outside: cancels
        }
        if (!Open) return 0;
        Geom(out double cx3, out double cy3);
        if ((ux - (cx3 + _cw - 22)) * (ux - (cx3 + _cw - 22)) + (uy - (cy3 + 22)) * (uy - (cy3 + 22)) <= 81) return 1212;
        if (ux >= cx3 + 24 && ux <= cx3 + 158 && uy >= cy3 + _footY && uy <= cy3 + _footY + 30) return 1213;
        if (Manage && _hidN > 0 && ux >= cx3 + _fbX && ux <= cx3 + _fbX + _fbW && uy >= cy3 + _footY && uy <= cy3 + _footY + 30) return 1215;
        for (int i = 1; i <= _show; i++)
        {
            double tx = cx3 + 24 + ((i - 1) % 4) * (TH + GP), ty = cy3 + TY0 + ((i - 1) / 4) * (TH + GP);
            if (ux >= tx - 2 && ux <= tx + TH + 2 && uy >= ty - 2 && uy <= ty + TH + 2) return 1240 + i;
        }
        if (ux >= cx3 && ux <= cx3 + _cw && uy >= cy3 && uy <= cy3 + _ch) return 1214;
        return 1211;                                                                              // the veil: closes
    }
    public static bool Press(HubSurface hub, int z)
    {
        if (z == 1225 && Crop) { _dragging = true; _dragX0 = hub.PtrX; _dragY0 = hub.PtrY; _panX0 = _panX; _panY0 = _panY; hub.BeginPtrDrag(); return true; }
        return false;
    }
    public static void Drag(HubSurface hub)
    {
        if (!Crop || !_dragging || _src is null) return;
        CropGeom(out _, out _, out _, out _, out double fs);
        double k = CropSide() / fs;
        _panX = _panX0 - (hub.PtrX - _dragX0) * k; _panY = _panY0 - (hub.PtrY - _dragY0) * k;
        CropClamp();
        hub.Tim(Pace.TICK_A);
    }
    public static void MouseUp() => _dragging = false;
    public static bool Wheel(double delta)
    {
        if (!Crop) return false;
        _zoom = Clamp(_zoom * (delta > 0 ? 1.12 : 1 / 1.12), 1.0, 6.0);
        CropClamp();
        return true;
    }
    public static bool Click(HubSurface hub, int z)
    {
        if (Crop)
        {
            if (z == 1226) CropCommitNow(); else if (z == 1227 || z == 1223) CropCancel();
            return z >= 1223 && z <= 1227;
        }
        if (!Open) return false;
        if (CloseAt != 0 || PickAt != 0) return z >= 1211 && z <= 1252;
        switch (z)
        {
            case 1212: _pend = 0; Close(); break;
            case 1215: _pend = -4; Close(); break;
            case 1213: _pend = Manage ? -2 : -1; Close(); break;
            case 1211: _pend = 0; Close(); break;
            case >= 1241 and <= 1252:
            {
                int gi = z - 1240;
                _pend = gi >= 1 && gi <= Idx.Count ? Idx[gi - 1] : 0;
                if (Manage && _pend >= 1) { _rmIdx = _pend; _pend = -3; }
                PickAt = Clock.Tick;
                break;
            }
            case 1214: break;
            default: return false;
        }
        hub.Tim(Pace.TICK_A);
        return true;
    }

    // ---- drawing ----
    public static void Draw(HubSurface hub, long now)
    {
        if (Crop) { DrawCrop(hub, now); return; }
        if (!Open) return;
        uint acc = HubState.Accent;
        double it = Math.Min((now - IntroAt) / 180.0, 1.0), ei = 1 - Math.Pow(1 - it, 3), winA = Math.Min(it * 2.2, 1.0), yO = (1 - ei) * 12, scl = 0.97 + 0.03 * ei;
        if (PickAt != 0 && now - PickAt >= 200) Close();
        if (CloseAt != 0)
        {
            double ft = Math.Min((now - CloseAt) / 240.0, 1.0);
            winA *= 1 - ft; scl = 1 - 0.04 * ft;
            if (ft >= 1) { Finish(); return; }
        }
        int hz = CloseAt != 0 ? 0 : Zone(hub.PtrX, hub.PtrY);
        _clE += ((hz == 1212 ? 1.0 : 0.0) - _clE) * EK(0.2);
        _brE += ((hz == 1213 ? 1.0 : 0.0) - _brE) * EK(0.2);
        int nhi = hz >= 1241 ? hz - 1240 : 0;
        if (nhi != _hovIdx) { _hovIdx = nhi; _hovE = 0; }
        _hovE += ((_hovIdx != 0 ? 1.0 : 0.0) - _hovE) * EK(0.25);
        double emb3 = (Math.Sin(DecT(now) * 0.0035) + 1) / 2;
        double f = winA;
        double pd = HubLayout.pd, cw = HubLayout.cw, ch = HubLayout.ch;
        FillRR(pd - 2, pd - 2, cw + 4, ch + 4, 22, SBrush(FA(0xB0060710, f)));
        Geom(out double cx3, out double cy3);
        cy3 += yO;
        int st0 = PushXform(cx3 + _cw / 2, cy3 + _ch / 2, scl, 0);
        ShadowDraw(cx3, cy3 + 3, _cw, _ch, 15, 8, 9, 9, f);
        FillRR(cx3, cy3, _cw, _ch, 15, VBrush(cx3, cy3, _cw, _ch, FA(0xF2222438, f), FA(0xF8121423, f)));
        int stC = PushG(); ClipRR(cx3, cy3, _cw, _ch, 15);
        MiniBackdrop(cx3, cy3, _cw, _ch, 15, acc, f, now, 0.9);
        for (int k = 1; k <= 8; k++)
        {
            double cyc = 4800 + k * 401, prt = (now + k * 887) % cyc / cyc;
            double mx_ = cx3 + 24 + (k * 151) % (_cw - 48) + 7 * Math.Sin(DecT(now) * 0.001 + k * 1.9), my_ = cy3 + _ch - 8 - prt * (_ch - 16);
            FillEll(mx_ - 1.1, my_ - 1.1, 2.2, 2.2, SBrush(FA(Alpha(AccHi(acc, 0.35), R(40 * Math.Sin(Math.PI * prt))), f)));
        }
        FillRect(cx3, cy3, 10, _ch, SBrush(FA(Alpha(acc, R(44 + 14 * emb3)), f)));
        FillRect(cx3, cy3, 3.5, _ch, VBrush(cx3, cy3, 3.5, _ch, FA(Alpha(acc, 240), f), FA(Alpha(acc, 110), f)));
        Pop(stC);
        StrokeRR(cx3, cy3, _cw, _ch, 15, Pen(FA(0x28FFFFFF, f), 1));
        { var pnD = Pen(FA(Alpha(acc, R(20 + 10 * emb3)), f), 1); PenDash(pnD, 1); StrokeRR(cx3 + 5, cy3 + 5, _cw - 10, _ch - 10, 11, pnD); }
        Txt("GALLERY", cx3 + 24, cy3 + 14, 200, 18, Fonts.fBrand, FA(0xE0E8EAF6, f), Fmt.L);
        double wGal = Fonts.MeasureW("GALLERY", Fonts.fBrand);
        FillEll(cx3 + 24 + wGal + 7, cy3 + 21.5, 5, 5, SBrush(FA(Alpha(acc, R(200 + 40 * emb3)), f)));
        Txt(Manage ? "manage the gate's rotation" : Target >= 1 ? "select the card's picture" : "select display picture", cx3 + 24, cy3 + 33, 220, 13, Fonts.fHint, FA(0x55C7CBE0, f), Fmt.L);
        Txt(Idx.Count + " items", cx3 + _cw - 104, cy3 + 32, 80, 15, Fonts.fBadge, FA(0x62C7CBE0, f), Fmt.R);
        double ec = 1 + 0.28 * _clE, clbx = cx3 + _cw - 22 + 3 * _clE, clby = cy3 + 22 - 1.2 * _clE;
        FillEll(clbx - 7 * ec, clby - 7 * ec, 14 * ec, 14 * ec, SBrush(FA(Alpha(acc, R(36 + 60 * _clE)), f)));
        Ell(clbx - 7 * ec, clby - 7 * ec, 14 * ec, 14 * ec, Pen(FA(Alpha(acc, R(100 + 80 * _clE)), f), 1));
        var pnX = Pen(FA(Alpha(AccHi(acc, 0.45), R(210 + 45 * _clE)), f), 1.5);
        Line(clbx - 3.2 * ec, clby - 3.2 * ec, clbx + 3.2 * ec, clby + 3.2 * ec, pnX); Line(clbx - 3.2 * ec, clby + 3.2 * ec, clbx + 3.2 * ec, clby - 3.2 * ec, pnX);
        int curSel = Target >= 1 ? 0 : Pool.ProfPicSet ? (Pool.SelFadeAt != 0 ? Pool.SelNext : Pool.SelCur) : 0;
        for (int i = 1; i <= _show; i++)
        {
            double tdel = Clamp((now - IntroAt - 20 - i * 16) / 150.0, 0.0, 1.0), ta = 1 - Math.Pow(1 - tdel, 3);
            if (ta <= 0) continue;
            double tx = cx3 + 24 + ((i - 1) % 4) * (TH + GP), ty = cy3 + TY0 + ((i - 1) / 4) * (TH + GP) + (1 - ta) * 8;
            double hv = i == _hovIdx ? _hovE : 0.0, gxp = 2 * hv;
            int realI = Idx[i - 1];
            var bmp = Pool.SelAt(realI);
            if (bmp is null) continue;
            FillRR(tx + 2, ty + 3, TH, TH, 10, SBrush(FA(Alpha(0x000000, R(90 * ta)), f)));
            Img.FitRR(bmp, tx - gxp, ty - gxp, TH + gxp * 2, TH + gxp * 2, 10, f * ta * (0.82 + 0.18 * hv + (realI == curSel ? 0.18 : 0)), 1.0, 0.5, 0.35);
            StrokeRR(tx - gxp + 0.5, ty - gxp + 0.5, TH + gxp * 2 - 1, TH + gxp * 2 - 1, 9.5, Pen(FA(Alpha(0x000000, R(80 * ta)), f), 1));
            if (realI == curSel)
            {
                StrokeRR(tx - gxp - 1.5, ty - gxp - 1.5, TH + gxp * 2 + 3, TH + gxp * 2 + 3, 11, Pen(FA(Alpha(acc, R(235 * ta)), f), 2));
                Arc(tx - gxp - 1.5, ty - gxp - 1.5, TH + gxp * 2 + 3, TH + gxp * 2 + 3, DecT(now) * 0.1 % 360, 46, Pen(FA(Alpha(AccHi(acc, 0.5), R(235 * ta)), f), 2));
                FillEll(tx + TH - 16, ty + TH - 16, 16, 16, SBrush(FA(Alpha(0x0B0C14, R(235 * ta)), f)));
                var pnT = Pen(FA(Alpha(acc, R(245 * ta)), f), 1.8);
                Line(tx + TH - 12, ty + TH - 8, tx + TH - 9.5, ty + TH - 5.5, pnT); Line(tx + TH - 9.5, ty + TH - 5.5, tx + TH - 4.5, ty + TH - 11.5, pnT);
            }
            else if (hv > 0.01) StrokeRR(tx - gxp - 1, ty - gxp - 1, TH + gxp * 2 + 2, TH + gxp * 2 + 2, 10.5, Pen(FA(Alpha(AccHi(acc, 0.6), R(90 + 130 * hv)), f), 1.4));
            else StrokeRR(tx, ty, TH, TH, 10, Pen(FA(Alpha(0xFFFFFF, R(46 * ta)), f), 1));
            if (PickAt != 0 && (Manage ? _rmIdx : _pend) == realI) { double pe = Math.Min((now - PickAt) / 200.0, 1.0); FillRR(tx, ty, TH, TH, 10, SBrush(FA(Alpha(0xFFFFFF, R(110 * (1 - pe))), f))); }
        }
        double bre = 1 + 0.03 * _brE;
        int stB = PushXform(cx3 + 24 + 67, cy3 + _footY + 15, bre, 0);
        FillRR(cx3 + 24, cy3 + _footY, 134, 30, 8, VBrush(cx3 + 24, cy3 + _footY, 134, 30, FA(Mix(0xFF31344C, 0xFF41466A, _brE), f), FA(Mix(0xFF20223A, 0xFF262A45, _brE), f)));
        MicroBackdrop(cx3 + 24, cy3 + _footY, 134, 30, 8, acc, f, now, 0.9);
        StrokeRR(cx3 + 24, cy3 + _footY, 134, 30, 8, Pen(FA(Alpha(acc, R(100 + 90 * _brE)), f), 1));
        var pnF = Pen(FA(Alpha(Mix(0xFFE8EAF6, acc, 0.3), 220), f), 1.3);
        double fy0 = cy3 + _footY;
        Line(cx3 + 36, fy0 + 11, cx3 + 40, fy0 + 11, pnF); Line(cx3 + 40, fy0 + 11, cx3 + 42, fy0 + 13, pnF); Line(cx3 + 42, fy0 + 13, cx3 + 50, fy0 + 13, pnF); Line(cx3 + 50, fy0 + 13, cx3 + 50, fy0 + 20, pnF); Line(cx3 + 50, fy0 + 20, cx3 + 36, fy0 + 20, pnF); Line(cx3 + 36, fy0 + 20, cx3 + 36, fy0 + 11, pnF);
        Txt(Manage ? "ADD IMAGES" : "BROWSE FILES", cx3 + 54, fy0, 100, 30, Fonts.fBadge, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.25), 235), f), Fmt.L);
        Pop(stB);
        if (Manage && _hidN > 0)
        {
            double hvR = hz == 1215 ? 1.0 : 0.0;
            StrokeRR(cx3 + _fbX, fy0, _fbW, 30, 8, Pen(FA(Alpha(acc, R(70 + 120 * hvR)), f), 1));
            Txt("RESET GALLERY", cx3 + _fbX, fy0, _fbW, 30, Fonts.fBadge, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.3), R(200 + 55 * hvR)), f), Fmt.C);
        }
        else Txt(Manage ? "click to remove" : "click to use", cx3 + _fbX, fy0, _fbW, 30, Fonts.fHint, FA(0x4EC7CBE0, f), Fmt.R);
        Pop(st0);
    }
    /// <summary>Open the cropper on the first pool picture, for the harness and the snapshot tool.</summary>
    public static void CropTest() { Show(0, false); var b = Pool.SelAt(1); if (b is null) return; _src = b; _srcPath = ""; Crop = true; _cropAt = Clock.Tick - 900; double cs = CropSide(); _zoom = 2.4; _panX = b.PixelSize.Width / 2.0; _panY = b.PixelSize.Height / 2.0; }
    static Font HL_M() => HubSurface.Live?.HL.fM ?? Fonts.fBadge;
    static void DrawCrop(HubSurface hub, long now)
    {
        if (_src is null) return;
        uint acc = HubState.Accent;
        double co = _cropOut != 0 ? Clamp((now - _cropOut) / 260.0, 0.0, 1.0) : 0.0, coE = Ease3(co);
        if (co >= 1) { CropDone(); return; }
        double f = Ease3(Clamp((now - _cropAt) / 220.0, 0.0, 1.0)) * (1 - coE);
        double pd = HubLayout.pd, cw = HubLayout.cw, ch = HubLayout.ch;
        FillRR(pd - 2, pd - 2, cw + 4, ch + 4, 22, SBrush(FA(0xC0060710, f)));
        CropGeom(out double kx, out double ky, out double fx, out double fy, out double fs);
        double pul = (Math.Sin(DecT(now) * 0.005) + 1) / 2, emb = (Math.Sin(DecT(now) * 0.0016) + 1) / 2;
        double ei = 1 - Math.Pow(1 - Clamp((now - _cropAt) / 420.0, 0.0, 1.0), 3);
        int stCard = PushXform(kx + CW_C / 2, ky + CH_C / 2 - 10 * coE, (0.955 + 0.045 * ei) * (1 - 0.09 * coE), 0);
        ShadowDraw(kx, ky + 3, CW_C, CH_C, 16, 10, 9, 9, f);
        FillRR(kx, ky, CW_C, CH_C, 16, VBrush(kx, ky, CW_C, CH_C, FA(0xF2222438, f), FA(0xF8121423, f)));
        // ---- the card, in the suite's language ----
        // It was a plain plate with a Mini backdrop and one hairline, next to
        // cards that all carry a rail, a scan band, corner arcs and a section
        // rule. Nothing here is decoration for its own sake: the rail says which
        // card is live, the arcs make the corners read as corners, and the rule
        // separates the frame from its controls.
        int stC = PushG(); ClipRR(kx, ky, CW_C, CH_C, 16);
        PanelBackdrop(kx, ky, CW_C, CH_C, acc, f, now, 0.95);
        FillRect(kx, ky, CW_C, CH_C, LineBrush(kx - 1, ky - 1, CW_C + 2, CH_C + 2, FA(0x0EFFFFFF, f), 0x00FFFFFF, 2));
        double per = 3400.0, swx = kx - 26 + (CW_C + 52) * (DecT(now) % per / per);
        uint c0s = AccHi(acc, 0.3) & 0xFFFFFF, c1s = FA(Alpha(c0s, 30), f);
        FillRect(swx - 24, ky, 24, CH_C, LineBrush(swx - 25, ky - 2, 26, CH_C + 4, c0s, c1s, 0));
        FillRect(swx, ky, 24, CH_C, LineBrush(swx - 1, ky - 2, 26, CH_C + 4, c1s, c0s, 0));
        FillRect(kx, ky, 14, CH_C, SBrush(FA(Alpha(acc, Math.Min(255, 46 + 24 * pul + 16 * emb)), f)));
        FillRect(kx, ky, 4.5, CH_C, VBrush(kx, ky, 4.5, CH_C, FA(Alpha(acc, 245), f), FA(Alpha(acc, R(110 + 40 * emb)), f)));
        Pop(stC);
        StrokeRR(kx, ky, CW_C, CH_C, 16, Pen(FA(0x28FFFFFF, f), 1));
        Line(kx + 18, ky + 1.5, kx + CW_C - 18, ky + 1.5, Pen(FA(0x16FFFFFF, f), 1));
        Line(kx + 18, ky + CH_C - 1.5, kx + CW_C - 18, ky + CH_C - 1.5, Pen(FA(0x30000000, f), 1));
        for (int k = 0; k < 4; k++)
        {
            double shim = (Math.Sin(DecT(now) * 0.005 + k * 1.5708) + 1) / 2;
            double xk = k == 1 || k == 2 ? kx + CW_C - 31.5 : kx + 0.5, yk = k >= 2 ? ky + CH_C - 31.5 : ky + 0.5;
            double ang = k == 0 ? 195 : k == 1 ? 285 : k == 2 ? 15 : 105;
            Arc(xk, yk, 31, 31, ang, 60, Pen(FA(Alpha(acc, R(105 + 40 * pul + 45 * shim)), f), 2));
        }
        Txt("CROP PICTURE", kx + 24, ky + 14, 240, 22, Fonts.fBrand, FA(0xF0E8EAF6, f), Fmt.L);
        Txt("drag to move  \u00B7  scroll to zoom", kx + 24, ky + 36, 260, 16, Fonts.fHint, FA(0x7EC7CBE0, f), Fmt.L);
        Txt(_srcPath == "" ? "built-in picture" : Path.GetFileName(_srcPath), kx + CW_C - 184, ky + 32, 160, 15, Fonts.fBadge, FA(0x62C7CBE0, f), Fmt.R);
        // the picture, cover-fitted to the frame at the pan / zoom
        double side = CropSide(), sc = fs / side;
        double w = _src.PixelSize.Width, h = _src.PixelSize.Height;
        double ix = fx + fs / 2 - _panX * sc, iy = fy + fs / 2 - _panY * sc;
        for (int i = 1; i <= 3; i++) FillRR(fx - i * 2, fy - i * 2 + 1, fs + i * 4, fs + i * 4, 12 + i * 2, SBrush(FA(Alpha(acc, R(14 - i * 4)), f)));
        int stF = PushG(); ClipRR(fx, fy, fs, fs, 12);
        DrawImage(_src, new Rect(ix, iy, w * sc, h * sc), new Rect(0, 0, w, h), f);
        var pnG = Pen(FA(Alpha(0xFFFFFF, 40), f), 1);
        Line(fx + fs / 3, fy, fx + fs / 3, fy + fs, pnG); Line(fx + 2 * fs / 3, fy, fx + 2 * fs / 3, fy + fs, pnG); Line(fx, fy + fs / 3, fx + fs, fy + fs / 3, pnG); Line(fx, fy + 2 * fs / 3, fx + fs, fy + 2 * fs / 3, pnG);
        Pop(stF);
        StrokeRR(fx, fy, fs, fs, 12, Pen(FA(Alpha(acc, 200), f), 1.6));
        Ell(fx + 2, fy + 2, fs - 4, fs - 4, Pen(FA(Alpha(0xFFFFFF, 70), f), 1));
        // the four crop-frame brackets, so the square reads as a viewfinder
        var pnK = Pen(FA(Alpha(AccHi(acc, 0.4), R(180 + 60 * pul)), f), 2);
        for (int k = 0; k < 4; k++)
        {
            double sgx = k == 1 || k == 2 ? -1 : 1, sgy = k >= 2 ? -1 : 1;
            double bx2 = k == 1 || k == 2 ? fx + fs : fx, by2 = k >= 2 ? fy + fs : fy;
            Line(bx2 + sgx * 2, by2 + sgy * 12, bx2 + sgx * 2, by2 + sgy * 2, pnK);
            Line(bx2 + sgx * 2, by2 + sgy * 2, bx2 + sgx * 12, by2 + sgy * 2, pnK);
        }
        // ---- the controls ----
        // The zoom readout used to be laid at kx + 24 across the row the buttons
        // are on, and the USE button starts at kx + 60 - so the two overlapped.
        // It is its own row now, with the level drawn as a bar: a number nobody
        // can compare against anything is worth less than a filled track.
        double zy = ky + CH_C - 76;
        FadeLine(kx + 24, kx + CW_C - 24, zy - 14, 0x24FFFFFF, f);
        Txt("ZOOM", kx + 24, zy, 44, 14, Fonts.fBadge, FA(0x8AC7CBE0, f), Fmt.L);
        double zbx = kx + 72, zbw = CW_C - 152;
        FillRR(zbx, zy + 5, zbw, 4, 2, SBrush(FA(0x30FFFFFF, f)));
        double zt = Clamp((_zoom - 1.0) / 5.0, 0.0, 1.0);
        FillRR(zbx, zy + 5, Math.Max(4, zbw * zt), 4, 2, VBrush(zbx, zy + 5, Math.Max(4, zbw * zt), 4, FA(Alpha(AccHi(acc, 0.4), 230), f), FA(Alpha(acc, 190), f)));
        FillEll(zbx + zbw * zt - 5, zy + 2, 10, 10, SBrush(FA(Alpha(AccHi(acc, 0.5), 245), f)));
        Txt(_zoom.ToString("0.0") + "x", kx + CW_C - 74, zy, 50, 14, HL_M(), FA(Alpha(0xFFE8EAF6, 225), f), Fmt.R);
        double by = ky + CH_C - 46;
        Upd.BtnDraw(hub, 1226, kx + CW_C / 2 - 130, by, 120, 30, "USE", acc, f, now, true);
        Upd.BtnDraw(hub, 1227, kx + CW_C / 2 + 10, by, 120, 30, "CANCEL", acc, f, now, false);
        Pop(stCard);
    }
}
