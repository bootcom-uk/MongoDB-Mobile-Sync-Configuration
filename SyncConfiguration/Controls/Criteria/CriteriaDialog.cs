#nullable enable
namespace SyncConfiguration.Controls.Criteria;

/// <summary>Hosts a small, keyboard-accessible editor without owning consumer persistence.</summary>
internal static class CriteriaDialog
{
    /// <summary>Displays an isolated editor draft and returns null when dismissed.</summary>
    public static async Task<T?> ShowAsync<T>(Page owner, string title, Func<Action<T>, View> build) where T : class
    {
        var completion = new TaskCompletionSource<T?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = new DialogPage { BackgroundColor = Color.FromArgb("#99000000") };
        var closing = false;
        /// <summary>Completes or cancels the isolated dialog draft.</summary>
        async void Finish(T? result)
        {
            if (closing) return;
            closing = true;
            await page.Navigation.PopModalAsync(false);
            completion.TrySetResult(result);
        }
        var close = new Button { Text = "Cancel", HorizontalOptions = LayoutOptions.End, BackgroundColor = Colors.Transparent };
        close.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#25466B"), Color.FromArgb("#C3D6EF"));
        close.Clicked += (_, _) => Finish(null);
        var scroll = new ScrollView { Content = build(value => Finish(value)), MaximumHeightRequest = 400 };
        var panel = new VerticalStackLayout { Padding = 24, Spacing = 16 };
        panel.Add(new Label { Text = title, FontSize = 20, FontAttributes = FontAttributes.Bold });
        panel.Add(scroll); panel.Add(close);
        var card = new Border { Content = panel, StrokeThickness = 1, Padding = 0, Margin = 24,
            WidthRequest = 512, MaximumWidthRequest = 560, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 } };
        card.SetAppThemeColor(VisualElement.BackgroundColorProperty, Colors.White, Color.FromArgb("#202B38"));
        page.Content = new Grid { Children = { card } };
        page.SizeChanged += (_, _) => { scroll.MaximumHeightRequest = Math.Max(100, page.Height - 200); card.WidthRequest = Math.Min(560, Math.Max(200, page.Width - 48)); };
        page.Cancel = () => Finish(null);
        page.Disappearing += (_, _) => page.Dispatcher.Dispatch(() =>
        {
            if (!closing && !owner.Navigation.ModalStack.Contains(page)) completion.TrySetResult(null);
        });
        await owner.Navigation.PushModalAsync(page, false);
        return await completion.Task;
    }

    /// <summary>Routes keyboard/back dismissal through the same cancellation path as the button.</summary>
    private sealed class DialogPage : ContentPage
    {
        /// <summary>Gets or sets the dialog cancellation action.</summary>
        public Action? Cancel { get; set; }
        /// <summary>Cancels the isolated draft when the platform back action is used.</summary>
        protected override bool OnBackButtonPressed() { Cancel?.Invoke(); return true; }
    }

    /// <summary>Displays a searchable list with stable item identities rather than matching display strings.</summary>
    public static Task<T?> ChooseAsync<T>(Page owner, string title, IReadOnlyList<T> items, Func<T, string> label) where T : class =>
        ShowAsync<T>(owner, title, finish =>
        {
            var rows = new VerticalStackLayout { Spacing = 4 };
            var search = new SearchBar { Placeholder = "Search…" };
            /// <summary>Refreshes choices for the search text.</summary>
            void Filter()
            {
                rows.Clear();
                foreach (var item in items.Where(item => label(item).Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase)))
                {
                    var button = new Button { Text = label(item), HorizontalOptions = LayoutOptions.Fill, BackgroundColor = Colors.Transparent };
                    button.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#25466B"), Color.FromArgb("#C3D6EF"));
                    button.Clicked += (_, _) => finish(item); rows.Add(button);
                }
                if (rows.Count == 0) rows.Add(new Label { Text = "No matching items.", Padding = 8 });
            }
            search.TextChanged += (_, _) => Filter(); Filter();
            return new VerticalStackLayout { Spacing = 8, Children = { search, rows } };
        });
}

