using AquaTrack.Application.Billing;
using AquaTrack.Application.Common;
using AquaTrack.Application.Expenses;
using AquaTrack.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AquaTrack.Web.Controllers;

/// <summary>Plant expenses: electricity, filters, salary, fuel, new bottles (SRS EXP-01).</summary>
[Authorize(Roles = Roles.OwnerOrManager)]
public class ExpensesController : Controller
{
    private readonly IExpenseService _expenses;
    private readonly IStringLocalizer<SharedResource> _l;

    public ExpensesController(IExpenseService expenses, IStringLocalizer<SharedResource> l)
    {
        _expenses = expenses;
        _l = l;
    }

    public async Task<IActionResult> Index(int? year, int? month, ExpenseCategory? category, CancellationToken ct)
    {
        var current = BillPeriod.Of(PakistanTime.Today);
        var period = year.HasValue && month.HasValue && BillPeriod.IsValid(year.Value, month.Value)
            ? new BillPeriod(year.Value, month.Value)
            : current;
        if (period.Start > current.Start) period = current;

        ViewBag.Current = current;
        ViewBag.Category = category;
        ViewBag.Today = PakistanTime.Today;
        return View(await _expenses.GetMonthAsync(period, category, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Add(ExpenseInput input, CancellationToken ct)
    {
        var result = await _expenses.AddAsync(input, ct);
        if (result.Succeeded) TempData["Success"] = _l["Expense saved."].Value;
        else TempData["Error"] = _l[result.Errors[0]].Value;
        return RedirectToAction(nameof(Index), new { year = input.Date.Year, month = input.Date.Month });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, int year, int month, CancellationToken ct)
    {
        var result = await _expenses.DeleteAsync(id, ct);
        if (result.Succeeded) TempData["Success"] = _l["Expense removed."].Value;
        else TempData["Error"] = _l[result.Errors[0]].Value;
        return RedirectToAction(nameof(Index), new { year, month });
    }
}
