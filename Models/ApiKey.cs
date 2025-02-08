using System;

namespace BusInfo.Models
{
    public class ApiKey
    {
        public string Key { get; set; } = string.Empty;
        public required string UserId { get; set; }
        public ApplicationUser? User { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}
