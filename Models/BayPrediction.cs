namespace BusInfo.Models
{
    public class BayPrediction
    {
        public int Id { get; set; }
        public string? Bay { get; set; }
        public int Probability { get; set; }
    }
}