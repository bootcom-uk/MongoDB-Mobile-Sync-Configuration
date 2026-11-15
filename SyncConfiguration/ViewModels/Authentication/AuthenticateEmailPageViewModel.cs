using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using SyncConfiguration.Services;
using Services;
using SyncConfiguration.Models.Config;

namespace SyncConfiguration.ViewModels.Authentication
{
    public partial class AuthenticateEmailPageViewModel : ViewModelBase
    {

        internal readonly HttpService _httpService;

        [ObservableProperty]
        bool openPopup;

        [ObservableProperty]
        bool loaderImagePlaying;

        [ObservableProperty]
        string quickAccessCode;

        public AuthenticateEmailPageViewModel(InternalSettingsService internalSettingsService, INavigationService navigationService, HttpService httpService) : base(internalSettingsService, navigationService)
        {
            _httpService = httpService;
        }

        [RelayCommand]
        async Task CompleteSignIn()
        {


            PopupHeight = 50;
            PopupWidth = 50;

            IsProcessing = true;
            ProcessingMessage = "Completing Login";

            var builder = _httpService.CreateBuilder(new Uri(Endpoints.ValidateAccessCodeUrl), HttpMethod.Post);

            var response = await builder.WithFormContent(new()
            {
                { "accessCode", QuickAccessCode },
                { "deviceId", (await InternalSettingsService.GetSetting<Guid>(InternalSettingsService.DeviceIdKey)).ToString() }
            })
                .SendAsync<Dictionary<string, string>>();

            if(response is null)
            {
                IsProcessing = false;
                return;
            }


            await InternalSettingsService.SetSetting<string>(InternalSettingsService.UserTokenKey, response.Result["JwtToken"]);
            await InternalSettingsService.SetSetting<string>(InternalSettingsService.RefreshTokenKey, response.Result["RefreshToken"]);
            await InternalSettingsService.SetSetting<ObjectId>(InternalSettingsService.UserIdKey, new ObjectId(response.Result["UserId"]));

            await InternalSettingsService.SetSetting<string>(InternalSettingsService.CurrentScreenKey, "MainPage");

            IsProcessing = false;

            await NavigationService.NavigateAsync("MainPage");
        }


        public override void OnNavigatedFrom(INavigationParameters parameters)
        {

        }

        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
            OpenPopup = true;
            LoaderImagePlaying = true;

            var appUrl = parameters.GetValue<AppURLInfo>("appUrl");

            await InternalSettingsService.SetSetting(InternalSettingsService.AppURLInfo, appUrl);

            var builder = _httpService.CreateBuilder(new Uri($"{Endpoints.GenerateAccessCodeUrl}{appUrl.Audience}"), HttpMethod.Post);

            var response = await builder.WithFormContent(new Dictionary<string, string> ()
            {
                { "deviceId", (await InternalSettingsService.GetSetting<Guid>(InternalSettingsService.DeviceIdKey)).ToString() },
                { "emailAddress", await InternalSettingsService.GetSetting<string>(InternalSettingsService.EmailKey) }
            })
                .SendAsync();

            
            OpenPopup = false;
        }
    }
}
