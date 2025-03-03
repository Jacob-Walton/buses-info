using System.Threading.Tasks;
using BusInfo.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace BusInfo.Middleware
{
    public class ClaimsRefreshMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        public async Task InvokeAsync(HttpContext context, ClaimsRefreshService claimsRefreshService)
        {
            if (context.User.Identity?.IsAuthenticated == true && claimsRefreshService.ShouldRefreshClaims(context.User))
            {
                System.Security.Claims.ClaimsPrincipal newPrincipal = await claimsRefreshService.RefreshClaimsAsync(context.User);
                await context.SignInAsync(newPrincipal);
            }

            await _next(context);
        }
    }
}
