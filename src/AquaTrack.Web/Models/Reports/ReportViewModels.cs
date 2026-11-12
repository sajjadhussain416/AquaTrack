using AquaTrack.Application.Operations;
using AquaTrack.Application.Reports;

namespace AquaTrack.Web.Models.Reports;

public class UdhaarPageViewModel
{
    public required UdhaarReport Report { get; init; }
    public required IReadOnlyList<UdhaarRow> Rows { get; init; }
    public required IReadOnlyList<AreaOption> Areas { get; init; }
    public required IReadOnlyDictionary<int, string> ReminderLinks { get; init; }
    public int? AreaId { get; init; }

    /// <summary>"amount" (default) or "age".</summary>
    public string Sort { get; init; } = "amount";
}

public class BottlePageViewModel
{
    public required BottleReport Report { get; init; }
    public required IReadOnlyList<BottleRow> Rows { get; init; }
    public required IReadOnlyList<AreaOption> Areas { get; init; }
    public int? AreaId { get; init; }

    /// <summary>"all" (default), "attention", "over".</summary>
    public string Show { get; init; } = "all";
}

public class SalesPageViewModel
{
    public required SalesReport Report { get; init; }
    public required IReadOnlyList<AreaOption> Areas { get; init; }
    public required IReadOnlyList<StaffOption> Staff { get; init; }
    public int? AreaId { get; init; }
    public string? StaffId { get; init; }

    /// <summary>"today", "7d", "month", "last-month" or "custom".</summary>
    public string Range { get; init; } = "month";
    public DateOnly Today { get; init; }
}
