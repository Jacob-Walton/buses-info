using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BusInfo.Models
{
    public class BusPredictionResponse
    {
        public Dictionary<string, PredictionInfo> Predictions { get; init; } = [];
        public string LastUpdated { get; set; } = string.Empty;
    }

    public class PredictionInfo
    {
        private static readonly ReadOnlyCollection<BayPrediction> EmptyCollection =
            new([]);

        public ReadOnlyCollection<BayPrediction> Predictions { get; init; } = EmptyCollection;
    }
}