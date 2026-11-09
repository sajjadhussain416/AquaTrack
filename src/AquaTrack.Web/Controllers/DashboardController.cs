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

    public DashboardController(IPlantDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await _dashboard.GetSummaryAsync(ct));
}
