using AquaTrack.Application.Common;
using AquaTrack.Application.Operations;
using AquaTrack.Web.Models.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AquaTrack.Web.Controllers;

/// <summary>Owner's daily delivery register with edit and delete (SRS DLV-07, RPT-04).</summary>
[Authorize(Roles = Roles.OwnerOrManager)]
public class DeliveriesController : Controller
{
    private readonly IDeliveryService _deliveries;
    private readonly IStringLocalizer<SharedResource> _l;

    public DeliveriesController(IDeliveryService deliveries, IStringLocalizer<SharedResource> l)
    {
        _deliveries = deliveries;
        _l = l;
    }

    public async Task<IActionResult> Index(DateOnly? date, int? areaId, string? boyId, CancellationToken ct)
    {
        var today = PakistanTime.Today;
        var day = date is null || date > today ? today : date.Value;

        return View(new DeliveriesIndexViewModel
        {
            Report = await _deliveries.GetDayAsync(day, areaId, boyId, ct),
            AreaId = areaId,
            DeliveryBoyId = boyId,
            Today = today
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var row = await _deliveries.GetAsync(id, ct);
        return row is null ? NotFound() : View(DeliveryEditViewModel.From(row));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, DeliveryEditViewModel model, CancellationToken ct)
    {
        var row = await _deliveries.GetAsync(id, ct);
        if (row is null) return NotFound();

        if (ModelState.IsValid)
        {
            var result = await _deliveries.SaveAsync(
                new DeliveryEntry(row.CustomerId, row.Date, model.Status, model.Filled, model.Empties, model.Cash, model.Note), ct);

            if (result.Succeeded)
            {
                TempData["Success"] = _l["Delivery updated."].Value;
                return RedirectToAction(nameof(Index), new { date = row.Date.ToString("yyyy-MM-dd") });
            }

            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, _l[e]);
        }

        var vm = DeliveryEditViewModel.From(row);
        vm.Status = model.Status; vm.Filled = model.Filled; vm.Empties = model.Empties; vm.Cash = model.Cash; vm.Note = model.Note;
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var row = await _deliveries.GetAsync(id, ct);
        var result = await _deliveries.DeleteAsync(id, ct);

        if (result.Succeeded) TempData["Success"] = _l["Delivery removed. Bottles and cash were reversed."].Value;
        else TempData["Error"] = _l[result.Errors[0]].Value;

        return RedirectToAction(nameof(Index), new { date = row?.Date.ToString("yyyy-MM-dd") });
    }
}
