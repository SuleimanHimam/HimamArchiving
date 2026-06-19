using System.Text.RegularExpressions;

namespace Archiving.Infrastructure.Security;

/// <summary>Enforces a minimum password strength: 8+ chars, upper, lower, digit and a symbol.</summary>
public static partial class PasswordPolicy
{
    public const int MinLength = 8;

    public static bool IsStrong(string? password, out string? error)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
        {
            error = $"كلمة المرور يجب ألا تقل عن {MinLength} أحرف";
            return false;
        }
        if (!UpperRegex().IsMatch(password)) { error = "كلمة المرور يجب أن تحتوي على حرف كبير (A-Z)"; return false; }
        if (!LowerRegex().IsMatch(password)) { error = "كلمة المرور يجب أن تحتوي على حرف صغير (a-z)"; return false; }
        if (!DigitRegex().IsMatch(password)) { error = "كلمة المرور يجب أن تحتوي على رقم (0-9)"; return false; }
        if (!SymbolRegex().IsMatch(password)) { error = "كلمة المرور يجب أن تحتوي على رمز خاص (!@#$...)"; return false; }

        error = null;
        return true;
    }

    [GeneratedRegex("[A-Z]")] private static partial Regex UpperRegex();
    [GeneratedRegex("[a-z]")] private static partial Regex LowerRegex();
    [GeneratedRegex("[0-9]")] private static partial Regex DigitRegex();
    [GeneratedRegex(@"[^A-Za-z0-9]")] private static partial Regex SymbolRegex();
}
