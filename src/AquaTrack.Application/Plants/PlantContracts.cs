using AquaTrack.Application.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Application.Plants;

public record PlanOption(int Id, string Name, decimal MonthlyPrice);

public record PlantListItem(
    int Id,
    string Name,
    string OwnerName,
    string Phone,
    string City,
    string PlanName,
    decimal MonthlyPrice,
    PlantStatus Status,
    DateTime? TrialEndsOn,
    DateTime? NextDueDate,
    int CustomerCount,
    DateTime CreatedAt);

public record PlatformStats(
    int TotalPlants,
    int ActivePlants,
    int TrialPlants,
    int SuspendedPlants,
    int TotalCustomers,
    decimal ExpectedMonthlyRevenue);

public class CreatePlantRequest
{
    public string PlantName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string PlantPhone { get; set; } = string.Empty;
    public int PlanId { get; set; }
    public decimal DefaultRate { get; set; }
    public decimal DepositPerBottle { get; set; }
    public int BillingDay { get; set; } = 1;
    public int TotalBottles { get; set; }

    public string OwnerName { get; set; } = string.Empty;
    public string OwnerPhone { get; set; } = string.Empty;
    public string OwnerPassword { get; set; } = string.Empty;
}

/// <summary>Super Admin operations on plants (SRS PLANT-01..04, SUB-04).</summary>
public interface IPlantService
{
    Task<PlatformStats> GetPlatformStatsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PlantListItem>> GetPlantsAsync(string? search, PlantStatus? status, CancellationToken ct = default);
    Task<IReadOnlyList<PlantListItem>> GetRecentPlantsAsync(int count, CancellationToken ct = default);
    Task<IReadOnlyList<PlanOption>> GetPlansAsync(CancellationToken ct = default);
    Task<Result<int>> CreatePlantAsync(CreatePlantRequest request, CancellationToken ct = default);
    Task<Result> SetStatusAsync(int plantId, PlantStatus status, CancellationToken ct = default);

    /// <summary>Used at login, before any tenant is known.</summary>
    Task<PlantStatus?> GetStatusForLoginAsync(int plantId, CancellationToken ct = default);
}
