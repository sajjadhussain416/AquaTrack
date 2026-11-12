namespace AquaTrack.Web;

/// <summary>
/// Identifies this run of the app. Logins made without "Keep me signed in" carry it,
/// so they stop working when the app is stopped and started again.
/// </summary>
public static class AppSession
{
    public const string BootClaim = "app_boot";
    public static readonly string BootId = Guid.NewGuid().ToString("N");
}
