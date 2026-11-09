using System.ComponentModel.DataAnnotations;
using AquaTrack.Application.Plants;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AquaTrack.Web.Areas.SuperAdmin.Models;

public class PlantFormViewModel
{
    // ---- Plant ----
    [Required(ErrorMessage = "Please enter {0}.")]
    [StringLength(150, ErrorMessage = "{0} is too long.")]
    [Display(Name = "Plant name")]
    public string PlantName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter {0}.")]
    [StringLength(80, ErrorMessage = "{0} is too long.")]
    [Display(Name = "City")]
    public string City { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "{0} is too long.")]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [Required(ErrorMessage = "Please enter {0}.")]
    [Display(Name = "Plant phone")]
    public string PlantPhone { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Please select a plan.")]
    [Display(Name = "Plan")]
    public int PlanId { get; set; }

    [Range(0, 100000, ErrorMessage = "{0} must be between {1} and {2}.")]
    [Display(Name = "Price per bottle (PKR)")]
    public decimal DefaultRate { get; set; } = 100;

    [Range(0, 100000, ErrorMessage = "{0} must be between {1} and {2}.")]
    [Display(Name = "Security deposit per bottle (PKR)")]
    public decimal DepositPerBottle { get; set; } = 500;

    [Range(1, 28, ErrorMessage = "{0} must be between {1} and {2}.")]
    [Display(Name = "Billing day of month")]
    public int BillingDay { get; set; } = 1;

    [Range(0, 100000, ErrorMessage = "{0} must be between {1} and {2}.")]
    [Display(Name = "Total bottles owned")]
    public int TotalBottles { get; set; }

    // ---- Owner login ----
    [Required(ErrorMessage = "Please enter {0}.")]
    [StringLength(150, ErrorMessage = "{0} is too long.")]
    [Display(Name = "Owner name")]
    public string OwnerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter {0}.")]
    [Display(Name = "Owner mobile number")]
    public string OwnerPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter {0}.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "Owner password")]
    public string OwnerPassword { get; set; } = string.Empty;

    [ValidateNever]
    public IReadOnlyList<PlanOption> Plans { get; set; } = Array.Empty<PlanOption>();

    public CreatePlantRequest ToRequest() => new()
    {
        PlantName = PlantName,
        City = City,
        Address = Address,
        PlantPhone = PlantPhone,
        PlanId = PlanId,
        DefaultRate = DefaultRate,
        DepositPerBottle = DepositPerBottle,
        BillingDay = BillingDay,
        TotalBottles = TotalBottles,
        OwnerName = OwnerName,
        OwnerPhone = OwnerPhone,
        OwnerPassword = OwnerPassword
    };
}
