using AquaTrack.Application.Common;
using AquaTrack.Application.Plants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AquaTrack.Web.Areas.SuperAdmin.Controllers;

/// <summary>Who has paid, who is due, and recording subscription payments (SRS SUB-02..04).</summary>
[Area("SuperAdmin")]
[Authorize(Roles = Roles.SuperAdmin)]
public class SubscriptionsController : Controller
{
    private readonly ISubscriptionService _subs;
    private readonly IPlantService _plants;
    private readonly IStringLocalizer<SharedResource> _l;

    public SubscriptionsController(ISubscriptionService subs, IPlantService plants, IStringLocalizer<SharedResource> l)
    {
        _subs = subs;
        _plants = plants;
        _l = l;
    }

    public async Task<IActionResult> Index(string? filter, CancellationToken ct)
    {
        filter = filter is "overdue" or "week" or "trial" ? filter : "all";
        ViewBag.Filter = filter;
        return View(await _subs.GetOverviewAsync(filter, ct));
    }

    /// <summary>One plant: plan, due date, usage and payment history.</summary>
    public async Task<IActionResult> Plant(int id, CancellationToken ct)
    {
        var plant = await _subs.GetPlantAsync(id, ct);
        if (plant is null) return NotFound();
        ViewBag.Plans = await _plants.GetPlansAsync(ct);
        ViewBag.Today = PakistanTime.Today;
        return View(plant);
    }

    [HttpPost]
    public async Task<IActionResult> RecordPayment(SubscriptionPaymentInput input, CancellationToken ct)
    {
        var result = await _subs.RecordPaymentAsync(input, ct);
        Flash(result, "Subscription payment saved. The plant is active.");
        return RedirectToAction(nameof(Plant), new { id = input.PlantId });
    }

    [HttpPost]
    public async Task<IActionResult> DeletePayment(int id, int plantId, CancellationToken ct)
    {
        Flash(await _subs.DeletePaymentAsync(id, ct), "Payment removed.");
        return RedirectToAction(nameof(Plant), new { id = plantId });
    }

    [HttpPost]
    public async Task<IActionResult> ChangePlan(int id, int planId, CancellationToken ct)
    {
        Flash(await _subs.ChangePlanAsync(id, planId, ct), "Plan changed.");
        return RedirectToAction(nameof(Plant), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> ExtendTrial(int id, int days, CancellationToken ct)
    {
        Flash(await _subs.ExtendTrialAsync(id, days, ct), "Trial extended.");
        return RedirectToAction(nameof(Plant), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> SetStatus(int id, AquaTrack.Domain.Enums.PlantStatus status, CancellationToken ct)
    {
        Flash(await _plants.SetStatusAsync(id, status, ct), "Plant status updated.");
        return RedirectToAction(nameof(Plant), new { id });
    }

    private void Flash(Result result, string success)
    {
        if (result.Succeeded) TempData["Success"] = _l[success].Value;
        else TempData["Error"] = _l[result.Errors[0]].Value;
    }
}
