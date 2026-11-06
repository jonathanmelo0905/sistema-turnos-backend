using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendaApp.Migrations
{
    /// <inheritdoc />
    public partial class PlanBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Base nueva (Railway): toda empresa nace con PlanId = 1. Las bases que ya lo tienen no cambian.
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO Plans (PlanId, Name, MaxUsers, MaxAppointments, Price)
                VALUES (1, 'Free', 50, 100000, 0);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
