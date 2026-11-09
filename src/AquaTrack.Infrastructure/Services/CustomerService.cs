using AquaTrack.Application.Common;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Application.Operations;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Services;

/// <summary>
/// Customers of the logged-in plant.
/// Bottles in hand = Issue + Deliver - Collect - Lost - Damaged (SRS BTL-01).
/// </summary>
public class CustomerService : ICustomerService
{
    private const int PageSize = 25;

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CustomerService(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<CustomerPage> GetPageAsync(string? search, int? areaId, bool includeInactive, int page, CancellationToken ct = default)
    {
        var query = _db.Customers.AsNoTracking().AsQueryable();

        if (!includeInactive) query = query.Where(c => c.IsActive);
        if (areaId.HasValue) query = query.Where(c => c.AreaId == areaId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var phone = PhoneNumber.Normalize(s);
            query = query.Where(c => c.Name.Contains(s) || c.Address.Contains(s)
                || (phone.Length > 0 && c.Phone.Contains(phone)));
        }

        var total = await query.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)PageSize)));

        var items = await query
            .OrderBy(c => c.Area!.Name).ThenBy(c => c.RouteOrder).ThenBy(c => c.Name)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(c => new CustomerListItem(
                c.Id, c.Name, c.Phone, c.Address, c.Area!.Name, c.Type, c.Rate, c.ScheduleType,
                c.BottleMovements.Sum(m =>
                    m.Type == BottleMovementType.Issue || m.Type == BottleMovementType.Deliver ? m.Quantity : -m.Quantity),
                c.IsActive))
            .ToListAsync(ct);

        return new CustomerPage(items, total, page, PageSize);
    }

    public async Task<CustomerDetails?> GetDetailsAsync(int id, CancellationToken ct = default)
    {
        var c = await _db.Customers.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, x.Name, x.Phone, x.WhatsApp, x.Address, x.AreaId, AreaName = x.Area!.Name,
                x.Type, x.Rate, x.ScheduleType, x.ScheduleDays, x.MaxBottles, x.IsActive, x.CreatedAt,
                Bottles = x.BottleMovements.Sum(m =>
                    m.Type == BottleMovementType.Issue || m.Type == BottleMovementType.Deliver ? m.Quantity : -m.Quantity),
                Deposit = x.Deposits.Sum(d => d.Type == DepositType.Taken ? d.Amount : -d.Amount)
            })
            .FirstOrDefaultAsync(ct);

        if (c is null) return null;

        var movements = await _db.BottleMovements.AsNoTracking()
            .Where(m => m.CustomerId == id)
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id)
            .Take(20)
            .Select(m => new CustomerMovement(m.Date, m.Type, m.Quantity, m.Note))
            .ToListAsync(ct);

        return new CustomerDetails(c.Id, c.Name, c.Phone, c.WhatsApp, c.Address, c.AreaId, c.AreaName,
            c.Type, c.Rate, c.ScheduleType, c.ScheduleDays, c.MaxBottles, c.IsActive,
            c.Bottles, c.Deposit, c.CreatedAt, movements);
    }

    public async Task<decimal> GetDefaultRateAsync(CancellationToken ct = default) =>
        await _db.Plants.Select(p => p.DefaultRate).FirstOrDefaultAsync(ct);

    public async Task<Result<int>> CreateAsync(CustomerInput input, CancellationToken ct = default)
    {
        var check = await ValidateAsync(input, null, ct);
        if (check is not null) return Result<int>.Fail(check);

        // Plan limit (SRS SUB-01)
        var plantId = _currentUser.PlantId;
        var limit = await _db.Plants.Where(p => p.Id == plantId).Select(p => p.Plan!.MaxCustomers).FirstOrDefaultAsync(ct);
        if (limit.HasValue && await _db.Customers.CountAsync(c => c.IsActive, ct) >= limit.Value)
            return Result<int>.Fail("Your plan's customer limit is reached. Upgrade the plan to add more.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var customer = new Customer();
        Apply(customer, input);
        customer.RouteOrder = (await _db.Customers.Where(c => c.AreaId == input.AreaId).MaxAsync(c => (int?)c.RouteOrder, ct) ?? 0) + 1;

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);

        var now = DateTime.UtcNow;

        if (input.OpeningBottles > 0)
        {
            _db.BottleMovements.Add(new BottleMovement
            {
                CustomerId = customer.Id,
                Type = BottleMovementType.Issue,
                Quantity = input.OpeningBottles,
                Date = now,
                Note = "Opening bottles"
            });

            var stock = await _db.BottleStocks.FirstOrDefaultAsync(ct);
            if (stock is not null)
            {
                stock.AtPlantEmpty = Math.Max(0, stock.AtPlantEmpty - input.OpeningBottles);
            }
        }

        if (input.DepositTaken > 0)
        {
            _db.Deposits.Add(new Deposit
            {
                CustomerId = customer.Id,
                Amount = input.DepositTaken,
                Bottles = input.OpeningBottles,
                Type = DepositType.Taken,
                Date = now
            });
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result<int>.Ok(customer.Id);
    }

    public async Task<Result> UpdateAsync(int id, CustomerInput input, CancellationToken ct = default)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null) return Result.Fail("Customer not found.");

        var check = await ValidateAsync(input, id, ct);
        if (check is not null) return Result.Fail(check);

        if (customer.AreaId != input.AreaId)
        {
            customer.RouteOrder = (await _db.Customers.Where(c => c.AreaId == input.AreaId).MaxAsync(c => (int?)c.RouteOrder, ct) ?? 0) + 1;
        }

        Apply(customer, input);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetActiveAsync(int id, bool active, CancellationToken ct = default)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null) return Result.Fail("Customer not found.");

        customer.IsActive = active;
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private async Task<string?> ValidateAsync(CustomerInput input, int? existingId, CancellationToken ct)
    {
        if (!await _db.Areas.AnyAsync(a => a.Id == input.AreaId, ct))
            return "Please select an area.";

        var phone = PhoneNumber.Normalize(input.Phone);
        if (await _db.Customers.AnyAsync(c => c.Phone == phone && c.IsActive && c.Id != existingId, ct))
            return "Another active customer already has this phone number.";

        if (input.ScheduleType == ScheduleType.FixedWeekdays && input.ScheduleDays == WeekDays.None)
            return "Select at least one delivery day.";

        return null;
    }

    private static void Apply(Customer c, CustomerInput i)
    {
        c.AreaId = i.AreaId;
        c.Name = i.Name.Trim();
        c.Phone = PhoneNumber.Normalize(i.Phone);
        c.WhatsApp = string.IsNullOrWhiteSpace(i.WhatsApp) ? null : PhoneNumber.Normalize(i.WhatsApp);
        c.Address = i.Address.Trim();
        c.Type = i.Type;
        c.Rate = i.Rate;
        c.ScheduleType = i.ScheduleType;
        c.ScheduleDays = i.ScheduleType == ScheduleType.FixedWeekdays ? i.ScheduleDays : WeekDays.None;
        c.MaxBottles = i.MaxBottles;
    }
}
