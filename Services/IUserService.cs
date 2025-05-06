using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BusInfo.Models;

namespace BusInfo.Services
{
    public interface IUserService
    {
        Task<ApplicationUser?> AuthenticateAsync(string email, string password);
        Task<LoginResponse> AuthenticateGoogleUserAsync(string idToken);
        Task<LoginResponse> AuthenticateAppleUserAsync(string idToken);
        Task<LoginResponse> RefreshTokenAsync(string refreshToken);

        Task<ApplicationUser?> GetUserByIdAsync(string userId);
        Task<ApplicationUser?> GetUserByEmailAsync(string email);
        Task<ApplicationUser?> GetOrCreateUserAsync(ClaimsPrincipal principal);

        Task<(bool Success, string Message)> RegisterUserAsync(string email, string password, bool agreeToTerms);
        Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword);

        Task<bool> InitiateAccountDeletionAsync(string userId, string password, string? reason = null);
        Task<bool> CancelAccountDeletionAsync(string identifier, bool isEmail = false);

        Task<byte[]> ExportUserDataAsync(string userId);

        string GenerateJwtToken(ApplicationUser user);
        string GenerateRefreshToken();
        Task SaveRefreshTokenAsync(ApplicationUser user, string refreshToken, DateTime expiryTime);
        string GetDisplayName(ApplicationUser user);
    }
}