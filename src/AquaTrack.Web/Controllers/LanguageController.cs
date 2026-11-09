using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace AquaTrack.Web.Controllers;

/// <summary>Switches the UI between English and Urdu. The choice is kept in a cookie for a year.</summary>
[AllowAnonymous]
public class LanguageController : Controller
{
    [HttpPost]
    public IActionResult Set(string culture, string? returnUrl)
    {
        if (culture != AppCultures.English && culture != AppCultures.Urdu)
        {
            culture = AppCultures.English;
        }

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax
            });

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "~/");
    }
}
