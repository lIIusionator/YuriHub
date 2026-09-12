# YURI.ahk -> YURI 2.0 (Avalonia, .NET 8): the port plan

The source is YURI.ahk v1.0.3: 62,176 lines, 45,935 of them code (14,799
comment lines, 240 KB of embedded base64 images). This file is the manifest
the port works from: each section of the .ahk, where it lands in this
tree, how big it is, and whether it can exist on macOS at all.

## How the port is done

The .ahk draws every pixel itself through ~30 GDI+ helpers (SBrush, VBrush,
Pen, FillRR, StrokeRR, FillEll, Arc, Line, Txt, PushXform, Pop, the clip).
`Gfx/G.cs` is those helpers, same names, same argument order, same colour
maths (`Gfx/Col.cs` is bit-exact with the .ahk's Alpha/FA/Mix/AccHi/TH,
including AutoHotkey's rounding), over Avalonia's DrawingContext. A render
function therefore ports line for line:

    b := SBrush(FA(Alpha(acc, 26*a), f))          FillRR(x, y, w, h, 8, SBrush(FA(Alpha(acc, 26*a), f)));
    FillRR(x, y, w, h, 8, b), DelB(b)

`Gfx/Surface.cs` is the layered window: logical bw x bh at scale k, the
.ahk's timer tiers (TICK_A/S/Z/BG/LP), its frame pacing, the pointer
resolved to a zone id by a ZoneAt table exactly like HubZone, and the
per-zone hover ease (`HL.h`). `Gfx/Tex.cs` is the backdrops, shadow and
shared chips. Module state objects (FFM, SPF, RSET, DOP, SH ...) become
static classes with the same field names.

What is deliberately NOT ported, because Skia makes it unnecessary: the
BACKDROP CACHE, HATCH LAYER, PLATE CACHE, TEXT RASTER CACHE, FITTED-IMAGE
CACHE and the tab-switch layers (~1,300 lines). Textures draw live. LEAN and
LOW PERFORMANCE MODE keep their meaning (skip textures / flat surfaces).

Child processes (`if A_Args[1] = "ahkfile" / "ffdb" / "ffupd" / update
probe / account lookup ...`) become Tasks with HttpClient; one process.

## Platform verdicts

| Capability | Windows | macOS |
|---|---|---|
| Hub, tabs, fast flags, community sync, client settings, cursor, login items, script hub, profile, tour, town/minigames | yes | yes |
| Special features rows that read Roblox's log / write ClientSettings | yes | yes (Roblox's macOS paths) |
| Registry-backed rows (device optimizations, machine-wide settings, priority, desktop app, app theme) | yes | off, row says why |
| PROCESS MEMORY ENGINE (MemIO / FlagSingleton: RPM/WPM pattern scanning of the client) | Modules/FastFlags/FfmEngine.cs | done: MemIO, FlagSingleton (signature + structural scans, plausibility, per-pid caches), FFlags (hash walk, typed writes, in-place strings), the poll, attach / instances / tabs, INJECT (two passes), UNINJECT (+ ALL), the re-apply watchdog, CLOSE RBX, CLEAN; Windows-only, verified on the pure parts here — the live path needs a client |
| Autoblock overlay + PUZZLE AI (screen capture, synthetic input, transparent overlay windows) | yes | impossible as written |
| Global hotkeys | RegisterHotKey | Carbon RegisterEventHotKey (later) |
| Tray icon | Avalonia TrayIcon | Avalonia TrayIcon |

## Section map

Line ranges are the .ahk's; "code" is non-comment, non-blank lines.

