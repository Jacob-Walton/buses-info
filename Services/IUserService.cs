using System.Security.Claims;
using System.Threading.Tasks;
using BusInfo.Models;

namespace BusInfo.Services
{
    public interface IUserService
    {
        Task<ApplicationUser> GetOrCreateUserAsync(ClaimsPrincipal principal);
        Task<ApplicationUser?> AuthenticateAsync(string email, string password);
        Task<(bool Success, string Message)> RegisterUserAsync(string email, string password, bool agreeToTerms);
        Task<bool> InitiatePasswordResetAsync(string email);
        Task<(bool Success, string Message)> ResetPasswordAsync(string token, string email, string newPassword);
        Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
        Task<bool> DeleteAccountAsync(string userId, string password);
        Task<byte[]> ExportUserDataAsync(string userId);
        Task<bool> UpdateEmailPreferencesAsync(string userId, bool enableEmailNotifications);
        Task<bool> InitiateAccountDeletionAsync(string userId, string password, string? reason = null);
        Task<bool> CancelAccountDeletionAsync(string userIdOrEmail, bool isEmail = false);
        Task<bool> ConfirmAccountDeletionAsync(string userId);
        Task<ApplicationUser?> GetUserByIdAsync(string userId);
    }
}