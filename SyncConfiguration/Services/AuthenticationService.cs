using Services;
using System.Net.Http.Headers;

namespace SyncConfiguration.Services
{
    public class AuthenticationService(HttpService httpService, InternalSettingsService internalSettingsService)
    {

        private Func<HttpRequestMessage, Task> preRequestAction = async (request) =>
        {
            var token = await internalSettingsService.GetSetting<string>(InternalSettingsService.UserTokenKey);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        };

        // verify-token-validity
        public async Task<bool> VerifyTokenValidity(string token)
        {


                var builder = httpService.CreateBuilder(new Uri(Endpoints.ValidateTokenUrl), HttpMethod.Get);

                var response = await builder
                    .PreRequest(preRequestAction)
                    .SendAsync();

                // Response has not completed with a response.
                if (response == null || !response.Success)
                {
                    return false;
                }
                return response.Success;
            

        }


        public async Task<Dictionary<string, string>> CompleteLoginProcess(Guid deviceId, string accessCode)
        {

            var builder = httpService.CreateBuilder(new Uri(Endpoints.ValidateAccessCodeUrl), HttpMethod.Post);
            var response = await builder.WithFormContent(new()
            {
                { "accessCode", accessCode },
                { "deviceId", deviceId.ToString() }
            })
                .WithRetry(3, r => r.StatusCode != System.Net.HttpStatusCode.Unauthorized && !r.IsSuccessStatusCode)
                .SendAsync<Dictionary<string, string>>();

            // Response has not completed with a response.
            if (response == null || !response.Success)
            {
                return null;
            }

            return response.Result;

        }

    }
}
