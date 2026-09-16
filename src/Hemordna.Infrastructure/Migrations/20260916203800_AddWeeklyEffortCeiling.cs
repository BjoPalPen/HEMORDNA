using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hemordna.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyEffortCeiling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every new AND pre-existing member lands on Heavy every day (2 = TaskEffort.Heavy,
            // indexed Sunday..Saturday) - "no limitation", so nothing changes for any existing
            // household until someone deliberately lowers their own ceiling. EF cannot infer a
            // sensible array default (its own attempt was an empty array, which would have left
            // WeeklyEffortCeiling.CeilingFor throwing IndexOutOfRangeException for every existing
            // member), so the seven-element default is spelled out explicitly here - see
            // docs/ARCHITECTURE.md.
            migrationBuilder.AddColumn<int[]>(
                name: "WeeklyEffortCeiling",
                table: "HouseholdMembers",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "'{2,2,2,2,2,2,2}'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeeklyEffortCeiling",
                table: "HouseholdMembers");
        }
    }
}
