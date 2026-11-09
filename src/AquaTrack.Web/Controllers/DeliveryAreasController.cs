using AquaTrack.Application.Common;
using AquaTrack.Application.Operations;
using AquaTrack.Web.Models.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AquaTrack.Web.Controllers;

/// <summary>Delivery areas / routes of the plant (SRS AREA-01, AREA-02).</summary>
[Authorize(Roles = Roles.OwnerOrManager)]
public class DeliveryAreasController : Controller
{
    private readonly IAreaService _areas;
    private readonly IStaffService _staff;
    private readonly IStringLocalizer<SharedResource> _l;

    public DeliveryAreasController(IAreaService areas, IStaffService staff, IStringLocalizer<SharedResource> l)
    {
        _areas = areas;
        _staff = staff;
        _l = l;
    }

    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await BuildPageAsync(new AreaFormViewModel(), ct));

    [HttpPost]
    public async Task<IActionResult> Create([Bind(Prefix = "Form")] AreaFormViewModel form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await _areas.CreateAsync(form.Name, form.DeliveryBoyId, ct);
            if (result.Succeeded)
            {
                TempData["Success"] = _l["Area added."].Value;
                return RedirectToAction(nameof(Index));
            }
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, _l[e]);
        }

        return View(nameof(Index), await BuildPageAsync(form, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var area = await _areas.GetAsync(id, ct);
        if (area is null) return NotFound();

        return View(new AreaFormViewModel
        {
            Id = area.Id,
            Name = area.Name,
            DeliveryBoyId = area.DeliveryBoyId,
            Staff = await _staff.GetOptionsAsync(ct)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, AreaFormViewModel form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await _areas.UpdateAsync(id, form.Name, form.DeliveryBoyId, ct);
            if (result.Succeeded)
            {
                TempData["Success"] = _l["Area updated."].Value;
                return RedirectToAction(nameof(Index));
            }
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, _l[e]);
        }

        form.Id = id;
        form.Staff = await _staff.GetOptionsAsync(ct);
        return View(form);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _areas.DeleteAsync(id, ct);
        if (result.Succeeded) TempData["Success"] = _l["Area removed."].Value;
        else TempData["Error"] = _l[result.Errors[0]].Value;
        return RedirectToAction(nameof(Index));
    }

    private async Task<AreasPageViewModel> BuildPageAsync(AreaFormViewModel form, CancellationToken ct)
    {
        form.Staff = await _staff.GetOptionsAsync(ct);
        return new AreasPageViewModel { Areas = await _areas.GetAreasAsync(ct), Form = form };
    }
}
