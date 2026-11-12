using AquaTrack.Application.Common;
using AquaTrack.Application.Operations;
using AquaTrack.Application.Reports;
using AquaTrack.Web.Models.Reports;
using AquaTrack.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AquaTrack.Web.Controllers;

/// <summary>Owner reports: udhaar list, bottles, sales (SRS RPT-02..04).</summary>
[Authorize(Roles = Roles.OwnerOrManager)]
public class ReportsController : Controller
{
    private readonly IReportService _reports;
    private readonly IAreaService _areas;
    private readonly IStaffService _staff;
    private readonly WhatsAppMessages _whatsApp;
    private readonly AquaTrack.Application.Expenses.IExpenseService _expenses;
    private readonly IStringLocalizer<SharedResource> _l;

    public ReportsController(IReportService reports, IAreaService areas, IStaffService staff,
        WhatsAppMessages whatsApp, AquaTrack.Application.Expenses.IExpenseService expenses, IStringLocalizer<SharedResource> l)
    {
        _expenses = expenses;
        _reports = reports;
        _areas = areas;
        _staff = staff;
        _whatsApp = whatsApp;
        _l = l;
    }

    public IActionResult Index() => RedirectToAction(nameof(Udhaar));

    // ---------------- Udhaar list ----------------

    public async Task<IActionResult> Udhaar(int? areaId, string? sort, CancellationToken ct)
    {
        var report = await _reports.GetUdhaarAsync(areaId, ct);
        sort = sort == "age" ? "age" : "amount";

        var rows = sort == "age"
            ? report.Rows.OrderByDescending(r => r.DaysUnpaid).ThenByDescending(r => r.Balance).ToList()
            : report.Rows.OrderByDescending(r => r.Balance).ThenByDescending(r => r.DaysUnpaid).ToList();

        return View(new UdhaarPageViewModel
        {
            Report = report,
            Rows = rows,
            Areas = await _areas.GetOptionsAsync(ct),
            ReminderLinks = rows.ToDictionary(r => r.CustomerId, r => _whatsApp.ReminderLink(r, report.Plant)),
            AreaId = areaId,
            Sort = sort
        });
    }

    // ---------------- Bottles ----------------

    public async Task<IActionResult> Bottles(int? areaId, string? show, CancellationToken ct)
    {
        var report = await _reports.GetBottlesAsync(areaId, ct);
        show = show is "attention" or "over" ? show : "all";

        var rows = show switch
        {
            "attention" => report.Rows.Where(r => r.Idle || r.OverLimit).ToList(),
            "over" => report.Rows.Where(r => r.OverLimit).ToList(),
            _ => report.Rows
        };

        return View(new BottlePageViewModel
        {
            Report = report,
            Rows = rows,
            Areas = await _areas.GetOptionsAsync(ct),
            AreaId = areaId,
            Show = show
        });
    }

    [HttpPost]
    public async Task<IActionResult> TotalBottles(int total, CancellationToken ct)
    {
        var result = await _reports.SetTotalBottlesAsync(total, ct);
        if (result.Succeeded) TempData["Success"] = _l["Bottle stock saved."].Value;
        else TempData["Error"] = _l[result.Errors[0]].Value;
        return RedirectToAction(nameof(Bottles));
    }

    // ---------------- Profit (SRS EXP-02) ----------------

    public async Task<IActionResult> Profit(int? months, CancellationToken ct)
    {
        var m = months is 3 or 6 or 12 ? months.Value : 6;
        ViewBag.Months = m;
        return View(await _expenses.GetProfitAsync(m, ct));
    }

    // ---------------- Sales ----------------

    public async Task<IActionResult> Sales(string? range, DateOnly? from, DateOnly? to, int? areaId, string? staffId, CancellationToken ct)
    {
        var today = PakistanTime.Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        range = range is "today" or "7d" or "month" or "last-month" or "custom" ? range : "month";
        (DateOnly f, DateOnly t) = range switch
        {
            "today" => (today, today),
            "7d" => (today.AddDays(-6), today),
            "last-month" => (monthStart.AddMonths(-1), monthStart.AddDays(-1)),
            "custom" => (from ?? monthStart, to ?? today),
            _ => (monthStart, today)
        };
        if (t > today) t = today;
        if (f > t) (f, t) = (t, f);
        if (t.DayNumber - f.DayNumber > 366) f = t.AddDays(-366);

        staffId = string.IsNullOrWhiteSpace(staffId) ? null : staffId;

        return View(new SalesPageViewModel
        {
            Report = await _reports.GetSalesAsync(f, t, areaId, staffId, ct),
            Areas = await _areas.GetOptionsAsync(ct),
            Staff = await _staff.GetOptionsAsync(ct),
            AreaId = areaId,
            StaffId = staffId,
            Range = range,
            Today = today
        });
    }
}
