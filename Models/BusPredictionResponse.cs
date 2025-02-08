using System.Collections.Generic;

namespace BusInfo.Models
{
    public class BusPredictionResponse
    {
        public Dictionary<string, PredictionInfo> Predictions { get; set; } = [];
        public string LastUpdated { get; set; } = string.Empty;
    }

    public class PredictionInfo
    {
        public List<BayPrediction> Predictions { get; set; } = [];
    }
}