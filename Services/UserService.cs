using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BusInfo.Data;
using BusInfo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using static Microsoft.Extensions.Logging.LoggerMessage;

namespace BusInfo.Services
{
    public class UserService(
        ApplicationDbContext context,
        ILogger<UserService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory) : IUserService
    {
        private readonly ApplicationDbContext _context = context ?? throw new ArgumentNullException(nameof(context));
        private readonly ILogger<UserService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

        #region Logger Message Definitions

        // Authentication Logging (5000-5099)
        private static readonly Action<ILogger, string, string, string, Exception?> _logWrongAuthProvider =
            Define<string, string, string>(LogLevel.Warning, 5000, "User {Email} attempted {Method} login but account uses {Provider}");

        private static readonly Action<ILogger, string, Exception?> _logLockedOutAttempt =
            Define<string>(LogLevel.Warning, 5001, "User {Email} attempted login while locked out");

        private static readonly Action<ILogger, string, Exception?> _logAccountLockout =
            Define<string>(LogLevel.Warning, 5002, "User {Email} locked out after multiple failed login attempts");

        private static readonly Action<ILogger, string, Exception?> _logExternalLoginNoEmail =
            Define<string>(LogLevel.Warning, 5003, "External login for {UserIdentity} failed: No email claim found");

        // Token Validation Logging (5100-5149)
        private static readonly Action<ILogger, string, string, Exception?> _logSuccessfulGoogleValidation =
            Define<string, string>(LogLevel.Information, 5100, "Successfully validated Google token for email: {Email} with sub: {Sub}");

        private static readonly Action<ILogger, int, Exception?> _logGoogleValidationFailed =
            Define<int>(LogLevel.Warning, 5101, "Google token validation failed with status code: {StatusCode}");

        private static readonly Action<ILogger, string, Exception?> _logTokenValidationDebug =
            Define<string>(LogLevel.Debug, 5102, "Google token validation response: {Response}");

        private static readonly Action<ILogger, Exception?> _logDeserializationFailed =
            Define(LogLevel.Warning, 5103, "Failed to deserialize Google token payload");

        private static readonly Action<ILogger, Exception?> _logNoEmailInPayload =
            Define(LogLevel.Warning, 5104, "No email found in Google token payload");

        private static readonly Action<ILogger, string, Exception> _logHttpRequestError =
            Define<string>(LogLevel.Error, 5105, "Error making HTTP request to {Endpoint}");

        private static readonly Action<ILogger, string, Exception> _logJsonParseError =
            Define<string>(LogLevel.Error, 5106, "Error deserializing token payload: {Message}");

        private static readonly Action<ILogger, Exception> _logUnexpectedTokenError =
            Define(LogLevel.Error, 5107, "Unexpected error validating token");

        // Email Operations (5150-5199)
        private static readonly Action<ILogger, string, string, Exception> _logEmailSendError =
            Define<string, string>(LogLevel.Error, 5150, "Failed to send {EmailType} email to {Email}");

        // User Management (5200-5299)
        private static readonly Action<ILogger, string, Exception?> _logUserCreated =
            Define<string>(LogLevel.Information, 5200, "Created new user account for {Email}");

        private static readonly Action<ILogger, string, Exception?> _logUserUpdated = 
            Define<string>(LogLevel.Information, 5201, "Updated user account for {Email}");

        private static readonly Action<ILogger, string, Exception?> _logPasswordChanged =
            Define<string>(LogLevel.Information, 5202, "Password changed for user {Email}");

        private static readonly Action<ILogger, string, Exception?> _logAccountDeletion =
            Define<string>(LogLevel.Information, 5203, "Account deletion initiated for {Email}");

        private static readonly Action<ILogger, string, Exception?> _logAccountReactivation =
            Define<string>(LogLevel.Information, 5204, "Account reactivated for {Email}");

        #endregion Logger Message Definitions

        #region Authentication

        public async Task<ApplicationUser?> AuthenticateAsync(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                return null;

            ApplicationUser? user = await _context.Users!
                .FirstOrDefaultAsync(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && u.DeletedAt == null);

            if (user == null)
                return null;

            if (user.AuthProvider != AuthProvider.Local)
            {
                _logWrongAuthProvider(_logger, email, "password", user.AuthProvider.ToString(), null);
                throw new InvalidOperationException($"Account is linked to {user.AuthProvider}. Please sign in with that method.");
            }

            if (user.LockoutEnd > DateTime.UtcNow)
            {
                _logLockedOutAttempt(_logger, email, null);
                return null;
            }

            bool verified = VerifyPassword(password, user.PasswordHash, user.Salt);

            if (!verified)
            {
                user.FailedLoginAttempts++;

                // Lock account after 5 failed attempts
                if (user.FailedLoginAttempts >= 5)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                    _logAccountLockout(_logger, email, null);
                }

                await _context.SaveChangesAsync();
                return null;
            }

            // Reset failed attempts on successful login
            user.FailedLoginAttempts = 0;
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return user;
        }

