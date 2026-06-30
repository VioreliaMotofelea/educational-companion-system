using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EducationalCompanion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceAccessScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "LearningResources",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ResourceAccessScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LearningResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeType = table.Column<int>(type: "integer", nullable: false),
                    ScopeKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceAccessScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceAccessScopes_LearningResources_LearningResourceId",
                        column: x => x.LearningResourceId,
                        principalTable: "LearningResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAccessScopeMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScopeType = table.Column<int>(type: "integer", nullable: false),
                    ScopeKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccessScopeMemberships", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningResources_OwnerUserId",
                table: "LearningResources",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAccessScopes_LearningResourceId",
                table: "ResourceAccessScopes",
                column: "LearningResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAccessScopes_LearningResourceId_ScopeType_ScopeKey",
                table: "ResourceAccessScopes",
                columns: new[] { "LearningResourceId", "ScopeType", "ScopeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAccessScopes_ScopeType_ScopeKey",
                table: "ResourceAccessScopes",
                columns: new[] { "ScopeType", "ScopeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessScopeMemberships_ScopeType_ScopeKey",
                table: "UserAccessScopeMemberships",
                columns: new[] { "ScopeType", "ScopeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessScopeMemberships_UserId",
                table: "UserAccessScopeMemberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessScopeMemberships_UserId_ScopeType_ScopeKey",
                table: "UserAccessScopeMemberships",
                columns: new[] { "UserId", "ScopeType", "ScopeKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResourceAccessScopes");

            migrationBuilder.DropTable(
                name: "UserAccessScopeMemberships");

            migrationBuilder.DropIndex(
                name: "IX_LearningResources_OwnerUserId",
                table: "LearningResources");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "LearningResources");
        }
    }
}
