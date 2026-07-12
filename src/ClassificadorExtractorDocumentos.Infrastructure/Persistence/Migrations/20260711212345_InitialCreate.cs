using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassificadorExtractorDocumentos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Proveedores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nif = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proveedores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FacturasStaging",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProveedorId = table.Column<int>(type: "int", nullable: true),
                    NumeroFactura = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FechaFactura = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaVencimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    Moneda = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "EUR"),
                    BaseImponible = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CuotaIva = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RetencionIrpf = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReverseCharge = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "PendienteValidacion"),
                    JsonOriginalExtraido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NivelExtraccion = table.Column<byte>(type: "tinyint", nullable: false),
                    RutaPdfOriginal = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FechaIngesta = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturasStaging", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacturasStaging_Proveedores_ProveedorId",
                        column: x => x.ProveedorId,
                        principalTable: "Proveedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProveedorEjemplos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProveedorId = table.Column<int>(type: "int", nullable: false),
                    RutaImagen = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    JsonValidado = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProveedorEjemplos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProveedorEjemplos_Proveedores_ProveedorId",
                        column: x => x.ProveedorId,
                        principalTable: "Proveedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FacturasLineas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacturaId = table.Column<int>(type: "int", nullable: false),
                    NumLinea = table.Column<int>(type: "int", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PorcentajeIva = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ImporteLinea = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturasLineas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacturasLineas_FacturasStaging_FacturaId",
                        column: x => x.FacturaId,
                        principalTable: "FacturasStaging",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValidacionIncidencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacturaId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidacionIncidencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidacionIncidencias_FacturasStaging_FacturaId",
                        column: x => x.FacturaId,
                        principalTable: "FacturasStaging",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FacturasLineas_FacturaId",
                table: "FacturasLineas",
                column: "FacturaId");

            migrationBuilder.CreateIndex(
                name: "UQ_Factura",
                table: "FacturasStaging",
                columns: new[] { "ProveedorId", "NumeroFactura" },
                unique: true,
                filter: "[ProveedorId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProveedorEjemplos_ProveedorId",
                table: "ProveedorEjemplos",
                column: "ProveedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Proveedores_Nif",
                table: "Proveedores",
                column: "Nif",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValidacionIncidencias_FacturaId",
                table: "ValidacionIncidencias",
                column: "FacturaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacturasLineas");

            migrationBuilder.DropTable(
                name: "ProveedorEjemplos");

            migrationBuilder.DropTable(
                name: "ValidacionIncidencias");

            migrationBuilder.DropTable(
                name: "FacturasStaging");

            migrationBuilder.DropTable(
                name: "Proveedores");
        }
    }
}
