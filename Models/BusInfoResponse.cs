using System.Collections.Generic;
using Newtonsoft.Json;

namespace BusInfo.Models
{
    public class BusInfoResponse
    {
        [JsonProperty("busData")]
        public Dictionary<string, BusStatus> BusData { get; set; } = [];

        [JsonProperty("lastUpdated")]
        public string LastUpdated { get; set; } = string.Empty;
    }
}