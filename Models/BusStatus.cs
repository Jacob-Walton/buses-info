#nullable enable

using System;
using System.Collections.ObjectModel;

namespace BusInfo.Models
{
    public class BusStatus
    {
        public string? Status { get; set; }
        public string? Bay { get; set; } = string.Empty;
        public Collection<BayPrediction>? PredictedBays { get; set; }
        public int PredictionConfidence { get; set; }
    }
}