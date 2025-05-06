using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BusInfo.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    DeviceToken = table.Column<string>(type: "text", nullable: false),
                    DeviceType = table.Column<string>(type: "text", nullable: false),
                    AppVersion = table.Column<string>(type: "text", nullable: false),
                    OsVersion = table.Column<string>(type: "text", nullable: false),
                    BusArrivalNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    ServiceUpdateNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    SpecificBuses = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceRegistrations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceRegistrations_DeviceToken",
                table: "DeviceRegistrations",
                column: "DeviceToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceRegistrations");
        }
    }
}
