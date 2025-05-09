using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusInfo.Migrations
{
    /// <inheritdoc />
    public partial class RemoveArrivalDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpecificBuses",
                table: "DeviceRegistrations");

            migrationBuilder.DropColumn(
                name: "ArrivalDate",
                table: "BusArrivals");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpecificBuses",
                table: "DeviceRegistrations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArrivalDate",
                table: "BusArrivals",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
