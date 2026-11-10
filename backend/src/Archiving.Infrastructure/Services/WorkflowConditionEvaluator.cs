using System.Text.Json;

namespace Archiving.Infrastructure.Services;

/// <summary>Evaluates a stage's <c>ConditionExpression</c> (JSON: {"field","operator","value"}) against a
/// context of document/workflow values. Supports: equals, notEquals, greaterThan, lessThan, contains, isEmpty.
/// Unknown/empty expressions evaluate <c>true</c> (fail-open, so a misconfigured condition never blocks a flow).</summary>
public static class WorkflowConditionEvaluator
{
    public static bool Evaluate(string? expression, IReadOnlyDictionary<string, string?> context)
    {
        if (string.IsNullOrWhiteSpace(expression)) return true;
        try
        {
            using var doc = JsonDocument.Parse(expression);
            var root = doc.RootElement;
            var field = root.TryGetProperty("field", out var f) ? f.GetString() : null;
            var op = root.TryGetProperty("operator", out var o) ? o.GetString() : null;
            var value = root.TryGetProperty("value", out var v)
                ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText())
                : null;
            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(op)) return true;

            context.TryGetValue(field, out var actual);

            return op.ToLowerInvariant() switch
            {
                "equals"      => string.Equals(actual, value, StringComparison.OrdinalIgnoreCase),
                "notequals"   => !string.Equals(actual, value, StringComparison.OrdinalIgnoreCase),
                "contains"    => actual is not null && value is not null && actual.Contains(value, StringComparison.OrdinalIgnoreCase),
                "isempty"     => string.IsNullOrWhiteSpace(actual),
                "greaterthan" => TryNum(actual, out var a) && TryNum(value, out var b) && a > b,
                "lessthan"    => TryNum(actual, out var a2) && TryNum(value, out var b2) && a2 < b2,
                _ => true,
            };
        }
        catch { return true; }   // malformed condition never blocks the flow
    }

    private static bool TryNum(string? s, out decimal d) =>
        decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out d);
}
