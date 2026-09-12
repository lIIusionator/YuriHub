namespace Yuri.Platform;

/// <summary>Platform gates. Windows-only rows check IsWin at the row and say why they are off elsewhere.</summary>
public static class Os
{
    public static readonly bool IsWin = OperatingSystem.IsWindows();
    public static readonly bool IsMac = OperatingSystem.IsMacOS();
    /// <summary>The Roblox client's process name, without extension.</summary>
    public static string RobloxProcess => IsMac ? "RobloxPlayer" : "RobloxPlayerBeta";
    public static string RobloxStudioProcess => IsMac ? "RobloxStudio" : "RobloxStudioBeta";
}
