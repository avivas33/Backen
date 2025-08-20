using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Celero.Migrations
{
    /// <inheritdoc />
    public partial class AddPagoACHFotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PagoACHFotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    NumeroFactura = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EmpresaCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    NumeroTransaccion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    FotoComprobante = table.Column<byte[]>(type: "BLOB", nullable: false),
                    NombreArchivo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TipoArchivo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TamanoArchivo = table.Column<long>(type: "INTEGER", nullable: false),
                    MontoTransaccion = table.Column<decimal>(type: "TEXT", nullable: false),
                    FechaTransaccion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Observaciones = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    UsuarioRegistro = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FechaProcesamiento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MotivoRechazo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagoACHFotos", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PagoACHFotos");
        }
    }
}
