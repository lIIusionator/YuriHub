using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Yuri.Core;
using Yuri.Modules.FastFlags;
using Yuri.Modules.Special;
using Yuri.Modules.LoginItems;
using Yuri.Modules.DeviceOpt;
using Yuri.Modules.Cursor;
using Yuri.Modules.ClientSettings;
using Yuri.Modules.ScriptHub;
using Yuri.Modules.Forsaken;
using Yuri.Gfx;
using Yuri.Shell;
using Yuri.Shell.Hub;
using Yuri.Shell.Hub.Tabs;
using Yuri.Shell.Loading;

// Renders the surfaces headlessly (real Skia, no display) and saves PNGs, so a
// port can be eyeballed against the .ahk without a desktop; `test` drives the
// hub with synthetic input and asserts what the .ahk's handlers would have done.
AppBuilder.Configure<Yuri.App>()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .UseSkia()
    .SetupWithoutStarting();

Surface.OnFrameError = ex => throw new Exception("frame threw: " + ex, ex);
FfmComm.NoNetwork = true;
string what = args.Length > 0 ? args[0] : "loading";
if (what == "test") return Tests.Run();
if (what == "commsync")
{
    // One real sync against the live repository: the thing the loader waits on.
    Paths.InitDirs(); FfmComm.NoNetwork = false;
    try { Directory.Delete(FfmComm.Root, true); } catch { }
    var t = FfmComm.Sync(true);
    long tS = Clock.Tick;
    while (!t.IsCompleted && Clock.Tick - tS < 60000) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    Console.WriteLine($"sync done={t.IsCompleted} msg='{FfmComm.Msg}' entries={FfmComm.Entries.Count}");
    foreach (var e in FfmComm.Entries) Console.WriteLine($"  {e.Kind,-8} {e.Folder,-12} place={e.Place,-16} sets={e.Sets.Count}");
    return 0;
}
if (what == "bench")
{
    // What a frame actually costs, per tab. Guessing at what is hot is how a
    // session gets spent optimizing the wrong thing.
    Paths.InitDirs(); HubState.Load(); Fonts.Init(HubState.ProfileFont);
    Yuri.Gfx.Pool.Load();
    Yuri.Modules.FastFlags.FfmPanel.Register();
    Yuri.Modules.Special.SpfPanel.Register();
    Yuri.Modules.LoginItems.LgiPanel.Register();
    Yuri.Modules.DeviceOpt.DopPanel.Register();
    Yuri.Modules.Cursor.CurPanel.Register();
    Yuri.Modules.ClientSettings.RSetPanel.Register();
    Yuri.Modules.Forsaken.FskPanel.Register();
    Yuri.Shell.Hub.Gallery.Register();
    // the two the person actually complains about, measured the same way
    foreach (string mode in new[] { "collapsed", "dragging" })
    {
        var hq = new HubSurface { Tab = 1, TabPrev = 1 };
        var ws = new LayeredWindow(hq, "YURIBENCH" + mode, topmost: false, toolWindow: true);
        ws.Show();
        for (int i = 0; i < 200; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); System.Threading.Thread.Sleep(2); }
        if (mode == "collapsed") { hq.HubMin(1); for (int i = 0; i < 400; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); System.Threading.Thread.Sleep(4); } }
        else { hq.HL.dragT = 1.0; hq.HL.drag = 2; hq.HL.dragAt = Clock.Tick - 500; }
        HubState.Prof = true; Perf.Reset();
        var rb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(Math.Max(1,(int)(hq.Width)), Math.Max(1,(int)(hq.Height))), new Vector(96, 96));
        var s5 = System.Diagnostics.Stopwatch.StartNew();
        // the dispatcher runs between frames in the real app, and the drag's
        // snapshot is captured there
        for (int i = 0; i < 40; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); rb.Render(hq); }
        s5.Stop(); rb.Dispose();
        Console.WriteLine($"{mode,-10}: draw {s5.Elapsed.TotalMilliseconds / 40,6:0.00} ms   period {hq.Period,4} ms   surface {hq.Width}x{hq.Height}   holding: {hq.AnimWhy}");
        Console.WriteLine("            " + Perf.Report(40));
        HubState.Prof = false;
        hq.Stop(); ws.Close();
    }
    foreach (int tab in new[] { 1, 2, 3, 4, 5, 6 })
    {
        var hb = new HubSurface { Tab = tab, TabPrev = tab };
        var wb = new LayeredWindow(hb, "YURIBENCH" + tab, topmost: false, toolWindow: true);
        wb.Show();
        for (int i = 0; i < 200; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); System.Threading.Thread.Sleep(2); }
        HubState.Prof = true; Perf.Reset();
        var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)(hb.Bw * hb.K), (int)(hb.Bh * hb.K)), new Vector(96, 96));
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 40; i++) rtb.Render(hb);
        sw2.Stop(); rtb.Dispose();
        Console.WriteLine($"tab {tab}: draw {sw2.Elapsed.TotalMilliseconds / 40,6:0.00} ms   idle period {hb.Period,4} ms   holding: {hb.AnimWhy}");
        Console.WriteLine("        " + Perf.Report(40));
        HubState.Prof = false;
        hb.Stop(); wb.Close();
    }
    return 0;
}

Paths.InitDirs();
HubState.Load();
if (args.Length > 1 && args[1] == "light") { HubState.Theme = 1; HubState.ThT = 1.0; }
Fonts.Init(HubState.ProfileFont);

int tabN = 1;
if (what.StartsWith("hub:")) { tabN = int.Parse(what[4..]); what = "hub"; }
if (what == "abcard" || what == "puzcard")
{
    Yuri.Gfx.Pool.Load(); Yuri.Gfx.Pool.ProfPicSet = true; Yuri.Gfx.Pool.SelCur = Yuri.Gfx.Pool.SelNext = 2;
    Yuri.Modules.Forsaken.Keys.BindKey = "XB2"; Yuri.Modules.Forsaken.Keys.MkKey = "XB1"; Yuri.Modules.Forsaken.Keys.PuzKey = "XB2";
    Yuri.Modules.Forsaken.Ab.On = true; Yuri.Modules.Forsaken.Ab.Enabled = true;
    Yuri.Modules.Forsaken.Puz.On = true; Yuri.Modules.Forsaken.Puz.Sess = 2; Yuri.Modules.Forsaken.Puz.SpeedT = 0.7; Yuri.Modules.Forsaken.Puz.SpeedApply();
}
Surface surf = what == "hub" ? new HubSurface() : what == "gate" ? MakeGate() : what == "abcard" ? new Yuri.Modules.Forsaken.AbCard() : what == "puzcard" ? new Yuri.Modules.Forsaken.PuzCard() : what == "griddbg" ? new Yuri.Modules.Forsaken.DbgCard() : what == "calcard" ? new Yuri.Modules.Forsaken.CalCard() : new LoadingSurface(() => { });
static Surface MakeGate() { Yuri.Gfx.Pool.Load(); return new Yuri.Shell.Gate.GateSurface(_ => { }); }
if (surf is HubSurface hs && tabN != 1) { hs.Tab = tabN; hs.TabPrev = tabN; hs.HL.navY = hs.HL.NavY(Math.Min(tabN, 6)); }
if (surf is HubSurface hsT && args.Length > 1 && args[1].StartsWith("view:tut")) { int stepN = args[1].Length > 8 ? int.Parse(args[1][8..]) : 1; Ffm.Boot(); FfmPanel.Register(); CurPanel.Register(); RSetPanel.Register(); Yuri.Modules.Special.SpfPanel.Register(); DopPanel.Register(); LgiPanel.Register(); Tut.Start(false); for (int i = 2; i <= stepN; i++) Tut.Go(i); Tut.At -= 2000; Tut.StepAt -= 2000; }
if (surf is HubSurface hsW && args.Length > 1 && args[1].StartsWith("view:town")) { int tk = int.Parse(args[1][9..]); Yuri.Modules.Town.Tw.Town = tk; Yuri.Modules.Town.Tw.N[0] = 9; Yuri.Modules.Town.Tw.N[1] = 27; Yuri.Modules.Town.Tw.N[2] = 44; Yuri.Modules.Town.Tw.at = Clock.Tick - 1000; Yuri.Modules.Town.Tw.sx = 50; Yuri.Modules.Town.Tw.sy = 12; Yuri.Modules.Town.Tw.kind = 1; hsW.Tab = 7; hsW.TabPrev = 7; }
if (surf is HubSurface hsF && args.Length > 1 && args[1].StartsWith("view:flappy")) { Yuri.Modules.Town.Tw.Town = 4; hsF.Tab = 7; hsF.TabPrev = 7; Yuri.Modules.Town.Fl.coins = 42; Yuri.Modules.Town.Fl.best = 17; if (args[1].EndsWith("shop")) { Yuri.Modules.Town.Fl.shop = true; Yuri.Modules.Town.Fl.birds = 7; Yuri.Modules.Town.Fl.bird = 2; } else { Yuri.Modules.Town.Fl.Reset(); Yuri.Modules.Town.Fl.on = true; Yuri.Modules.Town.Fl.score = 12; Yuri.Modules.Town.Fl.world = 3; Yuri.Modules.Town.Fl.bird = 5; Yuri.Modules.Town.Fl.y = 40; Yuri.Modules.Town.Fl.pipes.Add(new Yuri.Modules.Town.FPipe { x = 60, gy = 30, coin = true }); Yuri.Modules.Town.Fl.pipes.Add(new Yuri.Modules.Town.FPipe { x = 110, gy = 48, coin = true }); Yuri.Modules.Town.Fl.lastT = Clock.Tick; Yuri.Modules.Town.Fl.spawnAt = Clock.Tick + 99999; } }
if (what == "calcard") { Yuri.Modules.Forsaken.Puz.On = true; Yuri.Modules.Forsaken.Puz.Cal = 1; }
if (surf is HubSurface hsCr && args.Length > 1 && args[1] == "view:crop") { Yuri.Gfx.Pool.Load(); hsCr.Tab = 6; hsCr.TabPrev = 6; Yuri.Shell.Hub.Gallery.CropTest(); }
if (surf is Yuri.Modules.Forsaken.FskCard fc0 && args.Length > 1 && args[1].StartsWith("view:bub")) { fc0.MinT_Public = 1.0; if (args[1].EndsWith("on")) Yuri.Modules.Forsaken.Ab.Enabled = true; }
if (surf is Yuri.Modules.Forsaken.FskCard fc1 && args.Length > 1 && args[1].StartsWith("view:fold")) { Yuri.Modules.Forsaken.Ab.On = true; Yuri.Modules.Forsaken.Ab.Enabled = true; fc1.MinT_Public = double.Parse(args[1][10..], System.Globalization.CultureInfo.InvariantCulture); }
if (surf is HubSurface hsD && args.Length > 1 && args[1] == "view:drag") { hsD.HL.dragT = 1.0; hsD.HL.drag = 2; hsD.HL.dragAt = Clock.Tick - 500; }
if (surf is HubSurface hsM && args.Length > 1 && args[1].StartsWith("view:min")) { Yuri.Gfx.Pool.Load(); Yuri.Gfx.Pool.ProfPicSet = true; hsM.HL.minT = hsM.HL.minV = hsM.HL.minTo = 1.0; hsM.HL.minStart = 0; hsM.HL.mbIdx = hsM.HL.mbNext = Yuri.Gfx.Pool.GalLo; hsM.HL.mbAt = Clock.Tick; if (args[1].EndsWith("hov")) { hsM.H[9] = 1.0; hsM.Hz.Add(9); } if (args[1].EndsWith("arm")) hsM.HL.armT = 1.0; }
if (surf is HubSurface hsG && args.Length > 1 && args[1] == "view:gal") { Yuri.Gfx.Pool.Load(); Yuri.Gfx.Pool.ProfPicSet = true; Yuri.Gfx.Pool.SelCur = Yuri.Gfx.Pool.SelNext = 3; Gallery.Show(0, false); Gallery.IntroAt -= 1000; }
if (surf is HubSurface hsP && args.Length > 1 && args[1] == "view:prof") { Yuri.Gfx.Pool.Load(); Yuri.Gfx.Pool.ProfPicSet = true; Yuri.Gfx.Pool.SelCur = Yuri.Gfx.Pool.SelNext = 2; HubState.ProfName = "ZEAL"; HubState.ProfBio = "lunaris made the flags"; HubState.ProfRing = 3; EditProfile.Open(); EditProfile.T = 1; }
if (surf is HubSurface hsU && args.Length > 1 && args[1] == "view:upd") { Upd.NoNetwork = true; Upd.Ver = "2.4.1"; Upd.Auto = true; Upd.Set(2); Upd.Raise(); Upd.GateAt -= 2000; Upd.At -= 2000; }
if (args.Length > 1 && args[1] == "view:scr")
{
    Scr.List.Clear();
    Scr.SetText("#Requires AutoHotkey v2\n; toggles the overlay\nF1::{\n    static on := false\n    on := !on\n    ToolTip(on ? \"overlay on\" : \"\")\n    Sleep(800)\n}\n\nWinWait(\"Roblox\", , 30)\nMsgBox(\"ready\", \"YURI\", 0x40)");
    Scr.Name = "OVERLAY TOGGLE"; Scr.Desc = "F1 flips the tooltip"; Scr.FocusSet(1); Scr.Cl = 6; Scr.Cc = 12; Scr.Sl = 6; Scr.Sc = 4; Scr.SelOn = true; Scr.CaretAt = long.MaxValue / 2;
    Scr.OutOk = 1; Scr.OutVer = "2"; Scr.OutMsg = "no errors - parses clean as AutoHotkey v2"; Scr.OutAt = Clock.Tick - 5000;
    string sd = Path.Combine(Path.GetTempPath(), "yuri-scr-snap"); Directory.CreateDirectory(sd);
    foreach (var (n, d, pid) in new[] { ("AUTO CLICKER", "left click every 40 ms while F6 is held", 1), ("ANTI AFK", "nudges the camera every four minutes", 0), ("QUICK REJOIN", "rejoins the last server on F8", 0) })
    { string fl = Path.Combine(sd, n.Replace(' ', '_') + ".ahk"); File.WriteAllText(fl, "; " + n); Scr.List.Add(new ScrItem { Name = n, Desc = d, File = fl, Pid = pid == 1 ? Environment.ProcessId : 0 }); }
    Scr.EditIdx = 2;
}
if (surf is HubSurface hs2 && args.Length > 1 && args[1].StartsWith("mod:")) { Ffm.Boot(); FfmPanel.Register(); if (Ffm.Flags.Count == 0) { Ffm.Add("FFlagDebugGraphicsPreferVulkan", "true"); Ffm.Add("DFIntTaskSchedulerTargetFps", "9999"); Ffm.Add("FStringPartTexturePackTable2022", "{}"); Ffm.Add("DFFlagDebugPerfMode", "false"); Ffm.Add("FIntRenderShadowIntensity", "0"); } hs2.HubModSel(int.Parse(args[1][4..]));
    if (args.Length > 2 && args[2] == "view:db") { FfmViews.Db.AddRange(new[] { "DFFlagDebugPerfMode", "DFIntTaskSchedulerTargetFps", "FFlagDebugGraphicsPreferVulkan", "FFlagDebugSkyGray", "FIntRenderShadowIntensity", "FStringPartTexturePackTable2022" }); FfmViews.OpenView("db"); }
    else if (args.Length > 2 && args[2] == "view:log") { Ffm.Snap("import"); Ffm.Snap("delete"); FfmViews.OpenView("log"); FfmViews.LogTabSet(3); }
    else if (args.Length > 2 && args[2] == "view:det") { Detail.FfmDetail(3); }
    else if (args.Length > 2 && args[2] == "view:comm") { FfmComm.NoNetwork = true; Yuri.Platform.GameInfo.NoNetwork = true; FfmViews.OpenView("comm"); }
    if (args.Length > 1 && args[1] == "mod:3") { CurPanel.Register(); string tmp = Path.Combine(Path.GetTempPath(), "yuri-cur-arrow.png"); Yuri.Gfx.Img.Asset("avatar.jpg")?.Save(tmp); Cur.SetImage(1, tmp); }
    if (args.Length > 2 && args[1] == "mod:1" && args[2] == "view:qsel") { FfmField.Begin("q", 0); FfmField.Char("DebugGraphicsPrefer"); FfmField.Sel = 5; FfmField.Car = 13; FfmField.CaretAt = long.MaxValue / 2; }
    if (args.Length > 1 && args[1] == "mod:2")
    {
        Yuri.Modules.Forsaken.FskPanel.Register();
        Yuri.Modules.Forsaken.Keys.BindKey = "F6"; Yuri.Modules.Forsaken.Keys.PuzKey = "F8";
        Yuri.Modules.Forsaken.Ab.On = true; Yuri.Modules.Forsaken.Puz.On = true; Yuri.Modules.Forsaken.Puz.SpeedT = 0.7; Yuri.Modules.Forsaken.Puz.SpeedApply(); Yuri.Modules.Forsaken.Puz.Sess = 3;
        if (args.Length > 2 && args[2] == "view:clash") { Yuri.Modules.Forsaken.Puz.ClashRaise("EXTERNAL BLOCK REBIND", "PUZZLE AI", "XB2"); Yuri.Modules.Forsaken.Puz.ClashAt = Clock.Tick - 3000; }
        if (args.Length > 2 && args[2] == "view:rebind") { Yuri.Modules.Forsaken.Keys.RebindOn = true; Yuri.Modules.Forsaken.Keys.RebindTgt = 3; }
    }
    if (args.Length > 1 && args[1] == "mod:4")
    {
        string inst = Path.Combine(Path.GetTempPath(), "yuri-fake-rset"); Directory.CreateDirectory(inst);
        string xml = Path.Combine(inst, "GlobalBasicSettings_13.xml");
        File.WriteAllText(xml, "<roblox><Item class=\"UserGameSettings\"><Properties>\n\t\t<int name=\"FramerateCap\">60</int>\n\t\t<token name=\"SavedQualityLevel\">7</token>\n\t\t<token name=\"GraphicsQualityLevel\">7</token>\n\t\t<float name=\"MasterVolume\">0.5</float>\n\t\t<bool name=\"Fullscreen\">true</bool>\n\t</Properties></Item></roblox>");
        RSet.NoProc = true; RSet.XmlOverride = () => new List<string> { xml }; RSet.PathsOverride = () => new List<string> { Path.Combine(inst, "ClientSettings") };
        RSetPanel.Register();
        RSet.ApplyPreset(2); RSet.SetPick(RSet.By("vol")!, 11); RSet.Extra["FFlagDebugSkyGray"] = "True"; RSet.FxBump();
        RSet.Say("MEDIUM PRESET STAGED  \u00B7  13 ROW(S) CHANGED  -  PRESS APPLY", RSet.C_ON);
        if (args.Length > 2 && args[2] == "view:dd") { RSet.Scr = RSet.ScrT = 3 * 52 + 26 + 40; RSet.DdOpen(5); RSet.DdT = 1; }
        if (args.Length > 2 && (args[2] == "view:fx" || args[2] == "view:fxdb")) { RSetFx.Open(); RSetFx.FxAt -= 1000; RSet.Extra["DFIntTaskSchedulerTargetFps"] = "9999"; RSet.FxBump(); RSetFx.FxName = "FFlagDebug"; if (args[2] == "view:fxdb") { RSetFx.DbOpen(true); RSetFx.DbAt -= 1000; RSetFx.DbQ = "Shadow"; } else { FfmField.Begin("rfv", 0); FfmField.Char("Tr"); } }
        if (args.Length > 2 && args[2] == "view:xml") { RSet.Scr = RSet.ScrT = 18 * 52 + 26 * 2 - 30; }
        if (args.Length > 2 && args[2] == "view:logo") { string lg = Path.Combine(inst, "mylogo.png"); Yuri.Gfx.Img.Asset("avatar.jpg")!.Save(lg); RSetLogo.TargetsOverride = (k, sub) => k == "1" ? new List<RSetLogo.Tgt> { new(Path.Combine(inst, "a.png"), inst, "a.png", "ui\\a.png", false) } : new(); RSet.Logo = lg; RSetLogo.LogoLoad(1); RSet.Pick["logo"] = 2; RSet.Scr = RSet.ScrT = 13 * 52 + 26 - 20; RSet.Say("PLAYER LOGO READY - mylogo.png  -  1 TARGET(S)  -  PRESS APPLY", RSet.C_ON); }
    }
    if (args.Length > 1 && args[1] == "mod:6") { DopPanel.Register(); Dop.Say("INPUT LATENCY APPLIED", Dop.C_ON); }
    if (args.Length > 1 && args[1] == "mod:7") { LgiPanel.Register(); Lgi.Dry = true; if (Lgi.Items.Count == 0) { Lgi.AddItem("C:\\Program Files\\Discord\\Discord.exe", true, true, false); Lgi.AddItem("C:\\Games\\overlay.lnk", true, false, false); } Lgi.Rbx = 1; Lgi.Say("ADDED DISCORD  -  OPENS AND CLOSES WITH ROBLOX"); }
    if (args.Length > 1 && args[1] == "mod:5") { Yuri.Modules.Special.SpfPanel.Register(); Yuri.Modules.Special.Spf.Discord = true; Yuri.Modules.Special.Spf.Region = true; Yuri.Modules.Special.Spf.MmLink = "https://www.roblox.com/games/2753915549";
        if (args.Length > 2 && args[2] == "view:sav") { Yuri.Platform.GameInfo.NoNetwork = true; SpfSaved.Saved.Clear(); SpfSaved.SavedAdd("2753915549"); SpfSaved.SavedAdd("920587237"); SpfSaved.SavedAdd("4924922222"); Spf.ViewOpen("sav"); }
        if (args.Length > 2 && args[2] == "view:sel") { Yuri.Platform.GameInfo.NoNetwork = true; SpfSaved.Saved.Clear(); Spf.ViewOpen("sav"); SpfSaved.AddOpenSheet(); FfmField.Char("2753915549"); FfmField.Sel = 2; FfmField.Car = 7; FfmField.CaretAt = long.MaxValue / 2; }
        if (args.Length > 2 && args[2] == "view:add") { Yuri.Platform.GameInfo.NoNetwork = true; SpfSaved.Saved.Clear(); Spf.ViewOpen("sav"); SpfSaved.AddOpenSheet(); FfmField.Char("2753915549"); }
        if (args.Length > 2 && args[2] == "view:acct") { SpfAcct.NoNetwork = true; SpfAcct.Acct.Clear(); SpfAcct.Acct.Add(new Yuri.Modules.Special.Account { Nm = "zeal", Id = "1", Ck = "x" }); SpfAcct.Acct.Add(new Yuri.Modules.Special.Account { Nm = "alt account", Id = "2", Ck = "y" }); SpfAcct.Sel = 1; Spf.ViewOpen("acct"); } } }
