using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hemordna.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskEffort : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Both new and pre-existing tasks land on TaskEffort.Medium (1), not the CLR
            // default (0 = Light) - see docs/ARCHITECTURE.md and TaskDefinitionConfiguration.
            migrationBuilder.AddColumn<int>(
                name: "Effort",
                table: "TaskDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Effort",
                table: "TaskDefinitions");
        }
    }
}
