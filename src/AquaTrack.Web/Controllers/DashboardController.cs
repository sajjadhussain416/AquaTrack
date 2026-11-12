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
    private readonly IPaymentService _payments;

    public DashboardController(IPlantDashboardService dashboard, IBillingService billing, IPaymentService payments)
    {
        _dashboard = dashboard;
        _billing = billing;
        _payments = payments;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Pending = await _billing.GetTotalPendingAsync(ct);
        ViewBag.PendingChecks = await _payments.GetPendingCountAsync(ct);
        return View(await _dashboard.GetSummaryAsync(ct));
    }
}
