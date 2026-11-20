using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Messaging;
using Maui.Android.InAppUpdates;
using Microsoft.Extensions.Configuration;
using Sentry.Protocol;
using Services;
using SyncConfiguration.Models.Config;
using SyncConfiguration.Services;
using SyncConfiguration.ViewModels;
using SyncConfiguration.ViewModels.AppDetails;
using SyncConfiguration.ViewModels.Authentication;
using SyncConfiguration.Views;
using SyncConfiguration.Views.AppDetails;
using SyncConfiguration.Views.Authentication;
using Syncfusion.Maui.Core.Hosting;
using System.Reflection;

namespace SyncConfiguration;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
#if DEBUG
        if (Environment.GetEnvironmentVariable("SYNC_CRITERIA_PREVIEW") == "1")
            return Diagnostics.CriteriaPreview.CreateApp();
#endif
        var builder = MauiApp.CreateBuilder();

        var assembly = Assembly.GetExecutingAssembly();
        var stream = null as Stream;

#if DEBUG
        stream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.appsettings.development.json");

#else
            stream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.appsettings.json");
#endif

        builder.Configuration.AddJsonStream(stream);

        var appSettings = builder.Configuration.Get<AppSettings>();

        // Register before configuring controls or loading application XAML resources.
        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(appSettings.Syncfusion.LicenceKey);

        builder
            .UseMauiApp<App>()
            .UseAndroidInAppUpdates()

            // Add this section anywhere on the builder:
            .UseSentry(options => {
                // The DSN is the only required setting.
                options.Dsn = appSettings.Sentry.Dsn;
            })

            .UsePrism(prism =>
            {
                prism.RegisterTypes(containerRegistry =>
                {
                    
                    containerRegistry.RegisterSingleton<IMessenger>(() => WeakReferenceMessenger.Default);

                    containerRegistry.RegisterSingleton<AppDetailService>();

                    containerRegistry.RegisterScoped<AuthenticationService>();

                    containerRegistry.RegisterScoped<HttpService>();

                    containerRegistry.RegisterInstance<AppSettings>(appSettings);

                    containerRegistry
                    .RegisterScoped<InternalSettingsService>();

                    containerRegistry
                    .RegisterForNavigation<NavigationPage>();

                    containerRegistry
                    .RegisterForNavigation<LoginPage, LoginPageViewModel>()
                    .RegisterInstance(SemanticScreenReader.Default);

                    containerRegistry
                    .RegisterForNavigation<AuthenticateEmailPage, AuthenticateEmailPageViewModel>()
                    .RegisterInstance(SemanticScreenReader.Default);

                    containerRegistry
                    .RegisterForNavigation<MainPage, MainPageViewModel>()
                    .RegisterInstance(SemanticScreenReader.Default);

                    containerRegistry
                    .RegisterForNavigation<ConfigureAppPage, ConfigureAppPageViewModel>()
                    .RegisterInstance(SemanticScreenReader.Default);

                    containerRegistry
                    .RegisterForNavigation<SaveCollectionPage, SaveCollectionPageViewModel>()
                    .RegisterInstance(SemanticScreenReader.Default);

                    containerRegistry.RegisterForNavigation<GenerateModelsPage, GenerateModelsPageViewModel>();


                });
                prism.CreateWindow(async (container, navigationService) =>
                {
                    
                    // Take the user to a page where they can authenticate themselves
                    await navigationService.NavigateAsync("LoginPage");
                });
            })
           .ConfigureFonts(fonts =>
           {
               fonts.AddFont("ARLRDBD.TTF", "ArialRoundedMtBold");
           })
            .ConfigureSyncfusionCore()
            .UseMauiCommunityToolkit();

        builder.Services.AddHttpClient();

        return builder.Build();
    }
}
