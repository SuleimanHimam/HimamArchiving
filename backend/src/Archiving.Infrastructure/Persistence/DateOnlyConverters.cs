using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Archiving.Infrastructure.Persistence;

/// <summary>Maps <see cref="DateOnly"/> to <see cref="DateTime"/> for storage.
/// Oracle's MySQL EF Core provider mis-reads native DateOnly columns (DateTime→DateOnly cast error),
/// so we store as datetime and convert in managed code.</summary>
public sealed class DateOnlyConverter : ValueConverter<DateOnly, DateTime>
{
    public DateOnlyConverter()
        : base(d => d.ToDateTime(TimeOnly.MinValue), dt => DateOnly.FromDateTime(dt)) { }
}

public sealed class NullableDateOnlyConverter : ValueConverter<DateOnly?, DateTime?>
{
    public NullableDateOnlyConverter()
        : base(
            d => d.HasValue ? d.Value.ToDateTime(TimeOnly.MinValue) : null,
            dt => dt.HasValue ? DateOnly.FromDateTime(dt.Value) : null) { }
}
