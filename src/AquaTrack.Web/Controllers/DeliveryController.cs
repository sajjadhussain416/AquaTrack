using AquaTrack.Application.Common;
using AquaTrack.Application.Operations;
using AquaTrack.Web.Models.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AquaTrack.Web.Controllers;

/// <summary>
/// The route screen. Delivery staff see today's list for their areas (SRS DLV-01..06).
/// Owners and managers can open any area and any past date to enter on their behalf.
/// </summary>
[Authorize(Roles = Roles.DeliveryBoy + "," + Roles.OwnerOrManager)]
public class DeliveryController : Controller
{
    private readonly IDeliveryService _deliveries;
    private readonly IStringLocalizer<SharedResource> _l;

    public DeliveryController(IDeliveryService deliveries, IStringLocalizer<SharedResource> l)
    {
        _deliveries = deliveries;
        _l = l;
    }

    private bool IsDeliveryBoy => User.IsInRole(Roles.DeliveryBoy);

    public async Task<IActionResult> Index(int? areaId, DateOnly? date, CancellationToken ct)
    {
        var today = PakistanTime.Today;
        var day = IsDeliveryBoy || date is null || date > today ? today : date.Value;

        return View(new RoutePageViewModel
        {
            Route = await _deliveries.GetRouteAsync(day, areaId, ct),
            CanPickDate = !IsDeliveryBoy,
            Today = today
        });
    }

    /// <summary>Saves one card. Returns the refreshed card HTML for the route screen's script.</summary>
    [HttpPost]
    public async Task<IActionResult> Save(DeliveryFormModel form, int? areaId, CancellationToken ct)
    {
        var result = await _deliveries.SaveAsync(form.ToEntry(), ct);
        var isScript = Request.Headers["X-Requested-With"] == "fetch";

        if (!result.Succeeded)
        {
            var message = _l[result.Errors[0]].Value;
            if (isScript) return BadRequest(new { message });

            TempData["Error"] = message;
            return RedirectToAction(nameof(Index), new { areaId, date = form.Date.ToString("yyyy-MM-dd") });
        }

        if (isScript)
        {
            ViewData["RouteDate"] = form.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            return PartialView("_Stop", result.Value);
        }

        TempData["Success"] = _l["Saved."].Value;
        return RedirectToAction(nameof(Index), new { areaId, date = form.Date.ToString("yyyy-MM-dd") });
    }
}
