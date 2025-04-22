using System;
using System.Collections.Generic;

namespace BusInfo.Models
{
    public class BusRankingResponse
    {
        public Dictionary<string, BusRankingInfo> Rankings { get; init; } = [];
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class BusRankingInfo
    {
        public string Service { get; set; } = string.Empty;
        public int Rank { get; set; }
        public int Score { get; set; }
    }
}