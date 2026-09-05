using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hemordna.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdInviteCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InviteCode",
                table: "Households",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            // Households created before this column existed each need a distinct code - a
            // shared default value would collide with the unique index added below as soon
            // as there is more than one existing household.
            migrationBuilder.Sql(
                "UPDATE \"Households\" SET \"InviteCode\" = upper(substr(md5(random()::text || \"Id\"::text), 1, 8)) WHERE \"InviteCode\" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "InviteCode",
                table: "Households",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Households_InviteCode",
                table: "Households",
                column: "InviteCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Households_InviteCode",
                table: "Households");

            migrationBuilder.DropColumn(
                name: "InviteCode",
                table: "Households");
        }
    }
}
