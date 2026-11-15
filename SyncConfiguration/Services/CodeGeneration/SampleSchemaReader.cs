using System.Text.Json;

namespace SyncConfiguration.Services.CodeGeneration;

/// <summary>Reads shapes only. Sample values are never included in generated code.</summary>
public static class SampleSchemaReader
{
    public static IReadOnlyList<ModelField> Read(string json)
    {
        if (json.Length > 2_000_000)
            throw new ArgumentException("Use a sample smaller than 2 MB.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object) return ReadObject(root);
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0 ||
            root.EnumerateArray().Any(value => value.ValueKind != JsonValueKind.Object))
            throw new ArgumentException("Paste one JSON document or a non-empty array of documents.");
        return root.EnumerateArray().Select(ReadObject).Aggregate(MergeFields);
    }

    private static IReadOnlyList<ModelField> ReadObject(JsonElement value) => value.EnumerateObject()
        .GroupBy(property => property.Name, StringComparer.Ordinal)
        .Select(group => new ModelField(group.Key, group.Select(property => ReadType(property.Value)).Aggregate(MergeType)))
        .ToList();

    private static ModelType ReadType(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                // MongoDB Extended JSON emitted by Compass and mongoexport.
                if (value.EnumerateObject().Count() == 1)
                {
                    var property = value.EnumerateObject().First();
                    var kind = property.Name switch
                    {
                        "$oid" => "ObjectId", "$date" => "DateTime", "$numberInt" => "Int32",
                        "$numberLong" => "Int64", "$numberDouble" => "Double", "$numberDecimal" => "Decimal128",
                        "$binary" => "Binary", "$uuid" => "Guid", _ => null
                    };
                    if (kind != null) return new(kind);
                }
                return new("Object", ReadObject(value));
            case JsonValueKind.Array:
                var elements = value.EnumerateArray().Select(ReadType).ToList();
                return new("Array", Element: elements.Count == 0 ? new("Unknown") : elements.Aggregate(MergeType));
            case JsonValueKind.String: return new("String");
            case JsonValueKind.Number:
                return new(value.TryGetInt32(out _) ? "Int32" : value.TryGetInt64(out _) ? "Int64" : "Double");
            case JsonValueKind.True:
            case JsonValueKind.False: return new("Boolean");
            default: return new("Null", IsNullable: true);
        }
    }

    public static IReadOnlyList<ModelField> MergeFields(IReadOnlyList<ModelField> left, IReadOnlyList<ModelField> right)
    {
        var rightByName = right.ToDictionary(field => field.Name, StringComparer.Ordinal);
        var leftNames = left.Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
        return left.Select(field => rightByName.TryGetValue(field.Name, out var other)
                ? field with { Type = MergeType(field.Type, other.Type) }
                : field with { Type = field.Type with { IsNullable = true } })
            .Concat(right.Where(field => !leftNames.Contains(field.Name))
                .Select(field => field with { Type = field.Type with { IsNullable = true } })).ToList();
    }

    private static ModelType MergeType(ModelType left, ModelType right)
    {
        if (left.Kind is "Unknown" or "Null") return right with { IsNullable = right.IsNullable || left.IsNullable };
        if (right.Kind is "Unknown" or "Null") return left with { IsNullable = left.IsNullable || right.IsNullable };
        var nullable = left.IsNullable || right.IsNullable;
        if (left.Kind == right.Kind)
            return left.Kind switch
            {
                "Object" => new("Object", MergeFields(left.Fields ?? [], right.Fields ?? []), IsNullable: nullable),
                "Array" => new("Array", Element: MergeType(left.Element, right.Element), IsNullable: nullable),
                _ => left with { IsNullable = nullable }
            };
        var numbers = new[] { "Int32", "Int64", "Double", "Decimal128" };
        if (numbers.Contains(left.Kind) && numbers.Contains(right.Kind))
            return new(numbers[Math.Max(Array.IndexOf(numbers, left.Kind), Array.IndexOf(numbers, right.Kind))], IsNullable: nullable);
        return new("Mixed", IsNullable: nullable);
    }

    /// <summary>Preserve authoritative scalar schema types; fill missing object/array shapes from a sample.</summary>
    public static IReadOnlyList<ModelField> Enrich(IReadOnlyList<ModelField> schema, IReadOnlyList<ModelField> sample)
    {
        var result = schema.ToList();
        foreach (var field in sample)
        {
            var index = result.FindIndex(existing => existing.Name == field.Name);
            if (index < 0) result.Add(field);
            else result[index] = result[index] with { Type = EnrichType(result[index].Type, field.Type) };
        }
        return result;
    }

    private static ModelType EnrichType(ModelType schema, ModelType sample)
    {
        if (schema.Kind is "Unknown" or "Null")
            return sample with { IsNullable = sample.IsNullable || schema.IsNullable || schema.Kind == "Null" };
        if (schema.Kind == "Object" && sample.Kind == "Object")
            return schema with { Fields = Enrich(schema.Fields ?? [], sample.Fields ?? []), IsNullable = schema.IsNullable || sample.IsNullable };
        if (schema.Kind == "Array" && sample.Kind == "Array")
            return schema with
            {
                Element = EnrichType(schema.Element ?? new("Unknown"), sample.Element ?? new("Unknown")),
                IsNullable = schema.IsNullable || sample.IsNullable
            };
        return schema with { IsNullable = schema.IsNullable || sample.IsNullable || sample.Kind == "Null" };
    }
}
