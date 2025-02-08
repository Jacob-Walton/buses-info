using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using BusInfo.Data;
using BusInfo.Models;
using BusInfo.Models.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BusInfo.Services
{
    public class ApiKeyGenerator(ApplicationDbContext context) : IApiKeyGenerator
    {
        private const int RANDOM_BYTES_LENGTH = 16; // 128 bits of entropy
        private const string VALID_CHARS = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        private readonly ApplicationDbContext _context = context;

        public async Task<string> GenerateApiKeyAsync(string userId)
        {
            if (!await _context.Users.AnyAsync(u => u.Id == userId))
            {
                throw new InvalidUserIdException(userId);
            }

            string key = GenerateKey();

            ApiKey apiKey = new()
            {
                Key = key,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                UserId = userId
            };

            _context.ApiKeys.Add(apiKey);
            await _context.SaveChangesAsync();

            return key;
        }

        public string GenerateApiKey(string userId)
        {
            if (!_context.Users.Any(u => u.Id == userId))
            {
                throw new InvalidUserIdException(userId);
            }

            string key = GenerateKey();

            ApiKey apiKey = new()
            {
                Key = key,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                UserId = userId
            };

            _context.ApiKeys.Add(apiKey);
            _context.SaveChanges();

            return key;
        }

        private static string GenerateKey()
        {
            string timestamp = DateTimeOffset.UtcNow.ToString("yyMMdd", CultureInfo.InvariantCulture);
            string entropy = GenerateSecureRandomString(RANDOM_BYTES_LENGTH);
            string baseKey = $"{timestamp}{entropy}";
            char checkDigit = CalculateLuhnDigit(baseKey);
            return FormatWithHyphens($"RB{baseKey}{checkDigit}");
        }

        private static string FormatWithHyphens(string key)
        {
            return $"{key[..2]}-{key[2..8]}-{key[8..12]}-{key[12..16]}-{key[16..20]}-{key[20..24]}-{key[24..]}";
        }

        private static string RemoveHyphens(string key)
        {
            return key.Replace("-", string.Empty, StringComparison.InvariantCulture);
        }

        public bool ValidateApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey)) return false;

            apiKey = RemoveHyphens(apiKey);

            if (apiKey.Length != 25) return false;
            if (!apiKey.StartsWith("RB", StringComparison.OrdinalIgnoreCase)) return false;

            try
            {
                string luhnDigit = apiKey[2..];
                return IsValidLuhn(luhnDigit);
            }
            catch (FormatException)
            {
                return false;
            }
            catch
            {
                throw;
            }
        }

        private static string GenerateSecureRandomString(int byteLength)
        {
            byte[] bytes = new byte[byteLength];
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);

            char[] chars = new char[byteLength];
            int cursor = 0;

            for (int i = 0; i < byteLength; i++)
            {
                int value;
                do
                {
                    if (cursor >= bytes.Length)
                    {
                        rng.GetBytes(bytes);
                        cursor = 0;
                    }
                    value = bytes[cursor++];
                }
                while (value >= (256 - (256 % VALID_CHARS.Length)));

                chars[i] = VALID_CHARS[value % VALID_CHARS.Length];
            }

            return new string(chars);
        }

        private static char CalculateLuhnDigit(string input)
        {
            int sum = 0;
            bool alternate = false;

            for (int i = input.Length - 1; i >= 0; i--)
            {
                int n = VALID_CHARS.IndexOf(char.ToUpperInvariant(input[i]), StringComparison.Ordinal);
                if (n == -1) n = int.Parse(input[i].ToString(), CultureInfo.InvariantCulture);

                if (alternate)
                {
                    n *= 2;
                    if (n > 31) n = (n % 32) + 1;
                }

                sum += n;
                alternate = !alternate;
            }

            int checkDigitValue = (32 - (sum % 32)) % 32;
            return VALID_CHARS[checkDigitValue];
        }

        private static bool IsValidLuhn(string input)
        {
            return !string.IsNullOrEmpty(input) && CalculateLuhnDigit(input[..^1]) == input[^1];
        }
    }
}