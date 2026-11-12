using AquaTrack.Application.Common;
using AquaTrack.Application.Settings;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Services;

/// <summary>The logged-in owner's own plant details and payment accounts (SRS PLANT-02).</summary>
public class PlantSettingsService : IPlantSettingsService
{
    private readonly AppDbContext _db;

    public PlantSettingsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PlantSettingsInput> GetAsync(CancellationToken ct = default)
    {
        var p = await _db.Plants.AsNoTracking().FirstAsync(ct);
        return new PlantSettingsInput
        {
            Name = p.Name,
            Phone = p.Phone,
            City = p.City,
            Address = p.Address,
            DefaultRate = p.DefaultRate,
            DepositPerBottle = p.DepositPerBottle,
            BillingDay = p.BillingDay,
            EasypaisaNumber = p.EasypaisaNumber,
            EasypaisaTitle = p.EasypaisaTitle,
            JazzCashNumber = p.JazzCashNumber,
            JazzCashTitle = p.JazzCashTitle,
            BankName = p.BankName,
            BankAccountTitle = p.BankAccountTitle,
            BankAccountNumber = p.BankAccountNumber
        };
    }

    public async Task<Result> UpdateAsync(PlantSettingsInput i, CancellationToken ct = default)
    {
        // Mobile wallets: a valid number and the name the customer will see when sending money.
        var wallet = CheckWallet(i.EasypaisaNumber, i.EasypaisaTitle, "Enter a valid Easypaisa number like 03001234567.", "Enter the name on the Easypaisa account.");
        if (wallet is not null) return Result.Fail(wallet);
        wallet = CheckWallet(i.JazzCashNumber, i.JazzCashTitle, "Enter a valid JazzCash number like 03001234567.", "Enter the name on the JazzCash account.");
        if (wallet is not null) return Result.Fail(wallet);

        // Bank: all three fields, and a correct IBAN or account number.
        var account = BankAccount.Normalize(i.BankAccountNumber);
        var hasBankNumber = account.Length > 0;
        var anyBankField = hasBankNumber || !string.IsNullOrWhiteSpace(i.BankName) || !string.IsNullOrWhiteSpace(i.BankAccountTitle);
        if (anyBankField)
        {
            if (string.IsNullOrWhiteSpace(i.BankName) || string.IsNullOrWhiteSpace(i.BankAccountTitle) || !hasBankNumber)
                return Result.Fail("For a bank account, enter the bank name, account title and account number / IBAN.");

            if (BankAccount.LooksLikeIban(account))
            {
                if (!BankAccount.IsValidIban(account))
                    return Result.Fail("This IBAN is not correct. A Pakistani IBAN has 24 characters and starts with PK. Please check it again.");

                // The IBAN decides the bank, so the two can never disagree.
                var fromIban = PakistanBanks.FromIban(account);
                if (fromIban is not null) i.BankName = fromIban.Name;
            }
            else if (!BankAccount.IsValidAccountNumber(account))
            {
                return Result.Fail("Enter the IBAN (starts with PK) or the account number in digits only.");
            }
        }

        var p = await _db.Plants.FirstAsync(ct);

        p.Name = i.Name.Trim();
        p.Phone = PhoneNumber.Normalize(i.Phone);
        p.City = i.City.Trim();
        p.Address = Clean(i.Address);
        p.DefaultRate = i.DefaultRate;
        p.DepositPerBottle = i.DepositPerBottle;
        p.BillingDay = i.BillingDay;

        p.EasypaisaNumber = string.IsNullOrWhiteSpace(i.EasypaisaNumber) ? null : PhoneNumber.Normalize(i.EasypaisaNumber);
        p.EasypaisaTitle = p.EasypaisaNumber is null ? null : Clean(i.EasypaisaTitle);
        p.JazzCashNumber = string.IsNullOrWhiteSpace(i.JazzCashNumber) ? null : PhoneNumber.Normalize(i.JazzCashNumber);
        p.JazzCashTitle = p.JazzCashNumber is null ? null : Clean(i.JazzCashTitle);
        p.BankName = hasBankNumber ? Clean(i.BankName) : null;
        p.BankAccountTitle = hasBankNumber ? Clean(i.BankAccountTitle) : null;
        p.BankAccountNumber = hasBankNumber ? account : null;

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public Task<PaymentAccounts> GetPaymentAccountsAsync(CancellationToken ct = default) => Accounts(_db, ct);

    internal static async Task<PaymentAccounts> Accounts(AppDbContext db, CancellationToken ct) =>
        await db.Plants.AsNoTracking()
            .Select(p => new PaymentAccounts(p.EasypaisaNumber, p.JazzCashNumber, p.BankName, p.BankAccountTitle, p.BankAccountNumber, p.EasypaisaTitle, p.JazzCashTitle))
            .FirstOrDefaultAsync(ct)
        ?? PaymentAccounts.None;

    /// <summary>Null when the wallet is fine or not used; otherwise the error to show.</summary>
    private static string? CheckWallet(string? number, string? title, string badNumber, string noTitle)
    {
        if (string.IsNullOrWhiteSpace(number)) return null;
        if (!PhoneNumber.IsValidMobile(number)) return badNumber;
        if (string.IsNullOrWhiteSpace(title)) return noTitle;
        return null;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
