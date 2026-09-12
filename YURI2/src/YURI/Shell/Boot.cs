using Avalonia.Controls.ApplicationLifetimes;
using Yuri.Shell.Hub;
using Yuri.Shell.Loading;

namespace Yuri.Shell;

/// <summary>
/// The launch sequence: loading splash -> launch gate -> hub. The gate comes
/// with the gallery it draws its hero pictures from (phase 5); until then the
/// splash hands straight to the hub, as OPEN HUB does.
///
/// Boot work: the .ahk's loader counted the saved-places prefetch, the artwork
/// warm, the flag database and the community sync as pending items and drove
/// the bar's ceiling from them. Modules register the same fetches here as
/// Tasks when they boot; the bar reads Left()/Total.
/// </summary>
public static class Boot
{
    public static IClassicDesktopStyleApplicationLifetime? Desktop;
    /// <summary>What ExitApp does; a host without a desktop lifetime (the test harness) replaces it.</summary>
    public static Action Exiting = () => Desktop?.Shutdown();
    public static string GateChoice = "";                    // gateChoice: "roblox" / "hub" / "acct"

    // filled by the modules that own them (SPF, the gallery) when they land
    public static int SavedPlaces;                            // SPF.saved.Length + SPF.savedP.Length
    public static int SavedAccounts;                          // SPF.acct.Length
    public static int AvatarPoolN;                            // selN

    static readonly List<Task> Work = new();
    public static void AddWork(Task t) => Work.Add(t);
    /// <summary>
    /// Work that has no Task of its own: fire it, then hold the loader until the
    /// store it fills says it is done, or until the cap. The loader already
    /// waits on Boot.Left(); the pieces below simply never told it they were
    /// running, so it counted two jobs and opened while the rest was still in
    /// the air - which is why saved places came up as bare ids and accounts as
    /// blanks the first time they were opened.
    /// </summary>
    public static void AddWait(Action start, Func<bool> done, int capMs)
    {
        try { start(); } catch { return; }
        AddWork(Task.Run(async () =>
        {
            long t0 = Core.Clock.Tick;
            while (Core.Clock.Tick - t0 < capMs)
            {
                bool ok; try { ok = done(); } catch { ok = true; }
                if (ok) return;
                await Task.Delay(120);
            }
        }));
    }
    public static int Total => Work.Count;
    public static int Left() { int n = 0; foreach (var t in Work) if (!t.IsCompleted) n++; return n; }

    public static void Run(IClassicDesktopStyleApplicationLifetime desktop)
    {
        Desktop = desktop;
        Hub.Upd.SweepOld();                                     // the build the last update moved aside
        Gfx.Pool.Load(); Gfx.Pool.ProfPicLoad();                // the selfie pool and the profile picture ([profile] pic)
        AvatarPoolN = Gfx.Pool.N;                              // selN: the loader's third caption counts it, and it read 0
        Core.Usage.Start();                                   // UsageLoad() + the one-second tick
        Modules.FastFlags.Ffm.Boot();                          // FFMBoot(): the staged list and its history
        Modules.FastFlags.FfmEngine.Boot();                    // the PROCESS MEMORY ENGINE's poll (Windows)
        Modules.FastFlags.FfmPanel.Register();                 // module 1's SYSTEMS panel and its [ffm] settings
        AddWork(Modules.FastFlags.FfmViews.FetchDb());          // the flag database, counted by the loader like FFM.loading
        Modules.Special.SpfPanel.Register();                    // module 5: SPECIAL FEATURES
        Modules.Special.SpfEngine.Register();                   // its log tail, region lookup, Discord activity and matchmaker
        Modules.LoginItems.LgiPanel.Register();                 // module 7: LOGIN ITEMS
        Modules.DeviceOpt.DopPanel.Register();                  // module 6: DEVICE OPTIMIZATIONS
        Modules.Cursor.CurPanel.Register();                     // module 3: CURSOR
        Modules.ClientSettings.RSetPanel.Register();            // module 4: CLIENT SETTINGS
        Modules.Forsaken.FskPanel.Register();                   // module 2: FORSAKEN
        Hub.Gallery.Register();                                 // the pool's counts, which SETTINGS and UPDATE LOGS read
        Modules.ScriptHub.Scr.Hooks(); Modules.ScriptHub.Scr.LoadListOnly();   // the placed / running counts the dashboard reads
        Modules.FastFlags.FfmComm.IndexLoad();                   // FFMCommIndexLoad(): the cached community sets
        var sync = Modules.FastFlags.FfmComm.Sync();             // FFMCommSync(): checked at launch, counted by the loader
        AddWork(sync);
        Prefetch();
        // The community view names each game-specific set by its place and
        // shows its art, and both come from the games API - which was only asked
        // when the view was opened. Chained after the sync, since the list of
        // places IS the sync's result.
        if (!Modules.FastFlags.FfmComm.NoNetwork)
            AddWork(sync.ContinueWith(_ =>
            {
                var places = new List<string>();
                foreach (var e in Modules.FastFlags.FfmComm.Entries) if (e.Place != "") places.Add(e.Place);
                if (places.Count == 0) return Task.CompletedTask;
                foreach (var id in places) Platform.GameInfo.Fetch(id);
                long t0 = Core.Clock.Tick;
                return Task.Run(async () =>
                {
                    while (Core.Clock.Tick - t0 < 9000)
                    {
                        bool all = true;
                        foreach (var id in places) if (Platform.GameInfo.Get(id) is not { At: not 0 }) { all = false; break; }
                        if (all) return;
                        await Task.Delay(120);
                    }
                });
            }, TaskScheduler.FromCurrentSynchronizationContext()).Unwrap());
        LoadingScreen.Show(AfterLoading);
    }

