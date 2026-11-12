namespace AquaTrack.Domain.Enums;

public enum PlantStatus
{
    Trial = 0,
    Active = 1,
    Suspended = 2
}

public enum CustomerType
{
    Home = 0,
    Office = 1,
    Shop = 2
}

public enum ScheduleType
{
    Daily = 0,
    AlternateDays = 1,
    FixedWeekdays = 2,
    OnCall = 3
}

/// <summary>Bit flags so one customer can have several fixed weekdays (SRS CUST-03).</summary>
[Flags]
public enum WeekDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64
}

public enum DeliveryStatus
{
    Done = 0,
    NotHome = 1,
    Refused = 2,
    Skipped = 3
}

public enum BottleMovementType
{
    Issue = 0,     // bottles given at account opening
    Deliver = 1,   // filled bottle delivered
    Collect = 2,   // empty bottle taken back
    Lost = 3,
    Damaged = 4
}

public enum DepositType
{
    Taken = 0,
    Refunded = 1
}

public enum PaymentMethod
{
    Cash = 0,
    JazzCash = 1,
    Easypaisa = 2,
    BankTransfer = 3
}

public enum BillStatus
{
    Draft = 0,
    Sent = 1
}

public enum ExpenseCategory
{
    Electricity = 0,
    Filters = 1,
    Salary = 2,
    Fuel = 3,
    NewBottles = 4,
    Maintenance = 5,
    Other = 6
}

public enum AuditAction
{
    Created = 0,
    Updated = 1,
    Deleted = 2
}

/// <summary>Digital payments taken at the door wait for the owner to check them (Day 6).</summary>
public enum PaymentVerification
{
    Verified = 0,
    Pending = 1,
    Rejected = 2
}
