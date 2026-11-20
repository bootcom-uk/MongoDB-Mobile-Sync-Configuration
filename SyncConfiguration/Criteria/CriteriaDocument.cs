#nullable enable
using System.Globalization;

namespace SyncConfiguration.Criteria;

/// <summary>Owns a lossless text draft and an immutable visual expression tree.</summary>
public sealed class CriteriaDocument(CriteriaBuilderOptions options)
{
    /// <summary>Gets the consumer capabilities.</summary>
    public CriteriaBuilderOptions Options { get; } = options;
    /// <summary>Gets the exact text draft, including invalid user input.</summary>
    public string Text { get; private set; } = "";
    /// <summary>Gets the last successfully parsed visual tree.</summary>
    public CriteriaNode? Root { get; private set; }
    /// <summary>Gets the current validation failure, if any.</summary>
    public string? Error { get; private set; }

    /// <summary>Stores user text verbatim and attempts to parse it without overwriting a previous tree.</summary>
    public bool Read(string text)
    {
        Text = text;
        if (!Options.Adapter.TryRead(text, out var root, out var error) || root is null)
        { Error = error ?? "Enter a condition."; return false; }
        if (Validate(root) is { } problem) { Error = problem; return false; }
        Root = root; Error = null; return true;
    }

    /// <summary>Accepts a visual edit only when it is valid and serializable.</summary>
    public bool SetRoot(CriteriaNode root)
    {
        if (Validate(root) is { } problem) { Error = problem; return false; }
        try
        {
            var text = Options.Adapter.Write(root);
            if (!Options.Adapter.TryRead(text, out _, out var error)) { Error = error; return false; }
            Root = root; Text = text; Error = null; return true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or OverflowException)
        { Error = ex.Message; return false; }
    }

    /// <summary>Changes a group operation while preserving every existing condition.</summary>
    public static CriteriaGroup Regroup(CriteriaGroup group, CriteriaJunction junction) =>
        junction == CriteriaJunction.Not && group.Children.Count != 1
            ? new(junction, [group]) : new(junction, group.Children);

    /// <summary>Formats a readable expression for summaries and operand tokens.</summary>
    public string Describe(CriteriaNode node) => node switch
    {
        CriteriaCondition condition => $"{Describe(condition.Left)} {Options.Operators.FirstOrDefault(x => x.Id == condition.OperatorId && x.Type == condition.Left.ValueType)?.Label ?? condition.OperatorId} {Describe(condition.Right)}",
        CriteriaGroup group when group.Junction == CriteriaJunction.Not => $"not ({Describe(group.Children[0])})",
        CriteriaGroup group => string.Join(group.Junction == CriteriaJunction.All ? " and " : " or ", group.Children.Select(child => child is CriteriaGroup ? $"({Describe(child)})" : Describe(child))),
        _ => ""
    };

    /// <summary>Formats an operand using consumer-provided labels and functions.</summary>
    public string Describe(CriteriaOperand operand) => operand switch
    {
        CriteriaFieldOperand field => Options.Fields.FirstOrDefault(x => x.Path == field.Path)?.Label ?? field.Path,
        CriteriaLiteral literal when literal.ValueType == CriteriaValueType.DateTime && DateTimeOffset.TryParse(literal.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) => date.ToUniversalTime().ToString("dd MMM yyyy HH:mm:ss 'UTC'", CultureInfo.InvariantCulture),
        CriteriaLiteral literal => literal.Value,
        CriteriaFunction function => Options.Functions.FirstOrDefault(x => x.Id == function.FunctionId)?.Format(function.Arguments.Select(Describe).ToArray()) ?? function.FunctionId,
        _ => ""
    };

    /// <summary>Checks type safety and capabilities before calling the consumer validator.</summary>
    private string? Validate(CriteriaNode root) => CheckNode(root, 0) ?? Options.Validate?.Invoke(root);

    /// <summary>Checks groups and comparisons with bounded nesting.</summary>
    private string? CheckNode(CriteriaNode node, int depth)
    {
        if (depth > Options.MaximumDepth) return "This expression is nested too deeply.";
        if (node is CriteriaGroup group)
        {
            if (!Options.Junctions.Contains(group.Junction)) return "This grouping operation is unavailable.";
            if (group.Children.Count == 0 || (group.Junction == CriteriaJunction.Not && group.Children.Count != 1)) return "A group must contain conditions; Not requires exactly one child.";
            return group.Children.Select(child => CheckNode(child, depth + 1)).FirstOrDefault(error => error is not null);
        }
        if (node is not CriteriaCondition condition) return "Unknown condition.";
        if (condition.Left.ValueType != condition.Right.ValueType || !Options.Operators.Any(op => op.Id == condition.OperatorId && op.Type == condition.Left.ValueType)) return "The comparison and value types do not match.";
        if (condition.Left is CriteriaFieldOperand selected && condition.Right is CriteriaLiteral choice &&
            Options.Fields.FirstOrDefault(x => x.Path == selected.Path)?.Choices is { Count: > 0 } choices && !choices.Contains(choice.Value)) return "Choose a permitted field value.";
        return CheckOperand(condition.Left, depth + 1) ?? CheckOperand(condition.Right, depth + 1);
    }

    /// <summary>Checks field names, typed literals and function argument contracts.</summary>
    private string? CheckOperand(CriteriaOperand operand, int depth)
    {
        if (depth > Options.MaximumDepth) return "This expression is nested too deeply.";
        if (operand is CriteriaFieldOperand field)
        {
            var definition = Options.Fields.FirstOrDefault(x => x.Path == field.Path);
            if (string.IsNullOrWhiteSpace(field.Path)) return "Choose a field.";
            return definition is null ? Options.AllowCustomFields ? null : $"Field '{field.Path}' is unavailable."
                : definition.Type != field.ValueType ? "The field's type has changed." : null;
        }
        if (operand is CriteriaLiteral literal) return ValidateLiteral(literal);
        if (operand is not CriteriaFunction function) return "Unknown operand.";
        var descriptor = Options.Functions.FirstOrDefault(x => x.Id == function.FunctionId && x.ResultType == function.ValueType);
        if (descriptor is null || descriptor.Parameters.Count != function.Arguments.Count) return "This function or its arguments are unavailable.";
        for (var i = 0; i < function.Arguments.Count; i++)
        {
            var argument = function.Arguments[i]; var parameter = descriptor.Parameters[i];
            if (argument.ValueType != parameter.Type) return $"{parameter.Label} has the wrong type.";
            if (parameter.Choices is { Count: > 0 } && argument is CriteriaLiteral choice && !parameter.Choices.Contains(choice.Value)) return $"Choose a supported {parameter.Label.ToLowerInvariant()}.";
            if (CheckOperand(argument, depth + 1) is { } error) return error;
        }
        return null;
    }

    /// <summary>Validates culture-independent primitive values without evaluating a rule.</summary>
    public static string? ValidateLiteral(CriteriaLiteral literal) => literal.ValueType switch
    {
        CriteriaValueType.Number when !decimal.TryParse(literal.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _) => "Enter a valid number.",
        CriteriaValueType.Boolean when !bool.TryParse(literal.Value, out _) => "Choose true or false.",
        CriteriaValueType.DateTime when !DateTimeOffset.TryParse(literal.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
            !(literal.Value.EndsWith('Z') || (literal.Value.Length >= 6 && literal.Value[^3] == ':' && (literal.Value[^6] == '+' || literal.Value[^6] == '-'))) => "Choose a date and time with an explicit UTC offset.",
        _ => null
    };
}
