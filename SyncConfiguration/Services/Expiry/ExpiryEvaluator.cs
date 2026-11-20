namespace MongoDB.Sync.Expiry;

/// <summary>Caches parsed rules and limits diagnostics to once per collection, rule and error.</summary>
public sealed class ExpiryEvaluator
{
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    /// <summary>Determines cache eligibility; invalid rules or document values always retain data.</summary>
    public bool IsExpired(string key, string? text, Func<string, DateTimeOffset?> field, DateTimeOffset now, Action<string> log)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        lock (entries)
        {
            if (!entries.TryGetValue(key, out var entry) || entry.Text != text)
            {
                if (entries.Count >= 256) entries.Clear();
                ExpiryRule.TryParse(text, out var rule, out var error);
                entry = new Entry(text, rule, error); entries[key] = entry;
            }
            var expired = false;
            var failure = entry.Error;
            if (entry.Rule is not null) entry.Rule.TryEvaluate(field, now, out expired, out failure);
            if (failure is not null && entry.Errors.Count < 16 && entry.Errors.Add(failure)) log(failure);
            return expired;
        }
    }

    /// <summary>Stores one parsed mapping revision and its bounded diagnostics.</summary>
    private sealed class Entry(string text, ExpiryRule? rule, string? error)
    {
        /// <summary>Gets the expression revision.</summary>
        public string Text { get; } = text;
        /// <summary>Gets the validated rule, if enabled.</summary>
        public ExpiryRule? Rule { get; } = rule;
        /// <summary>Gets the parse failure, if any.</summary>
        public string? Error { get; } = error;
        /// <summary>Gets diagnostics already emitted for this revision.</summary>
        public HashSet<string> Errors { get; } = new(StringComparer.Ordinal);
    }
}
