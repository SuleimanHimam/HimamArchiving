using System.Text.Json;
using System.Text.Json.Serialization;

namespace Archiving.Api.Common;

/// <summary>
/// Serializes every <see cref="DateTime"/> as UTC ISO-8601 with a trailing 'Z'.
/// All timestamps in this system are stored as <c>DateTime.UtcNow</c>, but values read back
/// from MySQL come back as <see cref="DateTimeKind.Unspecified"/> and would otherwise be emitted
/// without a timezone — making browsers interpret them as local time and display them with the
/// wrong offset. Treating Unspecified as UTC and emitting 'Z' lets the browser convert to the
/// viewer's local time correctly.
/// </summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dt = reader.GetDateTime();
        return dt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            : dt.ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
        writer.WriteStringValue(utc.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"));
    }
}
