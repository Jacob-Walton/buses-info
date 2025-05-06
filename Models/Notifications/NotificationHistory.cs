using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusInfo.Models.Notifications
{
    /// <summary>
    /// Represents a record of a notification that was sent to users
    /// </summary>
    public class NotificationHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = "";
        
        [Required]
        [MaxLength(1000)]
        public string Body { get; set; } = "";
        
        [Required]
        [MaxLength(50)]
        public string NotificationType { get; set; } = "";
        
        [Required]
        [MaxLength(1000)]
        public string Recipients { get; set; } = "";
        
        public int DevicesReached { get; set; }
        
        public DateTime SentAt { get; set; }
        
        public string? SentBy { get; set; }
    }
}
