using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BusInfo.Controllers
{
    [AllowAnonymous]
    public class ExternalLoginController(ILogger<ExternalLoginController> logger) : ControllerBase
    {
        private static readonly Action<ILogger, string, string, Exception?> LogExternalAuthError =
            LoggerMessage.Define<string, string>(
                LogLevel.Warning,
                new EventId(1, nameof(LogExternalAuthError)),
                "External authentication error: {Error} ({Subcode})");

        private readonly ILogger<ExternalLoginController> _logger = logger;

        [HttpGet("/signin/{provider}")]
        public IActionResult SignIn(string provider, string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            AuthenticationProperties properties = new()
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

        [HttpGet("/signin-callback")]
        public IActionResult SignInCallback()
        {
            if (Request.Query.ContainsKey("error"))
            {
                string error = Request.Query["error"].ToString();
                string errorSubcode = Request.Query["error_subcode"].ToString();

                LogExternalAuthError(_logger, error, errorSubcode, null);

                string message = error == "access_denied" && errorSubcode == "cancel"
                    ? "You cancelled the login process."
                    : "An error occurred during external authentication.";

                HttpContext.Session.SetString("LoginErrorMessage", message);
                return LocalRedirect("/login");
            }

            return LocalRedirect("/");
        }

        public IActionResult SignIn(string provider, Uri? returnUrl = null)
        {
            throw new NotImplementedException();
        }
    }
}