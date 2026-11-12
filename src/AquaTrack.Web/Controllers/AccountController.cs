using AquaTrack.Application.Common;
using AquaTrack.Application.Plants;
using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Identity;
using AquaTrack.Web.Models.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AquaTrack.Web.Controllers;

/// <summary>Login with mobile number + password (SRS AUTH-01, AUTH-06, PLANT-04).</summary>
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPlantService _plants;
    private readonly IStringLocalizer<SharedResource> _l;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IPlantService plants,
        IStringLocalizer<SharedResource> localizer,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _plants = plants;
        _l = localizer;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var current = await _userManager.GetUserAsync(User);
            if (current is not null) return await RedirectToHomeAsync(current);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var phone = PhoneNumber.Normalize(model.Phone);
        var user = await _userManager.FindByNameAsync(phone);

        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, _l["Mobile number or password is incorrect."]);
            return View(model);
        }

        if (user.PlantId.HasValue)
        {
            var status = await _plants.GetStatusForLoginAsync(user.PlantId.Value);
            if (status is null || status == PlantStatus.Suspended)
            {
                ModelState.AddModelError(string.Empty, _l["Your plant account is suspended. Please contact support."]);
                return View(model);
            }
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Account locked: {Phone}", phone);
            ModelState.AddModelError(string.Empty, _l["Too many wrong attempts. Please try again after 15 minutes."]);
            return View(model);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, _l["Mobile number or password is incorrect."]);
            return View(model);
        }

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl) && await CanOpenAsync(user, model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return await RedirectToHomeAsync(user);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    /// <summary>
    /// Delivery staff only have the route screen; any other return address (for example the owner's
    /// dashboard link) would end on "Access denied", so they go to their route instead.
    /// </summary>
    private async Task<bool> CanOpenAsync(ApplicationUser user, string returnUrl)
    {
        if (!await _userManager.IsInRoleAsync(user, Roles.DeliveryBoy)) return true;
        return returnUrl.StartsWith("/Delivery", StringComparison.OrdinalIgnoreCase)
            && !returnUrl.StartsWith("/Deliveries", StringComparison.OrdinalIgnoreCase)
            && !returnUrl.StartsWith("/DeliveryAreas", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IActionResult> RedirectToHomeAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains(Roles.SuperAdmin))
            return RedirectToAction("Index", "Dashboard", new { area = "SuperAdmin" });

        if (roles.Contains(Roles.DeliveryBoy))
            return RedirectToAction("Index", "Delivery", new { area = "" });

        return RedirectToAction("Index", "Dashboard", new { area = "" });
    }
}
