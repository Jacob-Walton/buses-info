using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusInfo.Migrations
{
    /// <inheritdoc />
    public partial class ApiKeyRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "IntendedUse",
                table: "ApiKeyRequests",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DismissedAt",
                table: "ApiKeyRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DismissedByUser",
                table: "ApiKeyRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "ApiKeyRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ApiKeyRequests",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DismissedAt",
                table: "ApiKeyRequests");

            migrationBuilder.DropColumn(
                name: "DismissedByUser",
                table: "ApiKeyRequests");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "ApiKeyRequests");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ApiKeyRequests");

            migrationBuilder.AlterColumn<string>(
                name: "IntendedUse",
                table: "ApiKeyRequests",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
