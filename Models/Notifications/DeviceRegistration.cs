using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusInfo.Models.Notifications
{
    public class DeviceRegistration
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        public string DeviceToken { get; set; }

        [Required]
        public string DeviceType { get; set; }

        public string AppVersion { get; set; }

        public string OsVersion { get; set; }

        public bool BusArrivalNotifications { get; set; } = true;

        public bool ServiceUpdateNotifications { get; set; } = true;

        public string SpecificBuses { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;
    }

    public class DeviceRegistrationRequest
    {
        [Required]
        public string UserId { get; set; }

        [Required]
        public string DeviceToken { get; set; }

        [Required]
        public string DeviceType { get; set; }

        public string AppVersion { get; set; }

        public string OsVersion { get; set; }

        public NotificationSettings NotificationSettings { get; set; } = new NotificationSettings();
    }

    public class NotificationSettings
    {
        public bool BusArrivalNotifications { get; set; } = true;

        public bool ServiceUpdateNotifications { get; set; } = true;

        public string[] SpecificBuses { get; set; } = Array.Empty<string>();
    }
}
