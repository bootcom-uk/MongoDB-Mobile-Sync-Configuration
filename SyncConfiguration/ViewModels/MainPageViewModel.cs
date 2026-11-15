using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using SyncConfiguration.Models.AppDetail;
using SyncConfiguration.Models.Config;
using SyncConfiguration.Services;
using SyncConfiguration.Views.AppDetails;

namespace SyncConfiguration.ViewModels;

public partial class MainPageViewModel : ViewModelBase
{

    [ObservableProperty]
    private AppDetailService appDetailService;

    private readonly IConfiguration _configuration;

    public MainPageViewModel(InternalSettingsService internalSettingsService, INavigationService navigationService, AppDetailService appDetailService, IConfiguration configuration) : base(internalSettingsService, navigationService)
    {
        AppDetailService = appDetailService;
        _configuration = configuration;
    }


    public override async void OnNavigatedTo(INavigationParameters parameters)
    {
        var appSettings = _configuration.Get<AppSettings>();

        var appUrl = await InternalSettingsService.GetSetting<AppURLInfo>(InternalSettingsService.AppURLInfo);
        await AppDetailService.CollectApps(appUrl.Url);
    }


    [RelayCommand]
    async Task ModifyApp(AppDetail selectedApp)
    {

        if(selectedApp is null)
        {
            await NavigationService.NavigateAsync(nameof(ConfigureAppPage));
            return;
        }   

        await NavigationService.NavigateAsync(nameof(ConfigureAppPage), new NavigationParameters()
        {
            { "appId", selectedApp.Id }
        });
    }
}
