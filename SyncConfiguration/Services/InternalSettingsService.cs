using System.Text.Json;

namespace SyncConfiguration.Services
{
    public class InternalSettingsService
    {

        public const string UserTokenKey = "UserToken";

        public const string EmailKey = "EmailAddress";

        public const string DeviceIdKey = "DeviceId";

        public const string RefreshTokenKey = "RefreshToken";

        public const string UserIdKey = "UserId";

        public const string CurrentScreenKey = "CurrentScreen";

        public const string AppURLInfo = "AppURLInfo";

        private Dictionary<string, string> _settings = new();

        public void ClearSettings()
        {
            _settings.Clear();
            SecureStorage.Default.RemoveAll();
        }

        public async Task<T> GetSetting<T>(string key)
        {

            string serializedValue = string.Empty;

            if (_settings.ContainsKey(key))
            {
                serializedValue = _settings[key];
            }
            else
            {
                
                serializedValue = await SecureStorage.Default.GetAsync(key) ?? string.Empty;
                
                _settings[key] = serializedValue;
            }

            if(string.IsNullOrEmpty(serializedValue))
            {
                return default(T);
            }

            return JsonSerializer.Deserialize<T>(serializedValue);
        }

        public async Task SetSetting<T>(string key, T value)
        {
            var serializedValue = JsonSerializer.Serialize(value);
            await SecureStorage.Default.SetAsync(key, serializedValue);
            _settings[key] = serializedValue;
        }

    }
}
