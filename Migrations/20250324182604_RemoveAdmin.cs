using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BusInfo.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminActivities");

            migrationBuilder.DropTable(
                name: "AdminSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdminId = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Icon = table.Column<string>(type: "text", nullable: false),
                    Metadata = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "AdminSettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApiKeyExpirationDays = table.Column<int>(type: "integer", nullable: false),
                    ApiKeyExpirationWarningDays = table.Column<int>(type: "integer", nullable: false),
                    ApiLogRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    BusArrivalDataRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    DefaultApiRateLimit = table.Column<int>(type: "integer", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LockoutDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaintenanceEndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaintenanceMessage = table.Column<string>(type: "text", nullable: true),
                    MaintenanceMode = table.Column<bool>(type: "boolean", nullable: false),
                    MaintenanceStartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaxApiKeysPerUser = table.Column<int>(type: "integer", nullable: false),
                    MaxLoginAttempts = table.Column<int>(type: "integer", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: false),
                    PasswordResetTokenExpiryHours = table.Column<int>(type: "integer", nullable: false),
                    RequireApiKeyApproval = table.Column<bool>(type: "boolean", nullable: false),
                    RequireTwoFactorForAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    SendApiKeyApprovalEmails = table.Column<bool>(type: "boolean", nullable: false),
                    SendApiKeyExpirationWarnings = table.Column<bool>(type: "boolean", nullable: false),
                    UserActivityLogRetentionDays = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminSettings", x => x.Id);
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
        }
    }
}
