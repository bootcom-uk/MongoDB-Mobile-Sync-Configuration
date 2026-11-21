#nullable enable
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MongoDB.Sync.Filtering;

/// <summary>Validates the bounded, typed collection visibility expression shared by server and editor.</summary>
public static class SyncFilterRule
{
    /// <summary>Validates a rule; blank rules retain the legacy unrestricted behaviour.</summary>
    public static void Validate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length > 16384) throw new FormatException("Filters are limited to 16384 characters.");
        try
        {
            using var json = JsonDocument.Parse(text);
            ValidateNode(json.RootElement, 0);
        }
        catch (JsonException ex) { throw new FormatException("Enter a valid JSON filter.", ex); }
    }

    /// <summary>Checks groups and typed field comparisons without accepting arbitrary MongoDB operators.</summary>
    private static void ValidateNode(JsonElement node, int depth)
    {
        if (depth > 12 || node.ValueKind != JsonValueKind.Object || node.EnumerateObject().Count() != 1)
            throw new FormatException("Each filter must contain one operator, nested at most 12 levels.");
        var op = node.EnumerateObject().Single();
        if (op.Value.ValueKind != JsonValueKind.Array) throw new FormatException("Operator arguments must be an array.");
        var args = op.Value.EnumerateArray().ToArray();
        if (op.Name is "$and" or "$or")
        {
            if (args.Length is < 1 or > 32) throw new FormatException("Groups require 1–32 conditions.");
            foreach (var arg in args) ValidateNode(arg, depth + 1);
            return;
        }
        if (op.Name is not ("$eq" or "$ne" or "$lt" or "$lte" or "$gt" or "$gte") || args.Length != 2)
            throw new FormatException("Use a supported comparison with a field and a typed value.");
        if (args[0].ValueKind != JsonValueKind.String ||
            !Regex.IsMatch(args[0].GetString()!, @"^\$[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$") ||
            args[0].GetString()!.StartsWith("$__meta", StringComparison.Ordinal))
            throw new FormatException("Choose a source field path, prefixed with $.");
        var value = args[1];
        if (value.ValueKind == JsonValueKind.String)
        {
            if (value.GetString()!.StartsWith('$') && value.GetString() != "$$USER_ID")
                throw new FormatException("The only dynamic value is $$USER_ID (authenticated user ObjectId).");
            return;
        }
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) return;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _)) return;
        if (value.ValueKind == JsonValueKind.Object && value.EnumerateObject().Count() == 1)
        {
            var literal = value.EnumerateObject().Single();
            if (literal.Value.ValueKind == JsonValueKind.String &&
                ((literal.Name == "$oid" && Regex.IsMatch(literal.Value.GetString()!, "^[a-fA-F0-9]{24}$")) ||
                 (literal.Name == "$date" && DateTimeOffset.TryParse(literal.Value.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                     System.Globalization.DateTimeStyles.RoundtripKind, out _) && literal.Value.GetString()!.EndsWith('Z')))) return;
        }
        throw new FormatException("Use a string, number, Boolean, ObjectId, UTC date, or current user.");
    }
}
