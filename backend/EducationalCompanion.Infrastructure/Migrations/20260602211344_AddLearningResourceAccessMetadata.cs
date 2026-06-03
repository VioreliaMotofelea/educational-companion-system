using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EducationalCompanion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningResourceAccessMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessInstructions",
                table: "LearningResources",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccessType",
                table: "LearningResources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceName",
                table: "LearningResources",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "LearningResources",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "LearningResources",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_LearningResources_AccessType",
                table: "LearningResources",
                column: "AccessType");

            migrationBuilder.CreateIndex(
                name: "IX_LearningResources_Topic",
                table: "LearningResources",
                column: "Topic");

            migrationBuilder.CreateIndex(
                name: "IX_LearningResources_Visibility",
                table: "LearningResources",
                column: "Visibility");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LearningResources_AccessType",
                table: "LearningResources");

            migrationBuilder.DropIndex(
                name: "IX_LearningResources_Topic",
                table: "LearningResources");

            migrationBuilder.DropIndex(
                name: "IX_LearningResources_Visibility",
                table: "LearningResources");

            migrationBuilder.DropColumn(
                name: "AccessInstructions",
                table: "LearningResources");

            migrationBuilder.DropColumn(
                name: "AccessType",
                table: "LearningResources");

            migrationBuilder.DropColumn(
                name: "SourceName",
                table: "LearningResources");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "LearningResources");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "LearningResources");
        }
    }
}
