using AquaTrack.Application.Billing;
using AquaTrack.Application.Common;
using AquaTrack.Application.Operations;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Services;

/// <summary>
/// Monthly bills (SRS BILL-01..07).
/// Bill = bottles delivered in the month × rate + previous balance + adjustments − payments in the month.
/// Draft bills are rebuilt from the latest deliveries and payments every time they are generated;
/// a bill marked Sent is frozen until it is reopened.
/// Author: Attique
/// </summary>
public class BillingService : IBillingService
{
    private readonly AppDbContext _db;

    public BillingService(AppDbContext db)
    {
        _db = db;
    }

    // ------------------------------------------------------------------ Generate

    public Task<GenerateResult> GenerateAsync(BillPeriod period, CancellationToken ct = default) =>
        BuildAsync(period, null, ct);

    public async Task<Result> RegenerateAsync(int billId, CancellationToken ct = default)
    {
        var bill = await _db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == billId, ct);
        if (bill is null) return Result.Fail("Bill not found.");
        if (bill.Status == BillStatus.Sent) return Result.Fail("This bill is already sent. Reopen it to make changes.");

        await BuildAsync(new BillPeriod(bill.Year, bill.Month), bill.CustomerId, ct);
        return Result.Ok();
    }

    private async Task<GenerateResult> BuildAsync(BillPeriod p, int? onlyCustomerId, CancellationToken ct)
    {
        var start = p.Start;
        var next = p.NextStart;
        var startUtc = p.StartUtc;
        var nextUtc = p.NextStartUtc;
        var one = onlyCustomerId;

        var customers = await _db.Customers
            .Where(c => one == null || c.Id == one)
            .Select(c => new { c.Id, c.Rate })
            .ToListAsync(ct);

        var existing = await _db.Bills
            .Include(b => b.AdjustmentItems)
            .Where(b => b.Year == p.Year && b.Month == p.Month && (one == null || b.CustomerId == one))
            .ToDictionaryAsync(b => b.CustomerId, ct);

        var oldLines = (await _db.BillLines
                .Where(l => l.Bill!.Year == p.Year && l.Bill.Month == p.Month && (one == null || l.Bill.CustomerId == one))
                .ToListAsync(ct))
            .ToLookup(l => l.BillId);

        var deliveries = (await _db.Deliveries
                .Where(d => d.Date >= start && d.Date < next && d.Status == DeliveryStatus.Done && d.FilledGiven > 0
                            && (one == null || d.CustomerId == one))
                .Select(d => new { d.CustomerId, d.Date, d.FilledGiven })
                .ToListAsync(ct))
            .GroupBy(d => d.CustomerId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(x => x.Date).Select(x => (Date: x.Key, Bottles: x.Sum(y => y.FilledGiven))).OrderBy(x => x.Date).ToList());

        var billedBefore = await _db.Bills
            .Where(b => (b.Year < p.Year || (b.Year == p.Year && b.Month < p.Month)) && (one == null || b.CustomerId == one))
            .GroupBy(b => b.CustomerId)
            .Select(g => new { g.Key, Sum = g.Sum(b => b.Amount + b.Adjustments) })
            .ToDictionaryAsync(x => x.Key, x => x.Sum, ct);

        var paidBefore = await _db.Payments
            .Where(x => x.Date < startUtc && (one == null || x.CustomerId == one))
            .GroupBy(x => x.CustomerId)
            .Select(g => new { g.Key, Sum = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Sum, ct);

        var paidInPeriod = await _db.Payments
            .Where(x => x.Date >= startUtc && x.Date < nextUtc && (one == null || x.CustomerId == one))
            .GroupBy(x => x.CustomerId)
            .Select(g => new { g.Key, Sum = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Sum, ct);

        int created = 0, updated = 0, skippedSent = 0, skippedEmpty = 0;

        foreach (var c in customers)
        {
            existing.TryGetValue(c.Id, out var bill);

            if (bill?.Status == BillStatus.Sent)
            {
                skippedSent++;
                continue;
            }

            var lines = deliveries.TryGetValue(c.Id, out var l) ? l : new List<(DateOnly Date, int Bottles)>();
            var bottles = lines.Sum(x => x.Bottles);
            var previous = billedBefore.GetValueOrDefault(c.Id) - paidBefore.GetValueOrDefault(c.Id);
            var paid = paidInPeriod.GetValueOrDefault(c.Id);

            if (bill is null && bottles == 0 && previous == 0 && paid == 0)
            {
                skippedEmpty++;
                continue;
            }

            if (bill is null)
            {
                bill = new Bill { CustomerId = c.Id, Year = p.Year, Month = p.Month };
                _db.Bills.Add(bill);
                created++;
            }
            else
            {
                _db.BillLines.RemoveRange(oldLines[bill.Id]);
                updated++;
            }

            bill.Status = BillStatus.Draft;
            bill.SentAt = null;
            bill.Rate = c.Rate;
            bill.BottleCount = bottles;
            bill.Amount = bottles * c.Rate;
            bill.PreviousBalance = previous;
            bill.PaymentsInPeriod = paid;
            bill.Adjustments = bill.AdjustmentItems.Sum(a => a.Amount);
            bill.TotalDue = bill.Amount + bill.PreviousBalance + bill.Adjustments - bill.PaymentsInPeriod;

            foreach (var (date, count) in lines)
            {
                _db.BillLines.Add(new BillLine { Bill = bill, DeliveryDate = date, Bottles = count });
            }
        }

        await _db.SaveChangesAsync(ct);
        return new GenerateResult(created, updated, skippedSent, skippedEmpty);
    }

    // ------------------------------------------------------------------ Read

    public async Task<BillMonthView> GetMonthAsync(BillPeriod p, string? search, int? areaId, BillStatus? status, CancellationToken ct = default)
    {
        var query = _db.Bills.AsNoTracking().Where(b => b.Year == p.Year && b.Month == p.Month);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var phone = PhoneNumber.Normalize(s);
            query = query.Where(b => b.Customer!.Name.Contains(s) || (phone.Length > 0 && b.Customer.Phone.Contains(phone)));
        }

        if (areaId.HasValue) query = query.Where(b => b.Customer!.AreaId == areaId.Value);
        if (status.HasValue) query = query.Where(b => b.Status == status.Value);

        var bills = await query
            .OrderBy(b => b.Customer!.Area!.Name).ThenBy(b => b.Customer!.RouteOrder).ThenBy(b => b.Customer!.Name)
            .Select(b => new BillListItem(
                b.Id, b.CustomerId, b.Customer!.Name, b.Customer.Phone, b.Customer.WhatsApp, b.Customer.Area!.Name,
                b.BottleCount, b.Amount, b.PreviousBalance, b.PaymentsInPeriod, b.Adjustments, b.TotalDue, b.Status))
            .ToListAsync(ct);

        var totals = new BillMonthTotals(
            bills.Count,
            bills.Sum(b => b.Bottles),
            bills.Sum(b => b.Amount + b.Adjustments),
            bills.Sum(b => Math.Max(0, b.TotalDue)),
            bills.Count(b => b.Status == BillStatus.Sent));

        var areas = await _db.Areas.AsNoTracking().OrderBy(a => a.Name).Select(a => new AreaOption(a.Id, a.Name)).ToListAsync(ct);
        var billingDay = await _db.Plants.Select(x => x.BillingDay).FirstOrDefaultAsync(ct);

        return new BillMonthView(p, bills, totals, areas, billingDay);
    }

    public async Task<BillDetails?> GetAsync(int id, CancellationToken ct = default) =>
        (await LoadDetailsAsync(_db.Bills.Where(b => b.Id == id), ct)).FirstOrDefault();

    public async Task<IReadOnlyList<BillDetails>> GetForPrintAsync(BillPeriod p, int? areaId, CancellationToken ct = default)
    {
        var query = _db.Bills.Where(b => b.Year == p.Year && b.Month == p.Month);
        if (areaId.HasValue) query = query.Where(b => b.Customer!.AreaId == areaId.Value);
        return await LoadDetailsAsync(query, ct);
    }

    private async Task<List<BillDetails>> LoadDetailsAsync(IQueryable<Bill> query, CancellationToken ct)
    {
        var bills = await query.AsNoTracking()
            .Include(b => b.Lines)
            .Include(b => b.AdjustmentItems)
            .Include(b => b.Customer!).ThenInclude(c => c.Area)
            .OrderBy(b => b.Customer!.Area!.Name).ThenBy(b => b.Customer!.RouteOrder).ThenBy(b => b.Customer!.Name)
            .ToListAsync(ct);

        if (bills.Count == 0) return new List<BillDetails>();

        var plant = await Ledger.PlantHeaderAsync(_db, ct);

        // Payments for every (customer, month) on the page, in one query
        var minUtc = bills.Min(b => new BillPeriod(b.Year, b.Month).StartUtc);
        var maxUtc = bills.Max(b => new BillPeriod(b.Year, b.Month).NextStartUtc);
        var customerIds = bills.Select(b => b.CustomerId).Distinct().ToList();

        var payments = await _db.Payments.AsNoTracking()
            .Where(x => customerIds.Contains(x.CustomerId) && x.Date >= minUtc && x.Date < maxUtc)
            .OrderBy(x => x.Date)
            .Select(x => new { x.CustomerId, x.Date, x.Amount, x.Method })
            .ToListAsync(ct);

        return bills.Select(b =>
        {
            var period = new BillPeriod(b.Year, b.Month);
            return new BillDetails(
                b.Id, period, b.Status, b.SentAt, b.UpdatedAt ?? b.CreatedAt,
                b.CustomerId, b.Customer!.Name, b.Customer.Phone, b.Customer.WhatsApp, b.Customer.Address, b.Customer.Area?.Name ?? "",
                b.BottleCount, b.Rate, b.Amount, b.PreviousBalance, b.Adjustments, b.PaymentsInPeriod, b.TotalDue,
                b.Lines.OrderBy(x => x.DeliveryDate).Select(x => new BillLineItem(x.DeliveryDate, x.Bottles, x.Bottles * b.Rate)).ToList(),
                b.AdjustmentItems.OrderBy(x => x.Id).Select(x => new BillAdjustmentItem(x.Id, x.Amount, x.Reason)).ToList(),
                payments.Where(x => x.CustomerId == b.CustomerId && x.Date >= period.StartUtc && x.Date < period.NextStartUtc)
                    .Select(x => new BillPaymentItem(x.Date, x.Amount, x.Method)).ToList(),
                plant);
        }).ToList();
    }

    // ------------------------------------------------------------------ Change

    public async Task<Result> AddAdjustmentAsync(int billId, decimal amount, string reason, CancellationToken ct = default)
    {
        var bill = await _db.Bills.FirstOrDefaultAsync(b => b.Id == billId, ct);
        if (bill is null) return Result.Fail("Bill not found.");
        if (bill.Status == BillStatus.Sent) return Result.Fail("This bill is already sent. Reopen it to make changes.");
        if (amount == 0 || Math.Abs(amount) > 10_000_000) return Result.Fail("Enter an amount. Use minus for a discount.");
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail("Please enter a reason.");

        _db.BillAdjustments.Add(new BillAdjustment { BillId = bill.Id, Amount = decimal.Round(amount, 2), Reason = reason.Trim()[..Math.Min(reason.Trim().Length, 200)] });
        bill.Adjustments += decimal.Round(amount, 2);
        bill.TotalDue = bill.Amount + bill.PreviousBalance + bill.Adjustments - bill.PaymentsInPeriod;

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RemoveAdjustmentAsync(int billId, int adjustmentId, CancellationToken ct = default)
    {
        var bill = await _db.Bills.FirstOrDefaultAsync(b => b.Id == billId, ct);
        if (bill is null) return Result.Fail("Bill not found.");
        if (bill.Status == BillStatus.Sent) return Result.Fail("This bill is already sent. Reopen it to make changes.");

        var adj = await _db.BillAdjustments.FirstOrDefaultAsync(a => a.Id == adjustmentId && a.BillId == billId, ct);
        if (adj is null) return Result.Fail("Adjustment not found.");

        _db.BillAdjustments.Remove(adj);
        bill.Adjustments -= adj.Amount;
        bill.TotalDue = bill.Amount + bill.PreviousBalance + bill.Adjustments - bill.PaymentsInPeriod;

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetSentAsync(int billId, bool sent, CancellationToken ct = default)
    {
        var bill = await _db.Bills.FirstOrDefaultAsync(b => b.Id == billId, ct);
        if (bill is null) return Result.Fail("Bill not found.");

        bill.Status = sent ? BillStatus.Sent : BillStatus.Draft;
        bill.SentAt = sent ? (bill.SentAt ?? DateTime.UtcNow) : null;

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<int> MarkAllSentAsync(BillPeriod p, CancellationToken ct = default)
    {
        var drafts = await _db.Bills.Where(b => b.Year == p.Year && b.Month == p.Month && b.Status == BillStatus.Draft).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var b in drafts)
        {
            b.Status = BillStatus.Sent;
            b.SentAt = now;
        }

        await _db.SaveChangesAsync(ct);
        return drafts.Count;
    }

    // ------------------------------------------------------------------ Customer money

    public async Task<CustomerAccount> GetCustomerAccountAsync(int customerId, CancellationToken ct = default)
    {
        var balance = await Ledger.BalanceAsync(_db, customerId, ct);

        var recentBills = await _db.Bills.AsNoTracking()
            .Where(b => b.CustomerId == customerId)
            .OrderByDescending(b => b.Year).ThenByDescending(b => b.Month)
            .Take(6)
            .Select(b => new { b.Id, b.Year, b.Month, b.TotalDue, b.Status })
            .ToListAsync(ct);

        var recentPayments = await _db.Payments.AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.Date)
            .Take(6)
            .Select(x => new CustomerPaymentSummary(x.Id, x.Date, x.Amount, x.Method, x.DeliveryId != null))
            .ToListAsync(ct);

        // Bottles delivered after the last billed month are not on any bill yet
        var last = recentBills.FirstOrDefault();
        var unbilledFrom = last is null ? DateOnly.MinValue : new BillPeriod(last.Year, last.Month).NextStart;
        var rate = await _db.Customers.Where(c => c.Id == customerId).Select(c => c.Rate).FirstOrDefaultAsync(ct);
        var unbilledBottles = await _db.Deliveries
            .Where(d => d.CustomerId == customerId && d.Date >= unbilledFrom && d.Status == DeliveryStatus.Done)
            .SumAsync(d => d.FilledGiven, ct);

        return new CustomerAccount(
            balance,
            unbilledBottles * rate,
            recentBills.Select(b => new CustomerBillSummary(b.Id, new BillPeriod(b.Year, b.Month), b.TotalDue, b.Status)).ToList(),
            recentPayments);
    }

    public async Task<decimal> GetTotalPendingAsync(CancellationToken ct = default) =>
        (await Ledger.BalancesAsync(_db, ct)).Values.Where(v => v > 0).Sum();
}
