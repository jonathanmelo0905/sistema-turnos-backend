using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AgendaApp.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionsAndOwnerRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StartTime",
                table: "UserSchedules",
                type: "varchar(8)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "EndTime",
                table: "UserSchedules",
                type: "varchar(8)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "BreakStart",
                table: "UserSchedules",
                type: "varchar(8)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "BreakEnd",
                table: "UserSchedules",
                type: "varchar(8)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "StartTime",
                table: "UserScheduleExceptions",
                type: "varchar(8)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "EndTime",
                table: "UserScheduleExceptions",
                type: "varchar(8)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "OpenTime",
                table: "BusinessSchedules",
                type: "varchar(8)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "CloseTime",
                table: "BusinessSchedules",
                type: "varchar(8)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.PermissionId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserPermissions",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    PermissionId = table.Column<int>(type: "int", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => new { x.UserId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_UserPermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "PermissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "PermissionId", "Code", "Description" },
                values: new object[,]
                {
                    { 1, "business.edit", "Editar datos de la empresa" },
                    { 2, "collaborators.manage", "Gestionar colaboradores (crear/editar perfiles)" },
                    { 3, "services.manage", "Gestionar servicios (crear/editar catálogo)" },
                    { 4, "accounting.view", "Ver contabilidad" },
                    { 5, "stats.view", "Ver estadísticas de flujo de clientes" },
                    { 6, "clients.contacts.view", "Ver contactos de clientes" },
                    { 7, "flexibility.approve", "Aprobar políticas de flexibilidad (horario, turnos fuera de horario)" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_GrantedByUserId",
                table: "UserPermissions",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_PermissionId",
                table: "UserPermissions",
                column: "PermissionId");

            // El rol del dueño del negocio se renombra de "Admin" a "Owner" en el código de
            // la aplicación (ver Context/Roles.cs); se actualizan los datos existentes para
            // que coincidan.
            migrationBuilder.Sql("UPDATE Users SET Role = 'Owner' WHERE Role = 'Admin';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Users SET Role = 'Admin' WHERE Role = 'Owner';");

            migrationBuilder.DropTable(
                name: "UserPermissions");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.AlterColumn<string>(
                name: "StartTime",
                table: "UserSchedules",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(8)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "EndTime",
                table: "UserSchedules",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(8)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "BreakStart",
                table: "UserSchedules",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(8)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "BreakEnd",
                table: "UserSchedules",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(8)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "StartTime",
                table: "UserScheduleExceptions",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(8)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "EndTime",
                table: "UserScheduleExceptions",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(8)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "OpenTime",
                table: "BusinessSchedules",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(8)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "CloseTime",
                table: "BusinessSchedules",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(8)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
