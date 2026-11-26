namespace ClassificadorExtractorDocumentos.Application.Grounding;

/// <summary>Opciones del grounding, sección "Grounding" de la configuración.</summary>
public class GroundingOptions
{
    public const string SectionName = "Grounding";

    /// <summary>
    /// Si es true, el backend ofrece el grounding (GET /facturas/{id}/grounding y /facturas/{id}/paginas/{n}) y el
    /// frontend muestra el botón "D'on surt cada dada". Por defecto false: es una prueba que añade carga (render de
    /// páginas y, con el método LLM, una llamada al modelo) y de momento no aporta al flujo normal.
    /// El frontend lo lee de GET /configuracion.
    /// </summary>
    public bool Habilitado { get; set; }
}
