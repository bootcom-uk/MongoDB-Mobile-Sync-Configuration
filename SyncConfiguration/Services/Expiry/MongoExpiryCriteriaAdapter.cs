#nullable enable
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using MongoDB.Sync.Expiry;
using SyncConfiguration.Criteria;

namespace SyncConfiguration.Services.Expiry;

/// <summary>Maps the expiry JSON contract to the reusable criteria model.</summary>
public sealed class MongoExpiryCriteriaAdapter : ICriteriaTextAdapter
{
    /// <summary>Gets the textual mode's label.</summary>
    public string TextLabel => "JSON";

    /// <summary>Validates and imports a MongoDB-style Boolean expiry expression.</summary>
    public bool TryRead(string text, out CriteriaNode? root, out string? error)
    {
        root = null;
        if (!ExpiryRule.TryParse(text, out var rule, out error)) return false;
        if (rule is null) { error = "Enter an expiry condition."; return false; }
        using var json = JsonDocument.Parse(text);
        root = ReadNode(json.RootElement);
        return true;
    }

    /// <summary>Writes a visual expression using the existing expiry contract.</summary>
    public string Write(CriteriaNode root) => WriteNode(root).ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    /// <summary>Creates date-only capabilities for the first consumer of the generic control.</summary>
    public static CriteriaBuilderOptions CreateOptions(IReadOnlyList<CriteriaField> fields)
    {
        var date = CriteriaValueType.DateTime;
        var units = new[] { "millisecond", "second", "minute", "hour", "day", "week", "month", "quarter", "year" };
        return new CriteriaBuilderOptions
        {
            Adapter = new MongoExpiryCriteriaAdapter(), Fields = fields, AllowCustomFields = true,
            Operators = [new("$lt", "is before", date), new("$lte", "is on or before", date),
                new("$gt", "is after", date), new("$gte", "is on or after", date), new("$eq", "equals", date), new("$ne", "does not equal", date)],
            Functions = [new("now", "Current time", date, [], () => new("now", [], date), _ => "Current time"),
                Offset("dateAdd", "Add a time period", "+"), Offset("dateSubtract", "Subtract a time period", "−")],
            CreateCondition = () => new("$lte", new CriteriaFunction("dateAdd", [new CriteriaFieldOperand(fields.FirstOrDefault()?.Path ?? "date", date),
                new CriteriaLiteral("30", CriteriaValueType.Number), new CriteriaLiteral("day", CriteriaValueType.Choice)], date), new CriteriaFunction("now", [], date))
        };

        /// <summary>Describes one date-offset function without exposing JSON to the visual control.</summary>
        CriteriaFunctionDefinition Offset(string id, string label, string sign) => new(id, label, date,
            [new("Starting date", date), new("Amount", CriteriaValueType.Number), new("Unit", CriteriaValueType.Choice, units)],
            () => new(id, [new CriteriaFieldOperand(fields.FirstOrDefault()?.Path ?? "date", date),
                new CriteriaLiteral("30", CriteriaValueType.Number), new CriteriaLiteral("day", CriteriaValueType.Choice)], date),
            args => $"{args[0]} {sign} {args[1]} {args[2]}{(args[1] == "1" ? "" : "s")}");
    }

    /// <summary>Reads Boolean groups and date comparisons after contract validation.</summary>
    private static CriteriaNode ReadNode(JsonElement node)
    {
        var op = node.EnumerateObject().Single(); var args = op.Value.EnumerateArray().ToArray();
        return op.Name switch
        {
            "$and" => new CriteriaGroup(CriteriaJunction.All, args.Select(ReadNode).ToArray()),
            "$or" => new CriteriaGroup(CriteriaJunction.Any, args.Select(ReadNode).ToArray()),
            "$not" => new CriteriaGroup(CriteriaJunction.Not, args.Select(ReadNode).ToArray()),
            _ => new CriteriaCondition(op.Name, ReadOperand(args[0]), ReadOperand(args[1]))
        };
    }

    /// <summary>Reads field paths, dates and date functions.</summary>
    private static CriteriaOperand ReadOperand(JsonElement node)
    {
        var date = CriteriaValueType.DateTime;
        if (node.ValueKind == JsonValueKind.String)
            return node.GetString() == "$$NOW" ? new CriteriaFunction("now", [], date) : new CriteriaFieldOperand(node.GetString()![1..], date);
        var op = node.EnumerateObject().Single();
        if (op.Name == "$date") return new CriteriaLiteral(ExpiryRule.ReadDate(node)!.Value.ToString("O", CultureInfo.InvariantCulture), date);
        return new CriteriaFunction(op.Name[1..], [ReadOperand(op.Value.GetProperty("startDate")),
            new CriteriaLiteral(op.Value.GetProperty("amount").GetInt32().ToString(CultureInfo.InvariantCulture), CriteriaValueType.Number),
            new CriteriaLiteral(op.Value.GetProperty("unit").GetString()!, CriteriaValueType.Choice)], date);
    }

    /// <summary>Writes a Boolean node to the consumer's JSON representation.</summary>
    private static JsonObject WriteNode(CriteriaNode node) => node switch
    {
        CriteriaCondition condition => new() { [condition.OperatorId] = new JsonArray(WriteOperand(condition.Left), WriteOperand(condition.Right)) },
        CriteriaGroup group => new() { [group.Junction switch { CriteriaJunction.All => "$and", CriteriaJunction.Any => "$or", _ => "$not" }] = new JsonArray(group.Children.Select(child => (JsonNode)WriteNode(child)).ToArray()) },
        _ => throw new FormatException("Unsupported expiry condition.")
    };

    /// <summary>Writes typed date operands, rejecting unsupported consumer functions.</summary>
    private static JsonNode WriteOperand(CriteriaOperand operand) => operand switch
    {
        CriteriaFieldOperand field => JsonValue.Create("$" + field.Path)!,
        CriteriaLiteral { ValueType: CriteriaValueType.DateTime } literal => new JsonObject { ["$date"] = literal.Value },
        CriteriaFunction { FunctionId: "now" } => JsonValue.Create("$$NOW")!,
        CriteriaFunction function when function.FunctionId is "dateAdd" or "dateSubtract" => new JsonObject
        {
            ["$" + function.FunctionId] = new JsonObject { ["startDate"] = WriteOperand(function.Arguments[0]),
                ["amount"] = int.Parse(((CriteriaLiteral)function.Arguments[1]).Value, CultureInfo.InvariantCulture),
                ["unit"] = ((CriteriaLiteral)function.Arguments[2]).Value }
        },
        _ => throw new FormatException("Unsupported expiry operand.")
    };
}
