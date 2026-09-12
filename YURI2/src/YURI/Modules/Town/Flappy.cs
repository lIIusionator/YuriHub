using Yuri.Core;
using Yuri.Gfx;
using Yuri.Shell.Hub;
using static Yuri.Gfx.Ease;

namespace Yuri.Modules.Town;

public sealed class FPipe { public double x, gy; public bool passed, coin; }
public sealed class FWorld { public double sT, sB, hill, gnd, gT, gS, pipe, pL, pD, cT, cU, sun; public bool stars; }

/// <summary>
/// FLAPPY (FB): the fourth town. A tap anywhere on the scene flaps; the pipes
/// close their gap and speed up with the score; a coin sits in six gaps out
/// of ten; a death pays a third of the score into the coin purse; the shop
/// sells six birds and five worlds. best / coins / birds / worlds / bird /
/// world persist in zeal.ini exactly as the .ahk kept them.
/// </summary>
public static class Fl
{
    public static bool on, dead, shop; public static double y = 46, vy, gx; public static int score, best, coins, run, bird = 1, world = 1, birds = 1, worlds = 1, bonus, bought;
    public static long lastT, deadAt, spawnAt, flapAt, bestAt, sparkAt, boughtAt; public static double spx, spy;
    public static readonly List<FPipe> pipes = new(); public static readonly List<(double x, double y, long at)> trail = new();
    public static readonly (string n, int p)[] BIRDS = { ("PLAIN", 0), ("ROBIN", 15), ("JAY", 25), ("GHOST", 40), ("GOLD", 60), ("NEON", 80) };
    public static readonly (string n, int p)[] WORLDS = { ("DAY", 0), ("DUSK", 20), ("NIGHT", 30), ("SNOW", 45), ("CAVE", 70) };
    static readonly Random _rnd = new();
    public static void Load()
    {
        best = Math.Max(0, (int)Ini.ReadInt(Paths.IniFile, "town", "fbbest", 0));
        coins = Math.Max(0, (int)Ini.ReadInt(Paths.IniFile, "flappy", "coins", 0));
        birds = Math.Max(1, (int)Ini.ReadInt(Paths.IniFile, "flappy", "birds", 1)); worlds = Math.Max(1, (int)Ini.ReadInt(Paths.IniFile, "flappy", "worlds", 1));
        bird = Math.Clamp((int)Ini.ReadInt(Paths.IniFile, "flappy", "bird", 1), 1, 6); world = Math.Clamp((int)Ini.ReadInt(Paths.IniFile, "flappy", "world", 1), 1, 5);
    }
    public static void Leave() { on = false; dead = false; shop = false; }
    public static void Tap()
    {
        long now = Clock.Tick;
        if (shop) return;
        if (dead) { if (now - deadAt < 700) return; Reset(); }
        else if (!on) Reset();
        on = true; vy = -0.072; flapAt = now;
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    public static void Reset()
    {
        y = 46.0; vy = 0; pipes.Clear(); score = 0; dead = false; run = 0; bonus = 0;
        lastT = Clock.Tick; spawnAt = Clock.Tick + 1500; gx = 0; trail.Clear();
    }
    public static void Die(long now)
    {
        dead = true; deadAt = now;
        bonus = score / 3; coins += bonus;
        Ini.Write(Paths.IniFile, "flappy", "coins", (long)coins);
        if (score > best) { best = score; bestAt = now; Ini.Write(Paths.IniFile, "town", "fbbest", (long)best); }
    }
    public static void Buy(int kind, int i)
    {
        var lst = kind == 1 ? BIRDS : WORLDS; int own = kind == 1 ? birds : worlds;
        if (i < 1 || i > lst.Length) return;
        int bit = 1 << (i - 1);
        if ((own & bit) == 0)
        {
            if (coins < lst[i - 1].p) { bought = -i * kind; boughtAt = Clock.Tick; HubSurface.Live?.Tim(Pace.TICK_A); return; }
            coins -= lst[i - 1].p; own |= bit;
            if (kind == 1) birds = own; else worlds = own;
            Ini.Write(Paths.IniFile, "flappy", "coins", (long)coins); Ini.Write(Paths.IniFile, "flappy", kind == 1 ? "birds" : "worlds", (long)own);
            bought = i * kind; boughtAt = Clock.Tick;
        }
        if (kind == 1) bird = i; else world = i;
        Ini.Write(Paths.IniFile, "flappy", kind == 1 ? "bird" : "world", (long)i);
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    public static FWorld World(int w) => w switch
    {
        2 => new() { sT = 0xFF6A3F8A, sB = 0xFFF2A468, hill = 0xFF5A3F7A, gnd = 0xFF8A6A4A, gT = 0xFF6E9A4E, gS = 0xFF6A4A34, pipe = 0xFF4FA85C, pL = 0xFF6FBF63, pD = 0xFF2E7A45, cT = 0xFFF6C8A8, cU = 0xFFD99A78, stars = false, sun = 0xFFFFC46B },
        3 => new() { sT = 0xFF0B1230, sB = 0xFF2A3466, hill = 0xFF1A2448, gnd = 0xFF3A3F5C, gT = 0xFF2E5A3A, gS = 0xFF2A2E44, pipe = 0xFF3E8A48, pL = 0xFF5AB366, pD = 0xFF246A38, cT = 0xFF3A4270, cU = 0xFF2A3058, stars = true, sun = 0xFFF4F1E8 },
        4 => new() { sT = 0xFFB9D3E6, sB = 0xFFE8EEF4, hill = 0xFFC9D6E0, gnd = 0xFFF4F1E8, gT = 0xFFFFFFFF, gS = 0xFFD8DEE6, pipe = 0xFF8FC3E6, pL = 0xFFB9DCF2, pD = 0xFF5A8FB8, cT = 0xFFFFFFFF, cU = 0xFFD8E4EE, stars = false, sun = 0xFFFFF4C8 },
        5 => new() { sT = 0xFF2A2436, sB = 0xFF3A3050, hill = 0xFF241E2E, gnd = 0xFF4A3A5A, gT = 0xFF6A4A8A, gS = 0xFF3A2E48, pipe = 0xFF6A4A8A, pL = 0xFF8A6AAA, pD = 0xFF4A3260, cT = 0xFF3A3050, cU = 0xFF2A2436, stars = false, sun = 0 },
        _ => new() { sT = 0xFF6CB8EA, sB = 0xFFCFE9F8, hill = 0xFF9CCB8A, gnd = 0xFFC9A87A, gT = 0xFF7CBF5A, gS = 0xFFB08A5A, pipe = 0xFF4FA85C, pL = 0xFF6FBF63, pD = 0xFF2E7A45, cT = 0xFFFFFFFF, cU = 0xFFD8EAF6, stars = false, sun = 0xFFFFE9A8 },
    };
    // ---- the 3x5 pixel font ----
    static readonly Dictionary<char, string> Glyphs = new()
    {
        ['0'] = "111101101101111", ['1'] = "010110010010111", ['2'] = "111001111100111", ['3'] = "111001111001111", ['4'] = "101101111001001", ['5'] = "111100111001111", ['6'] = "111100111101111", ['7'] = "111001001001001", ['8'] = "111101111101111", ['9'] = "111101111001111",
        ['A'] = "010101111101101", ['B'] = "110101110101110", ['C'] = "111100100100111", ['D'] = "110101101101110", ['E'] = "111100110100111", ['F'] = "111100110100100", ['G'] = "111100101101111", ['H'] = "101101111101101", ['I'] = "111010010010111", ['J'] = "001001001101111",
        ['K'] = "101101110101101", ['L'] = "100100100100111", ['M'] = "101111111101101", ['N'] = "110101101101101", ['O'] = "111101101101111", ['P'] = "111101111100100", ['Q'] = "010101101110011", ['R'] = "110101110101101", ['S'] = "111100111001111", ['T'] = "111010010010010",
        ['U'] = "101101101101111", ['V'] = "101101101101010", ['W'] = "101101111111101", ['X'] = "101101010101101", ['Y'] = "101101010010010", ['Z'] = "111001010100111", ['!'] = "010010010000010", ['.'] = "000000000000010", ['-'] = "000000111000000", ['+'] = "000010111010000",
        [':'] = "000010000010000", ['/'] = "001001010100100", ['?'] = "111001011000010", ['\''] = "010010000000000",
    };
    public static double TextW(string s, double sc) => s.Length * 4 * sc - sc;
    public static int Glyphs_Count() => Glyphs.Count;
    public static void Text(TownScene S, double x, double y, string str, double c, double sc = 1, double shadow = 0)
    {
        if (shadow != 0) Text(S, x + sc, y + sc, str, shadow, sc);
        foreach (char ch in str.ToUpperInvariant())
        {
            if (Glyphs.TryGetValue(ch, out var g)) for (int i = 0; i < 15; i++) if (g[i] == '1') S.P(x + (i % 3) * sc, y + (i / 3) * sc, sc, sc, c);
            x += 4 * sc;
        }
    }
    public static void Bird(TownScene S, double x, double y, int skin, int wing, long now)
    {
        double cW;
        if (skin == 2) { S.P(x + 1, y, 5, 4, 0xFF8A5A3A); S.P(x + 2, y - 1, 3, 1, 0xFF8A5A3A); S.P(x + 1, y + 2, 3, 2, 0xFFE8574A); S.P(x + 2, y + 4, 3, 1, 0xFF6A4A2A); S.P(x + 4, y, 2, 1, 0xFFFFFFFF); S.P(x + 5, y, 1, 1, 0xFF23263A); S.P(x + 6, y + 2, 2, 1, 0xFFE8A64A); cW = 0xFF5A3E2E; }
        else if (skin == 3) { S.P(x + 1, y, 5, 4, 0xFF3F7FC9); S.P(x + 2, y - 1, 3, 1, 0xFF3F7FC9); S.P(x + 3, y - 2, 1, 1, 0xFF3F7FC9); S.P(x + 4, y - 3, 1, 1, 0xFF2F5F99); S.P(x + 1, y + 3, 5, 1, 0xFFDDEEFF); S.P(x + 3, y, 3, 1, 0xFF23263A); S.P(x + 4, y, 1, 1, 0xFFFFFFFF); S.P(x + 6, y + 2, 2, 1, 0xFF23263A); cW = 0xFF7FB7E0; }
        else if (skin == 4) { S.P(x + 1, y, 5, 4, TownScene.Alpha(0xFFF4F1E8, 200)); S.P(x + 2, y - 1, 3, 1, TownScene.Alpha(0xFFF4F1E8, 200)); S.P(x + 1, y + 4, 1, 1, TownScene.Alpha(0xFFF4F1E8, 160)); S.P(x + 3, y + 4, 1, 1, TownScene.Alpha(0xFFF4F1E8, 160)); S.P(x + 5, y + 4, 1, 1, TownScene.Alpha(0xFFF4F1E8, 160)); S.P(x + 3, y + 1, 1, 1, 0xFF23263A); S.P(x + 5, y + 1, 1, 1, 0xFF23263A); S.P(x + 4, y + 2, 1, 1, TownScene.Alpha(0xFF23263A, 120)); cW = TownScene.Alpha(0xFFDDDAD0, 200); }
        else if (skin == 5) { S.P(x + 1, y, 5, 4, 0xFFFFD86B); S.P(x + 2, y - 1, 3, 1, 0xFFFFD86B); S.P(x + 2, y, 2, 1, 0xFFFFF0B8); S.P(x + 1, y + 3, 5, 1, 0xFFE8A64A); S.P(x + 4, y, 2, 1, 0xFFFFFFFF); S.P(x + 5, y, 1, 1, 0xFF23263A); S.P(x + 6, y + 2, 2, 1, 0xFFE0872E); S.P(x + 3 + (now / 180) % 3, y - 2, 1, 1, 0xFFFFFFFF); cW = 0xFFE8B84A; }
        else if (skin == 6) { S.P(x, y - 1, 7, 6, TownScene.Alpha(0xFF9FD3F0, 40)); S.P(x + 1, y, 5, 4, 0xFFB8A6FF); S.P(x + 2, y - 1, 3, 1, 0xFFB8A6FF); S.P(x + 1, y + 3, 5, 1, 0xFF8A6AFF); S.P(x + 4, y, 2, 1, 0xFFEAF6FF); S.P(x + 5, y, 1, 1, 0xFF2A2436); S.P(x + 6, y + 2, 2, 1, 0xFF9FD3F0); cW = 0xFF9FD3F0; }
        else { S.P(x + 1, y, 5, 4, 0xFFFFE08A); S.P(x + 2, y - 1, 3, 1, 0xFFFFE08A); S.P(x + 2, y + 4, 3, 1, 0xFFE8C95A); S.P(x + 4, y, 2, 1, 0xFFFFFFFF); S.P(x + 5, y, 1, 1, 0xFF23263A); S.P(x + 6, y + 2, 2, 1, 0xFFE8A64A); cW = 0xFFE8C95A; }
        if (wing == 0) { S.P(x, y, 3, 1, cW); S.P(x + 1, y - 1, 2, 1, cW); }
        else if (wing == 2) { S.P(x, y + 3, 3, 1, cW); S.P(x + 1, y + 4, 2, 1, cW); }
        else S.P(x, y + 1, 3, 2, cW);
    }
    static double W(double v, double m) => ((v % m) + m) % m;
    /// <summary>FlappyDraw: the world scrolling with gx, the pipes and their coins, the ground, the bird with its wing pose, the trail, the hud, the idle card, GAME OVER, the SHOP.</summary>
    public static void Draw(HubSurface hub, double ox, double oy, double cs, uint acc, double f, long now)
    {
        var wd = World(world);
        double dt = lastT != 0 ? Math.Min(now - lastT, 40) : 0;
        lastT = now;
        bool live = on && !dead && !shop;
        double gap = 26 - Math.Min(score, 30) * 0.2, spd = 0.028 + Math.Min(score, 40) * 0.00018;
        if (live)
        {
            run += (int)dt;
            vy = Math.Min(vy + 0.00019 * dt, 0.11); y += vy * dt; gx += spd * dt;
            if (y < 0) { y = 0; vy = 0; }
            if (now >= spawnAt) { pipes.Add(new FPipe { x = 150.0, gy = _rnd.Next(18, (int)Math.Round(84 - gap - 8) + 1), coin = _rnd.Next(1, 11) <= 6 }); spawnAt = now + 1700; }
            foreach (var pp in pipes) pp.x -= spd * dt;
            while (pipes.Count > 0 && pipes[0].x < -12) pipes.RemoveAt(0);
            double bx = 30, by = Math.Round(y);
            foreach (var pp in pipes)
            {
                if (!pp.passed && pp.x + 10 < bx) { pp.passed = true; score += 1; }
                double gb = pp.gy + Math.Round(gap);
                if (bx + 6 > pp.x && bx + 1 < pp.x + 10 && (by < pp.gy || by + 4 > gb)) Die(now);
                if (pp.coin && Math.Abs(pp.x + 5 - (bx + 3)) < 4 && Math.Abs(pp.gy + Math.Round(gap / 2) - (by + 2)) < 5)
                {
                    pp.coin = false; coins += 1; sparkAt = now; spx = Math.Round(pp.x) + 5; spy = pp.gy + Math.Round(gap / 2);
                    Ini.Write(Paths.IniFile, "flappy", "coins", (long)coins);
                }
            }
            if (by + 4 >= 88) Die(now);
            if (bird == 5 || bird == 6) { trail.Add((29, y + 2, now)); while (trail.Count > 0 && now - trail[0].at > 360) trail.RemoveAt(0); }
        }
        else if (dead) { if (y + 4 < 88) { vy = Math.Min(vy + 0.00019 * dt, 0.11); y = Math.Min(y + vy * dt, 84.0); } }
        else if (!on) y = 46 + 2 * Math.Sin(now * 0.004);
        double shk = dead && now - deadAt < 220 ? Math.Round(2.5 * Math.Sin((now - deadAt) * 0.09) * (1 - (now - deadAt) / 220.0)) : 0;
        ox += shk;
        var S = new TownScene(hub, ox, oy, cs, acc, f, now);
        S.SkyBands(ox, oy, cs, 88, wd.sT, wd.sB, f, 8);
        if (wd.stars) for (int q = 1; q <= 30; q++) S.P(W(q * 37 + 11 - Math.Round(gx * 0.05), 142), (q * 23 + 5) % 60 + 1, 1, 1, TownScene.Alpha(0xFFF4F1E8, Math.Round(90 + 130 * Math.Abs(Math.Sin(now * 0.0029 + q * 1.3)))));
        if (wd.sun != 0)
        {
            double sx0 = world == 3 ? 110 : 112, sy0 = world == 3 ? 14 : 12;
            S.P(sx0 - 3, sy0 - 2, 7, 5, wd.sun); S.P(sx0 - 2, sy0 - 3, 5, 7, wd.sun);
            if (world == 3) S.P(sx0 - 2, sy0 - 2, 5, 5, wd.sT);
        }
        if (world == 5) for (int q = 1; q <= 18; q++) { double sh = 4 + (q * 5) % 9; S.P(W((q - 1) * 8 + (q * 3) % 5 - Math.Round(gx * 0.2), 150) - 4, 0, 3, sh, 0xFF241E2E); S.P(W((q - 1) * 8 + (q * 3) % 5 - Math.Round(gx * 0.2), 150) - 3, sh, 1, 2, 0xFF241E2E); }
        else for (int q = 1; q <= 5; q++) S.TownCloud(null!, W(q * 37 - Math.Round(gx * 0.15), 170) - 20, 6 + (q * 9) % 22, 10 + (q % 3) * 4, wd.cT, wd.cU);
        for (int q = 1; q <= 36; q++) { double hh = 8 + Math.Round(6 * Math.Sin(q * 0.6) + 3 * Math.Sin(q * 1.4 + 1)); S.P(W((q - 1) * 4 - Math.Round(gx * 0.3), 144) - 2, 88 - hh, 4, hh, wd.hill); }
        if (world == 4) for (int q = 1; q <= 40; q++) { double fp = (now * 0.00008 + q * 0.137) % 1.0; S.P(W(q * 37 + Math.Round(fp * 14) - Math.Round(gx * 0.1), 142), Math.Round(fp * 88), 1, 1, TownScene.Alpha(0xFFFFFFFF, 200)); }
        foreach (var pp in pipes)
        {
            double px = Math.Round(pp.x), gb = pp.gy + Math.Round(gap);
            S.P(px, 0, 10, pp.gy, wd.pipe); S.P(px, 0, 2, pp.gy, wd.pL); S.P(px + 8, 0, 2, pp.gy, wd.pD);
            S.P(px - 1, pp.gy - 3, 12, 3, wd.pipe); S.P(px - 1, pp.gy - 3, 12, 1, wd.pL); S.P(px - 1, pp.gy - 1, 12, 1, wd.pD);
            S.P(px, gb, 10, 88 - gb, wd.pipe); S.P(px, gb, 2, 88 - gb, wd.pL); S.P(px + 8, gb, 2, 88 - gb, wd.pD);
            S.P(px - 1, gb, 12, 3, wd.pipe); S.P(px - 1, gb, 12, 1, wd.pL); S.P(px - 1, gb + 2, 12, 1, wd.pD);
            if (world == 3) { S.P(px + 3, pp.gy - 2, 4, 1, 0xFFFFD98A); S.P(px + 3, gb + 1, 4, 1, 0xFFFFD98A); }
            if (pp.coin)
            {
                double cy0 = pp.gy + Math.Round(gap / 2); int cw = new[] { 3, 2, 1, 2 }[(int)((now / 110) % 4)];
                S.P(px + 5 - cw / 2, cy0 - 1, cw, 3, 0xFFFFD86B); S.P(px + 5 - cw / 2, cy0 - 2, cw, 1, 0xFFE8A64A); S.P(px + 5 - cw / 2, cy0 + 2, cw, 1, 0xFFE8A64A);
                if (cw == 3) S.P(px + 5, cy0, 1, 1, 0xFFFFF0B8);
            }
        }
        S.P(0, 88, 142, 13, wd.gnd); S.P(0, 88, 142, 1, wd.gT); S.P(0, 89, 142, 1, TownScene.Mix(wd.gT, 0xFF000000, 0.2));
        for (int q = 1; q <= 20; q++) S.P(W((q - 1) * 8 - Math.Round(gx), 152) - 8, 92, 4, 1, wd.gS);
        for (int q = 1; q <= 20; q++) S.P(W((q - 1) * 8 + 4 - Math.Round(gx), 152) - 8, 96, 4, 1, wd.gS);
        if (world == 5) for (int q = 1; q <= 6; q++) { S.P(W((q - 1) * 24 + 6 - Math.Round(gx), 152) - 8, 86, 1, 2, Col.AccHi(acc, 0.4)); S.P(W((q - 1) * 24 + 5 - Math.Round(gx), 152) - 8, 87, 3, 1, acc); }
        foreach (var tr in trail) { double ta = 1 - (now - tr.at) / 360.0; S.P(Math.Round(tr.x - (now - tr.at) * spd), Math.Round(tr.y), 1, 1, TownScene.Alpha(bird == 5 ? 0xFFFFE08A : 0xFF9FD3F0, Math.Round(200 * ta))); }
        double byd = Math.Round(y);
        int wing = now - flapAt < 160 ? 0 : vy < 0 ? 1 : 2;
        Bird(S, 29, byd, bird, wing, now);
        if (dead && y + 4 >= 84) { S.P(31, byd - 3, 1, 1, 0xFFFFFFFF); S.P(33, byd - 4, 1, 1, 0xFFFFFFFF); S.P(35, byd - 3, 1, 1, 0xFFFFFFFF); }
        if (sparkAt != 0 && now - sparkAt < 380)
        {
            double e = (now - sparkAt) / 380.0;
            for (int q = 0; q < 6; q++) { double an = q * 1.047; S.P(spx + Math.Round((2 + 5 * e) * Math.Cos(an)) - Math.Round(spd * (now - sparkAt)), spy + Math.Round((2 + 5 * e) * Math.Sin(an)), 1, 1, TownScene.Alpha(0xFFFFE08A, Math.Round(230 * (1 - e)))); }
        }
        else if (sparkAt != 0) sparkAt = 0;
        const double inkS = 0xFF23263A;
        if (on && !shop) { string sc = score.ToString(); Text(S, 71 - Math.Floor(TextW(sc, 3) / 2), 6, sc, 0xFFFFFFFF, 3, TownScene.Alpha(inkS, 140)); }
        if (!shop) { S.P(126, 5, 3, 3, 0xFFFFD86B); S.P(127, 6, 1, 1, 0xFFFFF0B8); S.P(126, 4, 3, 1, 0xFFE8A64A); S.P(126, 8, 3, 1, 0xFFE8A64A); Text(S, 131, 4, coins.ToString(), 0xFFFFF4C8, 1, TownScene.Alpha(inkS, 140)); }
        if (!on && !shop)
        {
            S.P(26, 22, 90, 26, TownScene.Alpha(0xFF12141F, 170)); S.P(26, 22, 90, 1, TownScene.Alpha(0xFFFFE08A, 120)); S.P(26, 47, 90, 1, TownScene.Alpha(0xFFFFE08A, 120)); S.P(26, 22, 1, 26, TownScene.Alpha(0xFFFFE08A, 120)); S.P(115, 22, 1, 26, TownScene.Alpha(0xFFFFE08A, 120));
            Text(S, 71 - Math.Floor(TextW("TAP TO FLY", 2) / 2), 26 + (now / 500) % 2, "TAP TO FLY", 0xFFFFFFFF, 2, TownScene.Alpha(inkS, 140));
            Text(S, 71 - Math.Floor(TextW("BEST " + best, 1) / 2), 40, "BEST " + best, 0xFFFFE08A, 1);
        }
        if (dead && !shop)
        {
            double e = Ease3(Math.Min((now - deadAt) / 420.0, 1.0)), py0 = 22 - Math.Round(6 * (1 - e));
            S.P(31, py0, 80, 50, TownScene.Alpha(0xFF12141F, Math.Round(190 * e))); S.P(31, py0, 80, 1, TownScene.Alpha(0xFFFFE08A, Math.Round(150 * e))); S.P(31, py0 + 49, 80, 1, TownScene.Alpha(0xFFFFE08A, Math.Round(150 * e))); S.P(31, py0, 1, 50, TownScene.Alpha(0xFFFFE08A, Math.Round(150 * e))); S.P(110, py0, 1, 50, TownScene.Alpha(0xFFFFE08A, Math.Round(150 * e)));
            Text(S, 71 - Math.Floor(TextW("GAME OVER", 2) / 2), py0 + 4, "GAME OVER", TownScene.Alpha(0xFFFFFFFF, Math.Round(240 * e)), 2, TownScene.Alpha(inkS, Math.Round(140 * e)));
            bool nb = bestAt != 0 && bestAt == deadAt;
            double mc = score >= 50 ? 0xFFFFD86B : score >= 25 ? 0xFFD8DEE6 : score >= 10 ? 0xFFC98A5A : 0;
            if (mc != 0) { S.P(38, py0 + 17, 12, 2, 0xFFE8574A); S.P(40, py0 + 20, 8, 8, mc); S.P(41, py0 + 19, 6, 10, mc); S.P(39, py0 + 21, 10, 6, mc); S.P(42, py0 + 22, 2, 2, 0xFFFFFFFF); }
            Text(S, 53, py0 + 18, "SCORE " + score, TownScene.Alpha(0xFFFFF4C8, Math.Round(230 * e)), 1);
            Text(S, 53, py0 + 25, nb ? "NEW BEST!" : "BEST " + best, TownScene.Alpha(nb ? 0xFFFFE08A : 0xFFFFF4C8, Math.Round(230 * e)), 1);
            Text(S, 53, py0 + 32, "+" + bonus + " COINS", TownScene.Alpha(0xFFFFD86B, Math.Round(230 * e)), 1);
            if ((now / 450) % 2 != 0) Text(S, 71 - Math.Floor(TextW("TAP TO RETRY", 1) / 2), py0 + 41, "TAP TO RETRY", TownScene.Alpha(0xFFFFF4C8, Math.Round(200 * e)), 1);
        }
        if (!live && !shop)
        {
            double hvS = hub.Hv(2239);
            S.P(54, 76, 34, 10, TownScene.Alpha(0xFF12141F, Math.Round(170 + 60 * hvS))); S.P(54, 76, 34, 1, TownScene.Alpha(0xFFFFE08A, Math.Round(140 + 100 * hvS))); S.P(54, 85, 34, 1, TownScene.Alpha(0xFFFFE08A, Math.Round(140 + 100 * hvS))); S.P(54, 76, 1, 10, TownScene.Alpha(0xFFFFE08A, Math.Round(140 + 100 * hvS))); S.P(87, 76, 1, 10, TownScene.Alpha(0xFFFFE08A, Math.Round(140 + 100 * hvS)));
            Text(S, 60, 78, "SHOP", 0xFFFFFFFF, 1);
            S.P(80, 79, 3, 3, 0xFFFFD86B); S.P(81, 80, 1, 1, 0xFFFFF0B8); S.P(80, 78, 3, 1, 0xFFE8A64A); S.P(80, 82, 3, 1, 0xFFE8A64A);
        }
        if (shop)
        {
            S.P(6, 4, 130, 96, TownScene.Alpha(0xFF12141F, 228)); S.P(6, 4, 130, 1, 0xFFFFE08A); S.P(6, 99, 130, 1, 0xFFFFE08A); S.P(6, 4, 1, 96, 0xFFFFE08A); S.P(135, 4, 1, 96, 0xFFFFE08A);
            Text(S, 11, 7, "SHOP", 0xFFFFFFFF, 2, TownScene.Alpha(inkS, 140));
            S.P(64, 9, 3, 3, 0xFFFFD86B); S.P(65, 10, 1, 1, 0xFFFFF0B8); S.P(64, 8, 3, 1, 0xFFE8A64A); S.P(64, 12, 3, 1, 0xFFE8A64A);
            Text(S, 69, 8, coins.ToString(), 0xFFFFE08A, 1);
            double hvB = hub.Hv(2238);
            S.P(104, 6, 28, 10, TownScene.Alpha(0xFF1E2238, Math.Round(200 + 40 * hvB))); S.P(104, 6, 28, 1, 0xFFFFE08A); S.P(104, 15, 28, 1, 0xFFFFE08A); S.P(104, 6, 1, 10, 0xFFFFE08A); S.P(131, 6, 1, 10, 0xFFFFE08A);
            Text(S, 118 - Math.Floor(TextW("BACK", 1) / 2), 9, "BACK", 0xFFFFFFFF, 1);
            Text(S, 11, 20, "BIRDS", 0xFFFFE08A, 1); Text(S, 11, 57, "WORLDS", 0xFFFFE08A, 1);
            for (int kind = 1; kind <= 2; kind++)
            {
                var lst = kind == 1 ? BIRDS : WORLDS; int own = kind == 1 ? birds : worlds, eq = kind == 1 ? bird : world;
                double ty0 = kind == 1 ? 26 : 63, x00 = kind == 1 ? 8 : 18;
                for (int i = 1; i <= lst.Length; i++)
                {
                    var it = lst[i - 1];
                    double tx0 = x00 + (i - 1) * 21; int z = (kind == 1 ? 2240 : 2250) + i;
                    double hv = hub.Hv(z);
                    bool has = (own & (1 << (i - 1))) != 0;
                    double sh2 = bought == -i * kind && now - boughtAt < 400 ? Math.Round(1.5 * Math.Sin((now - boughtAt) * 0.06)) : 0;
                    double jb = bought == i * kind && now - boughtAt < 500 ? Math.Round(2 * Math.Sin((now - boughtAt) / 500.0 * 3.14159)) : 0;
                    double cB = eq == i ? 0xFFFFE08A : has ? 0xFF8A8FA8 : 0xFF3A3F5C, tx = tx0 + sh2, ty = ty0 - jb;
                    S.P(tx, ty, 20, 28, TownScene.Alpha(0xFF1E2238, Math.Round(200 + 40 * hv))); S.P(tx, ty, 20, 1, cB); S.P(tx, ty + 27, 20, 1, cB); S.P(tx, ty, 1, 28, cB); S.P(tx + 19, ty, 1, 28, cB);
                    if (kind == 1) Bird(S, tx + 6, ty + 6 + Math.Round(Math.Sin(now * 0.004 + i) * 0.6), i, (int)((now / 220 + i) % 3), now);
                    else
                    {
                        var w2 = World(i);
                        S.P(tx + 2, ty + 2, 16, 6, w2.sT); S.P(tx + 2, ty + 6, 16, 4, w2.sB); S.P(tx + 2, ty + 9, 16, 2, w2.hill); S.P(tx + 2, ty + 11, 16, 2, w2.gnd); S.P(tx + 2, ty + 11, 16, 1, w2.gT);
                        S.P(tx + 9, ty + 2, 2, 5, w2.pipe); S.P(tx + 9, ty + 10, 2, 1, w2.pipe);
                    }
                    Text(S, tx + 10 - Math.Floor(TextW(it.n, 1) / 2), ty + 15, it.n, has ? 0xFFFFFFFF : 0xFF8A8FA8, 1);
                    if (eq == i) Text(S, tx + 10 - Math.Floor(TextW("ON", 1) / 2), ty + 22, "ON", 0xFFFFE08A, 1);
                    else if (has) Text(S, tx + 10 - Math.Floor(TextW("USE", 1) / 2), ty + 22, "USE", 0xFFB9DCF2, 1);
                    else
                    {
                        string pr = it.p.ToString();
                        S.P(tx + 10 - Math.Floor(TextW(pr, 1) / 2) - 4, ty + 22, 3, 3, coins >= it.p ? 0xFFFFD86B : 0xFF6A6F8E);
                        Text(S, tx + 10 - Math.Floor(TextW(pr, 1) / 2), ty + 22, pr, coins >= it.p ? 0xFFFFD86B : 0xFF6A6F8E, 1);
                    }
                }
            }
        }
        if (bought != 0 && now - boughtAt > 520) bought = 0;
    }
    /// <summary>The scene's zones while FLAPPY is up: the shop's BACK and tiles, the SHOP button, the scene itself (a tap).</summary>
    public static int Zone(HubLayout HL, double ux, double uy)
    {
        double cxc = (ux - HL.ctx - 1) / 4.0, cyc = (uy - HL.cty - 36) / 4.0;
        if (shop)
        {
            if (cxc >= 104 && cxc <= 132 && cyc >= 6 && cyc <= 16) return 2238;
            for (int kind = 1; kind <= 2; kind++)
            {
                double ty0 = kind == 1 ? 26 : 63, x00 = kind == 1 ? 8 : 18; int nI = kind == 1 ? 6 : 5;
                if (cyc >= ty0 && cyc <= ty0 + 28) for (int i = 1; i <= nI; i++) { double tx0 = x00 + (i - 1) * 21; if (cxc >= tx0 && cxc <= tx0 + 20) return (kind == 1 ? 2240 : 2250) + i; }
            }
            return 2237;
        }
        if ((!on || dead) && cxc >= 54 && cxc <= 88 && cyc >= 76 && cyc <= 86) return 2239;
        return 2226;
    }
    public static bool Click(HubSurface hub, int z)
    {
        switch (z)
        {
            case 2226: Tap(); return true;
            case 2239: shop = true; hub.Tim(Pace.TICK_A); return true;
            case 2238: shop = false; hub.Tim(Pace.TICK_A); return true;
            case 2237: return true;
            case >= 2241 and <= 2246: Buy(1, z - 2240); return true;
            case >= 2251 and <= 2255: Buy(2, z - 2250); return true;
        }
        return false;
    }
}
