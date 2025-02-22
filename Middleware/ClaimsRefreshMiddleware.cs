using System.Threading.Tasks;
using BusInfo.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace BusInfo.Middleware
{
    public class ClaimsRefreshMiddleware
    {
        private readonly RequestDelegate _next;

        public ClaimsRefreshMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ClaimsRefreshService claimsRefreshService)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                if (claimsRefreshService.ShouldRefreshClaims(context.User))
                {
                    var newPrincipal = await claimsRefreshService.RefreshClaimsAsync(context.User);
                    await context.SignInAsync(newPrincipal);
                }
            }

            await _next(context);
        }
    }
}
