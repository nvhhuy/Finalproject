using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaProcessingStartedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingStartedAtUtc",
                table: "MediaAssets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_Status_ProcessingStartedAtUtc",
                table: "MediaAssets",
                columns: new[] { "Status", "ProcessingStartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaAssets_Status_ProcessingStartedAtUtc",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "ProcessingStartedAtUtc",
                table: "MediaAssets");
        }
    }
}
