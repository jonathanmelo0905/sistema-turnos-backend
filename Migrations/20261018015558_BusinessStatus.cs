using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendaApp.Migrations
{
    /// <inheritdoc />
    public partial class BusinessStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Businesses",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Approved")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "TermsAcceptedAt",
                table: "Businesses",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAt",
                table: "Businesses");
        }
    }
}
