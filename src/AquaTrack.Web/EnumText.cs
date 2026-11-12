using AquaTrack.Domain.Enums;

namespace AquaTrack.Web;

/// <summary>English labels for enums. They are also the keys for the Urdu translations.</summary>
public static class EnumText
{
    public static string Of(CustomerType t) => t switch
    {
        CustomerType.Office => "Office",
        CustomerType.Shop => "Shop",
        _ => "Home"
    };

    public static string Of(ScheduleType s) => s switch
    {
        ScheduleType.AlternateDays => "Alternate days",
        ScheduleType.FixedWeekdays => "Fixed days",
        ScheduleType.OnCall => "On call",
        _ => "Daily"
    };

    public static string Of(BottleMovementType m) => m switch
    {
        BottleMovementType.Issue => "Given at start",
        BottleMovementType.Deliver => "Delivered",
        BottleMovementType.Collect => "Empty collected",
        BottleMovementType.Lost => "Lost",
        _ => "Damaged"
    };

    public static string Of(DeliveryStatus s) => s switch
    {
        DeliveryStatus.NotHome => "Not home",
        DeliveryStatus.Refused => "Refused",
        DeliveryStatus.Skipped => "Skipped",
        _ => "Delivered"
    };

    public static string Css(DeliveryStatus s) => s switch
    {
        DeliveryStatus.Done => "active",
        DeliveryStatus.Skipped => "neutral",
        _ => "trial"
    };

    public static readonly (WeekDays Day, string Label)[] Week =
    {
        (WeekDays.Monday, "Mon"), (WeekDays.Tuesday, "Tue"), (WeekDays.Wednesday, "Wed"),
        (WeekDays.Thursday, "Thu"), (WeekDays.Friday, "Fri"), (WeekDays.Saturday, "Sat"), (WeekDays.Sunday, "Sun")
    };
}
