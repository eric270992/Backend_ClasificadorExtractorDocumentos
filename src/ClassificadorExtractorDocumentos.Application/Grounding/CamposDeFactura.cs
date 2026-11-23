using System.Globalization;
using ClassificadorExtractorDocumentos.Domain.Entities;

namespace ClassificadorExtractorDocumentos.Application.Grounding;

/// <summary>
/// Decideix QUINS camps de la factura es busquen al document i amb QUINES pistes. És compartit pels dos
/// mètodes (híbrid i LLM) perquè la comparació entre tots dos sigui justa: busquen exactament el mateix.
/// <para>
/// Es fan servir els valors ACTUALS de la factura (després de les edicions manuals), no el JSON original:
/// així, si algú ha corregit un import a mà, la caixa mostra on és el valor bo.
/// </para>
/// </summary>
public static class CamposDeFactura
{
    /// <summary>Límit de línies que es localitzen. Una factura amb centenars de línies faria el prompt del mètode
    /// LLM enorme i lent; per a la prova n'hi ha prou amb les primeres.</summary>
    public const int MaxLineas = 40;

    public static IReadOnlyList<CampoBuscado> Construir(FacturaStaging factura)
    {
        var campos = new List<CampoBuscado>();

        // ── Capçalera. Els NIF i el número de factura han de coincidir exactament (un caràcter diferent és un
        //    altre NIF); els noms, en canvi, es comparen de manera aproximada.
        AfegirTexto(campos, "emisor.nif", "NIF emissor", factura.Proveedor?.Nif, aproximado: false);
        AfegirTexto(campos, "emisor.nombre", "Nom emissor", factura.Proveedor?.Nombre, aproximado: true);
        AfegirTexto(campos, "receptor.nif", "NIF receptor", factura.NifCliente, aproximado: false);
        AfegirTexto(campos, "receptor.nombre", "Nom receptor", factura.NombreCliente, aproximado: true);
        AfegirTexto(campos, "factura.numero", "Número factura", factura.NumeroFactura, aproximado: false);

        campos.Add(new CampoBuscado("factura.fecha", "Data factura", factura.FechaFactura.ToString("yyyy-MM-dd"),
            Fecha: factura.FechaFactura));
        if (factura.FechaVencimiento is { } vencimiento)
        {
            campos.Add(new CampoBuscado("factura.vencimiento", "Venciment", vencimiento.ToString("yyyy-MM-dd"),
                Fecha: vencimiento));
        }

        // ── Totals: PreferirUltimo perquè són al peu. Si l'import també surt en una línia (factura d'una sola
        //    línia: base = import de línia), la primera aparició seria la de la línia i no la del peu.
        AfegirImporteTotal(campos, "totales.baseImponible", "Base imposable", factura.BaseImponible);
        AfegirImporteTotal(campos, "totales.cuotaIva", "Quota IVA", factura.CuotaIva);
        AfegirImporteTotal(campos, "totales.retencionIrpf", "Retenció IRPF", factura.RetencionIrpf);
        AfegirImporteTotal(campos, "totales.total", "Total", factura.Total);

        // ── Línies, en ordre. Cada descripció es busca DESPRÉS de l'anterior (les línies van de dalt a baix) i
        //    cada import, a la MATEIXA FILA que la seva descripció. Sense aquestes pistes, un import repetit
        //    (dues línies de 10,00 €) aniria sempre a parar a la primera aparició.
        var lineas = factura.Lineas.OrderBy(linea => linea.NumLinea).Take(MaxLineas).ToList();
        for (var indice = 0; indice < lineas.Count; indice++)
        {
            var linea = lineas[indice];
            var campoDescripcion = $"lineas[{indice}].descripcion";
            var descripcionAnterior = indice > 0 ? $"lineas[{indice - 1}].descripcion" : null;

            campos.Add(new CampoBuscado(campoDescripcion, $"Línia {indice + 1}: descripció", linea.Descripcion,
                Texto: linea.Descripcion, Aproximado: true, DespuesDe: descripcionAnterior));

            campos.Add(new CampoBuscado($"lineas[{indice}].importeLinea", $"Línia {indice + 1}: import",
                FormatearImporte(linea.ImporteLinea), Numero: linea.ImporteLinea, MismaFilaQue: campoDescripcion));
        }

        return campos;
    }

    private static void AfegirTexto(List<CampoBuscado> campos, string campo, string etiqueta, string? valor, bool aproximado)
    {
        // Un camp buit no es pot localitzar: no s'afegeix (no és un error, el document no el portava)
        if (string.IsNullOrWhiteSpace(valor))
        {
            return;
        }
        campos.Add(new CampoBuscado(campo, etiqueta, valor, Texto: valor, Aproximado: aproximado));
    }

    private static void AfegirImporteTotal(List<CampoBuscado> campos, string campo, string etiqueta, decimal? importe)
    {
        // Un 0 (quota d'IVA amb inversió del subjecte passiu, retenció inexistent...) no es busca: hi ha zeros
        // per tot el document i qualsevol caixa que triéssim seria una endevinalla.
        if (importe is null || importe == 0)
        {
            return;
        }
        campos.Add(new CampoBuscado(campo, etiqueta, FormatearImporte(importe.Value),
            Numero: importe.Value, PreferirUltimo: true));
    }

    private static string FormatearImporte(decimal importe) => importe.ToString("0.00", CultureInfo.InvariantCulture);
}