        public async Task<LoginResponse> AuthenticateGoogleUserAsync(string idToken)
        {
            // Validate the Google ID token
            GoogleTokenPayload payload = await ValidateGoogleIdTokenAsync(idToken);

            // Find or create user account
            string email = payload.Email;

            // Modified query to avoid using IsPendingDeletion
            ApplicationUser? user = await _context.Users!
                .FirstOrDefaultAsync(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

            if (user == null)
            {
                // Create new user
                user = new ApplicationUser
                {
                    Email = email,
                    AuthProvider = AuthProvider.Google,
                    ExternalId = payload.Sub, // Google's user ID
                    IsEmailVerified = payload.EmailVerified,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                };

                _context.Users!.Add(user);
                _logUserCreated(_logger, email, null);
            }
            else if (user.AuthProvider != AuthProvider.Google && !string.IsNullOrEmpty(user.PasswordHash))
            {
                // User exists but with a different auth provider
                _logWrongAuthProvider(_logger, email, "Google", user.AuthProvider.ToString(), null);
                throw new InvalidOperationException(
                    $"Account already exists with {user.AuthProvider}. Please sign in with that method.");
            }
            else
            {
                // Update existing Google user
                user.AuthProvider = AuthProvider.Google;
                user.ExternalId = payload.Sub;
                user.LastLoginAt = DateTime.UtcNow;

                if (!user.IsEmailVerified && payload.EmailVerified)
                {
                    user.IsEmailVerified = true;
                }

                if (user.DeletedAt != null) // Instead of IsPendingDeletion
                {
                    user.DeletedAt = null;
                    user.DeletionConfirmedAt = null;
                    _logAccountReactivation(_logger, email, null);
                }
                else
                {
                    _logUserUpdated(_logger, email, null);
                }
            }

            await _context.SaveChangesAsync();

            // Generate JWT token
            string jwtToken = GenerateJwtToken(user);

            // Generate refresh token
            string refreshToken = GenerateRefreshToken();
            DateTime expiryTime = DateTime.UtcNow.AddDays(30);

            // Save refresh token
            await SaveRefreshTokenAsync(user, refreshToken, expiryTime);

            // Return response
            return new LoginResponse
            {
                Token = jwtToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddHours(1).ToString("o"),
                User = new User
                {
                    Id = user.Id,
                    Email = user.Email,
                    Name = GetDisplayName(user),
                    Role = user.IsAdmin ? UserRole.Admin : UserRole.Student
                }
            };
        }

        public async Task<LoginResponse> AuthenticateAppleUserAsync(string idToken)
        {
            // Validate the Apple ID token
            AppleTokenPayload payload = await ValidateAppleIdTokenAsync(idToken);

            if (string.IsNullOrEmpty(payload.Email))
            {
                throw new UnauthorizedAccessException("Email is required for authentication");
            }

            // Find or create user account
            string email = payload.Email;

            // Modified query to avoid using IsPendingDeletion
            ApplicationUser? user = await _context.Users!
                .FirstOrDefaultAsync(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

            if (user == null)
            {
                // Create new user
                user = new ApplicationUser
                {
                    Email = email,
                    AuthProvider = AuthProvider.Apple,
                    ExternalId = payload.Sub, // Apple's user ID
                    IsEmailVerified = true, // Apple email is verified
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                };

                _context.Users!.Add(user);
                _logUserCreated(_logger, email, null);
            }
            else if (user.AuthProvider != AuthProvider.Apple && !string.IsNullOrEmpty(user.PasswordHash))
            {
                // User exists but with a different auth provider
                _logWrongAuthProvider(_logger, email, "Apple", user.AuthProvider.ToString(), null);
                throw new InvalidOperationException(
                    $"Account already exists with {user.AuthProvider}. Please sign in with that method.");
            }
            else
            {
                // Update existing Apple user
                user.AuthProvider = AuthProvider.Apple;
                user.ExternalId = payload.Sub;
                user.LastLoginAt = DateTime.UtcNow;
                user.IsEmailVerified = true;

                if (user.DeletedAt != null) // Instead of IsPendingDeletion
                {
                    user.DeletedAt = null;
                    user.DeletionConfirmedAt = null;
                    _logAccountReactivation(_logger, email, null);
                }
                else
                {
                    _logUserUpdated(_logger, email, null);
                }
            }

            await _context.SaveChangesAsync();

            // Generate JWT token
            string jwtToken = GenerateJwtToken(user);

            // Generate refresh token
            string refreshToken = GenerateRefreshToken();
            DateTime expiryTime = DateTime.UtcNow.AddDays(30);

            // Save refresh token
            await SaveRefreshTokenAsync(user, refreshToken, expiryTime);

            // Return response
            return new LoginResponse
            {
                Token = jwtToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddHours(1).ToString("o"),
                User = new User
                {
                    Id = user.Id,
                    Email = user.Email,
                    Name = GetDisplayName(user),
                    Role = user.IsAdmin ? UserRole.Admin : UserRole.Student
                }
            };
        }

        public async Task<LoginResponse> RefreshTokenAsync(string refreshToken)
        {
            ApplicationUser? user = await _context.Users!
                .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken &&
                                         u.RefreshTokenExpiry > DateTime.UtcNow) ?? throw new UnauthorizedAccessException("Invalid or expired refresh token");

            // Generate new tokens
            string jwtToken = GenerateJwtToken(user);
            string newRefreshToken = GenerateRefreshToken();
            DateTime expiryTime = DateTime.UtcNow.AddDays(30);

            // Save new refresh token
            await SaveRefreshTokenAsync(user, newRefreshToken, expiryTime);

            return new LoginResponse
            {
                Token = jwtToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddHours(1).ToString("o"),
                User = new User
                {
                    Id = user.Id,
                    Email = user.Email,
                    Name = GetDisplayName(user),
                    Role = user.IsAdmin ? UserRole.Admin : UserRole.Student
                }
            };
        }

        #endregion Authentication

        #region User Management

        public async Task<ApplicationUser?> GetUserByIdAsync(string userId)
        {
            return await _context.Users!.FindAsync(userId);
        }

        public Task<ApplicationUser?> GetUserByEmailAsync(string email)
        {
            return _context.Users!
                .FirstOrDefaultAsync(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<ApplicationUser?> GetOrCreateUserAsync(ClaimsPrincipal principal)
        {
            string? email = principal.FindFirstValue(ClaimTypes.Email) ??
                        principal.FindFirstValue("email");

            if (string.IsNullOrEmpty(email))
            {
                _logExternalLoginNoEmail(_logger, principal.Identity?.Name ?? "unknown", null);
                return null;
            }

            ApplicationUser? user = await GetUserByEmailAsync(email);

            if (user != null)
            {
                // Update existing user's last login
                user.LastLoginAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return user;
            }

            // Determine authentication provider
            string providerName = principal.Identity?.AuthenticationType?.ToUpperInvariant() ?? string.Empty;
            AuthProvider authProvider = DetermineAuthProvider(providerName);

            // Create a new user
            ApplicationUser newUser = new()
            {
                Email = email,
                AuthProvider = authProvider,
                IsEmailVerified = true, // Assume verified from external providers
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                // Try to get additional info from claims
                ExternalId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
                                    principal.FindFirstValue("sub") ??
                                    Guid.NewGuid().ToString()
            };

            _context.Users!.Add(newUser);
            await _context.SaveChangesAsync();
            _logUserCreated(_logger, email, null);

            return newUser;
        }

        public async Task<(bool Success, string Message)> RegisterUserAsync(
            string email, string password, bool agreeToTerms)
        {
            if (!agreeToTerms)
            {
                return (false, "You must agree to the terms and conditions to register.");
            }

            ApplicationUser? existingUser = await GetUserByEmailAsync(email);
            if (existingUser != null)
            {
                return (false, "A user with this email already exists.");
            }

            // Create salt and hash password
            string salt = GenerateSalt();
            string passwordHash = HashPassword(password, salt);

            // Create new user
            ApplicationUser user = new()
            {
                Email = email,
                PasswordHash = passwordHash,
                Salt = salt,
                AuthProvider = AuthProvider.Local,
                CreatedAt = DateTime.UtcNow,
                IsEmailVerified = false, // Requires verification for local accounts
                TermsAgreedAt = DateTime.UtcNow,
                HasAgreedToTerms = true,
                // Generate verification token
                EmailVerificationToken = GenerateRandomToken(),
                EmailVerificationTokenExpiry = DateTime.UtcNow.AddDays(7)
            };

            _context.Users!.Add(user);
            await _context.SaveChangesAsync();
            _logUserCreated(_logger, email, null);

            // Send verification email
            try
            {
                // TODO: Implement verification email sending
                await SendVerificationEmailAsync(user);
            }
            catch (Exception ex)
            {
                _logEmailSendError(_logger, "verification", email, ex);
                // Continue despite email failure
            }

            return (true, "Registration successful. Please check your email to verify your account.");
        }

        public async Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
        {
            ApplicationUser? user = await GetUserByIdAsync(userId);
            if (user == null || user.AuthProvider != AuthProvider.Local)
            {
                return false;
            }

            if (!VerifyPassword(currentPassword, user.PasswordHash, user.Salt))
            {
                return false;
            }

            // Update password
            string salt = GenerateSalt();
            user.PasswordHash = HashPassword(newPassword, salt);
            user.Salt = salt;
            user.LastPasswordChangeDate = DateTime.UtcNow;
            user.RequiresPasswordChange = false;

            await _context.SaveChangesAsync();
            _logPasswordChanged(_logger, user.Email, null);
            return true;
        }

        public async Task<bool> InitiateAccountDeletionAsync(string userId, string password, string? reason = null)
        {
            ApplicationUser? user = await GetUserByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            // For local accounts, verify password
            if (user.AuthProvider == AuthProvider.Local && !VerifyPassword(password, user.PasswordHash, user.Salt))
            {
                return false;
            }

            user.DeletedAt = DateTime.UtcNow;
            user.DeletionReason = reason;

            await _context.SaveChangesAsync();
            _logAccountDeletion(_logger, user.Email, null);

            // Send confirmation email
            try
            {
                // TODO: Implement deletion confirmation email sending
                await SendDeletionEmailAsync(user);
            }
            catch (Exception ex)
            {
                _logEmailSendError(_logger, "deletion", user.Email, ex);
                // Continue despite email failure
            }

            return true;
        }

        public async Task<bool> CancelAccountDeletionAsync(string identifier, bool isEmail = false)
        {
            ApplicationUser? user = isEmail ? await GetUserByEmailAsync(identifier) : await GetUserByIdAsync(identifier);
            if (user?.IsPendingDeletion != true)
            {
                return false;
            }

            user.DeletedAt = null;
            user.DeletionConfirmedAt = null;

            await _context.SaveChangesAsync();
            _logAccountReactivation(_logger, user.Email, null);
            return true;
        }

        public async Task<byte[]> ExportUserDataAsync(string userId)
        {
            ApplicationUser? user = await GetUserByIdAsync(userId) ?? throw new ArgumentException("User not found", nameof(userId));

            // Gather user data
            var userData = new
            {
                user.Id,
                user.Email,
                user.CreatedAt,
                user.LastLoginAt,
                user.IsEmailVerified,
                user.HasAgreedToTerms,
                user.TermsAgreedAt,
                Preferences = new
                {
                    user.PreferredRoutes,
                    user.ShowPreferredRoutesFirst,
                    user.EnableEmailNotifications
                },
                AuthenticationInfo = new
                {
                    Provider = user.AuthProvider.ToString(),
                    LastPasswordChange = user.LastPasswordChangeDate
                }
                // Add any other exportable data here
            };

            // Serialize to JSON
            string json = JsonSerializer.Serialize(userData, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return Encoding.UTF8.GetBytes(json);
        }

        #endregion User Management

        #region Helper Methods

        public string GenerateJwtToken(ApplicationUser user)
        {
            SymmetricSecurityKey securityKey = new(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key not configured")));
            SigningCredentials credentials = new(securityKey, SecurityAlgorithms.HmacSha256);

            List<Claim> claims =
            [
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Email, user.Email),
                new("email", user.Email)
            ];

            if (user.IsAdmin)
            {
                claims.Add(new(ClaimTypes.Role, "Admin"));
            }

            JwtSecurityToken token = new(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            byte[] randomNumber = new byte[32];
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public Task SaveRefreshTokenAsync(ApplicationUser user, string refreshToken, DateTime expiryTime)
        {
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = expiryTime;
            return _context.SaveChangesAsync();
        }

        public string GetDisplayName(ApplicationUser user)
        {
            // Use email username as display name if no name is set
            return !string.IsNullOrEmpty(user.Email)
                ? user.Email.Split('@')[0]
                : "User";
        }

        private static string GenerateRandomToken()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static string GenerateSalt()
        {
            byte[] randomBytes = new byte[32];
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        private static string HashPassword(string password, string salt)
        {
            byte[] passwordWithSalt = Encoding.UTF8.GetBytes(password + salt);
            byte[] hashBytes = SHA256.HashData(passwordWithSalt);
            return Convert.ToBase64String(hashBytes);
        }

        private static bool VerifyPassword(string password, string storedHash, string salt)
        {
            string computedHash = HashPassword(password, salt);
            return storedHash == computedHash;
        }

        private async Task<GoogleTokenPayload> ValidateGoogleIdTokenAsync(string idToken)
        {
            try
            {
                using HttpClient httpClient = _httpClientFactory.CreateClient();
                HttpResponseMessage response = await httpClient.GetAsync(
                    new Uri($"https://oauth2.googleapis.com/tokeninfo?id_token={idToken}"));

                if (!response.IsSuccessStatusCode)
                {
                    _logGoogleValidationFailed(_logger, (int)response.StatusCode, null);
                    throw new UnauthorizedAccessException("Invalid Google token");
                }

                string content = await response.Content.ReadAsStringAsync();
                _logTokenValidationDebug(_logger, content, null);

                // Use options to handle case sensitivity and other issues
                JsonSerializerOptions options = new()
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                };

                GoogleTokenPayload? payload = JsonSerializer.Deserialize<GoogleTokenPayload>(content, options);

                if (payload == null)
                {
                    _logDeserializationFailed(_logger, null);
                    throw new UnauthorizedAccessException("Invalid Google token payload format");
                }

                if (string.IsNullOrEmpty(payload.Email))
                {
                    // As a fallback, try to manually extract email using a more manual approach
                    try
                    {
                        JsonDocument jsonDoc = JsonDocument.Parse(content);
                        if (jsonDoc.RootElement.TryGetProperty("email", out JsonElement emailElement))
                        {
                            payload.Email = emailElement.GetString() ?? string.Empty;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logJsonParseError(_logger, "email extraction", ex);
                    }

                    if (string.IsNullOrEmpty(payload.Email))
                    {
                        _logNoEmailInPayload(_logger, null);
                        throw new UnauthorizedAccessException("Invalid Google token payload: No email found");
                    }
                }

                _logSuccessfulGoogleValidation(_logger, payload.Email, payload.Sub, null);
                return payload;
            }
            catch (HttpRequestException ex)
            {
                _logHttpRequestError(_logger, "Google token validation endpoint", ex);
                throw new UnauthorizedAccessException("Failed to validate Google token", ex);
            }
            catch (JsonException ex)
            {
                _logJsonParseError(_logger, ex.Message, ex);

                // Add more context to make the error more understandable
                throw new UnauthorizedAccessException("Failed to parse Google token response", ex);
            }
            catch (Exception ex) when (ex is not UnauthorizedAccessException)
            {
                _logUnexpectedTokenError(_logger, ex);
                throw new UnauthorizedAccessException("Failed to validate Google token", ex);
            }
        }

        private async Task<AppleTokenPayload> ValidateAppleIdTokenAsync(string idToken)
        {
            try
            {
                JwtSecurityTokenHandler handler = new();
                if (!handler.CanReadToken(idToken))
                {
                    throw new UnauthorizedAccessException("Invalid Apple token format");
                }

                if (handler.ReadToken(idToken) is not JwtSecurityToken jsonToken)
                {
                    throw new UnauthorizedAccessException("Invalid Apple token");
                }

                // In a production environment, you should validate the token signature
                // against Apple's public keys and check other claims like issuer, 
                // audience, expiration, etc.

                string? email = jsonToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
                string? sub = jsonToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

                return string.IsNullOrEmpty(sub)
                    ? throw new UnauthorizedAccessException("Invalid Apple token: missing sub claim")
                    : new AppleTokenPayload
                    {
                        Email = email ?? "",
                        Sub = sub
                    };
            }
            catch (Exception ex) when (ex is not UnauthorizedAccessException)
            {
                _logUnexpectedTokenError(_logger, ex);
                throw new UnauthorizedAccessException("Failed to validate Apple token", ex);
            }
        }

        private static AuthProvider DetermineAuthProvider(string providerName)
        {
            return providerName switch
            {
                "GOOGLE" => AuthProvider.Google,
                "APPLE" => AuthProvider.Apple,
                _ => AuthProvider.Local
            };
        }

        private async Task SendVerificationEmailAsync(ApplicationUser user)
        {
            if (string.IsNullOrEmpty(user.EmailVerificationToken))
            {
                return;
            }

            string baseUrl = _configuration["BaseUrl"] ?? "https://localhost";
            string verificationUrl = $"{baseUrl}/verify-email?token={user.EmailVerificationToken}&email={Uri.EscapeDataString(user.Email)}";

            // await _emailService.SendEmailAsync(
            //     user.Email,
            //     "Verify Your Email",
            //     "verification-email",
            //     new Dictionary<string, string>
            //     {
            //         { "VerificationLink", verificationUrl },
            //         { "UserEmail", user.Email }
            //     });
        }

        private async Task SendDeletionEmailAsync(ApplicationUser user)
        {
            string baseUrl = _configuration["BaseUrl"] ?? "https://localhost";
            string reactivationUrl = $"{baseUrl}/reactivate?email={Uri.EscapeDataString(user.Email)}";

            // await _emailService.SendEmailAsync(
            //     user.Email,
            //     "Account Deletion Confirmation",
            //     "account-deletion",
            //     new Dictionary<string, string>
            //     {
            //         { "ReactivationLink", reactivationUrl },
            //         { "UserEmail", user.Email },
            //         { "DeletionDate", user.DeletedAt?.AddDays(30).ToString("MMMM dd, yyyy") ?? "" }
            //     });
        }

        #endregion Helper Methods
    }

    // Add necessary payload classes if they don't exist elsewhere
    public class GoogleTokenPayload
    {
        public string Sub { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EmailVerified { get; set; }
    }

    public class AppleTokenPayload
    {
        public string Sub { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}