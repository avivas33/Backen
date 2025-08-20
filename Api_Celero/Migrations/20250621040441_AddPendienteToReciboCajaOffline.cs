using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Celero.Migrations
{
    /// <inheritdoc />
    public partial class AddPendienteToReciboCajaOffline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Pendiente",
                table: "RecibosOffline",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Pendiente",
                table: "RecibosOffline");
        }
    }
}
