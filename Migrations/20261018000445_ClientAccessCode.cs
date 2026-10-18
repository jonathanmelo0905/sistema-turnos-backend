using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendaApp.Migrations
{
    /// <inheritdoc />
    public partial class ClientAccessCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessCodeAttempts",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "AccessCodeExpiresAt",
                table: "Clients",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessCodeHash",
                table: "Clients",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessCodeAttempts",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "AccessCodeExpiresAt",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "AccessCodeHash",
                table: "Clients");
        }
    }
}
