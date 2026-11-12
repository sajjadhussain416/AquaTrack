using AquaTrack.Application.Billing;
using AquaTrack.Application.Common;
using AquaTrack.Application.Expenses;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Services;

/// <summary>Plant expenses and monthly profit (SRS EXP-01, EXP-02).</summary>
public class ExpenseService : IExpenseService
{
    private const decimal MaxAmount = 10_000_000m;

    private readonly AppDbContext _db;

    public ExpenseService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ExpenseMonth> GetMonthAsync(BillPeriod period, ExpenseCategory? category, CancellationToken ct = default)
    {
        var from = period.Start;
        var to = period.NextStart;

        var all = await _db.Expenses.AsNoTracking()
            .Where(e => e.Date >= from && e.Date < to)
            .OrderByDescending(e => e.Date).ThenByDescending(e => e.Id)
            .Select(e => new ExpenseRow(e.Id, e.Date, e.Category, e.Amount, e.Note))
            .ToListAsync(ct);

        var byCategory = all.GroupBy(e => e.Category)
            .Select(g => new CategoryTotal(g.Key, g.Sum(e => e.Amount)))
            .OrderByDescending(c => c.Amount)
            .ToList();

        var rows = category is null ? all : all.Where(e => e.Category == category).ToList();
        return new ExpenseMonth(period, rows, all.Sum(e => e.Amount), byCategory);
    }

    public async Task<Result> AddAsync(ExpenseInput input, CancellationToken ct = default)
    {
        var today = PakistanTime.Today;
        if (!Enum.IsDefined(input.Category)) return Result.Fail("Choose a category.");
        if (input.Amount <= 0 || input.Amount > MaxAmount) return Result.Fail("Enter an amount greater than zero.");
        if (input.Date > today) return Result.Fail("The date cannot be in the future.");
        if (input.Date < today.AddYears(-1)) return Result.Fail("The date is too old.");
        if (input.Category == ExpenseCategory.Other && string.IsNullOrWhiteSpace(input.Note))
            return Result.Fail("For \"Other\", write what the money was for.");

        _db.Expenses.Add(new Expense
        {
            Category = input.Category,
            Amount = decimal.Round(input.Amount, 2),
            Date = input.Date,
            Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim()[..Math.Min(input.Note.Trim().Length, 300)]
        });
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var e = await _db.Expenses.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result.Fail("Expense not found.");
        _db.Expenses.Remove(e); // soft delete
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<ProfitReport> GetProfitAsync(int months, CancellationToken ct = default)
    {
        months = Math.Clamp(months, 1, 24);
        var current = BillPeriod.Of(PakistanTime.Today);
        var first = new BillPeriod(current.Start.AddMonths(-(months - 1)).Year, current.Start.AddMonths(-(months - 1)).Month);
        var from = first.Start;
        var to = current.NextStart;

        var deliveries = await _db.Deliveries.AsNoTracking()
            .Where(d => d.Date >= from && d.Date < to && d.Status == DeliveryStatus.Done)
            .Select(d => new { d.Date, d.FilledGiven, d.Customer!.Rate, d.CashCollected })
            .ToListAsync(ct);

        var fromUtc = first.StartUtc;
        var toUtc = current.NextStartUtc;
        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.Date >= fromUtc && p.Date < toUtc)
            .Select(p => new { p.Date, p.Amount })
            .ToListAsync(ct);

        var expenses = await _db.Expenses.AsNoTracking()
            .Where(e => e.Date >= from && e.Date < to)
            .Select(e => new { e.Date, e.Category, e.Amount })
            .ToListAsync(ct);

        var list = new List<ProfitMonth>();
        for (var p = current; p.Start >= first.Start; p = p.Previous)
        {
            var period = p;
            var monthDeliveries = deliveries.Where(d => d.Date >= period.Start && d.Date < period.NextStart).ToList();
            var collected = payments.Where(x => x.Date >= period.StartUtc && x.Date < period.NextStartUtc).Sum(x => x.Amount);
            var spent = expenses.Where(e => e.Date >= period.Start && e.Date < period.NextStart).Sum(e => e.Amount);

            list.Add(new ProfitMonth(period,
                monthDeliveries.Sum(d => d.FilledGiven),
                monthDeliveries.Sum(d => d.FilledGiven * d.Rate),
                collected, spent));
        }

        var byCategory = expenses.GroupBy(e => e.Category)
            .Select(g => new CategoryTotal(g.Key, g.Sum(e => e.Amount)))
            .OrderByDescending(c => c.Amount)
            .ToList();

        return new ProfitReport(list, byCategory);
    }
}
