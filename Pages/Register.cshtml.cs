using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using BusInfo.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BusInfo.Pages
{
    public class RegisterModel(IUserService userService) : PageModel
    {
        private readonly IUserService _userService = userService;

        [BindProperty]
        public RegisterInputModel RegisterInput { get; set; } = new();

        public IActionResult OnGet()
        {
            return User.Identity?.IsAuthenticated == true ? Redirect("/") : Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            try
            {
                (bool Success, string Message) = await _userService.RegisterUserAsync(
                    RegisterInput.Email,
                    RegisterInput.Password,
                    RegisterInput.AgreeToTerms);

                if (!Success)
                {
                    ModelState.AddModelError(string.Empty, Message);
                    return Page();
                }

                return Redirect("/login");
            }
            catch (InvalidOperationException)
            {
                ModelState.AddModelError(string.Empty, "Registration service is currently unavailable.");
                return Page();
            }
            catch (Exception)
            {
                throw; // Rethrow the exception to be handled by global error handler
            }
        }
    }

    public class RegisterInputModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(24, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long and no more than 24 characters long")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "You must agree to the terms and conditions")]
        [Display(Name = "I agree to the terms and conditions")]
        public bool AgreeToTerms { get; set; }
    }
}