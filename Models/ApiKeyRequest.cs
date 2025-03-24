using System;

namespace BusInfo.Models
{
    public class ApiKeyRequest
    {
        public int Id { get; set; }
        public required string UserId { get; set; }
        public ApplicationUser? User { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string IntendedUse { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public string? RejectionReason { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool DismissedByUser { get; set; }
        public DateTime? DismissedAt { get; set; }

        /// <summary>
        /// Add missing properties used in AdminApiController
        /// </summary>
        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNotes { get; set; }
    }
}