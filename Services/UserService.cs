using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BusInfo.Data;
using BusInfo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BusInfo.Services
{
    public class UserService(ApplicationDbContext context, IEmailService emailService, IConfiguration configuration) : IUserService
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IEmailService _emailService = emailService;
        private readonly IConfiguration _configuration = configuration;
        private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public async Task<ApplicationUser?> AuthenticateAsync(string email, string password)
        {
            ApplicationUser? user = await _context!.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null || user.AuthProvider != AuthProvider.Local)
                return null;

            // Only block authentication if account is permanently deleted
            if (user.DeletionConfirmedAt.HasValue)
                return null;

            if (!VerifyPassword(password, user.PasswordHash, user.Salt))
                return null;

            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return user;
        }

        public async Task<bool> CancelAccountDeletionAsync(string userIdOrEmail, bool isEmail = false)
        {
            ApplicationUser? user = isEmail
                ? _context!.Users.FirstOrDefault(u => u.Email == userIdOrEmail)
                : _context!.Users.FirstOrDefault(u => u.Id == userIdOrEmail);

            if (user?.IsPendingDeletion != true) return false;

            user.DeletedAt = null;
            user.DeletionReason = null;
            user.DeletionConfirmedAt = null;

            if (user.EnableEmailNotifications)
            {
                await _emailService.SendAccountReactivationNotificationAsync(user.Email);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
        {
            ApplicationUser? user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            if (!VerifyPassword(currentPassword, user.PasswordHash, user.Salt))
                return false;

            (string hash, string salt) = HashPassword(newPassword);
            user.PasswordHash = hash;
            user.Salt = salt;
            user.LastPasswordChangeDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ConfirmAccountDeletionAsync(string userId)
        {
            ApplicationUser? user = await _context.Users.FindAsync(userId);
            if (user?.DeletedAt.HasValue != true) return false;

            if ((DateTime.UtcNow - user.DeletedAt.Value).TotalDays >= 30)
            {
                user.DeletionConfirmedAt = DateTime.UtcNow;
                user.Email = $"deleted_{userId}@deleted.com";
                user.PasswordHash = string.Empty;
                user.Salt = string.Empty;
                _context.ApiKeys.RemoveRange(_context.ApiKeys.Where(k => k.UserId == userId));
                user.PreferredRoutes?.Clear();
                user.IsEmailVerified = false;

                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }

        public async Task<bool> DeleteAccountAsync(string userId, string password)
        {
            ApplicationUser? user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            if (!VerifyPassword(password, user.PasswordHash, user.Salt))
                return false;

            user.Email = $"deleted_{userId}@deleted.com";
            user.IsEmailVerified = false;
            _context.ApiKeys.RemoveRange(_context.ApiKeys.Where(k => k.UserId == userId));
            user.DeletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<byte[]> ExportUserDataAsync(string userId)
        {
            ApplicationUser user = await _context.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found");

            var userData = new
            {
                user.Email,
                user.CreatedAt,
                user.LastLoginAt,
                user.PreferredRoutes,
                user.EnableEmailNotifications,
                ApiAccess = !string.IsNullOrEmpty(_context!.ApiKeys.FirstOrDefault(k => k.UserId == userId)?.Key),
            };
            string json = System.Text.Json.JsonSerializer.Serialize(userData, _jsonOptions);

            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public async Task<ApplicationUser> GetOrCreateUserAsync(ClaimsPrincipal principal)
        {
            ArgumentNullException.ThrowIfNull(principal, nameof(principal));

            string email = principal.FindFirst("preferred_username")?.Value
                ?? principal.FindFirst(ClaimTypes.Email)?.Value
                ?? throw new ArgumentException("Email claim not found in principal", nameof(principal));
            string name = principal.FindFirst("name")?.Value
                ?? principal.FindFirst(ClaimTypes.Name)?.Value
                ?? "Anonymous";

            if (string.IsNullOrEmpty(email))
                throw new ArgumentException("Email claim not found in principal", nameof(principal));

            ApplicationUser? user = _context!.Users.FirstOrDefault(u => u.Email == email);

            string? externalId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            AuthProvider provider = DetermineAuthProvider(principal);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    Email = email,
                    LastLoginAt = DateTime.UtcNow,
                    AuthProvider = provider,
                    ExternalId = externalId,
                    IsEmailVerified = true,
                    HasAgreedToTerms = true,
                    TermsAgreedAt = DateTime.UtcNow
                };
                _context.Users.Add(user);
            }
            else if (user.AuthProvider != provider)
            {
                // If user exists but with different auth provider, block access
                throw new InvalidOperationException($"Account exists with different authentication method: {user.AuthProvider}");
            }

            await _context.SaveChangesAsync();

            if (user.IsAdmin && principal.Identity is ClaimsIdentity identity)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
            }

            return user;
        }

        public async Task<ApplicationUser?> GetUserByIdAsync(string userId)
        {
            return await _context.Users.FindAsync(userId);
        }

        public async Task<bool> InitiateAccountDeletionAsync(string userId, string password, string? reason = null)
        {
            ApplicationUser? user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            if (!VerifyPassword(password, user.PasswordHash, user.Salt))
                return false;

            user.DeletedAt = DateTime.UtcNow;
            user.DeletionReason = reason;

            if (user.EnableEmailNotifications)
            {
                await _emailService.SendAccountDeletionNotificationAsync(user.Email, 30);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> InitiatePasswordResetAsync(string email)
        {
            ApplicationUser? user = await _context!.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
                return false;

            string token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            user.PasswordResetToken = token;
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(24);

            await _context.SaveChangesAsync();

            string baseUrl = _configuration["BaseUrl"] ?? "https://localhost:3000";
            string resetLink = $"{baseUrl}/ResetPassword?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(email)}";

            await _emailService.SendPasswordResetEmailAsync(email, resetLink);

            return true;
        }

        public async Task<(bool Success, string Message)> RegisterUserAsync(string email, string password, bool agreeToTerms)
        {
            if (!agreeToTerms)
                return (false, "You must agree to the terms and conditions to register");

            if (await _context!.Users.AnyAsync(u => u.Email == email))
                return (false, "An account with that email address already exists");

            (string hash, string salt) = HashPassword(password);
            ApplicationUser user = new()
            {
                Email = email,
                PasswordHash = hash,
                Salt = salt,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow,
                HasAgreedToTerms = true,
                TermsAgreedAt = DateTime.UtcNow,
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return (true, "Account created successfully");
        }

        public async Task<(bool Success, string Message)> ResetPasswordAsync(string token, string email, string newPassword)
        {
            ApplicationUser? user = await _context!.Users.FirstOrDefaultAsync(u =>
                u.Email == email &&
                u.PasswordResetToken == token &&
                u.PasswordResetTokenExpiry > DateTime.UtcNow);

            if (user == null)
                return (false, "Invalid or expired reset token");

            (string hash, string salt) = HashPassword(newPassword);
            user.PasswordHash = hash;
            user.Salt = salt;
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;

            await _context.SaveChangesAsync();

            return (true, "Password reset successful");
        }

        public async Task<bool> UpdateEmailPreferencesAsync(string userId, bool enableEmailNotifications)
        {
            ApplicationUser? user = await _context.Users.FindAsync(userId);
            if (user == null) return false;

            user.EnableEmailNotifications = enableEmailNotifications;
            await _context.SaveChangesAsync();
            return true;
        }

        private static bool VerifyPassword(string password, string hash, string salt)
        {
            using System.Security.Cryptography.HMACSHA512 hmac = new(Convert.FromBase64String(salt));
            byte[] computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(computedHash) == hash;
        }

        private static (string Hash, string Salt) HashPassword(string password)
        {
            using System.Security.Cryptography.HMACSHA512 hmac = new();
            string salt = Convert.ToBase64String(hmac.Key);
            string hash = Convert.ToBase64String(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password)));
            return (hash, salt);
        }

        private static AuthProvider DetermineAuthProvider(ClaimsPrincipal principal)
        {
            // Check for direct provider claims first
            string? providerClaim = principal.FindFirst("provider")?.Value;
            if (!string.IsNullOrEmpty(providerClaim))
            {
                return Enum.Parse<AuthProvider>(providerClaim, true);
            }

            // Check for identity provider claims
            string? idp = principal.FindFirst("http://schemas.microsoft.com/identity/claims/identityprovider")?.Value
                ?? principal.FindFirst("urn:google:issuer")?.Value;

            // Check for login provider
            string? loginProvider = principal.FindFirst("LoginProvider")?.Value;

            // Check scheme
            string? scheme = principal.Identity?.AuthenticationType;

            return idp?.Contains("google", StringComparison.OrdinalIgnoreCase) == true ||
                loginProvider?.Equals("Google", StringComparison.OrdinalIgnoreCase) == true ||
                scheme?.Equals("Google", StringComparison.OrdinalIgnoreCase) == true
                ? AuthProvider.Google
                : idp?.Contains("microsoft", StringComparison.OrdinalIgnoreCase) == true ||
                                loginProvider?.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) == true ||
                                scheme?.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) == true
                    ? AuthProvider.Microsoft
                    : AuthProvider.Local;
        }
    }
}