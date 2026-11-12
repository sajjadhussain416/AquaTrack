using AquaTrack.Application.Billing;
using AquaTrack.Application.Common;
using AquaTrack.Application.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaTrack.Web.Controllers;

/// <summary>Plant owner / manager home (SRS RPT-01).</summary>
[Authorize(Roles = Roles.OwnerOrManager)]
public class DashboardController : Controller
{
    private readonly IPlantDashboardService _dashboard;
    private readonly IBillingService _billing;

    public DashboardController(IPlantDashboardService dashboard, IBillingService billing)
    {
        _dashboard = dashboard;
        _billing = billing;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Pending = await _billing.GetTotalPendingAsync(ct);
        return View(await _dashboard.GetSummaryAsync(ct));
    }
}
