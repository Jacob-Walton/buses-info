using System.IO;
using System.Reflection;
using System.Globalization;
using Microsoft.Extensions.Logging;
using System;

namespace BusInfo.Services
{
    public class EmailTemplateService(ILogger<EmailTemplateService> logger) : IEmailTemplateService
    {
        private readonly ILogger<EmailTemplateService> _logger = logger;
        private const string TemplateBasePath = "BusInfo.Templates.Email";

        private static readonly Action<ILogger, string, Exception> _templateNotFound =
            LoggerMessage.Define<string>(
                LogLevel.Error,
                new EventId(1, nameof(LoadTemplate)),
                "Email template not found: {Template}");

        private string LoadTemplate(string templateName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourcePath = $"{TemplateBasePath}.{templateName}.html";

            using Stream stream = assembly.GetManifestResourceStream(resourcePath);
            if (stream == null)
            {
                _templateNotFound(_logger, resourcePath, null);
                throw new FileNotFoundException($"Email template not found: {resourcePath}");
            }

            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }

        public string GetPasswordResetTemplate(string resetLink)
        {
            string template = LoadTemplate("PasswordReset");
            return template.Replace("{{resetLink}}", resetLink, StringComparison.Ordinal);
        }
        public string GetAccountDeletionTemplate(int gracePeriodDays)
        {
            string template = LoadTemplate("AccountDeletion");
            return template.Replace("{{gracePeriodDays}}", gracePeriodDays.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        public string GetAccountReactivationTemplate()
        {
            return LoadTemplate("AccountReactivation");
        }
    }
}
