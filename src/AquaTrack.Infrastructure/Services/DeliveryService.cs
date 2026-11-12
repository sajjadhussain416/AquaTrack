using AquaTrack.Application.Common;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Application.Operations;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Services;

/// <summary>
/// Daily deliveries (SRS DLV-01..08).
/// One delivery row per customer per day. Saving again updates it: the old bottle
/// movements and cash payment of that delivery are soft-deleted and written again,
/// so balances always match the latest entry and every change is audit-logged.
/// Author: Attique
/// </summary>
public class DeliveryService : IDeliveryService
{
    private const int MaxBottlesPerVisit = 200;
    private const decimal MaxCashPerVisit = 1_000_000m;

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IStaffService _staff;

    public DeliveryService(AppDbContext db, ICurrentUserService currentUser, IStaffService staff)
    {
        _db = db;
        _currentUser = currentUser;
        _staff = staff;
    }

    private bool IsDeliveryBoy => _currentUser.IsInRole(Roles.DeliveryBoy);

    // ------------------------------------------------------------------ Route

    public async Task<RouteDay> GetRouteAsync(DateOnly date, int? areaId, CancellationToken ct = default)
    {
        var areaQuery = _db.Areas.AsNoTracking();
        if (IsDeliveryBoy)
        {
            var me = _currentUser.UserId;
            areaQuery = areaQuery.Where(a => a.DeliveryBoyId == me);
        }

        var areas = await areaQuery.OrderBy(a => a.Name).Select(a => new AreaOption(a.Id, a.Name)).ToListAsync(ct);

        var areaIds = areas.Select(a => a.Id).ToList();
        if (areaId.HasValue && areaIds.Contains(areaId.Value))
            areaIds = new List<int> { areaId.Value };
        else
            areaId = null;

        var customers = await _db.Customers.AsNoTracking()
            .Where(c => areaIds.Contains(c.AreaId))
            .OrderBy(c => c.Area!.Name).ThenBy(c => c.RouteOrder).ThenBy(c => c.Name)
            .Select(c => new
            {
                c.Id, c.Name, c.Phone, c.Address, AreaName = c.Area!.Name, c.Rate, c.IsActive,
                c.ScheduleType, c.ScheduleDays, c.CreatedAt,
                Bottles = c.BottleMovements.Sum(m =>
                    m.Type == BottleMovementType.Issue || m.Type == BottleMovementType.Deliver ? m.Quantity : -m.Quantity)
            })
            .ToListAsync(ct);

        var deliveries = await _db.Deliveries.AsNoTracking()
            .Where(d => d.Date == date && areaIds.Contains(d.Customer!.AreaId))
            .ToDictionaryAsync(d => d.CustomerId, ct);

        var stops = new List<RouteStop>();
        var others = new List<RouteStop>();

        foreach (var c in customers)
        {
            deliveries.TryGetValue(c.Id, out var d);
            var scheduled = c.IsActive && IsScheduled(c.ScheduleType, c.ScheduleDays, c.CreatedAt, date);

            if (!c.IsActive && d is null) continue;

            var stop = new RouteStop(
                c.Id, c.Name, c.Phone, c.Address, c.AreaName, c.Rate, c.Bottles, scheduled,
                d?.Id, d?.Status, d?.FilledGiven ?? 0, d?.EmptyCollected ?? 0, d?.CashCollected ?? 0, d?.Note);

            if (scheduled || d is not null) stops.Add(stop);
            else others.Add(stop);
        }

        return new RouteDay(date, areas, areaId, stops, others, Totals(deliveries.Values, stops.Count));
    }

    /// <summary>Is this customer due on this date? On-call customers are never pre-listed.</summary>
    public static bool IsScheduled(ScheduleType type, WeekDays days, DateTime createdUtc, DateOnly date)
    {
        switch (type)
        {
            case ScheduleType.Daily:
                return true;

            case ScheduleType.FixedWeekdays:
                return days.HasFlag(ToFlag(date.DayOfWeek));

            case ScheduleType.AlternateDays:
                var start = DateOnly.FromDateTime(PakistanTime.ToLocal(createdUtc));
                return Math.Abs(date.DayNumber - start.DayNumber) % 2 == 0;

            default:
                return false;
        }
    }

