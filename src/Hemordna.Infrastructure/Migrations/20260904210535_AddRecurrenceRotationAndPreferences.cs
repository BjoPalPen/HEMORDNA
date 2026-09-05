using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hemordna.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurrenceRotationAndPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Recurrence",
                table: "TaskDefinitions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MemberPreferences",
                columns: table => new
                {
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Presentation = table.Column<int>(type: "integer", nullable: false),
                    Motivation = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberPreferences", x => x.MemberId);
                    table.ForeignKey(
                        name: "FK_MemberPreferences_HouseholdMembers_MemberId",
                        column: x => x.MemberId,
                        principalTable: "HouseholdMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskAssignments_HouseholdMembers_MemberId",
                        column: x => x.MemberId,
                        principalTable: "HouseholdMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskAssignments_TaskDefinitions_TaskDefinitionId",
                        column: x => x.TaskDefinitionId,
                        principalTable: "TaskDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberPreferences_HouseholdId",
                table: "MemberPreferences",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_HouseholdId",
                table: "TaskAssignments",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_MemberId",
                table: "TaskAssignments",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_TaskDefinitionId_ScheduledDate",
                table: "TaskAssignments",
                columns: new[] { "TaskDefinitionId", "ScheduledDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberPreferences");

            migrationBuilder.DropTable(
                name: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "Recurrence",
                table: "TaskDefinitions");
        }
    }
}
