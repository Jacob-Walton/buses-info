using System.Threading.Tasks;

namespace BusInfo.Services
{
    public interface IEmailService
    {
        Task SendPasswordResetEmailAsync(string toEmail, string resetLink);
        Task SendAccountDeletionNotificationAsync(string toEmail, int gracePeriodDays);
        Task SendAccountReactivationNotificationAsync(string toEmail);
    }
}