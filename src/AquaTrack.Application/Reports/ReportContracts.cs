using AquaTrack.Application.Billing;
using AquaTrack.Application.Common;

namespace AquaTrack.Application.Reports;

// ---------------- Udhaar list (SRS RPT-02) ----------------

/// <summary>
/// A customer who owes money. UnpaidSince = first day of the month of the oldest bill that payments have not covered;
/// DaysUnpaid counts from when that bill fell due (the 1st of the following month), 0 while it is not due yet.
/// </summary>
public record UdhaarRow(
    int CustomerId, string Name, string Phone, string? WhatsApp, string AreaName,
    decimal Balance, DateOnly? UnpaidSince, int DaysUnpaid,
    DateOnly? LastPaymentDate, decimal LastPaymentAmount);

public record UdhaarReport(
    IReadOnlyList<UdhaarRow> Rows,
    decimal Total, decimal Over30Days, decimal Over60Days,
    int AdvanceCustomers, decimal AdvanceTotal,
    PlantHeader Plant);

// ---------------- Bottles (SRS RPT-03, BTL-01, BTL-02, BTL-04) ----------------

public record BottleRow(
    int CustomerId, string Name, string Phone, string AreaName, bool IsActive,
    int InHand, int? MaxBottles, DateOnly? LastDelivery, int? DaysSinceDelivery,
    bool OverLimit, bool Idle);

public record BottleReport(
    IReadOnlyList<BottleRow> Rows,
    int TotalOwned, int WithCustomers, int LostOrDamaged, int AtPlant,
    int OverLimitCount, int IdleCount, int IdleDays);

// ---------------- Sales (SRS RPT-04) ----------------

/// <summary>One line of the sales report. Value = bottles × the customer's price.</summary>
public record SalesLine(string Name, int Deliveries, int Bottles, decimal Value, decimal Cash, decimal Digital);

public record SalesDay(DateOnly Date, int Deliveries, int Bottles, decimal Value, decimal Cash, decimal Digital);

public record SalesReport(
    DateOnly From, DateOnly To,
    int Deliveries, int Bottles, int Empties, decimal Value, decimal Cash, decimal Digital,
    IReadOnlyList<SalesLine> ByArea, IReadOnlyList<SalesLine> ByStaff, IReadOnlyList<SalesDay> ByDay);

public interface IReportService
{
    Task<UdhaarReport> GetUdhaarAsync(int? areaId, CancellationToken ct = default);
    Task<BottleReport> GetBottlesAsync(int? areaId, CancellationToken ct = default);
    Task<SalesReport> GetSalesAsync(DateOnly from, DateOnly to, int? areaId, string? staffId, CancellationToken ct = default);

    /// <summary>Owner records how many bottles the plant owns in total (SRS BTL-02).</summary>
    Task<Result> SetTotalBottlesAsync(int total, CancellationToken ct = default);
}
