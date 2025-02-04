using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using BusInfo.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusInfo.Services
{
    public class EmailService(IOptions<SmtpSettings> smtpSettings, ILogger<EmailService> logger, IEmailTemplateService templateService) : IEmailService
    {
        private readonly SmtpSettings _smtpSettings = smtpSettings.Value;
        private readonly ILogger<EmailService> _logger = logger;
        private readonly IEmailTemplateService _templateService = templateService;

        private static readonly Action<ILogger, string, Exception> _passwordResetEmailSent =
            LoggerMessage.Define<string>(LogLevel.Information,
                new EventId(1, nameof(SendPasswordResetEmailAsync)),
                "Password reset email sent to {Email}");

        private static readonly Action<ILogger, string, string, Exception> _passwordResetEmailFailed =
            LoggerMessage.Define<string, string>(LogLevel.Error,
                new EventId(2, nameof(SendPasswordResetEmailAsync)),
                "Failed to send password reset email to {Email}. Error: {Error}");

        private static readonly Action<ILogger, string, Exception> _accountDeletionEmailSent =
            LoggerMessage.Define<string>(LogLevel.Information,
                new EventId(3, nameof(SendAccountDeletionNotificationAsync)),
                "Account deletion email sent to {Email}");

        private static readonly Action<ILogger, string, string, Exception> _accountDeletionEmailFailed =
            LoggerMessage.Define<string, string>(LogLevel.Error,
                new EventId(4, nameof(SendAccountDeletionNotificationAsync)),
                "Failed to send account deletion email to {Email}. Error: {Error}");

        private static readonly Action<ILogger, string, Exception> _accountReactivationEmailSent =
            LoggerMessage.Define<string>(LogLevel.Information,
                new EventId(5, nameof(SendAccountReactivationNotificationAsync)),
                "Account reactivation email sent to {Email}");

        private static readonly Action<ILogger, string, string, Exception> _accountReactivationEmailFailed =
            LoggerMessage.Define<string, string>(LogLevel.Error,
                new EventId(6, nameof(SendAccountReactivationNotificationAsync)),
                "Failed to send account reactivation email to {Email}. Error: {Error}");

        public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
        {
            using MailMessage mail = new()
            {
                From = new MailAddress(_smtpSettings.Username, "BusInfo Support"),
                Subject = "Reset Your Password - BusInfo",
                Body = _templateService.GetPasswordResetTemplate(resetLink),
                IsBodyHtml = true
            };
            mail.To.Add(toEmail);

            using SmtpClient client = new()
            {
                Host = _smtpSettings.Host,
                Port = _smtpSettings.Port,
                EnableSsl = _smtpSettings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password)
            };

            try
            {
                await client.SendMailAsync(mail);
                _passwordResetEmailSent(_logger, toEmail, null!);
            }
            catch (Exception ex)
            {
                _passwordResetEmailFailed(_logger, toEmail, ex.Message, ex);
                throw;
            }
        }

        public async Task SendAccountDeletionNotificationAsync(string toEmail, int gracePeriodDays)
        {
            using MailMessage mail = new()
            {
                From = new MailAddress(_smtpSettings.Username, "BusInfo Support"),
                Subject = "Account Deletion Request - BusInfo",
                Body = _templateService.GetAccountDeletionTemplate(gracePeriodDays),
                IsBodyHtml = true
            };
            mail.To.Add(toEmail);

            using SmtpClient client = new()
            {
                Host = _smtpSettings.Host,
                Port = _smtpSettings.Port,
                EnableSsl = _smtpSettings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password)
            };

            try
            {
                await client.SendMailAsync(mail);
                _accountDeletionEmailSent(_logger, toEmail, null!);
            }
            catch (Exception ex)
            {
                _accountDeletionEmailFailed(_logger, toEmail, ex.Message, ex);
                throw;
            }
        }

        public async Task SendAccountReactivationNotificationAsync(string toEmail)
        {
            using MailMessage mail = new()
            {
                From = new MailAddress(_smtpSettings.Username, "BusInfo Support"),
                Subject = "Account Reactivation Confirmation - BusInfo",
                Body = _templateService.GetAccountReactivationTemplate(),
                IsBodyHtml = true
            };

            mail.To.Add(toEmail);

            using SmtpClient client = new()
            {
                Host = _smtpSettings.Host,
                Port = _smtpSettings.Port,
                EnableSsl = _smtpSettings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password)
            };

            try
            {
                await client.SendMailAsync(mail);
                _accountReactivationEmailSent(_logger, toEmail, null!);
            }
            catch (Exception ex)
            {
                _accountReactivationEmailFailed(_logger, toEmail, ex.Message, ex);
                throw;
            }
        }
    }
}