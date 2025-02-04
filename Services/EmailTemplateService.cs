using System.IO;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;

namespace BusInfo.Services
{
    public class EmailTemplateService(ILogger<EmailTemplateService> logger) : IEmailTemplateService
    {
        private readonly ILogger<EmailTemplateService> _logger = logger;
        private readonly string _templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "Email");

        private static readonly Action<ILogger, string, Exception?> LogTemplateNotFound =
            LoggerMessage.Define<string>(
                LogLevel.Error,
                new EventId(1, nameof(LogTemplateNotFound)),
                "Email template not found: {Template}");

        private string LoadTemplate(string templateName)
        {
            string filePath = Path.Combine(_templatePath, $"{templateName}.html");

            if (!File.Exists(filePath))
            {
                LogTemplateNotFound(_logger, templateName, null);
                throw new FileNotFoundException($"Email template not found: {templateName}");
            }

            return File.ReadAllText(filePath);
        }

        public string GetPasswordResetTemplate(string resetLink)
        {
            string template = LoadTemplate("PasswordReset");
            return template.Replace("{{resetLink}}", resetLink, StringComparison.InvariantCulture);
        }

        public string GetAccountDeletionTemplate(int gracePeriodDays)
        {
            string template = LoadTemplate("AccountDeletion");
            return template.Replace("{{gracePeriodDays}}", gracePeriodDays.ToString(CultureInfo.InvariantCulture), StringComparison.InvariantCulture);
        }

        public string GetAccountReactivationTemplate()
        {
            return LoadTemplate("AccountReactivation");
        }
    }
}
