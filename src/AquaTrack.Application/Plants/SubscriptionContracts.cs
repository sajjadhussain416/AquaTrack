using AquaTrack.Application.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Application.Plants;

// ---------------- Plans (SRS SUB-01) ----------------

public record PlanRow(int Id, string Name, decimal MonthlyPrice, int? MaxCustomers, int? MaxDeliveryBoys, bool IncludesWhatsAppApi, int PlantCount);

public class PlanInput
{
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public int? MaxCustomers { get; set; }
    public int? MaxDeliveryBoys { get; set; }
    public bool IncludesWhatsAppApi { get; set; }
}

// ---------------- One plant's subscription (SRS SUB-02, SUB-03) ----------------

public record SubscriptionPaymentRow(int Id, DateTime PaidOn, decimal Amount, DateTime PeriodFrom, DateTime PeriodTo, string? Note);

public record PlantSubscription(
    int Id, string Name, string OwnerName, string Phone, string City,
    int PlanId, string PlanName, decimal MonthlyPrice, int? MaxCustomers, int? MaxDeliveryBoys,
    PlantStatus Status, DateTime? TrialEndsOn, DateTime? NextDueDate,
    int Customers, int DeliveryStaff, DateTime CreatedAt,
    IReadOnlyList<SubscriptionPaymentRow> Payments)
{
    /// <summary>Trial plants are due when the trial ends; others on the next due date.</summary>
    public DateTime? DueDate => Status == PlantStatus.Trial ? TrialEndsOn : NextDueDate;
    public decimal TotalPaid => Payments.Sum(p => p.Amount);
}

public class SubscriptionPaymentInput
{
    public int PlantId { get; set; }
    public decimal Amount { get; set; }
    public int Months { get; set; } = 1;
    public DateOnly PaidOn { get; set; }
    public string? Note { get; set; }
}

// ---------------- All plants (SRS SUB-04) ----------------

/// <summary>DaysLeft is negative when overdue.</summary>
public record DueRow(int PlantId, string Name, string OwnerName, string Phone, string PlanName, decimal MonthlyPrice,
    PlantStatus Status, DateTime? DueDate, int? DaysLeft);

public record SubscriptionOverview(
    IReadOnlyList<DueRow> Rows,
    int Overdue, int DueThisWeek, int TrialsEndingThisWeek,
    decimal CollectedThisMonth, decimal ExpectedMonthly);

// ---------------- The plant owner's own view ----------------

public record MySubscription(
    string PlanName, decimal MonthlyPrice, PlantStatus Status, DateTime? DueDate, int? DaysLeft,
    int? MaxCustomers, int Customers, int? MaxDeliveryBoys, int DeliveryStaff);

public interface ISubscriptionService
{
    Task<IReadOnlyList<PlanRow>> GetPlansAsync(CancellationToken ct = default);
    Task<PlanInput?> GetPlanAsync(int id, CancellationToken ct = default);
    Task<Result> SavePlanAsync(int? id, PlanInput input, CancellationToken ct = default);
    Task<Result> DeletePlanAsync(int id, CancellationToken ct = default);

    Task<PlantSubscription?> GetPlantAsync(int plantId, CancellationToken ct = default);
    Task<Result> RecordPaymentAsync(SubscriptionPaymentInput input, CancellationToken ct = default);
    Task<Result> DeletePaymentAsync(int paymentId, CancellationToken ct = default);
    Task<Result> ChangePlanAsync(int plantId, int planId, CancellationToken ct = default);
    Task<Result> ExtendTrialAsync(int plantId, int days, CancellationToken ct = default);

    /// <summary>filter: "all", "overdue", "week" (due in 7 days), "trial".</summary>
    Task<SubscriptionOverview> GetOverviewAsync(string? filter, CancellationToken ct = default);

    /// <summary>For the logged-in plant owner. Null for Super Admin.</summary>
    Task<MySubscription?> GetMineAsync(CancellationToken ct = default);
}
