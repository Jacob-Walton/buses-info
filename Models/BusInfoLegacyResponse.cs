using System.Collections.Generic;
using Newtonsoft.Json;

namespace BusInfo.Models
{
    public class BusInfoLegacyResponse
    {
        [JsonProperty("busData")]
        public Dictionary<string, string> BusData { get; set; } = [];

        [JsonProperty("lastUpdated")]
        public string LastUpdated { get; set; } = string.Empty;
    }
}