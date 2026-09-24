using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hemordna.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "Reminders",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "Reminders");
        }
    }
}
