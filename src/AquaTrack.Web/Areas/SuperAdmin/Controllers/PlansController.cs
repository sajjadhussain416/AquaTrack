using AquaTrack.Application.Common;
using AquaTrack.Application.Plants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AquaTrack.Web.Areas.SuperAdmin.Controllers;

/// <summary>Subscription plans: price and limits (SRS SUB-01).</summary>
[Area("SuperAdmin")]
[Authorize(Roles = Roles.SuperAdmin)]
public class PlansController : Controller
{
    private readonly ISubscriptionService _subs;
    private readonly IStringLocalizer<SharedResource> _l;

    public PlansController(ISubscriptionService subs, IStringLocalizer<SharedResource> l)
    {
        _subs = subs;
        _l = l;
    }

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _subs.GetPlansAsync(ct));

    [HttpGet]
    public IActionResult Create()
    {
        ViewBag.Id = null;
        return View("Edit", new PlanInput { MonthlyPrice = 1000, MaxCustomers = 200, MaxDeliveryBoys = 2 });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var plan = await _subs.GetPlanAsync(id, ct);
        if (plan is null) return NotFound();
        ViewBag.Id = id;
        return View(plan);
    }

    [HttpPost]
    public async Task<IActionResult> Save(int? id, PlanInput input, CancellationToken ct)
    {
        var result = await _subs.SavePlanAsync(id, input, ct);
        if (result.Succeeded)
        {
            TempData["Success"] = _l["Plan saved."].Value;
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError(string.Empty, _l[result.Errors[0]]);
        ViewBag.Id = id;
        return View("Edit", input);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _subs.DeletePlanAsync(id, ct);
        if (result.Succeeded) TempData["Success"] = _l["Plan removed."].Value;
        else TempData["Error"] = _l[result.Errors[0]].Value;
        return RedirectToAction(nameof(Index));
    }
}
