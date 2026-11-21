#nullable enable
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using MongoDB.Sync.Filtering;
using SyncConfiguration.Criteria;

namespace SyncConfiguration.Services.Filtering;

/// <summary>Connects the shared criteria designer to server-enforced visibility rules.</summary>
public sealed class MongoFilterCriteriaAdapter : ICriteriaTextAdapter
{
    /// <summary>Gets the expression format label.</summary>
    public string TextLabel => "JSON";

    /// <summary>Validates and imports a typed visibility expression.</summary>
    public bool TryRead(string text, out CriteriaNode? root, out string? error)
    {
        root = null; error = null;
        try
        {
            if (string.IsNullOrWhiteSpace(text)) throw new FormatException("Enter a filter condition.");
            SyncFilterRule.Validate(text);
            using var json = JsonDocument.Parse(text);
            root = ReadNode(json.RootElement);
            return true;
        }
        catch (FormatException ex) { error = ex.Message; return false; }
    }

    /// <summary>Writes a designer tree without evaluating user identity on the administrator's device.</summary>
    public string Write(CriteriaNode root) => WriteNode(root).ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    /// <summary>Offers typed comparisons, nested groups, ObjectId literals and the authenticated user placeholder.</summary>
    public static CriteriaBuilderOptions CreateOptions(IReadOnlyList<CriteriaField> fields) => new()
    {
        Adapter = new MongoFilterCriteriaAdapter(), Fields = fields, AllowCustomFields = true,
        Junctions = [CriteriaJunction.All, CriteriaJunction.Any],
        Operators = new[] { CriteriaValueType.String, CriteriaValueType.Number, CriteriaValueType.Boolean, CriteriaValueType.DateTime }
            .SelectMany(type => new[] { new CriteriaOperator("$eq", "equals", type), new("$ne", "does not equal", type),
                new("$lt", "is less than", type), new("$lte", "is at most", type), new("$gt", "is greater than", type), new("$gte", "is at least", type) }).ToArray(),
        Functions =
        [
            new("user", "Current user (ObjectId)", CriteriaValueType.String, [],
                () => new("user", [], CriteriaValueType.String), _ => "Current user (ObjectId)"),
            new("oid", "ObjectId", CriteriaValueType.String, [new("24-character ObjectId", CriteriaValueType.String)],
                () => new("oid", [new CriteriaLiteral("000000000000000000000000", CriteriaValueType.String)], CriteriaValueType.String),
                args => $"ObjectId({args[0]})")
        ],
        CreateCondition = () => new("$eq", new CriteriaFieldOperand(fields.FirstOrDefault(f => f.Type == CriteriaValueType.String)?.Path ?? "userId", CriteriaValueType.String),
            new CriteriaFunction("user", [], CriteriaValueType.String)),
        Validate = root =>
        {
            try { SyncFilterRule.Validate(new MongoFilterCriteriaAdapter().Write(root)); return null; }
            catch (FormatException ex) { return ex.Message; }
        }
    };

    /// <summary>Reads a group or field-to-value comparison.</summary>
    private static CriteriaNode ReadNode(JsonElement node)
    {
        var op = node.EnumerateObject().Single(); var args = op.Value.EnumerateArray().ToArray();
        if (op.Name is "$and" or "$or") return new CriteriaGroup(op.Name == "$and" ? CriteriaJunction.All : CriteriaJunction.Any, args.Select(ReadNode).ToArray());
        var value = ReadValue(args[1]);
        return new CriteriaCondition(op.Name, new CriteriaFieldOperand(args[0].GetString()![1..], value.ValueType), value);
    }

    /// <summary>Reads scalar literals and the server-resolved user value.</summary>
    private static CriteriaOperand ReadValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() == "$$USER_ID" ? new CriteriaFunction("user", [], CriteriaValueType.String)
            : new CriteriaLiteral(value.GetString()!, CriteriaValueType.String),
        JsonValueKind.Number => new CriteriaLiteral(value.GetRawText(), CriteriaValueType.Number),
        JsonValueKind.True or JsonValueKind.False => new CriteriaLiteral(value.GetRawText(), CriteriaValueType.Boolean),
        _ => value.TryGetProperty("$oid", out var oid)
            ? new CriteriaFunction("oid", [new CriteriaLiteral(oid.GetString()!, CriteriaValueType.String)], CriteriaValueType.String)
            : new CriteriaLiteral(value.GetProperty("$date").GetString()!, CriteriaValueType.DateTime)
    };

    /// <summary>Serializes supported Boolean nodes.</summary>
    private static JsonNode WriteNode(CriteriaNode node) => node switch
    {
        CriteriaGroup group => new JsonObject { [group.Junction == CriteriaJunction.All ? "$and" : "$or"] = new JsonArray(group.Children.Select(WriteNode).ToArray()) },
        CriteriaCondition condition => new JsonObject { [condition.OperatorId] = new JsonArray(WriteValue(condition.Left), WriteValue(condition.Right)) },
        _ => throw new FormatException("Unsupported condition.")
    };

    /// <summary>Serializes fields and typed literal operands.</summary>
    private static JsonNode WriteValue(CriteriaOperand value) => value switch
    {
        CriteriaFieldOperand field => JsonValue.Create("$" + field.Path)!,
        CriteriaFunction { FunctionId: "user" } => JsonValue.Create("$$USER_ID")!,
        CriteriaFunction { FunctionId: "oid" } function when function.Arguments[0] is CriteriaLiteral literal => new JsonObject { ["$oid"] = literal.Value },
        CriteriaLiteral literal when literal.ValueType == CriteriaValueType.DateTime => new JsonObject { ["$date"] = DateTimeOffset.Parse(literal.Value, CultureInfo.InvariantCulture).UtcDateTime.ToString("O") },
        CriteriaLiteral literal when literal.ValueType == CriteriaValueType.Number => JsonValue.Create(decimal.Parse(literal.Value, NumberStyles.Float, CultureInfo.InvariantCulture))!,
        CriteriaLiteral literal when literal.ValueType == CriteriaValueType.Boolean => JsonValue.Create(bool.Parse(literal.Value))!,
        CriteriaLiteral literal => JsonValue.Create(literal.Value)!,
        _ => throw new FormatException("Choose a field on the left and a value on the right.")
    };
}
