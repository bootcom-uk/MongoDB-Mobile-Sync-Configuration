using System.Globalization;
using System.Text.Json;

namespace MongoDB.Sync.Expiry;

/// <summary>A bounded, database-independent interpreter for the sync expiry expression contract.</summary>
public sealed class ExpiryRule
{
    private readonly JsonElement expression;
    private readonly HashSet<string> fields = new(StringComparer.Ordinal);

    /// <summary>Gets the document paths required to evaluate this rule.</summary>
    public IReadOnlyCollection<string> Fields => fields;

    /// <summary>Creates a validated rule from an owned JSON element.</summary>
    private ExpiryRule(JsonElement expression) { this.expression = expression; Validate(expression, true, 0); }

    /// <summary>Parses a Boolean expression; blank text disables expiry without an error.</summary>
    public static bool TryParse(string? text, out ExpiryRule? rule, out string? error)
    {
        rule = null; error = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        try
        {
            if (text.Length > 16384) throw new FormatException("Expiry expressions may contain at most 16384 characters.");
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 32 });
            rule = new ExpiryRule(document.RootElement.Clone());
            return true;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or OverflowException or ArgumentException)
        { error = ex.Message; return false; }
    }

    /// <summary>Evaluates with one captured UTC time; failures retain the document.</summary>
    public bool TryEvaluate(Func<string, DateTimeOffset?> dateField, DateTimeOffset now, out bool expired, out string? error)
    {
        expired = false; error = null;
        try { expired = Boolean(expression, dateField, now.ToUniversalTime()); return true; }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or InvalidOperationException)
        { error = ex.Message; return false; }
    }

    /// <summary>Reads an ISO UTC/offset date, including MongoDB Extended JSON dates.</summary>
    public static DateTimeOffset? ReadDate(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("$date", out var date))
        {
            if (date.ValueKind == JsonValueKind.Object && date.TryGetProperty("$numberLong", out var number) &&
                long.TryParse(number.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds))
                return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
            value = date;
        }
        if (value.ValueKind != JsonValueKind.String) return null;
        var text = value.GetString()!;
        if (!(text.EndsWith('Z') || (text.Length >= 6 && text[^3] == ':' && (text[^6] == '+' || text[^6] == '-')))) return null;
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed.ToUniversalTime() : null;
    }

    /// <summary>Checks expression shape and types without executing document-dependent values.</summary>
    private void Validate(JsonElement node, bool boolean, int depth)
    {
        if (depth > 12) throw new FormatException("Expiry expressions may be nested at most 12 levels.");
        if (!boolean && node.ValueKind == JsonValueKind.String)
        {
            var name = node.GetString()!;
            if (name == "$$NOW") return;
            if (!name.StartsWith('$') || name.StartsWith("$$") || name.Length < 2 || name[1..].Split('.').Any(string.IsNullOrWhiteSpace))
                throw new FormatException("Use a $field path, $$NOW, or a {$date: ...} UTC date.");
            fields.Add(name[1..]); return;
        }
        var property = Operator(node);
        if (boolean)
        {
            if (property.Name is "$and" or "$or")
            {
                var children = Array(property.Value, 1, 32);
                foreach (var child in children) Validate(child, true, depth + 1);
            }
            else if (property.Name == "$not") Validate(Array(property.Value, 1, 1)[0], true, depth + 1);
            else if (property.Name is "$lt" or "$lte" or "$gt" or "$gte" or "$eq" or "$ne")
            {
                foreach (var child in Array(property.Value, 2, 2)) Validate(child, false, depth + 1);
            }
            else throw new FormatException($"Unsupported Boolean operator {property.Name}.");
        }
        else if (property.Name == "$date")
        {
            if (ReadDate(node) is null) throw new FormatException("Explicit dates must contain an ISO date with Z or an offset.");
        }
        else if (property.Name is "$dateAdd" or "$dateSubtract")
        {
            var args = property.Value;
            if (args.ValueKind != JsonValueKind.Object || args.EnumerateObject().Count() != 3 ||
                !args.TryGetProperty("startDate", out var start) || !args.TryGetProperty("unit", out var unit) ||
                !args.TryGetProperty("amount", out var amount) || !amount.TryGetInt32(out var count) || Math.Abs((long)count) > 100000)
                throw new FormatException("Date offsets require startDate, unit and an integer amount between -100000 and 100000.");
            if (unit.GetString() is not ("millisecond" or "second" or "minute" or "hour" or "day" or "week" or "month" or "quarter" or "year"))
                throw new FormatException("Unsupported date unit. Calendar calculations use UTC.");
            Validate(start, false, depth + 1);
        }
        else throw new FormatException($"Unsupported date operator {property.Name}.");
    }

    /// <summary>Requires a single, unambiguous operator.</summary>
    private static JsonProperty Operator(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object || node.EnumerateObject().Count() != 1)
            throw new FormatException("Each expression must be an object containing exactly one operator.");
        return node.EnumerateObject().First();
    }

    /// <summary>Validates and reads an operator's argument list.</summary>
    private static JsonElement[] Array(JsonElement node, int minimum, int maximum)
    {
        if (node.ValueKind != JsonValueKind.Array || node.GetArrayLength() < minimum || node.GetArrayLength() > maximum)
            throw new FormatException($"Expected {minimum} to {maximum} arguments.");
        return node.EnumerateArray().ToArray();
    }

    /// <summary>Evaluates all branches so a malformed field never causes deletion through Boolean short-circuiting.</summary>
    private static bool Boolean(JsonElement node, Func<string, DateTimeOffset?> field, DateTimeOffset now)
    {
        var op = Operator(node); var args = op.Value.EnumerateArray().ToArray();
        if (op.Name is "$and" or "$or" or "$not")
        {
            var values = args.Select(x => Boolean(x, field, now)).ToArray();
            return op.Name == "$and" ? values.All(x => x) : op.Name == "$or" ? values.Any(x => x) : !values[0];
        }
        var comparison = Date(args[0], field, now).CompareTo(Date(args[1], field, now));
        return op.Name switch
        {
            "$lt" => comparison < 0,
            "$lte" => comparison <= 0,
            "$gt" => comparison > 0,
            "$gte" => comparison >= 0,
            "$eq" => comparison == 0,
            "$ne" => comparison != 0,
            _ => false
        };
    }

    /// <summary>Evaluates UTC date operands using calendar-aware month and year arithmetic.</summary>
    private static DateTimeOffset Date(JsonElement node, Func<string, DateTimeOffset?> field, DateTimeOffset now)
    {
        if (node.ValueKind == JsonValueKind.String)
            return node.GetString() == "$$NOW" ? now : field(node.GetString()![1..])?.ToUniversalTime()
                ?? throw new FormatException($"Missing or invalid date field {node.GetString()}.");
        var op = Operator(node);
        if (op.Name == "$date") return ReadDate(node)!.Value;
        var args = op.Value;
        var start = Date(args.GetProperty("startDate"), field, now);
        var amount = args.GetProperty("amount").GetInt32() * (op.Name == "$dateSubtract" ? -1 : 1);
        return args.GetProperty("unit").GetString() switch
        {
            "millisecond" => start.AddMilliseconds(amount),
            "second" => start.AddSeconds(amount),
            "minute" => start.AddMinutes(amount),
            "hour" => start.AddHours(amount),
            "day" => start.AddDays(amount),
            "week" => start.AddDays(amount * 7),
            "month" => start.AddMonths(amount),
            "quarter" => start.AddMonths(amount * 3),
            "year" => start.AddYears(amount),
            _ => throw new FormatException("Unsupported date unit.")
        };
    }
}
