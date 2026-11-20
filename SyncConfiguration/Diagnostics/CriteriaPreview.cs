#if DEBUG
using CommunityToolkit.Maui;
using SyncConfiguration.Models.AppDetail;
using SyncConfiguration.Views.AppDetails;

namespace SyncConfiguration.Diagnostics;

/// <summary>Starts an offline UI harness without authentication, network services or persistence.</summary>
internal static class CriteriaPreview
{
    /// <summary>Creates a preview-only application for visual acceptance checks.</summary>
    public static MauiApp CreateApp() => MauiApp.CreateBuilder().UseMauiApp<PreviewApp>().UseMauiCommunityToolkit()
        .ConfigureFonts(fonts => fonts.AddFont("ARLRDBD.TTF", "ArialRoundedMtBold")).Build();

    /// <summary>Uses the real application styles with an isolated synthetic collection.</summary>
    private sealed class PreviewApp : App
    {
        /// <summary>Creates a dark preview matching the supported desktop theme.</summary>
        public PreviewApp() { UserAppTheme = AppTheme.Dark; }

        /// <summary>Creates a preview window without starting normal application services.</summary>
        protected override Window CreateWindow(IActivationState? activationState)
        {
            var open = new Button { Text = "Open expiry designer", HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            var landing = new ContentPage { Title = "Criteria Builder Preview", Content = open };
            open.Clicked += async (_, _) =>
            {
                try
                {
                var collection = new EditCollectionModel { CollectionName = "Supermarket", DatabaseName = "Preview" };
                foreach (var name in new[] { "DateAdded", "LastUpdated", "OfferEnds" })
                    collection.Fields.Add(new EditFieldModel { Name = name, Type = new MongoDB.Sync.Models.FieldType { Type = "Date" }, IsSelected = true });
                collection.Expiry = """{"$lte":[{"$dateAdd":{"startDate":"$DateAdded","unit":"day","amount":30}},"$$NOW"]}""";
                await landing.Navigation.PushModalAsync(new NavigationPage(new ExpiryEditorPage(collection)), false);
                }
                catch (Exception error) { landing.Content = new ScrollView { Content = new Label { Text = error.ToString() } }; }
            };
            return new Window(new NavigationPage(landing)) { Title = "Criteria Builder Preview", Width = 1100, Height = 760 };
        }
    }
}
#endif


