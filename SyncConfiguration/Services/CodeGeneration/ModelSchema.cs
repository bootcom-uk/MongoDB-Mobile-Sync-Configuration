using MongoDB.Sync.Models;
using SyncConfiguration.Models.AppDetail;

namespace SyncConfiguration.Services.CodeGeneration;

public sealed record ModelField(string Name, ModelType Type);

public sealed record ModelType(string Kind, IReadOnlyList<ModelField> Fields = null,
    ModelType Element = null, bool IsNullable = false)
{
    public static IReadOnlyList<ModelField> FromCollection(EditCollectionModel collection, bool selectedOnly) =>
        collection.Fields.Where(field => !selectedOnly || field.IsSelected)
            .Select(field => new ModelField(field.Name, FromType(field.Type))).ToList();

    private static ModelType FromType(FieldType type) => Parse(type?.Type, type?.NestedFields);

    private static ModelType Parse(string kind, List<FieldSchema> nested = null)
    {
        kind = kind?.Trim() ?? "Unknown";
        var fields = nested?.Select(field => new ModelField(field.Name, FromType(field.Type))).ToList();
        if (kind.StartsWith("Array<", StringComparison.OrdinalIgnoreCase) && kind.EndsWith('>'))
            return new("Array", Element: Parse(kind[6..^1], nested));
        if (kind.Equals("Array", StringComparison.OrdinalIgnoreCase))
            return new("Array", Element: fields?.Count > 0 ? new("Object", fields) : new("Unknown"));
        return new(kind, fields);
    }
}

public sealed record GeneratedModelFile(string ClassName, string Code)
{
    public string FileName => ClassName + ".cs";
}

public sealed record ModelGenerationResult(IReadOnlyList<GeneratedModelFile> Files, string CombinedCode,
    IReadOnlyList<string> Warnings);

public sealed record ModelGenerationOptions(string ClassName, string Namespace = "Models.Data",
    string BaseNamespace = "MongoDB.Sync.Core.Services.Models.Models", string CollectionMapping = "");
