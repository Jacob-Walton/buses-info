using System;

namespace BusInfo.Models.Admin
{
    public class AdminUserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastLoginAt { get; set; }
        public bool IsAdmin { get; set; }
        public bool HasApiAccess { get; set; }
        public bool IsPendingDeletion { get; set; }
        public int PreferredRoutesCount { get; set; }
        public int FailedLoginAttempts { get; set; }
        public bool IsLocked { get; set; }
    }
}