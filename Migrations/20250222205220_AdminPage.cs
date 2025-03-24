using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BusInfo.Migrations
{
    /// <inheritdoc />
    public partial class AdminPage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiKeys_Users_UserId",
                table: "ApiKeys");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AdminSettings",
                table: "AdminSettings");

            migrationBuilder.RenameColumn(
                name: "MaintenanceWindow",
                table: "AdminSettings",
                newName: "UserActivityLogRetentionDays");

            migrationBuilder.RenameColumn(
                name: "AutomaticMaintenance",
                table: "AdminSettings",
                newName: "SendApiKeyExpirationWarnings");

            migrationBuilder.RenameColumn(
                name: "ArchivedDataRetentionDays",
                table: "AdminSettings",
                newName: "PasswordResetTokenExpiryHours");

            migrationBuilder.RenameColumn(
                name: "ApiRateLimit",
                table: "AdminSettings",
                newName: "MaxLoginAttempts");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsed",
                table: "ApiKeys",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Id",
                table: "AdminSettings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ApiKeyExpirationWarningDays",
                table: "AdminSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ApiLogRetentionDays",
                table: "AdminSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BusArrivalDataRetentionDays",
                table: "AdminSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefaultApiRateLimit",
                table: "AdminSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LockoutDurationMinutes",
                table: "AdminSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "MaintenanceEndTime",
                table: "AdminSettings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaintenanceMessage",
                table: "AdminSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MaintenanceMode",
                table: "AdminSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MaintenanceStartTime",
                table: "AdminSettings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxApiKeysPerUser",
                table: "AdminSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequireApiKeyApproval",
                table: "AdminSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireTwoFactorForAdmin",
                table: "AdminSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SendApiKeyApprovalEmails",
                table: "AdminSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_AdminSettings",
                table: "AdminSettings",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "AdminActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Icon = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    AdminId = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminActivities_Users_AdminId",
                        column: x => x.AdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AdminActivities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminActivities_AdminId",
                table: "AdminActivities",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminActivities_Timestamp",
                table: "AdminActivities",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AdminActivities_Type",
                table: "AdminActivities",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_AdminActivities_UserId",
                table: "AdminActivities",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiKeys_Users_UserId",
                table: "ApiKeys",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiKeys_Users_UserId",
                table: "ApiKeys");

            migrationBuilder.DropTable(
                name: "AdminActivities");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AdminSettings",
                table: "AdminSettings");

            migrationBuilder.DropColumn(
                name: "LastUsed",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "AdminSettings");

            migrationBuilder.DropColumn(
                name: "ApiKeyExpirationWarningDays",
                table: "AdminSettings");

            migrationBuilder.DropColumn(
                name: "ApiLogRetentionDays",
                table: "AdminSettings");

            migrationBuilder.DropColumn(
                name: "BusArrivalDataRetentionDays",
                table: "AdminSettings");

            migrationBuilder.DropColumn(
                name: "DefaultApiRateLimit",
                table: "AdminSettings");

            migrationBuilder.DropColumn(
                name: "LockoutDurationMinutes",
                table: "AdminSettings");

            migrationBuilder.DropColumn(
                name: "MaintenanceEndTime",
                table: "AdminSettings");

            migrationBuilder.DropColumn(
                name: "MaintenanceMessage",
                table: "AdminSettings");

            migrationBuilder.DropColumn(
                name: "MaintenanceMode",
                table: "AdminSettings");

            migrationBuilder.DropColumn(
                name: "MaintenanceStartTime",
                table: "AdminSettings");

            migrationBuilder.DropColumn(
                name: "MaxApiKeysPerUser",
                table: "AdminSettings");

            migrationBuilder.DropColumn(
                name: "RequireApiKeyApproval",
                table: "AdminSettings");

            migrationBuilder.DropColumn(
                name: "RequireTwoFactorForAdmin",
                table: "AdminSettings");

            migrationBuilder.DropColumn(
                name: "SendApiKeyApprovalEmails",
                table: "AdminSettings");

            migrationBuilder.RenameColumn(
                name: "UserActivityLogRetentionDays",
                table: "AdminSettings",
                newName: "MaintenanceWindow");

            migrationBuilder.RenameColumn(
                name: "SendApiKeyExpirationWarnings",
                table: "AdminSettings",
                newName: "AutomaticMaintenance");

            migrationBuilder.RenameColumn(
                name: "PasswordResetTokenExpiryHours",
                table: "AdminSettings",
                newName: "ArchivedDataRetentionDays");

            migrationBuilder.RenameColumn(
                name: "MaxLoginAttempts",
                table: "AdminSettings",
                newName: "ApiRateLimit");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AdminSettings",
                table: "AdminSettings",
                column: "LastModified");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiKeys_Users_UserId",
                table: "ApiKeys",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
