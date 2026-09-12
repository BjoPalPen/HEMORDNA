using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hemordna.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCanManageHousehold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanManageHousehold",
                table: "HouseholdMembers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Deliberate one-time backfill, not the column's own default: nothing may change for
            // any EXISTING household the day this ships - see docs/ARCHITECTURE.md "Beslut: Vem
            // får ändra vad". Every member who already has an account (UserId IS NOT NULL) could
            // already do everything this flag now gates, so they all keep being able to.
            // Account-less members (UserId IS NULL) are left at the column's own default(false) -
            // HouseholdMember.SetCanManageHousehold would reject true for them anyway, since they
            // can never sign in to use it.
            migrationBuilder.Sql(
                """
                UPDATE "HouseholdMembers" SET "CanManageHousehold" = TRUE WHERE "UserId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanManageHousehold",
                table: "HouseholdMembers");
        }
    }
}
