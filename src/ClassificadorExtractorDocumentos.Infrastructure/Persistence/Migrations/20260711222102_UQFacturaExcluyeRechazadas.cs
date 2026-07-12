using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassificadorExtractorDocumentos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UQFacturaExcluyeRechazadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Factura",
                table: "FacturasStaging");

            migrationBuilder.CreateIndex(
                name: "UQ_Factura",
                table: "FacturasStaging",
                columns: new[] { "ProveedorId", "NumeroFactura" },
                unique: true,
                filter: "[ProveedorId] IS NOT NULL AND [Estado] <> N'Rechazada'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Factura",
                table: "FacturasStaging");

            migrationBuilder.CreateIndex(
                name: "UQ_Factura",
                table: "FacturasStaging",
                columns: new[] { "ProveedorId", "NumeroFactura" },
                unique: true,
                filter: "[ProveedorId] IS NOT NULL");
        }
    }
}
