using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassificadorExtractorDocumentos.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// FacturasLineas.PorcentajeIva passa a admetre NULL: "el document no indica el %IVA de la línia" ja no es
    /// desa com a 0 (que vol dir exempt). Les files existents conserven el valor que tenien.
    /// </summary>
    public partial class LineaPorcentajeIvaOpcional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "PorcentajeIva",
                table: "FacturasLineas",
                type: "decimal(5,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "PorcentajeIva",
                table: "FacturasLineas",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldNullable: true);
        }
    }
}
