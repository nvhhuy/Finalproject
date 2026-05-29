using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_CreatedByUserId_CreatedAtUtc",
                table: "SupportTickets");

            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_TicketNumber",
                table: "SupportTickets");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "SupportTickets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "MediaAssets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicTagline = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PublicDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantMembers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_TenantId_CreatedByUserId_CreatedAtUtc",
                table: "SupportTickets",
                columns: new[] { "TenantId", "CreatedByUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_TenantId_TicketNumber",
                table: "SupportTickets",
                columns: new[] { "TenantId", "TicketNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_TenantId",
                table: "MediaAssets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMembers_TenantId_UserId",
                table: "TenantMembers",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantMembers_UserId",
                table: "TenantMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.Sql("""
                UPDATE "SupportTickets"
                SET "TenantId" = '00000000-0000-0000-0000-000000000001'
                WHERE "TenantId" = '00000000-0000-0000-0000-000000000000';

                UPDATE "MediaAssets"
                SET "TenantId" = '00000000-0000-0000-0000-000000000001'
                WHERE "TenantId" = '00000000-0000-0000-0000-000000000000';

                INSERT INTO "Tenants" ("Id", "Slug", "DisplayName", "Status", "OwnerUserId", "PublicTagline", "PublicDescription", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT '00000000-0000-0000-0000-000000000001', 'default', 'Default Organization', 'Active', u."Id", NULL, NULL, NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC'
                FROM "Users" u
                WHERE u."Role" = 'Admin'
                ORDER BY u."RegisteredAtUtc" NULLS LAST
                LIMIT 1
                ON CONFLICT ("Id") DO NOTHING;

                INSERT INTO "TenantMembers" ("Id", "TenantId", "UserId", "Role", "JoinedAtUtc")
                SELECT gen_random_uuid(), '00000000-0000-0000-0000-000000000001', t."OwnerUserId", 'Owner', NOW() AT TIME ZONE 'UTC'
                FROM "Tenants" t
                WHERE t."Id" = '00000000-0000-0000-0000-000000000001'
                  AND NOT EXISTS (
                      SELECT 1 FROM "TenantMembers" m
                      WHERE m."TenantId" = t."Id" AND m."UserId" = t."OwnerUserId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantMembers");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_TenantId_CreatedByUserId_CreatedAtUtc",
                table: "SupportTickets");

            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_TenantId_TicketNumber",
                table: "SupportTickets");

            migrationBuilder.DropIndex(
                name: "IX_MediaAssets_TenantId",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MediaAssets");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_CreatedByUserId_CreatedAtUtc",
                table: "SupportTickets",
                columns: new[] { "CreatedByUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_TicketNumber",
                table: "SupportTickets",
                column: "TicketNumber",
                unique: true);
        }
    }
}
