using SyncConfiguration.Services.Expiry;
using System.Text.Json.Nodes;

CriteriaChecks.Run();

var text = """{"$and":[{"$lte":[{"$dateAdd":{"startDate":"$completedAt","unit":"day","amount":30}},"$$NOW"]},{"$not":[{"$gt":["$completedAt",{"$date":"2026-11-01T00:00:00Z"}]}]}]}""";
var draft = new ExpiryEditorDocument { Text = text };
Check(draft.TryDesign(out var tree, out _), "Nested JSON loads in designer");
draft.SetDesign(tree!);
Check(JsonNode.DeepEquals(JsonNode.Parse(text), JsonNode.Parse(draft.Text)), "Designer/text round trip preserves expression");
Check(draft.Validate(["completedAt"]) is null, "Selected field accepted");
Check(draft.Validate([])!.Contains("completedAt"), "Missing synchronized field reported");
Check(draft.Preview("""{"completedAt":{"$date":"2026-01-01T00:00:00Z"}}""", DateTimeOffset.Parse("2026-11-15T00:00:00Z")).StartsWith("Expired"), "Preview evaluates Extended JSON dates");
Check(draft.Preview("{}", DateTimeOffset.UtcNow).StartsWith("Cannot evaluate"), "Preview explains missing dates");
draft.Text = "{broken";
Check(!draft.TryDesign(out _, out _), "Invalid text cannot enter designer");
Check(draft.Text == "{broken", "Invalid draft is never discarded");
draft.Text = """{"$lte":["$nested.date","$$NOW"]}""";
Check(draft.Validate(["nested"]) is null, "Synchronized parent supplies nested date");
Check(draft.Preview("""{"nested":{"date":"2000-01-01T00:00:00Z"}}""", DateTimeOffset.UtcNow).StartsWith("Expired"), "Nested sample dates evaluate");
Console.WriteLine("Expiry designer checks passed: nested round trips, invalid draft preservation, selected fields, and preview.");

/// <summary>Fails a designer-contract assertion with its scenario name.</summary>
static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

