using System.ComponentModel.DataAnnotations;
using AquaTrack.Application.Operations;
using AquaTrack.Domain.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AquaTrack.Web.Models.Operations;

// ---------- Areas ----------
public class AreaFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Please enter {0}.")]
    [StringLength(100, ErrorMessage = "{0} is too long.")]
    [Display(Name = "Area name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Delivery staff")]
    public string? DeliveryBoyId { get; set; }

    [ValidateNever] public IReadOnlyList<StaffOption> Staff { get; set; } = Array.Empty<StaffOption>();
}

public class AreasPageViewModel
{
    public IReadOnlyList<AreaListItem> Areas { get; set; } = Array.Empty<AreaListItem>();
    public AreaFormViewModel Form { get; set; } = new();
}

// ---------- Delivery staff ----------
public class StaffFormViewModel
{
    [Required(ErrorMessage = "Please enter {0}.")]
    [StringLength(150, ErrorMessage = "{0} is too long.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter {0}.")]
    [Display(Name = "Mobile number")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter {0}.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Areas")]
    public List<int> AreaIds { get; set; } = new();

    [ValidateNever] public IReadOnlyList<AreaOption> AllAreas { get; set; } = Array.Empty<AreaOption>();
}

// ---------- Customers ----------
public class CustomerFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Please enter {0}.")]
    [StringLength(150, ErrorMessage = "{0} is too long.")]
    [Display(Name = "Customer name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter {0}.")]
    [StringLength(20, ErrorMessage = "{0} is too long.")]
    [Display(Name = "Phone")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "{0} is too long.")]
    [Display(Name = "WhatsApp number")]
    public string? WhatsApp { get; set; }

    [Required(ErrorMessage = "Please enter {0}.")]
    [StringLength(300, ErrorMessage = "{0} is too long.")]
    [Display(Name = "Address")]
    public string Address { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Please select an area.")]
    [Display(Name = "Area")]
    public int AreaId { get; set; }

    [Display(Name = "Customer type")]
    public CustomerType Type { get; set; } = CustomerType.Home;

    [Range(0, 100000, ErrorMessage = "{0} must be between {1} and {2}.")]
    [Display(Name = "Price per bottle (PKR)")]
    public decimal Rate { get; set; }

    [Display(Name = "Delivery schedule")]
    public ScheduleType ScheduleType { get; set; } = ScheduleType.Daily;

    public List<WeekDays> Days { get; set; } = new();

    [Range(0, 1000, ErrorMessage = "{0} must be between {1} and {2}.")]
    [Display(Name = "Bottle limit")]
    public int? MaxBottles { get; set; }

    [Range(0, 1000, ErrorMessage = "{0} must be between {1} and {2}.")]
    [Display(Name = "Bottles given at start")]
    public int OpeningBottles { get; set; }

    [Range(0, 10000000, ErrorMessage = "{0} must be between {1} and {2}.")]
    [Display(Name = "Security deposit taken (PKR)")]
    public decimal DepositTaken { get; set; }

    [ValidateNever] public IReadOnlyList<AreaOption> Areas { get; set; } = Array.Empty<AreaOption>();

    public bool IsEdit => Id.HasValue;

    public CustomerInput ToInput() => new()
    {
        AreaId = AreaId,
        Name = Name,
        Phone = Phone,
        WhatsApp = WhatsApp,
        Address = Address,
        Type = Type,
        Rate = Rate,
        ScheduleType = ScheduleType,
        ScheduleDays = Days.Aggregate(WeekDays.None, (acc, d) => acc | d),
        MaxBottles = MaxBottles,
        OpeningBottles = OpeningBottles,
        DepositTaken = DepositTaken
    };

    public static CustomerFormViewModel From(CustomerDetails d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Phone = d.Phone,
        WhatsApp = d.WhatsApp,
        Address = d.Address,
        AreaId = d.AreaId,
        Type = d.Type,
        Rate = d.Rate,
        ScheduleType = d.ScheduleType,
        Days = Enum.GetValues<WeekDays>().Where(w => w != WeekDays.None && d.ScheduleDays.HasFlag(w)).ToList(),
        MaxBottles = d.MaxBottles
    };
}

public class CustomersIndexViewModel
{
    public CustomerPage Page { get; set; } = new(Array.Empty<CustomerListItem>(), 0, 1, 25);
    public IReadOnlyList<AreaOption> Areas { get; set; } = Array.Empty<AreaOption>();
    public string? Search { get; set; }
    public int? AreaId { get; set; }
    public bool ShowInactive { get; set; }
}
