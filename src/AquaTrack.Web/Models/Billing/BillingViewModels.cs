using System.ComponentModel.DataAnnotations;
using AquaTrack.Application.Billing;
using AquaTrack.Domain.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AquaTrack.Web.Models.Billing;

public class BillsIndexViewModel
{
    public BillMonthView Month { get; set; } = null!;
    public string? Search { get; set; }
    public int? AreaId { get; set; }
    public BillStatus? Status { get; set; }
    public BillPeriod Latest { get; set; }
}

public class BillDetailsViewModel
{
    public BillDetails Bill { get; set; } = null!;
    public string WhatsAppLink { get; set; } = string.Empty;
}

public class PrintBillsViewModel
{
    public IReadOnlyList<BillDetails> Bills { get; set; } = Array.Empty<BillDetails>();
    public BillPeriod Period { get; set; }
    public string BackUrl { get; set; } = "/";
}

public class PaymentsIndexViewModel
{
    public PaymentMonthView Month { get; set; } = null!;
    public string? Search { get; set; }
    public PaymentMethod? Method { get; set; }
    public BillPeriod Current { get; set; }
    public int PendingCount { get; set; }
}

public class PaymentFormViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Please select a customer.")]
    [Display(Name = "Customer")]
    public int CustomerId { get; set; }

    [Range(1, 10000000, ErrorMessage = "Enter an amount greater than zero.")]
    [Display(Name = "Amount (PKR)")]
    public decimal Amount { get; set; }

    [Display(Name = "Payment method")]
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    [Display(Name = "Date")]
    public DateOnly Date { get; set; }

    [StringLength(50, ErrorMessage = "{0} is too long.")]
    [Display(Name = "Transaction ID (TID)")]
    public string? TransactionRef { get; set; }

    [StringLength(300, ErrorMessage = "{0} is too long.")]
    [Display(Name = "Note")]
    public string? Note { get; set; }

    [ValidateNever] public IReadOnlyList<CustomerBalanceOption> Customers { get; set; } = Array.Empty<CustomerBalanceOption>();
    [ValidateNever] public DateOnly Today { get; set; }

    public PaymentInput ToInput() => new(CustomerId, Amount, Method, Date, TransactionRef, Note);
}

public class ReceiptViewModel
{
    public ReceiptView Receipt { get; set; } = null!;
    public string WhatsAppLink { get; set; } = string.Empty;
}
