using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusInfo.Migrations
{
    /// <inheritdoc />
    public partial class AddedAuthProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BusArrivals_Service_ArrivalTime",
                table: "BusArrivals");

            migrationBuilder.AddColumn<int>(
                name: "AuthProvider",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthProvider",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_BusArrivals_Service_ArrivalTime",
                table: "BusArrivals",
                columns: ["Service", "ArrivalTime"]);
        }
    }
}
