using System;
using Npgsql;

namespace BusInfo.Data
{
    public static class NpgsqlConfiguration
    {
        public static void Configure()
        {
        }

        public static Action<NpgsqlDataSourceBuilder> ConfigureDataSource => builder => builder.EnableDynamicJson();
    }
}
