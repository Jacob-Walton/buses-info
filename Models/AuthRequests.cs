using System.ComponentModel.DataAnnotations;

namespace BusInfo.Models
{
    /// <summary>
    /// Request model for exchanging a Google OAuth ID token for a JWT token
    /// </summary>
    public class GoogleTokenExchangeRequest
    {
        /// <summary>
        /// The ID token received from Google OAuth
        /// </summary>
        [Required(ErrorMessage = "Google ID token is required")]
        public string IdToken { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request model for exchanging an Apple OAuth ID token for a JWT token
    /// </summary>
    public class AppleTokenExchangeRequest
    {
        /// <summary>
        /// The ID token received from Apple Sign In
        /// </summary>
        [Required(ErrorMessage = "Apple ID token is required")]
        public string IdToken { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request model for refreshing an expired JWT token
    /// </summary>
    public class TokenRefreshRequest
    {
        /// <summary>
        /// The refresh token provided during the initial authentication
        /// </summary>
        [Required(ErrorMessage = "Refresh token is required")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}