namespace BusInfo.Models
{
    public class AuthenticationDetails
    {
        public AuthProvider Provider { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public bool CanChangePassword { get; set; }
        public bool RequiresEmailVerification { get; set; }
        public bool IsEmailVerified { get; set; }
        public string Email { get; set; } = string.Empty;
        public bool HasExternalId { get; set; }
    }
}
