namespace DineMaster.Application.Common;

/// <summary>
/// Telefon raqamini yagona ko'rinishga keltiradi: "+998 90 123-45-67" → "+998901234567".
/// OTP yuborish, tasdiqlash va bronda bir xil qiymat solishtirilishi uchun kerak.
/// </summary>
public static class PhoneNumber
{
    public static string Normalize(string? raw)
    {
        var digits = new string((raw ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == 9) digits = "998" + digits; // mahalliy format: 90 123 45 67
        return digits.Length == 0 ? string.Empty : "+" + digits;
    }

    /// <summary>O'zbekiston raqami: +998 va 9 xonali abonent raqami.</summary>
    public static bool IsValid(string normalized)
        => normalized.Length == 13 && normalized.StartsWith("+998");
}
