using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendaApp.Migrations
{
    /// <inheritdoc />
    public partial class Recordatorios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReminderChannel",
                table: "Appointments",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentAt",
                table: "Appointments",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderChannel",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "Appointments");
        }
    }
}
