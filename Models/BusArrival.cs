using System;

namespace BusInfo.Models
{
    public class BusArrival
    {
        public int Id { get; set; }
        public string Service { get; set; } = string.Empty;
        public string Bay { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime ArrivalTime { get; set; }
        public int DayOfWeek { get; set; }
        public double Temperature { get; set; }
        public string Weather { get; set; } = string.Empty;
        public int WeekOfYear { get; set; }
        public bool IsSchoolTerm { get; set; }
    }
}
