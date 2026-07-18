using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassificadorExtractorDocumentos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteYAprobacionManual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Factura",
                table: "FacturasStaging");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAprobacionManual",
                table: "FacturasStaging",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminacion",
                table: "FacturasStaging",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Factura",
                table: "FacturasStaging",
                columns: new[] { "ProveedorId", "NumeroFactura" },
                unique: true,
                filter: "[ProveedorId] IS NOT NULL AND [Estado] <> N'Rechazada' AND [FechaEliminacion] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Factura",
                table: "FacturasStaging");

            migrationBuilder.DropColumn(
                name: "FechaAprobacionManual",
                table: "FacturasStaging");

            migrationBuilder.DropColumn(
                name: "FechaEliminacion",
                table: "FacturasStaging");

            migrationBuilder.CreateIndex(
                name: "UQ_Factura",
                table: "FacturasStaging",
                columns: new[] { "ProveedorId", "NumeroFactura" },
                unique: true,
                filter: "[ProveedorId] IS NOT NULL AND [Estado] <> N'Rechazada'");
        }
    }
}
