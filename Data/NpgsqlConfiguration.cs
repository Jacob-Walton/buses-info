using Npgsql;

namespace BusInfo.Data
{
    public static class NpgsqlConfiguration
    {
        public static void Configure()
        {
            // Enable dynamic JSON serialization for Dictionary<string, string> to jsonb conversion
            NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson();
        }
    }
}