    /// <summary>
    /// The lists are read off disk by the modules' own Register, but what makes
    /// them READABLE - a place's name and thumbnail, an account's username and
    /// avatar - is fetched over the network, and none of it was asked for until
    /// the view was opened. Asked for here instead, so the hub is furnished when
    /// it arrives rather than filling in while being looked at.
    /// </summary>
    static void Prefetch()
    {
        if (Modules.FastFlags.FfmComm.NoNetwork) return;
        // saved places: every one, not just the selected
        var places = new List<string>();
        foreach (var sv in Modules.Special.SpfSaved.Saved) places.Add(sv.Id);
        if (places.Count > 0)
            AddWait(() => { foreach (var id in places) Platform.GameInfo.Fetch(id); },
                    () => { foreach (var id in places) if (Platform.GameInfo.Get(id) is not { At: not 0 }) return false; return true; },
                    9000);
        // saved accounts: the username and the profile behind it
        int an = Modules.Special.SpfAcct.Acct.Count;
        if (an > 0)
            AddWait(() => { for (int i = 1; i <= an; i++) { Modules.Special.SpfAcct.NameFetch(i); Modules.Special.SpfAcct.ProfFetch(i); } },
                    () => Modules.Special.SpfAcct.ResolvedN() >= an,
                    9000);
    }

    static void AfterLoading()
    {
        // LaunchGate(): the card decides what opens; closing it ends the app
        Gate.GateWindow.Show(choice =>
        {
            if (choice == "") { Exit(); return; }
            GateChoice = choice;
            HubWindow.Open();
            Hub.Upd.GateKind = 1; Hub.Upd.GateAt = Core.Clock.Tick; Hub.Upd.GateOut = 0;   // the opening gate: BEFORE YOU START, as every hub open in the .ahk
            if (Gfx.HubState.AutoUpdate) Hub.Upd.AutoStart();       // UpdAutoStart(): the launch check, silent unless it finds something
            if (choice == "roblox") Platform.Roblox.Launch();                     // OPEN ROBLOX: the client too, the way the dashboard's LAUNCH does it
            else if (choice == "acct" && HubSurface.Live is { } hub)
            {
                // ACCOUNTS: the hub, opened straight onto the account manager - the INTEGRATIONS tab, the saved-places module (its accounts view lands with that module)
                hub.TabSet(2);
                if (hub.HubMod != 5) hub.HubModSel(5);
                Modules.Special.Spf.ViewOpen("acct");
            }
        });
    }

    /// <summary>ExitApp.</summary>
    public static void Exit()
    {
        try { Modules.Special.SpfFps.Exiting(); Modules.Special.SpfWin.Exiting(); Modules.ScriptHub.Scr.Exiting(); Modules.Forsaken.Ab.Exiting(); } catch { }
        if (Hub.Upd.RestartCmd != "") { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Hub.Upd.RestartCmd) { UseShellExecute = true }); } catch { } }   // the updater's restart
        Exiting();
    }
}
