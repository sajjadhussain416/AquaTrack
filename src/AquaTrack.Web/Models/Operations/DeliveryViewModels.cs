using System.ComponentModel.DataAnnotations;
using AquaTrack.Application.Operations;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Web.Models.Operations;

/// <summary>Posted from one customer card on the route screen.</summary>
public class DeliveryFormModel
{
    public int CustomerId { get; set; }
    public DateOnly Date { get; set; }
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Done;
    public int Filled { get; set; }
    public int Empties { get; set; }
    public decimal Cash { get; set; }
    public string? Note { get; set; }

    public DeliveryEntry ToEntry() => new(CustomerId, Date, Status, Filled, Empties, Cash, Note);
}

public class RoutePageViewModel
{
    public RouteDay Route { get; set; } = null!;
    public bool CanPickDate { get; set; }
    public DateOnly Today { get; set; }
}

public class DeliveryEditViewModel
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string AreaName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }

    [Display(Name = "Status")]
    public DeliveryStatus Status { get; set; }

    [Range(0, 200, ErrorMessage = "{0} must be between {1} and {2}.")]
    [Display(Name = "Filled bottles given")]
    public int Filled { get; set; }

    [Range(0, 200, ErrorMessage = "{0} must be between {1} and {2}.")]
    [Display(Name = "Empty bottles collected")]
    public int Empties { get; set; }

    [Range(0, 1000000, ErrorMessage = "{0} must be between {1} and {2}.")]
    [Display(Name = "Cash received (PKR)")]
    public decimal Cash { get; set; }

    [StringLength(300, ErrorMessage = "{0} is too long.")]
    [Display(Name = "Note")]
    public string? Note { get; set; }

    public static DeliveryEditViewModel From(DeliveryRow r) => new()
    {
        Id = r.Id, CustomerId = r.CustomerId, CustomerName = r.CustomerName, AreaName = r.AreaName,
        Date = r.Date, Status = r.Status, Filled = r.Filled, Empties = r.Empties, Cash = r.Cash, Note = r.Note
    };
}

public class DeliveriesIndexViewModel
{
    public DeliveryDayReport Report { get; set; } = null!;
    public int? AreaId { get; set; }
    public string? DeliveryBoyId { get; set; }
    public DateOnly Today { get; set; }
}
