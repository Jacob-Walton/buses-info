#nullable enable

using System;

namespace BusInfo.Models
{
    public class ApiKeyRequest
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? IntendedUse { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNotes { get; set; }
    }
}