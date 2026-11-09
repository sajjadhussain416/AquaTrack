using AquaTrack.Application.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Application.Operations;

// ===================== Areas (SRS AREA-01..03) =====================

public record AreaListItem(int Id, string Name, string? DeliveryBoyId, string? DeliveryBoyName, int CustomerCount);

public record AreaOption(int Id, string Name);

public interface IAreaService
{
    Task<IReadOnlyList<AreaListItem>> GetAreasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AreaOption>> GetOptionsAsync(CancellationToken ct = default);
    Task<AreaListItem?> GetAsync(int id, CancellationToken ct = default);
    Task<Result> CreateAsync(string name, string? deliveryBoyId, CancellationToken ct = default);
    Task<Result> UpdateAsync(int id, string name, string? deliveryBoyId, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

// ===================== Delivery staff (SRS STAFF-01) =====================

public record StaffListItem(string Id, string FullName, string Phone, bool IsActive, IReadOnlyList<string> Areas);

public record StaffOption(string Id, string FullName);

public interface IStaffService
{
    Task<IReadOnlyList<StaffListItem>> GetDeliveryStaffAsync(CancellationToken ct = default);
    Task<IReadOnlyList<StaffOption>> GetOptionsAsync(CancellationToken ct = default);
    Task<Result> CreateDeliveryBoyAsync(string fullName, string phone, string password, IReadOnlyList<int> areaIds, CancellationToken ct = default);
    Task<Result> SetActiveAsync(string userId, bool active, CancellationToken ct = default);
    Task<Result> ResetPasswordAsync(string userId, string newPassword, CancellationToken ct = default);
}

// ===================== Customers (SRS CUST-01..07, BTL-01) =====================

public record CustomerListItem(
    int Id,
    string Name,
    string Phone,
    string Address,
    string AreaName,
    CustomerType Type,
    decimal Rate,
    ScheduleType ScheduleType,
    int BottlesInHand,
    bool IsActive);

public record CustomerPage(IReadOnlyList<CustomerListItem> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public class CustomerInput
{
    public int AreaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? WhatsApp { get; set; }
    public string Address { get; set; } = string.Empty;
    public CustomerType Type { get; set; } = CustomerType.Home;
    public decimal Rate { get; set; }
    public ScheduleType ScheduleType { get; set; } = ScheduleType.Daily;
    public WeekDays ScheduleDays { get; set; } = WeekDays.None;
    public int? MaxBottles { get; set; }

    // Only used when the customer is created (SRS CUST-04)
    public int OpeningBottles { get; set; }
    public decimal DepositTaken { get; set; }
}

public record CustomerMovement(DateTime Date, BottleMovementType Type, int Quantity, string? Note);

public record CustomerDetails(
    int Id,
    string Name,
    string Phone,
    string? WhatsApp,
    string Address,
    int AreaId,
    string AreaName,
    CustomerType Type,
    decimal Rate,
    ScheduleType ScheduleType,
    WeekDays ScheduleDays,
    int? MaxBottles,
    bool IsActive,
    int BottlesInHand,
    decimal DepositHeld,
    DateTime CreatedAt,
    IReadOnlyList<CustomerMovement> RecentMovements);

public interface ICustomerService
{
    Task<CustomerPage> GetPageAsync(string? search, int? areaId, bool includeInactive, int page, CancellationToken ct = default);
    Task<CustomerDetails?> GetDetailsAsync(int id, CancellationToken ct = default);
    Task<decimal> GetDefaultRateAsync(CancellationToken ct = default);
    Task<Result<int>> CreateAsync(CustomerInput input, CancellationToken ct = default);
    Task<Result> UpdateAsync(int id, CustomerInput input, CancellationToken ct = default);
    Task<Result> SetActiveAsync(int id, bool active, CancellationToken ct = default);
}

// ===================== Owner dashboard =====================

public record PlantSummary(
    int ActiveCustomers,
    int BottlesWithCustomers,
    int Areas,
    int DeliveryStaff,
    int TodayDeliveries,
    decimal TodayCash);

public interface IPlantDashboardService
{
    Task<PlantSummary> GetSummaryAsync(CancellationToken ct = default);
}
