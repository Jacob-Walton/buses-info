using System;
using System.Runtime.Serialization;

namespace BusInfo.Models.Exceptions
{
    [Serializable]
    public class ConfigCatClientException : Exception
    {
        public ConfigCatClientException()
        {
        }

        public ConfigCatClientException(string message) : base(message)
        {
        }

        public ConfigCatClientException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}