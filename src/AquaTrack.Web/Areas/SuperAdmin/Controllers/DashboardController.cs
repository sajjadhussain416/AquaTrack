using AquaTrack.Application.Common;
using AquaTrack.Application.Plants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaTrack.Web.Areas.SuperAdmin.Controllers;

/// <summary>Platform overview for the product owner (SRS SUB-04).</summary>
[Area("SuperAdmin")]
[Authorize(Roles = Roles.SuperAdmin)]
public class DashboardController : Controller
{
    private readonly IPlantService _plants;
    private readonly ISubscriptionService _subs;

    public DashboardController(IPlantService plants, ISubscriptionService subs)
    {
        _plants = plants;
        _subs = subs;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Recent = await _plants.GetRecentPlantsAsync(5, ct);
        ViewBag.Subs = await _subs.GetOverviewAsync("all", ct);
        return View(await _plants.GetPlatformStatsAsync(ct));
    }
}
