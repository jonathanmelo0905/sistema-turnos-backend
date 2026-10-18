using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendaApp.Migrations
{
    /// <inheritdoc />
    // Clients.Phone es heredada y quedó NOT NULL aunque el modelo la tiene opcional:
    // desde T4 se puede reservar solo con correo, y el INSERT fallaba. EF no ve la diferencia, va a mano.
    public partial class ClientPhoneNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `Clients` MODIFY `Phone` longtext NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE `Clients` SET `Phone` = '' WHERE `Phone` IS NULL;");
            migrationBuilder.Sql("ALTER TABLE `Clients` MODIFY `Phone` longtext NOT NULL;");
        }
    }
}
