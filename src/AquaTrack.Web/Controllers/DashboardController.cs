using AquaTrack.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaTrack.Web.Controllers;

/// <summary>Plant owner / manager home. Real numbers arrive with customers and deliveries (Week 2-3).</summary>
[Authorize(Roles = Roles.OwnerOrManager)]
public class DashboardController : Controller
{
    public IActionResult Index() => View();
}
