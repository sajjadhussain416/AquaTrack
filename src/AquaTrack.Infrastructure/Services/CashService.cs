using AquaTrack.Application.Common;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Application.Operations;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Services;

/// <summary>
/// Delivery staff cash sheet (SRS STAFF-02).
/// Cash in hand = cash collected at doors − cash handed to the owner, carried over day to day.
/// Easypaisa / JazzCash / bank money is shown separately; it never passes through the staff member's hands.
/// Author: Attique
/// </summary>
public class CashService : ICashService
{
    private const decimal MaxHandover = 10_000_000m;

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IStaffService _staff;

    public CashService(AppDbContext db, ICurrentUserService currentUser, IStaffService staff)
    {
        _db = db;
        _currentUser = currentUser;
        _staff = staff;
    }

    public async Task<CashSheet> GetDayAsync(DateOnly date, CancellationToken ct = default)
    {
        var staff = await _staff.GetDeliveryStaffAsync(ct);

        var cashBefore = await SumCashAsync(d => d.Date < date, ct);
        var cashToday = await SumCashAsync(d => d.Date == date, ct);

        var handedBefore = await _db.CashHandovers.Where(h => h.Date < date)
            .GroupBy(h => h.DeliveryBoyId).Select(g => new { g.Key, Sum = g.Sum(h => h.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Sum, ct);

        var handedToday = await _db.CashHandovers.Where(h => h.Date == date)
            .GroupBy(h => h.DeliveryBoyId).Select(g => new { g.Key, Sum = g.Sum(h => h.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Sum, ct);

        var today = await _db.Deliveries.Where(d => d.Date == date)
            .GroupBy(d => d.DeliveryBoyId)
            .Select(g => new
            {
                g.Key,
                Digital = g.Where(d => d.PaymentMethod != PaymentMethod.Cash).Sum(d => d.CashCollected),
                Count = g.Count(d => d.Status == DeliveryStatus.Done)
            })
            .ToDictionaryAsync(x => x.Key, ct);

        var rows = new List<CashSheetRow>();
        foreach (var s in staff)
        {
            var opening = cashBefore.GetValueOrDefault(s.Id) - handedBefore.GetValueOrDefault(s.Id);
            var collected = cashToday.GetValueOrDefault(s.Id);
            var handed = handedToday.GetValueOrDefault(s.Id);
            today.TryGetValue(s.Id, out var t);

            if (!s.IsActive && opening == 0 && collected == 0 && handed == 0 && t is null) continue;

            rows.Add(new CashSheetRow(s.Id, s.FullName, opening, collected, handed, opening + collected - handed,
                t?.Digital ?? 0, t?.Count ?? 0));
        }

        return new CashSheet(date, rows);
    }

    public async Task<IReadOnlyList<HandoverItem>> GetHandoversAsync(DateOnly date, CancellationToken ct = default)
    {
        var names = (await _staff.GetDeliveryStaffAsync(ct)).ToDictionary(s => s.Id, s => s.FullName);

        var list = await _db.CashHandovers.AsNoTracking()
            .Where(h => h.Date == date)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(ct);

        return list.Select(h => new HandoverItem(h.Id, h.Date, names.GetValueOrDefault(h.DeliveryBoyId, "-"), h.Amount, h.Note, h.CreatedAt)).ToList();
    }

    public async Task<Result> RecordHandoverAsync(string staffId, DateOnly date, decimal amount, string? note, CancellationToken ct = default)
    {
        var today = PakistanTime.Today;

        if (!(await _staff.GetDeliveryStaffAsync(ct)).Any(s => s.Id == staffId))
            return Result.Fail("Staff member not found.");

        if (amount <= 0 || amount > MaxHandover)
            return Result.Fail("Enter an amount greater than zero.");

        if (date > today || date < today.AddDays(-60))
            return Result.Fail("Choose a date within the last 60 days.");

        _db.CashHandovers.Add(new CashHandover
        {
            DeliveryBoyId = staffId,
            Date = date,
            Amount = decimal.Round(amount, 2),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 300)]
        });

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteHandoverAsync(int id, CancellationToken ct = default)
    {
        var h = await _db.CashHandovers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (h is null) return Result.Fail("Entry not found.");

        _db.CashHandovers.Remove(h); // soft delete
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<decimal> GetMyCashInHandAsync(CancellationToken ct = default)
    {
        var me = _currentUser.UserId;
        var collected = await _db.Deliveries
            .Where(d => d.DeliveryBoyId == me && d.PaymentMethod == PaymentMethod.Cash)
            .SumAsync(d => d.CashCollected, ct);
        var handed = await _db.CashHandovers.Where(h => h.DeliveryBoyId == me).SumAsync(h => h.Amount, ct);
        return collected - handed;
    }

    private async Task<Dictionary<string, decimal>> SumCashAsync(System.Linq.Expressions.Expression<Func<Delivery, bool>> when, CancellationToken ct) =>
        await _db.Deliveries
            .Where(when)
            .Where(d => d.PaymentMethod == PaymentMethod.Cash)
            .GroupBy(d => d.DeliveryBoyId)
            .Select(g => new { g.Key, Sum = g.Sum(d => d.CashCollected) })
            .ToDictionaryAsync(x => x.Key, x => x.Sum, ct);
}
