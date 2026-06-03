using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationBroadcastEventId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BroadcastEventId",
                table: "UserNotifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_BroadcastEventId_UserId",
                table: "UserNotifications",
                columns: new[] { "BroadcastEventId", "UserId" },
                unique: true,
                filter: "\"BroadcastEventId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_BroadcastEventId_UserId",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "BroadcastEventId",
                table: "UserNotifications");
        }
    }
}