var win = new LayeredWindow(surf, "snap", true, true);
win.Show();
int[] times = what == "hub" ? new[] { 2600 } : what == "gate" ? new[] { 1800 } : what == "abcard" || what == "puzcard" ? new[] { 1400 } : new[] { 60, 900, 2300, 3600 };
long t0 = Clock.Tick;
foreach (var t in times)
{
    while (Clock.Tick - t0 < t)
    {
        if (what is "hub" or "gate" or "abcard" or "puzcard") { surf.InvalidateVisual(); Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
        Dispatcher.UIThread.RunJobs(); Thread.Sleep(5);
    }
    surf.InvalidateVisual();
    Dispatcher.UIThread.RunJobs();
    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    Dispatcher.UIThread.RunJobs();
    var bmp = win.CaptureRenderedFrame();
    if (bmp is null) { Console.WriteLine("no frame at " + t); continue; }
    var path = what == "hub" ? $"/home/claude/snaps/hub{tabN}{(args.Length > 1 ? "_" + string.Join("_", args.Skip(1)).Replace(":", "") : "")}.png" : $"/home/claude/snaps/{what}_{t}.png";
    Directory.CreateDirectory("/home/claude/snaps");
    bmp.Save(path);
    Console.WriteLine($"{path} {bmp.PixelSize} scale={surf.K:0.###}");
}
return 0;

static class Tests
{
    static int _fails;
    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
        if (!ok) _fails++;
    }
    static void Pump(Surface s, int frames, int sleepMs = 4)
    {
        for (int i = 0; i < frames; i++)
        {
            s.InvalidateVisual();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            if (sleepMs > 0) Thread.Sleep(sleepMs);
        }
    }
    static void Click(LayeredWindow w, HubSurface h, double x, double y)
    {
        var p = new Point(x * h.K, y * h.K);
        w.MouseMove(p); Pump(h, 2);
        w.MouseDown(p, MouseButton.Left); Pump(h, 2);
        w.MouseUp(p, MouseButton.Left); Pump(h, 3);
    }

    public static int Run()
    {
        Console.WriteLine("== ini ==");
        var tmp = Path.Combine(Path.GetTempPath(), "yuri-ini-test");
        Directory.CreateDirectory(tmp);
        var f = Path.Combine(tmp, "zeal.ini");
        File.Delete(f);
        Ini.Write(f, "hub", "accent", "0xFFFB7185");
        Ini.Write(f, "hub", "opacity", 0.875);
        Ini.Write(f, "profile", "name", "ZEAL = the one");
        Ini.Write(f, "HUB", "theme", 1);
        var bytes = File.ReadAllBytes(f);
        Check(bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xFE, "new file is UTF-16 LE with BOM (AutoHotkey v2)");
        Check(Ini.ReadArgb(f, "hub", "accent", 0) == 0xFFFB7185, "ReadArgb round trip");
        Check(Math.Abs(Ini.ReadNum(f, "hub", "opacity", 0) - 0.875) < 1e-9, "ReadNum round trip");
        Check(Ini.Read(f, "profile", "name") == "ZEAL = the one", "a value containing '=' survives");
        Check(Ini.ReadInt(f, "hub", "THEME", 0) == 1, "section and key are case-insensitive, written into the existing section");
        Check(File.ReadAllText(f, System.Text.Encoding.Unicode).Split("[hub]").Length == 2, "no duplicate [hub] section");
        Check(Ini.Read(f, "nope", "x", "dflt") == "dflt", "missing section -> default");
        Ini.Delete(f, "hub", "theme");
        Check(Ini.Read(f, "hub", "theme", "gone") == "gone", "IniDelete removes the key");
        var ansi = Path.Combine(tmp, "legacy.ini");
        File.WriteAllBytes(ansi, System.Text.Encoding.Latin1.GetBytes("[hub]\r\naccent=0xFF38BDF8\r\nlowperf=1\r\n"));
        Check(Ini.ReadArgb(ansi, "hub", "accent", 0) == 0xFF38BDF8 && Ini.ReadInt(ansi, "hub", "lowperf", 0) == 1, "legacy ANSI file reads");
        Ini.Write(ansi, "hub", "scale", "1.4");
        var b2 = File.ReadAllBytes(ansi);
        Check(!(b2[0] == 0xFF && b2[1] == 0xFE) && Ini.ReadNum(ansi, "hub", "scale", 0) == 1.4, "legacy file keeps its encoding on write");

        Console.WriteLine("== fast flags ==");
        {
            Paths.InitDirs();
            try { File.Delete(Ffm.File); File.Delete(Ffm.HistFile); } catch { }
            Ffm.Boot();
            Check(Ffm.Flags.Count == 0, "starts empty");
            Check(Ffm.Add("FFlagDebugGraphicsPreferVulkan", "true") == 1, "adds a flag");
            Check(Ffm.Add("DFIntTaskSchedulerTargetFps", "9999") == 1, "adds an int");
            Check(Ffm.Add("FFlagDebugGraphicsPreferVulkan", "false") == 2, "same name updates");
            Check(Ffm.Add("DFIntTaskSchedulerTargetFps", "lots") == 0 && Ffm.BadN == 1, "a non-integer is refused for a DFInt");
            Check(Ffm.Add("FFlagNope", "maybe") == 0, "a non-bool is refused for a FFlag");
            Check(Ffm.Add("FStringPartTexturePackTable2022", "{\"foo\":\"bar\"}") == 1, "strings take anything, escaped");
            Check(Ffm.Add("FFlagNoValue", "") == 1 && Ffm.Flags[^1].Value == "true", "an empty value takes the prefix's default");
            Check(Ffm.Flags.Count == 4, "four staged");
            Ffm.ToggleOn(1);
            Check(!Ffm.Flags[1].On, "toggle off");
            var json = Ffm.Json();
            Check(!json.Contains("TaskScheduler") && json.Contains("\"FFlagDebugGraphicsPreferVulkan\": \"False\""), "switched-off flags are kept out of the json, bools normalised");
            var saved = File.ReadAllText(Ffm.File);
            Check(saved.StartsWith("[{\"name\":\"FFlagDebugGraphicsPreferVulkan\",\"value\":\"false\",\"type\":\"bool\"},{\"name\":\"DFIntTaskSchedulerTargetFps\",\"value\":\"9999\",\"type\":\"int\",\"on\":\"0\"}"), "zeal_flags.json is the .ahk's own format");
            Ffm.Flags.Clear(); Ffm.Boot();
            Check(Ffm.Flags.Count == 4 && !Ffm.Flags[1].On && Ffm.Flags[2].Value == "{\"foo\":\"bar\"}", "reloads with escapes and the on/off state");
            int n = Ffm.ParseInto("{ \"FFlagA\": true, \"DFIntB\": 12, \"FStringC\": \"x y\", \"DFIntBad\": \"z\" }");
            Check(n == 3 && Ffm.Flags.Count == 7, "import takes three of four pairs");
            Check(Ffm.Hist.Count == 1 && Ffm.Hist[0].Act == "import" && Ffm.Hist[0].Flags.Count == 4, "the import snapshotted the list before it");
            Ffm.Del(0);
            Check(Ffm.Flags.Count == 6 && Ffm.Hist.Count == 2 && Ffm.IndexOf("DFIntTaskSchedulerTargetFps") == 0, "delete reindexes and snapshots");
            Ffm.HistRestore(1);
            Check(Ffm.Flags.Count == 4 && Ffm.Hist.Count == 3 && Ffm.Hist[0].Act == "restore", "restore brings the pre-import list back and snapshots the way back");
            Ffm.Hist.Clear(); Ffm.HistLoad();
            Check(Ffm.Hist.Count == 3 && Ffm.Hist[2].Act == "import", "zeal_hist.json round trip");
            Ffm.ClearAll();
            Check(Ffm.Flags.Count == 0 && Ffm.Msg.StartsWith("CLEARED 4"), "clear all");
            Check(Ffm.AppSettingsPaths() is { } paths && paths.Count == 0, "no client installed here: no delivery folders");
        }

        Console.WriteLine("== update pass ==");
        {
            try { File.Delete(Ffm.File); File.Delete(Ffm.HistFile); } catch { }
            Ffm.Boot();
            Ffm.Bulk = true;
            Ffm.Add("FFlagDebugGraphicsPreferVulkan", "true");        // current
            Ffm.Add("DFIntTaskSchedulerTargetFPS", "9999");           // same name, other case: the .ahk's = is case-insensitive, so CURRENT
            Ffm.Add("FFlagRenderShadowIntensity", "true");            // a dropped word: RENAMED
            Ffm.Add("FIntPhysicsFoo", "0");                           // wrong prefix, right bare: PREFIX
            Ffm.Add("FFlagNothingRemotelyLikeThis", "false");         // SKIPPED
            Ffm.Add("DebugSkyGray", "true");                          // bare and current: counted, NOTE
            Ffm.Bulk = false; Ffm.Reindex(); Ffm.SaveFlags();
            var liveList = new List<string> { "FFlagDebugGraphicsPreferVulkan", "DFIntTaskSchedulerTargetFps", "FFlagRenderShadowIntensityFix", "FFlagDebugSkyGray", "DFIntPhysicsFoo", "FFlagRenderShadowMapSwapped" };
            for (int i = 0; i < 15000; i++) liveList.Add("FFlagSynthetic" + i);
            FfmUpdate.UpdateGot(liveList, 3, 3);
            Check(!FfmUpdate.LivePartial, "a full list is not partial");
            Check(Ffm.Flags[0].Name == "FFlagDebugGraphicsPreferVulkan", "a current name is left alone");
            var u1 = FfmViews.Upd.FirstOrDefault(u => u.Old == "DFIntTaskSchedulerTargetFPS");
            Check(u1 is null && Ffm.Flags[1].Name == "DFIntTaskSchedulerTargetFPS", "a case-only difference counts as current, as the .ahk's case-insensitive compare had it");
            var u2 = FfmViews.Upd.FirstOrDefault(u => u.Old == "FFlagRenderShadowIntensity");
            Check(u2 is { Kind: "RENAMED", New: "FFlagRenderShadowIntensityFix" } && Ffm.Flags[2].Name == "FFlagRenderShadowIntensityFix", "a dropped word renames");
            var u3 = FfmViews.Upd.FirstOrDefault(u => u.Old == "FIntPhysicsFoo");
            Check(u3 is { Kind: "PREFIX", New: "DFIntPhysicsFoo" } && Ffm.Flags[3].Name == "DFIntPhysicsFoo", "a wrong prefix takes the live form that fits the value");
            var u4 = FfmViews.Upd.FirstOrDefault(u => u.Old == "FFlagNothingRemotelyLikeThis");
            Check(u4 is { Kind: "SKIPPED" } && Ffm.Flags[4].Name == "FFlagNothingRemotelyLikeThis", "nothing close is skipped, name kept");
            Check(FfmViews.Upd.Any(u => u.Kind == "NOTE" && u.Note.StartsWith("1 staged flag(s) are bare")), "a bare current name is noted");
            Check(Ffm.Msg.StartsWith("UPDATED 2 FLAG(S) - 3 CURRENT - 1 UNKNOWN"), "the summary counts: " + Ffm.Msg);
            Check(FfmUpdate.CanUndo && Ffm.Hist.Count == 1 && Ffm.Hist[0].Act == "update", "undo armed and the snapshot taken");
            FfmUpdate.UpdateUndo();
            Check(Ffm.Flags[2].Name == "FFlagRenderShadowIntensity" && Ffm.Flags[3].Name == "FIntPhysicsFoo" && !FfmUpdate.CanUndo, "UNDO restores the two names");
            // a swapped word (not merely dropped) is applied but flagged REVIEW
            Ffm.Bulk = true; Ffm.Add("FFlagRenderShadowMapSwapper", "true"); Ffm.Bulk = false; Ffm.Reindex();
            FfmUpdate.UpdateGot(liveList, 3, 3);
            var u5 = FfmViews.Upd.FirstOrDefault(u => u.Old == "FFlagRenderShadowMapSwapper");
            Check(u5 is { Kind: "REVIEW", New: "FFlagRenderShadowMapSwapped" }, "a swapped word renames with REVIEW: " + (u5?.Kind ?? "none") + " " + (u5?.Note ?? ""));
            FfmUpdate.UpdateUndo();
            FfmUpdate.NormalizePrefixes();
            Check(Ffm.Flags[5].Name == "FFlagDebugSkyGray" && Ffm.Msg.StartsWith("PREFIXED 1"), "PREFIX gives the bare name its live prefix");
            FfmUpdate.UpdateGot(new List<string> { "FFlagDebugGraphicsPreferVulkan" }, 1, 3);
            Check(FfmUpdate.LivePartial && FfmViews.Upd.Count(u => u.Kind == "SKIPPED") >= 3 && Ffm.Msg.Contains("LIST INCOMPLETE"), "a partial list judges nothing retired");
            Ffm.ClearAll(); Ffm.Hist.Clear(); Ffm.HistSave(); FfmViews.Upd.Clear();
        }

        Console.WriteLine("== community cache ==");
        {
            Check(FfmComm.BlobSha("hello\n") == "ce013625030ba8dba906f756967f9e9ca394464a", "git blob sha of 'hello\\n'");
            Check(FfmComm.Safe("a<b>:c/d\\e|f?g*h. ") == "a_b__c_d_e_f_g_h", "unsafe characters become underscores, trailing dots and spaces go");
            int cnt = FfmComm.CountPairs("// # Zeal\n{ \"FFlagA\": true, // note\n \"DFIntB\": 12 }", out string txt, out string who);
            Check(cnt == 2 && who == "Zeal" && !txt.Contains("# Zeal") && txt.Contains("// note"), "a set's author line and pair count; only whole comment lines are dropped");
            Check(FfmComm.CountPairs("[1,2]", out _, out _) == 0, "not a { } object: not a set");
            string sep = "\u0001";
            Directory.CreateDirectory(Path.Combine(FfmComm.Root, "General"));
            Directory.CreateDirectory(Path.Combine(FfmComm.Root, "Rivals"));
            File.WriteAllText(Path.Combine(FfmComm.Root, "General", "smooth.json"), "{ \"FFlagSmoothA\": true, \"DFIntSmoothB\": 3 }");
            File.WriteAllText(Path.Combine(FfmComm.Root, "Rivals", "aim.json"), "{ \"FFlagAimA\": false }");
            File.WriteAllText(Path.Combine(FfmComm.Root, "index.txt"),
                "H" + sep + "abc" + sep + "1\n" +
                "C" + sep + "General" + sep + "" + sep + "General" + sep + "\n" +
                "S" + sep + "General" + sep + "smooth.json" + sep + "Zeal" + sep + "2" + sep + "smooth.json" + sep + "sha1\n" +
                "C" + sep + "Rivals" + sep + "17625359962" + sep + "Rivals" + sep + "msha\n" +
                "S" + sep + "Rivals" + sep + "aim.json" + sep + "" + sep + "1" + sep + "aim.json" + sep + "sha2\n" +
                "S" + sep + "Rivals" + sep + "gone.json" + sep + "Luna" + sep + "5" + sep + "gone.json" + sep + "sha3\n");
            Check(FfmComm.IndexLoad() == 2 && FfmComm.Entries[0].Kind == "general" && FfmComm.Entries[1].Place == "17625359962", "the index parses into GENERAL and a game");
            var riv = FfmComm.Entries[1];
            Check(riv.Sets.Count == 2 && riv.Sets[0].Author == "UNKNOWN" && riv.Sets[0].Have && !riv.Sets[1].Have && riv.Sets[1].N == 5, "sets carry author, count and whether the file is cached");
            Ffm.ClearAll(); Ffm.Hist.Clear();
            FfmComm.Insert(0, 0);
            Check(Ffm.Flags.Count == 2 && Ffm.Flags[0].Name == "FFlagSmoothA" && Ffm.Hist.Count == 0 && Ffm.Msg.StartsWith("ZEAL / SMOOTH LOADED - 2"), "INSERT fills an empty list without a snapshot (nothing to keep)");
            FfmComm.Insert(1, 0);
            Check(Ffm.Flags.Count == 1 && Ffm.Flags[0].Name == "FFlagAimA" && Ffm.Hist.Count >= 1 && Ffm.Hist[0].Act == "community set", "INSERT over a list snapshots it first");
            FfmComm.Insert(1, 1);
            Check(Ffm.Flags.Count == 1 && !riv.Sets[1].Have && Ffm.Msg.Contains("MISSING FROM THE CACHE"), "a set missing from the cache is reported, the list untouched");
            Ffm.ClearAll(); Ffm.Hist.Clear(); Ffm.HistSave();
            try { Directory.Delete(FfmComm.Root, true); } catch { }
        }

        Console.WriteLine("== special features engine ==");
        {
            var logs = Path.Combine(Path.GetTempPath(), "yuri-test-logs");
            Directory.CreateDirectory(logs);
            foreach (var lf in Directory.GetFiles(logs)) File.Delete(lf);
            SpfEngine.LogDirOverride = logs;
            SpfEngine.NoNetwork = true; SpfEngine.Rethrow = true;
            SpfEngine.Register();
            Check(SpfEngine.IsPublicIP("128.116.1.2") && !SpfEngine.IsPublicIP("10.0.0.1") && !SpfEngine.IsPublicIP("192.168.1.1") && !SpfEngine.IsPublicIP("100.64.0.1"), "public IP test");
            Check(SpfEngine.DistKm("51.5", "-0.12", "48.85", "2.35") is > 330 and < 350, "haversine London-Paris ~343 km");
            var log = Path.Combine(logs, "a.log");
            File.WriteAllText(log, "2026-09-07T10:00:00.000Z,0.1,abc,6 [FLog::Output] hello\n");
            Spf.Srv = true; Spf.Region = false; Spf.Discord = false; Spf.Match = false; Spf.Odds = false; Spf.NoApp = false;
            SpfEngine.Sync();
            SpfEngine.Tick();
            Check(!Spf.InGame, "an idle log is no session");
            string ts = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss");
            string pre = DateTime.UtcNow.AddMinutes(-3).ToString("yyyyMMdd'T'HHmmss");
            File.AppendAllText(log, ts + ".000Z,0.1,abc,6 [FLog::Output] ! Joining game 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee' place 123456789 at 128.116.5.6\n"
                + ts + ".100Z,0.1,abc,6 [FLog::Output] Server Prefix: xxx_" + pre + "Z_RCC\n"
                + ts + ".200Z,0.1,abc,6 [FLog::Output] UDMUX Address = 128.116.9.9\n");
            SpfEngine.LiveOverride = () => new List<SpfEngine.Client> { new(4242, DateTime.UtcNow.AddMinutes(-5)) };   // one client, started before the log
            SpfEngine.Tick();
            Check(Spf.InGame && Spf.Place == "123456789" && Spf.Job == "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "the join line becomes the shown session");
            Check(SpfEngine.Ip == "128.116.9.9", "the UDMUX address replaces the join address");
            Check(Spf.Hist.Count == 1 && Spf.Hist[0].Place == "123456789", "SERVER DETAILS noted the server");
            Check(SpfEngine.SrvTypeLabel() == "public" && SpfEngine.SrvUptime().EndsWith("m"), "public, with an uptime from the Server Prefix");
            Check(Spf.SrvHintShort!().StartsWith("public  ·  up "), "the row's hint");
            File.AppendAllText(log, DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss") + ".500Z,0.1,abc,6 [FLog::Output] NetworkClient:Disconnect\n");
            SpfEngine.Tick();
            Check(!Spf.InGame && Spf.Place == "" && Spf.Hist.Count == 1, "the leave line ends the session, the history keeps it");
            SpfEngine.LiveOverride = () => new List<SpfEngine.Client>();
            Spf.Discord = true; SpfDiscord.Push();
            Check(Spf.Dc == 3 && Spf.DcErr == "discord not running", "no Discord here: the link reports it, nothing throws");
            Spf.Discord = false; Spf.Srv = false; SpfEngine.Sync();
        }

        Console.WriteLine("== login items ==");
        {
            Lgi.Dry = true;
            try { File.Delete(Lgi.File); } catch { }
            Lgi.Items.Clear();
            string exe = Path.Combine(Path.GetTempPath(), "yuri-lgi-test.exe"); File.WriteAllText(exe, "x");
            string doc = Path.Combine(Path.GetTempPath(), "yuri-lgi-test.txt"); File.WriteAllText(doc, "x");
            Check(Lgi.AddItem(exe) == 1 && Lgi.Items.Count == 1 && Lgi.Items[0].Exe == "yuri-lgi-test.exe" && Lgi.Items[0].Name == "yuri-lgi-test", "an exe is listed under its own name");
            Check(Lgi.AddItem(exe) == 0 && Lgi.Msg.StartsWith("ALREADY LISTED"), "the same path twice is refused");
            Check(Lgi.AddItem(doc) == 1 && Lgi.Items.Count == 2, "a document is listed");
            Check(File.ReadAllText(Lgi.File).Contains("\"open\":\"1\",\"close\":\"1\""), "login_items.txt in the .ahk's line format");
            Lgi.Toggle(0, "open");
            Check(!Lgi.Items[0].Open && Lgi.Items[0].Close, "OPENS off, CLOSES kept");
            Lgi.Load();
            Check(Lgi.Items.Count == 2 && !Lgi.Items[0].Open && Lgi.Items[1].Open, "the list reloads with its switches");
            Lgi.Observe(true); Lgi.Observe(false);
            Check(Lgi.Rbx == 1, "the first observation seeds the state, the next one waits for its hold");
            Lgi.OpenAll();
            Check(Lgi.Msg.StartsWith("OPENED 1 APP"), "OPEN ALL starts what is set to open: " + Lgi.Msg);
            Lgi.CloseAll();
            Check(Lgi.Msg == "NOTHING TO CLOSE", "nothing of ours runs: " + Lgi.Msg);
            Lgi.Remove(1);
            Check(Lgi.Items.Count == 1 && Lgi.Msg.StartsWith("REMOVED"), "removed");
            Lgi.Master(); Check(!Lgi.On, "master off"); Lgi.Master(); Check(Lgi.On, "master on");
            Lgi.Items.Clear(); Lgi.Save();
        }

        Console.WriteLine("== device optimizations ==");
        {
            var g = Dop.Groups[0];
            string ps = Dop.Build(g, "apply");
            Check(ps.Contains("$snap += Cap-Reg 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\mouclass\\Parameters' 'MouseDataQueueSize'") && !ps.Contains("Cap-Reg 'HKCU:"), "the elevated script captures the HKLM values and leaves HKCU to the hub");
            Check(ps.Contains("Put-Reg 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\mouhid\\Parameters' 'UseOnlyMice' '1' 'DWord'") && ps.Contains("if ($fail.Count -gt 0) { exit 4 }"), "apply then verify");
            string rv = Dop.Build(g, "revert");
            Check(rv.Contains("Undo-Reg $r") && rv.Contains("Remove-Item -LiteralPath $snapPath -Force") && !rv.Contains("Put-Reg 'HKLM"), "revert walks the snapshot backwards and removes it");
            string net = Dop.Build(Dop.Groups[2], "apply");
            Check(net.Contains("Set-NetAdapterAdvancedProperty -Name $a.Name -DisplayName 'Interrupt Moderation' -DisplayValue 'Disabled' -NoRestart") && net.Contains("foreach ($i in (Ifaces)) { New-ItemProperty -LiteralPath $i -Name 'TcpAckFrequency' -Value 1"), "NIC and TCP items");
            string pw = Dop.Build(Dop.Groups[3], "apply");
            Check(pw.Contains("powercfg /setactive '8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c'") && pw.Contains("$snap += Cap-Usb"), "power plan and USB suspend");
            string tm = Dop.Build(Dop.Groups[1], "apply");
            Check(tm.Contains("bcdedit /set 'disabledynamictick' 'yes'") && tm.Contains("Chk-Bcd 'useplatformclock' 'no'"), "bcdedit items");
            string chk = Dop.CheckBuild(new[] { "input", "timers" });
            Check(chk.Contains("$out += ('timers' + \"`t?\")") && chk.Contains("$out += ('input' + \"`t\" + $fail.Count)"), "the drift check counts what can be read and marks bcd unreadable");
            Check(Dop.Groups.Length == 5 && Dop.Groups.Sum(x => x.It.Length) == 46, "five groups, 46 settings - the .ahk table exactly");
            Check(Dop.Status().Length > 10 && Dop.OnCount() == 0, "status with nothing applied: " + Dop.Status());
            Check(!Dop.Run("input", "apply") && (Dop.Msg == "DEVICE OPTIMIZATIONS IS A WINDOWS FEATURE" || Dop.Msg.StartsWith("COULD NOT")), "off Windows the run declines: " + Dop.Msg);
        }

        Console.WriteLine("== memory engine (pure parts) ==");
        {
            Check(FFlags.Hash("DebugGraphicsPreferVulkan") == 0xfbce6ad594a64a48UL && FFlags.Hash("TaskSchedulerTargetFps") == 0x1ea5fd75921c4a3bUL, "FNV-1a matches the .ahk's FFMFlagHash");
            Check(FFlags.Coerce("true") == 1 && FFlags.Coerce("False") == 0 && FFlags.Coerce(" 9999 ") == 9999 && FFlags.Coerce("2147483648") is null && FFlags.Coerce("abc") is null && FFlags.Coerce("1.6") == 2, "Coerce: bools, ints, the 32-bit fence, rounding");
            Check(!FfmEngine.Attached && FfmEngine.ProcList(out var cr).Count == 0 && cr.Count == 0, "no client here: nothing attached");
            Check(FfmEngine.ApplyLive() == 0 && Ffm.Msg.Contains("WINDOWS") , "off Windows INJECT explains itself: " + Ffm.Msg);
        }

        Console.WriteLine("== cursor ==");
        {
            Check(Cur.Key("C:\\x\\ArrowCursor.png") == Cur.Key("c:\\X\\arrowcursor.png") && Cur.Key("a").Length == 8, "the backup key is case-blind, eight hex digits");
            Cur.Boot();
            string ctmp = Path.Combine(Path.GetTempPath(), "yuri-cur-test.png");
            Yuri.Gfx.Img.Asset("avatar.jpg")!.Save(ctmp);
            Cur.SetImage(2, ctmp);
            Check(Cur.Slot[1].Has && File.Exists(Cur.SlotImg(2)) && File.Exists(Cur.SlotSrc(2)) && Cur.Msg.StartsWith("SHIFT LOCK READY  256x256 -> 64x64"), "a picture is fitted into the 64x64 working copy: " + Cur.Msg);
            using (var b = new Avalonia.Media.Imaging.Bitmap(Cur.SlotImg(2))) Check(b.PixelSize.Width == 64 && b.PixelSize.Height == 64, "the working copy is 64x64");
            Check(Cur.Apply() == 0 && (Cur.Msg == "NO MATCHING ROBLOX CURSOR FILES FOUND"), "no client here: APPLY finds nothing to write: " + Cur.Msg);
            Check(Cur.ClearSlot(2) && !Cur.Slot[1].Has && !File.Exists(Cur.SlotImg(2)), "the slot clears and its files go");
            Check(Cur.DimText(2) == "not set", "dimension text");
        }

        Console.WriteLine("== saved places ==");
        {
            Yuri.Platform.GameInfo.NoNetwork = true;
            SpfSaved.Saved.Clear(); SpfSaved.SavedP.Clear(); SpfSaved.SavTab = 1;
            Check(SpfSaved.SavedAdd("https://www.roblox.com/games/2753915549/Blox-Fruits") && SpfSaved.Saved.Count == 1 && SpfSaved.Saved[0].Id == "2753915549", "a game link becomes a saved place");
            Check(!SpfSaved.SavedAdd("2753915549") && SpfSaved.AddErr == "already saved", "a duplicate is refused: " + SpfSaved.AddErr);
            Check(!SpfSaved.SavedAdd("hello") && SpfSaved.AddErr == "no place id in that", "no id, no entry");
            Check(Ini.Read(Paths.IniFile, "special", "sv1", "") == "2753915549|", "sv1 written in the .ahk's form");
            SpfSaved.SavTab = 2;
            Check(SpfSaved.PrivAdd("920587237", "https://www.roblox.com/games/920587237?privateServerLinkCode=abcDEF123") && SpfSaved.SavedP.Count == 1 && SpfSaved.SavedP[0].Code == "abcDEF123", "a private server link with its code");
            Check(!SpfSaved.PrivAdd("920587237", "https://www.roblox.com/games/920587237") && SpfSaved.AddErr == "that link has no private server code", "a link without a code is refused");
            Check(SpfSaved.PrivAdd("4924922222", "https://www.roblox.com/share?code=zzz999&type=Server") && SpfSaved.SavedP[1].Url != "" && SpfSaved.PrivUrl(SpfSaved.SavedP[1]).Contains("share?code=zzz999"), "a share link is kept whole");
            SpfSaved.SavSelP = 1; SpfSaved.RenIdx = 1; SpfSaved.RenTxt = "main"; SpfSaved.RenOpen = true; SpfSaved.RenConfirm();
            Check(SpfSaved.SavedP[0].Nm == "main" && Ini.Read(Paths.IniFile, "special", "pv1", "").StartsWith("920587237|abcDEF123|main|"), "RENAME writes pv1");
            SpfSaved.Use(1);
            Check(Spf.MmPriv.Contains("privateServerLinkCode=abcDEF123"), "USE loads the private link field");
            SpfSaved.Del(1);
            Check(SpfSaved.SavedP.Count == 1 && SpfSaved.SavedP[0].Code == "zzz999", "REMOVE drops the entry");
            SpfSaved.Load();
            Check(SpfSaved.Saved.Count == 1 && SpfSaved.SavedP.Count == 1, "the lists reload from the ini");
            SpfSaved.Saved.Clear(); SpfSaved.SavedP.Clear(); SpfSaved.SavTab = 1;
            for (int i = 1; i <= 8; i++) { Spf.Save("sv" + i, ""); Spf.Save("pv" + i, ""); }
        }

        Console.WriteLine("== accounts ==");
        {
            SpfAcct.NoNetwork = true;
            SpfAcct.Acct.Clear();
            string longCk = "_|WARNING:-DO-NOT-SHARE-THIS.--Sharing-this-will-allow-someone-to-log-in-as-you.|" + new string('A', 120);
            Check(SpfAcct.AddFromClip(".ROBLOSECURITY=" + longCk + "; path=/") && SpfAcct.Acct.Count == 1 && SpfAcct.Acct[0].Ck == longCk, "the cookie is pulled out of a pasted header");
            Check(!SpfAcct.AddFromClip("hello there") && Spf.Msg.Contains("DOES NOT LOOK LIKE"), "junk is refused: " + Spf.Msg);
            Check(!SpfAcct.AddFromClip(".ROBLOSECURITY=" + longCk) && Spf.Msg.Contains("ALREADY SAVED"), "a duplicate cookie is refused");
            SpfAcct.Store();
            if (Yuri.Platform.Os.IsWin) { string blob = System.IO.File.ReadAllText(SpfAcct.File); Check(!blob.Contains(longCk) && blob.Contains("zeal".Replace("zeal","")) == false && blob.Split('|').Length >= 3, "the stored file does not contain the raw cookie"); }
            SpfAcct.Load();
            Check(SpfAcct.Acct.Count == 1 && SpfAcct.Acct[0].Ck == longCk, "the sealed cookie round-trips through the file");
            Check(SpfAcct.ParseTarget("https://www.roblox.com/games/2753915549/Blox-Fruits", out var p1, out _, out _, out _) && p1 == "2753915549", "a game link is a place target");
            Check(SpfAcct.ParseTarget("2753915549 6d3f-1a2b-c4e5-aaaa-bbbbccccdddd", out var p2, out var j2, out _, out _) && p2 == "2753915549" && j2.StartsWith("6d3f"), "place id + job id");
            Check(SpfAcct.ParseTarget("https://www.roblox.com/games/920587237?privateServerLinkCode=xyz", out _, out _, out var c3, out _) && c3 == "xyz", "a private server code");
            Check(!SpfAcct.ParseTarget("just words", out _, out _, out _, out _), "no id, no target");
            Check(SpfAcct.TargetDesc("") == "opens roblox on its home screen, signed in", "empty target description");
            Check(SpfAcct.TargetDesc("2753915549").Contains("any public server"), "place-only description");
            if (!Yuri.Platform.Os.IsWin) { SpfAcct.Launch(1); Check(Spf.Msg.Contains("WINDOWS FEATURE"), "off Windows the launch explains itself"); }
            SpfAcct.Acct.Clear(); try { System.IO.File.Delete(SpfAcct.File); } catch { }
        }

        Console.WriteLine("== content mods ==");
        {
            SpfMods.NoNetwork = true;
            string inst = Path.Combine(Path.GetTempPath(), "yuri-fake-install"); string snd = Path.Combine(inst, "content", "sounds"); string modsub = Path.Combine(Path.GetTempPath(), "yuri-fake-mods", "content", "sounds");
            try { Directory.Delete(inst, true); } catch { } try { Directory.Delete(Path.Combine(Path.GetTempPath(), "yuri-fake-mods"), true); } catch { }
            Directory.CreateDirectory(snd);
            foreach (var fnm in new[] { "action_footsteps_plastic.mp3", "action_jump.mp3", "action_get_up.mp3", "action_falling.mp3", "action_jump_land.mp3", "action_swim.mp3", "impact_water.mp3" }) File.WriteAllText(Path.Combine(snd, fnm), "ORIG-" + fnm);
            SpfMods.TargetsOverride = sub => new List<(string, bool)> { (Path.Combine(inst, sub), false), (Path.Combine(Path.GetTempPath(), "yuri-fake-mods", sub), true) };
            string mdir = Path.Combine(Paths.Mods, "oldsnd"); Directory.CreateDirectory(mdir);
            foreach (var src in new[] { "OldWalk.mp3", "OldJump.mp3", "OldGetUp.mp3", "Empty.mp3" }) File.WriteAllText(Path.Combine(mdir, src), "MOD-" + src);
            Check(SpfMods.Have("oldsnd") && !SpfMods.Have("avbg"), "Have reads YURI\\mods");
            int nM = SpfMods.Apply("oldsnd");
            Check(nM == 14 && File.Exists(Path.Combine(snd, "action_jump.mp3.yuri-orig")) && File.ReadAllText(Path.Combine(snd, "action_jump.mp3")) == "MOD-OldJump.mp3" && File.Exists(Path.Combine(modsub, "action_jump.mp3")), "APPLY writes seven files into the install and the strap's Modifications, originals backed up: " + nM);
            Check(SpfMods.HintShort("oldsnd") == "" , "hint is empty while the row is off");
            File.WriteAllText(Path.Combine(snd, "action_jump.mp3"), "NEWBUILD"); File.Delete(Path.Combine(snd, "action_jump.mp3.yuri-orig"));
            Spf.Snd = true; SpfMods.AutoTick();
            Check(File.ReadAllText(Path.Combine(snd, "action_jump.mp3")) == "MOD-OldJump.mp3" && File.ReadAllText(Path.Combine(snd, "action_jump.mp3.yuri-orig")) == "NEWBUILD", "a fresh build's file is re-applied and re-backed-up");
            Check(SpfMods.HintShort("oldsnd").StartsWith("14 files in place"), "hint counts the files: " + SpfMods.HintShort("oldsnd"));
            Spf.Snd = false;
            int r = SpfMods.Revert("oldsnd");
            Check(r == 14 && File.ReadAllText(Path.Combine(snd, "action_footsteps_plastic.mp3")) == "ORIG-action_footsteps_plastic.mp3" && !File.Exists(Path.Combine(modsub, "action_jump.mp3")), "REVERT puts the originals back and removes the strap copies: " + r);
            SpfMods.Fetch("avbg");
            Check(Spf.Msg.Contains("COULD NOT FETCH"), "no network: the fetch says so");
            SpfMods.TargetsOverride = null;
            try { Directory.Delete(inst, true); Directory.Delete(Path.Combine(Path.GetTempPath(), "yuri-fake-mods"), true); Directory.Delete(mdir, true); } catch { }
        }

        Console.WriteLine("== windows rows ==");
        {
            Check(SpfWin.MemFmt(512) == "512 MB" && SpfWin.MemFmt(1024) == "1 GB" && SpfWin.MemFmt(1536) == "1.5 GB", "memory sizes format as the .ahk's");
            Spf.MemInt = 10; Spf.MemThr = 0;
            SpfWin.MemSubToggle(1); Check(Spf.MemInt == 30 && Spf.Msg == "TRIM EVERY 30 S", "the interval chip cycles: " + Spf.Msg);
            SpfWin.MemSubToggle(2); Check(Spf.MemThr == 1024 && Spf.Msg == "TRIM ONLY A CLIENT OVER 1 GB", "the size chip cycles: " + Spf.Msg);
            Spf.MemInt = 300; SpfWin.MemSubToggle(1); Check(Spf.MemInt == 5, "the cycle wraps");
            Spf.MemInt = 10; Spf.MemThr = 0;
            Check(SpfFps.Levers.Length == 6 && SpfFps.Report() == "" && SpfFps.Missing().Count == 6, "FPS BOOST idle: six levers, nothing taken");
            Check(SpfFps.PriName(0x80) == "high" && SpfFps.PriName(0x8000) == "above normal", "priority names");
            if (!Yuri.Platform.Os.IsWin) { Check(SpfWin.MultiHold() == 0 && SpfFps.Apply(true) == 0, "off Windows the singleton and the levers do nothing"); }
        }

        Console.WriteLine("== client settings ==");
        {
            string inst = Path.Combine(Path.GetTempPath(), "yuri-rset-test"); try { Directory.Delete(inst, true); } catch { } Directory.CreateDirectory(inst);
            string xml = Path.Combine(inst, "GlobalBasicSettings_13.xml");
            string xml0 = "<roblox><Item class=\"UserGameSettings\"><Properties>\n\t\t<int name=\"FramerateCap\">60</int>\n\t\t<token name=\"SavedQualityLevel\">7</token>\n\t\t<token name=\"GraphicsQualityLevel\">7</token>\n\t\t<float name=\"MasterVolume\">0.5</float>\n\t\t<bool name=\"Fullscreen\">true</bool>\n\t</Properties></Item></roblox>";
            File.WriteAllText(xml, xml0);
            string cs = Path.Combine(inst, "ClientSettings"); Directory.CreateDirectory(cs);
            File.WriteAllText(Path.Combine(cs, "ClientAppSettings.json"), "{\n    \"FFlagUserOwn\": \"true\",\n    \"DFIntTaskSchedulerTargetFps\": \"144\"\n}");
            RSet.NoProc = true; RSet.XmlOverride = () => new List<string> { xml }; RSet.PathsOverride = () => new List<string> { cs };
            try { File.Delete(Path.Combine(RSet.Dir, "appsettings_prev.json")); File.Delete(RSet.XmlBak); File.Delete(Path.Combine(RSet.Dir, "xml_owned.json")); } catch { }
            RSet.Boot();
            RSet.RowsToDefault(); RSet.Extra.Clear(); RSet.SrcOn = false; RSet.SrcSel = 1; RSet.FxBump(); RSet.On = false;
            Check(RSet.Opts.Count == 32 && RSet.By("fps")!.V.Count == 38 && RSet.By("fps")!.F!.Count == 38, "the 32 rows, FRAMERATE CAP's 38 steps");
            Check(RSet.By("mesh")!.F![5]["DFIntCSGLevelOfDetailSwitchingDistanceL12"] == "6000", "MESH ULTRA ladder");
            Check(RSet.By("render")!.Alp == 1 && RSet.By("light")!.Al == 0 && RSet.By("dpi")!.Al == 1, "allowlist marks: RENDERING partly, LIGHTING off, DPI on");
            Check(RSet.Chosen() == 0 && RSet.Flags().Count == 0, "all auto: nothing chosen");
            RSet.ApplyPreset(3);
            Check(RSet.PresetActive() == 3 && RSet.Idx(RSet.By("fps")!) == 38 && RSet.Idx(RSet.By("gmode")!) == 2, "LOW preset: fps MAX, gmode PERFORMANCE");
            var fl = RSet.Flags();
            Check(fl["DFIntTaskSchedulerTargetFps"] == "9999" && fl["FFlagDisablePostFx"] == "True" && fl["DFIntTextureQualityOverride"] == "0", "LOW's flag list");
            Check(RSet.RowMoot(RSet.By("shmap")!) && !fl.ContainsKey("FIntRenderShadowmapResolution"), "SHADOWS OFF makes SHADOW DETAIL moot - its flag is not written");
            RSet.SetPick(RSet.By("frm")!, 5);
            Check(RSet.RowMoot(RSet.By("gfx")!) && RSet.RowOverride(RSet.By("maxq")!).StartsWith("FRM QUALITY"), "FRM override pins gfx and maxq");
            RSet.SetPick(RSet.By("frm")!, 1);
            RSet.SetPick(RSet.By("maxq")!, 2);
            Check(RSet.Idx(RSet.By("gfx")!) == 12, "MAX QUALITY ON pulls GRAPHICS QUALITY to 10");
            RSet.SetPick(RSet.By("gfx")!, 5);
            Check(RSet.Idx(RSet.By("maxq")!) == 3, "the slider leaving its top turns MAX QUALITY OFF");
            RSet.Extra["FFlagDisablePostFx"] = "False"; RSet.FxBump();
            Check(RSet.RowLocked(RSet.By("postfx")!) && RSet.Flags()["FFlagDisablePostFx"] == "False", "an extra naming a row's flag locks the row and wins");
            RSet.SrcSelect(1);
            Check(!RSet.RowLocked(RSet.By("postfx")!) && RSet.Flags()["FFlagDisablePostFx"] == "True", "CLIENT SYSTEMS only: the rows alone");
            RSet.SrcSelect(2);
            Check(RSet.Flags().Count == 1, "EDIT FAST FLAGS only: the extras alone");
            RSet.SrcOn = false; RSet.SrcSel = 1; RSet.FxBump();
            int nTake = RSet.TakeRow(RSet.By("msaa")!);
            Check(nTake == 1 && RSet.Extra["FIntDebugForceMSAASamples"] == "1" && RSet.Idx(RSet.By("msaa")!) == 1, "TakeRow moves the pair into the raw layer");
            RSet.Apply();
            var cur = RSet.ParseJson(File.ReadAllText(Path.Combine(cs, "ClientAppSettings.json")));
            Check(cur["FFlagUserOwn"] == "true" && cur["DFIntTaskSchedulerTargetFps"] == "9999" && cur["FIntDebugForceMSAASamples"] == "1", "APPLY merged the flags over the user's file: " + RSet.Msg);
            var prev = RSet.ParseJson(File.ReadAllText(Path.Combine(RSet.Dir, "appsettings_prev.json")));
            Check(prev["DFIntTaskSchedulerTargetFps"] == "144" && prev["FFlagDisablePostFx"] == "\f", "the journal holds what each key held before");
            Check(RSet.XmlRead(xml, "FramerateCap") == "9999" && RSet.XmlRead(xml, "SavedQualityLevel") == "3" && RSet.XmlRead(xml, "GraphicsQualityLevel") == "3" && RSet.XmlRead(xml, "MasterVolume") == "0.5", "xml: fps and quality set, the twin followed, volume kept");
            Check(RSet.XmlRead(xml, "GraphicsOptimizationMode") == "0" && File.Exists(RSet.XmlBak) && RSet.On, "a missing property was inserted, the backup held, the module is on");
            Check((File.GetAttributes(xml) & FileAttributes.ReadOnly) != 0, "the xml is locked after APPLY");
            Check(RSet.Msg.StartsWith("APPLIED  ") && RSet.Msg.Contains("1 CLIENT(S)"), "the APPLY message: " + RSet.Msg);
            RSet.Revert();
            cur = RSet.ParseJson(File.ReadAllText(Path.Combine(cs, "ClientAppSettings.json")));
            Check(cur.Count == 1 && cur["FFlagUserOwn"] == "true", "REVERT took only known keys back out - the user's own flag survives, the pre-existing fps cap too since it is a known key");
            Check(!File.Exists(Path.Combine(RSet.Dir, "appsettings_prev.json")) && !File.Exists(RSet.XmlBak) && !RSet.On, "the journal and the backup are gone; the module is off");
            Check(RSet.XmlRead(xml, "FramerateCap") == "" && RSet.XmlRead(xml, "SavedQualityLevel") == "" && RSet.XmlRead(xml, "MasterVolume") == "0.5" && RSet.XmlRead(xml, "Fullscreen") == "true", "xml: owned values cut, the rest untouched");
            Check(RSet.Chosen() == 0 && RSet.Extra.Count == 0 && RSet.Msg.StartsWith("REVERTED"), "REVERT put the hub back to default: " + RSet.Msg);
            // the EDIT FAST FLAGS page
            RSetFx.Register();
            RSetFx.Open();
            Check(RSetFx.Fx && RSetFx.List().Count == 0, "the page opens empty when every row is auto");
            RSet.ApplyPreset(1);
            var rows = RSetFx.List();
            Check(rows.Count == RSet.PresetFlags().Count && rows.All(r => !r.Ext) && rows[0].N.CompareTo(rows[1].N) < 0, "HIGH's pairs listed from their rows, sorted");
            RSetFx.FxName = "FFlagDebugSkyGray"; RSetFx.FxVal = "True";
            Check(RSetFx.Add() && RSet.Extra["FFlagDebugSkyGray"] == "True" && RSetFx.FxName == "" && RSet.Msg == "ADDED", "ADD takes the fields into the raw layer");
            RSetFx.FxName = "bad name"; RSetFx.FxVal = "1";
            Check(!RSetFx.Add() && RSet.Msg == "THAT IS NOT A FLAG NAME", "a bad name is refused");
            RSetFx.FxName = "FIntDebugForceMSAASamples"; RSetFx.FxVal = "8";
            Check(RSetFx.Add() && RSet.Idx(RSet.By("msaa")!) == 1 && RSet.Extra["FIntDebugForceMSAASamples"] == "8" && RSet.Msg.StartsWith("ANTI-ALIASING IS NOW YOURS"), "adding a row's flag takes the row over");
            RSetFx.EditRow("FFlagDisablePostFx");
            Check(FfmField.Edit == "rfe" && RSetFx.FxSel == "FFlagDisablePostFx" && RSet.Idx(RSet.By("postfx")!) == 1 && FfmField.Buf == "False", "editing a preset pair takes its row and opens the value");
            FfmField.Char("X"); FfmField.End(true);
            Check(RSet.Extra["FFlagDisablePostFx"] == "FalseX" && FfmField.Edit == "", "the edit commits into the raw layer");
            RSetFx.Del("FFlagDebugSkyGray");
            Check(!RSet.Extra.ContainsKey("FFlagDebugSkyGray") && RSetFx.FxGone.ContainsKey("FFlagDebugSkyGray") && RSetFx.List().Any(r => r.N == "FFlagDebugSkyGray" && r.Gone != 0), "a removed extra collapses out of the list");
            RSetFx.DbOpen(true); RSetFx.DbQ = "Shadow";
            var dl = RSetFx.DbList();
            Check(RSetFx.DbAll().Count >= 24 && dl.Count == 2 && dl.All(e => e.N.Contains("Shadow")), "the database filters by name: " + dl.Count);
            RSetFx.DbPick(dl[0].N);
            Check(!RSetFx.Db && RSetFx.FxName == dl[0].N && FfmField.Edit == "rfv", "a database pick lands the name and opens the value field");
            FfmField.End(false); RSetFx.Close();
            RSet.Revert();
            // the file replacements on a fake install
            RSetLogo.Register();
            string fake = Path.Combine(inst, "textures"); Directory.CreateDirectory(Path.Combine(fake, "ui"));
            string logoA = Path.Combine(fake, "ui", "roblox_logo.png"), logoB = Path.Combine(fake, "logo_big.png"), mirror = Path.Combine(inst, "mods", "textures", "ui", "roblox_logo.png");
            File.WriteAllBytes(logoA, new byte[] { 1, 2, 3 }); File.WriteAllBytes(logoB, new byte[] { 4, 5, 6 });
            RSetLogo.TargetsOverride = (kind, sub) => kind != "1" ? new List<RSetLogo.Tgt>() : new List<RSetLogo.Tgt> { new(logoA, Path.GetDirectoryName(logoA)!, "roblox_logo.png", "ui\\roblox_logo.png", false), new(logoB, fake, "logo_big.png", "logo_big.png", false), new(mirror, Path.GetDirectoryName(mirror)!, "roblox_logo.png", "ui\\roblox_logo.png", true) };
            var shortcuts = new Dictionary<string, string> { [Path.Combine(inst, "Roblox Player.lnk")] = "C:\\old\\RobloxPlayerBeta.exe,0" };
            RSetLogo.LnkOverride = kind => kind == 1 ? shortcuts.Keys.Select(k => new RSetLogo.Lnk(k, Path.GetFileName(k))).ToList() : new List<RSetLogo.Lnk>();
            RSetLogo.LnkIconGet = pth => shortcuts.TryGetValue(pth, out var v) ? v : null;
            RSetLogo.LnkIconSet = (pth, ico) => { shortcuts[pth] = ico; return true; };
            foreach (var k in shortcuts.Keys) File.WriteAllText(k, "lnk");
            string src = Path.Combine(inst, "mylogo.png"); Yuri.Gfx.Img.Asset("avatar.jpg")!.Save(src);
            RSetLogo.SetLogo(1, src);
            Check(RSet.Logo == src && RSet.Idx(RSet.By("logo")!) == 2 && RSet.Msg.StartsWith("PLAYER LOGO READY") && RSet.Msg.Contains("4 TARGET(S)"), "BROWSE stages the image and counts its targets: " + RSet.Msg);
            RSetLogo.LogoApplyOne(1);
            Check(File.ReadAllBytes(logoA).Length > 100 && File.ReadAllBytes(mirror).Length > 100 && File.Exists(Path.Combine(RSet.Dir, "logo", Yuri.Modules.Cursor.Cur.Key(logoA) + "_roblox_logo.png")), "APPLY writes every target and backs the originals up");
            Check(RSetLogo.BakLog.Count == 2 && File.Exists(Path.Combine(RSet.Dir, "backups.dat")), "the backup log lists the two originals (the mirror had none)");
            Check(shortcuts.Values.First().StartsWith(RSetLogo.IcoPath(1)) && RSetLogo.IcoOrig.Count == 1 && File.Exists(RSetLogo.IcoPath(1)), "the shortcut points at the written .ico, its old icon recorded");
            var icoBytes = File.ReadAllBytes(RSetLogo.IcoPath(1));
            Check(icoBytes[2] == 1 && icoBytes[4] == 1 && icoBytes[12] == 32 && BitConverter.ToInt32(icoBytes, 18) == 22 && icoBytes[22] == 0x89 && icoBytes[23] == (byte)'P', "the .ico is a one-entry PNG icon");
            Check(RSet.Msg.StartsWith("PLAYER LOGO APPLIED  -  3 FILE(S), 1 SHORTCUT(S)"), "the message: " + RSet.Msg);
            RSetLogo.LogoRestore();
            Check(File.ReadAllBytes(logoA).SequenceEqual(new byte[] { 1, 2, 3 }) && File.ReadAllBytes(logoB).SequenceEqual(new byte[] { 4, 5, 6 }) && RSetLogo.BakLog.Count == 0, "RESTORE puts the originals back");
            Check(shortcuts.Values.First() == "C:\\old\\RobloxPlayerBeta.exe,0" && RSetLogo.IcoOrig.Count == 0 && RSet.Logo == "" && RSet.Idx(RSet.By("logo")!) == 1, "the shortcut goes back to what it had; the row is DEFAULT again");
            // the font
            string f1 = Path.Combine(inst, "fonts", "SourceSans.ttf"); Directory.CreateDirectory(Path.GetDirectoryName(f1)!); File.WriteAllBytes(f1, new byte[] { 9, 9 });
            string mine = Path.Combine(inst, "mine.ttf"); File.WriteAllBytes(mine, new byte[] { 7, 7, 7, 7 });
            RSetLogo.FontTargetsOverride = () => new List<RSetLogo.Tgt> { new(f1, Path.GetDirectoryName(f1)!, "SourceSans.ttf", "", false) };
            RSetLogo.SetFont(mine);
            Check(RSet.Font == mine && RSet.Idx(RSet.By("font")!) == 2, "the font is staged");
            Check(RSetLogo.FontApply() == 1 && File.ReadAllBytes(f1).Length == 4 && RSetLogo.BakLog.Count == 1, "the font is written over every target with a backup");
            Directory.Delete(Path.GetDirectoryName(f1)!, true);                   // the install went away: the sweep drops the orphan backup
            Check(RSetLogo.FontRevert() == 0 && RSetLogo.BakLog.Count == 0, "a backup whose install is gone is swept, not restored into nowhere");
            RSetLogo.TargetsOverride = null; RSetLogo.LnkOverride = null; RSetLogo.LnkIconGet = null; RSetLogo.LnkIconSet = null; RSetLogo.FontTargetsOverride = null;
            RSet.Font = ""; RSet.Pick["font"] = 1;
            Check(RSetLogo.FpsLive() == 0 && RSetLogo.FpsLiveRevert() == 0, "no fps target, no client: the live cap does nothing");
            RSet.PathsOverride = null; RSet.XmlOverride = null; RSet.NoProc = false;
        }

        Console.WriteLine("== script hub ==");
        {
            Scr.Register(); Scr.List.Clear(); Scr.Tabs.Clear(); Scr.Tabs.Add(new ScrTabState { Lines = Scr.Lines }); Scr.Cur = 1;
            try { Directory.Delete(Scr.Dir, true); } catch { }
            Scr.Clear();
            Scr.Char("a"); Scr.FocusSet(1); Scr.Char("x := 1"); 
            Check(Scr.Text() == "x := 1" && Scr.Cc == 6, "typing lands in the line once the editor has focus");
            Scr.Paste("\nif x\n    y := \"two\" ; note");
            Check(Scr.Lines.Count == 3 && Scr.Cl == 3 && Scr.Lines[2] == "    y := \"two\" ; note", "a multi-line paste splits into lines and lands the caret at the end");
            Scr.Retok();
            var t3 = Scr.Toks[2];
            Check(t3.Any(t => t.T == 3 && t.S == "\"two\"") && t3.Any(t => t.T == 4 && t.S.StartsWith("; note")) && Scr.Toks[1].Any(t => t.T == 1 && t.S == "if"), "the tokenizer sees the string, the comment and the keyword");
            Scr.Sl = 1; Scr.Sc = 0; Scr.SelOn = true; Scr.Cl = 2; Scr.Cc = 2;
            Check(Scr.SelText() == "x := 1\nif" && Scr.SelLen() == 9, "the selection reads across lines");
            Check(Scr.DelSel() && Scr.Text() == " x\n    y := \"two\" ; note" && Scr.Cl == 1 && Scr.Cc == 0, "deleting the selection joins the lines");
            Scr.Cl = 1; Scr.Cc = 2; Scr.WordSelect();
            Check(Scr.SelOn && Scr.Sc == 1 && Scr.Cc == 2, "a double click selects the word");
            Check(Scr.GuessVer("#NoEnv\nSendMode Input\nSleep, 100") == "1" && Scr.GuessVer("x := Map()\nfor k, v in x\n    y.Push(k)") == "2" && Scr.ReqVer("#Requires AutoHotkey v2.0") == "2" && Scr.GuessVer("abc") == "", "the version guesser");
            Check(Scr.ErrPretty("C:\\x.ahk (7) : ==> Missing \"}\"\n     Specifically: at line 7", "2") == "v2 line 7 - Missing \"}\"  [at line 7]", "the error line is pretty: " + Scr.ErrPretty("C:\\x.ahk (7) : ==> Missing \"}\"\n     Specifically: at line 7", "2"));
            Scr.AhkExeOverride = major => major == "2" ? "fake-ahk2.exe" : "";
            Scr.ValidateOverride = (exe, file) => File.ReadAllText(file).Contains("bad") ? (2, "x.ahk (1) : ==> This line does not contain a recognized action.") : (0, "");
            Scr.SetText("MsgBox(\"hi\")"); Scr.Name = "HELLO"; Scr.Desc = "";
            Check(Scr.Check() && Scr.OutOk == 1 && Scr.OutVer == "2" && Scr.ChkVer == "2", "CHECK passes a clean v2 script: " + Scr.OutMsg);
            Scr.Place();
            Check(Scr.List.Count == 1 && Scr.List[0].Name == "HELLO" && Scr.List[0].Desc == "placed from the script hub" && File.Exists(Scr.List[0].File) && Scr.EditIdx == 0 && Scr.Focus == 0, "PLACE writes the file and lists the card");
            Check(Ini.ReadInt(Paths.IniFile, "scripts", "count", 0) == 1 && Ini.Read(Paths.IniFile, "script1", "name", "") == "HELLO", "the placed list is in zeal.ini");
            Scr.SetText("bad line here");
            Scr.Place();
            Check(Scr.List.Count == 1 && Scr.WarnMsg.StartsWith("script has errors") && Scr.OutOk == 0 && Scr.OutMsg.StartsWith("v2 line 1 - "), "PLACE refuses a script that fails to validate: " + Scr.OutMsg);
            Scr.Edit(1);
            Check(Scr.Tabs.Count == 2 && Scr.Cur == 2 && Scr.Text() == "MsgBox(\"hi\")" && Scr.EditIdx == 1 && Scr.Name == "HELLO", "EDIT opens the card in a new tab with the row marked");
            Scr.FocusSet(1); Scr.Char("; x"); Scr.Place();
            Check(Scr.List.Count == 1 && File.ReadAllText(Scr.List[0].File).EndsWith("; x") && Scr.EditIdx == 0, "PLACE while editing updates the card in place");
            Scr.TabSwitch(1);
            Check(Scr.Cur == 1 && Scr.Text() == "bad line here", "the first tab kept its own text");
            Scr.TabClose(2);
            Check(Scr.Tabs.Count == 1 && Scr.Cur == 1, "closing the other tab keeps this one");
            Scr.SetText("z := 3"); Scr.Name = "keep me"; Scr.File = "";
            Scr.Save(); Scr.LibRefresh();
            Check(Scr.LibList.Count == 1 && Scr.LibList[0].Disp == "keep me" && Scr.OutMsg.StartsWith("saved - "), "SAVE writes into the library: " + Scr.OutMsg);
            Scr.Clear(); Scr.LibOpenAt(1);
            Check(Scr.Text() == "z := 3" && Scr.Name == "KEEP ME" && Scr.File == Scr.LibList[0].Path, "OPEN loads it back with its name");
            Scr.CardEdit(1);
            Check(Scr.CeIdx == 1 && Scr.Text().StartsWith("MsgBox") && Scr.Focus == 1, "the inline edit loads the card's text");
            Scr.Char(" ; note"); Scr.CardSave();
            Check(Scr.CeIdx == 0 && File.ReadAllText(Scr.List[0].File).EndsWith("; note") && Scr.Text() == "z := 3", "SAVE writes the card and returns to the tab as it was");
            Scr.Delete(1, 0, 70, 3);
            Check(Scr.List.Count == 0 && Scr.DelName == "HELLO" && Ini.ReadInt(Paths.IniFile, "scripts", "count", 0) == 0, "DELETE removes the card and its file");
            Scr.AhkExeOverride = null; Scr.ValidateOverride = null; Scr.Clear(); Scr.Blur();
        }

        Console.WriteLine("== forsaken ==");
        {
            var K = typeof(Yuri.Modules.Forsaken.Keys);
            Check(Yuri.Modules.Forsaken.Keys.Label("XButton1") == "XB1" && Yuri.Modules.Forsaken.Keys.Label("Numpad5") == "N5" && Yuri.Modules.Forsaken.Keys.Label("") == "???", "the key labels");
            Check(Yuri.Modules.Forsaken.Keys.VkName(0x75) == "F6" && Yuri.Modules.Forsaken.Keys.VkOf("F6") == 0x75 && Yuri.Modules.Forsaken.Keys.VkOf("q") == 0x51 && Yuri.Modules.Forsaken.Keys.VkName(0x51) == "q", "vk <-> AutoHotkey names round-trip");
            Yuri.Modules.Forsaken.Keys.BindKey = ""; Yuri.Modules.Forsaken.Keys.PuzKey = ""; Yuri.Modules.Forsaken.Keys.MkKey = "";
            Yuri.Modules.Forsaken.Keys.Start(2);
            Check(Yuri.Modules.Forsaken.Keys.RebindOn && Yuri.Modules.Forsaken.Keys.Feed("F8") && Yuri.Modules.Forsaken.Keys.PuzKey == "F8" && !Yuri.Modules.Forsaken.Keys.RebindOn, "a rebind takes the next key");
            Check(Ini.Read(Paths.IniFile, "keys", "solve", "") == "F8", "and saves it under [keys] as the .ahk did");
            Yuri.Modules.Forsaken.Keys.Start(3);
            Check(Yuri.Modules.Forsaken.Keys.Feed("F8") && Yuri.Modules.Forsaken.Keys.MkKey == "" && !Yuri.Modules.Forsaken.Keys.RebindOn, "the markers key refuses the solve key");
            Yuri.Modules.Forsaken.Keys.Start(1);
            Check(Yuri.Modules.Forsaken.Keys.Feed("Escape") && Yuri.Modules.Forsaken.Keys.BindKey == "", "ESC cancels");
            Yuri.Modules.Forsaken.Puz.On = true; Yuri.Modules.Forsaken.Keys.Start(1); Yuri.Modules.Forsaken.Keys.Feed("F8");
            Check(Yuri.Modules.Forsaken.Puz.ClashUp && Yuri.Modules.Forsaken.Puz.ClashKey == "F8" && Yuri.Modules.Forsaken.Keys.BindKey == "", "binding the block to the solve key raises the conflict card");
            Yuri.Modules.Forsaken.Puz.ClashDismiss(); Yuri.Modules.Forsaken.Puz.ClashOut -= 1000; Yuri.Modules.Forsaken.Puz.ClashExpire();
            Check(!Yuri.Modules.Forsaken.Puz.ClashUp, "GOT IT takes it down");
            Yuri.Modules.Forsaken.Puz.On = false;
            Yuri.Modules.Forsaken.Puz.GridPick(3); Yuri.Modules.Forsaken.Puz.SlideSet(0.25);
            Check(Yuri.Modules.Forsaken.Puz.GridN == 10 && Math.Abs(Yuri.Modules.Forsaken.Puz.FrameMs - (42 + (5 - 42) * 0.25)) < 1e-9 && Ini.ReadInt(Paths.IniFile, "puzzle", "gridn", 0) == 3, "grid and speed persist");
            Yuri.Modules.Forsaken.Keys.Clear(2); Yuri.Modules.Forsaken.Puz.GridPick(2); Yuri.Modules.Forsaken.Puz.SlideSet(1.0);
        }

        Console.WriteLine("== puzzle ai ==");
        {
            var PS = typeof(Yuri.Modules.Forsaken.PuzSolver);
            Yuri.Modules.Forsaken.Puz.GridPick(1);                                     // 6x6
            Yuri.Modules.Forsaken.PuzSolver.vx = 0; Yuri.Modules.Forsaken.PuzSolver.vy = 0; Yuri.Modules.Forsaken.PuzSolver.vw = 1920; Yuri.Modules.Forsaken.PuzSolver.vh = 1080;
            double sx = Yuri.Modules.Forsaken.PuzSolver.SX;
            Check(Math.Abs(sx - 517.20 / 6) < 0.01 && Yuri.Modules.Forsaken.PuzSolver.PX(1) == (int)Math.Round(960 - (2.5 * sx + 0.5)), "the 1920x1080 reference lays the 6x6 grid out as the .ahk's table says: " + sx + " " + Yuri.Modules.Forsaken.PuzSolver.PX(1));
            // a board: four pairs on a 6x6 grid, drawn into a fake capture
            var dotsIn = new (int r, int c, uint col)[] { (1, 1, 0xFFE8574A), (1, 6, 0xFFE8574A), (6, 1, 0xFF4FA85C), (6, 6, 0xFF4FA85C), (2, 2, 0xFF3F7FC9), (2, 5, 0xFF3F7FC9), (4, 3, 0xFFFFD86B), (5, 4, 0xFFFFD86B) };
            Yuri.Modules.Forsaken.PuzSolver.Cap Fake(int x, int y, int w, int h)
            {
                var cap = new Yuri.Modules.Forsaken.PuzSolver.Cap { x = x, y = y, w = w, h = h, px = new byte[w * h * 4] };
                foreach (var (r, c, col) in dotsIn) { int cx = Yuri.Modules.Forsaken.PuzSolver.PX(c) - x, cy = Yuri.Modules.Forsaken.PuzSolver.PY(r) - y; for (int dy = -12; dy <= 12; dy++) for (int dx = -30; dx <= 30; dx++) { int px = cx + dx, py = cy + dy; if (px < 0 || py < 0 || px >= w || py >= h) continue; int i = (py * w + px) * 4; cap.px[i] = (byte)(col & 0xFF); cap.px[i + 1] = (byte)(col >> 8 & 0xFF); cap.px[i + 2] = (byte)(col >> 16 & 0xFF); } }
                return cap;
            }
            Yuri.Modules.Forsaken.PuzSolver.CaptureOverride = Fake;
            var dots = Yuri.Modules.Forsaken.PuzSolver.ScanGrid();
            Check(dots.Count == 8, "the scan finds the eight dots: " + dots.Count);
            var pairs = Yuri.Modules.Forsaken.PuzSolver.GroupPairs(dots)!;
            Check(pairs is not null && pairs.Count == 4 && pairs.All(pp => pp.a.col == pp.b.col), "they pair by colour into four");
            var paths = Yuri.Modules.Forsaken.PuzSolver.SolveBoard(pairs)!;
            Check(paths is not null && paths.Count == 4, "the search routes every pair");
            bool ok = true; var used = new HashSet<(int, int)>();
            for (int k = 1; k <= 4; k++)
            {
                var p = paths[k]; var pr = pairs[k - 1];
                if (p[0] != (pr.a.r, pr.a.c) || p[^1] != (pr.b.r, pr.b.c)) ok = false;
                for (int i = 1; i < p.Count; i++) if (Math.Abs(p[i].r - p[i - 1].r) + Math.Abs(p[i].c - p[i - 1].c) != 1) ok = false;
                foreach (var cell in p) if (!used.Add(cell)) ok = false;
            }
            Check(ok, "every path runs end to end in unit steps and no two share a cell");
            int cellsBefore = paths.Values.Sum(p => p.Count);
            Yuri.Modules.Forsaken.PuzSolver.OptimizePaths(paths);
            int cellsAfter = paths.Values.Sum(p => p.Count);
            Check(cellsAfter <= cellsBefore, "the reroute pass never lengthens the board: " + cellsBefore + " -> " + cellsAfter);
            // drawing through the fakes: the moves land on the cell centres, the button goes down once and up once per line
            var moves = new List<(int x, int y)>(); int downs = 0, ups = 0;
            Yuri.Modules.Forsaken.PuzSolver.MoveOverride = (x, y) => moves.Add((x, y));
            Yuri.Modules.Forsaken.PuzSolver.ButtonOverride = d => { if (d) downs++; else ups++; };
            Yuri.Modules.Forsaken.PuzSolver.FocusOverride = () => true; Yuri.Modules.Forsaken.PuzSolver.HeldOverride = () => false;
            Yuri.Modules.Forsaken.Puz.On = true; Yuri.Modules.Forsaken.Puz.SpeedT = 1.0; Yuri.Modules.Forsaken.Puz.SpeedApply();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string sig = Yuri.Modules.Forsaken.PuzSolver.SolveOne(dots);
            sw.Stop();
            Check(sig != "" && downs >= 4 && downs <= 8 && ups == downs, "SolveOne draws the four lines, redrawing the ones the (blank) verify capture cannot see: " + downs + " down, " + ups + " up");
            Check(moves.Contains((Yuri.Modules.Forsaken.PuzSolver.PX(1), Yuri.Modules.Forsaken.PuzSolver.PY(1))) && moves.Contains((Yuri.Modules.Forsaken.PuzSolver.PX(6), Yuri.Modules.Forsaken.PuzSolver.PY(1))), "the cursor visited both ends of the top row pair");
            Check(sw.ElapsedMilliseconds < 8000, "and did it at the slider's pace: " + sw.ElapsedMilliseconds + " ms");
            Yuri.Modules.Forsaken.PuzSolver.CaptureOverride = null; Yuri.Modules.Forsaken.PuzSolver.MoveOverride = null; Yuri.Modules.Forsaken.PuzSolver.ButtonOverride = null; Yuri.Modules.Forsaken.PuzSolver.FocusOverride = null; Yuri.Modules.Forsaken.PuzSolver.HeldOverride = null;
            Yuri.Modules.Forsaken.Puz.On = false; Yuri.Modules.Forsaken.Puz.GridPick(2);
        }

        Console.WriteLine("== text fields ==");
        {
            Fonts.Init(HubState.ProfileFont);
            // the selection and caret must be laid out in the face the text is
            // DRAWN in - EDIT PROFILE and the script hub draw in fHint
            Yuri.Modules.FastFlags.FfmField.Begin("pn", -1);
            Check(Yuri.Modules.FastFlags.FfmField.FontPublic == Fonts.fHint, "EDIT PROFILE's fields measure in the face they are drawn in");
            Yuri.Modules.FastFlags.FfmField.Begin("sn", -1);
            Check(Yuri.Modules.FastFlags.FfmField.FontPublic == Fonts.fHint, "and so do the script hub's");
            // the caret table is exact: the last stop IS the drawn width
            string ww = new string('w', 12);
            double drawn = Fonts.MeasureW(ww, Fonts.fHint) - Fonts.fHint.Pad * 2;
            Check(Math.Abs(Fonts.AdvAt(ww, Fonts.fHint, ww.Length) - drawn) < 0.01, "the caret's last stop lands exactly on the end of the text");
            Check(Fonts.AdvAt(ww, Fonts.fHint, 0) == 0, "and its first on the start");
            double half = Fonts.AdvAt(ww, Fonts.fHint, 6);
            Check(half > drawn * 0.35 && half < drawn * 0.65, "a caret halfway along the word sits halfway across it");
            // a stale double-click timer used to select the whole of the NEXT field
            Yuri.Modules.FastFlags.FfmField.Begin("pn", -1); Yuri.Modules.FastFlags.FfmField.Buf = "hello"; Yuri.Modules.FastFlags.FfmField.Car = 5;
            Yuri.Modules.FastFlags.FfmField.Mouse(4); Yuri.Modules.FastFlags.FfmField.Mouse(4);
            Check(Yuri.Modules.FastFlags.FfmField.Sel == 0 && Yuri.Modules.FastFlags.FfmField.Car == Yuri.Modules.FastFlags.FfmField.Buf.Length, "a second click inside the window selects the whole field");
            Yuri.Modules.FastFlags.FfmField.Begin("pb", -1); Yuri.Modules.FastFlags.FfmField.Buf = "second"; Yuri.Modules.FastFlags.FfmField.Car = 6;
            Yuri.Modules.FastFlags.FfmField.Mouse(4);
            Check(Yuri.Modules.FastFlags.FfmField.Sel == Yuri.Modules.FastFlags.FfmField.Car, "moving to another field does not carry the double-click into it");
            Yuri.Modules.FastFlags.FfmField.End(false);
        }

        Console.WriteLine("== fields, focus and wheels ==");
        {
            // a blur COMMITS - the .ahk had two fields when FFMBlur was written
            Yuri.Modules.FastFlags.FfmField.Begin("pn", -1);
            Yuri.Modules.FastFlags.FfmField.Buf = "kept on blur";
            Yuri.Modules.FastFlags.FfmField.Blur();
            Check(Yuri.Shell.Hub.EditProfile.BufFor("pn") == "kept on blur", "clicking away from a field keeps what was typed in it");
            // the editor's highlight goes with its focus
            Yuri.Modules.ScriptHub.Scr.FocusSet(1);
            Yuri.Modules.ScriptHub.Scr.Lines = new List<string> { "hello world", "second line" };
            Yuri.Modules.ScriptHub.Scr.Sl = 1; Yuri.Modules.ScriptHub.Scr.Sc = 0;
            Yuri.Modules.ScriptHub.Scr.Cl = 1; Yuri.Modules.ScriptHub.Scr.Cc = 5;
            Yuri.Modules.ScriptHub.Scr.SelOn = true;
            Check(Yuri.Modules.ScriptHub.Scr.SelText() == "hello", "the editor has a selection");
            Yuri.Modules.ScriptHub.Scr.Blur();
            Check(!Yuri.Modules.ScriptHub.Scr.SelOn && Yuri.Modules.ScriptHub.Scr.SelText() == "", "and it goes when the editor loses focus");
            // a cut that removes lines must not leave the old tokens rendering
            Yuri.Modules.ScriptHub.Scr.FocusSet(1);
            Yuri.Modules.ScriptHub.Scr.Retok();
            Check(!Yuri.Modules.ScriptHub.Scr.TokStale, "the tokens describe the lines");
            Yuri.Modules.ScriptHub.Scr.Sl = 1; Yuri.Modules.ScriptHub.Scr.Sc = 0;
            Yuri.Modules.ScriptHub.Scr.Cl = 2; Yuri.Modules.ScriptHub.Scr.Cc = 6;
            Yuri.Modules.ScriptHub.Scr.SelOn = true;
            Yuri.Modules.ScriptHub.Scr.DelSel();
            Check(Yuri.Modules.ScriptHub.Scr.TokStale, "a cut that removes a line marks them stale");
            Yuri.Modules.ScriptHub.Scr.Blur();
            Yuri.Modules.ScriptHub.Scr.Cl = 9999; Yuri.Modules.ScriptHub.Scr.Cc = 9999;
            Yuri.Modules.ScriptHub.Scr.Sl = 9999; Yuri.Modules.ScriptHub.Scr.Sc = 9999;
            Yuri.Modules.ScriptHub.Scr.SelOn = true;
            Yuri.Modules.ScriptHub.Scr.SelGet(out _, out _, out _, out _);
            Check(Yuri.Modules.ScriptHub.Scr.Cl <= Yuri.Modules.ScriptHub.Scr.Lines.Count, "a caret past the end of the buffer is pulled back into it");
            Yuri.Modules.ScriptHub.Scr.SelOn = false;
            // a panel wheel must not eat the module rail's
            {
                var hw = new HubSurface(); var HLw = hw.HL;
                double bfr = HLw.mdscrT;
                hw.HubModSel(6); HLw.modT = 1.0;
                bool took = Yuri.Shell.Hub.Tabs.Integrations.Wheel(hw, HLw.mdx + 10, HLw.mdy + 40, -1);
                Check(took && HLw.mdscrT != bfr, "with DEVICE OPTIMIZATIONS open the module rail still scrolls");
                bfr = HLw.mdscrT;
                hw.HubModSel(7); HLw.modT = 1.0;
                took = Yuri.Shell.Hub.Tabs.Integrations.Wheel(hw, HLw.mdx + 10, HLw.mdy + 40, -1);
                Check(took && HLw.mdscrT != bfr, "and with LOGIN ITEMS open");
                hw.HubModSel(0); hw.Stop();
            }
        }

        Console.WriteLine("== this round's fixes ==");
        {
            var h2 = new HubSurface(); var H2 = h2.HL;
            h2.Tab = 2; h2.TabPrev = 2;
            // the rail owns the wheel over itself, whatever panel is open
            h2.HubModSel(3); H2.modT = 1.0;                       // CURSOR - its wheel guards on y only
            double b0 = H2.mdscrT;
            bool took = Yuri.Shell.Hub.Tabs.Integrations.Wheel(h2, H2.mdx + 10, H2.aby + 30, -1);
            Check(took && H2.mdscrT != b0, "over the rail the rail scrolls, not the systems list");
            double r0 = H2.mdscrT;
            Yuri.Shell.Hub.Tabs.Integrations.Wheel(h2, H2.abx + 40, H2.aby + 30, -1);
            Check(H2.mdscrT == r0, "over the systems the rail keeps its hands off");
            // and its scrollbar is grabbable
            Check(Yuri.Shell.Hub.Tabs.Integrations.BarZone(H2, H2.mdx + H2.mdw + 7, (Yuri.Shell.Hub.Tabs.Integrations.MdRailTop(H2) + Yuri.Shell.Hub.Tabs.Integrations.MdRailBot()) / 2.0) == 1150, "the module rail's scrollbar is a zone");
            h2.HubModSel(0); h2.Stop();
            // re-arming a macro mid-close cancels the close instead of queueing a disarm
            Yuri.Modules.Forsaken.Ab.On = true;
            var rc = new Yuri.Modules.Forsaken.AbCard();
            var rw = new LayeredWindow(rc, "YURITESTREV", topmost: false, toolWindow: true);
            rw.Show(); Pump(rc, 2);
            rc.Dismiss();
            Check(rc.Closing, "turning the macro off starts the close");
            rc.Revive();
            Pump(rc, 40, 14);
            Check(!rc.Closing && !rc.ClosedPublic, "turning it straight back on cancels it, and the card survives");
            rc.Stop(); rw.Close();
            // collapsed, the card's window is the bubble - the rest of it was a
            // transparent rectangle over a game, taking every click that landed
            {
                var bc = new Yuri.Modules.Forsaken.AbCard();
                var bw = new LayeredWindow(bc, "YURITESTBUB", topmost: false, toolWindow: true);
                bw.Show(); Pump(bc, 4);
                Check(bc.CropW == 0, "open, the card's window is the whole card");
                bc.MinT_Public = 1.0; Pump(bc, 20, 6);
                Check(bc.CropW > 0 && bc.Width <= 140 * bc.K, "collapsed it is the bubble: " + bc.Width + "x" + bc.Height);
                Check(bc.ZoneAtPublic(Yuri.Modules.Forsaken.FskCard.BBX_Public, Yuri.Modules.Forsaken.FskCard.BBY_Public) == 3, "and the bubble still answers");
                bc.Stop(); bw.Close();
            }
            Yuri.Modules.Forsaken.Ab.On = false;
            // the update probe reads APP_VERSION out of the script, as the .ahk does
            Upd.ProbeOverride = () => Task.FromResult("; header\nglobal APP_VERSION := \"9.9.9\"\n");
            var pr = Upd.ProbeAsync(default).GetAwaiter().GetResult();
            Check(pr.ok && pr.ver == "9.9.9" && pr.newer, "the probe reads APP_VERSION out of the repository copy of the script");
            Upd.ProbeOverride = () => Task.FromResult("2.5.0\n");
            pr = Upd.ProbeAsync(default).GetAwaiter().GetResult();
            Check(pr.ok && pr.ver == "2.5.0", "and a one-line version file still works");
            Upd.ProbeOverride = null;
        }

        Console.WriteLine("== cursor restore ==");
        {
            // A mod file WE made goes on the first press; one that was already
            // there is handed back to Bloxstrap and a second press leaves it be.
            string root = Path.Combine(Path.GetTempPath(), "yuri-cur-" + Guid.NewGuid().ToString("N")[..8]);
            string mod = Path.Combine(root, "Modifications", "content", "textures", "Cursors", "KeyboardMouse");
            Directory.CreateDirectory(mod);
            string mine = Path.Combine(mod, "ArrowFarCursor.png"), theirs = Path.Combine(mod, "ArrowCursor.png");
            File.WriteAllText(theirs, "bloxstrap's own");
            var made = new HashSet<string> { Yuri.Modules.Cursor.Cur.Key(mine) };
            // the rule the loop applies, exercised directly
            bool DeleteMine(string path) => made.Contains(Yuri.Modules.Cursor.Cur.Key(path));
            Check(DeleteMine(mine) && !DeleteMine(theirs), "only the file we created is ours to remove");
            made.Remove(Yuri.Modules.Cursor.Cur.Key(mine));
            Check(!DeleteMine(mine), "and once removed it is not ours a second time");
            // an EMPTY record is not the same answer as NO record: once every key
            // has been dropped the legacy fallback must not come back
            Ini.Write(Paths.IniFile, "cursor", "madeset", 1L);
            Ini.Write(Paths.IniFile, "cursor", "made", "");
            Check(Ini.ReadInt(Paths.IniFile, "cursor", "madeset", 0) == 1, "the record survives being emptied");
            try { Directory.Delete(root, true); } catch { }
        }

        Console.WriteLine("== counts that read the real list ==");
        {
            Yuri.Gfx.Pool.Load();
            Yuri.Shell.Hub.Gallery.Register();
            Yuri.Modules.ScriptHub.Scr.Hooks();
            Check(Yuri.Shell.Hub.Tabs.SettingsTab.GalRotN() == Yuri.Gfx.Pool.RotN && Yuri.Gfx.Pool.RotN > 0, "SETTINGS counts the pictures actually in rotation");
            Check(Yuri.Shell.Hub.Tabs.UpdateLogs.GalHasRot() == Yuri.Gfx.Pool.HasRot, "UPDATE LOGS knows there is a rotation");
            Check(Yuri.Shell.Hub.Tabs.Dashboard.ScriptsPlaced() == Yuri.Modules.ScriptHub.Scr.List.Count, "the dashboard counts the placed scripts");
            Check(Yuri.Modules.Special.Spf.AcctCount == Yuri.Modules.Special.SpfAcct.Acct.Count, "the ACCOUNTS chip reads the list that is loaded");
            // a failed game lookup has to expire, or the place keeps its raw id forever
            var g = new Yuri.Platform.GameRec { Place = "1", Failed = true, At = Clock.Tick - 30000 };
            Check(g.Failed && Clock.Tick - g.At > 20000, "a failed lookup older than the retry window is asked again");
        }

        Console.WriteLine("== rings, menu order and focus ==");
        {
            // eight rings, both rows reachable and no zone shared with the sheet
            var he = new HubSurface(); var HE = he.HL;
            Yuri.Shell.Hub.EditProfile.Open();
            he.Tab = 1; he.TabPrev = 1; Pump(he, 20, 6); Yuri.Shell.Hub.EditProfile.T = 1.0;   // the sheet is eased open by Draw; the hit test only needs it open
            var seen = new HashSet<int>();
            for (double yy = 0; yy < HubLayout.bh; yy += 2)
                for (double xx = 0; xx < HubLayout.bw; xx += 2)
                { int z = Yuri.Shell.Hub.EditProfile.Zone(xx, yy); if (z is (>= 132 and <= 135) or (>= 150 and <= 153)) seen.Add(z); }
            Check(seen.Count == 8, "all eight ring choices are reachable: " + seen.Count);
            Check(!seen.Contains(138) && !seen.Contains(139), "and none of them lands on the veil or the sheet body");
            foreach (int z in seen) { Yuri.Shell.Hub.EditProfile.Click(he, z); }
            Check(HubState.ProfRing >= 1 && HubState.ProfRing <= 8, "a ring choice sticks inside the range");
            HubState.ProfRing = 1;
            Yuri.Shell.Hub.EditProfile.Close(); Pump(he, 30, 8);
            he.Stop();
            Check(Changelog.Entries.Any(e => e.text.Contains("built application")), "the changelog records the move off the script");
        }

        Console.WriteLine("== scrollbars keep their grip ==");
        {
            // The thumb must keep the point it was taken by. A press that misses
            // the thumb centres it; a press ON it must not move it at all.
            var HLs = new HubSurface(); var L = HLs.HL;
            double track = 200, thumb = 40, total = 1000, view = 200;
            double span = track - thumb;
            double atScr = 0.5 * (total - view);                       // thumb centred in the track
            double thTop = 10 + span * 0.5;
            L.dragOff = -1;
            double got = HubUI.ScrollFromY(L, thTop + 3, 10, track, total, view, thumb, atScr);
            Check(Math.Abs(got - atScr) < 0.01, "grabbing the thumb near its top does not move it");
            L.dragOff = -1;
            got = HubUI.ScrollFromY(L, thTop + thumb - 3, 10, track, total, view, thumb, atScr);
            Check(Math.Abs(got - atScr) < 0.01, "nor near its bottom");
            L.dragOff = -1;
            got = HubUI.ScrollFromY(L, 10 + track - 4, 10, track, total, view, thumb, atScr);
            Check(got > atScr + 100, "a press on empty track jumps the thumb to the cursor");
            L.dragOff = -1;
            HLs.Stop();
        }

        Console.WriteLine("== the mark follows the accent ==");
        {
            uint was = HubState.Accent;
            for (int i = 0; i < HubState.Accents.Length; i++)
            {
                HubState.Accent = HubState.Accents[i];
                Check(Yuri.Shell.LayeredWindow.IconIndex() == i + 1, "accent " + (i + 1) + " picks its own mark");
                Check(Yuri.Shell.LayeredWindow.AppIcon(i + 1) is not null, "and that mark is shipped");
            }
            HubState.Accent = 0xFF123456;
            Check(Yuri.Shell.LayeredWindow.IconIndex() == 1, "an accent outside the palette falls back to the default mark");
            HubState.Accent = was;
        }

        Console.WriteLine("== the collapsed window is the pill ==");
        {
            var hc = new HubSurface(); var HC = hc.HL;
            var wc = new LayeredWindow(hc, "YURITESTCROP", topmost: false, toolWindow: true);
            wc.Show(); Pump(hc, 20, 4);
            Check(hc.CropW == 0, "open, the window is the whole card");
            hc.HubMin(1); Pump(hc, 90, 6);
            Check(HC.minT == 1.0, "the fold lands");
            Check(hc.CropW > 0 && hc.CropH > 0, "and the window has shrunk to the pill");
            Check(hc.Width <= 200 * hc.K && hc.Height <= 220 * hc.K, "which is the size it reports: " + hc.Width + "x" + hc.Height);
            // the pill still answers, and its own coordinates are unchanged
            Check(hc.ZoneAtPublic(HC.mbx, HC.mby) == 9, "the pill is still the zone under its own centre");
            hc.HubMin(0); Pump(hc, 90, 6);
            Check(hc.CropW == 0 && Math.Abs(hc.Width - HubLayout.bw * hc.K) < 1, "unfolding gives the whole card back");
            hc.Stop(); wc.Close();
        }

        Console.WriteLine("== the file route ==");
        {
            // The client reads ClientAppSettings.json on every launch, on both
            // platforms. This is the only route macOS has, and it was written
            // and never wired to anything.
            Ffm.ClearAll();
            Ffm.Add("FFlagDebugGraphicsPreferVulkan", "True");
            Ffm.Add("DFIntTaskSchedulerTargetFps", "144");
            string body = Ffm.JsonPublic();
            Check(body.Contains("FFlagDebugGraphicsPreferVulkan") && body.Contains("144"), "the staged list becomes a ClientAppSettings body");
            Check(body.TrimStart().StartsWith("{") && body.TrimEnd().EndsWith("}"), "which is a JSON object");
            // and the delivery folders are looked for on this machine's terms
            Check(Ffm.AppSettingsN() >= 0, "the install scan runs without a client present");
            Ffm.ClearAll();
        }

        Console.WriteLine("== windows-only modules are inert here ==");
        {
            // This harness runs on Linux, so Os.IsWin is false and the veil is up:
            // the panels must refuse every zone under it rather than look live.
            Yuri.Modules.Forsaken.FskPanel.Register();
            Yuri.Modules.DeviceOpt.DopPanel.Register();
            var hw2 = new HubSurface(); var HW = hw2.HL;
            hw2.Tab = 2; hw2.TabPrev = 2;
            foreach (int mod in new[] { 2, 6 })
            {
                hw2.HubModSel(mod); HW.modT = 1.0;
                int live = 0;
                for (double yy = HW.aby; yy < HW.aby + 300; yy += 7)
                    for (double xx = HW.abx; xx < HW.abx + HW.abw; xx += 7)
                        if (Yuri.Shell.Hub.Tabs.Integrations.PanelZones[mod](hw2, xx, yy) != 0) live++;
                Check(live == 0, "module " + mod + " answers no zone behind the veil: " + live);
            }
            hw2.HubModSel(0); hw2.Stop();
        }

        Console.WriteLine("== the file route does not run away with itself ==");
        {
            // AfterChange used to hang off Say as well as off the list, so every
            // status line wrote the file - and FileApply says something, so it
            // wrote twice per press and again for anything else that spoke.
            Ffm.ClearAll();
            bool wasAuto = FfmPanel.AutoInject; FfmPanel.AutoInject = true;
            Ffm.WriteFault = "";
            Ffm.Say("A STATUS LINE");
            Check(Ffm.WriteFault == "", "a status line does not touch the file");
            FfmPanel.AutoInject = wasAuto;
            Ffm.ClearAll();
        }

        Console.WriteLine("== the loader waits for real work ==");
        {
            // The loader already gated on Boot.Left(); the pieces that fill the
            // saved places and the accounts never told it they were running, so
            // it counted two jobs and opened while the rest was in the air.
            int wBefore = Yuri.Shell.Boot.Total;
            Yuri.Shell.Boot.AddWait(() => { }, () => true, 500);
            Check(Yuri.Shell.Boot.Total == wBefore + 1, "work with no Task of its own is still counted");
            long w0 = Clock.Tick;
            while (Yuri.Shell.Boot.Left() > 0 && Clock.Tick - w0 < 3000) { System.Threading.Thread.Sleep(20); Dispatcher.UIThread.RunJobs(); }
            Check(Yuri.Shell.Boot.Left() == 0, "and clears when its store says it is done");
            // a job that never finishes must not hold the loader for ever
            long t0 = Clock.Tick;
            Yuri.Shell.Boot.AddWait(() => { }, () => false, 400);
            while (Yuri.Shell.Boot.Left() > 0 && Clock.Tick - t0 < 4000) { System.Threading.Thread.Sleep(20); Dispatcher.UIThread.RunJobs(); }
            Check(Yuri.Shell.Boot.Left() == 0 && Clock.Tick - t0 < 3000, "and a job that never lands gives up at its cap");
            Check(Yuri.Gfx.Pool.N >= 0 && Yuri.Shell.Boot.AvatarPoolN == Yuri.Gfx.Pool.N || Yuri.Shell.Boot.AvatarPoolN == 0, "the avatar count the loader prints is the pool's own");
        }

        Console.WriteLine("== the grid comes back without rebuilding ==");
        {
            // Building a window is the slow part. The grid was destroyed for the
            // duration of every solve and rebuilt afterwards, which is the pause
            // between asking for it and seeing it.
            var mkS = new Yuri.Modules.Forsaken.MarkerSurface(300, 300) { Ox = 10, Oy = 10 };
            var mkW = new LayeredWindow(mkS, "YURITESTMK", topmost: false, toolWindow: true) { ShowActivated = false };
            mkW.Show(); Pump(mkS, 3);
            Check(mkW.IsVisible, "the grid window is up");
            // The guarantee is that hiding does not DESTROY it - a timing
            // comparison here measures the harness, not the fix: headless window
            // creation is cheap and the noise is larger than the difference.
            mkS.Stop(); mkW.Hide();
            Check(!mkW.IsVisible, "hidden");
            mkW.Show(); mkS.Tim(Pace.TICK_A); Pump(mkS, 3);
            Check(mkW.IsVisible && mkS.Bw == 300, "shown again, and it is the same surface - never rebuilt");
            mkS.Stop(); mkW.Close();
        }

        Console.WriteLine("== a cropped window still knows where it is ==");
        {
            // Everything that stores or reasons about a position means the
            // CARD's origin, not the bubble's. Saving the cropped corner and
            // restoring it uncropped moves the card by the crop next launch, and
            // the drag clamp measures the grip from the window's origin.
            Yuri.Modules.Forsaken.Ab.On = true;             // a card whose system is off closes itself, and a closing card is not cropped
            var cc = new Yuri.Modules.Forsaken.AbCard();
            var cwn = new LayeredWindow(cc, "YURITESTPOS", topmost: false, toolWindow: true);
            cwn.Show(); Pump(cc, 4);
            cwn.Position = new PixelPoint(400, 300);
            var open = cc.UncroppedPos();
            Check(open.X == 400 && open.Y == 300, "uncropped, the origin is the window's own");
            cc.MinT_Public = 1.0; Pump(cc, 20, 6);
            Check(cc.CropW > 0, "the card collapsed");
            var folded = cc.UncroppedPos();
            Check(Math.Abs(folded.X - 400) <= 2 && Math.Abs(folded.Y - 300) <= 2,
                  $"collapsed, it still reports the card's origin: {folded.X},{folded.Y}");
            cc.Stop(); cwn.Close(); Yuri.Modules.Forsaken.Ab.On = false;
        }

        Console.WriteLine("== a card can be focused again ==");
        {
            // WS_EX_NOACTIVATE stops a CLICK activating the window, which is what
            // keeps an overlay off a game's input. It also means the process can
            // never become the foreground one, and a background frame cap then
            // holds the card at that cap for good. A deliberate press asks for
            // the foreground when there is no client in front to take it from.
            Yuri.Modules.Forsaken.Ab.On = true;
            var fc = new Yuri.Modules.Forsaken.AbCard();
            var fw = new LayeredWindow(fc, "YURITESTFOC", topmost: false, toolWindow: true);
            fw.Show(); Pump(fc, 3);
            Check(!fc.FocusTried, "nothing is asked for before a press");
            fc.PressAt(26 + 160, 26 + 80);
            Check(fc.FocusTried, "a press asks for the foreground");
            // and the block must not eat that press: the card is over the game,
            // and the click that focuses it arrives while the game is still in
            // front. Without this guard it would be swallowed and sent as Q.
            Check(Yuri.Modules.Forsaken.FskOverlays.HitAt(-99999, -99999) == false, "a point nowhere near a card is not a card hit");
            fc.ReleaseAt(26 + 160, 26 + 80);
            fc.Stop(); fw.Close(); Yuri.Modules.Forsaken.Ab.On = false;
        }

        Console.WriteLine("== the flag database ==");
        {
            // A flag IS its prefix. Roughly a hundred keys in the JSON dumps
            // carry none, and one of those staged is a name the client will
            // never read, sitting in the list looking like a flag.
            Check(FfmViews.Typed("DFFlagFluidForcesDefaultEnabled"), "a real name is kept");
            Check(FfmViews.Typed("FIntTaskSchedulerTargetFps"), "so is an FInt");
            Check(!FfmViews.Typed("AllowVideoPreRoll"), "a key with no prefix is not a flag");
            Check(!FfmViews.Typed("FFlag"), "and the bare prefix is not one either");
            // a name the tracker has not heard of is still stageable
            Ffm.ClearAll();
            FfmViews.Db.Clear(); FfmViews.Db.Add("DFFlagSomethingKnown");
            Ffm.Q = "DFFlagBrandNewNeverSeen"; FfmViews.DbSync();
            Check(FfmViews.DbList().Count == 0, "it matches nothing in the list");
            FfmViews.DbEnter();
            Check(Ffm.Flags.Exists(f => f.Name == "DFFlagBrandNewNeverSeen"), "ENTER stages it anyway");
            Ffm.ClearAll();
            Ffm.Q = "notaflag"; FfmViews.DbSync();
            FfmViews.DbEnter();
            Check(Ffm.Flags.Count == 0, "but a name with no prefix is refused");
            Ffm.Q = ""; FfmViews.DbSync(); Ffm.ClearAll();
        }

        Console.WriteLine("== finding the client ==");
        {
            // The whole test used to be GetProcessesByName of ONE name, which on
            // macOS never matched: the client is not RobloxPlayerBeta there, the
            // unified app is not always RobloxPlayer either, and .NET's name
            // comparison is not case-insensitive off Windows.
            bool ran = Yuri.Platform.Roblox.IsRunning();
            Check(ran == false || Yuri.Platform.Roblox.FoundAs != "", "when it finds a client it says what it found");
            Check(Yuri.Platform.Roblox.MacBundle() == "" || Yuri.Platform.Roblox.MacBundle().EndsWith(".app"), "a macOS bundle path is a bundle path");
            // this machine has no Roblox, so the scan must simply answer no
            Check(!ran, "and answers plainly when there is none");
            // The cached answer is up to 1.2 s old. Both places that judged a
            // CLOSE read it straight after the kill, so a client that had gone
            // was still "running" and a close that worked said it had not.
            Check(Yuri.Platform.Roblox.Processes().Count == 0, "the shared lookup finds no client here");
            Check(Yuri.Platform.Roblox.Kill(50) == 0, "a kill with nothing to kill counts zero and does not throw");
            Yuri.Platform.Roblox.Refresh();
            Check(!Yuri.Platform.Roblox.IsRunningNow(), "and the fresh answer after it is no");
        }

        Console.WriteLine("== the tour is modal ==");
        {
            var ht = new HubSurface();
            var wt = new LayeredWindow(ht, "YURITESTTUT", topmost: false, toolWindow: true);
            wt.Show(); Pump(ht, 3);
            var HT = ht.HL;
            // a live zone behind the tour, before it starts
            int zBehind = ht.ZoneAtPublic(HT.sbx + 20, HT.sby + 40);
            Check(zBehind != 0, "there is a live zone on the sidebar");
            Tut.Start(false); Pump(ht, 10, 4);
            Check(Tut.On, "the tour is up");
            Check(ht.ZoneAtPublic(HT.sbx + 20, HT.sby + 40) != zBehind, "and nothing behind it answers the hit test any more");
            // its own controls still do
            int zc = 0;
            for (double yy = 0; yy < HubLayout.bh; yy += 3)
                for (double xx = 0; xx < HubLayout.bw; xx += 3)
                { int z = ht.ZoneAtPublic(xx, yy); if (z == 2200) { zc = z; break; } }
            Check(zc == 2200, "NEXT is still reachable");
            Tut.End(); Pump(ht, 30, 12);
            Check(!Tut.On && ht.ZoneAtPublic(HT.sbx + 20, HT.sby + 40) == zBehind, "and the hub comes back when the tour ends");
            ht.Stop(); wt.Close();
        }

        Console.WriteLine("== loading screen ==");
        {
            Fonts.Init(HubState.ProfileFont);
            var ls = new LoadingSurface(() => { });
            var lw = new LayeredWindow(ls, "YURITESTLOAD", topmost: false, toolWindow: true);
            lw.Show(); Pump(ls, 2);
            var lp = new Point(200 * ls.K, 100 * ls.K);
            lw.MouseDown(lp, MouseButton.Left); Pump(ls, 2);
            lw.MouseMove(new Point(260 * ls.K, 140 * ls.K)); Pump(ls, 2);
            Check(!ls.Dragging, "the loading card is not draggable - LoadingScreen() installs no hit test in the .ahk");
            lw.MouseUp(new Point(260 * ls.K, 140 * ls.K), MouseButton.Left); Pump(ls, 2);
            ls.Stop(); lw.Close();
        }

        Console.WriteLine("== forsaken overlays ==");
        {
            Yuri.Modules.Forsaken.Keys.BindKey = "F6"; Yuri.Modules.Forsaken.Keys.PuzKey = "F8";
            Yuri.Modules.Forsaken.Ab.Enabled = true;
            Fonts.Init(HubState.ProfileFont);
            var card = new Yuri.Modules.Forsaken.AbCard();
            Check(card.ZoneAtPublic(26 + 312 - 22, 26 + 16) == 2 && card.ZoneAtPublic(26 + 312 - 44, 26 + 16) == 1 && card.ZoneAtPublic(26 + 312 / 2, 26 + 15) == 4 && card.ZoneAtPublic(26 + 46, 26 + 65) == 5 && card.ZoneAtPublic(26 + 30, 26 + 116) == 6, "the block card's zones: close, minimise, grip, portrait, keybind");
            var pc = new Yuri.Modules.Forsaken.PuzCard();
            Check(pc.ZoneAtPublic(26 + 88 + 40, 26 + 90) == 9 && pc.ZoneAtPublic(2, 2) == 0, "the puzzle card's speed slider is a zone, its halo is not");
            pc.MinSet(1); pc.MinT_Public = 1.0;
            Check(pc.ZoneAtPublic(26 + 312 - 36, 26 + 132 - 36) == 3, "minimised, the bubble is the only zone");
            pc.MinT_Public = 0;
            Yuri.Modules.Forsaken.FskOverlays.MkWant = false;
            Ini.Write(Paths.IniFile, "puzzle", "markers", 0L);
            Yuri.Modules.Forsaken.FskOverlays.Load();
            Check(!Yuri.Modules.Forsaken.FskOverlays.MkWant, "the markers remember being off");
            Ini.Write(Paths.IniFile, "puzzle", "markers", 1L); Yuri.Modules.Forsaken.FskOverlays.Load();
            Check(Yuri.Modules.Forsaken.FskOverlays.MkWant, "and being on");
            Ini.Write(Paths.IniFile, "puzzle", "markers", 0L); Yuri.Modules.Forsaken.FskOverlays.MkWant = false;
            Yuri.Modules.Forsaken.Ab.Enabled = false;

            // ---- the drag: the grip and the bubble only, promoted at 4 px ----
            Yuri.Modules.Forsaken.Ab.On = true;                 // an AbCard whose system is off closes itself
            var dc = new Yuri.Modules.Forsaken.AbCard();
            var dw = new LayeredWindow(dc, "YURITESTBLOCK", topmost: false, toolWindow: true);
            dw.Show(); Pump(dc, 2);
            dc.PressAt(26 + 312 / 2, 26 + 15);
            Check(dc.DragOnPublic == 1 && !dc.DragMoves, "the grip takes the press but the card has not moved yet");
            dc.MoveAt(26 + 312 / 2 + 12, 26 + 15);
            Check(dc.DragOnPublic == 2 && dc.DragMoves, "past 4 px it is a drag");
            dc.ReleaseAt(26 + 312 / 2 + 12, 26 + 15);
            Check(dc.DragOnPublic == 0, "and the release ends it");
            dc.PressAt(26 + 160, 26 + 80);
            Check(dc.DragOnPublic == 0 && !dc.Dragging, "the card BODY is not a drag handle - only the six-dot grip is");
            dc.ReleaseAt(26 + 160, 26 + 80);
            // minimise acts on the PRESS, as the .ahk's Click() does
            dc.PressAt(26 + 312 - 44, 26 + 16);
            Check(dc.MinTo_Public == 1.0, "minimise fires on the press, not the release");
            dc.MinT_Public = 1.0;
            // the bubble: a click restores it, a drag moves it
            dc.PressAt(26 + 312 - 36, 26 + 132 - 36);
            Check(dc.DragOnPublic == 1, "the collapsed bubble takes a press");
            dc.ReleaseAt(26 + 312 - 36, 26 + 132 - 36);
            Check(dc.MinTo_Public == 0.0, "a bubble press that never moved is the click that restores");
            dc.Stop(); dw.Close(); Yuri.Modules.Forsaken.Ab.On = false;

            // ---- a bind on a side or middle mouse button ----
            Yuri.Modules.Forsaken.Ab.Register();
            Yuri.Modules.Forsaken.Keys.RebindOn = false;
            Yuri.Modules.Forsaken.Keys.BindKey = "XButton2";
            Yuri.Modules.Forsaken.Ab.On = true; Yuri.Modules.Forsaken.Ab.Enabled = false;
            Check(Yuri.Modules.Forsaken.Keys.VkOf("XButton2") == 0, "VkOf cannot name a mouse button - the keyboard path can never see this bind");
            Yuri.Modules.Forsaken.Ab.FeedMouse(0x20B, 2, true); Dispatcher.UIThread.RunJobs();
            Check(Yuri.Modules.Forsaken.Ab.Enabled, "XButton2 toggles the block on");
            Yuri.Modules.Forsaken.Ab.FeedMouse(0x20B, 2, true); Dispatcher.UIThread.RunJobs();
            Check(!Yuri.Modules.Forsaken.Ab.Enabled, "and off again");
            Yuri.Modules.Forsaken.Ab.FeedMouse(0x20B, 1, true); Dispatcher.UIThread.RunJobs();
            Check(!Yuri.Modules.Forsaken.Ab.Enabled, "XButton1 is a different button and does nothing");
            Yuri.Modules.Forsaken.Keys.BindKey = "MButton";
            Yuri.Modules.Forsaken.Ab.FeedMouse(0x207, 0, true); Dispatcher.UIThread.RunJobs();
            Check(Yuri.Modules.Forsaken.Ab.Enabled, "so does a bind on the middle button");
            Yuri.Modules.Forsaken.Ab.Enabled = false; Yuri.Modules.Forsaken.Ab.On = false;
            Yuri.Modules.Forsaken.Keys.BindKey = "F6";
            // the rebind chip takes one too
            Yuri.Modules.Forsaken.Keys.Start(1);
            Yuri.Modules.Forsaken.Ab.FeedMouse(0x20B, 1, true); Dispatcher.UIThread.RunJobs();
            Check(!Yuri.Modules.Forsaken.Keys.RebindOn && Yuri.Modules.Forsaken.Keys.BindKey == "XButton1", "and the rebind chip accepts one");
            Yuri.Modules.Forsaken.Keys.BindKey = "F6"; Yuri.Modules.Forsaken.Keys.Save();

            // ---- the banner offset ----
            // Board 1 of a session sits one banner lower; every board after it
            // is down on the grid. It is a translation applied at READ, so the
            // calibration is untouched either way.
            Yuri.Modules.Forsaken.PuzSolver.ScreenSync();
            int ban = Yuri.Modules.Forsaken.PuzSolver.BannerDy();
            Check(ban > 0, "the reference table carries a banner offset for this screen");
            Yuri.Modules.Forsaken.Puz.DyPin = false; Yuri.Modules.Forsaken.Puz.Sess = 0;
            Yuri.Modules.Forsaken.Puz.BoardApply();
            int upY = Yuri.Modules.Forsaken.PuzSolver.PY(1);
            Check(Yuri.Modules.Forsaken.Puz.BoardDy == ban, "board 1 of a session is UP by the banner");
            Yuri.Modules.Forsaken.Puz.Sess = 1; Yuri.Modules.Forsaken.Puz.BoardApply();
            int dnY = Yuri.Modules.Forsaken.PuzSolver.PY(1);
            Check(Yuri.Modules.Forsaken.Puz.BoardDy == 0 && upY - dnY == ban, "every board after it is DOWN, and PY moves with it");
            Yuri.Modules.Forsaken.Puz.BoardSet(ban);
            Yuri.Modules.Forsaken.Puz.Sess = 5; Yuri.Modules.Forsaken.Puz.BoardApply();
            Check(Yuri.Modules.Forsaken.Puz.DyPin && Yuri.Modules.Forsaken.Puz.BoardDy == ban, "a button press pins it for the rest of the session");
            Yuri.Modules.Forsaken.Puz.DyPin = false; Yuri.Modules.Forsaken.Puz.Sess = 0; Yuri.Modules.Forsaken.Puz.BoardDy = 0;

            // ---- the grid inspector ----
            var dbg = new Yuri.Modules.Forsaken.DbgCard();
            var dbw = new LayeredWindow(dbg, "YURITESTDBG", topmost: false, toolWindow: true);
            dbw.Show(); Pump(dbg, 3);
            Check(dbg.ZoneAtPublicDbg(20, 0) == 0, "the inspector's plate is inert");
            int zDown = 0, zUp = 0;
            for (double yy = 0; yy < Yuri.Modules.Forsaken.DbgCard.H; yy++)
                for (double xx = 16; xx < Yuri.Modules.Forsaken.DbgCard.W - 16; xx += 8)
                { int z = dbg.ZoneAtPublicDbg(xx, yy); if (z == 1) zDown = 1; if (z == 2) zUp = 1; }
            Check(zDown == 1 && zUp == 1, "DOWN BOARD and UP BOARD are both hit zones");
            dbg.Stop(); dbw.Close();

            // ---- the clash card ----
            Yuri.Modules.Forsaken.Puz.ClashRaise("EXTERNAL BLOCK REBIND", "PUZZLE AI", "XB2");
            Check(Yuri.Modules.Forsaken.Puz.ClashUp && Yuri.Modules.Forsaken.Puz.ClashOwn == "PUZZLE AI", "the holder is the OWN system, the refused one is WHO");
            Yuri.Modules.Forsaken.Puz.ClashDismiss();
            Check(Yuri.Modules.Forsaken.Puz.ClashOut != 0 && Yuri.Modules.Forsaken.Puz.ClashUp, "GOT IT starts the exit without clearing the card under it");
            Yuri.Modules.Forsaken.Puz.ClashOut -= 400; Yuri.Modules.Forsaken.Puz.ClashExpire();
            Check(!Yuri.Modules.Forsaken.Puz.ClashUp, "and it clears when the exit lands");

            // ---- the toggle has to reach the card ----
            Yuri.Modules.Forsaken.Ab.On = true;
            var ac = new Yuri.Modules.Forsaken.AbCard();
            var aw = new LayeredWindow(ac, "YURITESTAB", topmost: false, toolWindow: true);
            aw.Show(); Pump(ac, 3);
            ac.ToggleAnim(1, 1.0);
            Check(ac.AnimLive && ac.RippleLive && ac.TogAt_Public != 0 && ac.TogDir_Public == 1, "a toggle starts the state ease, the ripple and the sweep");
            Pump(ac, 30, 12);
            Check(!ac.AnimLive && Math.Abs(ac.AnimT_Public - 1.0) < 1e-9, "and the ease lands exactly on the target");
            ac.ToggleAnim(-1, 0.0);
            Pump(ac, 30, 12);
            Check(ac.AnimT_Public == 0.0 && !ac.RippleLive, "back down, and the ripple expires even off the draw path");
            // the keybind row eases on hover, which it had no ease for at all
            aw.MouseMove(new Point((26 + 30) * ac.K, (26 + 116) * ac.K)); Pump(ac, 12);
            Check(ac.H4_Public > 0.3, "the keybind row has a hover ease");
            aw.MouseMove(new Point((26 + 150) * ac.K, (26 + 60) * ac.K)); Pump(ac, 20);
            Check(ac.H4_Public < 0.2, "and it falls back off it");
            // the close runs its full curve and only then tears the card down
            ac.Dismiss();
            Pump(ac, 6, 6);
            Check(!ac.ClosedPublic, "the close is still playing a third of the way in");
            Pump(ac, 40, 14);
            Check(ac.ClosedPublic, "and it tears down only when the curve lands");
            ac.Stop(); aw.Close(); Yuri.Modules.Forsaken.Ab.On = false;
        }

        Console.WriteLine("== fps boost chips ==");
        {
            try { File.Delete(SpfFpsChips.JnlPath); } catch { }
            if (!Yuri.Platform.Os.IsWin)
            {
                Spf.FpsSys = false; SpfFpsChips.SubToggle(3);
                Check(Spf.Msg.Contains("WINDOWS FEATURES") && !Spf.FpsSys, "off Windows the chips explain themselves: " + Spf.Msg);
                Check(SpfFpsChips.JnlRecover() == 0 && !File.Exists(SpfFpsChips.JnlPath), "no journal, nothing to recover");
            }
            Check(SpfFpsChips.SysMax() >= 4, "SYSTEM counts its levers");
        }

        Console.WriteLine("== hub ==");
        Paths.InitDirs();
        try { File.Delete(Paths.IniFile); } catch { }
        HubState.Load();
        Fonts.Init(HubState.ProfileFont);
        bool exited = false;
        Boot.Exiting = () => exited = true;
        var h = new HubSurface();
        var w = new LayeredWindow(h, "test", true, true);
        w.Show();
        Pump(h, 40);                                              // the intro
        var HL = h.HL;
        Check(HubSurface.Live == h && h.Period > 0, "hub live with its loop running");
        {
            // the field's geometry: the selection and the caret sit on the glyphs, a click lands on the character under it
            var fnt = Fonts.fHint;
            Check(Fonts.Adv("", fnt) == 0 && Math.Abs(Fonts.Adv("ab", fnt) - (Fonts.MeasureW("ab", fnt) - fnt.Pad * 2)) < 1e-6, "Adv is the glyph run without the pads");
            FfmField.Begin("db", 0); FfmField.Char("2753915549");
            double x0 = FfmField.TextX("db");
            Check(FfmField.CaretX("2753915549", fnt.Pad + Fonts.Adv("275", fnt) + 0.5) == 3, "a point just past the third glyph is caret 3");
            Check(FfmField.CaretX("2753915549", 0.0) == 0 && FfmField.CaretX("2753915549", 9999) == 10, "the ends clamp");
            FfmField.Mouse(x0 + fnt.Pad + Fonts.Adv("2753", fnt) + 0.5);
            Check(FfmField.Car == 4 && FfmField.Sel == 4 && FfmField.MSel, "a press places the caret and anchors the selection");
            FfmField.Drag(x0 + fnt.Pad + Fonts.Adv("27539155", fnt) + 0.5);
            Check(FfmField.Car == 8 && FfmField.Sel == 4, "the drag extends it");
            FfmField.MouseUp(); FfmField.End(false);
            Check(!FfmField.MSel && FfmField.Edit == "", "the release ends the mouse selection");
            // ScrollFromY keeps the grip
            HL.dragOff = -1;
            double s1 = HubUI.ScrollFromY(HL, 100 + 5, 100, 200, 1000, 200, 40, 0);          // press 5 px into a thumb sitting at the top
            Check(Math.Abs(HL.dragOff - 5) < 1e-9 && s1 == 0, "the grip is where the thumb was pressed");
            double s2 = HubUI.ScrollFromY(HL, 100 + 5 + 80, 100, 200, 1000, 200, 40, s1);
            Check(Math.Abs(s2 - 800 * 0.5) < 1e-9, "moving 80 px down a 160 px span scrolls half the way");
            HL.dragOff = -1;
            double s3 = HubUI.ScrollFromY(HL, 100 + 150, 100, 200, 1000, 200, 40, 0);         // press well below the thumb: it jumps, centred on the pointer
            Check(Math.Abs(HL.dragOff - 20) < 1e-9 && Math.Abs(s3 - 800 * (130.0 / 160.0)) < 1e-9, "a press off the thumb centres it on the pointer");
            HL.dragOff = -1;
        }
        Console.WriteLine("== gallery + profile ==");
        {
            Yuri.Gfx.Pool.Load();
            Check(Yuri.Gfx.Pool.N >= 5 && Yuri.Gfx.Pool.GalLo == Yuri.Gfx.Pool.Base + 1, "the pool loads the embedded set: " + Yuri.Gfx.Pool.N);
            string k3 = Yuri.Gfx.Pool.SelKey(3);
            Check(k3 != "" && Yuri.Gfx.Pool.SelFindKey(k3.ToUpperInvariant()) == 3, "keys round-trip by name, case-insensitive");
            Yuri.Gfx.Pool.ProfPicSet = false;
            Gallery.Show(0, false);
            Check(Gallery.Open && !Gallery.Manage, "the gallery opens for the profile picture");
            Pump(h, 30);
            int zTile = Gallery.Zone(HubLayout.pd + (HubLayout.cw - 300) / 2 + 24 + 32, HubLayout.pd + (HubLayout.ch - (58 + 2 * 78 - 14 + 16 + 48)) / 2 + 58 + 32);
            Check(zTile == 1241, "the first tile sits where the grid puts it: " + zTile);
            Gallery.Click(h, 1241); Pump(h, 100, 4);
            Check(!Gallery.Open && Gallery.Crop, "a tile pick opens the cropper");
            // USE commits at once; the card then plays its exit before it is gone
            bool used = Gallery.Wheel(1) && Gallery.Click(h, 1226);
            Check(used && Gallery.CropClosing, "USE crops and starts the card's exit");
            Pump(h, 40, 12);
            Check(!Gallery.Crop, "and the card is gone when the exit lands");
            Check(Yuri.Gfx.Pool.ProfPicSet && Yuri.Gfx.Pool.CustomIdx != 0 && File.Exists(Path.Combine(Paths.Av, "profile_crop.png")) && Yuri.Gfx.Pool.SelNext == Yuri.Gfx.Pool.CustomIdx, "the crop is the profile picture and profile_crop.png exists");
            Check(Ini.Read(Paths.IniFile, "profile", "pic", "") == "profile_crop.png", "the choice is saved by name");
            Gallery.Show(0, true);
            Check(Gallery.Open && Gallery.Manage, "manage mode lists the gate rotation");
            Gallery.Click(h, 1212); Pump(h, 100, 4);
            Check(!Gallery.Open, "the cross closes it");
            EditProfile.Open(); Pump(h, 40);
            Check(EditProfile.On && EditProfile.Zone(HubLayout.pd + HubLayout.cw / 2 - 190 + 354, HubLayout.pd + HubLayout.ch / 2 - 177 + 24) == 130, "EDIT PROFILE opens with its cross where the .ahk puts it");
            EditProfile.Click(h, 134);
            Check(HubState.ProfRing == 3 && Ini.ReadInt(Paths.IniFile, "profile", "ring", 1) == 3, "a ring chip saves");
            FfmField.Begin("pn", 0); FfmField.Char("ZEAL"); FfmField.End(true);
            Check(HubState.ProfName == "ZEAL" && Ini.Read(Paths.IniFile, "profile", "name", "") == "ZEAL", "the name field commits");
            EditProfile.Click(h, 144); Pump(h, 60, 4);
            Check(!EditProfile.On, "DONE closes it");
            HubState.ProfName = "USERNAME"; HubState.ProfRing = 1; EditProfile.Save();
        }

        Console.WriteLine("== low performance mode ==");
        {
            // the loading screen and the gate run on RendTim's mapping (40 / 220), the hub on HubTim's (40 / 250 / stop)
            HubState.LowPerf = true;
            var ls = new Yuri.Shell.Loading.LoadingSurface(() => { });
            ls.Tim(Pace.ModalTick());
            Check(ls.Period == 40 && ls.PEff == 40, "the loading screen ticks at 40 ms under low performance: " + ls.Period);
            ls.Tim(Pace.TICK_S);
            Check(ls.Period == 220, "and 220 for its slower tiers, never stopped: " + ls.Period);
            ls.Stop();
            h.Tim(Pace.TICK_A); Check(h.Period == 40, "the hub's fast tier is 40");
            h.Tim(Pace.TICK_LP); Check(h.Period == 250, "its idle tier 250");
            h.Tim(Pace.TICK_BG); Check(h.Period == 0, "and deep idle stops outright");
            HubState.LowPerf = false; h.Tim(Pace.TICK_A);
            Check(h.Period == Pace.TICK_A, "off again, the real rate is back");
        }

        Console.WriteLine("== right-click menu ==");
        {
            h.TabSet(2); Pump(h, 20); if (h.HubMod != 1) h.HubModSel(1); Pump(h, 40);
            FfmField.Begin("q", 0); FfmField.Char("FFlagTest"); FfmField.Sel = 0; FfmField.Car = 5;
            FfmCtx.Open(h, 300, 300); Pump(h, 20);
            Check(FfmCtx.On && h.ZoneAtPublic(300 + 20, 300 + 12) == 701 && h.ZoneAtPublic(300 + 20, 300 + 12 + 26) == 702 && h.ZoneAtPublic(300 + 20, 300 + 12 + 52) == 703, "the menu's three rows are zones 701-703");
            var items = FfmCtx.Items();
            Check(items[0].t == "Cut" && items[0].on && items[1].on, "Cut and Copy are live while the field has a selection");
            FfmCtx.Run(h, 2); Pump(h, 10);
            Check(!FfmCtx.On, "a row closes the menu");
            FfmField.Sel = -1;
            FfmCtx.Open(h, 300, 300); Pump(h, 10);
            Check(!FfmCtx.Items()[0].on, "with nothing selected Cut is greyed");
            FfmCtx.Close(h); FfmField.End(false); Pump(h, 20); h.TabSet(1); Pump(h, 20);
        }

        Console.WriteLine("== town ==");
        {
            Yuri.Modules.Town.Tw.N[0] = 0; Yuri.Modules.Town.Tw.Town = 1;
            h.TabSet(7); Pump(h, 40);
            Yuri.Modules.Town.Tw.at = Clock.Tick - 100; Yuri.Modules.Town.Tw.kind = 1; Yuri.Modules.Town.Tw.sx = 50; Yuri.Modules.Town.Tw.sy = 12;
            Pump(h, 5);
            var (sx, sy) = (h.HL.ctx + 1 + 50 * 4 + 2, h.HL.cty + 36 + 12 * 4 + 2);
            Check(h.ZoneAtPublic(sx, sy) == 2225, "the spark is a zone while it is up");
            Yuri.Modules.Town.TownTab.Collect();
            Check(Yuri.Modules.Town.Tw.N[0] == 2 && Ini.ReadInt(Paths.IniFile, "town", "n1", 0) == 2 && Yuri.Modules.Town.Tw.at == 0, "a fresh spark is worth two and is saved");
            Yuri.Modules.Town.Tw.at = Clock.Tick - 9000; Yuri.Modules.Town.TownTab.Collect();
            Check(Yuri.Modules.Town.Tw.N[0] == 3 && Yuri.Modules.Town.Tw.LevelOf(3) == 1 && Yuri.Modules.Town.Tw.lvNew == 1, "an old one is worth one, and level 1 lands at three");
            Yuri.Modules.Town.TownTab.Pick(2); Pump(h, 60);
            Check(Yuri.Modules.Town.Tw.Town == 2 && Ini.ReadInt(Paths.IniFile, "hub", "town", 1) == 2, "a chip switches the town and saves it");
            for (int tk = 1; tk <= 4; tk++) { Yuri.Modules.Town.TownTab.Pick(tk); Pump(h, 30); }
            Check(true, "all four scenes drew without throwing");
            // FLAPPY
            Yuri.Modules.Town.TownTab.Pick(4); Pump(h, 30);
            Yuri.Modules.Town.Fl.coins = 0; Yuri.Modules.Town.Fl.best = 0;
            Check(h.ZoneAtPublic(h.HL.ctx + 200, h.HL.cty + 200) == 2226 && h.ZoneAtPublic(h.HL.ctx + 1 + 70 * 4, h.HL.cty + 36 + 80 * 4) == 2239, "the scene is a tap, the SHOP button its own zone");
            Yuri.Modules.Town.Fl.Tap();
            Check(Yuri.Modules.Town.Fl.on && Yuri.Modules.Town.Fl.vy < 0, "a tap starts the game and flaps");
            Yuri.Modules.Town.Fl.y = 86; Pump(h, 6);
            Check(Yuri.Modules.Town.Fl.dead && Yuri.Modules.Town.Fl.deadAt != 0, "the ground kills");
            Yuri.Modules.Town.Fl.score = 9; Yuri.Modules.Town.Fl.dead = false; Yuri.Modules.Town.Fl.Die(Clock.Tick);
            Check(Yuri.Modules.Town.Fl.coins == 3 && Yuri.Modules.Town.Fl.best == 9 && Ini.ReadInt(Paths.IniFile, "town", "fbbest", 0) == 9, "a death pays a third of the score and keeps the best");
            Yuri.Modules.Town.Fl.Buy(1, 2);
            Check(Yuri.Modules.Town.Fl.bought == -2 && Yuri.Modules.Town.Fl.bird == 1, "too poor for ROBIN: the tile shakes");
            Yuri.Modules.Town.Fl.coins = 20; Yuri.Modules.Town.Fl.Buy(1, 2);
            Check(Yuri.Modules.Town.Fl.coins == 5 && (Yuri.Modules.Town.Fl.birds & 2) != 0 && Yuri.Modules.Town.Fl.bird == 2 && Ini.ReadInt(Paths.IniFile, "flappy", "bird", 1) == 2, "buying ROBIN takes its price, owns it and wears it");
            Check(Yuri.Modules.Town.Fl.Glyphs_Count() == 44 && Yuri.Modules.Town.Fl.TextW("TAP", 2) == 22, "the pixel font");
            Yuri.Modules.Town.Fl.coins = 0; Yuri.Modules.Town.Fl.birds = 1; Yuri.Modules.Town.Fl.bird = 1; Yuri.Modules.Town.Fl.best = 0;
            Ini.Write(Paths.IniFile, "flappy", "coins", 0L); Ini.Write(Paths.IniFile, "flappy", "birds", 1L); Ini.Write(Paths.IniFile, "flappy", "bird", 1L); Ini.Write(Paths.IniFile, "town", "fbbest", 0L);
            Yuri.Modules.Town.Tw.N[0] = 0; Ini.Write(Paths.IniFile, "town", "n1", 0L); Yuri.Modules.Town.TownTab.Pick(1); h.TabSet(1); Pump(h, 30);
        }

        Console.WriteLine("== the tour ==");
        {
            Check(Tut.Steps.Count == 60 && Tut.Steps[0].Ttl == "WELCOME TO YURI" && Tut.Steps[59].Ttl == "THAT'S THE TOUR", "sixty steps, first and last as the .ahk");
            int anchored = Tut.Steps.Count(st => st.Anchor is not null);
            Check(anchored >= 55, "nearly every step lights a control: " + anchored);
            Ini.Write(Paths.IniFile, "hub", "tourseen", 0L);
            Tut.Start(false); Pump(h, 30);
            Check(Tut.On && Tut.Step == 1 && h.Tab == 1, "the tour opens on the welcome step");
            Tut.Next(); Pump(h, 30);
            Check(Tut.Step == 2 && Tut.Zone(h.HL.sbx + 10, h.HL.NavY(2)) == 2204, "step 2 lights the sidebar: a click there counts as NEXT");
            for (int i = 0; i < 12; i++) { Tut.Next(); Pump(h, 12); }
            Check(Tut.Step == 14 && h.Tab == 2, "the tour walks onto INTEGRATIONS");
            Tut.Next(); Pump(h, 20);
            Check(Tut.Step == 15 && h.HubMod == 1, "and opens the FAST FLAG MANAGER for its steps");
            for (int i = 0; i < 45; i++) { Tut.Next(); Pump(h, 6); }
            Check(Tut.Step == 60 && !Tut.Animating || Tut.On, "it reaches the last step");
            Tut.End(); Pump(h, 120, 4);
            Check(!Tut.On && Tut.Seen && h.Tab == 1 && h.HubMod == 0, "FINISH marks the tour seen and puts the hub back");
        }

        Console.WriteLine("== updater ==");
        {
            Upd.NoNetwork = true; Upd.Reset(); Upd.GateAt = 0;
            Upd.ProbeOverride = () => Task.FromResult("2.4.1\n");
            Upd.CheckStart();
            Check(Upd.Phase == 1 && Upd.GateUp && Upd.GateKind == 3, "CHECK FOR UPDATES raises the card in the checking phase");
            Pump(h, 30);
            Check(Upd.Phase == 2 && Upd.Ver == "2.4.1", "a newer version lands the card in phase 2: " + Upd.Phase + " " + Upd.Msg);
            Check(Upd.Zone(HubLayout.pd + HubLayout.cw / 2 - 80, HubLayout.pd + HubLayout.ch / 2 - 4 + 232 / 2 - 34 - 24 + 10) == 2002, "UPDATE sits where the card puts its first button");
            Upd.DownloadOverride = path => { File.WriteAllBytes(path, new byte[] { 1, 2, 3 }); return Task.FromResult(true); };
            Upd.InstallBegin();
            Check(Upd.Phase == 3, "UPDATE starts the download");
            Pump(h, 30);
            Check(Upd.Phase == 5 && Upd.Msg.Contains("not a YURI"), "a stub download is refused and nothing changes: " + Upd.Msg);
            Upd.Cancel(); Pump(h, 40);
            Check(!Upd.GateUp && Upd.Phase == 0, "CANCEL dismisses the card and resets");
            Upd.ProbeOverride = () => Task.FromResult("2.0.0");
            Upd.CheckStart(); Pump(h, 30);
            Check(Upd.Phase == 4, "the same version is 'up to date'");
            Upd.Cancel(); Pump(h, 40);
            Upd.ProbeOverride = () => Task.FromResult("garbage");
            Upd.CheckStart(); Pump(h, 30);
            Check(Upd.Phase == 5 && Upd.Ttl == "COULD NOT CHECK" && Upd.Msg.Contains("no version"), "a bad answer is 'could not check': " + Upd.Msg);
            Upd.Cancel(); Pump(h, 120, 4);
            Check(!Upd.GateUp, "the card is gone before the hub tests continue");
            Upd.ProbeOverride = null; Upd.DownloadOverride = null;
        }

        Check(Directory.Exists(Paths.Cfg), "data folder exists: " + Paths.Root);
        // the rail
        for (int i = 6; i >= 1; i--)
        {
            Click(w, h, HL.sbx + 20, HL.NavY(i) + HL.nvh / 2);
            Check(h.Tab == i, $"nav {i} selects tab {i}");
            Pump(h, 25);                                          // the switch
        }
        Click(w, h, HL.sbx + HL.sbw / 2 + 5, HL.NavY(6) + HL.nvh + 53);
        Check(h.Tab == 7, "the heart opens TOWN");
        Click(w, h, HL.sbx + HL.sbw / 2 + 5, HL.NavY(6) + HL.nvh + 53);
        Check(h.Tab == 1, "the heart again returns to the dashboard");
        Pump(h, 30);
        // every tab renders for a while, dark and light, without throwing
        foreach (int t in new[] { 1, 2, 3, 4, 5, 6, 7 }) { h.TabSet(t); Pump(h, 12, 2); }
        HubState.ThemeSet(1); Pump(h, 40, 2);
        foreach (int t in new[] { 1, 4, 5, 6 }) { h.TabSet(t); Pump(h, 8, 2); }
        HubState.ThemeSet(0); Pump(h, 40, 2);
        Check(HubState.ThT == 0.0, "theme returned to dark");
        // settings
        h.TabSet(4); Pump(h, 30);
        Click(w, h, HL.ctx + 118 + 2 * 40, HL.setr + HL.rh / 2);
        Check(HubState.Accent == HubState.Accents[2] && Ini.ReadArgb(Paths.IniFile, "hub", "accent", 0) == HubState.Accents[2], "accent 3 selected and written");
        double ry2 = HL.setr + (HL.rh + HL.rgap);
        var pA = new Point(HL.sldx + HL.sldw * 0.5, ry2 + HL.rh / 2);
        var pB = new Point(HL.sldx + HL.sldw, ry2 + HL.rh / 2);
        w.MouseMove(pA); Pump(h, 2);
        w.MouseDown(pA, MouseButton.Left); Pump(h, 2);
        Check(HL.drag == 3 && Math.Abs(HubState.BgOpacity - (0.15 + 0.85 * 0.5)) < 0.01, "backdrop slider press sets 57.5%");
        w.MouseMove(pB); Pump(h, 2);
        Check(Math.Abs(HubState.BgOpacity - 1.0) < 0.01, "slider drag follows the pointer");
        w.MouseUp(pB, MouseButton.Left); Pump(h, 3);
        Check(HL.drag == 0 && Ini.ReadNum(Paths.IniFile, "hub", "bg", -1) == 1.0, "slider release saves bg=1");
        double ry4 = HL.setr + 3 * (HL.rh + HL.rgap);
        Click(w, h, HL.ctx + 214 + 42, ry4 + HL.rh / 2);
        Check(HubState.Theme == 1 && Ini.ReadInt(Paths.IniFile, "hub", "theme", 0) == 1, "LIGHT selected and written");
        Pump(h, 60, 2);
        Click(w, h, HL.ctx + 396 + 22, ry4 + HL.rh / 2);
        Check(HubState.ArmTint == 0 && Ini.ReadInt(Paths.IniFile, "hub", "armtint", 1) == 0, "ARMED TINT toggled off");
        Click(w, h, HL.ctx + 3 * (HL.setw + 10) + 60, HL.sett + HL.seth / 2);
        Check(HubState.AutoUpdate && Ini.ReadInt(Paths.IniFile, "hub", "autoupdate", 0) == 1, "AUTO UPDATE tile toggled on");
        Click(w, h, HL.ctx + 60, HL.sett + HL.seth / 2);
        Check(HubState.LowPerf && Ini.ReadInt(Paths.IniFile, "hub", "lowperf", 0) == 1, "PERFORMANCE tile toggled on");
        foreach (int t in new[] { 1, 4, 5, 6 }) { h.TabSet(t); Pump(h, 6, 2); }
        h.TabSet(4); Pump(h, 6, 2);
        Click(w, h, HL.ctx + 60, HL.sett + HL.seth / 2);
        Check(!HubState.LowPerf, "PERFORMANCE tile toggled off");
        double ryR2 = Math.Min(HL.setp + 48, HubLayout.FtrY() - 46);
        Click(w, h, HL.ctx + 14 + 79, ryR2 + 23);
        Check(HubState.Accent == HubState.Accents[0] && HubState.Theme == 0 && HubState.BgOpacity == 1.0 && HubState.ArmTint == 1, "RESET APPEARANCE restores the defaults");
        Pump(h, 40, 2);
        // integrations: the module rail
        h.TabSet(2); Pump(h, 30);
        Click(w, h, HL.mdx + HL.mdw / 2, HL.mdy + HL.m1h / 2);
        Check(h.HubMod == 1, "the FAST FLAG MANAGER card opens module 1");
        Pump(h, 50, 2);
        Check(HL.modT == 1.0, "the panel eases fully open");
        Click(w, h, HL.mdx + HL.mdw / 2, HL.m5y + HL.mdh / 2);
        Check(h.HubMod == 2 && HL.modOut == 1, "the FORSAKEN card opens module 2 and marks 1 as leaving");
        Pump(h, 50, 2);
        Click(w, h, HL.mdx + HL.mdw / 2, HL.m5y + HL.mdh / 2);
        Check(h.HubMod == 0, "the same card again closes it");
        Pump(h, 50, 2);
        Check(HL.modT == 0.0, "and the panel eases shut");
        w.MouseWheel(new Point(HL.mdx + 20, HL.mdy + 40), new Vector(0, -1)); Pump(h, 5);
        Check(HL.mdscrT >= 0 && HL.mdscrT <= Integrations.MdRailMax(HL), "wheel keeps the rail inside its range");
        Pump(h, 20, 2);
        // the fast flag panel
        try { File.Delete(Ffm.File); File.Delete(Ffm.HistFile); } catch { }
        Ffm.Boot(); FfmPanel.Register();
        w.MouseWheel(new Point(HL.mdx + 20, HL.mdy + 40), new Vector(0, 3)); Pump(h, 30, 2);   // the rail back to the top
        Click(w, h, HL.mdx + HL.mdw / 2, HL.mdy - HL.mdscr + HL.m1h / 2);
        Pump(h, 60, 2);
        Check(h.HubMod == 1 && HL.modT == 1.0, "FAST FLAG MANAGER open");
        Click(w, h, HL.abx + 100, HL.ffqy + 12);
        Check(FfmField.Edit == "q", "the filter/add field takes focus");
        w.KeyTextInput("FFlagTestOne"); Pump(h, 3);
        Check(FfmField.Buf == "FFlagTestOne", "typing lands in the field");
        w.KeyPress(Key.Return, RawInputModifiers.None); Pump(h, 5);
        Check(Ffm.Flags.Count == 1 && Ffm.Flags[0].Name == "FFlagTestOne" && Ffm.Flags[0].Value == "true" && FfmField.Buf == "", "Enter stages the name with its default");
        Click(w, h, HL.abx + 10, HL.ffly + 6 + 13);
        Check(!Ffm.Flags[0].On, "the pip switches the flag off");
        Click(w, h, HL.abx + HL.abw - 120, HL.ffly + 6 + 13);
        Check(FfmField.Edit == "v" && FfmField.EditRow == 0, "clicking the value opens it for editing");
        w.KeyPress(Key.A, RawInputModifiers.Control); w.KeyTextInput("false"); w.KeyPress(Key.Return, RawInputModifiers.None); Pump(h, 5);
        Check(FfmField.Edit == "" && Ffm.Flags[0].Value == "false", "select-all, type, Enter replaces the value");
        Click(w, h, HL.abx + HL.abw - 120, HL.ffly + 6 + 13);
        w.KeyPress(Key.A, RawInputModifiers.Control); w.KeyTextInput("maybe"); w.KeyPress(Key.Return, RawInputModifiers.None); Pump(h, 5);
        Check(Ffm.Flags[0].Value == "false" && Ffm.Msg.Contains("EXPECTS TRUE/FALSE"), "a value the prefix refuses is refused with the reason");
        Click(w, h, HL.abx + 100, HL.ffqy + 12); w.KeyTextInput("FFlagTest"); Pump(h, 3);
        Check(Ffm.FlagList("FFlagTest").Count == 1 && Ffm.FlagList("zzz").Count == 0, "the field filters the staged list");
        w.KeyPress(Key.Escape, RawInputModifiers.None); Pump(h, 3);
        Check(FfmField.Edit == "" && Ffm.Q2 == "", "Escape cancels the field");
        Click(w, h, HL.abx + HL.abw - 26, HL.ffly + 6 + 13);
        Check(Ffm.Flags.Count == 0 && Ffm.Hist.Count == 1, "the X deletes the row, after a snapshot");
        for (int i = 0; i < 6; i++) w.MouseWheel(new Point(HL.abx + 50, HL.aby + 50), new Vector(0, -1));
        Pump(h, 40, 2);
        Check(Math.Abs(FfmPanel.SysScr - (FfmPanel.SysTotal(HL) - (HL.ffh - 20))) < 0.5, "the systems column scrolls to its end");
        Click(w, h, HL.abx + HL.abw - 62, HL.aby + FfmPanel.SysRowY(HL, 7) - FfmPanel.SysScr + 18);
        Check(Ffm.Flags.Count == FfmPresets.Hitbox.Length && Ffm.Msg.StartsWith("HITBOX PRESET LOADED"), "INSERT HITBOX loads the preset");
        Pump(h, 10, 2);
        Click(w, h, HL.abx + 3 * 82 + 37, HL.ffby + 13);
        Check(Ffm.Flags.Count == 0 && Ffm.Msg.StartsWith("CLEARED"), "CLEAR empties the list into the history");
        var top = Avalonia.Controls.TopLevel.GetTopLevel(h);
        if (top?.Clipboard is { } cbd)
        {
            cbd.SetTextAsync("{\"FFlagFromClip\": true, \"DFIntClipInt\": 7}").GetAwaiter().GetResult();
            Click(w, h, HL.abx + 82 + 37, HL.ffby + 13);
            Pump(h, 20, 5); Dispatcher.UIThread.RunJobs();
            Check(Ffm.Flags.Count == 2 && Ffm.Msg.StartsWith("IMPORTED 2 FROM CLIPBOARD"), "IMPORT with no dialog reads the clipboard");
            Click(w, h, HL.abx + 2 * 82 + 37, HL.ffby + 13);
            Pump(h, 20, 5); Dispatcher.UIThread.RunJobs();
            var got = cbd.GetTextAsync().GetAwaiter().GetResult() ?? "";
            Check(got.Contains("\"FFlagFromClip\": \"True\"") && Ffm.Msg.StartsWith("COPIED 2 TO CLIPBOARD"), "EXPORT with no dialog copies the json");
        }
        Pump(h, 20, 2);
        // the DATABASE view, on a list set by hand (no network in a test)
        FfmViews.Db.Clear(); FfmViews.Db.AddRange(new[] { "DFIntAlpha", "FFlagBetaOne", "FFlagBetaTwo", "FStringGamma" });
        FfmViews.Loading = false;
        Ffm.ClearAll(); Pump(h, 3);
        Click(w, h, HL.abx + 37, HL.ffby + 13);
        Pump(h, 30, 2);
        Check(FfmViews.View == "db" && FfmField.Edit == "db" && FfmViews.ViewT > 0.5, "DATABASE opens with its field live");
        w.KeyTextInput("Beta"); Pump(h, 3);
        Check(FfmViews.DbList().Count == 2, "the field filters the database");
        w.KeyPress(Key.Return, RawInputModifiers.None); Pump(h, 3);
        Check(Ffm.Flags.Count == 1 && Ffm.Flags[0].Name == "FFlagBetaOne", "Enter stages the first match");
        Click(w, h, HL.ctx + 60, HL.cty + 30 + 74 + HL.ffrh + 12);
        Check(Ffm.Flags.Count == 2 && Ffm.Flags[1].Name == "FFlagBetaTwo", "clicking a row stages it");
        w.KeyPress(Key.Escape, RawInputModifiers.None); Pump(h, 30, 2);
        Check(FfmViews.View == "" && FfmField.Edit == "", "Escape closes the database");
        // the LOG view: HISTORY, RESTORE
        Click(w, h, HL.abx + 4 * 82 + 37, HL.ffby + 13);
        Pump(h, 30, 2);
        Check(FfmViews.View == "log", "LOGS opens");
        Click(w, h, HL.ctx + 22 + 92 * 2 + 46, HL.cty + 30 + 18);
        Pump(h, 30, 2);
        Check(FfmViews.LogTab == 3, "the HISTORY tab selects");
        int before = Ffm.Hist.Count;
        Check(before > 0, "history has snapshots");
        Click(w, h, HL.ctx + HL.ctw - 116 + 37, HL.cty + 30 + 38 + 16);
        Pump(h, 5);
        Check(Ffm.Hist.Count == before + 1 && Ffm.Hist[0].Act == "restore", "RESTORE puts a snapshot back and snapshots the way back");
        Click(w, h, HL.ctx + 14 + 37, HL.cty + 30 + 38 + 16 * 20 + 14 + 12);
        Pump(h, 30, 2);
        Check(FfmViews.View == "", "CLOSE closes the log");
        // the DETAIL sheet
        for (int i = 0; i < 8; i++) w.MouseWheel(new Point(HL.abx + 50, HL.aby + 50), new Vector(0, 1));
        Pump(h, 40, 2);
        // a press that lands outside the surface (a stray synthetic point) must not wedge the input
        w.MouseDown(new Point(100, -60), MouseButton.Left); Pump(h, 2);
        w.MouseUp(new Point(100, -60), MouseButton.Left); Pump(h, 3);
        Click(w, h, HL.abx + 100, HL.aby + FfmPanel.SysRowY(HL, 1) - FfmPanel.SysScr + 18);
        Pump(h, 20, 2);
        Check(Detail.On && Detail.Title == "INJECT LIVE" && Detail.T > 0.9, "a system row opens its explainer (after a stray press outside)");
        Detail.Geom(out double ddx, out double ddy, out double ddw, out double ddh);
        Click(w, h, ddx + ddw - 30, ddy + 28);
        Pump(h, 30, 2);
        Check(!Detail.On && Detail.T == 0.0, "the sheet's X closes it");
        // the COMMUNITY view, on a cache built by hand (no network in a test)
        Yuri.Platform.GameInfo.NoNetwork = true;
        Directory.CreateDirectory(Path.Combine(FfmComm.Root, "general"));
        File.WriteAllText(Path.Combine(FfmComm.Root, "general", "zeal_set.json"), "{ \"FFlagCommOne\": true, \"DFIntCommTwo\": 5 }");
        const string SEP = "\u0001";
        File.WriteAllText(Path.Combine(FfmComm.Root, "index.txt"),
            "C" + SEP + "general" + SEP + "" + SEP + "general\n" +
            "S" + SEP + "general" + SEP + "zeal_set.json" + SEP + "zeal" + SEP + "2" + SEP + "zeal_set.json\n" +
            "S" + SEP + "general" + SEP + "missing.json" + SEP + "nobody" + SEP + "1" + SEP + "missing.json\n" +
            "C" + SEP + "Some Game" + SEP + "1234567" + SEP + "1234567\n" +
            "S" + SEP + "Some Game" + SEP + "fast.json" + SEP + "luna" + SEP + "3" + SEP + "fast.json\n");
        Check(FfmComm.IndexLoad() == 2 && FfmComm.Entries[0].Sets.Count == 2 && FfmComm.Entries[0].Sets[0].Have && !FfmComm.Entries[0].Sets[1].Have, "the cache index reads (the .ahk's H / C / S lines)");
        for (int i = 0; i < 8; i++) w.MouseWheel(new Point(HL.abx + 50, HL.aby + 50), new Vector(0, -1));
        Pump(h, 40, 2);
        Click(w, h, HL.abx + HL.abw - 62, HL.aby + FfmPanel.SysRowY(HL, 9) - FfmPanel.SysScr + 18);
        Pump(h, 40, 2);
        Check(FfmViews.View == "comm" && FfmViews.ViewT > 0.5, "BROWSE opens the community view");
        Check(FfmCommView.ScrMax() > 0, "the body scrolls (banner + two cards overflow)");
        w.MouseMove(new Point(HL.ctx + 100, HL.cty + 30 + 38 + 60)); Pump(h, 2);
        Check(h.ZoneCursor() == 2010, "the banner is the reel's skip zone");
        FfmCommView.ReelOff = (6100 * 8 - Clock.Tick % (6100 * 8)) % (6100 * 8);   // park the reel at the start of a hold, where SKIP acts
        Click(w, h, HL.ctx + 100, HL.cty + 30 + 38 + 60);
        Check(FfmCommView.ReelSkipAt != 0, "clicking the banner skips the reel");
        for (int i = 0; i < 6; i++) w.MouseWheel(new Point(HL.ctx + 100, HL.cty + 30 + 200), new Vector(0, -1));
        Pump(h, 40, 2);
        Check(Math.Abs(FfmCommView.CmScr - FfmCommView.ScrMax()) < 0.5, "the wheel scrolls the body to the end");
        double bodyY = HL.cty + 30 + 38;
        double genTop = bodyY - FfmCommView.CmScr + 236 + 14;
        double insY = genTop + 44 + 44 / 2.0 - 18 + 12;
        double insX = HL.ctx + 14 + 12 + (HL.ctw - 28 - 24 - 10) - 52;
        w.MouseMove(new Point(insX, insY)); Pump(h, 2);
        Check(h.ZoneCursor() == 1931, "the first GENERAL set's INSERT is zone 1931");
        Click(w, h, insX, insY);
        Pump(h, 30, 2);
        Check(Ffm.Flags.Count == 2 && Ffm.Flags[0].Name == "FFlagCommOne" && FfmViews.View == "" && Ffm.Msg.StartsWith("ZEAL / ZEAL_SET LOADED"), "INSERT loads the cached set and closes the view");
        Check(Ffm.Hist[0].Act == "community set", "after a snapshot");
        // SPECIAL FEATURES
        Yuri.Modules.Special.SpfPanel.Register();
        Click(w, h, HL.mdx + HL.mdw / 2, HL.m4y - HL.mdscr + HL.mdh / 2);
        Pump(h, 60, 2);
        Check(h.HubMod == 5 && HL.modT == 1.0, "the SPECIAL card opens module 5");
        var spf = typeof(Yuri.Modules.Special.Spf);
        for (int i = 0; i < 8; i++) w.MouseWheel(new Point(HL.abx + 50, HL.aby + 50), new Vector(0, -1));   // row 11 is below the fold
        Pump(h, 40, 2);
        double r11 = HL.aby + 12 + 10 * 36 - Yuri.Modules.Special.Spf.SysScr;
        Check(Math.Abs(Yuri.Modules.Special.Spf.SysScr - 288) < 0.5 && r11 >= HL.aby + 10 && r11 + 32 <= HL.aby + 128, "the column scrolls row 11 into view");
        Click(w, h, HL.abx + HL.abw - 40, r11 + 16);
        Check(Yuri.Modules.Special.Spf.Clean && Ini.ReadInt(Paths.IniFile, "special", "clean", 0) == 1, "ROBLOX CLEANER switches on and saves");
        Pump(h, 30, 2);
        Click(w, h, HL.abx + 54 + 10, r11 + 16 + 8);
        Check(Yuri.Modules.Special.Spf.CleanAge == 30 && Ini.ReadInt(Paths.IniFile, "special", "cleanage", 0) == 30, "the age chip cycles 7 -> 30 and saves");
        Click(w, h, HL.abx + HL.abw - 40, r11 + 16);
        Check(!Yuri.Modules.Special.Spf.Clean, "and switches off");
        for (int i = 0; i < 2; i++) w.MouseWheel(new Point(HL.abx + 50, HL.aby + 50), new Vector(0, -1));
        Pump(h, 30, 2);
        double r13 = HL.aby + 12 + 12 * 36 - Yuri.Modules.Special.Spf.SysScr;
        Click(w, h, HL.abx + HL.abw - 40, r13 + 16);
        Check(Yuri.Modules.Special.Spf.Theme && Yuri.Modules.Special.Spf.ThemeState == "no app storage yet", "ROBLOX APP THEME on: no app storage on this machine, says so");
        Click(w, h, HL.abx + HL.abw - 40, r13 + 16);
        Click(w, h, HL.abx + 100, r13 + 8);
        Pump(h, 20, 2);
        Check(Detail.On && Detail.Title == "ROBLOX APP THEME", "a row body opens its explainer");
        Detail.Close(); Pump(h, 30, 2);
        Click(w, h, HL.abx + 54 + 40, HL.aby + 188 + 13);
        Check(FfmField.Edit == "gl", "the GAME LINK field takes focus");
        w.KeyTextInput("123456"); w.KeyPress(Key.Return, RawInputModifiers.None); Pump(h, 5);
        Check(FfmField.Edit == "" && Yuri.Modules.Special.Spf.MmLink == "123456" && Ini.Read(Paths.IniFile, "special", "gamelink") == "123456", "Enter keeps the link and saves it");
        h.HubModSel(5); Pump(h, 50, 2);
        h.HubModSel(1); Pump(h, 50, 2);
        // credits
        h.TabSet(5); Pump(h, 30);
        Click(w, h, HL.ctx + HL.ctw - 37, HL.lky - 19);
        Check(Credits.Who == 2, "FIND ME switches to LUNARIS");
        Pump(h, 20);
        // update logs
        h.TabSet(6); Pump(h, 30);
        w.MouseWheel(new Point(HL.ctx + 50, HL.cty + 200), new Vector(0, -1)); Pump(h, 5);
        Check(HL.lgScT >= 0 && HL.lgScT <= HL.lgMax, "wheel keeps the log scroll inside its range");
        // dashboard
        h.TabSet(1); Pump(h, 30);
        Click(w, h, HL.ctx + (HL.ctw - 192) - 22 - HL.zvw - HL.ubw / 2, HL.ly + 20);
        Check(h.ClickAt.ContainsKey(2001) && Upd.GateUp && Upd.Phase >= 1, "CHECK FOR UPDATES raises the update card");
        Pump(h, 30); Upd.Cancel(); Pump(h, 120, 4);
        Check(!Upd.GateUp, "and CANCEL takes it away again");
        Pump(h, 30, 2);
        // the grip: a press must not leave the hub in a drag state
        w.MouseDown(new Point(HL.brx + 86, HubLayout.pd + 22), MouseButton.Left); Pump(h, 2);
        w.MouseUp(new Point(HL.brx + 86, HubLayout.pd + 22), MouseButton.Left); Pump(h, 3);
        Check(HL.drag == 0 && !h.Pressed, "grip press leaves no drag state behind");
        // minimise and back
        Click(w, h, HL.bmx, HL.bty);
        Check(HL.minStart != 0 && HL.minTo == 1.0, "minimise starts the fold");
        Pump(h, 60, 4);
        Check(HL.minT == 1.0, "the fold finishes");
        // ---- the minimised pill ----
        Check(h.ZoneAtPublic(HL.mbx, HL.mby) == 9 && h.ZoneAtPublic(HL.mbx - 60, HL.mby) == 0, "collapsed, the pill is the only zone and it is the pill's own rect");
        Check(HL.burstAt != 0 || HL.minV >= 1.0, "the collapse armed the landing burst");
        {
            // it actually draws: the orb's rect is no longer background
            var bmp = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)(h.Bw * h.K), (int)(h.Bh * h.K)), new Vector(96, 96));
            bmp.Render(h);
            using var ms = new MemoryStream(); bmp.Save(ms); ms.Position = 0;
            var img = new Avalonia.Media.Imaging.Bitmap(ms);
            Check(img.PixelSize.Width > 0, "the collapsed hub renders a frame");
            bmp.Dispose();
        }
        Check(MiniPill.MBW == 74 && MiniPill.MBH == 96, "the pill is the .ahk's 74 x 96");
        {
            // TRUE MINIMISE hides the window and the tray is the only way back,
            // so with no tray on this desktop the button must still fold.
            bool wasTm = HubState.TrueMin; HubState.TrueMin = true;
            h.HubMin(0); Pump(h, 60, 4);
            h.HubMinPress(); Pump(h, 4);
            Check(Yuri.Shell.Tray.Available ? h.Hidden : (!h.Hidden && HL.minTo == 1.0), "TRUE MINIMIZE hides only when there is a tray to come back through");
            if (h.Hidden) h.HubShow();
            HubState.TrueMin = wasTm;
            h.HubMin(1); Pump(h, 60, 4);
        }
        h.HubMin(0); Pump(h, 60, 4);
        Check(HL.minT == 0.0, "and unfolds");
        Check(HL.scatAt != 0, "the expand armed the scatter ring");
        // close
        Click(w, h, HL.bcx, HL.bty);
        Check(HL.closeAt != 0, "close starts the fade");
        Pump(h, 60, 5);
        Dispatcher.UIThread.RunJobs();
        Check(exited && HubSurface.Live is null, "the fade ends the process");
        Console.WriteLine(_fails == 0 ? "ALL PASSED" : $"{_fails} FAILED");
        return _fails == 0 ? 0 : 1;
    }
}
