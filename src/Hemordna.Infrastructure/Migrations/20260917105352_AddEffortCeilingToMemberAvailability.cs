using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hemordna.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEffortCeilingToMemberAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EffortCeiling",
                table: "MemberAvailabilities",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EffortCeiling",
                table: "MemberAvailabilities");
        }
    }
}
