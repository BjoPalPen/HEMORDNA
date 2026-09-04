using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hemordna.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Households",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Households", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Areas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Areas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Areas_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WeeklyTimeBudgetMinutes = table.Column<int[]>(type: "integer[]", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdMembers_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberAvailabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    AvailableMinutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberAvailabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberAvailabilities_HouseholdMembers_MemberId",
                        column: x => x.MemberId,
                        principalTable: "HouseholdMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: true),
                    EstimatedMinutes = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    DefaultResponsibleMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreferredWeekday = table.Column<int>(type: "integer", nullable: true),
                    CanBeDeferred = table.Column<bool>(type: "boolean", nullable: false),
                    HasRotatingResponsibility = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresMultiplePeople = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskDefinitions_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TaskDefinitions_HouseholdMembers_DefaultResponsibleMemberId",
                        column: x => x.DefaultResponsibleMemberId,
                        principalTable: "HouseholdMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TaskDefinitions_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskOccurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OriginalScheduledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CanBeDeferred = table.Column<bool>(type: "boolean", nullable: false),
                    AssignedMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedByMemberId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskOccurrences_HouseholdMembers_AssignedMemberId",
                        column: x => x.AssignedMemberId,
                        principalTable: "HouseholdMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TaskOccurrences_TaskDefinitions_TaskDefinitionId",
                        column: x => x.TaskDefinitionId,
                        principalTable: "TaskDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Areas_HouseholdId",
                table: "Areas",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMembers_HouseholdId",
                table: "HouseholdMembers",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberAvailabilities_HouseholdId",
                table: "MemberAvailabilities",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberAvailabilities_MemberId_Date",
                table: "MemberAvailabilities",
                columns: new[] { "MemberId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskDefinitions_AreaId",
                table: "TaskDefinitions",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDefinitions_DefaultResponsibleMemberId",
                table: "TaskDefinitions",
                column: "DefaultResponsibleMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDefinitions_HouseholdId",
                table: "TaskDefinitions",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskOccurrences_AssignedMemberId",
                table: "TaskOccurrences",
                column: "AssignedMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskOccurrences_HouseholdId_ScheduledDate_Status",
                table: "TaskOccurrences",
                columns: new[] { "HouseholdId", "ScheduledDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskOccurrences_TaskDefinitionId",
                table: "TaskOccurrences",
                column: "TaskDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberAvailabilities");

            migrationBuilder.DropTable(
                name: "TaskOccurrences");

            migrationBuilder.DropTable(
                name: "TaskDefinitions");

            migrationBuilder.DropTable(
                name: "Areas");

            migrationBuilder.DropTable(
                name: "HouseholdMembers");

            migrationBuilder.DropTable(
                name: "Households");
        }
    }
}
