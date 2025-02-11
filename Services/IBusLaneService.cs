using System.Collections.Generic;
using System.Threading.Tasks;
using BusInfo.Models;

namespace BusInfo.Services
{
    public interface IBusLaneService
    {
        Task<byte[]> GenerateBusLaneMapAsync(Dictionary<string, string> bayServiceMap);
    }
}
