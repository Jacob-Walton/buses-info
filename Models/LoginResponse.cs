using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace BusInfo.Models
{
    /// <summary>
    /// Response model returned after successful authentication
    /// </summary>
    public class LoginResponse
    {
        /// <summary>
        /// JWT authentication token
        /// </summary>
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Refresh token for obtaining a new JWT when it expires
        /// </summary>
        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>
        /// ISO-8601 formatted expiration timestamp
        /// </summary>
        [JsonPropertyName("expiresAt")]
        public string ExpiresAt { get; set; } = string.Empty;

        /// <summary>
        /// Authenticated user information
        /// </summary>
        [JsonPropertyName("user")]
        public User User { get; set; } = new User();
    }

    /// <summary>
    /// User information returned during authentication
    /// </summary>
    public class User
    {
        /// <summary>
        /// Unique identifier for the user
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// User's email address
        /// </summary>
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// User's display name
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// User's role in the system
        /// </summary>
        [JsonPropertyName("role")]
        public UserRole Role { get; set; } = UserRole.Student;
    }

    /// <summary>
    /// Represents the role of a user in the system.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UserRole
    {
        /// <summary>
        /// Regular student user with standard access
        /// </summary>
        [EnumMember(Value = "student")]
        Student = 0,

        /// <summary>
        /// Administrator with elevated access privileges
        /// </summary>
        [EnumMember(Value = "admin")]
        Admin = 1
    }
}