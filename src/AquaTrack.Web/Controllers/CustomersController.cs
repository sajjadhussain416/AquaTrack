using AquaTrack.Application.Common;
using AquaTrack.Application.Operations;
using AquaTrack.Web.Models.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AquaTrack.Web.Controllers;

/// <summary>Customers of the plant (SRS CUST-01..07).</summary>
[Authorize(Roles = Roles.OwnerOrManager)]
public class CustomersController : Controller
{
    private readonly ICustomerService _customers;
    private readonly IAreaService _areas;
    private readonly IStringLocalizer<SharedResource> _l;

    public CustomersController(ICustomerService customers, IAreaService areas, IStringLocalizer<SharedResource> l)
    {
        _customers = customers;
        _areas = areas;
        _l = l;
    }

    public async Task<IActionResult> Index(string? q, int? areaId, bool inactive = false, int page = 1, CancellationToken ct = default)
    {
        return View(new CustomersIndexViewModel
        {
            Page = await _customers.GetPageAsync(q, areaId, inactive, page, ct),
            Areas = await _areas.GetOptionsAsync(ct),
            Search = q,
            AreaId = areaId,
            ShowInactive = inactive
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var customer = await _customers.GetDetailsAsync(id, ct);
        return customer is null ? NotFound() : View(customer);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var areas = await _areas.GetOptionsAsync(ct);
        if (areas.Count == 0)
        {
            TempData["Error"] = _l["Add a delivery area first, then add customers."].Value;
            return RedirectToAction("Index", "DeliveryAreas");
        }

        return View("Form", new CustomerFormViewModel
        {
            Areas = areas,
            AreaId = areas.Count == 1 ? areas[0].Id : 0,
            Rate = await _customers.GetDefaultRateAsync(ct)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CustomerFormViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await _customers.CreateAsync(model.ToInput(), ct);
            if (result.Succeeded)
            {
                TempData["Success"] = _l["Customer added."].Value;
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, _l[e]);
        }

        model.Areas = await _areas.GetOptionsAsync(ct);
        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var customer = await _customers.GetDetailsAsync(id, ct);
        if (customer is null) return NotFound();

        var model = CustomerFormViewModel.From(customer);
        model.Areas = await _areas.GetOptionsAsync(ct);
        return View("Form", model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, CustomerFormViewModel model, CancellationToken ct)
    {
        // Opening bottles and deposit are only set when the customer is created
        ModelState.Remove(nameof(model.OpeningBottles));
        ModelState.Remove(nameof(model.DepositTaken));

        if (ModelState.IsValid)
        {
            var result = await _customers.UpdateAsync(id, model.ToInput(), ct);
            if (result.Succeeded)
            {
                TempData["Success"] = _l["Customer updated."].Value;
                return RedirectToAction(nameof(Details), new { id });
            }
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, _l[e]);
        }

        model.Id = id;
        model.Areas = await _areas.GetOptionsAsync(ct);
        return View("Form", model);
    }

    [HttpPost]
    public async Task<IActionResult> SetActive(int id, bool active, CancellationToken ct)
    {
        var result = await _customers.SetActiveAsync(id, active, ct);
        if (result.Succeeded) TempData["Success"] = _l[active ? "Customer activated." : "Customer deactivated."].Value;
        else TempData["Error"] = _l[result.Errors[0]].Value;
        return RedirectToAction(nameof(Details), new { id });
    }
}
