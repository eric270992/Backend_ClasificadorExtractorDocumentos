using ClassificadorExtractorDocumentos.Domain.Contracts;
using Microsoft.Extensions.Options;

namespace ClassificadorExtractorDocumentos.Infrastructure.Storage;

public class FileSystemDocumentStorage(IOptions<StorageOptions> options) : IDocumentStorage
{
    private readonly string _basePath = options.Value.BasePath;

    public async Task<(string RutaOriginal, IReadOnlyList<string> RutasImagenes)> GuardarDocumentoAsync(
        Guid documentoId,
        byte[] originalBytes,
        string extensionOriginal,
        IReadOnlyList<byte[]> paginasPng,
        CancellationToken cancellationToken = default)
    {
        using var activity = DocumentosTracing.Source.StartActivity("storage.guardar");
        activity?.SetTag("storage.paginas", paginasPng.Count);
        activity?.SetTag("storage.bytes", originalBytes.Length + paginasPng.Sum(p => (long)p.Length));

        var carpetaDocumento = Path.Combine(_basePath, documentoId.ToString());
        Directory.CreateDirectory(carpetaDocumento);

        var rutaOriginal = Path.Combine(carpetaDocumento, $"original.{extensionOriginal}");
        await File.WriteAllBytesAsync(rutaOriginal, originalBytes, cancellationToken);

        var rutasImagenes = new List<string>();
        for (var i = 0; i < paginasPng.Count; i++)
        {
            var rutaImagen = Path.Combine(carpetaDocumento, $"page-{i + 1}.png");
            await File.WriteAllBytesAsync(rutaImagen, paginasPng[i], cancellationToken);
            rutasImagenes.Add(rutaImagen);
        }

        return (rutaOriginal, rutasImagenes);
    }

    public async Task<IReadOnlyList<byte[]>?> ObtenerImagenesAsync(Guid documentoId, CancellationToken cancellationToken = default)
    {
        using var activity = DocumentosTracing.Source.StartActivity("storage.leer");

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

    public async Task<byte[]?> ObtenerImagenPaginaAsync(Guid documentoId, int pagina, CancellationToken cancellationToken = default)
    {
        using var activity = DocumentosTracing.Source.StartActivity("storage.leer_pagina");

        // El nom de fitxer el construïm nosaltres a partir d'un Guid i un int: no hi ha cap manera que
        // la petició HTTP colui un "../" i llegeixi fora de la carpeta del document.
        var rutaPagina = Path.Combine(_basePath, documentoId.ToString(), $"page-{pagina}.png");
        if (!File.Exists(rutaPagina))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(rutaPagina, cancellationToken);
    }

    public Task<int> ContarPaginasAsync(Guid documentoId, CancellationToken cancellationToken = default)
    {
        var carpetaDocumento = Path.Combine(_basePath, documentoId.ToString());
        var paginas = Directory.Exists(carpetaDocumento)
            ? Directory.GetFiles(carpetaDocumento, "page-*.png").Length
            : 0;
        return Task.FromResult(paginas);
    }

    public async Task<DocumentoOriginal?> ObtenerOriginalAsync(Guid documentoId, CancellationToken cancellationToken = default)
    {
        using var activity = DocumentosTracing.Source.StartActivity("storage.leer_original");

        var carpetaDocumento = Path.Combine(_basePath, documentoId.ToString());
        if (!Directory.Exists(carpetaDocumento))
        {
            return null;
        }

        // GuardarDocumentoAsync el desa com "original.{extensió}": n'hi ha exactament un per carpeta
        var rutaOriginal = Directory.GetFiles(carpetaDocumento, "original.*").FirstOrDefault();
        if (rutaOriginal is null)
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(rutaOriginal, cancellationToken);
        var extension = Path.GetExtension(rutaOriginal).TrimStart('.').ToLowerInvariant();
        return new DocumentoOriginal(bytes, extension);
    }
}
