using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hemordna.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberTimeCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AddedAsExtra",
                table: "TaskOccurrences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MemberTimeCredits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Minutes = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    OccurrenceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberTimeCredits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberTimeCredits_HouseholdMembers_MemberId",
                        column: x => x.MemberId,
                        principalTable: "HouseholdMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberTimeCredits_HouseholdId_MemberId_OccurredOn",
                table: "MemberTimeCredits",
                columns: new[] { "HouseholdId", "MemberId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberTimeCredits_MemberId",
                table: "MemberTimeCredits",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberTimeCredits_OccurrenceId",
                table: "MemberTimeCredits",
                column: "OccurrenceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberTimeCredits");

            migrationBuilder.DropColumn(
                name: "AddedAsExtra",
                table: "TaskOccurrences");
        }
    }
}
