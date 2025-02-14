using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BusInfo.Models
{
    public enum AuthProvider
    {
        Local,      // Email/Password
        Google,     // Google OAuth
        Microsoft   // Microsoft OAuth
    }

    public class ApplicationUser
    {
        public ApplicationUser()
        {
            Id = Guid.NewGuid().ToString();
            RecoveryCodes = [];
            PreferredRoutes = [];
        }
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Email { get; set; } = string.Empty;
        public bool HasRequestedApiAccess { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastLoginAt { get; set; }
        public List<string> PreferredRoutes { get; set; }
        public bool ShowPreferredRoutesFirst { get; set; } = true;
        public bool EnableEmailNotifications { get; set; } = true;
        public string PasswordHash { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;

        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiry { get; set; }

        public bool IsEmailVerified { get; set; }
        public string? EmailVerificationToken { get; set; }
        public DateTime? EmailVerificationTokenExpiry { get; set; }
        public DateTime? LastPasswordChangeDate { get; set; }
        public bool RequiresPasswordChange { get; set; }

        public bool TwoFactorEnabled { get; set; }
        public string? TwoFactorSecret { get; set; }
        public List<string> RecoveryCodes { get; set; }
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public bool IsLocked => LockoutEnd.HasValue && LockoutEnd.Value > DateTime.UtcNow;

        public DateTime? DeletedAt { get; set; }
        public bool IsPendingDeletion => DeletedAt.HasValue && DeletedAt.Value.AddDays(30) > DateTime.UtcNow;
        public DateTime? DeletionConfirmedAt { get; set; }
        public string? DeletionReason { get; set; }

        public bool HasAgreedToTerms { get; set; }
        public DateTime? TermsAgreedAt { get; set; }

        public ApiKey? ActiveApiKey { get; set; }

        public AuthProvider AuthProvider { get; set; } = AuthProvider.Local;
        public string? ExternalId { get; set; }
    }
}