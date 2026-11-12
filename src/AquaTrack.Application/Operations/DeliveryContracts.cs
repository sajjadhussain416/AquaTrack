using AquaTrack.Application.Common;
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
    string? Note)
{
    public bool IsSaved => DeliveryId.HasValue;
}

public record RouteTotals(int Stops, int Done, int Filled, int Empties, decimal Cash);

public record RouteDay(
    DateOnly Date,
    IReadOnlyList<AreaOption> Areas,
    int? AreaId,
    IReadOnlyList<RouteStop> Stops,
    IReadOnlyList<RouteStop> Others,
    RouteTotals Totals);

/// <summary>What the delivery boy enters at one door (SRS DLV-02..05).</summary>
public record DeliveryEntry(
    int CustomerId,
    DateOnly Date,
    DeliveryStatus Status,
    int Filled,
    int Empties,
    decimal Cash,
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
