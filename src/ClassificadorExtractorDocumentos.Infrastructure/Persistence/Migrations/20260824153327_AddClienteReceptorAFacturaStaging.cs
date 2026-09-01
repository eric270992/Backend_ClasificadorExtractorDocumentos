using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassificadorExtractorDocumentos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClienteReceptorAFacturaStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NifCliente",
                table: "FacturasStaging",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreCliente",
                table: "FacturasStaging",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // Relleno retroactivo: el receptor ya se extraía y se guardaba dentro de
            // JsonOriginalExtraido, solo no se promovía a columna. Se recupera de ahí para que las
            // facturas ya ingestadas muestren cliente sin reprocesar el PDF. El NIF se normaliza
            // igual que en Nif.Normalizar (sin guiones/puntos/espacios, mayúsculas).
            migrationBuilder.Sql("""
                UPDATE FacturasStaging
                SET NifCliente = NULLIF(LEFT(UPPER(REPLACE(REPLACE(REPLACE(
                        LTRIM(RTRIM(JSON_VALUE(JsonOriginalExtraido, '$.receptor.nif'))),
                        '-', ''), '.', ''), ' ', '')), 20), ''),
                    NombreCliente = NULLIF(LEFT(LTRIM(RTRIM(JSON_VALUE(JsonOriginalExtraido, '$.receptor.nombre'))), 200), '')
                WHERE ISJSON(JsonOriginalExtraido) = 1
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NifCliente",
                table: "FacturasStaging");

            migrationBuilder.DropColumn(
                name: "NombreCliente",
                table: "FacturasStaging");
        }
    }
}
