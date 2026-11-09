using AquaTrack.Application.Common;
using AquaTrack.Application.Plants;
using AquaTrack.Domain.Enums;
using AquaTrack.Web.Areas.SuperAdmin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AquaTrack.Web.Areas.SuperAdmin.Controllers;

/// <summary>Create and manage water plants (SRS PLANT-01, PLANT-03).</summary>
[Area("SuperAdmin")]
[Authorize(Roles = Roles.SuperAdmin)]
public class PlantsController : Controller
{
    private readonly IPlantService _plants;
    private readonly IStringLocalizer<SharedResource> _l;

    public PlantsController(IPlantService plants, IStringLocalizer<SharedResource> localizer)
    {
        _plants = plants;
        _l = localizer;
    }

    public async Task<IActionResult> Index(string? q, PlantStatus? status, CancellationToken ct)
    {
        ViewBag.Query = q;
        ViewBag.Status = status;
        return View(await _plants.GetPlantsAsync(q, status, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var plans = await _plants.GetPlansAsync(ct);
        return View(new PlantFormViewModel
        {
            Plans = plans,
            PlanId = plans.FirstOrDefault()?.Id ?? 0
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(PlantFormViewModel model, CancellationToken ct)
    {
        if (!PhoneNumber.IsValidMobile(model.OwnerPhone))
        {
            ModelState.AddModelError(nameof(model.OwnerPhone), _l["Enter a valid mobile number like 03001234567."]);
        }

        if (!ModelState.IsValid)
        {
            model.Plans = await _plants.GetPlansAsync(ct);
            return View(model);
        }

        var result = await _plants.CreatePlantAsync(model.ToRequest(), ct);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, _l[error]);
            }

            model.Plans = await _plants.GetPlansAsync(ct);
            return View(model);
        }

        TempData["Success"] = _l["Plant created. The owner can now sign in with their mobile number."].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SetStatus(int id, PlantStatus status, CancellationToken ct)
    {
        var result = await _plants.SetStatusAsync(id, status, ct);

        if (result.Succeeded)
            TempData["Success"] = _l["Plant status updated."].Value;
        else
            TempData["Error"] = _l[result.Errors.First()].Value;

        return RedirectToAction(nameof(Index));
    }
}
