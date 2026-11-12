using AquaTrack.Application.Billing;
using AquaTrack.Application.Common;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Services;

/// <summary>Payments taken at the plant and the monthly payment register (SRS PAY-01..03).</summary>
public class PaymentService : IPaymentService
{
    private const decimal MaxPayment = 10_000_000m;

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public PaymentService(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PaymentMonthView> GetMonthAsync(BillPeriod p, string? search, PaymentMethod? method, CancellationToken ct = default)
    {
        var startUtc = p.StartUtc;
        var nextUtc = p.NextStartUtc;

        var query = _db.Payments.AsNoTracking().Where(x => x.Date >= startUtc && x.Date < nextUtc);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var phone = PhoneNumber.Normalize(s);
            query = query.Where(x => x.Customer!.Name.Contains(s) || (phone.Length > 0 && x.Customer.Phone.Contains(phone)));
        }

        if (method.HasValue) query = query.Where(x => x.Method == method.Value);

        var rows = await query
            .OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id, x.Date, x.CustomerId, CustomerName = x.Customer!.Name, AreaName = x.Customer.Area!.Name,
                x.Amount, x.Method, x.ReceivedById, FromDelivery = x.DeliveryId != null, x.Note
            })
            .ToListAsync(ct);

        var names = await UserNamesAsync(ct);

        var items = rows.Select(x => new PaymentListItem(
                x.Id, x.Date, x.CustomerId, x.CustomerName, x.AreaName, x.Amount, x.Method,
                names.GetValueOrDefault(x.ReceivedById, "-"), x.FromDelivery, x.Note))
            .ToList();

        var byMethod = items
            .GroupBy(i => i.Method)
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, g.Sum(i => i.Amount)))
            .ToList();

        return new PaymentMonthView(p, items, items.Sum(i => i.Amount), byMethod);
    }

    public async Task<IReadOnlyList<CustomerBalanceOption>> GetCustomerOptionsAsync(CancellationToken ct = default)
    {
        var balances = await Ledger.BalancesAsync(_db, ct);

        var customers = await _db.Customers.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Phone, AreaName = c.Area!.Name, c.IsActive })
            .ToListAsync(ct);

        return customers
            .Select(c => new { c, Balance = balances.GetValueOrDefault(c.Id) })
            .Where(x => x.c.IsActive || x.Balance != 0)
            .Select(x => new CustomerBalanceOption(x.c.Id, x.c.Name, x.c.Phone, x.c.AreaName, x.Balance))
            .ToList();
    }

    public async Task<Result<int>> RecordAsync(PaymentInput input, CancellationToken ct = default)
    {
        var today = PakistanTime.Today;

        if (!await _db.Customers.AnyAsync(c => c.Id == input.CustomerId, ct))
            return Result<int>.Fail("Customer not found.");

        if (input.Amount <= 0 || input.Amount > MaxPayment)
            return Result<int>.Fail("Enter an amount greater than zero.");

        if (input.Date > today)
            return Result<int>.Fail("Payment date cannot be in the future.");

        if (input.Date < today.AddYears(-1))
            return Result<int>.Fail("Payment date is too old.");

        var payment = new Payment
        {
            CustomerId = input.CustomerId,
            Amount = decimal.Round(input.Amount, 2),
            Method = input.Method,
            ReceivedById = _currentUser.UserId!,
            Date = Ledger.StampFor(input.Date, today),
            Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim()[..Math.Min(input.Note.Trim().Length, 300)]
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);
        return Result<int>.Ok(payment.Id);
    }

    public async Task<ReceiptView?> GetReceiptAsync(int id, CancellationToken ct = default)
    {
        var x = await _db.Payments.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id, p.Date, p.CustomerId, CustomerName = p.Customer!.Name, p.Customer.Phone, AreaName = p.Customer.Area!.Name,
                p.Amount, p.Method, p.ReceivedById, p.Note
            })
            .FirstOrDefaultAsync(ct);

        if (x is null) return null;

        var names = await UserNamesAsync(ct);

        return new ReceiptView(
            x.Id, x.Date, x.CustomerId, x.CustomerName, x.Phone, x.AreaName, x.Amount, x.Method,
            names.GetValueOrDefault(x.ReceivedById, "-"), x.Note,
            await Ledger.BalanceAsync(_db, x.CustomerId, ct),
            await Ledger.PlantHeaderAsync(_db, ct));
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (payment is null) return Result.Fail("Payment not found.");

        if (payment.DeliveryId != null)
            return Result.Fail("This cash was collected at a delivery. Change it from the Deliveries page.");

        _db.Payments.Remove(payment); // soft delete, audit-logged
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private async Task<Dictionary<string, string>> UserNamesAsync(CancellationToken ct)
    {
        var plantId = _currentUser.PlantId;
        return await _db.Users.AsNoTracking()
            .Where(u => u.PlantId == plantId)
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }
}
