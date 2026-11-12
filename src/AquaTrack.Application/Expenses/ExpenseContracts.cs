using AquaTrack.Application.Billing;
using AquaTrack.Application.Common;
using AquaTrack.Domain.Enums;

namespace AquaTrack.Application.Expenses;

public record ExpenseRow(int Id, DateOnly Date, ExpenseCategory Category, decimal Amount, string? Note);

public record CategoryTotal(ExpenseCategory Category, decimal Amount);

public record ExpenseMonth(BillPeriod Period, IReadOnlyList<ExpenseRow> Rows, decimal Total, IReadOnlyList<CategoryTotal> ByCategory);

public class ExpenseInput
{
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Electricity;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// One month of the profit report (SRS EXP-02).
/// Sales = bottles delivered × customer price; Profit = Sales − Expenses.
/// </summary>
public record ProfitMonth(BillPeriod Period, int Bottles, decimal Sales, decimal Collected, decimal Expenses)
{
    public decimal Profit => Sales - Expenses;
}

public record ProfitReport(IReadOnlyList<ProfitMonth> Months, IReadOnlyList<CategoryTotal> ExpensesByCategory)
{
    public decimal TotalSales => Months.Sum(m => m.Sales);
    public decimal TotalExpenses => Months.Sum(m => m.Expenses);
    public decimal TotalProfit => TotalSales - TotalExpenses;
}

/// <summary>Plant expenses and monthly profit (SRS EXP-01, EXP-02).</summary>
public interface IExpenseService
{
    Task<ExpenseMonth> GetMonthAsync(BillPeriod period, ExpenseCategory? category, CancellationToken ct = default);
    Task<Result> AddAsync(ExpenseInput input, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>The last <paramref name="months"/> months, newest first, including the current month.</summary>
    Task<ProfitReport> GetProfitAsync(int months, CancellationToken ct = default);
}
