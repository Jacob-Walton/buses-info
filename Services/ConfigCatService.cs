using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BusInfo.Models.Exceptions;
using ConfigCat.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.Configuration;

namespace BusInfo.Services
{
    public class ConfigCatService : IConfigCatService
    {
        private static readonly Action<ILogger, Exception> LogConfigCatError =
            LoggerMessage.Define(Microsoft.Extensions.Logging.LogLevel.Error, new EventId(1, nameof(LogConfigCatError)), "Error getting flag value from ConfigCat");

        private readonly IConfigCatClient _configCatClient;
        private readonly ILogger<ConfigCatService> _logger;

        public ConfigCatService(IConfiguration configuration, ILogger<ConfigCatService> logger)
        {
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

            string sdkKey = configuration["ConfigCat:SdkKey"] ?? throw new InvalidConfigurationException("ConfigCat:SdkKey is not configured");
            _logger = logger;
            _configCatClient = ConfigCatClient.Get(sdkKey,
                options =>
                    options.PollingMode = PollingModes.AutoPoll(pollInterval: TimeSpan.FromSeconds(30)));
        }

        public async Task<bool> GetFlagValueAsync(ClaimsPrincipal user, string flagKey, bool defaultValue)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(user, nameof(user));

                User configCatUser = new(user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous")
                {
                    Custom = {
                        { "Role", user?.FindFirst(ClaimTypes.Role)?.Value ?? "Anonymous" }
                    }
                };

                return await _configCatClient.GetValueAsync(flagKey, defaultValue, configCatUser);
            }
            catch (ConfigCatClientException ex)
            {
                LogConfigCatError(_logger, ex);
                return defaultValue;
            }
        }

        public async Task<IDictionary<string, bool>> GetAllFeatureFlagsAsync(ClaimsPrincipal user)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(user, nameof(user));

                Dictionary<string, bool> flags = [];
                User configCatUser = new(user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous")
                {
                    Custom = {
                        { "Role", user?.FindFirst(ClaimTypes.Role)?.Value ?? "Anonymous" }
                    }
                };

                foreach (string flag in await _configCatClient.GetAllKeysAsync())
                {
                    flags.Add(flag, await _configCatClient.GetValueAsync(flag, false, configCatUser));
                }

                return flags;
            }
            catch (ConfigCatClientException ex)
            {
                LogConfigCatError(_logger, ex);
                return new Dictionary<string, bool>();
            }
        }
    }
}