#nullable enable
namespace SyncConfiguration.Criteria;

/// <summary>Defines the scalar kinds understood by the visual editor.</summary>
public enum CriteriaValueType { DateTime, String, Number, Boolean, Choice }

/// <summary>Defines how a group combines its children.</summary>
public enum CriteriaJunction { All, Any, Not }

/// <summary>Represents a Boolean expression independently of its persisted syntax.</summary>
public abstract record CriteriaNode;

/// <summary>Combines Boolean children.</summary>
/// <param name="Junction">The Boolean operation.</param>
/// <param name="Children">The child expressions, in display order.</param>
public sealed record CriteriaGroup(CriteriaJunction Junction, IReadOnlyList<CriteriaNode> Children) : CriteriaNode;

/// <summary>Compares two typed operands.</summary>
/// <param name="OperatorId">The consumer-defined comparison identifier.</param>
/// <param name="Left">The left operand.</param>
/// <param name="Right">The right operand.</param>
public sealed record CriteriaCondition(string OperatorId, CriteriaOperand Left, CriteriaOperand Right) : CriteriaNode;

/// <summary>Represents a typed field, literal or function result.</summary>
/// <param name="ValueType">The value's scalar kind.</param>
public abstract record CriteriaOperand(CriteriaValueType ValueType);

/// <summary>References a consumer-defined field.</summary>
/// <param name="Path">The stable field identifier or path.</param>
/// <param name="Type">The field's scalar kind.</param>
public sealed record CriteriaFieldOperand(string Path, CriteriaValueType Type) : CriteriaOperand(Type);

/// <summary>Contains an invariant scalar value; dates use ISO 8601 with an offset.</summary>
/// <param name="Value">The invariant scalar representation.</param>
/// <param name="Type">The literal's scalar kind.</param>
public sealed record CriteriaLiteral(string Value, CriteriaValueType Type) : CriteriaOperand(Type);

/// <summary>Calls a consumer-defined function without embedding executable code.</summary>
/// <param name="FunctionId">The stable function identifier.</param>
/// <param name="Arguments">The typed argument values.</param>
/// <param name="Type">The result's scalar kind.</param>
public sealed record CriteriaFunction(string FunctionId, IReadOnlyList<CriteriaOperand> Arguments, CriteriaValueType Type) : CriteriaOperand(Type);

/// <summary>Describes an available field.</summary>
/// <param name="Path">The stable field path.</param>
/// <param name="Label">The user-facing name.</param>
/// <param name="Type">The value kind.</param>
/// <param name="Choices">Optional permitted literal choices.</param>
public sealed record CriteriaField(string Path, string Label, CriteriaValueType Type, IReadOnlyList<string>? Choices = null);

/// <summary>Describes a comparison permitted by a consumer.</summary>
/// <param name="Id">The stable operator identifier.</param>
/// <param name="Label">The phrase shown between operands.</param>
/// <param name="Type">The accepted operand kind.</param>
public sealed record CriteriaOperator(string Id, string Label, CriteriaValueType Type);

/// <summary>Describes one function argument.</summary>
/// <param name="Label">The argument editor label.</param>
/// <param name="Type">The required value kind.</param>
/// <param name="Choices">Optional permitted literal choices.</param>
public sealed record CriteriaParameter(string Label, CriteriaValueType Type, IReadOnlyList<string>? Choices = null);

/// <summary>Describes a function and its visual presentation.</summary>
/// <param name="Id">The stable function identifier.</param>
/// <param name="Label">The function chooser label.</param>
/// <param name="ResultType">The result kind.</param>
/// <param name="Parameters">The argument schema.</param>
/// <param name="Create">Creates a fresh default function.</param>
/// <param name="Format">Formats already-readable argument labels.</param>
public sealed record CriteriaFunctionDefinition(string Id, string Label, CriteriaValueType ResultType,
    IReadOnlyList<CriteriaParameter> Parameters, Func<CriteriaFunction> Create, Func<IReadOnlyList<string>, string> Format);

/// <summary>Translates a consumer's text syntax without coupling the control to a database.</summary>
public interface ICriteriaTextAdapter
{
    /// <summary>Gets the label of the textual editing mode.</summary>
    string TextLabel { get; }
    /// <summary>Parses a supported expression; failures must not alter the supplied draft.</summary>
    bool TryRead(string text, out CriteriaNode? root, out string? error);
    /// <summary>Serializes a supported visual expression.</summary>
    string Write(CriteriaNode root);
}

/// <summary>Supplies the editor's capabilities and consumer-specific validation.</summary>
public sealed class CriteriaBuilderOptions
{
    /// <summary>Gets the available fields.</summary>
    public IReadOnlyList<CriteriaField> Fields { get; init; } = [];
    /// <summary>Gets the available typed comparisons.</summary>
    public IReadOnlyList<CriteriaOperator> Operators { get; init; } = [];
    /// <summary>Gets the available functions.</summary>
    public IReadOnlyList<CriteriaFunctionDefinition> Functions { get; init; } = [];
    /// <summary>Gets the allowed grouping operations.</summary>
    public IReadOnlyList<CriteriaJunction> Junctions { get; init; } = [CriteriaJunction.All, CriteriaJunction.Any, CriteriaJunction.Not];
    /// <summary>Gets the persistence-format adapter.</summary>
    public required ICriteriaTextAdapter Adapter { get; init; }
    /// <summary>Gets a factory for a fresh condition.</summary>
    public required Func<CriteriaCondition> CreateCondition { get; init; }
    /// <summary>Gets additional validation supplied by the consumer.</summary>
    public Func<CriteriaNode, string?>? Validate { get; init; }
    /// <summary>Gets whether manually entered field paths are allowed.</summary>
    public bool AllowCustomFields { get; init; }
    /// <summary>Gets the maximum visual tree depth.</summary>
    public int MaximumDepth { get; init; } = 12;
}
