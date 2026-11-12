using System.ComponentModel.DataAnnotations;
using AquaTrack.Application.Common;
using AquaTrack.Application.Settings;

namespace AquaTrack.Web.Models.Settings;

public class SettingsViewModel
{
    [Required(ErrorMessage = "Please enter {0}.")]
    [StringLength(150, ErrorMessage = "{0} is too long.")]
    [Display(Name = "Plant name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter {0}.")]
    [Display(Name = "Plant phone")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter {0}.")]
    [StringLength(80, ErrorMessage = "{0} is too long.")]
    [Display(Name = "City")]
    public string City { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "{0} is too long.")]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [Range(0, 100000, ErrorMessage = "{0} must be between {1} and {2}.")]
    [Display(Name = "Price per bottle (PKR)")]
    public decimal DefaultRate { get; set; }

    [Range(0, 100000, ErrorMessage = "{0} must be between {1} and {2}.")]
    [Display(Name = "Security deposit per bottle (PKR)")]
    public decimal DepositPerBottle { get; set; }

    [Range(1, 28, ErrorMessage = "{0} must be between {1} and {2}.")]
    [Display(Name = "Billing day of month")]
    public int BillingDay { get; set; } = 1;

    [Display(Name = "Easypaisa number")]
    public string? EasypaisaNumber { get; set; }

    [StringLength(150, ErrorMessage = "{0} is too long.")]
    [Display(Name = "Account name")]
    public string? EasypaisaTitle { get; set; }

    [Display(Name = "JazzCash number")]
    public string? JazzCashNumber { get; set; }

    [StringLength(150, ErrorMessage = "{0} is too long.")]
    [Display(Name = "Account name")]
    public string? JazzCashTitle { get; set; }

    /// <summary>A bank from <see cref="PakistanBanks.All"/>, or <see cref="PakistanBanks.Other"/>.</summary>
    [Display(Name = "Bank")]
    public string? BankChoice { get; set; }

    [StringLength(100, ErrorMessage = "{0} is too long.")]
    [Display(Name = "Bank name")]
    public string? BankOther { get; set; }

    public string? BankName => BankChoice == PakistanBanks.Other ? BankOther?.Trim() : BankChoice;

    [StringLength(150, ErrorMessage = "{0} is too long.")]
    [Display(Name = "Account title")]
    public string? BankAccountTitle { get; set; }

    [StringLength(40, ErrorMessage = "{0} is too long.")]
    [Display(Name = "Account number / IBAN")]
    public string? BankAccountNumber { get; set; }

    public static SettingsViewModel From(PlantSettingsInput i) => new()
    {
        Name = i.Name, Phone = i.Phone, City = i.City, Address = i.Address,
        DefaultRate = i.DefaultRate, DepositPerBottle = i.DepositPerBottle, BillingDay = i.BillingDay,
        EasypaisaNumber = i.EasypaisaNumber, EasypaisaTitle = i.EasypaisaTitle,
        JazzCashNumber = i.JazzCashNumber, JazzCashTitle = i.JazzCashTitle,
        BankChoice = string.IsNullOrWhiteSpace(i.BankName) ? null
            : PakistanBanks.Find(i.BankName)?.Name ?? PakistanBanks.Other,
        BankOther = string.IsNullOrWhiteSpace(i.BankName) || PakistanBanks.Find(i.BankName) is not null ? null : i.BankName,
        BankAccountTitle = i.BankAccountTitle,
        BankAccountNumber = BankAccount.Pretty(i.BankAccountNumber)
    };

    public PlantSettingsInput ToInput() => new()
    {
        Name = Name, Phone = Phone, City = City, Address = Address,
        DefaultRate = DefaultRate, DepositPerBottle = DepositPerBottle, BillingDay = BillingDay,
        EasypaisaNumber = EasypaisaNumber, EasypaisaTitle = EasypaisaTitle,
        JazzCashNumber = JazzCashNumber, JazzCashTitle = JazzCashTitle,
        BankName = BankName, BankAccountTitle = BankAccountTitle, BankAccountNumber = BankAccountNumber
    };
}
