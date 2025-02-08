using System;

namespace BusInfo.Models.Exceptions
{
    public class InvalidUserIdException(string userId) : Exception($"User with ID '{userId}' does not exist")
    {
        public string UserId { get; } = userId;
        public InvalidUserIdException() : this("unknown")
        {
        }

        public InvalidUserIdException(string message, Exception innerException) : this(message)
        {
        }
    }
}
