using AquaTrack.Application.Billing;
using AquaTrack.Application.Settings;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Services;

/// <summary>
/// Customer money position. Balance = everything billed (bill amounts + adjustments) − everything paid.
/// Positive = customer owes the plant (udhaar). Negative = advance.
/// </summary>
internal static class Ledger
{
    public static async Task<decimal> BalanceAsync(AppDbContext db, int customerId, CancellationToken ct)
    {
        var billed = await db.Bills.Where(b => b.CustomerId == customerId).SumAsync(b => b.Amount + b.Adjustments, ct);
        var paid = await db.Payments.Where(p => p.CustomerId == customerId).SumAsync(p => p.Amount, ct);
        return billed - paid;
    }

    public static async Task<Dictionary<int, decimal>> BalancesAsync(AppDbContext db, CancellationToken ct)
    {
        var billed = await db.Bills
            .GroupBy(b => b.CustomerId)
            .Select(g => new { g.Key, Sum = g.Sum(b => b.Amount + b.Adjustments) })
            .ToDictionaryAsync(x => x.Key, x => x.Sum, ct);

        var paid = await db.Payments
            .GroupBy(p => p.CustomerId)
            .Select(g => new { g.Key, Sum = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Sum, ct);

        var result = new Dictionary<int, decimal>();
        foreach (var id in billed.Keys.Union(paid.Keys))
        {
            result[id] = billed.GetValueOrDefault(id) - paid.GetValueOrDefault(id);
        }
        return result;
    }

    public static Task<PlantHeader> PlantHeaderAsync(AppDbContext db, CancellationToken ct) =>
        db.Plants.AsNoTracking()
            .Select(p => new PlantHeader(p.Name, p.Phone, p.City, p.Address, p.LogoUrl,
                new PaymentAccounts(p.EasypaisaNumber, p.JazzCashNumber, p.BankName, p.BankAccountTitle, p.BankAccountNumber, p.EasypaisaTitle, p.JazzCashTitle)))
            .FirstAsync(ct);

    /// <summary>A payment on a past date is stamped at noon Pakistan time; today's at the actual time.</summary>
    public static DateTime StampFor(DateOnly date, DateOnly today) =>
        date == today ? DateTime.UtcNow : date.ToDateTime(new TimeOnly(7, 0), DateTimeKind.Utc);
}
