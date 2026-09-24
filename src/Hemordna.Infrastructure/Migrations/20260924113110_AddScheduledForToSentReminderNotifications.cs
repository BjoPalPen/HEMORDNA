using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hemordna.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledForToSentReminderNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SentReminderNotifications_ReminderId_Kind",
                table: "SentReminderNotifications");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScheduledFor",
                table: "SentReminderNotifications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_SentReminderNotifications_ReminderId_Kind_ScheduledFor",
                table: "SentReminderNotifications",
                columns: new[] { "ReminderId", "Kind", "ScheduledFor" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SentReminderNotifications_ReminderId_Kind_ScheduledFor",
                table: "SentReminderNotifications");

            migrationBuilder.DropColumn(
                name: "ScheduledFor",
                table: "SentReminderNotifications");

            migrationBuilder.CreateIndex(
                name: "IX_SentReminderNotifications_ReminderId_Kind",
                table: "SentReminderNotifications",
                columns: new[] { "ReminderId", "Kind" },
                unique: true);
        }
    }
}
