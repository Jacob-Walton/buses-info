using System;
using System.Collections.Generic;

namespace BusInfo.Models.Admin
{
    public class AdminActivity
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Icon { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? AdminId { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = [];
    }
}