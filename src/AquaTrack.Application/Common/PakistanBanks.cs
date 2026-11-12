namespace AquaTrack.Application.Common;

/// <summary>A bank or digital bank a plant can receive money in. IbanCode = characters 5-8 of a Pakistani IBAN.</summary>
public record PakistanBank(string Name, string? IbanCode, bool Digital = false);

/// <summary>
/// Banks shown in the plant settings dropdown. Anything missing is entered with "Other".
/// IBAN bank codes match the first four letters of each bank's SWIFT code.
/// </summary>
public static class PakistanBanks
{
    public const string Other = "other";

    public static readonly IReadOnlyList<PakistanBank> All = new List<PakistanBank>
    {
        new("Al Baraka Bank", "AIIN"),
        new("Allied Bank", "ABPA"),
        new("Askari Bank", "ASCM"),
        new("Bank Alfalah", "ALFH"),
        new("Bank AL Habib", "BAHL"),
        new("Bank Makramah (Summit)", "SUMB"),
        new("Bank of Khyber", "KHYB"),
        new("Bank of Punjab", "BPUN"),
        new("BankIslami", "BKIP"),
        new("Citibank", "CITI"),
        new("Dubai Islamic Bank", "DUIB"),
        new("Faysal Bank", "FAYS"),
        new("Habib Bank (HBL)", "HABB"),
        new("Habib Metro Bank", "MPBL"),
        new("JS Bank", "JSBL"),
        new("MCB Bank", "MUCB"),
        new("MCB Islamic Bank", null),
        new("Meezan Bank", "MEZN"),
        new("National Bank of Pakistan (NBP)", "NBPA"),
        new("Samba Bank", "SAMB"),
        new("Silkbank", "SAUD"),
        new("Sindh Bank", "SIND"),
        new("Soneri Bank", "SONE"),
        new("Standard Chartered", "SCBL"),
        new("United Bank (UBL)", "UNIL"),

        new("Easypaisa Bank (Telenor Microfinance)", "TMFB", Digital: true),
        new("JazzCash (Mobilink Bank)", "JCMA", Digital: true),
        new("NayaPay", "NAYA", Digital: true),
        new("SadaPay", "SADA", Digital: true)
    };

    public static PakistanBank? Find(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null
        : All.FirstOrDefault(b => string.Equals(b.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The bank an IBAN belongs to, when its bank code is in the list.</summary>
    public static PakistanBank? FromIban(string? iban)
    {
        var n = BankAccount.Normalize(iban);
        if (n.Length < 8 || !n.StartsWith("PK")) return null;
        var code = n.Substring(4, 4);
        return All.FirstOrDefault(b => b.IbanCode == code);
    }
}

/// <summary>Pakistani bank account numbers and IBANs (PK + 2 check digits + 4-letter bank code + 16 digits = 24 characters).</summary>
public static class BankAccount
{
    public static string Normalize(string? input) =>
        string.IsNullOrWhiteSpace(input)
            ? string.Empty
            : new string(input.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    public static bool LooksLikeIban(string normalized) => normalized.StartsWith("PK");

    /// <summary>Full IBAN check, including the ISO 13616 mod-97 checksum, so a single wrong digit is caught.</summary>
    public static bool IsValidIban(string? input)
    {
        var n = Normalize(input);
        if (n.Length != 24 || !n.StartsWith("PK")) return false;
        if (!char.IsDigit(n[2]) || !char.IsDigit(n[3])) return false;
        if (!n.Substring(4, 4).All(char.IsAsciiLetterUpper)) return false;
        if (!n[8..].All(char.IsAsciiLetterOrDigit)) return false;

        var rearranged = n[4..] + n[..4];
        var remainder = 0;
        foreach (var c in rearranged)
        {
            remainder = char.IsDigit(c)
                ? (remainder * 10 + (c - '0')) % 97
                : (remainder * 100 + (c - 'A' + 10)) % 97;
        }
        return remainder == 1;
    }

    /// <summary>A plain (non-IBAN) account number: digits only.</summary>
    public static bool IsValidAccountNumber(string? input)
    {
        var n = Normalize(input);
        return n.Length is >= 6 and <= 20 && n.All(char.IsDigit);
    }

    /// <summary>IBAN in groups of four for reading aloud / printing: PK56 MEZN 0001 2345 6789 0123.</summary>
    public static string Pretty(string? value)
    {
        var n = Normalize(value);
        if (!LooksLikeIban(n)) return value?.Trim() ?? string.Empty;
        return string.Join(' ', Enumerable.Range(0, (n.Length + 3) / 4).Select(i => n.Substring(i * 4, Math.Min(4, n.Length - i * 4))));
    }
}
