namespace AquaTrack.Application.Common;

/// <summary>
/// Pakistani mobile numbers are the login user name. Everything is stored as 03XXXXXXXXX.
/// Accepts "0300 1234567", "0300-1234567", "+923001234567", "923001234567".
/// </summary>
public static class PhoneNumber
{
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var digits = new string(input.Where(char.IsDigit).ToArray());

        if (digits.StartsWith("92") && digits.Length == 12)
        {
            digits = "0" + digits[2..];
        }

        return digits;
    }

    public static bool IsValidMobile(string? input)
    {
        var n = Normalize(input);
        return n.Length == 11 && n.StartsWith("03");
    }
}
