using MongoDB.Sync.Models;
using SyncConfiguration.Models.AppDetail;
using SyncConfiguration.Services.CodeGeneration;

internal static class GeneratorChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }

    public static void Run(string outputDirectory)
    {
        var collection = new EditCollectionModel
        {
            CollectionName = "ShoppingList", DatabaseName = "home-db",
            Fields = [
                new() { Name = "_id", Type = new FieldType { Type = "ObjectId" }, IsSelected = true },
                new() { Name = "title", Type = new FieldType { Type = "String" }, IsSelected = true },
                new() { Name = "items", Type = new FieldType { Type = "Array<Object>" }, IsSelected = true },
                new() { Name = "privateNote", Type = new FieldType { Type = "String" } }
            ]
        };
        var generator = new CSharpModelGenerator();
        var schema = ModelType.FromCollection(collection, true);
        var incomplete = generator.Generate(schema, new("ShoppingList"));
        Check(incomplete.Files.Count == 2 && incomplete.Warnings.Any(note => note.Contains("no field details")), "Missing object-array shape produces a named class and visible warning");
        var sample = SampleSchemaReader.Read("""
        [{"_id":{"$oid":"507f1f77bcf86cd799439011"},"title":"SAMPLE_VALUE_MUST_NOT_LEAK","items":[
           {"productId":{"$oid":"507f1f77bcf86cd799439011"},"quantity":1,"prices":[{"amount":2.5,"at":{"$date":"2026-01-01T00:00:00Z"}}]},
           {"quantity":2,"checked":true,"prices":[{"amount":3.1}]}],"tags":["weekly"]},
         {"title":"Other","items":[]}]
        """);
        var enriched = SampleSchemaReader.Enrich(schema, sample);
        var result = generator.Generate(enriched, new("ShoppingList", "Fixtures", CollectionMapping: "home_db_ShoppingList"));
        Check(result.Files.Select(file => file.ClassName).SequenceEqual(["ShoppingList", "ShoppingList_Items", "ShoppingList_Items_Prices"]), "Nested classes follow collection_property_path naming");
        Check(result.CombinedCode.Contains("ObservableCollection<ShoppingList_Items>") && result.CombinedCode.Contains("ObservableCollection<ShoppingList_Items_Prices>"), "Object lists use observable typed collections");
        Check(result.CombinedCode.Contains("ObservableCollection<string>"), "Primitive arrays stay primitive collections");
        Check(!result.CombinedCode.Contains("SAMPLE_VALUE_MUST_NOT_LEAK"), "Sample values never appear in exported code");
        Check(!result.Files[0].Code.Contains("private ObjectId _id"), "Root Id is inherited rather than redeclared");
        Check(result.CombinedCode.Contains("bool? _checked") && result.CombinedCode.Contains("DateTime? _at"), "Missing fields across sample objects become nullable");
        Check(!result.CombinedCode.Contains("_privateNote"), "Unselected fields are excluded from selected-only schema");
        Check(result.CombinedCode.Contains("[property: BsonField(\"productId\")]"), "Source field names are forwarded to generated property attributes");
        Check(result.Warnings.Count == 0, "Complete sample resolves missing nested schema warnings");

        var odd = generator.Generate([
            new("class", new("String")), new("a-b", new("Int32")), new("a b", new("Int64")),
            new("Name\"\n", new("Boolean")), new("1st", new("Decimal128")), new("GetType", new("String")),
            new("unknown", new("Array", Element: new("Unknown"))), new("variant", new("Mixed"))
        ], new("123 weird/name", "EdgeCases"));
        Check(odd.Files[0].ClassName == "Model123_Weird_Name", "Invalid class names are normalized safely");
        Check(odd.CombinedCode.Contains("_a_B2") && odd.CombinedCode.Contains("_getType2"), "Property collisions and inherited member names are disambiguated");
        Check(odd.CombinedCode.Contains("Name\\u0022\\n"), "Source field names are escaped as C# literals");
        Check(odd.Warnings.Count >= 3, "Ambiguous types and renamed fields are reported");

        var scalars = SampleSchemaReader.Read("""
        {"i":{"$numberInt":"1"},"l":{"$numberLong":"1"},"d":{"$numberDouble":"1.2"},"m":{"$numberDecimal":"1.23"},
         "reference":{"$oid":"507f1f77bcf86cd799439011"},"address":{"city":"Sample"},
         "guid":{"$uuid":"1"},"binary":{"$binary":{"base64":"AA==","subType":"00"}},"matrix":[[1,2],[3]],"mixed":[1,"one"],"nothing":null}
        """);
        var scalarCode = generator.Generate(scalars, new("ScalarModel", "EdgeCases"));
        Check(scalarCode.CombinedCode.Contains("ObservableCollection<ObservableCollection<int>>"), "Nested primitive arrays retain their shape");
        Check(scalarCode.CombinedCode.Contains("ObservableCollection<object?>") && scalarCode.Warnings.Count > 0, "Mixed arrays have an explicit fallback and review note");
        Check(scalarCode.CombinedCode.Contains("decimal _m") && scalarCode.CombinedCode.Contains("byte[] _binary"), "Extended JSON numeric and binary types are recognized");
        try { generator.Generate(schema, new("Model", "bad;namespace")); throw new Exception("Accepted invalid namespace"); }
        catch (ArgumentException) { Console.WriteLine("PASS: Invalid namespaces rejected"); }
        try { SampleSchemaReader.Read("[1,2]"); throw new Exception("Accepted scalar document sample"); }
        catch (ArgumentException) { Console.WriteLine("PASS: Non-document samples rejected"); }
        Check(collection.Fields.Count == 4 && collection.Fields[2].Type.NestedFields.Count == 0, "Generation leaves sync configuration untouched");
        var nullableFields = SampleSchemaReader.Enrich([new("amount", new("Decimal128"))], SampleSchemaReader.Read("[{\"amount\":1},{}]"));
        Check(nullableFields[0].Type.Kind == "Decimal128" && nullableFields[0].Type.IsNullable, "Samples preserve known types while supplying missing-field nullability");
        Check(generator.Generate([], new("CON")).Files[0].FileName != "CON.cs", "Export filenames avoid Windows device names");
        Check(generator.Generate([], new("ObservablePropertyAttribute")).Files[0].ClassName != "ObservablePropertyAttribute", "Class names cannot shadow source-generator attributes");

        if (outputDirectory != null)
        {
            Directory.CreateDirectory(outputDirectory);
            // Both copy-all and separate-file exports are compiled by the fixture project.
            File.WriteAllText(Path.Combine(outputDirectory, "Combined.cs"), result.CombinedCode);
            var separate = generator.Generate(enriched, new("ShoppingList", "SeparateFiles", CollectionMapping: "home_db_ShoppingList"));
            foreach (var file in separate.Files) File.WriteAllText(Path.Combine(outputDirectory, file.FileName), file.Code);
            File.WriteAllText(Path.Combine(outputDirectory, "OddNames.cs"), odd.CombinedCode);
            File.WriteAllText(Path.Combine(outputDirectory, "Scalars.cs"), scalarCode.CombinedCode);
        }
    }
}
