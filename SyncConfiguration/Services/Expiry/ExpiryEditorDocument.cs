using System.Text.Json;
using System.Text.Json.Nodes;
using MongoDB.Sync.Expiry;

namespace SyncConfiguration.Services.Expiry;

/// <summary>Preserves expression drafts while switching between textual and visual editing.</summary>
public sealed class ExpiryEditorDocument
{
    /// <summary>Gets or sets the exact draft, including invalid user input.</summary>
    public string Text { get; set; } = "";

    /// <summary>Validates a draft and constructs an independent designer tree without changing the text.</summary>
    public bool TryDesign(out JsonObject? root, out string? error)
    {
        root = null;
        if (!ExpiryRule.TryParse(Text, out var rule, out error)) return false;
        if (rule is null) { error = "Choose a condition or enter an expression to enable expiry."; return false; }
        root = JsonNode.Parse(Text)!.AsObject();
        return true;
    }

    /// <summary>Formats visual edits as the canonical expression draft.</summary>
    public void SetDesign(JsonObject root) => Text = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    /// <summary>Reports rule and synchronized-field errors before applying a mapping.</summary>
    public string? Validate(IEnumerable<string> selectedFields)
    {
        if (!ExpiryRule.TryParse(Text, out var rule, out var error)) return error;
        if (rule is null) return "Choose a condition or enter an expression to enable expiry.";
        var selected = selectedFields.ToArray();
        var missing = rule.Fields.Where(path => !selected.Any(field => path == field || path.StartsWith(field + ".", StringComparison.Ordinal))).ToArray();
        return missing.Length == 0 ? null : "Select these fields for synchronization first: " + string.Join(", ", missing);
    }

    /// <summary>Evaluates a sample without any persistence or database access.</summary>
    public string Preview(string sample, DateTimeOffset now)
    {
        if (!ExpiryRule.TryParse(Text, out var rule, out var error) || rule is null) return "Cannot evaluate: " + (error ?? "Expiry is disabled.");
        try
        {
            using var json = JsonDocument.Parse(sample);
            /// <summary>Reads the requested date from the sample document.</summary>
            DateTimeOffset? Field(string path)
            {
                var value = json.RootElement;
                foreach (var part in path.Split('.'))
                    if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(part, out value)) return null;
                return ExpiryRule.ReadDate(value);
            }
            if (!rule.TryEvaluate(Field, now, out var expired, out error)) return "Cannot evaluate: " + error;
            return expired ? "Expired — this document would be removed from the cache only." : "Not expired — this document would stay in the cache.";
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException) { return "Cannot evaluate: " + ex.Message; }
    }
}
