using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BusInfo.Models
{
    public class BusStatusMongo
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("service")]
        public string Service { get; set; } = string.Empty;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        [BsonElement("arrivalTime")]
        public DateTime ArrivalTime { get; set; } = DateTime.MinValue;

        [BsonElement("bay")]
        public string Bay { get; set; } = string.Empty;

        [BsonElement("status")]
        public string Status { get; set; } = string.Empty;

        [BsonElement("dayOfWeek")]
        public int DayOfWeek { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("__v")]
        public int V { get; set; }
    }
}