using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

/// <summary>Subscription plan defined by Super Admin (SRS SUB-01). Not tenant-owned.</summary>
public class Plan : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Null = unlimited customers.</summary>
    public int? MaxCustomers { get; set; }

    /// <summary>Null = unlimited delivery boys.</summary>
    public int? MaxDeliveryBoys { get; set; }

    public decimal MonthlyPrice { get; set; }
    public bool IncludesWhatsAppApi { get; set; }

    public ICollection<Plant> Plants { get; set; } = new List<Plant>();
}

/// <summary>One water plant business = one tenant (SRS PLANT-01/02).</summary>
public class Plant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? LogoUrl { get; set; }

    /// <summary>Default price per 19 L bottle in PKR, copied to new customers.</summary>
    public decimal DefaultRate { get; set; }

    /// <summary>Day of month (1-28) when monthly bills are generated.</summary>
    public int BillingDay { get; set; } = 1;

    public decimal DepositPerBottle { get; set; }

    public int PlanId { get; set; }
    public Plan? Plan { get; set; }

    public PlantStatus Status { get; set; } = PlantStatus.Trial;
    public DateTime? TrialEndsOn { get; set; }
    public DateTime? NextDueDate { get; set; }

    public ICollection<SubscriptionPayment> SubscriptionPayments { get; set; } = new List<SubscriptionPayment>();
}

/// <summary>Payment a plant makes to the product owner (SRS SUB-03).</summary>
public class SubscriptionPayment : TenantEntity
{
    public Plant? Plant { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidOn { get; set; }
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public string? Note { get; set; }
}
