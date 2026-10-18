using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendaApp.Migrations
{
    /// <inheritdoc />
    public partial class BusinessIsPublished : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Businesses",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // Las empresas que ya estaban aprobadas siguen visibles; las nuevas empiezan ocultas
            migrationBuilder.Sql("UPDATE Businesses SET IsPublished = 1 WHERE Status = 'Approved';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "Businesses");
        }
    }
}
