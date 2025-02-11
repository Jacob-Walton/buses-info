using System.Collections.Generic;

namespace BusInfo.Models
{
    public class PredictionInfo
    {
        public List<BayPrediction> Predictions { get; set; } = [];
        public int OverallConfidence { get; set; }
    }
}
