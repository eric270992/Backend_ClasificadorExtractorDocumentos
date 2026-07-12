using ClassificadorExtractorDocumentos.Domain.Contracts;
using Microsoft.Extensions.Options;

namespace ClassificadorExtractorDocumentos.Infrastructure.Storage;

public class FileSystemDocumentStorage(IOptions<StorageOptions> options) : IDocumentStorage
{
    private readonly string _basePath = options.Value.BasePath;

    public async Task<(string RutaPdf, IReadOnlyList<string> RutasImagenes)> GuardarDocumentoAsync(
        Guid documentoId,
        byte[] pdfBytes,
        IReadOnlyList<byte[]> paginasPng,
        CancellationToken cancellationToken = default)
    {
        var carpetaDocumento = Path.Combine(_basePath, documentoId.ToString());
        Directory.CreateDirectory(carpetaDocumento);

        var rutaPdf = Path.Combine(carpetaDocumento, "original.pdf");
        await File.WriteAllBytesAsync(rutaPdf, pdfBytes, cancellationToken);

        var rutasImagenes = new List<string>();
        for (var i = 0; i < paginasPng.Count; i++)
        {
            var rutaImagen = Path.Combine(carpetaDocumento, $"page-{i + 1}.png");
            await File.WriteAllBytesAsync(rutaImagen, paginasPng[i], cancellationToken);
            rutasImagenes.Add(rutaImagen);
        }

        return (rutaPdf, rutasImagenes);
    }

    public async Task<IReadOnlyList<byte[]>?> ObtenerImagenesAsync(Guid documentoId, CancellationToken cancellationToken = default)
    {
        var carpetaDocumento = Path.Combine(_basePath, documentoId.ToString());
        if (!Directory.Exists(carpetaDocumento))
        {
            return null;
        }

        var rutas = Directory.GetFiles(carpetaDocumento, "page-*.png")
            .OrderBy(r => int.Parse(Path.GetFileNameWithoutExtension(r)["page-".Length..]))
            .ToList();

        var imagenes = new List<byte[]>(rutas.Count);
        foreach (var ruta in rutas)
        {
            imagenes.Add(await File.ReadAllBytesAsync(ruta, cancellationToken));
        }
        return imagenes;
    }
}
