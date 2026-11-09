using AquaTrack.Domain.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Domain.Entities;

/// <summary>Delivery area / route (SRS AREA-01/02).</summary>
public class Area : TenantEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Identity user id of the assigned delivery boy.</summary>
    public string? DeliveryBoyId { get; set; }

    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
}

/// <summary>Household, office or shop that receives bottles (SRS CUST-01..07).</summary>
public class Customer : TenantEntity
{
    public int AreaId { get; set; }
    public Area? Area { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? WhatsApp { get; set; }
    public string Address { get; set; } = string.Empty;
    public CustomerType Type { get; set; } = CustomerType.Home;

    /// <summary>Price per bottle for this customer in PKR.</summary>
    public decimal Rate { get; set; }

    public ScheduleType ScheduleType { get; set; } = ScheduleType.Daily;
    public WeekDays ScheduleDays { get; set; } = WeekDays.None;

    /// <summary>Order inside the area route (SRS AREA-03).</summary>
    public int RouteOrder { get; set; }

    /// <summary>Alert when bottles in hand exceed this (SRS BTL-04). Null = no limit.</summary>
    public int? MaxBottles { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
    public ICollection<BottleMovement> BottleMovements { get; set; } = new List<BottleMovement>();
    public ICollection<Deposit> Deposits { get; set; } = new List<Deposit>();
    public ICollection<Bill> Bills { get; set; } = new List<Bill>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

/// <summary>Plant-level bottle inventory, one row per plant (SRS BTL-02).</summary>
public class BottleStock : TenantEntity
{
    public int TotalOwned { get; set; }
    public int AtPlantFilled { get; set; }
    public int AtPlantEmpty { get; set; }
}

/// <summary>
/// Every change in bottles a customer holds.
/// Bottles in hand = Issue + Deliver - Collect - Lost - Damaged (SRS BTL-01).
/// </summary>
public class BottleMovement : TenantEntity
{
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public BottleMovementType Type { get; set; }
    public int Quantity { get; set; }

    /// <summary>Set when the movement came from a delivery entry.</summary>
    public int? DeliveryId { get; set; }
    public Delivery? Delivery { get; set; }

    public DateTime Date { get; set; }
    public string? Note { get; set; }
}

/// <summary>Security deposit taken or refunded (SRS CUST-04, PAY-04).</summary>
public class Deposit : TenantEntity
{
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public decimal Amount { get; set; }
    public int Bottles { get; set; }
    public DepositType Type { get; set; }
    public DateTime Date { get; set; }
}

/// <summary>One visit by a delivery boy to a customer (SRS DLV-01..07).</summary>
public class Delivery : TenantEntity
{
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string DeliveryBoyId { get; set; } = string.Empty;

    /// <summary>Business date of the delivery (date only, Pakistan time).</summary>
    public DateOnly Date { get; set; }

    public int FilledGiven { get; set; }
    public int EmptyCollected { get; set; }
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Done;

    /// <summary>Cash taken at the door; also creates a Payment row.</summary>
    public decimal CashCollected { get; set; }

    public string? Note { get; set; }
}

/// <summary>Monthly bill for one customer (SRS BILL-01..07).</summary>
public class Bill : TenantEntity
{
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int Month { get; set; }
    public int Year { get; set; }

    public int BottleCount { get; set; }
    public decimal Rate { get; set; }

    /// <summary>BottleCount x Rate.</summary>
    public decimal Amount { get; set; }

    public decimal PreviousBalance { get; set; }
    public decimal Adjustments { get; set; }
    public decimal PaymentsInPeriod { get; set; }

    /// <summary>Amount + PreviousBalance + Adjustments - PaymentsInPeriod.</summary>
    public decimal TotalDue { get; set; }

    public BillStatus Status { get; set; } = BillStatus.Draft;
    public DateTime? SentAt { get; set; }

    public ICollection<BillLine> Lines { get; set; } = new List<BillLine>();
    public ICollection<BillAdjustment> AdjustmentItems { get; set; } = new List<BillAdjustment>();
}

/// <summary>One delivery date on a bill, kept as dispute proof (SRS BILL-03).</summary>
public class BillLine : TenantEntity
{
    public int BillId { get; set; }
    public Bill? Bill { get; set; }

    public DateOnly DeliveryDate { get; set; }
    public int Bottles { get; set; }
}

/// <summary>Manual discount (negative) or extra charge (positive) on a bill (SRS BILL-07).</summary>
public class BillAdjustment : TenantEntity
{
    public int BillId { get; set; }
    public Bill? Bill { get; set; }

    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Money received from a customer (SRS PAY-01..03).</summary>
public class Payment : TenantEntity
{
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    /// <summary>Identity user id of who received the money.</summary>
    public string ReceivedById { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    /// <summary>Set when cash was collected during a delivery.</summary>
    public int? DeliveryId { get; set; }
    public Delivery? Delivery { get; set; }

    public string? Note { get; set; }
}

/// <summary>Cash a delivery boy hands to the owner at day end (SRS STAFF-02).</summary>
public class CashHandover : TenantEntity
{
    public string DeliveryBoyId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}

/// <summary>Plant running cost (SRS EXP-01).</summary>
public class Expense : TenantEntity
{
    public ExpenseCategory Category { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string? Note { get; set; }
}

/// <summary>Who changed what and when (SRS SYS-01). PlantId is null for Super Admin actions.</summary>
public class AuditLog
{
    public long Id { get; set; }
    public int? PlantId { get; set; }
    public string? UserId { get; set; }
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime At { get; set; }
}
