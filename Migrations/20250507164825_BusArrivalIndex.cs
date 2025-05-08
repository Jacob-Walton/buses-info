using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusInfo.Migrations
{
    /// <inheritdoc />
    public partial class BusArrivalIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArrivalDate",
                table: "BusArrivals",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_BusArrivals_Service_Bay_ArrivalTime",
                table: "BusArrivals",
                columns: new[] { "Service", "Bay", "ArrivalTime" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BusArrivals_Service_Bay_ArrivalTime",
                table: "BusArrivals");

            migrationBuilder.DropColumn(
                name: "ArrivalDate",
                table: "BusArrivals");
        }
    }
}
