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
                x.Amount, x.Method, x.ReceivedById, FromDelivery = x.DeliveryId != null, x.TransactionRef, x.Verification, x.Note
            })
            .ToListAsync(ct);

        var names = await UserNamesAsync(ct);

        var items = rows.Select(x => new PaymentListItem(
                x.Id, x.Date, x.CustomerId, x.CustomerName, x.AreaName, x.Amount, x.Method,
                names.GetValueOrDefault(x.ReceivedById, "-"), x.FromDelivery, x.TransactionRef, x.Verification, x.Note))
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

        var tid = string.IsNullOrWhiteSpace(input.TransactionRef) || input.Method == PaymentMethod.Cash
            ? null
            : input.TransactionRef.Trim()[..Math.Min(input.TransactionRef.Trim().Length, 50)];

        var payment = new Payment
        {
            CustomerId = input.CustomerId,
            Amount = decimal.Round(input.Amount, 2),
            Method = input.Method,
            TransactionRef = tid,
            Verification = PaymentVerification.Verified,
            VerifiedById = input.Method == PaymentMethod.Cash ? null : _currentUser.UserId,
            VerifiedAt = input.Method == PaymentMethod.Cash ? null : DateTime.UtcNow,
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
                p.Amount, p.Method, p.ReceivedById, p.TransactionRef, p.Verification, p.Note
            })
            .FirstOrDefaultAsync(ct);

        if (x is null) return null;

        var names = await UserNamesAsync(ct);

        return new ReceiptView(
            x.Id, x.Date, x.CustomerId, x.CustomerName, x.Phone, x.AreaName, x.Amount, x.Method,
            names.GetValueOrDefault(x.ReceivedById, "-"), x.TransactionRef, x.Verification, x.Note,
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

    // ------------------------------------------------------------------ Verification

    public async Task<IReadOnlyList<PaymentListItem>> GetPendingAsync(CancellationToken ct = default)
    {
        var rows = await _db.Payments.AsNoTracking()
            .Where(x => x.Verification == PaymentVerification.Pending)
            .OrderBy(x => x.Date)
            .Select(x => new
            {
                x.Id, x.Date, x.CustomerId, CustomerName = x.Customer!.Name, AreaName = x.Customer.Area!.Name,
                x.Amount, x.Method, x.ReceivedById, FromDelivery = x.DeliveryId != null, x.TransactionRef, x.Verification, x.Note
            })
            .ToListAsync(ct);

        var names = await UserNamesAsync(ct);

        return rows.Select(x => new PaymentListItem(
                x.Id, x.Date, x.CustomerId, x.CustomerName, x.AreaName, x.Amount, x.Method,
                names.GetValueOrDefault(x.ReceivedById, "-"), x.FromDelivery, x.TransactionRef, x.Verification, x.Note))
            .ToList();
    }

    public Task<int> GetPendingCountAsync(CancellationToken ct = default) =>
        _db.Payments.CountAsync(x => x.Verification == PaymentVerification.Pending, ct);

    public async Task<Result> VerifyAsync(int id, CancellationToken ct = default)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (payment is null) return Result.Fail("Payment not found.");
        if (payment.Verification != PaymentVerification.Pending) return Result.Fail("This payment is already checked.");

        payment.Verification = PaymentVerification.Verified;
        payment.VerifiedById = _currentUser.UserId;
        payment.VerifiedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RejectAsync(int id, string? reason, CancellationToken ct = default)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (payment is null) return Result.Fail("Payment not found.");
        if (payment.Verification != PaymentVerification.Pending) return Result.Fail("This payment is already checked.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var why = string.IsNullOrWhiteSpace(reason) ? "Payment not received" : reason.Trim()[..Math.Min(reason.Trim().Length, 150)];

        // Keep a trail in the audit log, then remove the money from the customer's account
        payment.Verification = PaymentVerification.Rejected;
        payment.VerifiedById = _currentUser.UserId;
        payment.VerifiedAt = DateTime.UtcNow;
        payment.Note = $"Rejected: {why}";
        await _db.SaveChangesAsync(ct);

        if (payment.DeliveryId.HasValue)
        {
            var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.Id == payment.DeliveryId.Value, ct);
            if (delivery is not null)
            {
                delivery.CashCollected = 0;
                delivery.PaymentMethod = PaymentMethod.Cash;
                delivery.TransactionRef = null;
                var method = payment.Method == PaymentMethod.BankTransfer ? "Bank transfer" : payment.Method.ToString();
                var note = $"Rejected {method} Rs {payment.Amount:0} ({why})";
                delivery.Note = string.IsNullOrEmpty(delivery.Note) ? note : $"{delivery.Note} · {note}"[..Math.Min(delivery.Note.Length + note.Length + 3, 300)];
            }
        }

        _db.Payments.Remove(payment);
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
}
