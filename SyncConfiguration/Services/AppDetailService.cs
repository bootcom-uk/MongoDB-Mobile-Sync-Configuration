using CommunityToolkit.Mvvm.ComponentModel;
using MongoDB.Sync.Models;
using Services;
using SyncConfiguration.Convertors;
using SyncConfiguration.Models.AppDetail;
using SyncConfiguration.Models.Config;
using System.Collections.ObjectModel;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SyncConfiguration.Services
{
    public partial class AppDetailService(HttpService httpService, InternalSettingsService internalSettingsService, AppSettings appSettings) : ObservableObject
    {

        [ObservableProperty]
        private string appUrl;

        private Func<HttpRequestMessage, Task> preRequestAction = async (request) =>
        {
            var token = await internalSettingsService.GetSetting<string>(InternalSettingsService.UserTokenKey);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        };

        private Func<HttpRequestMessage, Task> statusChangeAction = async (response) =>
        {
            var userToken = await internalSettingsService.GetSetting<string>(InternalSettingsService.UserTokenKey);
            var refreshToken = await internalSettingsService.GetSetting<string>(InternalSettingsService.RefreshTokenKey);
            var deviceId = await internalSettingsService.GetSetting<Guid>(InternalSettingsService.DeviceIdKey);

            var refreshTokenHttpBuilder = httpService.CreateBuilder(new Uri(Endpoints.RefreshTokenUrl), HttpMethod.Post)
                .WithHeader("Authorization", $"bearer {userToken}")
                .WithFormContent(new()
                {
                    { "deviceId", deviceId.ToString() },
                    { "refreshToken", refreshToken }
                });

            var refreshTokenResponse = await refreshTokenHttpBuilder
                .SendAsync<Dictionary<string, string>>();

            if (!refreshTokenResponse.Success)
            {
                return;
            }

            await internalSettingsService.SetSetting<string>(InternalSettingsService.UserTokenKey, refreshTokenResponse.Result["JwtToken"]);
            await internalSettingsService.SetSetting<string>(InternalSettingsService.RefreshTokenKey, refreshTokenResponse.Result["RefreshToken"]);
        };

        [ObservableProperty]
        ObservableCollection<AppDetail> appDetails;

        public async Task<IEnumerable<EditDatabaseModel>> GetSchema()
        {
            var builder = httpService.CreateBuilder(new Uri($"{AppUrl}{Endpoints.GetSchemaPath}"), HttpMethod.Get);
            var response = await builder
                .PreRequest(preRequestAction)
                .OnStatus(System.Net.HttpStatusCode.Unauthorized, statusChangeAction)
                .WithRetry(3)
                .SendAsync<IEnumerable<EditDatabaseModel>>();
            if (response is null || !response.Success || response.Result is null)
            {
                throw new InvalidOperationException("The database schema could not be loaded.");
            }
            var result = response.Result!.ToList();
            result.Remove(result.FirstOrDefault(record => record.DatabaseName == "AppServices"));
            result.Remove(result.FirstOrDefault(record => record.DatabaseName == "admin"));
            result.Remove(result.FirstOrDefault(record => record.DatabaseName == "local"));
            return result;
        }

        public async Task CollectApps(string appUrl)
        {
            AppUrl = appUrl;

            var builder = httpService.CreateBuilder(new Uri($"{AppUrl}{Endpoints.GetAppsPath}"), HttpMethod.Get);
            var response = await builder
                .PreRequest(preRequestAction)
                .OnStatus(System.Net.HttpStatusCode.Unauthorized, statusChangeAction)
                .WithRetry(3)
                .SendAsync<List<AppDetail>>();
            if (response is null)
            {
                return;
            }
            AppDetails = new(response.Result);
        }

        public async Task ReSyncCollection(AppDetail appDetail, string databaseName, string collectionName)
        {
            var builder = httpService.CreateBuilder(new Uri($"{AppUrl}/SyncCollection/{databaseName}/{collectionName}"), HttpMethod.Post);            
            var response = await builder
                .PreRequest(preRequestAction)
                .OnStatus(System.Net.HttpStatusCode.Unauthorized, statusChangeAction)
                .WithRetry(3)
                .WithJsonContent(appDetail, new()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new ObjectIdConverter() }
                })
                .SendAsync();
            if (response is null)
            {
                return;
            }
        }

        public async Task SaveApp(AppDetail appDetail)
        {
            var builder = httpService.CreateBuilder(new Uri($"{AppUrl}"), HttpMethod.Post);
            var response = await builder
                .PreRequest(preRequestAction)
                .OnStatus(System.Net.HttpStatusCode.Unauthorized, statusChangeAction)
                .WithRetry(3)                
                .WithJsonContent(appDetail, new()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new ObjectIdConverter() }
                })
                .SendAsync();
            if (response is null)
            {
                return;
            }            
        }

    }
}
