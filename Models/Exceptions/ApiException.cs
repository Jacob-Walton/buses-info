using System;
using System.Net;

namespace BusInfo.Exceptions
{
    public sealed class ApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public ApiException() : base()
        {
        }

        public ApiException(string message, HttpStatusCode statusCode) : base(message)
        {
            StatusCode = statusCode;
        }

        public ApiException(string message, HttpStatusCode statusCode, Exception innerException) : base(message, innerException)
        {
            StatusCode = statusCode;
        }

        public ApiException(string message) : base(message)
        {
        }

        public ApiException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}