using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hemordna.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEstimatedMinutesToTaskAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstimatedMinutes",
                table: "TaskAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill existing rows from their task definition's current estimate - see
            // TaskAssignment.EstimatedMinutes for why this becomes a fixed snapshot from here on,
            // rather than something later definition edits would retroactively change.
            migrationBuilder.Sql(
                """
                UPDATE "TaskAssignments" ta
                SET "EstimatedMinutes" = td."EstimatedMinutes"
                FROM "TaskDefinitions" td
                WHERE ta."TaskDefinitionId" = td."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_HouseholdId_MemberId",
                table: "TaskAssignments",
                columns: new[] { "HouseholdId", "MemberId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskAssignments_HouseholdId_MemberId",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "EstimatedMinutes",
                table: "TaskAssignments");
        }
    }
}
