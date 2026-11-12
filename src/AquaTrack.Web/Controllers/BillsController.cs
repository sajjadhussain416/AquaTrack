using AquaTrack.Application.Billing;
using AquaTrack.Application.Common;
using AquaTrack.Domain.Enums;
using AquaTrack.Web.Models.Billing;
using AquaTrack.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AquaTrack.Web.Controllers;

/// <summary>Monthly bills: generate, review, adjust, print, send on WhatsApp (SRS BILL-01..07).</summary>
[Authorize(Roles = Roles.OwnerOrManager)]
public class BillsController : Controller
{
    private readonly IBillingService _billing;
    private readonly WhatsAppMessages _whatsApp;
    private readonly IStringLocalizer<SharedResource> _l;

    public BillsController(IBillingService billing, WhatsAppMessages whatsApp, IStringLocalizer<SharedResource> l)
    {
        _billing = billing;
        _whatsApp = whatsApp;
        _l = l;
    }

    private static BillPeriod PeriodOf(int? year, int? month) =>
        year.HasValue && month.HasValue && BillPeriod.IsValid(year.Value, month.Value)
            ? new BillPeriod(year.Value, month.Value)
            : BillPeriod.Default;

    public async Task<IActionResult> Index(int? year, int? month, string? q, int? areaId, BillStatus? status, CancellationToken ct)
    {
        var period = PeriodOf(year, month);

        return View(new BillsIndexViewModel
        {
            Month = await _billing.GetMonthAsync(period, q, areaId, status, ct),
            Search = q,
            AreaId = areaId,
            Status = status,
            Latest = BillPeriod.Of(PakistanTime.Today)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Generate(int year, int month, CancellationToken ct)
    {
        var period = PeriodOf(year, month);

        if (period.Start > PakistanTime.Today)
        {
            TempData["Error"] = _l["Bills cannot be made for a future month."].Value;
            return RedirectToAction(nameof(Index), new { year, month });
        }

        var r = await _billing.GenerateAsync(period, ct);

        TempData["Success"] = _l["Bills ready: {0} new, {1} refreshed, {2} already sent (not changed).", r.Created, r.Updated, r.SkippedSent].Value;
        return RedirectToAction(nameof(Index), new { year = period.Year, month = period.Month });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var bill = await _billing.GetAsync(id, ct);
        if (bill is null) return NotFound();

        return View(new BillDetailsViewModel { Bill = bill, WhatsAppLink = _whatsApp.BillLink(bill) });
    }

    [HttpPost]
    public async Task<IActionResult> Regenerate(int id, CancellationToken ct)
    {
        var r = await _billing.RegenerateAsync(id, ct);
        Flash(r, "Bill refreshed with the latest deliveries and payments.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> AddAdjustment(int id, decimal amount, string? reason, CancellationToken ct)
    {
        var r = await _billing.AddAdjustmentAsync(id, amount, reason ?? "", ct);
        Flash(r, "Adjustment added.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveAdjustment(int id, int adjustmentId, CancellationToken ct)
    {
        var r = await _billing.RemoveAdjustmentAsync(id, adjustmentId, ct);
        Flash(r, "Adjustment removed.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> SetSent(int id, bool sent, CancellationToken ct)
    {
        var r = await _billing.SetSentAsync(id, sent, ct);
        Flash(r, sent ? "Bill marked as sent." : "Bill reopened. You can change it now.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllSent(int year, int month, CancellationToken ct)
    {
        var count = await _billing.MarkAllSentAsync(PeriodOf(year, month), ct);
        TempData["Success"] = _l["{0} bills marked as sent.", count].Value;
        return RedirectToAction(nameof(Index), new { year, month });
    }

    /// <summary>Marks the bill as sent and opens WhatsApp with the bill message.</summary>
    [HttpPost]
    public async Task<IActionResult> Share(int id, CancellationToken ct)
    {
        var bill = await _billing.GetAsync(id, ct);
        if (bill is null) return NotFound();

        if (bill.Status == BillStatus.Draft) await _billing.SetSentAsync(id, true, ct);
        return Redirect(_whatsApp.BillLink(bill));
    }

    public async Task<IActionResult> Print(int id, CancellationToken ct)
    {
        var bill = await _billing.GetAsync(id, ct);
        if (bill is null) return NotFound();

        return View("Print", new PrintBillsViewModel
        {
            Bills = new[] { bill },
            Period = bill.Period,
            BackUrl = Url.Action(nameof(Details), new { id })!
        });
    }

    public async Task<IActionResult> PrintAll(int year, int month, int? areaId, CancellationToken ct)
    {
        var period = PeriodOf(year, month);

        return View("Print", new PrintBillsViewModel
        {
            Bills = await _billing.GetForPrintAsync(period, areaId, ct),
            Period = period,
            BackUrl = Url.Action(nameof(Index), new { year = period.Year, month = period.Month, areaId })!
        });
    }

    private void Flash(Result r, string success)
    {
        if (r.Succeeded) TempData["Success"] = _l[success].Value;
        else TempData["Error"] = _l[r.Errors[0]].Value;
    }
}
