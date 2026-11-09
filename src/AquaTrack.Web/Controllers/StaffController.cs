using AquaTrack.Application.Common;
using AquaTrack.Application.Operations;
using AquaTrack.Web.Models.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AquaTrack.Web.Controllers;

/// <summary>Delivery staff with their own login (SRS STAFF-01).</summary>
[Authorize(Roles = Roles.OwnerOrManager)]
public class StaffController : Controller
{
    private readonly IStaffService _staff;
    private readonly IAreaService _areas;
    private readonly IStringLocalizer<SharedResource> _l;

    public StaffController(IStaffService staff, IAreaService areas, IStringLocalizer<SharedResource> l)
    {
        _staff = staff;
        _areas = areas;
        _l = l;
    }

    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await _staff.GetDeliveryStaffAsync(ct));

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct) =>
        View(new StaffFormViewModel { AllAreas = await _areas.GetOptionsAsync(ct) });

    [HttpPost]
    public async Task<IActionResult> Create(StaffFormViewModel model, CancellationToken ct)
    {
        if (!PhoneNumber.IsValidMobile(model.Phone))
            ModelState.AddModelError(nameof(model.Phone), _l["Enter a valid mobile number like 03001234567."]);

        if (ModelState.IsValid)
        {
            var result = await _staff.CreateDeliveryBoyAsync(model.FullName, model.Phone, model.Password, model.AreaIds, ct);
            if (result.Succeeded)
            {
                TempData["Success"] = _l["Delivery staff added. They can now sign in with their mobile number."].Value;
                return RedirectToAction(nameof(Index));
            }
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, _l[e]);
        }

        model.AllAreas = await _areas.GetOptionsAsync(ct);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> SetActive(string id, bool active, CancellationToken ct)
    {
        var result = await _staff.SetActiveAsync(id, active, ct);
        if (result.Succeeded) TempData["Success"] = _l[active ? "Staff member activated." : "Staff member deactivated."].Value;
        else TempData["Error"] = _l[result.Errors[0]].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(string id, string newPassword, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            TempData["Error"] = _l["Password must be at least 8 characters."].Value;
            return RedirectToAction(nameof(Index));
        }

        var result = await _staff.ResetPasswordAsync(id, newPassword, ct);
        if (result.Succeeded) TempData["Success"] = _l["Password changed."].Value;
        else TempData["Error"] = _l[result.Errors[0]].Value;
        return RedirectToAction(nameof(Index));
    }
}
