#nullable enable
using SyncConfiguration.Controls.Criteria;
using SyncConfiguration.Criteria;
using SyncConfiguration.Models.AppDetail;
using SyncConfiguration.Services.Expiry;

namespace SyncConfiguration.Views.AppDetails;

/// <summary>Hosts the reusable criteria builder with expiry-specific validation and preview actions.</summary>
public sealed class ExpiryEditorPage : ContentPage
{
    private readonly EditCollectionModel collection;
    private readonly CriteriaBuilder builder;
    private readonly Switch enabled;
    private readonly Label summary = new() { FontSize = 14, LineBreakMode = LineBreakMode.WordWrap };
    private readonly Label validation = new() { FontSize = 13, LineBreakMode = LineBreakMode.WordWrap };
    private readonly Label preview = new() { LineBreakMode = LineBreakMode.WordWrap };
    private readonly Editor sample = new() { Text = "{}", MinimumHeightRequest = 100, AutoSize = EditorAutoSizeOption.TextChanges, FontFamily = "Consolas" };
    private readonly Switch currentTime = new() { IsToggled = true };
    private readonly DatePicker testDate = new() { Date = DateTime.UtcNow.Date, Format = "dd MMM yyyy" };
    private readonly TimePicker testTime = new() { Time = DateTime.UtcNow.TimeOfDay, Format = "HH:mm:ss" };
    private readonly Button apply;
    private readonly VerticalStackLayout tests;
    private readonly Label disabled = new() { Text = "No expiry rule is active. Enable expiry to choose which records leave the cache.", Margin = new Thickness(0, 12), LineBreakMode = LineBreakMode.WordWrap };

