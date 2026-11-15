using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SyncConfiguration.Services.CodeGeneration;

public sealed class CSharpModelGenerator
{
    private static readonly HashSet<string> Keywords = new(("abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while").Split(' '));
    private readonly List<(string Name, string Body)> classes = [];
    private readonly List<string> warnings = [];
    private readonly HashSet<string> classNames = new(StringComparer.OrdinalIgnoreCase);

    public static string SuggestClassName(string collectionName) => Identifier(collectionName, "CollectionModel");

    public ModelGenerationResult Generate(IReadOnlyList<ModelField> fields, ModelGenerationOptions options)
    {
        classes.Clear(); warnings.Clear(); classNames.Clear();
        ValidateNamespace(options.Namespace);
        ValidateNamespace(options.BaseNamespace);
        // Names imported by generated files must not be shadowed by model classes.
        foreach (var name in new[] { "BaseLocalCacheModel", "ObservableObject", "ObservableCollection", "ObjectId", "DateTime", "Guid", "BsonField", "JsonPropertyName", "CollectionName", "ObservableProperty" })
            classNames.Add(name);
        foreach (var name in new[] { "BsonFieldAttribute", "JsonPropertyNameAttribute", "CollectionNameAttribute", "ObservablePropertyAttribute" })
            classNames.Add(name);
        foreach (var name in new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" })
            classNames.Add(name);
        var nameSuggestion = Identifier(options.ClassName, "CollectionModel");
        var rootName = Unique(nameSuggestion, classNames);
        if (rootName != options.ClassName) warnings.Add($"Class name normalized to {rootName}.");
        BuildClass(rootName, fields, true, options.CollectionMapping, 0);
        var header = new StringBuilder("// Generated from the available schema. Review inferred types before use.\n#nullable enable\nusing System;\nusing System.Collections.ObjectModel;\nusing System.Text.Json.Serialization;\nusing CommunityToolkit.Mvvm.ComponentModel;\nusing LiteDB;\n");
        if (!string.IsNullOrWhiteSpace(options.BaseNamespace)) header.AppendLine($"using {options.BaseNamespace.Trim()};");
        if (!string.IsNullOrWhiteSpace(options.CollectionMapping)) header.AppendLine("using MongoDB.Sync.Models.Attributes;");
        header.AppendLine();
        string Wrap(string body) => string.IsNullOrWhiteSpace(options.Namespace)
            ? header + body
            : header + $"namespace {options.Namespace.Trim()}\n{{\n" + Indent(body) + "}\n";
        return new(classes.Select(item => new GeneratedModelFile(item.Name, Wrap(item.Body))).ToList(),
            Wrap(string.Join("\n", classes.Select(item => item.Body))), warnings.Distinct().ToList());
    }

    private void BuildClass(string name, IReadOnlyList<ModelField> fields, bool root, string mapping, int depth)
    {
        if (depth > 24) throw new ArgumentException("The schema is nested too deeply to generate safely.");
        var index = classes.Count;
        classes.Add((name, ""));
        var code = new StringBuilder();
        if (root && !string.IsNullOrWhiteSpace(mapping)) code.AppendLine($"[CollectionName({Literal(mapping)})]");
        code.AppendLine($"public partial class {name} : {(root ? "BaseLocalCacheModel" : "ObservableObject")}");
        code.AppendLine("{");
        if (root) code.AppendLine("    // Id is inherited from BaseLocalCacheModel.");
        if (fields.Count == 0)
        {
            warnings.Add($"{name}: no field details are available. Paste sample JSON to complete this class.");
            code.AppendLine("    // TODO: Field details are missing. Supply a representative JSON sample.");
        }
        var propertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            name, "PropertyChanged", "PropertyChanging", "OnPropertyChanged", "OnPropertyChanging",
            "SetProperty", "SetPropertyAndNotifyOnCompletion", "Equals", "GetHashCode", "GetType", "ToString"
        };
        if (root) propertyNames.Add("Id");
        foreach (var field in fields)
        {
            if (root && (field.Name == "_id" || string.Equals(field.Name, "id", StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.Equals(field.Type.Kind, "ObjectId", StringComparison.OrdinalIgnoreCase))
                    warnings.Add($"{name}.{field.Name}: the base model requires a LiteDB.ObjectId; check this field's {field.Type.Kind} type.");
                continue;
            }
            var suggestion = Identifier(field.Name, "Field");
            var property = Unique(suggestion, propertyNames);
            if (property != suggestion || !string.Equals(property, field.Name.TrimStart('_'), StringComparison.OrdinalIgnoreCase))
                warnings.Add($"{name}: field {Literal(field.Name)} is emitted as {property}. Review custom sync resolvers that match property names directly.");
            var type = ResolveType(field.Type, name + "_" + property, depth + 1);
            var nullable = field.Type.IsNullable || type is "string" or "object" ||
                (field.Type.Kind.Equals("Object", StringComparison.OrdinalIgnoreCase));
            if (nullable && !type.EndsWith('?')) type += "?";
            var initializer = type.StartsWith("ObservableCollection<", StringComparison.Ordinal) ? " = new();"
                : type == "byte[]" ? " = global::System.Array.Empty<byte>();"
                : type == "ObjectId" ? " = ObjectId.Empty;" : ";";
            code.AppendLine();
            if (type == "object?" || field.Type.Kind.StartsWith("Mixed", StringComparison.OrdinalIgnoreCase))
                code.AppendLine("    // TODO: The schema does not identify one concrete type for this value.");
            code.AppendLine("    [ObservableProperty]");
            code.AppendLine($"    [property: BsonField({Literal(field.Name)})]");
            code.AppendLine($"    [property: JsonPropertyName({Literal(field.Name)})]");
            code.AppendLine($"    private {type} _{char.ToLowerInvariant(property[0])}{property[1..]}{initializer}");
        }
        code.AppendLine("}");
        classes[index] = (name, code.ToString());
    }

    private string ResolveType(ModelType type, string nestedName, int depth)
    {
        if (depth > 24) throw new ArgumentException("The schema is nested too deeply to generate safely.");
        switch (type.Kind.ToLowerInvariant())
        {
            case "array":
                var element = type.Element ?? new("Unknown");
                var elementType = ResolveType(element, nestedName, depth + 1);
                if ((element.IsNullable || elementType == "object") && !elementType.EndsWith('?')) elementType += "?";
                return $"ObservableCollection<{elementType}>";
            case "object":
            case "document":
                var className = Unique(nestedName, classNames);
                BuildClass(className, type.Fields ?? [], false, null, depth);
                return className;
            case "string": case "text": return "string";
            case "int": case "int32": return "int";
            case "long": case "int64": return "long";
            case "number": case "double": return "double";
            case "decimal": case "decimal128": return "decimal";
            case "bool": case "boolean": return "bool";
            case "date": case "datetime": return "DateTime";
            case "objectid": return "ObjectId";
            case "uuid": case "guid": return "Guid";
            case "binary": case "bindata": return "byte[]";
            default:
                warnings.Add($"{nestedName}: {Literal(type.Kind)} needs review; generated object as a fallback. An empty array or null value cannot establish its item type.");
                return "object";
        }
    }

    private static string Identifier(string value, string fallback)
    {
        var parts = Regex.Matches(value ?? "", "[A-Za-z0-9]+").Select(match => match.Value).ToList();
        if (parts.Count == 0) return fallback;
        var name = string.Join("_", parts.Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        if (char.IsDigit(name[0])) name = "Model" + name;
        return name;
    }

    private static string Unique(string name, HashSet<string> used)
    {
        var candidate = name;
        for (var suffix = 2; !used.Add(candidate); suffix++) candidate = name + suffix;
        return candidate;
    }

    private static void ValidateNamespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (value.Trim().Split('.').Any(part => !Regex.IsMatch(part, "^[A-Za-z_][A-Za-z0-9_]*$") || Keywords.Contains(part)))
            throw new ArgumentException("Use a valid C# namespace, such as MyApp.Models.Data, or leave it blank.");
    }

    private static string Literal(string text) => JsonSerializer.Serialize(text);
    private static string Indent(string body) => string.Join("\n", body.TrimEnd().Split('\n').Select(line => "    " + line)) + "\n";
}
