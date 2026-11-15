namespace SyncConfiguration
{
    public static class Endpoints
    {

        public static readonly string LoginUrl = "https://bootcomidentity.azurewebsites.net/AuthenticationV2";

        public static readonly string RefreshTokenUrl = $"{LoginUrl}/refresh-token";

        public static readonly string ValidateAccessCodeUrl = $"{LoginUrl}/verify-access-code";

        // public static readonly string GenerateAccessCodeUrl = $"{LoginUrl}/generate-access-code/BOOTCOM_HOME";

        public static readonly string GenerateAccessCodeUrl = $"{LoginUrl}/generate-access-code/";

        public static readonly string ValidateTokenUrl = $"{LoginUrl}/verify-token-validity";

        public static readonly string GetAppsPath = "/all";

        public static readonly string GetSchemaPath = "/schema";

    }
}
