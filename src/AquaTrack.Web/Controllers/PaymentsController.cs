using AquaTrack.Application.Billing;
using AquaTrack.Application.Common;
using AquaTrack.Domain.Enums;
using AquaTrack.Web.Models.Billing;
using AquaTrack.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AquaTrack.Web.Controllers;

/// <summary>Payments received at the plant, the monthly register and printable receipts (SRS PAY-01..03).</summary>
[Authorize(Roles = Roles.OwnerOrManager)]
public class PaymentsController : Controller
{
    private readonly IPaymentService _payments;
    private readonly WhatsAppMessages _whatsApp;
    private readonly IStringLocalizer<SharedResource> _l;

    public PaymentsController(IPaymentService payments, WhatsAppMessages whatsApp, IStringLocalizer<SharedResource> l)
    {
        _payments = payments;
        _whatsApp = whatsApp;
        _l = l;
    }

    public async Task<IActionResult> Index(int? year, int? month, string? q, PaymentMethod? method, CancellationToken ct)
    {
        var current = BillPeriod.Of(PakistanTime.Today);
        var period = year.HasValue && month.HasValue && BillPeriod.IsValid(year.Value, month.Value)
            ? new BillPeriod(year.Value, month.Value)
            : current;

        return View(new PaymentsIndexViewModel
        {
            Month = await _payments.GetMonthAsync(period, q, method, ct),
            Search = q,
            Method = method,
            Current = current,
            PendingCount = await _payments.GetPendingCountAsync(ct)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId, CancellationToken ct)
    {
        var today = PakistanTime.Today;
        return View(new PaymentFormViewModel
        {
            CustomerId = customerId ?? 0,
            Date = today,
            Today = today,
            Customers = await _payments.GetCustomerOptionsAsync(ct)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(PaymentFormViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await _payments.RecordAsync(model.ToInput(), ct);
            if (result.Succeeded)
            {
                TempData["Success"] = _l["Payment saved."].Value;
                return RedirectToAction(nameof(Receipt), new { id = result.Value });
            }

            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, _l[e]);
        }

        model.Today = PakistanTime.Today;
        model.Customers = await _payments.GetCustomerOptionsAsync(ct);
        return View(model);
    }

    public async Task<IActionResult> Receipt(int id, CancellationToken ct)
    {
        var receipt = await _payments.GetReceiptAsync(id, ct);
        if (receipt is null) return NotFound();

        return View(new ReceiptViewModel { Receipt = receipt, WhatsAppLink = _whatsApp.ReceiptLink(receipt) });
    }

    /// <summary>Easypaisa / JazzCash / bank money taken at the door, waiting for the owner to check.</summary>
    public async Task<IActionResult> Verify(CancellationToken ct) =>
        View(await _payments.GetPendingAsync(ct));

    [HttpPost]
    public async Task<IActionResult> MarkVerified(int id, CancellationToken ct)
    {
        var result = await _payments.VerifyAsync(id, ct);
        if (result.Succeeded) TempData["Success"] = _l["Payment verified."].Value;
        else TempData["Error"] = _l[result.Errors[0]].Value;
        return RedirectToAction(nameof(Verify));
    }

    [HttpPost]
    public async Task<IActionResult> Reject(int id, string? reason, CancellationToken ct)
    {
        var result = await _payments.RejectAsync(id, reason, ct);
        if (result.Succeeded) TempData["Success"] = _l["Payment rejected. The amount was added back to the customer's balance."].Value;
        else TempData["Error"] = _l[result.Errors[0]].Value;
        return RedirectToAction(nameof(Verify));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, int? year, int? month, CancellationToken ct)
    {
        var result = await _payments.DeleteAsync(id, ct);

        if (result.Succeeded) TempData["Success"] = _l["Payment removed."].Value;
        else TempData["Error"] = _l[result.Errors[0]].Value;

        return RedirectToAction(nameof(Index), new { year, month });
    }
}
