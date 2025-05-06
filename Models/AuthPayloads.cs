using System.Text.Json.Serialization;

namespace BusInfo.Models
{
    public class GoogleTokenPayload
    {
        public string Sub { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EmailVerified { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Picture { get; set; } = string.Empty;
        public string GivenName { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;

        [JsonPropertyName("aud")]
        public string Audience { get; set; } = string.Empty;

        [JsonPropertyName("exp")]
        public string ExpirationTime { get; set; } = string.Empty;

        [JsonPropertyName("iat")]
        public string IssuedAt { get; set; } = string.Empty;

        [JsonPropertyName("iss")]
        public string Issuer { get; set; } = string.Empty;
    }

    public class AppleTokenPayload
    {
        public string Sub { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EmailVerified { get; set; } = true;
    }
}