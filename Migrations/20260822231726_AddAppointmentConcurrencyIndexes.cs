using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendaApp.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentConcurrencyIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // InnoDB suelta automáticamente el índice simple de la FK (ClientId/UserId) en cuanto
            // detecta que el compuesto de abajo ya la cubre (columna referenciada como primera
            // columna) — no hace falta (ni se puede) un DropIndex explícito después de esto.
            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ClientId_DateTimeStart",
                table: "Appointments",
                columns: new[] { "ClientId", "DateTimeStart" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_UserId_DateTimeStart",
                table: "Appointments",
                columns: new[] { "UserId", "DateTimeStart" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Al soltar el compuesto, InnoDB vuelve a crear automáticamente un índice simple
            // sobre la columna de la FK si hace falta para sostener la constraint.
            migrationBuilder.DropIndex(
                name: "IX_Appointments_ClientId_DateTimeStart",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_UserId_DateTimeStart",
                table: "Appointments");
        }
    }
}
