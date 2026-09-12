namespace Yuri.Core;

/// <summary>CHANGELOG: what the dashboard's WHAT'S NEW and the UPDATE LOGS page list. "+" added, "*" changed.</summary>
public static class Changelog
{
    public static readonly (string kind, string text)[] Entries =
    {
        ("*", "YURI is now a built application, not a script"),
        ("*", "one build for Windows and macOS - no AutoHotkey needed"),
        ("*", "hub performance optimizations"),
        ("*", "PERFORMANCE mode - further improved tweaks"),
        ("+", "FLAPPY - now with a progression system"),
        ("+", "SPECIAL - DISABLE CRASH HANDLER switch"),
        ("+", "SPECIAL - MEMORY TRIMMER, with interval and limit chips"),
        ("+", "SPECIAL - SERVER DETAILS: type, uptime, REJOIN, invite link"),
        ("+", "SPECIAL - NO DESKTOP APP, ROBLOX CLEANER, LAUNCH ON STARTUP"),
        ("+", "SPECIAL - ROBLOX APP THEME, old sounds, old avatar scene"),
        ("+", "SPECIAL - CUSTOM DEATH SOUND and EMOJI FONT mods"),
        ("*", "community flags now update the moment the repo does"),
    };
}
