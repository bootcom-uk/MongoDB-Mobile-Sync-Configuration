using System.Text.Json;
using SyncConfiguration.Criteria;
using SyncConfiguration.Services.Expiry;

/// <summary>Checks the reusable editor contract with expiry and an unrelated business-rule consumer.</summary>
internal static class CriteriaChecks
{
    /// <summary>Runs lossless editing, type-safety, adapter and grouping checks.</summary>
    public static void Run()
    {
        var options = MongoExpiryCriteriaAdapter.CreateOptions([new("DateAdded", "Date added", CriteriaValueType.DateTime)]);
        var document = new CriteriaDocument(options);
        var original = """{ "$lte": [ {"$dateAdd":{"startDate":"$DateAdded","unit":"day","amount":30}}, "$$NOW" ] }""";
        Assert(document.Read(original), "Existing expiry imports into the reusable model");
        Assert(document.Text == original, "Entering designer preserves exact original JSON");
        Assert(document.Describe(document.Root!).Contains("Date added + 30 days"), "Consumer supplies readable function captions");
        var prior = document.Root;
        Assert(!document.Read("{broken") && document.Text == "{broken" && ReferenceEquals(document.Root, prior), "Invalid text is preserved without replacing the valid tree");
        Assert(document.Read(original) && document.SetRoot(document.Root!), "Visual expiry edits serialize through the adapter");
        Assert(new ExpiryEditorDocument { Text = document.Text }.Preview("""{"DateAdded":"2020-01-01T00:00:00Z"}""", DateTimeOffset.UtcNow).StartsWith("Expired"), "Adapted expressions retain evaluator behaviour");
        var single = options.CreateCondition();
        var group = new CriteriaGroup(CriteriaJunction.Any, [single, single with { OperatorId = "$lt" }]);
        var inverted = CriteriaDocument.Regroup(group, CriteriaJunction.Not);
        Assert(inverted.Children.Count == 1 && ReferenceEquals(inverted.Children[0], group), "Changing a populated group to Not preserves all children");
        Assert(document.SetRoot(inverted), "Nested groups round-trip through the expiry adapter");
        Assert(!document.Read("""{"$where":"anything"}"""), "Unsupported textual functions do not enter designer mode");
        var invalidOffset = new CriteriaFunction("dateAdd", [new CriteriaFieldOperand("DateAdded", CriteriaValueType.DateTime),
            new CriteriaLiteral("1.5", CriteriaValueType.Number), new CriteriaLiteral("day", CriteriaValueType.Choice)], CriteriaValueType.DateTime);
        Assert(!document.SetRoot(single with { Left = invalidOffset }), "Consumer can reject fractional calendar offsets without crashing");

        var business = new CriteriaBuilderOptions
        {
            Fields = [new("Name", "Customer name", CriteriaValueType.String), new("Balance", "Balance", CriteriaValueType.Number),
                new("Active", "Active", CriteriaValueType.Boolean), new("Status", "Status", CriteriaValueType.Choice, ["Open", "Closed"])],
            Operators = [new("eq", "equals", CriteriaValueType.String), new("gt", "is greater than", CriteriaValueType.Number),
                new("eq", "equals", CriteriaValueType.Boolean), new("eq", "equals", CriteriaValueType.Choice)],
            Adapter = new BusinessAdapter(),
            CreateCondition = () => new("eq", new CriteriaFieldOperand("Name", CriteriaValueType.String), new CriteriaLiteral("", CriteriaValueType.String))
        };
        var other = new CriteriaDocument(business);
        Assert(other.Read("""["eq","Name","String","Ada"]"""), "An unrelated adapter imports string criteria");
        Assert(other.Describe(other.Root!) == "Customer name equals Ada", "Generic summaries use the consumer's field names");
        Assert(other.Read("""["gt","Balance","Number","12.5"]"""), "Number criteria use the same editing state");
        Assert(!other.Read("""["gt","Balance","Number","not a number"]"""), "Invalid typed literals are rejected");
        Assert(other.Read("""["eq","Active","Boolean","true"]"""), "Boolean criteria use the same editing state");
        Assert(other.Read("""["eq","Status","Choice","Open"]"""), "Enum choices are supported");
        Assert(!other.Read("""["eq","Status","Choice","Unknown"]"""), "Unknown enum choices are rejected");
        Assert(!other.Read("""["eq","Balance","String","text"]"""), "Field and operand types cannot disagree");
        Console.WriteLine("Reusable criteria checks passed: independent consumers, adapters, typed values, readable functions, grouping and lossless invalid drafts.");
    }

    /// <summary>Fails a scenario with an actionable name.</summary>
    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    /// <summary>Provides a deliberately non-MongoDB persistence format for reuse tests.</summary>
    private sealed class BusinessAdapter : ICriteriaTextAdapter
    {
        /// <summary>Gets this consumer's text-mode caption.</summary>
        public string TextLabel => "Rule";
        /// <summary>Reads a simple business-rule array independent of expiry.</summary>
        public bool TryRead(string text, out CriteriaNode? root, out string? error)
        {
            root = null; error = null;
            try
            {
                var values = JsonSerializer.Deserialize<string[]>(text)!;
                var type = Enum.Parse<CriteriaValueType>(values[2]);
                root = new CriteriaCondition(values[0], new CriteriaFieldOperand(values[1], type), new CriteriaLiteral(values[3], type));
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }
        /// <summary>Writes this test consumer's independent storage format.</summary>
        public string Write(CriteriaNode root)
        {
            var condition = (CriteriaCondition)root;
            return JsonSerializer.Serialize(new[] { condition.OperatorId, ((CriteriaFieldOperand)condition.Left).Path,
                condition.Right.ValueType.ToString(), ((CriteriaLiteral)condition.Right).Value });
        }
    }
}
