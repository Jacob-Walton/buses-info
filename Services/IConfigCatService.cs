using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BusInfo.Services
{
    public interface IConfigCatService
    {
        Task<bool> GetFlagValueAsync(ClaimsPrincipal user, string flagKey, bool defaultValue);
        Task<IDictionary<string, bool>> GetAllFeatureFlagsAsync(ClaimsPrincipal user);
    }
}