    private static WeekDays ToFlag(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => WeekDays.Monday,
        DayOfWeek.Tuesday => WeekDays.Tuesday,
        DayOfWeek.Wednesday => WeekDays.Wednesday,
        DayOfWeek.Thursday => WeekDays.Thursday,
        DayOfWeek.Friday => WeekDays.Friday,
        DayOfWeek.Saturday => WeekDays.Saturday,
        _ => WeekDays.Sunday
    };

    private static RouteTotals Totals(IEnumerable<Delivery> deliveries, int stops)
    {
        var list = deliveries.ToList();
        return new RouteTotals(
            stops,
            list.Count(d => d.Status == DeliveryStatus.Done),
            list.Sum(d => d.FilledGiven),
            list.Sum(d => d.EmptyCollected),
            list.Sum(d => d.CashCollected));
    }

    // ------------------------------------------------------------------ Save

    public async Task<Result<RouteStop>> SaveAsync(DeliveryEntry entry, CancellationToken ct = default)
    {
        var today = PakistanTime.Today;

        if (entry.Date > today)
            return Result<RouteStop>.Fail("You cannot record a delivery for a future date.");

        if (IsDeliveryBoy && entry.Date != today)
            return Result<RouteStop>.Fail("Delivery staff can only record today's deliveries.");

        var customer = await _db.Customers.Include(c => c.Area).FirstOrDefaultAsync(c => c.Id == entry.CustomerId, ct);
        if (customer is null)
            return Result<RouteStop>.Fail("Customer not found.");

        if (IsDeliveryBoy && customer.Area?.DeliveryBoyId != _currentUser.UserId)
            return Result<RouteStop>.Fail("This customer is not on your route.");

        // Only a completed visit moves bottles or money
        var done = entry.Status == DeliveryStatus.Done;
        var filled = done ? entry.Filled : 0;
        var empties = done ? entry.Empties : 0;
        var cash = done ? decimal.Round(entry.Cash, 2) : 0m;

        if (filled < 0 || empties < 0 || cash < 0 || filled > MaxBottlesPerVisit || empties > MaxBottlesPerVisit || cash > MaxCashPerVisit)
            return Result<RouteStop>.Fail("Please check the numbers.");

        if (done && filled == 0 && empties == 0 && cash == 0)
            return Result<RouteStop>.Fail("Enter bottles or cash, or choose Not home / Skip.");

        var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.CustomerId == customer.Id && d.Date == entry.Date, ct);
        var existingId = delivery?.Id;

        // Bottles with the customer, not counting this day's earlier entry
        var bottlesBefore = await _db.BottleMovements
            .Where(m => m.CustomerId == customer.Id && (existingId == null || m.DeliveryId != existingId))
            .SumAsync(m => m.Type == BottleMovementType.Issue || m.Type == BottleMovementType.Deliver ? m.Quantity : -m.Quantity, ct);

