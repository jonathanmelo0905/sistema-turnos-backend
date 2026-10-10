using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendaApp.Migrations
{
    /// <inheritdoc />
    public partial class HorasValle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ListPrice",
                table: "Appointments",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValleyDiscountPct",
                table: "Appointments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ValleyConfigs",
                columns: table => new
                {
                    BusinessId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Mode = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false, defaultValue: "Percent")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Threshold = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    DiscountMin = table.Column<int>(type: "int", nullable: false),
                    DiscountMax = table.Column<int>(type: "int", nullable: false),
                    MinOverlapPct = table.Column<int>(type: "int", nullable: false),
                    Proportional = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValleyConfigs", x => x.BusinessId);
                    table.ForeignKey(
                        name: "FK_ValleyConfigs_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "BusinessId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "PermissionId", "Code", "Description" },
                values: new object[] { 8, "valley.manage", "Configurar Horas Valle (descuentos en horas flojas)" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ValleyConfigs");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 8);

            migrationBuilder.DropColumn(
                name: "ListPrice",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ValleyDiscountPct",
                table: "Appointments");
        }
    }
}
