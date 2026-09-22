using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hemordna.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFloorToArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Floor",
                table: "Areas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Unpack the old "Våning – Rum" naming convention (see the now-obsolete
            // Hemordna.Client.Support.RoomFloors) into the real Floor column, so existing rooms
            // look exactly as if they had been created with the new field from the start. The
            // separator is space + EN DASH (U+2013) + space - three characters, matched here
            // exactly as RoomFloors.cs matches it. PostgreSQL's position()/substring() are
            // character-based (not byte-based) for text under a UTF8 database encoding, so "+ 3"
            // is correct for this three-character separator regardless of EN DASH's 3-byte UTF-8
            // encoding.
            migrationBuilder.Sql(
                """
                UPDATE "Areas"
                SET "Floor" = split_part("Name", ' – ', 1),
                    "Name" = substring("Name" FROM position(' – ' IN "Name") + 3)
                WHERE "Name" LIKE '% – %';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately does not re-join Floor and Name back into "Våning – Rum" on rollback.
            // That would be a reasonable thing to do but is not critical, since Down here is only
            // ever the "undo this deploy" path, not a supported downgrade of live data - see
            // implementation-hardening's stop conditions. Dropping the column alone is safe:
            // it only discards the Floor value, it does not resurrect the old combined Name.
            migrationBuilder.DropColumn(
                name: "Floor",
                table: "Areas");
        }
    }
}
