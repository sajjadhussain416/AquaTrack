using System.Diagnostics;
using AquaTrack.Application.Common;
using AquaTrack.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaTrack.Web.Controllers;

public class HomeController : Controller
{
    /// <summary>Sends each user to their own home screen.</summary>
    [AllowAnonymous]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        if (User.IsInRole(Roles.SuperAdmin))
            return RedirectToAction("Index", "Dashboard", new { area = "SuperAdmin" });

        if (User.IsInRole(Roles.DeliveryBoy))
            return RedirectToAction("Index", "Delivery");

        return RedirectToAction("Index", "Dashboard");
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
