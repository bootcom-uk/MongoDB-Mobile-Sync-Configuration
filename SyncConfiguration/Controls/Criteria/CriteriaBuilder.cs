#nullable enable
using System.Globalization;
using Microsoft.Maui.Layouts;
using SyncConfiguration.Criteria;

namespace SyncConfiguration.Controls.Criteria;

/// <summary>A reusable sentence-style criteria editor configured by a consumer and text adapter.</summary>
public sealed class CriteriaBuilder : ContentView
{
    /// <summary>Identifies the consumer's fields, operations, functions and syntax adapter.</summary>
    public static readonly BindableProperty OptionsProperty = BindableProperty.Create(nameof(Options), typeof(CriteriaBuilderOptions), typeof(CriteriaBuilder), null,
        propertyChanged: (view, _, _) => ((CriteriaBuilder)view).Load());
    /// <summary>Identifies the two-way text draft, including incomplete user input.</summary>
    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(CriteriaBuilder), "", BindingMode.TwoWay,
        propertyChanged: (view, _, _) => ((CriteriaBuilder)view).Load());
    private readonly VerticalStackLayout tree = new() { Spacing = 8 };
    private readonly Editor textEditor = new() { AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 170, FontFamily = "Consolas" };
    private readonly Label status = new() { FontSize = 13, LineBreakMode = LineBreakMode.WordWrap };
    private readonly Button designerTab;
    private readonly Button textTab;
    private CriteriaDocument? document;
    private bool updating;
    private bool textMode;

    /// <summary>Gets or sets the consumer capabilities.</summary>
    public CriteriaBuilderOptions? Options { get => (CriteriaBuilderOptions?)GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }
    /// <summary>Gets or sets the exact textual draft.</summary>
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value ?? ""); }
    /// <summary>Gets the current validation error.</summary>
    public string? ValidationError => document?.Error;
    /// <summary>Gets the last valid visual tree.</summary>
    public CriteriaNode? Root => document?.Root;
    /// <summary>Gets a readable summary when the current draft is valid.</summary>
    public string Summary => document is { Error: null, Root: { } root } ? document.Describe(root) : "";
    /// <summary>Notifies the host after text, visual structure or validation changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Builds the generic editing surface, with no persistence or domain actions.</summary>
    public CriteriaBuilder()
    {
        designerTab = Token("Designer", () => { if (document?.Read(Text) == true) { textMode = false; Refresh(); } else RefreshStatus(); }, 0);
        textTab = Token("Text", () => { textMode = true; Refresh(); }, 0);
        textEditor.TextChanged += (_, _) =>
        {
            if (updating || document is null) return;
            document.Read(textEditor.Text ?? ""); Publish(); RefreshStatus();
        };
        var tabs = new HorizontalStackLayout { Spacing = 4, Children = { designerTab, textTab } };
        var content = new VerticalStackLayout { Spacing = 14, Children = { tabs, tree, textEditor, status } };
        Content = content;
    }

    /// <summary>Loads a new draft without rewriting valid text when switching modes.</summary>
    private void Load()
    {
        if (updating || Options is null) return;
        document = new CriteriaDocument(Options);
        if (string.IsNullOrWhiteSpace(Text))
        {
            document.SetRoot(Options.CreateCondition()); Publish();
        }
        else if (!document.Read(Text)) textMode = true;
        Refresh(); Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Updates the bindable draft without recursively reparsing its own change.</summary>
    private void Publish()
    {
        if (document is null) return;
        updating = true; Text = document.Text; textEditor.Text = Text; updating = false;
        OnPropertyChanged(nameof(Summary)); OnPropertyChanged(nameof(ValidationError)); OnPropertyChanged(nameof(Root));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Accepts a visual edit atomically and keeps rejected drafts intact.</summary>
    private void Commit(CriteriaNode root)
    {
        if (document?.SetRoot(root) == true) { Publish(); Refresh(); }
        else RefreshStatus();
    }

    /// <summary>Renders the selected mode and its active-tab state.</summary>
    private void Refresh()
    {
        tree.Clear(); tree.IsVisible = !textMode; textEditor.IsVisible = textMode;
        textTab.Text = Options?.Adapter.TextLabel ?? "Text";
        designerTab.SetAppThemeColor(BackgroundColorProperty, !textMode ? Color.FromArgb("#E5EEFA") : Colors.Transparent, !textMode ? Color.FromArgb("#30445D") : Colors.Transparent);
        textTab.SetAppThemeColor(BackgroundColorProperty, textMode ? Color.FromArgb("#E5EEFA") : Colors.Transparent, textMode ? Color.FromArgb("#30445D") : Colors.Transparent);
        designerTab.FontAttributes = !textMode ? FontAttributes.Bold : FontAttributes.None;
        textTab.FontAttributes = textMode ? FontAttributes.Bold : FontAttributes.None;
        updating = true; textEditor.Text = Text; updating = false;
        if (!textMode && Root is { } root)
        {
            var group = root as CriteriaGroup ?? new CriteriaGroup(CriteriaJunction.All, [root]);
            tree.Add(RenderGroup(group, Commit, 0));
        }
        RefreshStatus();
    }

    /// <summary>Shows errors locally without discarding the current textual draft.</summary>
    private void RefreshStatus()
    {
        status.Text = document?.Error is { } error ? error + (textMode ? " Your text is preserved." : "") : "";
        status.IsVisible = !string.IsNullOrEmpty(status.Text);
        status.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#A02C2C"), Color.FromArgb("#FFB5B5"));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Renders a compact group header and an indented criteria tree.</summary>
    private View RenderGroup(CriteriaGroup group, Action<CriteriaNode> replace, int depth)
    {
        var panel = new VerticalStackLayout { Spacing = 8 };
        var header = new FlexLayout { Wrap = FlexWrap.Wrap, AlignItems = FlexAlignItems.Center };
        header.Add(Token(group.Junction + " ▾", async () =>
        {
            var choice = await CriteriaDialog.ChooseAsync(Owner(), "Combine conditions", Options!.Junctions.Select(x => new JunctionChoice(x)).ToArray(), x => x.Value.ToString());
            if (choice is not null) replace(CriteriaDocument.Regroup(group, choice.Value));
        }, 1));
        header.Add(new Label { Text = group.Junction == CriteriaJunction.Not ? "the following condition" : "of these conditions", VerticalOptions = LayoutOptions.Center, FontSize = 14 });
        panel.Add(header);
        var children = new VerticalStackLayout { Spacing = 6, Padding = new Thickness(14, 2, 0, 2) };
        for (var index = 0; index < group.Children.Count; index++)
        {
            var position = index;
            /// <summary>Replaces one child while preserving sibling conditions.</summary>
            void ReplaceChild(CriteriaNode child) { var nodes = group.Children.ToArray(); nodes[position] = child; replace(group with { Children = nodes }); }
            var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 4 };
            row.Add(group.Children[index] is CriteriaGroup nested ? RenderGroup(nested, ReplaceChild, depth + 1) : RenderCondition((CriteriaCondition)group.Children[index], ReplaceChild));
            if (group.Children.Count > 1)
            {
                var remove = Token("×", () => replace(group with { Children = group.Children.Where((_, i) => i != position).ToArray() }), 0);
                SemanticProperties.SetDescription(remove, "Remove this condition or group"); ToolTipProperties.SetText(remove, "Remove");
                remove.VerticalOptions = LayoutOptions.Start; row.Add(remove, 1);
            }
            children.Add(row);
        }
        var branch = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(new GridLength(2)), new(GridLength.Star) } };
        var line = new BoxView(); line.SetAppThemeColor(BoxView.ColorProperty, Color.FromArgb("#D9E2EC"), Color.FromArgb("#3E5065"));
        branch.Add(line); branch.Add(children, 1); panel.Add(branch);
        if (group.Junction != CriteriaJunction.Not && group.Children.Count < 32)
        {
            var actions = new FlexLayout { Wrap = FlexWrap.Wrap };
            actions.Add(Token("+ Condition", () => replace(group with { Children = [.. group.Children, Options!.CreateCondition()] }), 0));
            if (depth + 1 < Options!.MaximumDepth && Options.Junctions.Contains(CriteriaJunction.All))
                actions.Add(Token("+ Group", () => replace(group with { Children = [.. group.Children, new CriteriaGroup(CriteriaJunction.All, [Options.CreateCondition()])] }), 0));
            panel.Add(actions);
        }
        return panel;
    }

    /// <summary>Renders a condition as three editable sentence fragments.</summary>
    private View RenderCondition(CriteriaCondition condition, Action<CriteriaNode> replace)
    {
        var row = new FlexLayout { Wrap = FlexWrap.Wrap, AlignItems = FlexAlignItems.Center };
        row.Add(Token(document!.Describe(condition.Left) + " ▾", async () =>
        {
            var value = await EditOperandAsync(condition.Left, true);
            if (value is null) return;
            var op = Options!.Operators.FirstOrDefault(x => x.Id == condition.OperatorId && x.Type == value.ValueType) ?? Options.Operators.First(x => x.Type == value.ValueType);
            replace(condition with { Left = value, OperatorId = op.Id, Right = condition.Right.ValueType == value.ValueType ? condition.Right : DefaultLiteral(value.ValueType, Choices(value)) });
        }, 2));
        var operation = Options!.Operators.FirstOrDefault(x => x.Id == condition.OperatorId && x.Type == condition.Left.ValueType);
        row.Add(Token((operation?.Label ?? condition.OperatorId) + " ▾", async () =>
        {
            var choice = await CriteriaDialog.ChooseAsync(Owner(), "Comparison", Options.Operators.Where(x => x.Type == condition.Left.ValueType).ToArray(), x => x.Label);
            if (choice is not null) replace(condition with { OperatorId = choice.Id });
        }, 1));
        row.Add(Token(document.Describe(condition.Right) + " ▾", async () =>
        {
            var value = await EditOperandAsync(condition.Right, false, Choices(condition.Left));
            if (value is not null) replace(condition with { Right = value });
        }, 3));
        return row;
    }

    /// <summary>Gets literal choices associated with a selected field.</summary>
    private IReadOnlyList<string>? Choices(CriteriaOperand operand) => operand is CriteriaFieldOperand field ? Options!.Fields.FirstOrDefault(x => x.Path == field.Path)?.Choices : null;

    /// <summary>Offers only operand kinds and functions supported by this consumer.</summary>
    private async Task<CriteriaOperand?> EditOperandAsync(CriteriaOperand current, bool anyFieldType = false, IReadOnlyList<string>? choices = null)
    {
        var options = new List<OperandChoice> { new("field", "Choose a field"), new("value", "Enter a value") };
        if (current is CriteriaFunction function && function.Arguments.Count > 0) options.Insert(0, new("edit", "Edit " + document!.Describe(function)));
        options.AddRange(Options!.Functions.Where(x => x.ResultType == current.ValueType).Select(x => new OperandChoice(x.Id, x.Label)));
        var selected = await CriteriaDialog.ChooseAsync(Owner(), "Choose a value", options, x => x.Label);
        if (selected is null) return null;
        if (selected.Id == "field") return await ChooseFieldAsync(current.ValueType, anyFieldType);
        if (selected.Id == "value") return await EditLiteralAsync(current as CriteriaLiteral ?? DefaultLiteral(current.ValueType, choices), choices);
        var candidate = selected.Id == "edit" ? (CriteriaFunction)current : Options.Functions.First(x => x.Id == selected.Id).Create();
        return candidate.Arguments.Count == 0 ? candidate : await EditFunctionAsync(candidate);
    }

    /// <summary>Searches typed field metadata and optionally accepts a manually entered path.</summary>
    private async Task<CriteriaOperand?> ChooseFieldAsync(CriteriaValueType type, bool anyType)
    {
        var fields = Options!.Fields.Where(x => (anyType || x.Type == type) && Options.Operators.Any(op => op.Type == x.Type)).ToList();
        if (Options.AllowCustomFields) fields.Add(new("", "Enter a field path…", type));
        var field = await CriteriaDialog.ChooseAsync(Owner(), "Choose a field", fields, x => x.Label);
        if (field is null) return null;
        if (field.Path.Length > 0) return new CriteriaFieldOperand(field.Path, field.Type);
        return await CriteriaDialog.ShowAsync<CriteriaOperand>(Owner(), "Field path", finish =>
        {
            var entry = new Entry { Placeholder = "Field or nested.field" };
            var use = Token("Use field", () => { if (!string.IsNullOrWhiteSpace(entry.Text)) finish(new CriteriaFieldOperand(entry.Text.Trim(), type)); }, 2);
            return new VerticalStackLayout { Spacing = 12, Children = { entry, use } };
        });
    }

    /// <summary>Edits a function's arguments as compact tokens in an isolated draft.</summary>
    private Task<CriteriaOperand?> EditFunctionAsync(CriteriaFunction function) => CriteriaDialog.ShowAsync<CriteriaOperand>(Owner(),
        Options!.Functions.First(x => x.Id == function.FunctionId).Label, finish =>
        {
            var definition = Options.Functions.First(x => x.Id == function.FunctionId);
            var arguments = function.Arguments.ToArray();
            var panel = new VerticalStackLayout { Spacing = 12 };
            for (var index = 0; index < arguments.Length; index++)
            {
                var position = index; var parameter = definition.Parameters[index];
                var token = Token(document!.Describe(arguments[index]) + " ▾", () => { }, 2);
                token.Clicked += async (_, _) =>
                {
                    var value = parameter.Type is CriteriaValueType.Number or CriteriaValueType.Choice
                        ? await EditLiteralAsync(arguments[position] as CriteriaLiteral ?? DefaultLiteral(parameter.Type, parameter.Choices), parameter.Choices)
                        : await EditOperandAsync(arguments[position], false, parameter.Choices);
                    if (value is not null) { arguments[position] = value; token.Text = document.Describe(value) + " ▾"; }
                };
                panel.Add(new Label { Text = parameter.Label, FontSize = 13 }); panel.Add(token);
            }
            panel.Add(Token("Use function", () => finish(function with { Arguments = arguments }), 2));
            return panel;
        });

    /// <summary>Edits primitive values using their native input controls.</summary>
    private Task<CriteriaOperand?> EditLiteralAsync(CriteriaLiteral literal, IReadOnlyList<string>? choices) =>
        CriteriaDialog.ShowAsync<CriteriaOperand>(Owner(), "Set value", finish =>
        {
            var panel = new VerticalStackLayout { Spacing = 12 };
            Func<string> read;
            if (choices is { Count: > 0 })
            {
                var picker = new Picker { Title = "Choose a value", ItemsSource = choices.ToArray(), SelectedItem = literal.Value };
                panel.Add(picker); read = () => picker.SelectedItem?.ToString() ?? "";
            }
            else if (literal.ValueType == CriteriaValueType.DateTime)
            {
                var value = DateTimeOffset.TryParse(literal.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed.UtcDateTime : DateTime.UtcNow;
                var date = new DatePicker { Date = value.Date, Format = "dd MMM yyyy" };
                var time = new TimePicker { Time = value.TimeOfDay, Format = "HH:mm:ss" };
                panel.Add(new Label { Text = "Date and time (UTC)" }); panel.Add(date); panel.Add(time);
                read = () => DateTime.SpecifyKind((date.Date is DateTime day ? day : value).Date + (time.Time is TimeSpan clock ? clock : value.TimeOfDay), DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
            }
            else if (literal.ValueType == CriteriaValueType.Boolean)
            {
                var toggle = new Switch { IsToggled = bool.TryParse(literal.Value, out var value) && value };
                panel.Add(new Label { Text = "True when enabled; false when disabled" }); panel.Add(toggle); read = () => toggle.IsToggled ? "true" : "false";
            }
            else
            {
                var entry = new Entry { Text = literal.Value, Keyboard = literal.ValueType == CriteriaValueType.Number ? Keyboard.Numeric : Keyboard.Default };
                panel.Add(entry); read = () => entry.Text ?? "";
            }
            var error = new Label { TextColor = Colors.IndianRed }; panel.Add(error);
            panel.Add(Token("Use value", () =>
            {
                var value = new CriteriaLiteral(read(), literal.ValueType);
                error.Text = CriteriaDocument.ValidateLiteral(value) ?? (choices is { Count: > 0 } && !choices.Contains(value.Value) ? "Choose a value." : "");
                if (string.IsNullOrEmpty(error.Text)) finish(value);
            }, 2));
            return panel;
        });

    /// <summary>Creates an initial typed literal when a user explicitly changes a field's type.</summary>
    private static CriteriaLiteral DefaultLiteral(CriteriaValueType type, IReadOnlyList<string>? choices) => new(type switch
    { CriteriaValueType.DateTime => DateTimeOffset.UtcNow.ToString("O"), CriteriaValueType.Number => "0", CriteriaValueType.Boolean => "true", CriteriaValueType.Choice => choices?.FirstOrDefault() ?? "", _ => "" }, type);

    /// <summary>Finds the hosting page for contextual editors.</summary>
    private Page Owner()
    {
        Element? element = this;
        while (element is not null && element is not Page) element = element.Parent;
        return element as Page ?? throw new InvalidOperationException("Attach the criteria builder to a page before editing.");
    }

    /// <summary>Creates a compact, focusable token with theme-aware semantic colours.</summary>
    private static Button Token(string text, Action clicked, int tone)
    {
        var button = new Button { Text = text, FontSize = 14, BackgroundColor = Colors.Transparent, BorderWidth = 0,
            CornerRadius = 6, Padding = new Thickness(8, 5), Margin = new Thickness(0, 2), MinimumHeightRequest = 36, HorizontalOptions = LayoutOptions.Start };
        button.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb(tone switch { 1 => "#7752A3", 2 => "#235E9C", 3 => "#26704A", _ => "#506174" }),
            Color.FromArgb(tone switch { 1 => "#D2B4F2", 2 => "#9BC5FF", 3 => "#A3DDB5", _ => "#C3CFDD" }));
        button.Clicked += (_, _) => clicked(); return button;
    }

    /// <summary>Preserves junction identity in chooser results.</summary>
    private sealed record JunctionChoice(CriteriaJunction Value);
    /// <summary>Preserves operand action identity separately from its caption.</summary>
    private sealed record OperandChoice(string Id, string Label);
}

