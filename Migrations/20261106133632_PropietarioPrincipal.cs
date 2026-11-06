using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendaApp.Migrations
{
    /// <inheritdoc />
    public partial class PropietarioPrincipal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CollaboratorApprovesOutOfHours",
                table: "Businesses",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Businesses",
                type: "char(36)",
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            // Empresas existentes: el principal es el Owner más antiguo (quien la registró)
            migrationBuilder.Sql(@"
                UPDATE Businesses b
                SET b.OwnerUserId = (
                    SELECT u.UserId FROM Users u
                    WHERE u.BusinessId = b.BusinessId AND u.Role = 'Owner'
                    ORDER BY u.CreatedAt, u.UserId
                    LIMIT 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CollaboratorApprovesOutOfHours",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Businesses");
        }
    }
}
