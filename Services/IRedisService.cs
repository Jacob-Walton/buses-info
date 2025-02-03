#nullable enable

using System;
using System.Threading.Tasks;

namespace BusInfo.Services
{
    public interface IRedisService
    {
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
        Task<T?> GetAsync<T>(string key);
        Task RemoveAsync(string key);
        Task<bool> KeyExistsAsync(string key);
        Task<TimeSpan?> GetTtlAsync(string key);
    }
}