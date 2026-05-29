using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSsoAndNotificationDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LiveDeliveredAtUtc",
                table: "UserNotifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenantSsoSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    OidcAuthority = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OidcClientId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OidcClientSecret = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSsoSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_LiveDeliveredAtUtc_CreatedAtUtc",
                table: "UserNotifications",
                columns: new[] { "LiveDeliveredAtUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantSsoSettings_TenantId",
                table: "TenantSsoSettings",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantSsoSettings");

            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_LiveDeliveredAtUtc_CreatedAtUtc",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "LiveDeliveredAtUtc",
                table: "UserNotifications");
        }
    }
}
