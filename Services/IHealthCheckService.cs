using System.Threading.Tasks;
using BusInfo.Models.Admin;

namespace BusInfo.Services
{
    public interface IHealthCheckService
    {
        Task<SystemHealth> GetSystemHealthAsync();
    }
}