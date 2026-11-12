using AquaTrack.Application.Common;
using AquaTrack.Application.Operations;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Application.Billing;

/// <summary>A calendar month in Pakistan time, with its UTC boundaries for payment timestamps.</summary>
public readonly record struct BillPeriod(int Year, int Month)
{
    private static readonly TimeSpan PakistanOffset = TimeSpan.FromHours(5);

    public DateOnly Start => new(Year, Month, 1);
    public DateOnly NextStart => Start.AddMonths(1);
    public DateTime StartUtc => Start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - PakistanOffset;
    public DateTime NextStartUtc => NextStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - PakistanOffset;
    public BillPeriod Previous => new(Start.AddMonths(-1).Year, Start.AddMonths(-1).Month);
    public BillPeriod Next => new(NextStart.Year, NextStart.Month);

    public static BillPeriod Of(DateOnly d) => new(d.Year, d.Month);

    /// <summary>Bills are made for the month that just ended.</summary>
    public static BillPeriod Default => Of(PakistanTime.Today).Previous;

    public static bool IsValid(int year, int month) => year is >= 2020 and <= 2100 && month is >= 1 and <= 12;
}

public record PlantHeader(string Name, string Phone, string City, string? Address, string? LogoUrl);

// ---------------- Bills ----------------

public record GenerateResult(int Created, int Updated, int SkippedSent, int SkippedEmpty);

public record BillListItem(
    int Id,
    int CustomerId,
    string CustomerName,
    string Phone,
    string? WhatsApp,
    string AreaName,
    int Bottles,
    decimal Amount,
    decimal PreviousBalance,
    decimal PaymentsInPeriod,
    decimal Adjustments,
    decimal TotalDue,
    BillStatus Status);

public record BillMonthTotals(int Count, int Bottles, decimal Billed, decimal TotalDue, int Sent);

public record BillMonthView(
    BillPeriod Period,
    IReadOnlyList<BillListItem> Bills,
    BillMonthTotals Totals,
    IReadOnlyList<AreaOption> Areas,
    int BillingDay);

public record BillLineItem(DateOnly Date, int Bottles, decimal Amount);
public record BillAdjustmentItem(int Id, decimal Amount, string Reason);
public record BillPaymentItem(DateTime Date, decimal Amount, PaymentMethod Method);

public record BillDetails(
    int Id,
    BillPeriod Period,
    BillStatus Status,
    DateTime? SentAt,
    DateTime GeneratedAt,
    int CustomerId,
    string CustomerName,
    string Phone,
    string? WhatsApp,
    string Address,
    string AreaName,
    int Bottles,
    decimal Rate,
    decimal Amount,
    decimal PreviousBalance,
    decimal Adjustments,
    decimal PaymentsInPeriod,
    decimal TotalDue,
    IReadOnlyList<BillLineItem> Lines,
    IReadOnlyList<BillAdjustmentItem> AdjustmentItems,
    IReadOnlyList<BillPaymentItem> Payments,
    PlantHeader Plant)
{
    public string Number => $"B{Period.Year}{Period.Month:00}-{Id:00000}";
}

public record CustomerBillSummary(int Id, BillPeriod Period, decimal TotalDue, BillStatus Status);
public record CustomerPaymentSummary(int Id, DateTime Date, decimal Amount, PaymentMethod Method, bool FromDelivery);

/// <summary>Money position of one customer: billed minus paid.</summary>
public record CustomerAccount(
    decimal Balance,
    decimal UnbilledBottlesValue,
    IReadOnlyList<CustomerBillSummary> RecentBills,
    IReadOnlyList<CustomerPaymentSummary> RecentPayments);

public interface IBillingService
{
    /// <summary>Creates or refreshes draft bills for every customer with activity (SRS BILL-01..04). Sent bills are never changed.</summary>
    Task<GenerateResult> GenerateAsync(BillPeriod period, CancellationToken ct = default);

    Task<Result> RegenerateAsync(int billId, CancellationToken ct = default);

    Task<BillMonthView> GetMonthAsync(BillPeriod period, string? search, int? areaId, BillStatus? status, CancellationToken ct = default);

    Task<BillDetails?> GetAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<BillDetails>> GetForPrintAsync(BillPeriod period, int? areaId, CancellationToken ct = default);

    Task<Result> AddAdjustmentAsync(int billId, decimal amount, string reason, CancellationToken ct = default);

    Task<Result> RemoveAdjustmentAsync(int billId, int adjustmentId, CancellationToken ct = default);

    Task<Result> SetSentAsync(int billId, bool sent, CancellationToken ct = default);

    Task<int> MarkAllSentAsync(BillPeriod period, CancellationToken ct = default);

    Task<CustomerAccount> GetCustomerAccountAsync(int customerId, CancellationToken ct = default);

    /// <summary>Sum of all positive customer balances (udhaar).</summary>
    Task<decimal> GetTotalPendingAsync(CancellationToken ct = default);
}

// ---------------- Payments ----------------

public record PaymentListItem(
    int Id,
    DateTime Date,
    int CustomerId,
    string CustomerName,
    string AreaName,
    decimal Amount,
    PaymentMethod Method,
    string ReceivedBy,
    bool FromDelivery,
    string? Note);

public record PaymentMonthView(
    BillPeriod Period,
    IReadOnlyList<PaymentListItem> Items,
    decimal Total,
    IReadOnlyList<(PaymentMethod Method, decimal Amount)> ByMethod);

public record CustomerBalanceOption(int Id, string Name, string Phone, string AreaName, decimal Balance);

public record PaymentInput(int CustomerId, decimal Amount, PaymentMethod Method, DateOnly Date, string? Note);

public record ReceiptView(
    int Id,
    DateTime Date,
    int CustomerId,
    string CustomerName,
    string Phone,
    string AreaName,
    decimal Amount,
    PaymentMethod Method,
    string ReceivedBy,
    string? Note,
    decimal BalanceNow,
    PlantHeader Plant)
{
    public string Number => $"R{Date:yyyyMM}-{Id:00000}";
}

public interface IPaymentService
{
    Task<PaymentMonthView> GetMonthAsync(BillPeriod period, string? search, PaymentMethod? method, CancellationToken ct = default);

    Task<IReadOnlyList<CustomerBalanceOption>> GetCustomerOptionsAsync(CancellationToken ct = default);

    Task<Result<int>> RecordAsync(PaymentInput input, CancellationToken ct = default);

    Task<ReceiptView?> GetReceiptAsync(int id, CancellationToken ct = default);

    /// <summary>Only payments taken at the plant; cash collected at a delivery is changed from the delivery.</summary>
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}
