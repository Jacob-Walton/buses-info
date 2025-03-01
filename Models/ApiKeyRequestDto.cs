using System.ComponentModel.DataAnnotations;

namespace BusInfo.Models
{
    public class ApiKeyRequestDto
    {
        [Required(ErrorMessage = "The reason field is required")]
        public string Reason { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "The intended use field is required")]
        public string IntendedUse { get; set; } = string.Empty;
    }
}
