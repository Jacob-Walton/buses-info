namespace BusInfo.Models
{
    public static class AuthProviderExtensions
    {
        public static string GetDisplayName(this AuthProvider provider) => provider switch
        {
            AuthProvider.Local => "Email/Password",
            AuthProvider.Google => "Google",
            AuthProvider.Microsoft => "Microsoft",
            _ => provider.ToString()
        };
    }
}
