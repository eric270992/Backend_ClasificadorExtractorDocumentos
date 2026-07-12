using ClassificadorExtractorDocumentos.Application.Extraccion;
using ClassificadorExtractorDocumentos.Application.Validacion;
using ClassificadorExtractorDocumentos.Domain.Contracts;

namespace ClassificadorExtractorDocumentos.Application.Ingesta.Maf;

// Mensajes tipados que fluyen entre los executors del Workflow MAF. En MAF idiomático, cada handler
// DEVUELVE su tipo de salida (así queda declarado y se enruta solo); el desvío éxito/error de la
// extracción se hace con ARISTAS CONDICIONALES, no enviando tipos distintos a mano.

/// <summary>Entrada del workflow: documento ya ingestado (PDF→imágenes hecho) listo para extraer.</summary>
internal sealed record DocumentoParaProcesar(DocumentoIngestado Documento, IReadOnlyList<byte[]> Imagenes);

/// <summary>Salida del Extractor. Una arista condicional la lleva al Validador (si Exito) o al
/// executor de error (si no). Lleva el documento para no perder el hilo.</summary>
internal sealed record ExtraccionRealizada(DocumentoIngestado Documento, ResultadoExtraccion Extraccion);

/// <summary>Validador → Persistencia: factura validada lista para persistir.</summary>
internal sealed record FacturaParaPersistir(
    DocumentoIngestado Documento,
    ResultadoExtraccion Extraccion,
    ResultadoValidacion Validacion);
