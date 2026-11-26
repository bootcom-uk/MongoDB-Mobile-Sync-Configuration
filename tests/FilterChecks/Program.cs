using SyncConfiguration.Services.Filtering;
using SyncConfiguration.Criteria;

var adapter = new MongoFilterCriteriaAdapter();
foreach (var text in new[]
{
    """{"$eq":["$userId","$$USER_ID"]}""",
    """{"$and":[{"$eq":["$active",true]},{"$gte":["$score",3.5]},{"$eq":["$owner",{"$oid":"0123456789abcdef01234567"}]}]}""",
    """{"$or":[{"$eq":["$status","active"]},{"$lt":["$date",{"$date":"2026-11-21T00:00:00Z"}]}]}"""
})
{
    Assert(adapter.TryRead(text, out var tree, out var error), error ?? "Read expression");
    var written = adapter.Write(tree!);
    Assert(adapter.TryRead(written, out _, out error), error ?? "Read round trip");
    Assert(adapter.Write(tree!) == written, "Stable designer serialization");
}
Assert(!adapter.TryRead("{invalid", out _, out _), "Invalid expression rejected");
var options = MongoFilterCriteriaAdapter.CreateOptions([new("owner", "Owner", CriteriaValueType.String)]);
Assert(adapter.Write(options.CreateCondition()).Contains("$$USER_ID"), "Designer defaults to the current user placeholder");
Assert(options.Functions.Any(x => x.Label == "Current user (ObjectId)"), "Current-user picker is available");
Console.WriteLine("Filter editor checks passed: typed values, nested groups, round trips and current-user selection.");

/// <summary>Fails a named configuration-editor invariant.</summary>
static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
