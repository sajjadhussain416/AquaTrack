using AquaTrack.Application.Common;
using AquaTrack.Application.Settings;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Application.Operations;

/// <summary>One customer on a delivery route, with today's entry if already saved.</summary>
public record RouteStop(
    int CustomerId,
    string Name,
    string Phone,
    string Address,
    string AreaName,
    decimal Rate,
    int BottlesInHand,
    bool Scheduled,
    int? DeliveryId,
    DeliveryStatus? Status,
    int Filled,
    int Empties,
    decimal Cash,
    PaymentMethod Method,
    string? TransactionRef,
    string? Note)
{
    public bool IsSaved => DeliveryId.HasValue;
}

/// <summary>Cash = notes in the delivery boy's pocket. Digital = Easypaisa/JazzCash/bank sent to the plant.</summary>
public record RouteTotals(int Stops, int Done, int Filled, int Empties, decimal Cash, decimal Digital);

public record RouteDay(
    DateOnly Date,
    IReadOnlyList<AreaOption> Areas,
    int? AreaId,
    IReadOnlyList<RouteStop> Stops,
    IReadOnlyList<RouteStop> Others,
    RouteTotals Totals,
    PaymentAccounts Accounts);

/// <summary>What the delivery boy enters at one door (SRS DLV-02..05).</summary>
public record DeliveryEntry(
    int CustomerId,
    DateOnly Date,
    DeliveryStatus Status,
    int Filled,
    int Empties,
    decimal Cash,
    PaymentMethod Method,
    string? TransactionRef,
    string? Note);

public record DeliveryRow(
    int Id,
    DateOnly Date,
    int CustomerId,
    string CustomerName,
    string AreaName,
    string DeliveryBoyName,
    DeliveryStatus Status,
    int Filled,
    int Empties,
    decimal Cash,
    PaymentMethod Method,
    string? TransactionRef,
    string? Note,
    DateTime SavedAt);

public record DeliveryDayReport(
    DateOnly Date,
    IReadOnlyList<DeliveryRow> Rows,
    RouteTotals Totals,
    IReadOnlyList<AreaOption> Areas,
    IReadOnlyList<StaffOption> Staff);

public interface IDeliveryService
{
    /// <summary>Delivery boy: his own areas. Owner/manager: one area or all.</summary>
    Task<RouteDay> GetRouteAsync(DateOnly date, int? areaId, CancellationToken ct = default);

    /// <summary>Creates or updates the single delivery of a customer for that day.</summary>
    Task<Result<RouteStop>> SaveAsync(DeliveryEntry entry, CancellationToken ct = default);

    Task<DeliveryDayReport> GetDayAsync(DateOnly date, int? areaId, string? deliveryBoyId, CancellationToken ct = default);

    Task<DeliveryRow?> GetAsync(int id, CancellationToken ct = default);

    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

// ===================== Cash sheet (SRS STAFF-02) =====================

/// <summary>
/// One delivery staff member on one day. Only cash counts: digital money goes straight to the plant's account.
/// Closing = Opening + CashCollected − HandedOver.
/// </summary>
public record CashSheetRow(
    string StaffId,
    string StaffName,
    decimal Opening,
    decimal CashCollected,
    decimal HandedOver,
    decimal Closing,
    decimal DigitalCollected,
    int Deliveries);

public record CashSheet(DateOnly Date, IReadOnlyList<CashSheetRow> Rows)
{
    public decimal TotalCollected => Rows.Sum(r => r.CashCollected);
    public decimal TotalHanded => Rows.Sum(r => r.HandedOver);
    public decimal TotalOutstanding => Rows.Sum(r => r.Closing);
    public decimal TotalDigital => Rows.Sum(r => r.DigitalCollected);
}

public record HandoverItem(int Id, DateOnly Date, string StaffName, decimal Amount, string? Note, DateTime SavedAt);

public interface ICashService
{
    Task<CashSheet> GetDayAsync(DateOnly date, CancellationToken ct = default);

    Task<IReadOnlyList<HandoverItem>> GetHandoversAsync(DateOnly date, CancellationToken ct = default);

    Task<Result> RecordHandoverAsync(string staffId, DateOnly date, decimal amount, string? note, CancellationToken ct = default);

    Task<Result> DeleteHandoverAsync(int id, CancellationToken ct = default);

    /// <summary>For the delivery boy's own route screen: cash he should be carrying right now.</summary>
    Task<decimal> GetMyCashInHandAsync(CancellationToken ct = default);
}
