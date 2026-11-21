#nullable enable
using MongoDB.Sync.Filtering;
using SyncConfiguration.Controls.Criteria;
using SyncConfiguration.Criteria;
using SyncConfiguration.Models.AppDetail;
using SyncConfiguration.Services.Filtering;

namespace SyncConfiguration.Views.AppDetails;

/// <summary>Edits an isolated visibility policy draft using the shared criteria designer.</summary>
public sealed class FilterEditorPage : ContentPage
{
    private readonly EditCollectionModel collection;
    private readonly CriteriaBuilder builder;
    private readonly Switch enabled;
    private readonly Label validation = new() { LineBreakMode = LineBreakMode.WordWrap };
    private readonly Button apply = new() { Text = "Apply to collection" };

    /// <summary>Builds a designer with source fields and the authenticated current-user ObjectId operand.</summary>
    public FilterEditorPage(EditCollectionModel collection)
    {
        this.collection = collection;
        Title = $"Filter · {collection.CollectionName}";
        enabled = new Switch { IsToggled = !string.IsNullOrWhiteSpace(collection.Filter) };
        builder = new CriteriaBuilder { Options = MongoFilterCriteriaAdapter.CreateOptions(Fields(collection.Fields).ToArray()), Text = collection.Filter ?? "" };
        var cancel = new Button { Text = "Cancel" };
        cancel.Clicked += async (_, _) => await Navigation.PopModalAsync();
        apply.Clicked += ApplyClicked;
        builder.Changed += (_, _) => Validate();
        enabled.Toggled += (_, _) => Validate();
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 24, Spacing = 16, Children =
        {
            new Label { Text = "Record visibility", FontSize = 24, FontAttributes = FontAttributes.Bold },
            new Label { Text = "Only matching records reach a device. Use Current user (ObjectId) to compare an owner field with the signed-in user's ID. The server resolves the user; no fixed administrator ID is saved.", LineBreakMode = LineBreakMode.WordWrap },
            new HorizontalStackLayout { Spacing = 10, Children = { new Label { Text = "Enable filter" }, enabled } },
            builder, validation,
            new Label { Text = "Filtered collections are read-only to devices. Live changes refresh the authorized collection, including removals. Disabling the filter makes all records available again.", LineBreakMode = LineBreakMode.WordWrap },
            new Label { Text = "Apply updates this draft. Save changes on the application screen to persist it." },
            new HorizontalStackLayout { Spacing = 12, Children = { cancel, apply } }
        } } };
        Validate();
    }

    /// <summary>Flattens source schema paths; ownership fields do not need to be selected for synchronization.</summary>
    private static IEnumerable<CriteriaField> Fields(IEnumerable<EditFieldModel> fields, string prefix = "")
    {
        foreach (var field in fields)
        {
            var path = prefix + field.Name;
            var type = field.Type?.Type?.ToLowerInvariant() ?? "";
            yield return new CriteriaField(path, path, type.Contains("date") ? CriteriaValueType.DateTime :
                type.Contains("bool") ? CriteriaValueType.Boolean : type is "number" or "int" or "int32" or "int64" or "double" or "decimal" ? CriteriaValueType.Number : CriteriaValueType.String);
            foreach (var nested in Fields(field.Children, path + ".")) yield return nested;
        }
    }

    /// <summary>Prevents applying malformed expressions without losing the editor draft.</summary>
    private bool Validate()
    {
        builder.IsEnabled = enabled.IsToggled;
        try
        {
            if (enabled.IsToggled)
            {
                if (string.IsNullOrWhiteSpace(builder.Text)) throw new FormatException("Enter a filter condition.");
                SyncFilterRule.Validate(builder.Text);
            }
            validation.Text = enabled.IsToggled ? "Valid filter. Only matching records will synchronize." : "No filter: all records may synchronize.";
            apply.IsEnabled = true;
            return true;
        }
        catch (FormatException ex) { validation.Text = ex.Message; apply.IsEnabled = false; return false; }
    }

    /// <summary>Applies only a valid local draft and returns to the application configuration.</summary>
    private async void ApplyClicked(object? sender, EventArgs args)
    {
        if (!Validate()) return;
        collection.Filter = enabled.IsToggled ? builder.Text : null;
        await Navigation.PopModalAsync();
    }
}
