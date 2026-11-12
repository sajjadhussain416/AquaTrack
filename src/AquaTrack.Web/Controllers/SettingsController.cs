using AquaTrack.Application.Common;
using AquaTrack.Application.Settings;
using AquaTrack.Web.Models.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AquaTrack.Web.Controllers;

/// <summary>Plant details and payment accounts. Owner only (SRS PLANT-02).</summary>
[Authorize(Roles = Roles.PlantOwner)]
public class SettingsController : Controller
{
    private readonly IPlantSettingsService _settings;
    private readonly AquaTrack.Application.Plants.ISubscriptionService _subs;
    private readonly IStringLocalizer<SharedResource> _l;

    public SettingsController(IPlantSettingsService settings, AquaTrack.Application.Plants.ISubscriptionService subs, IStringLocalizer<SharedResource> l)
    {
        _settings = settings;
        _subs = subs;
        _l = l;
    }

    public override async Task OnActionExecutionAsync(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context, Microsoft.AspNetCore.Mvc.Filters.ActionExecutionDelegate next)
    {
        ViewBag.Subscription = await _subs.GetMineAsync(HttpContext.RequestAborted);
        await next();
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(SettingsViewModel.From(await _settings.GetAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Index(SettingsViewModel model, CancellationToken ct)
    {
        if (!PhoneNumber.IsValidMobile(model.Phone) && PhoneNumber.Normalize(model.Phone).Length < 10)
            ModelState.AddModelError(nameof(model.Phone), _l["Enter a valid phone number."]);

        if (ModelState.IsValid)
        {
            var result = await _settings.UpdateAsync(model.ToInput(), ct);
            if (result.Succeeded)
            {
                TempData["Success"] = _l["Settings saved."].Value;
                return RedirectToAction(nameof(Index));
            }
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, _l[e]);
        }

        return View(model);
    }
}
