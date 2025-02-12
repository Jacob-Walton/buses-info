using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BusInfo.Models
{
    public class BusPredictionResponse
    {
        public Dictionary<string, PredictionInfo> Predictions { get; init; } = [];
        public string LastUpdated { get; set; } = string.Empty;
    }
}