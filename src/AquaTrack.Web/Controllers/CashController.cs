using AquaTrack.Application.Common;
using AquaTrack.Application.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AquaTrack.Web.Controllers;

/// <summary>Daily cash sheet of delivery staff and cash handed to the owner (SRS STAFF-02).</summary>
[Authorize(Roles = Roles.OwnerOrManager)]
public class CashController : Controller
{
    private readonly ICashService _cash;
    private readonly IStringLocalizer<SharedResource> _l;

    public CashController(ICashService cash, IStringLocalizer<SharedResource> l)
    {
        _cash = cash;
        _l = l;
    }

    public async Task<IActionResult> Index(DateOnly? date, CancellationToken ct)
    {
        var today = PakistanTime.Today;
        var day = date is null || date > today ? today : date.Value;

        ViewBag.Today = today;
        ViewBag.Handovers = await _cash.GetHandoversAsync(day, ct);
        return View(await _cash.GetDayAsync(day, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Handover(string staffId, DateOnly date, decimal amount, string? note, CancellationToken ct)
    {
        var result = await _cash.RecordHandoverAsync(staffId, date, amount, note, ct);
        if (result.Succeeded) TempData["Success"] = _l["Cash received from staff saved."].Value;
        else TempData["Error"] = _l[result.Errors[0]].Value;
        return RedirectToAction(nameof(Index), new { date = Fmt.Iso(date) });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteHandover(int id, DateOnly date, CancellationToken ct)
    {
        var result = await _cash.DeleteHandoverAsync(id, ct);
        if (result.Succeeded) TempData["Success"] = _l["Entry removed."].Value;
        else TempData["Error"] = _l[result.Errors[0]].Value;
        return RedirectToAction(nameof(Index), new { date = Fmt.Iso(date) });
    }
}
