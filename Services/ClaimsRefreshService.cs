using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BusInfo.Models;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace BusInfo.Services
{
    public class ClaimsRefreshService(IUserService userService)
    {
        private readonly IUserService _userService = userService;

        public bool ShouldRefreshClaims(ClaimsPrincipal principal)
        {
            ArgumentNullException.ThrowIfNull(principal, nameof(principal));

            Claim lastRefreshClaim = principal.FindFirst("claims_last_refresh");

            return lastRefreshClaim == null || !DateTime.TryParse(lastRefreshClaim.Value, out DateTime lastRefresh) || DateTime.UtcNow.Subtract(lastRefresh) > TimeSpan.FromMinutes(5);
        }

        public async Task<ClaimsPrincipal> RefreshClaimsAsync(ClaimsPrincipal principal)
        {
            ArgumentNullException.ThrowIfNull(principal, nameof(principal));

            string userId = principal.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (string.IsNullOrEmpty(userId))
                return principal;

            ApplicationUser user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
                return principal;

            List<Claim> claims = [
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("claims_last_refresh", DateTime.UtcNow.ToString("o"))
            ];

            if (user.IsAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }

            ClaimsIdentity identity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            return new ClaimsPrincipal(identity);
        }
    }
}