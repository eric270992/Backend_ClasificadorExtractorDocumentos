using ClassificadorExtractorDocumentos.Application.Consultor;

namespace ClassificadorExtractorDocumentos.UnitTests.Consultor;

public class SqlGuardTests
{
    [Fact]
    public void Select_valido_pasa_y_recibe_top_1000()
    {
        var resultado = SqlGuard.Validar("SELECT Nombre, Total FROM FacturasStaging WHERE Estado = 'Validada'");

        Assert.True(resultado.EsSegura);
        Assert.StartsWith("SELECT TOP 1000 ", resultado.SqlSeguro);
    }

    [Fact]
    public void Select_con_top_propio_no_se_modifica()
    {
        var sql = "SELECT TOP 5 * FROM Proveedores";

        var resultado = SqlGuard.Validar(sql);

        Assert.True(resultado.EsSegura);
        Assert.Equal(sql, resultado.SqlSeguro);
    }

    [Fact]
    public void Select_distinct_recibe_top_tras_distinct()
    {
        var resultado = SqlGuard.Validar("SELECT DISTINCT Moneda FROM FacturasStaging");

        Assert.True(resultado.EsSegura);
        Assert.StartsWith("SELECT DISTINCT TOP 1000 ", resultado.SqlSeguro);
    }

    [Fact]
    public void Join_entre_tablas_permitidas_pasa()
    {
        var resultado = SqlGuard.Validar(
            "SELECT p.Nombre, SUM(f.Total) FROM FacturasStaging f JOIN Proveedores p ON p.Id = f.ProveedorId GROUP BY p.Nombre");

        Assert.True(resultado.EsSegura);
    }

    [Theory]
    [InlineData("DELETE FROM FacturasStaging")]                          // el intento de "borra las facturas"
    [InlineData("UPDATE FacturasStaging SET Total = 0")]
    [InlineData("DROP TABLE FacturasStaging")]
    [InlineData("INSERT INTO Proveedores VALUES ('X','Y')")]
    [InlineData("TRUNCATE TABLE FacturasLineas")]
    [InlineData("EXEC xp_cmdshell 'dir'")]
    public void Sentencias_de_escritura_se_rechazan(string sql)
    {
        var resultado = SqlGuard.Validar(sql);

        Assert.False(resultado.EsSegura);
        Assert.NotNull(resultado.Motivo);
    }

    [Theory]
    [InlineData("SELECT * FROM FacturasStaging; DROP TABLE FacturasStaging")] // multi-sentencia
    [InlineData("SELECT * FROM FacturasStaging -- comentario")]
    [InlineData("SELECT * FROM FacturasStaging /* comentario */")]
    [InlineData("SELECT * INTO Robadas FROM FacturasStaging")]                // SELECT INTO
    [InlineData("SELECT Total FROM FacturasStaging WHERE Id IN (SELECT Id FROM Usuarios)")] // tabla fuera de whitelist
    [InlineData("SELECT * FROM sys.tables")]
    [InlineData("SELECT 1")]                                                  // sin tabla reconocible
    [InlineData("")]
    public void Vectores_de_inyeccion_se_rechazan(string sql)
    {
        Assert.False(SqlGuard.Validar(sql).EsSegura);
    }

    [Fact]
    public void Subconsulta_sobre_tabla_permitida_pasa()
    {
        var resultado = SqlGuard.Validar(
            "SELECT * FROM FacturasStaging WHERE Total > (SELECT AVG(Total) FROM FacturasStaging)");

        Assert.True(resultado.EsSegura);
    }

    [Fact]
    public void Esquema_dbo_explicito_se_valida_igual()
    {
        var resultado = SqlGuard.Validar("SELECT * FROM dbo.FacturasStaging");

        Assert.True(resultado.EsSegura);
    }
}
