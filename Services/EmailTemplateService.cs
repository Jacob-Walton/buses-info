using System;
using System.IO;
using System.Reflection;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace BusInfo.Services
{
    public class EmailTemplateService(ILogger<EmailTemplateService> logger) : IEmailTemplateService
    {
        private readonly ILogger<EmailTemplateService> _logger = logger;
        private readonly Assembly _assembly = Assembly.GetExecutingAssembly();

        private static readonly Action<ILogger, string, Exception?> LogTemplateNotFound =
            LoggerMessage.Define<string>(
                LogLevel.Error,
                new EventId(1, nameof(LogTemplateNotFound)),
                "Email template not found: {Template}");

        private string LoadTemplate(string templateName)
        {
            string cleanName = Path.GetFileNameWithoutExtension(templateName);
            string resourcePath = $"{typeof(EmailTemplateService).Namespace}.Templates.Email.{cleanName}.html";
            
            using Stream? stream = _assembly.GetManifestResourceStream(resourcePath);
            
            if (stream == null)
            {
                LogTemplateNotFound(_logger, templateName, null);
                throw new InvalidOperationException($"Email template not found: {templateName}");
            }

            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
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
