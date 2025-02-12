using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using BusInfo.Data;
using BusInfo.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace BusInfo.Authentication
{
    public class ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApplicationDbContext context) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        private const string API_KEY_HEADER = "X-Api-Key";
        private readonly ApplicationDbContext _context = context;

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Cookies.ContainsKey("BusInfo.Auth"))
            {
                return AuthenticateResult.NoResult();
            }

            if (!Request.Headers.TryGetValue(API_KEY_HEADER, out StringValues apiKeyHeaderValues))
            {
                return AuthenticateResult.Fail("Missing API key");
            }

            string? providedApiKey = apiKeyHeaderValues.FirstOrDefault();

            if (string.IsNullOrEmpty(providedApiKey))
            {
                return AuthenticateResult.Fail("Invalid API key");
            }

            ApplicationUser? user = await _context.ApiKeys
                .Where(k => k.Key == providedApiKey)
                .Select(k => k.User)
                .FirstOrDefaultAsync();

            if (user == null)
                return AuthenticateResult.Fail("Invalid API key");

            Claim[] claims =
            [
                new(ClaimTypes.Name, user.Email),
                new(ClaimTypes.NameIdentifier, user.Id),
            ];

            ClaimsIdentity identity = new(claims, Scheme.Name);
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
    }
}