using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BusInfo.Data;
using BusInfo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BusInfo.Authentication.Authorization
{
    public class AdminRequirement : IAuthorizationRequirement;

    public class AdminAuthorizationHandler(ApplicationDbContext context) : AuthorizationHandler<AdminRequirement>
    {
        private readonly ApplicationDbContext _context = context;

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            AdminRequirement requirement)
        {
            ArgumentNullException.ThrowIfNull(context, nameof(context));

            ClaimsPrincipal user = context.User;
            if (user == null) return;

            string email = user.FindFirst("preferred_username")?.Value
                ?? user.FindFirst(ClaimTypes.Email)?.Value
                ?? string.Empty;

            if (string.IsNullOrEmpty(email)) return;

            ApplicationUser? dbUser = await _context!.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email);

            if (dbUser?.IsAdmin == true) context.Succeed(requirement);
        }
    }
}