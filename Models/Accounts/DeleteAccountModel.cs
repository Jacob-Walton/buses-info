using System.ComponentModel.DataAnnotations;

namespace BusInfo.Models.Accounts
{
    public class DeleteAccountModel
    {
        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; } = string.Empty;
    }
}
