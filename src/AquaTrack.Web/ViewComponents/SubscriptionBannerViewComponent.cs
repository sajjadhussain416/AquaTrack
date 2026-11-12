using AquaTrack.Application.Common;
using AquaTrack.Application.Plants;
using Microsoft.AspNetCore.Mvc;

namespace AquaTrack.Web.ViewComponents;

/// <summary>
/// Reminds the plant owner when the AquaTrack subscription or free trial is ending (within 7 days) or overdue.
/// </summary>
public class SubscriptionBannerViewComponent : ViewComponent
{
    private readonly ISubscriptionService _subs;
    private readonly IConfiguration _config;

    public SubscriptionBannerViewComponent(ISubscriptionService subs, IConfiguration config)
    {
        _subs = subs;
        _config = config;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!HttpContext.User.IsInRole(Roles.PlantOwner)) return Content(string.Empty);

        var mine = await _subs.GetMineAsync(HttpContext.RequestAborted);
        if (mine is null || mine.DaysLeft is null || mine.DaysLeft > 7) return Content(string.Empty);

        ViewBag.SupportPhone = PhoneNumber.Normalize(_config["SuperAdmin:Phone"]);
        return View(mine);
    }
}
