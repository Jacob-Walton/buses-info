using System;

namespace BusInfo.Models.Admin
{
    public class ApiKeyInfo
    {
        public string Key { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsActive { get; set; }
        public int RequestsToday { get; set; }
        public int TotalRequests { get; set; }
        public DateTime LastUsed { get; set; }
    }
}