using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Celero.Migrations
{
    /// <inheritdoc />
    public partial class InitialRecibosOffline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecibosOffline",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SerNr = table.Column<string>(type: "TEXT", nullable: false),
                    TransDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PayMode = table.Column<string>(type: "TEXT", nullable: false),
                    Person = table.Column<string>(type: "TEXT", nullable: false),
                    CUCode = table.Column<string>(type: "TEXT", nullable: false),
                    RefStr = table.Column<string>(type: "TEXT", nullable: false),
                    DetallesJson = table.Column<string>(type: "TEXT", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecibosOffline", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecibosOffline");
        }
    }
}
