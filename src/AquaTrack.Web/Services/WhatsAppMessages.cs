using System.Text;
using AquaTrack.Application.Billing;
using Microsoft.Extensions.Localization;

namespace AquaTrack.Web.Services;

/// <summary>
/// Builds wa.me links with a ready-to-send message (SRS NTF-04 manual share).
/// The message is written in the language the owner is using.
/// </summary>
public class WhatsAppMessages
{
    private readonly IStringLocalizer<SharedResource> _l;

    public WhatsAppMessages(IStringLocalizer<SharedResource> localizer)
    {
        _l = localizer;
    }

    public string BillLink(BillDetails b)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"*{b.Plant.Name}*");
        sb.AppendLine($"{_l["Monthly bill"]}: {Fmt.Month(b.Period)}");
        sb.AppendLine($"{_l["Customer"]}: {b.CustomerName}");
        sb.AppendLine();
        sb.AppendLine($"{_l["Bottles"]}: {b.Bottles} × {Fmt.Money(b.Rate)} = {Fmt.Money(b.Amount)}");
        if (b.PreviousBalance != 0) sb.AppendLine($"{_l["Previous balance"]}: {Fmt.Money(b.PreviousBalance)}");
        if (b.Adjustments != 0) sb.AppendLine($"{_l["Adjustments"]}: {Fmt.Money(b.Adjustments)}");
        if (b.PaymentsInPeriod != 0) sb.AppendLine($"{_l["Payments received"]}: {Fmt.Money(-b.PaymentsInPeriod)}");
        sb.AppendLine($"*{_l["Total due"]}: {Fmt.Money(b.TotalDue)}*");
        sb.AppendLine();
        sb.Append($"{_l["Thank you!"]} {b.Plant.Phone}");

        return Link(b.WhatsApp ?? b.Phone, sb.ToString());
    }

    public string ReceiptLink(ReceiptView r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"*{r.Plant.Name}*");
        sb.AppendLine($"{_l["Payment received"]}: {Fmt.Money(r.Amount)}");
        sb.AppendLine($"{_l["Date"]}: {Fmt.Day(r.Date)}");
        sb.AppendLine($"{_l["Receipt no."]}: {r.Number}");
        sb.AppendLine(r.BalanceNow > 0
            ? $"{_l["Balance due"]}: {Fmt.Money(r.BalanceNow)}"
            : $"{_l["No balance due"]}");
        sb.AppendLine();
        sb.Append($"{_l["Thank you!"]} {r.Plant.Phone}");

        return Link(r.Phone, sb.ToString());
    }

    private static string Link(string phone, string text) =>
        $"https://wa.me/{Fmt.WhatsAppNumber(phone)}?text={Uri.EscapeDataString(text)}";
}