| Phase | .ahk section | lines | code | target | status |
|---|---|---|---|---|---|
| 0 | child-process entry points (pickers, ffdb, ffupd, update probe/download, account lookup, geo, cookie detect) | 1-2085 | 1,518 | Tasks inside their modules | with each module |
| 0 | state, GDI+, DATA LAYOUT, keys, self-update | 2085-3379 | 786 | Core/*, Gfx/HubState.cs, Core/Updater.cs | done except Updater |
| 0 | helpers (theme, colour, easing, brushes, primitives) | 54785-56649 | 1,268 | Gfx/Col.cs, Gfx/G.cs, Gfx/Tex.cs, Gfx/Fonts.cs | done |
| 0 | loading screen | 37547-38014 | 333 | Shell/Loading/LoadingScreen.cs | done, verified by snapshot |
| 1 | main hub: frame, chrome, header, rail, heart, footer, minimise, drag | 38014-42640 (part) | ~700 | Shell/Hub/HubWindow.cs | done, verified by snapshot |
| 1 | shared widgets: FFMBtn, FFMTogDraw, FFMElide, SetHead, SetTileIcon, LAUNCH / TUTORIAL pills, CafeDraw, Doodle, MiniBackdrop | scattered | ~500 | Shell/Hub/HubUI.cs, Gfx/Tex.cs | done |
| 1 | dashboard tab + screen time (usage) | 43620-43900, 46437-46520 | ~300 | Shell/Hub/Tabs/Dashboard.cs, Core/Usage.cs | done, verified by snapshot |
| 1 | settings tab + its setters, zones, sliders | 45326-45702, 41620-41725 | ~450 | Shell/Hub/Tabs/SettingsTab.cs | done, verified by snapshot |
| 1 | credits tab | 45702-45943 | ~240 | Shell/Hub/Tabs/Credits.cs | done, verified by snapshot (avatars with the gallery) |
| 1 | update logs tab | 45951-46138 | ~190 | Shell/Hub/Tabs/UpdateLogs.cs | done, verified by snapshot (gallery panel with the gallery) |
| 1 | INTEGRATIONS tab: the module rail (FFMCards), HubModSel, the SYSTEMS panel frame, rail scroll | 11772-12184, 43880-43935, 41232 | ~480 | Shell/Hub/Tabs/Integrations.cs | done, verified by snapshot + tests |
| 2 | FAST FLAG MANAGER data core: staged list, zeal_flags.json / zeal_hist.json, add / delete / toggle / import / export / history, prefix typing, delivery folders | 3379-3540, 6428-6590, 8817-9100, 32045-32090 | ~450 | Modules/FastFlags/Ffm.cs | done, tested |
| 1 | HubZone / HubClick / HubWheel for the ported tabs | 39545-41746 (part) | ~200 | inside each tab file | done; the rest lands with each module |
| 1 | profile plate, EDIT PROFILE, bio, fonts, avatar; the update card (gate card kind 3) + Updater | 42640-43013, 38300-38700 | ~1,100 | Shell/Hub/Profile.cs, Core/Updater.cs | done (see the rows below: the plate, EDIT PROFILE and the bio; the updater and the gate card) |
| 2 | FAST FLAG MANAGER: the SYSTEMS panel (FFMSystems, FFMStatus, FFMSysIcon, FFMSysDivider, FFMTypePill), the filter/add field and value editor (FFMBeginEdit/EndEdit/Key/Char/FldMouse/FldPaint), FFMZone/FFMClick for the panel, IMPORT/EXPORT through the platform's file dialogs with the clipboard fallback, the two presets, CLOSE RBX | 10265-10835, 11038-11226, 12317-12800, 5715-5780 | ~1,100 | Modules/FastFlags/FfmPanel.cs, FfmField.cs, FfmPresets.cs | done, verified by snapshot + tests |
| 2 | FAST FLAG MANAGER: the DATABASE view (the tracker fetch — three sources, deduplicated, sorted — FFMDbView/FFMDbZone, the "db" field, Enter/click staging), the LOG view (INJECTION / UPDATES / HISTORY with RESTORE and CLEAR, the tab slide), the DETAIL sheet (DetailOpen/Draw/Zone/scroll, WrapText, FFMDetail's nine explainers), FFMOpenView/CloseView | 80-160, 9410-9476, 9951-10265, 13590-13704, 17825-18213, 19054-19122 | ~900 | Modules/FastFlags/FfmViews.cs | done, verified by snapshot + tests |
| 2 | FAST FLAG MANAGER: the UPDATE pass (the live-list fetch, the token index, weighted Dice, RENAMED / REVIEW / SKIPPED / PREFIX verdicts, UNDO, PREFIX normalisation) | 9476-9951, the ffupdate child | ~330 | Modules/FastFlags/FfmUpdate.cs | done, tested on a hand-built live list |
| 2 | COMMUNITY sets: the cache (index.txt), the repository sync (HEAD atom feed, git tree, per-set fetch with blob-SHA change detection, rate-limit fallback), INSERT | 571-905 (the commsync child), 5804-6000, 9319-9360 | ~380 | Modules/FastFlags/FfmComm.cs | done, cache and INSERT tested offline |
| 2 | FAST FLAG MANAGER: the COMMUNITY view (FFMCommView: the eight-piece banner reel with SKIP, GENERAL and game cards — picture reel, icon, name, PLACE chip, playing/visits/likes/dislikes, like bar, scrolling description — set rows with INSERT, REFRESH, three kinds of scrollbar) | 5478-5700, 12799-13590, 10930-11013 | ~700 | Modules/FastFlags/FfmCommView.cs | done, verified by snapshot + tests |
| 2 | the embedded art: the eight community banners and Lunaris's avatar, extracted from the .ahk's base64 into Assets/ (avares) | 3596-5455 | — | Assets/*.jpg, Gfx/Img.cs | done |
| 3 | GameInfo: the gameinfo child (universe, name, description, playing, visits, favourites, votes, ten thumbnails, the icon), cached twelve hours on disk, fetched in the background — what SPF.stats / SPFThumbN / SPFThumbAt / SPFIco read | 975-1100 | ~160 | Platform/GameInfo.cs | done |
| 2 | FAST FLAG MANAGER: the context menu (FFMCtx*), FFMInstTabs (multi-client, with the engine) | 10293-10370, 12184-12268 | ~150 | Modules/FastFlags/* | context menu next; tabs with phase 8 |
| 3 | SPECIAL FEATURES: the state ([special] keys), SPFSave/SPFSay, the seventeen switches with their messages (SPFToggle), the panel (SPFSystems, SPFIcon, SPFSubRow, the chips, the EXPERIMENTAL badge, ACCOUNTS / SAVED PLACES, the two link fields with JOIN, the LIVE card), SPFZone/SPFClick, the seventeen explainers; the CLEANER, LAUNCH ON STARTUP (Run key / LaunchAgent) and APP THEME back-ends | 20366-20993, 25611-25750, 27750-27967, 28115-28930, 29027-29300, 30680-30811, 19126-19526 | ~1,300 | Modules/Special/Spf.cs, SpfPanel.cs | done, verified by snapshot + tests |
| 3 | SPECIAL FEATURES engine: the log tail (SPFLogRead / LogOwners / Parse / PickSession / Tick / Sync — sessions per log, the focused client's log first, join / leave / UDMUX / server prefix / type), the region lookup (three geo services, cache, back-off, the whole-log rescan), the home lookup and distances, the game-name and user lookups, SERVER DETAILS (history, uptime from the Server Prefix, REJOIN LAST / COPY LINK / COPY ID, the RIGHT NOW paragraph), NO DESKTOP APP, the client watch | 21073-22100, 25485-25610, 27048-27170, 27558-27750 | ~700 | Modules/Special/SpfEngine.cs | done, tested on a synthetic log with an injected client |
| 3 | DISCORD ACTIVITY: the IPC link (named pipe / unix socket), handshake, READY / ERROR / CLOSE, the presence (game, region, distance, creator, account, join link, focus, instances), sent only on change | 27204-27560 | ~330 | Modules/Special/SpfDiscord.cs | done; the pipe is tested absent, the presence needs a live Discord |
| 3 | AUTO-REGION FINDER / BETTER MATCHMAKING: the target from the link fields, the public server list, the pick by measured distance, the reroll, the hunt, JOIN | 23055-23412 | ~330 | Modules/Special/SpfMatch.cs | done |
| 3 | SAVED PLACES: the two lists in zeal.ini (sv1..8 / pv1..8), add / remove / copy / use / rename, the view (tabs, rows with icons and player counts, the banner reel with SKIP and pips, name / ID / LIVE, the scrolling description, the four animated stat cards), the ADD and RENAME sheets on the shared field editor, zones 990-1016 | 22604-22990, 29779-30680 | ~1,100 | Modules/Special/SpfSaved.cs | done, tested |
| 3 | ACCOUNTS manager: the DPAPI-sealed cookie store (accounts.dat), add-from-clipboard, remove, the whoami / profile / groups / created / avatar fetches over HttpClient, launch-as (CSRF, authentication ticket, the roblox-player protocol), the view (list with avatars and presence, the ABOUT / GROUPS / CREATED tabs, the launch box) | 22101-22169, 26075-27048, 29187-29779 | ~1,500 | Modules/Special/SpfAcct.cs, SpfAcctView.cs | done, tested; the browser-login CDP scan is the manual-cookie path with a note |
| 3 | The content mods: old sounds, avatar background, death sound picker, emoji font — fetched from Bloxstrap's resources, written with .yuri-orig backups into every install and the straps' Modifications, reverted, re-applied after an update | 25750-26040 | ~300 | Modules/Special/SpfMods.cs | done, tested on a fake install |
| 3 | Windows-only rows: MULTI-ROBLOX INSTANCES (the singleton mutex + event, the RESTART ROBLOX count), DISABLE CRASH HANDLER (the sweep on the engine's poll), MEMORY TRIMMER (EmptyWorkingSet on a schedule with a size limit, the chips, the readings), FPS BOOST's six per-client levers + probe + power reading + timer resolution + report / hint / held | 21223-21248, 23412-23440, 23499-23660, 24068-24330, 24906-25330, 25333-25485, 26039-26075 | ~900 | Modules/Special/SpfWin.cs, SpfFps.cs | done; the pure parts tested, the levers need a client |
| 3 | FPS BOOST's three opt-in chips: GPU PREF (the per-app graphics preference in the registry, two-GPU machines only, never over a preference Windows already has), QUIET (the fixed background list demoted while the client has focus, restored on the way off), SYSTEM (Ultimate / High power plan unless the current plan is already unrestricted by its minimum processor state, Game DVR and background capture off, Game Mode on, the two HKLM values under admin, every value journaled to fpsrestore.ini before it is written, recovered at launch) | 23673-23870, 23861-24068, 24383-24900 | ~1,000 | Modules/Special/SpfFpsChips.cs | done; Windows-only, exercised for its off-platform path here |
| 4 | CLIENT SETTINGS (module 4): the 32 rows (RSetOptions), presets, the raw flag layer, FLAG SOURCE, locks / overrides / the gfx-maxq link, GlobalBasicSettings_*.xml editing with the one backup and the owned-value journal, ClientAppSettings.json delivery with the prev journal, APPLY / REVERT (the pending xml revert while Roblox runs), boot, the panel (rows in four groups, dropdowns, sliders, preset chips, the source switch, the STATE card), the explainers | 30811-33665, 34886-35038, 35934-36735, 19530-19744 | ~3,000 | Modules/ClientSettings/RSet.cs, RSetPanel.cs, RSetDetail.cs | done, tested on a fake install |
| 4 | CLIENT SETTINGS: the EDIT FAST FLAGS page - the name / value fields and ADD, the written list (extras editable in place, preset pairs that take their row over when edited, removed rows sliding out), the whitelist database body with its filter and pick, the four field modes (rfn / rfv / rfe / rfd) on the shared field, zones 1600-1999, the two scrollbars | 33197-33292, 33472-33575, 35038-35934 | ~900 | Modules/ClientSettings/RSetFx.cs | done |
| 4 | CLIENT SETTINGS: ROBLOX FONT (every install's content\\fonts ttf/otf + Bloxstrap's mirror, backed up first), ROBLOX LOGO / STUDIO LOGO (every logo*.png under content\\textures or the one file the user points at, kept relative so it survives updates; the shortcut icons via a 256px PNG-in-ICO and each .lnk's IconLocation with the previous one recorded), RESTORE LOGOS, backups.dat / icons.dat, the orphan-backup sweep, the previews, the pickers, the live fps cap into the attached client with what it held before kept for REVERT | 33665-34545, 31420-31650 | ~1,100 | Modules/ClientSettings/RSetLogo.cs | done; CLIENT SETTINGS complete |
| - | UI fixes from testing: every scrollbar drags from its press (FFMScrollFromY / ScrGrip ported), the field selection and caret sit on the glyphs and a click lands on the character under it, fields take the caret on the press (FFMKeepEdit ported for the click-blur) | 11226-11238, 40105-40130, 10602-10690 | | Shell/Hub/HubUI.cs, Modules/FastFlags/FfmField.cs | done |
| 5 | SCRIPT HUB (tab 3): the line editor with caret / anchor / selection, follow-scroll, the two scrollbars, the tokenizer, the keys (ctrl A/C/X/V, shift selection, enter with indent, tab), paste, the eight chips, PLACE / UPDATE, the warning strip, four tabs with IN MAIN, the name / description fields, CHECK through `/validate` with the version guessed from the source, SAVE / EXPORT / the saved library, the placed cards (sigil, EDIT, ENABLE / STOP under the AutoHotkey found, delete with its collapse), the inline card edit page | 39870-39990, 40400-40570, 44563-45326, 46138-47717, 52310 | ~2,400 | Modules/ScriptHub/Scr.cs, ScrTab.cs | done; card pictures wait for the gallery |
| 5 | launch gate (hero rotation, OPEN ROBLOX / OPEN HUB / ACCOUNTS) | 36843-36884, 47717-48799 | 910 | Shell/Gate/GateWindow.cs | done; the rotation is managed from the gallery |
| - | the GALLERY (the tile grid, hover, the selected tile's ring, BROWSE FILES / ADD IMAGES, click to use / remove, RESET GALLERY, manage mode for the gate rotation) with the cropper (pan by drag, zoom by wheel, USE / CANCEL, profile_crop.png), the pool's hidden set / reload / remove / reset / add, the profile picture with its fade ([profile] pic), the sidebar profile plate (portrait with CHANGE, ring styles, name, OPERATOR, the pen), EDIT PROFILE (RING, NAME, BIO, HUB FONT, DONE), the script cards' pictures | 49622-50502, 52571-52728, 54726-54800, 42590-42700, 16400 area (the modal) | ~1,900 | Shell/Hub/Gallery.cs, ProfilePlate.cs, EditProfile.cs, Gfx/Pool.cs | done; the gallery rides the hub as a modal rather than floating as its own window |
| 2 | FORSAKEN (module 2), part 1: EXTERNAL BLOCK REBIND - the low-level keyboard / mouse hooks (the bind key arms and disarms in game; while armed and Roblox is in front the left button is swallowed and held as Q), the three bindable keys under [keys] with the .ahk's AutoHotkey names and labels, rebinding by the next key or side / middle button with ESC cancelling and clashes refused, the panel with both cards (switches, key rows with clear crosses, PUZZLE AI's grid chips, SET GRID / RESET, status line, session pill, speed slider), the KEYBIND CONFLICT card | 2333-2345, 36800-37062, 37287-37340, 43870-44520, 50786-50960 | ~1,100 | Modules/Forsaken/WinHooks.cs, Keys.cs, Ab.cs, Puz.cs, FskPanel.cs | done; Windows-only hooks |
| 2 | FORSAKEN, part 2: PUZZLE AI's solver (capture, colour pairing, the DFS with rerouting, the glide along the paths), SET GRID's calibration overlay, the grid markers, the status overlay card, the debug window; the block's shield overlay window | 56680-62050, 50960-52779 | ~7,000 | Modules/Forsaken/PuzSolver.cs, PuzOverlay.cs | done for the solver, the screen and the session (the row above); the calibration reticle, the grid markers, the overlay card and the debug window are not ported - SET GRID is two clicks, the markers key reports the grid in the hub |
| 2 | FORSAKEN's PUZZLE AI solver: the dot scan (off-centre sample, brightness threshold), the pairing (exact colour key, key to within 8 per channel, then mutual-nearest with the loose-match count, sorted by distance), the board search with the .ahk's ordering, self-touch and end-alive rules, the reachability BFS and the iteration / depth / time stops, the two-round L-route optimiser; the screen (the reference-table board geometry interpolated to the screen, the DIB capture, SendInput moves and presses with the .ahk's frame budgets, pitch scale, adaptive pace, start settle with the jiggle, press hold, end hold), the session loop on a worker thread while the solve key is held and Roblox is in front (scan → deal check → pair → solve → optimise → draw and verify each line → wait for the next deal), [puzzle] solved / gridN; SET GRID by two clicks through the mouse hook; the markers key reports the grid | 56680-62050 | ~1,000 of the 5,000 (the algorithm and the input; not the overlay, markers, calibration reticle or debug windows) | Modules/Forsaken/PuzSolver.cs, PuzRun.cs, Puz.cs | done; Windows-only, the solver tested off Windows on a synthetic board |
| 7 | TOWN (tab 7, behind the heart): NIGHTFALL, MAIN STREET and GREENVALE transliterated from the .ahk's pixel drawing onto the 142 x 101 grid (the two-minute day, the rain, the real-time clock tower, the five rewards a town), the spark game (warm sparks at the twelve spots, the comet worth three, the burst, the +n, the level ribbon, the progress card), the four chips with the slide; FLAPPY - the physics, the pipes closing and speeding with the score, the coins in the gaps, the trail for GOLD and NEON, the ground, the hud, the idle card, GAME OVER with the medal, the SHOP with six birds and five worlds, the 3x5 pixel font; best / coins / birds / worlds / bird / world in zeal.ini | 52782-54725 | ~1,950 | Modules/Town/Tw.cs, TownScene.cs, TownTab.cs, Flappy.cs | done |
| - | second audit, against the user's screenshots: the two FORSAKEN overlays were my own design, not the .ahk's - rebuilt as Render / DrawCard and PuzOvDrawCard (brand + dot, grip pill, minimise AND close, the portrait with its ring / CHANGE / orbiting arcs, the mouse feeding chevrons into the Q keycap, the ENABLED / DISABLED pill, the puzzles-solved pill, the speed slider with its % and fps, the footer keybind rows and the shield), plus the minimise-to-bubble that was missing entirely (the fold with its lag ghosts and 8-degree rotation, the bubble with OPEN on hover, zone 3 to restore) and the drag tilt from window velocity; the profile name and bio now use the .ahk's fP / fPs (the chosen HUB FONT at 10.5 bold / 8.5 regular) instead of fStatus / fBadge | 50960-51560, 57916-58275, 46160-46170 | ~700 | Modules/Forsaken/Overlays.cs, Gfx/Fonts.cs, Shell/Hub/ProfilePlate.cs | done |
| - | audit against the re-uploaded YURI.ahk (md5 99240309): every user-visible label, every numeric global, every hub zone id and every click id diffed against the port. Fixed: the right-click Cut / Copy / Paste menu over every field and the script editor (FFMCtx*, zones 700-703 - it was missing), SET GRID's two clicks are the board's OUTER corners with the .ahk's GRID SETUP card and its nudge instead of a full-screen reticle, the PUZZLE AI overlay's "puzzles solved" counter row, the FORSAKEN panel's footer note and equaliser, the speed row's percentage chip and "assumes N fps" note, the dashboard's SYSTEMS subtitle chain (external block active / armed, puzzle AI armed), the clash card's "or press ESC", the cropper's CROP PICTURE title and hint, the resolution card's closing line, the community cache's three-minute freshness window | | | | done |
| - | fixes from testing: LOW PERFORMANCE MODE froze the loading screen (the hub's 40 / 250 / stop timer mapping was applied to every surface; the loading screen and the gate now use RendTim's 40 / 220), the UPDATE LOGS gallery reel (the gate rotation, 3 s per picture with the cross-fade, the counter, the sweep, the progress line), CREDITS' ZEAL portrait (DrawAvatarBase: the pool's first picture) | 46037-46080, 55108-55118, HubTim / RendTim | | Gfx/Surface.cs, Shell/Hub/Tabs/UpdateLogs.cs, Credits.cs | done |
| - | the tour: the sixty steps with their text, kickers, glyphs and anchors, each step opening its tab / module / view / sheet and scrolling its rows into place, the dimmed veil with the lit hole and its breathing rings, the card placed beside the hole (below / above / right / left, or pinned top / bottom), the previous step sliding out, BACK / NEXT / FINISH / SKIP, the progress dots, the light that flies from NEXT to the lit control, a click on the lit control counting as NEXT, tourseen, the first-run start after the welcome card; the bio pop-up | 16390-17660 | ~1,250 | Shell/Hub/Tut.cs, TutSteps.cs, ProfilePlate.cs | done; the title scramble is left out |
| - | the updater and the hub's gate card: the launch check and CHECK FOR UPDATES probe YURI2.version on the repository's main branch, the six-phase card with its copy / glyph / buttons, UPDATE downloads the platform's build, verifies it, swaps it in as .old and restarts; the opening BEFORE YOU START card on every hub open | 2560-2985, 38200-38500 | ~700 | Shell/Hub/Upd.cs | done; the repository needs YURI2.version + YURI2-win-x64.exe / the two macOS zips for it to find anything |
| 5 | gallery picker, avatar cropper, rotation editing, rebind, overlay interaction/render | 48799-52751 | 3,171 | Modules/Gallery/*, Shell/Rebind.cs | |
| 6 | DEVICE OPTIMIZATIONS (module 6) + drift | 13704-15518 | 1,378 | Modules/DeviceOpt/Dop.cs, DopPanel.cs | done: the five groups (46 settings), the elevated PowerShell child (capture / apply / verify / revert), HKCU items in-process, the drift check, RE-APPLY / REVERT ALL, the panel and explainers; Windows-only, the panel shows on macOS |
| 6 | LOGIN ITEMS (module 7) | 15518-16372 | 736 | Modules/LoginItems/Lgi.cs, LgiPanel.cs | done: list, watcher (2.5 s / 6 s holds), open / gentle close / forced end, shell icons on Windows, the panel, tests |
| 6 | CURSOR (module 3) | 18213-20366 | 1,747 | Modules/Cursor/Cur.cs, CurPanel.cs | done: the four slots (64x64 working copies via RenderTargetBitmap), the target scan (Roblox / Bloxstrap / Fishstrap / Voidstrap versions and Modifications, the running client, the macOS bundle), APPLY with keyed backups, RESTORE, AUTO RE-APPLY, FAR CURSOR, the panel, the preview tiles, the eight explainers; tested |
| 6 | THE TOUR | 16372-18213 | 1,488 | Shell/Hub/Tour.cs | |
| 7 | TOWN + NIGHTFALL / MAIN STREET / GREENVALE / FLAPPY | 52751-54785 | 1,778 | Modules/Town/* | |
| 7 | caches (backdrop, hatch, fitted image, plate, text raster) | 51866-56649 (part) | ~1,300 | dropped | see above |
| 8 | PROCESS MEMORY ENGINE (MemIO, FlagSingleton) | 6596-13704 | 5,297 | Modules/FastFlags/FfmEngine.cs | done: MemIO, FlagSingleton (signature + structural scans, plausibility, per-pid caches), FFlags (hash walk, typed writes, in-place strings), the poll, attach / instances / tabs, INJECT (two passes), UNINJECT (+ ALL), the re-apply watchdog, CLOSE RBX, CLEAN; Windows-only, verified on the pure parts here — the live path needs a client |
| 9 | PUZZLE AI (solver, overlay, grid inspector, grid setting) + autoblock overlay | 56649-62176, 36745-36884 | 3,312 | Modules/Puzzle/*, Modules/Autoblock/* | Windows-only |

## Conventions the remaining phases keep

- Names: the .ahk's. `FFMBtn` stays `FFMBtn`; `HL.abx` stays `HL.abx`;
  zone ids are the .ahk's numbers. A diff against the .ahk should be readable.
- Settings: `Core/Ini.cs` reads and writes zeal.ini the way AutoHotkey did, so
  a user's existing `YURI\config\zeal.ini` carries over untouched.
- Every render function takes `(f, dx, dy, now)` as DrawTab does and draws
  into `G`; every input handler resolves a zone from its own ZoneAt table.
- Anything that touches Roblox's files lives behind `Platform/Roblox.cs`
  (paths per OS); anything Windows-only checks `Os.IsWin` at the row and
  draws the row disabled with the reason on macOS.

## Tests

`dotnet run --project tools/Snap -- test` drives the hub headlessly with
synthetic pointer input (every nav item, the module cards, the settings
accents / sliders / theme / tiles / reset, the credits switch, the wheel, the
grip, minimise, close) and asserts the state and the zeal.ini writes; it also
round-trips zeal.ini and the flag files. Any frame that throws fails the run.
Bugs it has caught so far: the data folder colliding with the unix
executable name; the .ahk's own flag-file reader dropping values that
contain an escaped quote; a text-input event carrying several characters
(an IME commit, a synthetic input) losing all but the last through a stale
selection anchor.

## Building

    dotnet build src/YURI/YURI.csproj -c Release
    dotnet publish src/YURI/YURI.csproj -c Release -r win-x64   --self-contained -p:PublishSingleFile=true
    dotnet publish src/YURI/YURI.csproj -c Release -r osx-arm64 --self-contained
    dotnet publish src/YURI/YURI.csproj -c Release -r osx-x64   --self-contained

`tools/Snap` renders the surfaces headlessly (real Skia, no display) into
PNGs — `dotnet run --project tools/Snap -- loading|hub [light]` — which is how
each ported screen is checked against the .ahk without a desktop.

## Defects found against the .ahk, and what fixed them

Reported after the first Windows test of the whole build. Each is now gated by
an assertion in `tools/Snap -- test`.

| Reported | Cause | Fix |
|---|---|---|
| "minimizing just minimizes it entirely, there is no mini UI" | the `bt_` orb block of HubRender was never ported — `Frame` ended after the card, so `cf` reaching 0 left nothing on screen. `ZoneAt` still returned zone 9, so the collapsed hub was clickable but invisible | `Shell/Hub/MiniPill.cs`: the whole block — reel, scrim, drifting band, dots, grid button, halo/rim/status-bar/hairline/brackets, all four `profRing` styles, hover, armed ping, scatter and burst, its own drag veil |
| "the loading UI can be dragged" | `LoadingScreen.OnZoneDown` called `BeginDrag`; `LoadingScreen()` installs no hit test at all in the .ahk | the override is now empty, with the reason in the comment |
| "the drag animation isn't the same" | the veil drew four bare lines from r=10 to r=22, no arrowheads, no caption | the .ahk's arms (r=4→13) with two head strokes each, plus `MOVING` |
| "the macros aren't working" | `WinHooks.Key` sent `wVk` with `wScan = 0`; AutoHotkey's SendInput sends the scan code too, and a client reading raw input ignores a key without one | `MapVirtualKeyW(vk, MAPVK_VK_TO_VSC)` plus the extended-key flag |
| | a bind on `MButton` / `XButton1` / `XButton2` could be set but never fired — `VkOf` returns 0 for them and only the keyboard path dispatched | `Keys.BtnName` / `BtnDown`; `Ab.OnMouse` and `Puz.OnBtn` dispatch them like a key, as `Hotkey("*" bindKey)` does |
| | the block's HotIf carries `!HitZoneAtCursor()`; the port had no such guard, so a click on a card floating over a focused client was eaten and sent as Q | `FskOverlays.HitAt(x, y)` |
| | `HL.armT` / `actT` / `abT` were read in five places and never eased — the amber armed shell never appeared. `SysArmedN` counted six SPECIAL rows individually where the .ahk counts the block as one, skips FPS BOOST, and adds `abOn` / `puzOn` | eased in `Frame`; `SysArmedN` is a chain each module adds to |
| "the Forsaken macro animations (dragging, closing, opening) aren't the same" | `DragT` and `TiltS` gated on `Dragging` (true from the press) rather than the .ahk's promoted `dragOn = 2` / 130 ms hold; velocity sampled in device pixels, so the tilt was off by K; the whole card body dragged where only the six-dot grip should; the collapsed bubble could not be dragged at all; minimise and close fired on release, not press; no drag veil; no burst/scatter rings; no grid button on the bubble | all of the above, in `FskCard` — press/promote/release split into `CardPress` / `CardMove` / `CardRelease` |

TRUE MINIMISE now has its other half: `Shell/Tray.cs` is `TrayInit` / `TrayIcoBuild`
(the accent square with the Y knocked out, redrawn per accent, OPEN / HIDE / EXIT,
single-click default). The setting hides the window only when a tray icon actually
came up; with no tray the button still folds to the pill, because otherwise there
would be no way back.

## Second pass: the FORSAKEN module against the .ahk

Reported as "the macro UIs, the same keybind in macros warning, the grid design,
and many more". Each is gated in `tools/Snap -- test`.

**KEYBIND CONFLICT** was the most degraded thing in the port. Rewritten verbatim.
It was missing: the stacked full-width rows (they were side by side at half width,
so both subtitles elided), the dashed link with its barred circle, the solid/hollow
left bars and dot/ring markers that say which system holds the key, the warning
TRIANGLE (a disc with an "i" stood in for it), the drifting hazard stripes, the
watermark key, the marching chevrons along the foot, the accent rail, the corner
arcs with their reticle brackets, the perimeter light, the FadeLine under the
header, the six-stage entrance (it had three), the breathing border, the `fKey`
chip with its gradient plate and glow rings (it used the 46 pt display face), and
the whole exit — the collapsing ring and the shards. `PanelBackdrop` replaces
`MiniBackdrop`, which the .ahk has a comment about.

**GRID INSPECTOR** (`Modules/Forsaken/Overlays.cs`, `DbgCard`) was never ported at
all. `PZ_MkDbg` is on by default, so arming PUZZLE AI with markers on shows a
286 x 500 readout beside the board: origin, pitch, cells, corner coordinates,
span, box, mid, the board offset with its DOWN BOARD / UP BOARD buttons, the read
rect, probe, walk budget, retry, screen, reference scale with measured /
interpolated, and whether a solve would run.

**The banner offset was a functional bug, not a cosmetic one.** The .ahk knows the
board sits one banner lower on the first puzzle of a session and adds `PZ_boardDy`
in `PuzPY` at read time. The port's reference table carried the `ban` column and
never used it, so board 1 of every session was clicked at the wrong rows. Now:
`BannerDy()` interpolated on HEIGHT (the banner is text and tracks height, unlike
everything else in that table, which tracks width), `BoardApply()` off the session
counter, `BoardSet()` with the pin, plus `RefExact` and `GridGen` for the readout.

**The two overlay cards** were missing, from the shared `Card()`: the accent rail,
the four twinkling corner glints, the toggle sweep, both hairlines and the
`0x28FFFFFF` border, `PanelBackdrop` (Mini stood in), the armed 3000 ms scan rate
(it was pinned at the dormant 4600), and the grip's lift, dot swell and wave. The
minimise and close buttons had their hover indices crossed — the .ahk leans
minimise on `h2T` and close on `h1T` so the pair drifts apart under the pointer.

**SET GRID** was missing the accent rail, the scan band, `PanelBackdrop`, the
border and both hairlines, the warning edge ring and its caution triangle, the
`kick` that replays the middle of the card on a step change, the entrance scale,
and the warning's 3.2 s hold / 800 ms fade.

`ScreenSync` is now defensive: a metrics read that fails must not leave the solver
believing the desktop is zero wide, since every number the grid is made of derives
from it.

## Third pass: the macro cards' open / close / fold animations

**The close was a different animation.** The .ahk anticipates - a 5% swell over
the first 16% - then implodes to 3% with a -14 degree twist, holds the alpha for
the first third of that, sinks 12 px, and throws eight shards outward from the
card's centre after 30%. The port faded linearly with a 12% shrink and a +10
degree rotation and no shards: a window closing, not a card being put away.

**The card never dropped in.** `yOff` moves the WINDOW in the .ahk
(`winPY := baseY + yOff`). The port added it to the transform's PIVOT, which
translates nothing - it only moves the point the scale and tilt turn about. It is
a `PushShift` now.

**A toggle played no animation at all.** `FskCard.Toggled` existed and nothing
ever called it, so `TogAt` / `TogDir` were never set: no sweep across the card, no
pill pop, no bubble bounce, no ignition. `Ab.ToggleBind` now reaches the card
through `FskOverlays.AbToggled`.

**`stateT` was an EK ease, not the .ahk's timed cubic over ANIM_MS**, so there was
no `animStart` edge - and the ENABLED / DISABLED label swapped instantly under a
pill that was still moving instead of crossfading and sliding through the change.
`rippleStart` (the ring off the status dot) was missing entirely, along with its
expiry on the always-reached path - the .ahk notes that clearing it in the draw
path leaves it set for good after a toggle on the minimised bubble.

**The bubble had no reactions.** Missing: `sqB` (squash on disarm), `prsB` (the dip
as it is pressed open), the armed breath, `rotKick` (the twist as it ignites) and
`dipY` (the drop as it goes out). Its ring was three plain circles and one arc;
the .ahk draws 24 arcs with a bright head running them and a second, faster head
running against it, each arc widening under the head, a drift-and-glint pass when
dormant, sequenced ignite / extinguish on a toggle, a dotted counter-march ring
and twelve twinkle ticks.

`h4T` - the keybind row's hover ease - was never eased, so that chip did not
react to the pointer.

## Fourth pass

**A collapsed hub went on eating clicks over the whole card.** The .ahk's windows
are UpdateLayeredWindow layered windows, and those pass a click through any pixel
whose alpha is zero - so a minimised hub stopped catching clicks over the space
the card used to fill without anyone arranging it. Avalonia hit-tests the entire
client rect. `LayeredWindow.InputRect` now confines input (and painting) to a rect
with SetWindowRgn; the hub applies it to the pill once the fold has settled, and
clears it for anything mid-fold.

**Text selection was laid out in the wrong face.** EDIT PROFILE's name and bio and
the script hub's name and description DRAW in fHint, but `FfmField.Font` mapped
those modes to the monospace face - so the highlight and the caret were measured
in a font the text was not drawn in. Over wide letters like w the highlight ended
well short of the word and the caret sat where nothing was going to be typed,
which is exactly what a full selection looked like. The drawing site now passes
the face it draws with, and the mode map has the four missing modes.

**Caret positions come from one shaped run.** Measuring prefixes separately
re-shapes each prefix and the rounding drifts per glyph; `Fonts.Run` hit-tests a
single TextLayout, so the caret's last stop lands exactly on the end of the text.

**A stale double-click timer crossed fields.** `DblAt` is what makes a second
click inside 420 ms select everything, and `Begin` did not clear it - so a click
on one field followed by a click on another inside that window selected the whole
of the second one, which read as the first field's highlight never going away.

**The grid inspector was on the wrong side.** `PZ_dbgX := PZ_mkX + PZ_mkW + 10` -
to the RIGHT of the marker surface, at its own top edge.

**Dragging cost a full hub redraw at 60 fps.** A window's POSITION is the
compositor's job - moving it needs no repaint. What still needs frames during a
drag is the tilt easing and the marching ants on the veil, and neither reads
better at 60 than at 30, over a card the veil is dimming anyway. The hub and both
overlay cards drop to TICK_S for the duration of a drag.

## Fifth pass

- **The editor's highlight outlived its focus.** `Scr.Blur` cleared `Focus` and
  `MSel` but not `SelOn`, so the script editor went on drawing a selection over
  text nobody was editing - a click into a field left the previous box looking
  live. It now drops the selection with the focus.
- **A blur threw the edit away.** `FFMBlur` in the .ahk committed the only two
  fields that existed when it was written; every field added since is a
  type-and-it-is-yours box, and `Blur` discarded them - so a click anywhere
  outside, including on the next text box, silently reverted what had just been
  typed. Blur commits now; Escape is still the way to abandon an edit.
- **A cut could leave the old text on screen.** The editor's token cache is
  rebuilt on `Dirty`, but a cut REMOVES lines and the draw falls back to the
  cached list by index - a stale cache one line too long renders text that is no
  longer there. `TokStale` compares the counts as well.
- **The module rail could not be scrolled with DEVICE OPTIMIZATIONS or LOGIN
  ITEMS open.** Both panels' `Wheel` consumed every event wherever the pointer
  was and whether or not the list had anywhere left to go. They now bound-check
  the pointer and decline when they are already at the end, as the other panels do.
- **The macro cards took the foreground on a click.** They sit on top of a game
  and are clicked while it is being played, so `ShowActivated = false` plus
  WS_EX_NOACTIVATE: the card still receives the click, the game keeps its input.
- **The hub ran at full rate behind other applications.** `Surface.Active`
  follows the window's activation; unfocused, the hub stands down to the in-game
  tier, with the settled tier held while a transition still has to land. This is
  the largest saving available - the hub spends most of its life behind whatever
  the person is actually doing.

## Sixth pass

- **CHECK FOR UPDATES always answered 404.** The port asked for a `YURI2.version`
  that nobody publishes. The .ahk asks the repository for `YURI.ahk` and reads
  `APP_VERSION` out of it, which is why its check works - that file IS the
  release. The dedicated one-line file stays the first choice; the script is the
  fallback, so a repository that has only ever held the .ahk still answers.
- **Re-arming a macro mid-close disarmed it.** `AbSync` only opened a card when
  `_ab` was null, so a toggle off then straight back on did nothing - and when
  the close landed, `OnClosed` ran `Ab.Disable()` and put the module out again.
  `Revive()` cancels the close, so the card never dies and nothing is disarmed
  behind the person's back.
- **The wheel over the module rail scrolled the systems list.** A panel's own
  wheel guards on y and mostly not on x, so at any height inside the list it took
  the wheel wherever the pointer was. The rail is checked first now - unless a
  sheet covers the card, in which case what is drawn on top owns the wheel.
- **The module rail's scrollbar could not be grabbed.** It was the one bar in the
  hub that was a readout rather than a control. Zone 1150, drag 26, and it widens
  and brightens under the pointer like the others.
- **The DATABASE field kept showing text that had been cut.** It drew the
  module's copy of the filter rather than the field's own buffer, so anything
  that moved the buffer without the write-back landing left the old text on
  screen over a filter that had already moved on. While a field is being edited
  its buffer is what gets drawn.
- **Losing the foreground did not stop the typing.** A caret still blinking in a
  field is a lie once the keys are going to another application; deactivation now
  commits the edit and lets go of the keyboard, in both the fields and the editor.
- **The crop card.** The zoom readout was laid at `kx + 24` across the row the
  buttons are on, and USE starts at `kx + 60` - so they overlapped. It has its
  own row now, with the level drawn as a filled track (a number nobody can
  compare against anything is worth less than a bar). The card also gets the
  language the rest of the suite carries: the accent rail, the scan band,
  PanelBackdrop, the border and both hairlines, corner arcs, and viewfinder
  brackets on the crop square.

## Seventh pass

- **The clash card's corner brackets are gone.** The straight L outside each arc
  read as a second, misaligned corner sitting off the card. The 60-degree arcs on
  the real corner radius stay; nothing else about the card changed.
- **The tour is modal.** `Tut.Zone` returned 2206 for anything that was not its
  own and the hit test fell through, so every button, tab and row under the dim
  stayed live - a click meant to advance the tour could open a module or start a
  download behind it. 2206 is the scrim now: a real zone that swallows the click
  and does nothing. The wheel is refused for the duration, and the tour takes the
  keyboard - ESC leaves, the arrows and SPACE / ENTER walk it.

## Eighth pass

**RESTORE on the cursors took two presses.** Two faults, and the second is the
one that mattered.

The loop's rule for a Bloxstrap Modifications file was "no backup means we made
it, so remove it". That holds only until a restore has consumed the backup.
So the first press copied the backup back INTO the mod folder - the original
content, but still an override, which is why the preview then read `bloxstrap
mod` - and deleted the backup. The second press found no backup, concluded the
file was ours, and deleted it. Worse than untidy: on a machine where Bloxstrap
genuinely had its own cursor there, the second press destroyed it, because the
first press had just put it back.

Creation is RECORDED at apply time now (`[cursor] made`), so a mod file is only
removed when we are the ones who brought it into existence, and a file handed
back to Bloxstrap stops being ours the moment it is restored. One press finishes,
and a second cannot take a mod nobody asked it to touch. A hub that applied
before the record existed keeps the old rule, since an empty record there means
"unknown" rather than "none of it is ours".

`Restore` also lacked the `GetTargets(true)` that `Apply` has, so `SyncDefaults`
read the target list captured before the loop ran and the preview described the
files as they had been rather than as the restore had just left them.

## Ninth pass: an audit, and the fault the eighth pass introduced

- **The cursor record's legacy fallback came back once the record emptied.** The
  eighth pass treats an absent `[cursor] made` as "unknown, use the old rule".
  But `MadeDrop` empties the record as it works, so after a restore had dropped
  every key a third press would have fallen through to the old rule and deleted a
  Bloxstrap file again - the exact fault the pass was written to remove. "No
  record" and "a record that is now empty" are different answers, and
  `[cursor] madeset` is written once and never cleared to tell them apart.
- **Two more wheels guarded on height alone.** CLIENT SETTINGS and CURSOR took
  the wheel at any horizontal position on their band. The rail-first routing hid
  it, but the guard was still wrong; both check x now.
- **The collapsed hub's region clipped the pill's own rings.** `SetWindowRgn`
  clips painting as well as input, and the box was 25 px clear of a 37 x 48 orb
  that throws an armed ping 25 px out. Widened to clear the widest ring.
- **CHECK FOR UPDATES lost the status code.** Falling back to a second source
  discarded the first one's answer and reported "did not answer" for everything -
  a 404 on both sources and a dead network became the same message. The status is
  named again.
- **Two `async void` handlers could take the process down.** In both, only the
  clipboard call was inside the try; the parse after it (PASTE COOKIE) and an
  index into the links table ran unguarded, and an exception out of an
  `async void` has nowhere to go.
- **The editor's caret could point outside its buffer.** `SelGet` and everything
  under it index `Lines` with `Cl` / `Sl`; `ClampCaret` now runs first.

## Tenth pass

**Six hooks were declared and never assigned**, so every reader of them showed a
default. A sweep for `public static Func<...>` with no assignment anywhere found:
`GalRotN` / `GalHidN` (SETTINGS said "0 in rotation" with five pictures in it),
`GalHasRot` (UPDATE LOGS believed there was no rotation), `ScriptsPlaced` /
`ScriptsRunning` (the dashboard's SCRIPTS tile reported an empty hub), and
`ClientState` (the rail's CLIENT card always drew "nothing chosen, not applied").
All wired, at boot rather than on first draw - `Scr.Hooks` is split out of
`Scr.Register`, which resets the editor's tabs and is not safe to call twice.

**MANAGE did nothing.** `if (z == 54) return true;` - the handler swallowed the
click and stopped. It opens the gallery in manage mode.

**The ACCOUNTS chip read a list nothing fills.** `Spf.Acct` was an empty leftover
beside the real `SpfAcct.Acct`, so the row said 0/8 whatever was saved. Removed,
and the chip reads the loaded list.

**The accounts header ran its subtitle under the button.** `w - 300` put the
text's right edge 14 px past the left edge of ADD FROM CLIPBOARD. Measured against
the button and elided, so a longer line cannot reach it either.

**A failed game lookup was cached for the life of the session.** `GameInfo.Fetch`
stamped `At` on failures exactly as on successes, and the guard treats a stamped
record as an answer - so a place that failed once kept its raw id as a name until
the hub was restarted. Failures now expire after 20 s. And the saved-places view
only ever fetched the SELECTED place, so a row never clicked stayed as its id;
every saved place is fetched when the view opens.

**The crop card had no exit.** It arrived on a 420 ms scale-in and vanished
between two frames. USE and CANCEL both start the same 260 ms exit now, and the
card is inert while it plays.

## Eleventh pass

- **The GRID INSPECTOR overlapped the markers.** `PZ_dbgX := PZ_mkX + PZ_mkW + 10`
  measures off the MARKER SURFACE, which is the board plus a half-cell and 16 px
  of margin either side - where the corner brackets are drawn. It was measured
  off the board's own rect, so the card sat about a half-cell inside the marker
  window and the two ran together down the whole edge.
- **The markers took the foreground when they came back.** The extended style
  that makes those windows non-activating was applied AFTER the first `Show`, so
  the very Show that returned the grid after a solve pulled focus off the game -
  and a client set to throttle in the background dropped its frame rate the
  moment the puzzle ended. `ShowActivated = false` before Show, on the markers
  and the inspector. The SET GRID card keeps activation: it waits on clicks and
  on ESC and has nothing to steal focus from.
- **The right-click menu was painted underneath the sheets it was opened over.**
  `ZoneAt` gives the context menu the first look at a click, but `Draw` ran it
  before EDIT PROFILE and the gallery - so on those fields the menu opened,
  answered the hit test, and was invisible. It draws last now.
- **Four more portrait rings**: PULSE, TICKS, SEGMENT and HALO, on a second row
  in the picker. The second row uses zones 150..153 - 131+j would have run into
  138 (the veil, which closes the sheet) and 139 (the sheet body). All eight are
  drawn on the minimised pill too, since the picker promises they apply
  everywhere.
- **The tour**: a light with a comet trail running the cutout's perimeter, four
  corner motes breathing out of phase, and a step change that throws a short arc
  of motes toward the arriving card with a burst where they land. All in the
  hub's existing vocabulary, and all skipped under LOW PERFORMANCE MODE.
- **The changelog** records the move from the script to a built application.

## Twelfth pass

- **The app icon.** `Assets/appicon.png` (the artwork trimmed off its white
  margin and squared to 1024), `YURI.ico` for `ApplicationIcon` - what Explorer,
  the taskbar and the switcher read off the executable - and `YURI.icns`, copied
  into the bundle with `CFBundleIconFile` naming it. Every LayeredWindow sets
  `Icon`, since a frameless window has no title bar and the taskbar would
  otherwise show a blank sheet. The icns is written directly: the container is
  `icns` plus typed chunks, and `ic07` and up take a PNG payload, so it needs no
  iconutil. The TRAY mark is left alone - it follows the accent by design.
- **A scrollbar that jumped on grab.** The community view carried its own copy of
  `ScrollFromY` that always put the thumb's MIDDLE under the cursor, so taking
  hold of it anywhere but dead centre moved it before the drag began. Deleted;
  all three of that view's bars use HubUI's, which remembers where inside the
  thumb the press landed and only centres when the press missed it. A sweep found
  no other copies.

## Thirteenth pass

**Six marks, one per accent.** `Assets/appicon_1..6.png`, indexed to
`HubState.Accents` - rose, orange, amber, violet, sky, teal - each trimmed off
its white margin and squared to 1024. Matched to the palette by hue rather than
by eye, so the file order cannot drift from the accent order.

The mark IS the accent, which is the same reasoning the .ahk draws its tray icon
from `hubAccent` rather than shipping one. So:

- every LayeredWindow takes the mark for the live accent, and `IconRefresh`
  re-marks every open window when the accent changes - that is what the taskbar,
  the switcher and the window list read;
- the TRAY takes the same mark, with the drawn accent square kept as the fallback
  for an accent that has no artwork, so nothing is ever left without an icon;
- an accent outside the palette falls back to mark 1 rather than to nothing.

`YURI.ico` and `YURI.icns` carry the DEFAULT accent (rose, `Accents[0]`). An
executable's embedded icon cannot follow anything at run time, so the file on
disk shows what a fresh install shows; everything that CAN follow the accent
does.

## Fourteenth pass: measured, then cut

The icons are masked to their rounded rect now - the artwork had a white page
behind it, so the corners were white squares. The radius is measured off each
tile (a rounded rect's top row starts at left+r, so the first opaque pixel's
inset IS the radius) and the mask is drawn at 4x and shrunk, so the corners are
properly anti-aliased rather than stepped.

**A bench, before touching anything.** `tools/Snap -- bench` renders each tab and
reports its draw cost, its idle tier and - via `HubSurface.AnimWhy` - the term
holding it off the settled one. Guessing at what is hot is how a session gets
spent optimizing the wrong thing.

It found two:

- **Every string was re-shaped on every draw.** `TxtP` built a fresh
  FormattedText per call and the hub draws twenty to fifty of them a frame, which
  made text the largest bucket by a wide margin. The layout does not depend on
  the colour, so it is cached per (string, font) and the brush is set on it at
  draw time. Tab 1: **42.7 ms -> 18.3 ms a frame**, and every other tab down with
  it.
- **UPDATE LOGS held the ACTIVE tier for as long as it was open.** The predicate
  asked whether a rotation EXISTS, not whether a fade was running - so sixty full
  redraws a second for a 420 ms crossfade that happens once every three seconds.
  It now asks the reel, and sits on the settled tier between fades.

Every tab settles off the active tier with the pointer away; the numbers above
are headless software Skia, so they are useful against each other rather than as
absolutes.

## Fifteenth pass: macOS

**The Apple Silicon build could not have launched.** The apphost is produced by
stamping the app's name into a template, which invalidates the signature the
template carried - so it ships unsigned while every dylib beside it is already
signed by Microsoft. On Apple Silicon the kernel REFUSES to exec an unsigned
arm64 binary: not a Gatekeeper prompt, a hard failure. `tools/sign-mac.sh`
ad-hoc signs the bundle (codesign where it exists, rcodesign otherwise) and the
delivered builds are signed. Verified by walking every Mach-O in the bundle for
an LC_CODE_SIGNATURE load command - before, exactly one was missing: the apphost.

**The collapsed hub ate clicks on macOS.** The fix was `SetWindowRgn`, which has
no macOS counterpart, so the bug was Windows-only-fixed. Replaced with something
that works on both: the WINDOW shrinks to the part of the surface being drawn.
`Surface.SetCrop` reports the crop's size, translates the render by its origin,
puts the pointer back into surface space on the way in, and moves the window so
nothing appears to jump. The Win32 region is gone; one mechanism now.

The resize is posted to the dispatcher rather than done inline - `Frame` runs
inside the render pass, and a visual cannot be invalidated while it is being
drawn.

**No menu bar.** Every window is frameless, so the app had no application menu
and Cmd-Q did nothing - the hub's own close button was the only way out. A native
menu with About, Hide (Cmd-H) and Quit (Cmd-Q), macOS only.

## Sixteenth pass: flag injection on macOS

It was never the injection that was Windows-only - it was the ENGINE. The module
has two routes to a flag and only one of them needs Windows:

- the PROCESS MEMORY ENGINE patches a client that is already running, through
  OpenProcess and WriteProcessMemory. Windows only, and always was;
- `ClientAppSettings.json`, which the client reads on every launch. That needs
  nothing but a file write, and `Ffm.AppSettingsPaths()` has had a macOS branch
  (`Roblox.app/Contents/MacOS/ClientSettings`) the whole time.

`Ffm.WriteAppSettings()` existed and **nothing ever called it**, so macOS had no
way to apply a flag at all. Now wired: INJECT LIVE and INJECT ALL write the file
where there is no engine, UNINJECT ALL takes it back out, and AUTO INJECT means
"keep the file in step with the staged list" - the engine's client poll is what
fires it on Windows and there is no poll on macOS, so it writes on the switch and
after every change to the list.

`ClearAppSettings` writes `{}` rather than deleting: the client is happier
reading an empty object than finding the file gone part-way through a launch.

The file route is also strictly more durable than the memory one - it survives a
client restart, which a memory patch never does.

## Seventeenth pass: finding the client on macOS

`IsRunning()` was `GetProcessesByName(Os.RobloxProcess)` - one name, one call. On
macOS that never matched, for three reasons at once: the client there is not
`RobloxPlayerBeta`; the newer unified app is not reliably `RobloxPlayer` either;
and .NET's name comparison is not case-insensitive off Windows. So NO RBX with
the client plainly running, and with it every module that gates on the client.

It enumerates once and matches a set now - by case-insensitive name, and on macOS
also by whether the executable's own path sits inside a `Roblox.app`, which
answers for a bundle that has been renamed or installed somewhere unusual. What
it found is kept in `FoundAs` / `FoundPath`, so the next poll asks for that one
name instead of sweeping again - the sweep is what the old call cost, and this
runs while the hub is idle.

`Roblox.MacBundle()` falls out of it, and the flag file now goes into the bundle
the RUNNING client came out of before the two standard locations - that is the
one that will actually read it, wherever it was installed.

## Eighteenth pass

**A flag write that failed reported as "no install found".** `WriteAppSettings`
swallowed every exception per folder, so a permission refusal and an absent
client produced the same message - and that message sends someone looking for a
bug in the flags. It now names the fault (`WriteFault`), and reads the file back
after writing: a write that "succeeds" into a place the client does not read is
the failure that looks like success.

**WINDOWS ONLY, as a state rather than a sentence.** Some of this suite is a
Windows program wearing the hub's clothes - a global keyboard hook,
ReadProcessMemory, the registry, bcdedit - and a control that looks live and does
nothing is worse than one that says why. `HubUI.WinOnlyVeil` dims the panel,
hatches it, and puts a card on top naming what cannot run and the reason in the
module's own words. FORSAKEN and DEVICE OPTIMIZATIONS carry it, and both refuse
every zone underneath rather than merely looking disabled - gated by a test that
sweeps the whole panel for a live zone and expects none.

## Nineteenth pass: checking the eighteenth

- **The status line was writing the flag file.** `AfterChange` went onto every
  `Changed?.Invoke()`, and one of those is inside `Say` - so with AUTO INJECT on,
  any status message rewrote ClientAppSettings.json, and `FileApply` wrote twice
  per press because it says something afterwards. `Say` no longer calls it (a
  status line is not a change to the list), and `AfterChange` has a re-entrancy
  guard for the same reason.
- **The fast flag rows described a route the build does not have.** Off Windows
  INJECT LIVE said "write flags into Roblox memory" and the buttons said INJECT /
  NO RBX / UNINJECT - all of it about an engine that is not there. They read
  WRITE and CLEAR now, the hint says "write flags into the client's settings
  file", and rows 2 to 4 (SINGLETON, RE-APPLY, AUTO INJECT's engine half) say
  "needs the process memory engine - windows only" and are dimmed.

Both veils checked by eye at the panel's real size, and a test sweeps every
Windows-only panel for a live zone and expects none.

## Twentieth pass: the macros' bubble

**Collapsed, the card's window is the bubble.** Same fault the hub had, and worse
here: a 364 x 184 transparent rectangle with a 40 px bubble in one corner, sitting
over a game and taking every click that landed on the other nine tenths of it.
`SetCrop` again - 120 x 120 around the bubble, which clears its widest ring.

**The bubble was missing most of what says armed.** The port drew a portrait, three
plain rings and the 24-segment ring. The .ahk draws, in this order: three soft
halos that breathe with the pulse when armed (and one on hover when not), the
five-deep drop stack that deepens under a drag, one heavy 120-degree arc parked
at 15 degrees when armed and drifting when not, the portrait at 30 (not 26), the
hover scrim and OPEN, three hairlines, and THEN the state arcs -

- armed: two comet pairs sweeping one way at 61.5 - each a 104-degree tail, a
  64-degree body and a 28-degree head at 78 / 160 / 245 alpha - with two faint
  58-degree arcs turning the other way one ring out, and a flash on the head as
  it ignites;
- dormant: four 40-degree arcs breathing out of phase, with a filament flicker
  while it goes out;

- then the outer ring that widens on hover, and only then the segment ring.

All of it was absent, which is why the two states looked alike. The portrait size
was wrong too, so the hover scrim did not cover it.

## Twenty-first pass: the loader was telling the truth about nothing

The loading card's fourth caption is the .ahk's own - "resolving N place(s), N
account(s)" - and it was a caption over no work. `Boot.Left()` counted exactly
two jobs, the flag database and the community sync; the lists themselves come off
disk in each module's Register, but what makes them READABLE - a place's name,
icon and thumbnails, an account's username and avatar - is fetched over the
network, and none of it was asked for until the view was opened. So the hub
arrived furnished with placeholders and filled in while being looked at.

`Boot.Prefetch()` asks for all of it at boot, and `Boot.AddWait(start, done, cap)`
holds the loader until the store says it is done or the cap runs out - the loader
already waited on `Boot.Left()`, the work just never told it that it was running.
The cap is 9 s against the card's own 12 s ceiling, so a slow network delays the
hub without ever stranding it.

`Boot.AvatarPoolN` was never assigned either, so the third caption counted
"mounting avatar pool [0]" however many pictures were in it.

## Twenty-second pass: the pause before the grid

Building a window is the slow part, and the grid was rebuilt far more often than
it needed to be:

- every solve **destroyed** the marker window and the next Apply built a new one.
  Solving and calibrating are pauses, not endings - the window is hidden now and
  shown again, and only the module going off tears it down;
- `GridSet` killed the window before calling Apply, which already rebuilds when
  the geometry changes. That kill only guaranteed the slow path;
- `DbgApply` ran BEFORE the markers were placed, so the grid waited behind a
  second window's construction before it could be shown - and the grid is the
  thing that was asked for. It runs last now;
- and the window is built when the module is ARMED rather than on the first press
  of the markers key, which moves the one unavoidable build to a moment where
  nobody is waiting on it.

Hiding is as good as closing for the solver's screen scan - a hidden window is
not composited, so it cannot appear in the capture - and the existing scan settle
covers the latency either way.

## Twenty-third pass: what the crop broke

The window crop was the right fix for the click-through, but two things read a
window position as though it were the card's own, and a cropped window's corner
is the BUBBLE's corner:

- **`SavePos` stored the cropped origin.** A card closed while collapsed came
  back a crop-width (242 x 62 logical px) away from where it was left, on the
  next launch. `Surface.UncroppedPos()` gives the origin the card would have had,
  and SavePos writes that.
- **The drag clamp measured the grip from the wrong point.** `DragGripX/Y` are in
  SURFACE space and the clamp treats them as offsets from the WINDOW's origin -
  true until the window started at (CropX, CropY) of the surface. A collapsed
  card clamped as though its grip were a crop-width away from where it is, and
  the window was pushed off the edge of the screen near the boundaries.

Both were mine, from two passes ago, and neither would have shown up until
someone dragged a collapsed card to the edge of a screen or restarted with one
folded.

## SmartScreen

"Windows protected your PC - Unknown publisher" is not a metadata problem and
cannot be fixed in the build. The published exe's PE security directory is empty:
it carries no Authenticode signature, and that one fact is the whole of what
Windows is reporting. The icon, the version resource and the file name do not
enter into it.

`tools/sign-win.ps1` makes signing one command once a certificate exists
(signtool, SHA-256, RFC-3161 timestamped so the signature outlives the cert).

Worth knowing for whoever picks a certificate: reputation accrues to the
CERTIFICATE, not the file. Unsigned, every build is a new unknown file and starts
from zero however many downloads the last one had.

## Twenty-fourth pass: a card you could not click back into

WS_EX_NOACTIVATE is what keeps an overlay off a game's input, and the .ahk sets
it on every window it owns. What the .ahk never paid for is the consequence:
AutoHotkey draws with GDI on a timer, so nothing throttles it. This build is
composited, and a driver or an OS set to cap BACKGROUND applications will hold
the card at that cap - and since the style means the process can never become
the foreground one, no amount of clicking the card gets it back. The thing the
person is clicking on is the thing running at fifteen frames a second.

Two changes, both narrow:

- **A deliberate press asks for the foreground** when there is no client in
  front to take it from. The style stops a CLICK activating the window; it does
  not stop us calling Activate. With a game in front nothing changes - that is
  the case the style exists for. Both cards and the grid inspector do it.
- **The style follows the client.** It is only worth its cost while there is a
  game to protect; with no client running it comes off and the card behaves like
  any other window. Re-checked at the client poll's own rate, so it costs nothing.

Also replaced a timing assertion written last pass ("showing again is not slower
than rebuilding") - it measured the harness rather than the fix, since headless
window creation is cheap enough that the noise was larger than the difference.
The invariant that matters is that hiding does not DESTROY the surface, and that
is what it checks now.

## Twenty-fifth pass: the two expensive scenarios, measured

`tools/Snap -- bench` now measures the two states that were reported rather than
only the six tabs.

**The minimised pill was pinned at sixty frames a second.** The same mistake
UPDATE LOGS had, in a second place: the render tier asked whether a picture
rotation EXISTED rather than whether one was crossfading, so the pill held the
active tier for as long as the hub was folded - which is most of the time. It
now asks for the fast tier only while a fade is running, and folded-and-settled
has its own tier at thirty: everything still moving there (the drifting rim, the
ring, the armed ping) is slow enough that thirty reads the same as sixty. A hover
or a transition still gets sixty. Draw cost also came down, 1.95 ms to about 1.2.

**Dragging redrew the whole card every frame.** Nothing inside the card can
change while it is being dragged - no hover, no click, no tab - so every frame
was recomputing an identical result and then dimming it to a third under the
veil. The card is now rendered ONCE when the drag promotes and blitted after
that; only the veil, the tilt and the lift still cost anything.

The capture is POSTED rather than taken inline: this runs inside the render pass
and a bitmap cannot be rendered from within one. Until it lands, frames take the
ordinary path - so a capture that fails costs nothing and looks like today.

## Twenty-sixth pass: the style had to go, not be worked around

The previous attempt kept WS_EX_NOACTIVATE and tried to ask for the foreground on
a click. That cannot work, and it was the wrong shape anyway: Windows refuses
SetForegroundWindow to a process that does not already hold the foreground, and a
click on a non-activating window does not give it. So the card stayed
unfocusable, stayed background, and stayed capped.

The style is off the cards and the grid inspector now. `ShowActivated = false` is
the half that was actually wanted: APPEARING never takes the game's input, which
is the thing an overlay must not do. Being CLICKED does, which is what any window
does, and is the only way the person can use it at full rate.

The markers keep the style - they are click-through and never receive a click.

This makes `FskOverlays.HitAt` load-bearing rather than belt-and-braces: the click
that focuses a card arrives while the game is still in front, and without that
guard the block would swallow it and send Q.

## Twenty-seventh pass: the flag database

Checked the three sources directly. `FIntSimDefaultFluidForceEnabled` is in none
of them, and is not a name Roblox has shipped - the real ones near it are
`DFFlagFluidForcesDefaultEnabled`, `DFFlagFluidForcesSwitchToThreePhaseRollout`
and `DFFlagSimWakeBodiesOnFluidForcePropertyChange`. So that one was not a
loading failure. But looking for it turned up two things that were:

- **An empty search looked exactly like a broken database.** With the list
  loaded and nothing matching, the view drew an empty box and said nothing at
  all, which is indistinguishable from a fetch that failed. It now says what did
  not match, how many flags ARE listed, and that the tracker lags new ones.
- **ENTER on a name the tracker does not carry did nothing.** The client does not
  care whether this list has heard of a flag, so a properly-prefixed name is
  staged as typed - which is the only route to a flag newer than the dump. A name
  with no prefix is refused and says why.

And one real defect in the data: about a hundred of the JSON dump's 22,323 keys
carry no type prefix at all (`AllowVideoPreRoll`, `AdsCtrDebugging...`). A flag IS
its prefix - it is what tells the client the type and the scope - so those were
names that could be staged and would never be read, sitting in the list looking
like flags. They are dropped at parse time.

## Twenty-eighth pass: the second game-specific set

The community sync discovered new folders through api.github.com's tree
endpoint, which allows sixty unauthenticated calls an hour per address. A boot
and a few opens of the view ran through that; once limited, the sync fell back
to refreshing the folders it already knew about - so a set added since the cache
was written (`LOCKED::2`, here) never appeared until the hour turned, and the
only sign was a note in the REFRESH message that nobody reads at boot.

The whole repository now comes down as ONE archive from github.com itself, which
is not metered, and which carries every file's body - so the per-file raw fetches
are gone as well. A real sync against the live repository returns all three:
`general`, `FORSAKEN` and `LOCKED::2`.

Two more things in the same path:

- **The sync's Task completed a step early.** It resolved when the fetch had
  finished, before `Synced()` had run `IndexLoad` on the UI thread - so the
  loader could count the sync done and open the hub with the entries list still
  empty. It completes when the result has landed now.
- **The community games' names and art were not part of loading.** The view
  names each game-specific set by its place and shows its icon, both from the
  games API, and neither was asked for until the view was opened. Chained after
  the sync, since the list of places is the sync's result.

## Twenty-ninth pass: "could not close roblox" after closing it

`Roblox.IsRunning()` answers from a sweep up to 1.2 s old - the throttle put on
it when the client poll moved to a full process enumeration. Both places that
judged a CLOSE read it straight after the kill, so a client that had gone was
still "running", and a close that had worked reported "could not close roblox -
try running as admin". `Roblox.Kill` clears the cache after it, and both sites
ask `IsRunningNow()` for a fresh sweep. (The launch path already cleared it.)

The same look turned up the mirror of the macOS detection fix from earlier: TEN
call sites still asked `GetProcessesByName` for the one Windows name - the two
kills, the stale-client count, the priority and throttle changes, the mod and
cursor writers' "is a client holding this file" checks. Right on Windows,
nothing on macOS. All of them now go through `Roblox.Processes()`, which finds
the client the way `IsRunning` does. The names that remain hard-coded are file
names inside Windows install folders and the memory engine, which are Windows by
nature.
