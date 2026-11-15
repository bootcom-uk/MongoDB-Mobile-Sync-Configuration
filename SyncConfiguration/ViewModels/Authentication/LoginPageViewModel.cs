using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using SyncConfiguration.Models.Config;
using SyncConfiguration.Services;
using System.Collections.ObjectModel;

namespace SyncConfiguration.ViewModels.Authentication
{
    public partial class LoginPageViewModel : ViewModelBase
    {

        [ObservableProperty]
        string emailAddress;

        [ObservableProperty]
        ObservableCollection<AppURLInfo> appUrlDataSource;

        [ObservableProperty]
        AppURLInfo selectedAppUrl;

        private readonly IConfiguration _configuration;

        private readonly AuthenticationService _authenticationService;

        public LoginPageViewModel(InternalSettingsService internalSettingsService, INavigationService navigationService, AuthenticationService authenticationService, IConfiguration configuration) : base(internalSettingsService, navigationService)
        {
            _authenticationService = authenticationService;
            _configuration = configuration;
        }

        [RelayCommand]
        async Task Login()
        {
            await InternalSettingsService.SetSetting<string>(InternalSettingsService.EmailKey, EmailAddress);
            await NavigationService.NavigateAsync("AuthenticateEmailPage", new NavigationParameters()
            {
                { "appUrl", SelectedAppUrl }
            });
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
        }



        public async override void OnNavigatedTo(INavigationParameters parameters)
        {
            var appSettings = _configuration.Get<AppSettings>();

            AppUrlDataSource = new ObservableCollection<AppURLInfo>(appSettings.BaseAppUrl.ToList());

            SelectedAppUrl = AppUrlDataSource.FirstOrDefault();

            EmailAddress = await InternalSettingsService.GetSetting<string>(InternalSettingsService.EmailKey);

            // await InternalSettingsService.SetSetting<string>(InternalSettingsService.UserTokenKey, null);
            // await InternalSettingsService.SetSetting<string>(InternalSettingsService.RefreshTokenKey, null);

            var userToken = await InternalSettingsService.GetSetting<string>(InternalSettingsService.UserTokenKey);

            if (!string.IsNullOrWhiteSpace(userToken))
            {
                var tokenValid = await _authenticationService.VerifyTokenValidity(userToken);

                if (tokenValid)
                {
                    await NavigationService.NavigateAsync("MainPage");
                    return;
                }

                await InternalSettingsService.SetSetting<string>(InternalSettingsService.UserTokenKey, null);
                await InternalSettingsService.SetSetting<string>(InternalSettingsService.RefreshTokenKey, null);
                
            }

        }

    }
}
