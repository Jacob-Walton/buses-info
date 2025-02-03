using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusInfo.Services
{
    public interface IBusLaneService
    {
        Task<byte[]> GenerateBusLaneMapAsync(Dictionary<string, string> bayServiceMap);
    }
}
