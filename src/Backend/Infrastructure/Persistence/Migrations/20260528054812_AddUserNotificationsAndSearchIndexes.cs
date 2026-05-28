using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserNotificationsAndSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    LinkUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_CreatedAtUtc",
                table: "UserNotifications",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_ReadAtUtc",
                table: "UserNotifications",
                columns: new[] { "UserId", "ReadAtUtc" });

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_Users_NormalizedEmail_Trgm"" ON ""Users"" USING GIN (""NormalizedEmail"" gin_trgm_ops);");
            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_Users_NormalizedUserName_Trgm"" ON ""Users"" USING GIN (""NormalizedUserName"" gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Users_NormalizedUserName_Trgm"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Users_NormalizedEmail_Trgm"";");

            migrationBuilder.DropTable(
                name: "UserNotifications");
        }
    }
}
