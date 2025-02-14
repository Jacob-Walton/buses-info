using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BusInfo.Controllers
{
    [Route("/")]
    [AllowAnonymous]
    public class ExternalLoginController : ControllerBase
    {
        private readonly ILogger<ExternalLoginController> _logger;

        public ExternalLoginController(ILogger<ExternalLoginController> logger)
        {
            _logger = logger;
        }

        [HttpGet("signin/{provider}")]
        public IActionResult SignIn(string provider, string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            
            var properties = new AuthenticationProperties
            {
                RedirectUri = "/signin-callback",
                Items = 
                {
                    { "returnUrl", returnUrl },
                    { "provider", provider }
                }
            };

            return Challenge(properties, provider);
        }

        [HttpGet("signin-callback")]
        public IActionResult SignInCallback()
        {
            if (Request.Query.ContainsKey("error"))
            {
                string error = Request.Query["error"].ToString();
                string errorSubcode = Request.Query["error_subcode"].ToString();

                _logger.LogWarning("External authentication error: {Error} ({Subcode})", error, errorSubcode);

                string message = error == "access_denied" && errorSubcode == "cancel"
                    ? "You cancelled the login process."
                    : "An error occurred during external authentication.";

                HttpContext.Session.SetString("LoginErrorMessage", message);
                return LocalRedirect("/login");
            }

            return LocalRedirect("/");
        }
    }
}