        if (empties > bottlesBefore + filled)
            return Result<RouteStop>.Fail("Empty bottles cannot be more than the bottles with the customer.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (delivery is null)
        {
            delivery = new Delivery
            {
                CustomerId = customer.Id,
                Date = entry.Date,
                DeliveryBoyId = IsDeliveryBoy
                    ? _currentUser.UserId!
                    : customer.Area?.DeliveryBoyId ?? _currentUser.UserId!
            };
            _db.Deliveries.Add(delivery);
        }
        else
        {
            _db.BottleMovements.RemoveRange(await _db.BottleMovements.Where(m => m.DeliveryId == delivery.Id).ToListAsync(ct));
            _db.Payments.RemoveRange(await _db.Payments.Where(p => p.DeliveryId == delivery.Id).ToListAsync(ct));
        }

        delivery.Status = entry.Status;
        delivery.FilledGiven = filled;
        delivery.EmptyCollected = empties;
        delivery.CashCollected = cash;
        delivery.Note = string.IsNullOrWhiteSpace(entry.Note) ? null : entry.Note.Trim()[..Math.Min(entry.Note.Trim().Length, 300)];

        await _db.SaveChangesAsync(ct);

        var when = entry.Date == today
            ? DateTime.UtcNow
            : entry.Date.ToDateTime(new TimeOnly(7, 0), DateTimeKind.Utc); // 12:00 in Pakistan

        if (filled > 0)
        {
            _db.BottleMovements.Add(new BottleMovement
            {
                CustomerId = customer.Id, DeliveryId = delivery.Id, Type = BottleMovementType.Deliver,
                Quantity = filled, Date = when
            });
        }

        if (empties > 0)
        {
            _db.BottleMovements.Add(new BottleMovement
            {
                CustomerId = customer.Id, DeliveryId = delivery.Id, Type = BottleMovementType.Collect,
                Quantity = empties, Date = when
            });
        }

        if (cash > 0)
        {
            _db.Payments.Add(new Payment
            {
                CustomerId = customer.Id, DeliveryId = delivery.Id, Amount = cash, Method = PaymentMethod.Cash,
                ReceivedById = delivery.DeliveryBoyId, Date = when, Note = "Collected at delivery"
            });
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result<RouteStop>.Ok(new RouteStop(
            customer.Id, customer.Name, customer.Phone, customer.Address, customer.Area?.Name ?? "", customer.Rate,
            bottlesBefore + filled - empties, true,
            delivery.Id, delivery.Status, filled, empties, cash, delivery.Note));
    }

    // ------------------------------------------------------------------ Owner views

    public async Task<DeliveryDayReport> GetDayAsync(DateOnly date, int? areaId, string? deliveryBoyId, CancellationToken ct = default)
    {
        var query = _db.Deliveries.AsNoTracking().Where(d => d.Date == date);

        if (areaId.HasValue) query = query.Where(d => d.Customer!.AreaId == areaId.Value);
        if (!string.IsNullOrEmpty(deliveryBoyId)) query = query.Where(d => d.DeliveryBoyId == deliveryBoyId);

        var list = await query
            .OrderBy(d => d.Customer!.Area!.Name).ThenBy(d => d.Customer!.RouteOrder)
            .Select(d => new { d, CustomerName = d.Customer!.Name, AreaName = d.Customer!.Area!.Name })
            .ToListAsync(ct);

        var names = await UserNamesAsync(ct);

        var rows = list.Select(x => ToRow(x.d, x.CustomerName, x.AreaName, names)).ToList();

        var areas = await _db.Areas.AsNoTracking().OrderBy(a => a.Name).Select(a => new AreaOption(a.Id, a.Name)).ToListAsync(ct);

        return new DeliveryDayReport(date, rows, Totals(list.Select(x => x.d), rows.Count), areas, await _staff.GetOptionsAsync(ct));
    }

    public async Task<DeliveryRow?> GetAsync(int id, CancellationToken ct = default)
    {
        var x = await _db.Deliveries.AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new { d, CustomerName = d.Customer!.Name, AreaName = d.Customer!.Area!.Name })
            .FirstOrDefaultAsync(ct);

        return x is null ? null : ToRow(x.d, x.CustomerName, x.AreaName, await UserNamesAsync(ct));
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (delivery is null) return Result.Fail("Delivery not found.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        _db.BottleMovements.RemoveRange(await _db.BottleMovements.Where(m => m.DeliveryId == id).ToListAsync(ct));
        _db.Payments.RemoveRange(await _db.Payments.Where(p => p.DeliveryId == id).ToListAsync(ct));
        _db.Deliveries.Remove(delivery);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Result.Ok();
    }

    private async Task<Dictionary<string, string>> UserNamesAsync(CancellationToken ct)
    {
        var plantId = _currentUser.PlantId;
        return await _db.Users.AsNoTracking()
            .Where(u => u.PlantId == plantId)
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private static DeliveryRow ToRow(Delivery d, string customerName, string areaName, IReadOnlyDictionary<string, string> names) =>
        new(d.Id, d.Date, d.CustomerId, customerName, areaName,
            names.TryGetValue(d.DeliveryBoyId, out var n) ? n : "-",
            d.Status, d.FilledGiven, d.EmptyCollected, d.CashCollected, d.Note, d.UpdatedAt ?? d.CreatedAt);
}
