namespace ClassificadorExtractorDocumentos.Application.Ingesta;

/// <summary>
/// El document té més pàgines de les permeses (Ingesta:MaxPaginasPorDocumento). Mateix patró que
/// <see cref="FormatoNoSoportadoException"/>: la llança el servei i el controlador la converteix en resposta HTTP.
/// Porta les dues xifres perquè el frontend les pugui mostrar i actualitzar el límit que té en pantalla.
/// </summary>
public class DocumentoDemasiadoLargoException(int paginas, int maxPaginas)
    : Exception($"El document té {paginas} pàgines i el màxim permès és {maxPaginas}.")
{
    public int Paginas { get; } = paginas;
    public int MaxPaginas { get; } = maxPaginas;
}
