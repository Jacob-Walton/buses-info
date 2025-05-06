using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BusInfo.Models.Notifications
{
    public enum NotificationType
    {
        General = 0,
        BusArrival = 1,
        ServiceUpdate = 2
    }

    public class PushNotification
    {
        /// <summary>
        /// Unique identifier for the notification
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Notification title
        /// </summary>
        [Required]
        public string Title { get; set; } = "";

        /// <summary>
        /// Notification body text
        /// </summary>
        [Required]
        public string Body { get; set; } = "";

        /// <summary>
        /// Type of notification
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public NotificationType Type { get; set; } = NotificationType.General;

        /// <summary>
        /// Sound to play (default is "default")
        /// </summary>
        public string? Sound { get; set; } = "default";

        /// <summary>
        /// Additional data to include with the notification
        /// </summary>
        public Dictionary<string, string>? Data { get; set; }
    }
}
