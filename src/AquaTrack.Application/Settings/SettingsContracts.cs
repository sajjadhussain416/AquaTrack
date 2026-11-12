using AquaTrack.Application.Common;

namespace AquaTrack.Application.Settings;

/// <summary>Where customers can send money. Shown on route cards, bills and WhatsApp messages.</summary>
public record PaymentAccounts(
    string? EasypaisaNumber,
    string? JazzCashNumber,
    string? BankName,
    string? BankAccountTitle,
    string? BankAccountNumber,
    string? EasypaisaTitle,
    string? JazzCashTitle)
{
    public bool HasEasypaisa => !string.IsNullOrWhiteSpace(EasypaisaNumber);
    public bool HasJazzCash => !string.IsNullOrWhiteSpace(JazzCashNumber);
    public bool HasBank => !string.IsNullOrWhiteSpace(BankAccountNumber);
    public bool Any => HasEasypaisa || HasJazzCash || HasBank;

    public static PaymentAccounts None => new(null, null, null, null, null, null, null);

    /// <summary>IBAN printed in groups of four so customers can type it without mistakes.</summary>
    public string BankAccountDisplay => BankAccount.Pretty(BankAccountNumber);
}

public class PlantSettingsInput
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Address { get; set; }
    public decimal DefaultRate { get; set; }
    public decimal DepositPerBottle { get; set; }
    public int BillingDay { get; set; } = 1;

    public string? EasypaisaNumber { get; set; }
    public string? EasypaisaTitle { get; set; }
    public string? JazzCashNumber { get; set; }
    public string? JazzCashTitle { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountTitle { get; set; }
    public string? BankAccountNumber { get; set; }
}

/// <summary>The logged-in owner's own plant (SRS PLANT-02).</summary>
public interface IPlantSettingsService
{
    Task<PlantSettingsInput> GetAsync(CancellationToken ct = default);
    Task<Result> UpdateAsync(PlantSettingsInput input, CancellationToken ct = default);
    Task<PaymentAccounts> GetPaymentAccountsAsync(CancellationToken ct = default);
}
