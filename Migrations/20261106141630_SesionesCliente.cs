using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendaApp.Migrations
{
    /// <inheritdoc />
    public partial class SesionesCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clients_AccessToken",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "AccessToken",
                table: "Clients");

            migrationBuilder.AddColumn<int>(
                name: "LookupAttempts",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LookupBlockedUntil",
                table: "Clients",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClientSessions",
                columns: table => new
                {
                    ClientSessionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClientId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TokenHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientSessions", x => x.ClientSessionId);
                    table.ForeignKey(
                        name: "FK_ClientSessions_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ClientSessions_ClientId",
                table: "ClientSessions",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientSessions_TokenHash",
                table: "ClientSessions",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientSessions");

            migrationBuilder.DropColumn(
                name: "LookupAttempts",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LookupBlockedUntil",
                table: "Clients");

            migrationBuilder.AddColumn<string>(
                name: "AccessToken",
                table: "Clients",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_AccessToken",
                table: "Clients",
                column: "AccessToken");
        }
    }
}
