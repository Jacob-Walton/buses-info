using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using BusInfo.Models;
using BusInfo.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BusInfo.Pages
{
    public class LoginModel(IUserService userService) : PageModel
    {
        private readonly IUserService _userService = userService;

        [BindProperty]
        public LoginInputModel LoginInput { get; set; } = new();

        public Uri? ReturnUrl { get; set; }

        [TempData]
        public bool ShowReactivationModal { get; set; }

        [TempData]
        public string PendingReactivationEmail { get; set; } = string.Empty;

        [BindProperty(SupportsGet = false)]
        public string? RequestVerificationToken { get; set; }

        [TempData]
        public bool IsSubmitting { get; set; }

        public IActionResult OnGet(Uri? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return Redirect("/");

            if (returnUrl != null && Url.IsLocalUrl(returnUrl.ToString()))
                ReturnUrl = returnUrl;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Uri? returnUrl = null)
        {
            if (IsSubmitting)
                return RedirectToPage();

            if (!ModelState.IsValid)
                return Page();

            try
            {
                IsSubmitting = true;

                ApplicationUser? user = await _userService.AuthenticateAsync(LoginInput.Username, LoginInput.Password);
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid username or password.");
                    return Page();
                }

                if (user.IsPendingDeletion)
                {
                    ShowReactivationModal = true;
                    PendingReactivationEmail = user.Email;
                    return RedirectToPage();
                }

                List<Claim> claims = [
                    new(ClaimTypes.Email, user.Email),
                    new(ClaimTypes.NameIdentifier, user.Id)
                ];

                if (user.IsAdmin)
                    claims.Add(new(ClaimTypes.Role, "Admin"));

                ClaimsIdentity identity = new(claims, "local");
                ClaimsPrincipal principal = new(identity);

                AuthenticationProperties authProperties = new()
                {
                    IsPersistent = LoginInput.RememberMe,
                    RedirectUri = returnUrl?.ToString() ?? "/"
                };

                await HttpContext.SignInAsync(principal, authProperties);

                return Url.IsLocalUrl(authProperties.RedirectUri) ? Redirect(authProperties.RedirectUri) : Redirect("/");
            }
            catch (InvalidOperationException)
            {
                ModelState.AddModelError(string.Empty, "Authentication service is unavailable.");
                return Page();
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "Invalid login credentials format.");
                return Page();
            }
            finally
            {
                IsSubmitting = false;
            }
        }

        public async Task<IActionResult> OnPostReactiveAccountAsync([FromForm] string email)
        {
            if (IsSubmitting)
                return RedirectToPage();

            try
            {
                IsSubmitting = true;

                bool success = await _userService.CancelAccountDeletionAsync(email, isEmail: true);
                if (!success)
                {
                    ModelState.AddModelError(string.Empty, "An error occurred while reactivating your account.");
                    return Page();
                }

                // Set success message and redirect to login page
                TempData["StatusMessage"] = "Your account has been reactivated. Please login to continue.";
                return RedirectToPage("/Login");
            }
            catch (InvalidOperationException)
            {
                ModelState.AddModelError(string.Empty, "Account reactivation service is unavailable.");
                return Page();
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "Invalid email format.");
                return Page();
            }
            finally
            {
                IsSubmitting = false;
            }
        }
    }

    public class LoginInputModel
    {
        [Required(ErrorMessage = "Username or Email is required")]
        [Display(Name = "Username or Email")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}