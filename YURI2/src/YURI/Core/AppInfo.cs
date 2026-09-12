namespace Yuri.Core;

/// <summary>APP_VERSION / APP_STAGE / ZVER / APPNAME from the top of YURI.ahk.</summary>
public static class AppInfo
{
    public const string AppName = "YURI";                       // APPNAME
    // Bump for every release. The updater compares this against the repo's
    // version manifest (see Updater); the assembly Version in YURI.csproj is
    // the file version and should move with it.
    public const string Version = "2.0.0";                      // APP_VERSION
    public const string Stage   = "";                           // APP_STAGE: "" once it is not a beta
    public static string ZVer => "v" + Version + (Stage != "" ? " " + Stage : "");   // ZVER
}
