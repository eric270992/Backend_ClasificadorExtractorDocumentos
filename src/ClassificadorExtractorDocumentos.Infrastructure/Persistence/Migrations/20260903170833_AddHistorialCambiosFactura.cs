using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassificadorExtractorDocumentos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHistorialCambiosFactura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistorialCambiosFactura",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacturaId = table.Column<int>(type: "int", nullable: false),
                    Campo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ValorAnterior = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ValorNuevo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaCambio = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Usuario = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialCambiosFactura", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialCambiosFactura_FacturasStaging_FacturaId",
                        column: x => x.FacturaId,
                        principalTable: "FacturasStaging",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialCambiosFactura_FacturaId",
                table: "HistorialCambiosFactura",
                column: "FacturaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistorialCambiosFactura");
        }
    }
}
