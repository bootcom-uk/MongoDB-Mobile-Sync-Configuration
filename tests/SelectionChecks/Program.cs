using SyncConfiguration.Models.AppDetail;
using System.Text.Json;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine($"PASS: {message}");
}

// Exercise the same collection replacement performed when the schema is deserialized.
var collection = JsonSerializer.Deserialize<EditCollectionModel>("""
    {"CollectionName":"Orders","Fields":[
      {"Name":"_id","Type":{"Type":"ObjectId"}},
      {"Name":"Total","Type":{"Type":"Double"}},
      {"Name":"Notes","Type":{"Type":"String"}}]}
    """)!;
Check(collection.Fields.Count == 3, "Schema fields deserialize");
collection.IsSelected = true;
Check(collection.Fields.All(f => f.IsSelected), "Selecting a collection selects every field");
collection.Fields[2].IsSelected = false;
Check(collection.IsSelected && collection.Fields.Take(2).All(f => f.IsSelected), "Individual deselection preserves other selections");
var saved = collection.Fields.Where(f => f.IsSelected).Select(f => f.Name).ToArray();
collection.IsSelected = false;
Check(collection.Fields.All(f => !f.IsSelected), "Deselecting a collection clears every field");
collection.RestoreSelection(saved);
Check(collection.IsSelected && !collection.Fields[2].IsSelected && collection.Fields.Take(2).All(f => f.IsSelected), "Reload preserves the exact saved subset");
collection.RestoreSelection([]);
collection.Fields[1].IsSelected = true;
Check(collection.IsSelected && collection.Fields.Count(f => f.IsSelected) == 1, "Selecting one field enables only that field and its collection");
collection.Fields[1].IsSelected = false;
Check(!collection.IsSelected, "Clearing the last field clears the collection checkbox");
collection.RestoreSelection(["ArchivedField"]);
Check(collection.Fields.Single(f => f.Name == "ArchivedField").IsSelected, "Saved fields missing from sampled schema remain available");
collection.NewFieldName = "NewField";
collection.NewFieldType = "String";
collection.AddFieldCommand.Execute(null);
Check(collection.Fields.Count(f => f.IsSelected) == 2, "Adding a field preserves existing exclusions");
collection.IsSelected = false;
collection.IsSelected = true;
Check(collection.Fields.All(f => f.IsSelected), "Reselecting includes all available fields");

GeneratorChecks.Run(args.FirstOrDefault());
