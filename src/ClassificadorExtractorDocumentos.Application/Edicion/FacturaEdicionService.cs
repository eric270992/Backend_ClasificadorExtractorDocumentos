using System.Globalization;
using ClassificadorExtractorDocumentos.Application.Validacion;
using ClassificadorExtractorDocumentos.Domain.Contracts;
using ClassificadorExtractorDocumentos.Domain.Entities;
using ClassificadorExtractorDocumentos.Domain.Validacion;

namespace ClassificadorExtractorDocumentos.Application.Edicion;

/// <summary>
/// Edició manual d'una factura de staging (capçalera i línies) amb revalidació.
///
/// Abans aquesta lògica era a FacturaStagingRepository.ActualizarAsync, però per revalidar cal el
/// ValidadorAgent, que és a Application: Infrastructure no el pot cridar. Per això ara ho ordena aquest
/// servei (Controller → Service → Repositori) i el repositori només llegeix i desa.
///
/// Passos: llegir → comprovar que es pot editar → aplicar els canvis (registrant l'historial) →
/// revalidar amb les 9 regles → si hi ha un Rechazo, NO es desa → si no, nou estat + incidències → desar.
/// </summary>
public class FacturaEdicionService(
    IFacturaStagingRepository repositorio,
    ValidadorAgent validador,
    TimeProvider timeProvider)
{
    /// <summary>Sense autenticació encara: tot canvi manual s'atribueix a aquest usuari fix.</summary>
    public const string UsuarioPorDefecto = "Usuario desconocido";

    // Han de coincidir amb les longituds de les configuracions EF (FacturaLinea.Descripcion, Historial.Valor*)
    private const int DescripcionMaxLength = 500;
    private const int ValorHistorialMaxLength = 500;

    public async Task<ResultadoEdicion> ActualizarAsync(
        int facturaId, ActualizarFacturaRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Llegir i comprovar que es pot editar (mateixes condicions que abans)
        var factura = await repositorio.ObtenerParaEdicionAsync(facturaId, cancellationToken);
        if (factura is null)
        {
            return ResultadoEdicion.Fallo($"No existe ninguna factura con id {facturaId}.");
        }
        if (factura.FechaEliminacion is not null)
        {
            return ResultadoEdicion.Fallo("La factura está eliminada.");
        }
        if (factura.Estado is not (EstadoFactura.Validada or EstadoFactura.RevisionHumana))
        {
            return ResultadoEdicion.Fallo(
                $"Solo se puede editar una factura en Validada o RevisionHumana (estado actual: {factura.Estado}).");
        }

        // 2. Validar la forma de les línies abans de tocar res (ids que no són d'aquesta factura, buits...)
        if (request.Lineas is not null && ValidarLineas(factura, request.Lineas) is { } errorLineas)
        {
            return ResultadoEdicion.Fallo(errorLineas);
        }

        var ahora = timeProvider.GetUtcNow().UtcDateTime;
        var cambios = new List<HistorialCambioFactura>();

        // Una fila d'historial per cada camp que canvia de valor (mateix criteri que l'edició de capçalera d'abans)
        void Registrar(string campo, string? anterior, string? nuevo)
        {
            if (anterior == nuevo)
            {
                return;
            }
            cambios.Add(new HistorialCambioFactura
            {
                FacturaId = facturaId,
                Campo = campo,
                ValorAnterior = Truncar(anterior, ValorHistorialMaxLength),
                ValorNuevo = Truncar(nuevo, ValorHistorialMaxLength),
                FechaCambio = ahora,
                Usuario = UsuarioPorDefecto,
            });
        }

        // 3. Capçalera
        AplicarCabecera(factura, request, Registrar);

        // 4. Línies (només si n'arriben: null = no tocar-les)
        var lineasEliminadas = request.Lineas is null
            ? []
            : AplicarLineas(factura, request.Lineas, Registrar);

        // Sense cap canvi no es revalida: si no, prémer "Desar" sense tocar res podria canviar l'estat
        // d'una factura aprovada manualment (les seves incidències de revisió hi continuen sent).
        if (cambios.Count == 0)
        {
            return ResultadoEdicion.SinCambios(factura.Estado);
        }

        // 5. Revalidar amb les dades editades
        var camposEditados = (await repositorio.ObtenerCamposEditadosAsync(facturaId, cancellationToken))
            .Concat(cambios.Select(c => c.Campo))
            .ToHashSet();
        var extraida = FacturaRevalidacionMapper.Map(factura, camposEditados);

        var existeDuplicado = extraida.Emisor.Nif is not null && extraida.Factura.Numero is not null &&
            await repositorio.ExisteOtraFacturaAsync(extraida.Emisor.Nif, extraida.Factura.Numero, facturaId, cancellationToken);

        var validacion = validador.Validar(
            ContextoValidacion.Crear(extraida, existeDuplicado, DateOnly.FromDateTime(ahora)));

        // 6. Un Rechazo (p. ex. número buit o duplicat) no es desa: la factura deixaria de ser integrable i,
        //    un cop Rechazada, ja no es podria tornar a editar. L'usuari rep el motiu i pot corregir-ho.
        var rechazos = validacion.Incidencias.Where(i => i.Severidad == SeveridadIncidencia.Rechazo).ToList();
        if (rechazos.Count > 0)
        {
            return ResultadoEdicion.Rechazada(
                "No se puede guardar: " + string.Join(" ", rechazos.Select(r => r.Detalle)),
                validacion.Incidencias);
        }

        // 7. Nou estat i incidències. Si estava aprovada a mà i l'edició deixa incidències de revisió, torna a
        //    RevisionHumana: l'aprovació era sobre les dades d'abans, no sobre les noves.
        Registrar("Estado", factura.Estado.ToString(), validacion.Estado.ToString());
        if (validacion.Estado != factura.Estado && validacion.Estado == EstadoFactura.RevisionHumana)
        {
            factura.FechaAprobacionManual = null;
        }
        factura.Estado = validacion.Estado;

        var incidenciasAnteriores = factura.Incidencias.ToList();
        factura.Incidencias.Clear();
        factura.Incidencias.AddRange(validacion.Incidencias.Select(i => new ValidacionIncidencia
        {
            Codigo = i.Codigo,
            Detalle = i.Detalle,
            FechaCreacion = ahora,
        }));

        // 8. Desar-ho tot en una transacció
        await repositorio.GuardarEdicionAsync(factura, lineasEliminadas, incidenciasAnteriores, cambios, cancellationToken);

        return ResultadoEdicion.Ok(factura.Estado, validacion.Incidencias);
    }

    private static void AplicarCabecera(
        FacturaStaging factura, ActualizarFacturaRequest request, Action<string, string?, string?> registrar)
    {
        registrar(nameof(FacturaStaging.NifCliente), factura.NifCliente, request.NifCliente);
        registrar(nameof(FacturaStaging.NombreCliente), factura.NombreCliente, request.NombreCliente);
        registrar(nameof(FacturaStaging.NumeroFactura), factura.NumeroFactura, request.NumeroFactura);
        registrar(nameof(FacturaStaging.FechaFactura), Texto(factura.FechaFactura), Texto(request.FechaFactura));
        registrar(nameof(FacturaStaging.FechaVencimiento), Texto(factura.FechaVencimiento), Texto(request.FechaVencimiento));
        registrar(nameof(FacturaStaging.Moneda), factura.Moneda, request.Moneda);
        registrar(nameof(FacturaStaging.BaseImponible), Texto(factura.BaseImponible), Texto(request.BaseImponible));
        registrar(nameof(FacturaStaging.CuotaIva), Texto(factura.CuotaIva), Texto(request.CuotaIva));
        registrar(nameof(FacturaStaging.RetencionIrpf), Texto(factura.RetencionIrpf), Texto(request.RetencionIrpf));
        registrar(nameof(FacturaStaging.Total), Texto(factura.Total), Texto(request.Total));
        registrar(nameof(FacturaStaging.ReverseCharge), Texto(factura.ReverseCharge), Texto(request.ReverseCharge));

        factura.NifCliente = request.NifCliente;
        factura.NombreCliente = request.NombreCliente;
        factura.NumeroFactura = request.NumeroFactura;
        factura.FechaFactura = request.FechaFactura;
        factura.FechaVencimiento = request.FechaVencimiento;
        factura.Moneda = request.Moneda;
        factura.BaseImponible = request.BaseImponible;
        factura.CuotaIva = request.CuotaIva;
        factura.RetencionIrpf = request.RetencionIrpf;
        factura.Total = request.Total;
        factura.ReverseCharge = request.ReverseCharge;
    }

    /// <summary>
    /// Aplica la llista de línies rebuda: elimina les que no hi són, modifica les existents i afegeix les noves.
    /// Retorna les eliminades perquè el repositori les esborri explícitament.
    /// A l'historial, les línies s'identifiquen pel seu número ABANS de l'edició ("Línea 3: Cantidad").
    /// </summary>
    private static List<FacturaLinea> AplicarLineas(
        FacturaStaging factura, IReadOnlyList<LineaEditada> lineas, Action<string, string?, string?> registrar)
    {
        var idsRecibidos = lineas.Where(l => l.Id is not null).Select(l => l.Id!.Value).ToHashSet();

        // a) Eliminades: les que hi havia i ja no arriben
        var eliminadas = factura.Lineas.Where(l => !idsRecibidos.Contains(l.Id)).ToList();
        foreach (var linea in eliminadas)
        {
            registrar("Línea eliminada", $"{linea.NumLinea}: {Describir(linea)}", null);
            factura.Lineas.Remove(linea);
        }

        // b) Modificades i noves, en l'ordre en què arriben (aquest és el nou ordre)
        var existentes = factura.Lineas.ToDictionary(l => l.Id);
        for (var i = 0; i < lineas.Count; i++)
        {
            var editada = lineas[i];
            var descripcion = editada.Descripcion.Trim();

            if (editada.Id is null)
            {
                var nueva = new FacturaLinea
                {
                    Descripcion = descripcion,
                    Cantidad = editada.Cantidad,
                    PrecioUnitario = editada.PrecioUnitario,
                    PorcentajeIva = editada.PorcentajeIva,
                    ImporteLinea = editada.ImporteLinea,
                };
                registrar("Línea añadida", null, Describir(nueva));
                nueva.NumLinea = i + 1;
                factura.Lineas.Add(nueva);
                continue;
            }

            var linea = existentes[editada.Id.Value];
            var prefijo = $"Línea {linea.NumLinea}: ";
            registrar(prefijo + nameof(FacturaLinea.Descripcion), linea.Descripcion, descripcion);
            registrar(prefijo + nameof(FacturaLinea.Cantidad), Texto(linea.Cantidad), Texto(editada.Cantidad));
            registrar(prefijo + nameof(FacturaLinea.PrecioUnitario), Texto(linea.PrecioUnitario), Texto(editada.PrecioUnitario));
            registrar(prefijo + nameof(FacturaLinea.PorcentajeIva), Texto(linea.PorcentajeIva), Texto(editada.PorcentajeIva));
            registrar(prefijo + nameof(FacturaLinea.ImporteLinea), Texto(linea.ImporteLinea), Texto(editada.ImporteLinea));

            linea.Descripcion = descripcion;
            linea.Cantidad = editada.Cantidad;
            linea.PrecioUnitario = editada.PrecioUnitario;
            linea.PorcentajeIva = editada.PorcentajeIva;
            linea.ImporteLinea = editada.ImporteLinea;
            // El canvi de número per reordenar no s'apunta a l'historial: és cosmètic, no canvia cap dada
            linea.NumLinea = i + 1;
        }

        return eliminadas;
    }

    /// <summary>Errors de forma de la llista de línies. Null = correcta.</summary>
    private static string? ValidarLineas(FacturaStaging factura, IReadOnlyList<LineaEditada> lineas)
    {
        var idsFactura = factura.Lineas.Select(l => l.Id).ToHashSet();
        var idsVistos = new HashSet<int>();

        for (var i = 0; i < lineas.Count; i++)
        {
            var l = lineas[i];
            var posicion = i + 1;
            if (l.Id is { } id)
            {
                // Un id d'una altra factura no s'ha de poder modificar des d'aquí
                if (!idsFactura.Contains(id))
                {
                    return $"La línea {posicion} hace referencia a una línea (id {id}) que no es de esta factura.";
                }
                if (!idsVistos.Add(id))
                {
                    return $"La línea con id {id} aparece repetida.";
                }
            }
            if (string.IsNullOrWhiteSpace(l.Descripcion))
            {
                return $"La línea {posicion} no tiene descripción.";
            }
            if (l.Descripcion.Trim().Length > DescripcionMaxLength)
            {
                return $"La descripción de la línea {posicion} supera los {DescripcionMaxLength} caracteres.";
            }
            if (l.PorcentajeIva is < 0m or > 100m)
            {
                return $"El % de IVA de la línea {posicion} debe estar entre 0 y 100.";
            }
        }
        return null;
    }

    /// <summary>Resum d'una línia per a l'historial: "Descripció | 2 x 10.50 = 21.00 | IVA 21%".</summary>
    private static string Describir(FacturaLinea l) =>
        $"{l.Descripcion} | {Texto(l.Cantidad)} x {Texto(l.PrecioUnitario)} = {Texto(l.ImporteLinea)} | IVA {Texto(l.PorcentajeIva) ?? "—"}%";

    private static string? Truncar(string? texto, int max) =>
        texto is null || texto.Length <= max ? texto : texto[..max];

    // Format invariant (punt decimal, dates ISO), igual que l'historial que ja hi havia.
    // Decimals amb "G29": treu els zeros finals. Un decimal recorda quants decimals té: el de la BD (decimal(18,2))
    // arriba com 121.00 i el del frontend com 121, i amb ToString() normal donaven "121.00" ≠ "121". Això feia que
    // es registrés un canvi fals cada cop que es desava sense tocar un import (ja passava a l'edició de capçalera).
    private static string? Texto(DateOnly? valor) => valor?.ToString("O");
    private static string Texto(DateOnly valor) => valor.ToString("O");
    private static string? Texto(decimal? valor) => valor?.ToString("G29", CultureInfo.InvariantCulture);
    private static string Texto(decimal valor) => valor.ToString("G29", CultureInfo.InvariantCulture);
    private static string Texto(bool valor) => valor.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Resultat d'una edició. Estado i Incidencias són els de després de revalidar.</summary>
public sealed record ResultadoEdicion(
    bool Exito,
    string? Motivo,
    EstadoFactura? Estado,
    IReadOnlyList<Incidencia> Incidencias,
    bool HuboCambios)
{
    public static ResultadoEdicion Ok(EstadoFactura estado, IReadOnlyList<Incidencia> incidencias) =>
        new(true, null, estado, incidencias, HuboCambios: true);

    public static ResultadoEdicion SinCambios(EstadoFactura estado) =>
        new(true, null, estado, [], HuboCambios: false);

    public static ResultadoEdicion Fallo(string motivo) =>
        new(false, motivo, null, [], HuboCambios: false);

    /// <summary>La revalidació ha donat algun Rechazo: no s'ha desat res.</summary>
    public static ResultadoEdicion Rechazada(string motivo, IReadOnlyList<Incidencia> incidencias) =>
        new(false, motivo, null, incidencias, HuboCambios: false);
}
