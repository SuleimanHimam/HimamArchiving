using Archiving.Domain.Enums;

namespace Archiving.Domain.Common;

/// <summary>Conversions for a retention period expressed as a value + unit. Kept in one place so
/// the derived month count and the expiry date can never drift apart.</summary>
public static class Retention
{
    /// <summary>Whole months the period is worth, for consumers that reason in months.
    /// A period in days rounds UP so a short retention never collapses to "no retention".</summary>
    public static int ToMonths(int value, RetentionUnit unit)
    {
        if (value <= 0) return 0;
        return unit switch
        {
            RetentionUnit.Years => value * 12,
            RetentionUnit.Months => value,
            RetentionUnit.Days => (int)Math.Ceiling(value / 30.0),
            _ => value,
        };
    }

    /// <summary>Expiry from the document date, using the exact unit (days stay days).</summary>
    public static DateOnly? Expiry(DateOnly? documentDate, int value, RetentionUnit unit)
    {
        if (documentDate is not { } date || value <= 0) return null;
        return unit switch
        {
            RetentionUnit.Years => date.AddYears(value),
            RetentionUnit.Months => date.AddMonths(value),
            RetentionUnit.Days => date.AddDays(value),
            _ => date.AddMonths(value),
        };
    }

    /// <summary>Best-effort split of a legacy month count back into a friendly unit,
    /// so existing rows display as "10 سنوات" rather than "120 شهرًا".</summary>
    public static (int Value, RetentionUnit Unit) FromMonths(int months) =>
        months > 0 && months % 12 == 0 ? (months / 12, RetentionUnit.Years) : (months, RetentionUnit.Months);
}
