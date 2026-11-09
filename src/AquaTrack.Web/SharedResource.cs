using System.Globalization;

namespace AquaTrack.Web;

/// <summary>
/// Marker class for the shared Urdu/English strings in Resources/SharedResource.ur.resx.
/// Keys are the English text, so English needs no resource file.
/// Use in views: @L["Sign in"]   (L is injected in _ViewImports)
/// </summary>
public class SharedResource
{
}

public static class AppCultures
{
    public const string English = "en";
    public const string Urdu = "ur";

    public static bool IsUrdu => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == Urdu;
    public static string Direction => IsUrdu ? "rtl" : "ltr";
}
