using AquaTrack.Application.Common;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Application.Operations;
using AquaTrack.Application.Reports;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Services;

/// <summary>Owner reports: udhaar list, bottles, sales (SRS RPT-02..04).</summary>
public class ReportService : IReportService
{
    /// <summary>Bottles held this long without a delivery may be missing.</summary>
    private const int IdleDays = 14;

    private readonly AppDbContext _db;
    private readonly IStaffService _staff;

    public ReportService(AppDbContext db, IStaffService staff)
    {
        _db = db;
        _staff = staff;
    }

    // ---------------- Udhaar ----------------

    public async Task<UdhaarReport> GetUdhaarAsync(int? areaId, CancellationToken ct = default)
    {
        var today = PakistanTime.Today;

        var customers = await _db.Customers.AsNoTracking()
            .Where(c => areaId == null || c.AreaId == areaId)
            .Select(c => new { c.Id, c.Name, c.Phone, c.WhatsApp, AreaName = c.Area!.Name })
            .ToListAsync(ct);
        var ids = customers.Select(c => c.Id).ToHashSet();

        var bills = (await _db.Bills.AsNoTracking()
                .Select(b => new BillCharge(b.CustomerId, b.Year, b.Month, b.Amount + b.Adjustments))
                .ToListAsync(ct))
            .Where(b => ids.Contains(b.CustomerId))
            .GroupBy(b => b.CustomerId)
            .ToDictionary(g => g.Key, g => g.OrderBy(b => b.Year).ThenBy(b => b.Month).ToList());

        var payments = (await _db.Payments.AsNoTracking()
                .Select(p => new PaymentRow(p.CustomerId, p.Amount, p.Date))
                .ToListAsync(ct))
            .Where(p => ids.Contains(p.CustomerId))
            .GroupBy(p => p.CustomerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<UdhaarRow>();
        int advanceCustomers = 0;
        decimal advanceTotal = 0;

        foreach (var c in customers)
        {
            var custBills = bills.GetValueOrDefault(c.Id) ?? new List<BillCharge>();
            var custPays = payments.GetValueOrDefault(c.Id) ?? new List<PaymentRow>();
            var paid = custPays.Sum(p => p.Amount);
            var balance = custBills.Sum(b => b.Charge) - paid;

            if (balance < 0) { advanceCustomers++; advanceTotal += -balance; continue; }
            if (balance == 0) continue;

            // Oldest bill not yet covered by payments (payments settle the oldest bills first).
            // UnpaidSince = month of that bill; days are counted from when it fell due (1st of the next month).
            DateOnly? since = null;
            var left = paid;
            foreach (var b in custBills)
            {
                if (left >= b.Charge) { left -= b.Charge; continue; }
                since = new DateOnly(b.Year, b.Month, 1);
                break;
            }
            var days = since is null ? 0 : Math.Max(0, today.DayNumber - since.Value.AddMonths(1).DayNumber);

            var last = custPays.OrderByDescending(p => p.Date).FirstOrDefault();
            rows.Add(new UdhaarRow(
                c.Id, c.Name, c.Phone, c.WhatsApp, c.AreaName, balance, since, days,
                last is null ? null : DateOnly.FromDateTime(PakistanTime.ToLocal(last.Date)),
                last?.Amount ?? 0));
        }

        return new UdhaarReport(
            rows,
            rows.Sum(r => r.Balance),
            rows.Where(r => r.DaysUnpaid > 30).Sum(r => r.Balance),
            rows.Where(r => r.DaysUnpaid > 60).Sum(r => r.Balance),
            advanceCustomers, advanceTotal,
            await Ledger.PlantHeaderAsync(_db, ct));
    }

    private sealed record BillCharge(int CustomerId, int Year, int Month, decimal Charge);
    private sealed record PaymentRow(int CustomerId, decimal Amount, DateTime Date);

    // ---------------- Bottles ----------------

    public async Task<BottleReport> GetBottlesAsync(int? areaId, CancellationToken ct = default)
    {
        var today = PakistanTime.Today;

        var inHand = await _db.BottleMovements
            .GroupBy(m => m.CustomerId)
            .Select(g => new
            {
                g.Key,
                Qty = g.Sum(m => m.Type == BottleMovementType.Issue || m.Type == BottleMovementType.Deliver ? m.Quantity : -m.Quantity)
            })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

        var lostOrDamaged = await _db.BottleMovements
            .Where(m => m.Type == BottleMovementType.Lost || m.Type == BottleMovementType.Damaged)
            .SumAsync(m => m.Quantity, ct);

        var lastDelivery = await _db.Deliveries
            .Where(d => d.Status == DeliveryStatus.Done && d.FilledGiven > 0)
            .GroupBy(d => d.CustomerId)
            .Select(g => new { g.Key, Last = g.Max(d => d.Date) })
            .ToDictionaryAsync(x => x.Key, x => x.Last, ct);

        var customers = await _db.Customers.AsNoTracking()
            .Select(c => new { c.Id, c.Name, c.Phone, c.AreaId, AreaName = c.Area!.Name, c.IsActive, c.MaxBottles })
            .ToListAsync(ct);

        var withCustomers = customers.Sum(c => Math.Max(0, inHand.GetValueOrDefault(c.Id)));

        var rows = customers
            .Where(c => areaId == null || c.AreaId == areaId)
            .Select(c =>
            {
                var qty = Math.Max(0, inHand.GetValueOrDefault(c.Id));
                DateOnly? last = lastDelivery.TryGetValue(c.Id, out var l) ? l : null;
                int? days = last is null ? null : today.DayNumber - last.Value.DayNumber;
                var over = c.MaxBottles is not null && qty > c.MaxBottles;
                var idle = qty > 0 && (!c.IsActive || days is null || days >= IdleDays);
                return new BottleRow(c.Id, c.Name, c.Phone, c.AreaName, c.IsActive, qty, c.MaxBottles, last, days, over, idle);
            })
            .Where(r => r.InHand > 0)
            .OrderByDescending(r => r.OverLimit || r.Idle)
            .ThenByDescending(r => r.InHand)
            .ThenBy(r => r.Name)
            .ToList();

        var totalOwned = await _db.BottleStocks.Select(s => (int?)s.TotalOwned).FirstOrDefaultAsync(ct) ?? 0;

        return new BottleReport(
            rows, totalOwned, withCustomers, lostOrDamaged,
            Math.Max(0, totalOwned - withCustomers - lostOrDamaged),
            rows.Count(r => r.OverLimit), rows.Count(r => r.Idle), IdleDays);
    }

    public async Task<Result> SetTotalBottlesAsync(int total, CancellationToken ct = default)
    {
        if (total is < 0 or > 1_000_000) return Result.Fail("Enter a number between 0 and 1,000,000.");

        var stock = await _db.BottleStocks.FirstOrDefaultAsync(ct);
        if (stock is null)
        {
            _db.BottleStocks.Add(new BottleStock { TotalOwned = total });
        }
        else
        {
            stock.TotalOwned = total;
        }
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ---------------- Sales ----------------

    public async Task<SalesReport> GetSalesAsync(DateOnly from, DateOnly to, int? areaId, string? staffId, CancellationToken ct = default)
    {
        var rows = await _db.Deliveries.AsNoTracking()
            .Where(d => d.Date >= from && d.Date <= to && d.Status == DeliveryStatus.Done)
            .Where(d => areaId == null || d.Customer!.AreaId == areaId)
            .Where(d => staffId == null || d.DeliveryBoyId == staffId)
            .Select(d => new
            {
                d.Date, d.DeliveryBoyId, AreaName = d.Customer!.Area!.Name,
                d.FilledGiven, d.EmptyCollected, Rate = d.Customer.Rate,
                d.CashCollected, IsCash = d.PaymentMethod == PaymentMethod.Cash
            })
            .ToListAsync(ct);

        var names = (await _staff.GetDeliveryStaffAsync(ct)).ToDictionary(s => s.Id, s => s.FullName);

        var byArea = rows.GroupBy(r => r.AreaName)
            .Select(g => new SalesLine(g.Key, g.Count(), g.Sum(r => r.FilledGiven), g.Sum(r => r.FilledGiven * r.Rate),
                g.Where(r => r.IsCash).Sum(r => r.CashCollected), g.Where(r => !r.IsCash).Sum(r => r.CashCollected)))
            .OrderByDescending(l => l.Value).ToList();

        var byStaff = rows.GroupBy(r => r.DeliveryBoyId)
            .Select(g => new SalesLine(names.GetValueOrDefault(g.Key, "-"), g.Count(), g.Sum(r => r.FilledGiven), g.Sum(r => r.FilledGiven * r.Rate),
                g.Where(r => r.IsCash).Sum(r => r.CashCollected), g.Where(r => !r.IsCash).Sum(r => r.CashCollected)))
            .OrderByDescending(l => l.Value).ToList();

        var byDay = rows.GroupBy(r => r.Date)
            .Select(g => new SalesDay(g.Key, g.Count(), g.Sum(r => r.FilledGiven), g.Sum(r => r.FilledGiven * r.Rate),
                g.Where(r => r.IsCash).Sum(r => r.CashCollected), g.Where(r => !r.IsCash).Sum(r => r.CashCollected)))
            .OrderBy(d => d.Date).ToList();

        return new SalesReport(
            from, to,
            rows.Count, rows.Sum(r => r.FilledGiven), rows.Sum(r => r.EmptyCollected),
            rows.Sum(r => r.FilledGiven * r.Rate),
            rows.Where(r => r.IsCash).Sum(r => r.CashCollected),
            rows.Where(r => !r.IsCash).Sum(r => r.CashCollected),
            byArea, byStaff, byDay);
    }
}
