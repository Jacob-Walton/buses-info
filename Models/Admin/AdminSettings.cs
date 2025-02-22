using System;

namespace BusInfo.Models
{
    public class AdminSettings
    {
        public string Id { get; set;} = Guid.NewGuid().ToString();
        public DateTime LastModified { get; set; }
        public string ModifiedBy { get; set; } = string.Empty;

        // API Settings
        public int DefaultApiRateLimit { get; set; } = 1000;
        public int ApiKeyExpirationDays { get; set; } = 365;
        public bool RequireApiKeyApproval { get; set; } = true;
        public int MaxApiKeysPerUser { get; set; } = 3;

        // Data Retention
        public int BusArrivalDataRetentionDays { get; set; } = 90;
        public int ApiLogRetentionDays { get; set; } = 30;
        public int UserActivityLogRetentionDays { get; set; } = 30;

        // Email Settings
        public bool SendApiKeyApprovalEmails { get; set; } = true;
        public bool SendApiKeyExpirationWarnings { get; set; } = true;
        public int ApiKeyExpirationWarningDays { get; set; } = 14;

        // Security Settings
        public int MaxLoginAttempts { get; set; } = 5;
        public int LockoutDurationMinutes { get; set; } = 30;
        public bool RequireTwoFactorForAdmin { get; set; } = true;
        public int PasswordResetTokenExpiryHours { get; set; } = 24;

        // Maintenance Settings
        public bool MaintenanceMode { get; set; }
        public string? MaintenanceMessage { get; set; }
        public DateTime? MaintenanceStartTime { get; set; }
        public DateTime? MaintenanceEndTime { get; set; }
    }
}