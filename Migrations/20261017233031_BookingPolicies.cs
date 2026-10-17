using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgendaApp.Migrations
{
    /// <inheritdoc />
    public partial class BookingPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BufferMinutes",
                table: "Businesses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxDaysAhead",
                table: "Businesses",
                type: "int",
                nullable: false,
                defaultValue: 8);

            migrationBuilder.AddColumn<int>(
                name: "MinAdvanceMinutes",
                table: "Businesses",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<bool>(
                name: "ValleyOnReschedule",
                table: "Businesses",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BufferMinutes",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "MaxDaysAhead",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "MinAdvanceMinutes",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "ValleyOnReschedule",
                table: "Businesses");
        }
    }
}
