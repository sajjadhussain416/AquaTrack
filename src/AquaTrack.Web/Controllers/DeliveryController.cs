using AquaTrack.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaTrack.Web.Controllers;

/// <summary>Delivery boy home. The delivery screen arrives in Week 3.</summary>
[Authorize(Roles = Roles.DeliveryBoy)]
public class DeliveryController : Controller
{
    public IActionResult Index() => View();
}