    /// <summary>Creates an isolated collection draft; only Apply changes the host's mapping.</summary>
    public ExpiryEditorPage(EditCollectionModel collection)
    {
        this.collection = collection;
        Title = $"Expiry · {collection.CollectionName}";
        enabled = new Switch { IsToggled = !string.IsNullOrWhiteSpace(collection.Expiry) };
        builder = new CriteriaBuilder { Options = MongoExpiryCriteriaAdapter.CreateOptions(DateFields(collection.Fields).ToArray()), Text = collection.Expiry ?? "" };
        apply = new Button { Text = "Apply to collection", MinimumWidthRequest = 160 };
        apply.Clicked += ApplyClicked;
        var cancel = new Button { Text = "Cancel", BackgroundColor = Colors.Transparent };
        cancel.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#25466B"), Color.FromArgb("#C3D6EF"));
        cancel.Clicked += async (_, _) => await Navigation.PopModalAsync();

        var heading = new VerticalStackLayout { Spacing = 5, Children =
        {
            new Label { Text = "Cache expiry", FontSize = 24, FontAttributes = FontAttributes.Bold },
            new Label { Text = "Choose when cached records expire. Source records are kept.", FontSize = 14, LineBreakMode = LineBreakMode.WordWrap }
        } };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 20 };
        header.Add(heading);
        header.Add(new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Start, Children = { new Label { Text = "Enable expiry", VerticalOptions = LayoutOptions.Center }, enabled } }, 1);

        var rule = new VerticalStackLayout { Spacing = 12, Children = { disabled, builder, summary, validation } };
        var card = new Border { Content = rule, Padding = 18, StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 } };
        card.SetAppThemeColor(BackgroundColorProperty, Colors.White, Color.FromArgb("#17212D"));
        var testClock = new HorizontalStackLayout { Spacing = 12, IsVisible = false, Children = { testDate, testTime } };
        currentTime.Toggled += (_, _) => testClock.IsVisible = !currentTime.IsToggled;
        var run = new Button { Text = "Test rule", HorizontalOptions = LayoutOptions.Start };
        run.Clicked += (_, _) => Test();
        tests = new VerticalStackLayout { IsVisible = false, Spacing = 12, Children =
        {
            new Label { Text = "Paste a sample document. Testing does not change stored data.", FontSize = 13 }, sample,
            new HorizontalStackLayout { Spacing = 8, Children = { new Label { Text = "Use current UTC time", VerticalOptions = LayoutOptions.Center }, currentTime } },
            testClock, run, preview
        } };
        var testToggle = new Button { Text = "▸ Test this rule", HorizontalOptions = LayoutOptions.Start, BackgroundColor = Colors.Transparent };
        testToggle.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#25466B"), Color.FromArgb("#C3D6EF"));
        testToggle.Clicked += (_, _) => { tests.IsVisible = !tests.IsVisible; testToggle.Text = tests.IsVisible ? "▾ Test this rule" : "▸ Test this rule"; };
        var body = new VerticalStackLayout { Spacing = 14, Children = { card, testToggle, tests } };
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 12 };
        actions.Add(cancel); cancel.HorizontalOptions = LayoutOptions.Start; actions.Add(apply, 1);
        var footer = new VerticalStackLayout { Spacing = 8, Children =
        {
            new Label { Text = "Apply updates this draft. Save changes on the application screen to persist it.", FontSize = 12, LineBreakMode = LineBreakMode.WordWrap }, actions
        } };
        var layout = new Grid { Padding = 24, RowSpacing = 20, MaximumWidthRequest = 1100, HorizontalOptions = LayoutOptions.Fill,
            RowDefinitions = new RowDefinitionCollection { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        layout.Add(header); layout.Add(new ScrollView { Content = body }, 0, 1); layout.Add(footer, 0, 2);
        Content = layout;
        builder.Changed += (_, _) => Validate();
        enabled.Toggled += (_, _) => { if (!enabled.IsToggled) tests.IsVisible = false; testToggle.IsEnabled = enabled.IsToggled; Validate(); };
        testToggle.IsEnabled = enabled.IsToggled;
        Validate();
    }

    /// <summary>Flattens schema date fields into control metadata without coupling the control to sync models.</summary>
    private static IEnumerable<CriteriaField> DateFields(IEnumerable<EditFieldModel> fields, string prefix = "")
    {
        foreach (var field in fields)
        {
            var path = prefix + field.Name;
            if (field.Type?.Type?.Contains("date", StringComparison.OrdinalIgnoreCase) == true) yield return new(path, path, CriteriaValueType.DateTime);
            foreach (var nested in DateFields(field.Children, path + ".")) yield return nested;
        }
    }

    /// <summary>Updates expiry-specific feedback and the host's Apply action.</summary>
    private string? Validate()
    {
        builder.IsVisible = enabled.IsToggled; disabled.IsVisible = !enabled.IsToggled;
        summary.IsVisible = enabled.IsToggled && builder.ValidationError is null;
        summary.Text = "Expire when " + builder.Summary + ".";
        var error = enabled.IsToggled ? builder.ValidationError ?? new ExpiryEditorDocument { Text = builder.Text }
            .Validate(collection.Fields.Where(x => x.IsSelected).Select(x => x.Name)) : null;
        validation.Text = error ?? ""; validation.IsVisible = error is not null && builder.ValidationError is null;
        validation.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#A02C2C"), Color.FromArgb("#FFB5B5"));
        apply.IsEnabled = error is null;
        return error;
    }

    /// <summary>Commits only the current valid draft to the collection editor.</summary>
    private async void ApplyClicked(object? sender, EventArgs args)
    {
        if (Validate() is not null) return;
        collection.Expiry = enabled.IsToggled ? builder.Text : null;
        await Navigation.PopModalAsync();
    }

    /// <summary>Runs the existing expiry evaluator against an isolated sample and chosen clock.</summary>
    private void Test()
    {
        var now = DateTimeOffset.UtcNow;
        if (!currentTime.IsToggled)
        {
            var day = testDate.Date is DateTime date ? date : DateTime.UtcNow;
            var clock = testTime.Time is TimeSpan time ? time : TimeSpan.Zero;
            now = new DateTimeOffset(DateTime.SpecifyKind(day.Date + clock, DateTimeKind.Utc));
        }
        preview.Text = enabled.IsToggled ? new ExpiryEditorDocument { Text = builder.Text }.Preview(sample.Text ?? "{}", now) : "Expiry is disabled.";
    }
}

