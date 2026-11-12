using System.Globalization;
using AquaTrack.Application.Billing;
using AquaTrack.Application.Common;

namespace AquaTrack.Web;

/// <summary>Display formats shared by screens, printouts and WhatsApp messages.</summary>
public static class Fmt
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Rs 1,250 (or -Rs 300 for an advance).</summary>
    public static string Money(decimal v) =>
        (v < 0 ? "-" : "") + "Rs " + Math.Abs(v).ToString("#,0.##", Inv);

    /// <summary>Month name in the user's language: "November 2026" / "نومبر 2026".</summary>
    public static string Month(BillPeriod p) =>
        new DateTime(p.Year, p.Month, 1).ToString("MMMM yyyy", CultureInfo.CurrentUICulture);

    public static string Day(DateOnly d) => d.ToString("dd MMM yyyy", Inv);

    public static string Day(DateTime utc) => PakistanTime.ToLocal(utc).ToString("dd MMM yyyy", Inv);

    public static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", Inv);

    /// <summary>03001234567 → 923001234567 for wa.me links.</summary>
    public static string WhatsAppNumber(string phone)
    {
        var digits = PhoneNumber.Normalize(phone);
        return digits.StartsWith('0') ? "92" + digits[1..] : digits;
    }
}
