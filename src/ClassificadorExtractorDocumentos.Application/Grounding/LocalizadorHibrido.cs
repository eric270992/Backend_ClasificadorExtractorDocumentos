using System.Globalization;
using System.Text;
using ClassificadorExtractorDocumentos.Domain.Grounding;
using ClassificadorExtractorDocumentos.Domain.Parsers;

namespace ClassificadorExtractorDocumentos.Application.Grounding;

/// <summary>
/// Grounding HÍBRID: el model ja ens ha donat el VALOR de cada camp; aquí busquem aquest valor entre les
/// paraules de la capa de text del PDF, que porten coordenades exactes. El model no ha de "dibuixar" res.
/// <para>
/// Per què és interessant: és determinista (mateix PDF → mateixes caixes), no costa cap crida al model i les
/// caixes són exactes al píxel. A més, si un valor no surt enlloc del document, és un senyal clar que el model
/// l'ha inventat o mal llegit. Limitació: només funciona amb PDFs que tenen capa de text (no amb fotos).
/// </para>
/// <para>
/// Com funciona, en tres passos:
/// 1) Les paraules s'agrupen en FILES (mateixa alçada a la pàgina), com faria una persona llegint.
/// 2) Per a cada camp es generen candidats: seqüències de 1 a N paraules seguides d'una fila (n-grames)
///    i es comparen amb el valor segons el seu tipus (número, data, text exacte o text aproximat).
/// 3) Si hi ha més d'un candidat, es tria amb les pistes del camp (mateixa fila que, després de, l'últim...).
/// </para>
/// Classe estàtica i sense dependències: tota la lògica es pot provar amb paraules inventades, sense PDF.
/// </summary>
public static class LocalizadorHibrido
{
    /// <summary>Màxim de paraules seguides que formen un candidat. Una descripció de línia o un nom d'empresa
    /// llarg poden tenir 8-10 paraules; més enllà ja seria barrejar columnes.</summary>
    private const int MaxPalabrasPorCandidato = 12;

    /// <summary>Un import ocupa com a molt 2 paraules ("1.234,56" + "€", o "1 234,56" amb espai de milers).</summary>
    private const int MaxPalabrasNumero = 2;

    /// <summary>Una data textual pot ocupar fins a 5 paraules: "5 de julio de 2026".</summary>
    private const int MaxPalabrasFecha = 5;

    /// <summary>Un NIF o número de factura pot venir partit: "FAC 2026 / 001".</summary>
    private const int MaxPalabrasTextoExacto = 4;

    /// <summary>Similitud mínima (0-1) per acceptar un text aproximat. 0.85 tolera 1-2 lletres diferents en un nom
    /// de 15 caràcters ("ELECTRONICA" / "ELECTRÓNICA S.L.") però no confon dues empreses diferents.</summary>
    private const double SimilitudMinimaAproximada = 0.85;

    /// <summary>Penalització quan només coincideix el PRINCIPI del text (descripció partida en dues files):
    /// es puntua una mica per sota d'una coincidència completa perquè, si n'hi ha una de completa, guanyi.</summary>
    private const double PenalizacionCoincidenciaParcial = 0.95;

    /// <summary>Dues paraules són de la mateixa fila si els seus centres verticals difereixen menys que aquesta
    /// fracció de l'alçada de la lletra. Mateix criteri que PdfPigTextExtractor per reconstruir el text.</summary>
    private const double ToleranciaFilaRelativaAltura = 0.5;

    /// <summary>Si dos candidats tenen una puntuació que es diferencia menys d'això, es consideren empatats i
    /// decideixen les pistes de posició (després de, mateixa fila...).</summary>
    private const double MargenEmpate = 0.02;

    public static IReadOnlyList<CampoLocalizado> Localizar(
        IReadOnlyList<PaginaPalabras> paginas, IReadOnlyList<CampoBuscado> campos)
    {
        var filas = AgruparEnFilas(paginas);

        // Candidat triat per a cada camp ja resolt: el necessiten les pistes MismaFilaQue i DespuesDe
        var elegidos = new Dictionary<string, Candidato>();
        var resultados = new List<CampoLocalizado>(campos.Count);

        foreach (var campo in campos)
        {
            var candidatos = BuscarCandidatos(filas, campo);
            var elegido = Elegir(campo, candidatos, elegidos);

            if (elegido is null)
            {
                resultados.Add(new CampoLocalizado(campo.Campo, campo.Etiqueta, campo.ValorMostrado,
                    Pagina: null, Caja: null, Puntuacion: null, NumCandidatos: 0));
                continue;
            }

            elegidos[campo.Campo] = elegido;
            resultados.Add(new CampoLocalizado(campo.Campo, campo.Etiqueta, campo.ValorMostrado,
                elegido.Pagina, elegido.Caja, Math.Round(elegido.Puntuacion, 3), candidatos.Count));
        }

        return resultados;
    }

    // ─────────────────────────────────────────── 1) Files ───────────────────────────────────────────

    /// <summary>Una fila de text. <paramref name="Orden"/> és global a tot el document (pàgina 1 primer), de manera
    /// que comparar dos Orden diu quina fila es llegeix abans, encara que siguin de pàgines diferents.</summary>
    private sealed record Fila(int Pagina, int Orden, IReadOnlyList<PalabraDocumento> Palabras);

    private static List<Fila> AgruparEnFilas(IReadOnlyList<PaginaPalabras> paginas)
    {
        var filas = new List<Fila>();

        foreach (var pagina in paginas.OrderBy(pagina => pagina.Pagina))
        {
            // De dalt a baix (Y normalitzada creix cap avall) i, a igual alçada, d'esquerra a dreta
            var palabrasOrdenadas = pagina.Palabras
                .OrderBy(palabra => palabra.Caja.CentroY)
                .ThenBy(palabra => palabra.Caja.X1);

            var filaActual = new List<PalabraDocumento>();
            double? centroFilaActual = null;

            foreach (var palabra in palabrasOrdenadas)
            {
                var tolerancia = palabra.Caja.Alto * ToleranciaFilaRelativaAltura;
                if (centroFilaActual is not null && Math.Abs(palabra.Caja.CentroY - centroFilaActual.Value) > tolerancia)
                {
                    TancarFila(filas, pagina.Pagina, filaActual);
                    filaActual = [];
                }

                // El centre de la fila és el de la primera paraula: així una fila no "llisca" cap avall paraula a paraula
                if (filaActual.Count == 0)
                {
                    centroFilaActual = palabra.Caja.CentroY;
                }
                filaActual.Add(palabra);
            }

            TancarFila(filas, pagina.Pagina, filaActual);
        }

        return filas;
    }

    private static void TancarFila(List<Fila> filas, int pagina, List<PalabraDocumento> palabras)
    {
        if (palabras.Count == 0)
        {
            return;
        }
        // Dins la fila, d'esquerra a dreta: és l'ordre en què es llegeixen les paraules seguides d'un valor
        var ordenadas = palabras.OrderBy(palabra => palabra.Caja.X1).ToList();
        filas.Add(new Fila(pagina, filas.Count, ordenadas));
    }

    // ─────────────────────────────────────────── 2) Candidats ───────────────────────────────────────────

    /// <summary>Una zona del document que coincideix amb el valor buscat.</summary>
    /// <param name="Inicio">Índex de la primera paraula dins la fila.</param>
    /// <param name="Fin">Índex de l'última paraula dins la fila (inclosa).</param>
    private sealed record Candidato(int Pagina, int OrdenFila, int Inicio, int Fin, CajaNormalizada Caja, double Puntuacion);

    private static List<Candidato> BuscarCandidatos(List<Fila> filas, CampoBuscado campo)
    {
        if (campo.Numero is { } numero)
        {
            return BuscarPorComparacion(filas, MaxPalabrasNumero, texto => CoincideNumero(texto, numero));
        }

        if (campo.Fecha is { } fecha)
        {
            var fechaIso = fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return BuscarPorComparacion(filas, MaxPalabrasFecha, texto => FechaParser.Parse(texto) == fechaIso ? 1.0 : null);
        }

        if (string.IsNullOrWhiteSpace(campo.Texto))
        {
            return [];
        }

        return campo.Aproximado
            ? BuscarAproximado(filas, campo.Texto)
            : BuscarTextoExacto(filas, campo.Texto);
    }

    /// <summary>
    /// Recorre tots els n-grames (1..maxPalabras paraules seguides) de cada fila i es queda amb els que la funció
    /// <paramref name="comparar"/> accepta (retorna una puntuació; null = no coincideix).
    /// <para>
    /// Si un n-grama curt ja coincideix, els n-grames més llargs que el contenen es descarten: "1.234,56" i
    /// "1.234,56 €" són la mateixa aparició, i la caixa bona és la més ajustada.
    /// </para>
    /// </summary>
    private static List<Candidato> BuscarPorComparacion(List<Fila> filas, int maxPalabras, Func<string, double?> comparar)
    {
        var candidatos = new List<Candidato>();

        foreach (var fila in filas)
        {
            var candidatosFila = new List<Candidato>();
            for (var inicio = 0; inicio < fila.Palabras.Count; inicio++)
            {
                // De més curt a més llarg: el primer que coincideix des d'aquest inici és el més ajustat
                for (var longitud = 1; longitud <= maxPalabras && inicio + longitud <= fila.Palabras.Count; longitud++)
                {
                    var fin = inicio + longitud - 1;
                    var texto = UnirTexto(fila, inicio, fin);
                    if (comparar(texto) is not { } puntuacion)
                    {
                        continue;
                    }

                    candidatosFila.Add(new Candidato(fila.Pagina, fila.Orden, inicio, fin, UnirCaja(fila, inicio, fin), puntuacion));
                    break;
                }
            }

            candidatos.AddRange(DescartarContenidos(candidatosFila));
        }

        return candidatos;
    }

    /// <summary>Treu els candidats que contenen completament un altre candidat de la mateixa fila
    /// (p. ej. "€ 45,13" conté "45,13", que ja és una aparició més ajustada del mateix valor).</summary>
    private static IEnumerable<Candidato> DescartarContenidos(List<Candidato> candidatosFila) =>
        candidatosFila.Where(candidato => !candidatosFila.Any(otro =>
            !ReferenceEquals(otro, candidato) &&
            otro.Inicio >= candidato.Inicio && otro.Fin <= candidato.Fin &&
            (otro.Fin - otro.Inicio) < (candidato.Fin - candidato.Inicio)));

    private static double? CoincideNumero(string texto, decimal numero)
    {
        // Un percentatge ("21%", "21,00 %") mai és un import, encara que el número coincideixi
        if (texto.Contains('%'))
        {
            return null;
        }

        // Es compara el VALOR, no el text: "1.234,56 €", "1234.56" i "1,234.56" són el mateix import.
        // Valor absolut perquè els abonaments poden sortir com "-12,00" o "12,00-" segons el programa que genera el PDF.
        var valor = NumeroParser.Parse(texto.Trim('-'));
        return valor is not null && Math.Abs(Math.Round(valor.Value, 2)) == Math.Abs(Math.Round(numero, 2))
            ? 1.0
            : null;
    }

    private static List<Candidato> BuscarTextoExacto(List<Fila> filas, string textoBuscado)
    {
        var objetivo = NormalizarCompacto(textoBuscado);

        // Valors massa curts ("1", "A") coincidirien amb mig document: millor no localitzar-los que pintar-los malament
        if (objetivo.Length < 3)
        {
            return [];
        }

        return BuscarPorComparacion(filas, MaxPalabrasTextoExacto, texto =>
        {
            var candidato = NormalizarCompacto(texto);
            if (candidato == objetivo)
            {
                return 1.0;
            }

            // Prefix enganxat al valor dins la mateixa paraula: "NIF:B12345678", "ESB12345678" (VAT intracomunitari),
            // "Nº2026001". Es tolera fins a 4 caràcters de més; si n'hi ha més, ja és un altre text que el conté.
            const int MaxCaracteresExtra = 4;
            return candidato.Contains(objetivo, StringComparison.Ordinal) && candidato.Length - objetivo.Length <= MaxCaracteresExtra
                ? 0.95
                : null;
        });
    }

    /// <summary>
    /// Text aproximat (noms, descripcions): per a cada fila es busca el n-grama més semblant al valor i, si supera
    /// <see cref="SimilitudMinimaAproximada"/>, és un candidat. Com a molt un candidat per fila: dins d'una mateixa
    /// fila, molts n-grames encavalcats passarien el llindar i només volem el millor.
    /// </summary>
    private static List<Candidato> BuscarAproximado(List<Fila> filas, string textoBuscado)
    {
        var objetivo = NormalizarCompacto(textoBuscado);
        if (objetivo.Length < 3)
        {
            return [];
        }

        var candidatos = new List<Candidato>();
        foreach (var fila in filas)
        {
            Candidato? mejorDeLaFila = null;

            for (var inicio = 0; inicio < fila.Palabras.Count; inicio++)
            {
                for (var longitud = 1; longitud <= MaxPalabrasPorCandidato && inicio + longitud <= fila.Palabras.Count; longitud++)
                {
                    var fin = inicio + longitud - 1;
                    var candidato = NormalizarCompacto(UnirTexto(fila, inicio, fin));

                    // Si el candidat ja és força més llarg que el valor, afegir-hi més paraules només l'allunya:
                    // es talla aquí. A més, estalvia milers de comparacions (Levenshtein és quadràtic).
                    if (candidato.Length > objetivo.Length * 1.3)
                    {
                        break;
                    }

                    var puntuacion = PuntuarAproximado(candidato, objetivo);
                    if (puntuacion < SimilitudMinimaAproximada)
                    {
                        continue;
                    }

                    // A igual puntuació, el més curt (caixa més ajustada)
                    if (mejorDeLaFila is null || puntuacion > mejorDeLaFila.Puntuacion + 1e-9)
                    {
                        mejorDeLaFila = new Candidato(fila.Pagina, fila.Orden, inicio, fin, UnirCaja(fila, inicio, fin), puntuacion);
                    }
                }
            }

            if (mejorDeLaFila is not null)
            {
                candidatos.Add(mejorDeLaFila);
            }
        }

        return candidatos;
    }

    private static double PuntuarAproximado(string candidato, string objetivo)
    {
        var similitudCompleta = Similitud(candidato, objetivo);

        // Descripció partida en dues files: a la primera fila només hi ha el principi del text. Si el candidat
        // cobreix almenys la meitat del valor, es compara amb el principi del valor (amb una petita penalització).
        var similitudParcial = 0.0;
        if (candidato.Length < objetivo.Length && candidato.Length >= objetivo.Length / 2)
        {
            similitudParcial = Similitud(candidato, objetivo[..candidato.Length]) * PenalizacionCoincidenciaParcial;
        }

        return Math.Max(similitudCompleta, similitudParcial);
    }

    // ─────────────────────────────────────────── 3) Triar ───────────────────────────────────────────

    private static Candidato? Elegir(CampoBuscado campo, List<Candidato> candidatos, Dictionary<string, Candidato> elegidos)
    {
        if (candidatos.Count == 0)
        {
            return null;
        }

        // Ordre de lectura: fila (global a tot el document) i, dins la fila, d'esquerra a dreta
        IEnumerable<Candidato> enOrden = candidatos.OrderBy(candidato => candidato.OrdenFila).ThenBy(candidato => candidato.Inicio);

        // Pista "després de": si l'anterior s'ha trobat, només valen candidats de files posteriors.
        // Si no en queda cap, s'ignora la pista (millor una caixa dubtosa que cap, i el NumCandidatos ja ho delata).
        if (campo.DespuesDe is not null && elegidos.TryGetValue(campo.DespuesDe, out var anterior))
        {
            var posteriores = enOrden.Where(candidato => candidato.OrdenFila > anterior.OrdenFila).ToList();
            if (posteriores.Count > 0)
            {
                enOrden = posteriores;
            }
        }

        // Pista "mateixa fila que": primer els de la mateixa fila; si no n'hi ha, els que venen just després
        if (campo.MismaFilaQue is not null && elegidos.TryGetValue(campo.MismaFilaQue, out var referencia))
        {
            var mismaFila = enOrden.FirstOrDefault(candidato => candidato.OrdenFila == referencia.OrdenFila);
            if (mismaFila is not null)
            {
                return mismaFila;
            }

            var siguiente = enOrden.FirstOrDefault(candidato => candidato.OrdenFila > referencia.OrdenFila);
            if (siguiente is not null)
            {
                return siguiente;
            }
        }

        // Entre els millor puntuats (empat dins el marge), el primer o l'últim segons el camp
        var lista = enOrden.ToList();
        var mejorPuntuacion = lista.Max(candidato => candidato.Puntuacion);
        var mejores = lista.Where(candidato => candidato.Puntuacion >= mejorPuntuacion - MargenEmpate).ToList();

        return campo.PreferirUltimo ? mejores[^1] : mejores[0];
    }

    // ─────────────────────────────────────────── Utilitats de text ───────────────────────────────────────────

    private static string UnirTexto(Fila fila, int inicio, int fin) =>
        string.Join(' ', fila.Palabras.Skip(inicio).Take(fin - inicio + 1).Select(palabra => palabra.Texto));

    private static CajaNormalizada UnirCaja(Fila fila, int inicio, int fin)
    {
        var caja = fila.Palabras[inicio].Caja;
        for (var indice = inicio + 1; indice <= fin; indice++)
        {
            caja = caja.Unir(fila.Palabras[indice].Caja);
        }
        return caja;
    }

    /// <summary>Majúscules, sense accents i només lletres i dígits: "B-12.345.678" → "B12345678",
    /// "Electrònica Olot, S.L." → "ELECTRONICAOLOTSL". Així la comparació no depèn de la puntuació ni dels
    /// espais, que són justament el que el model "arregla" quan extreu el valor.</summary>
    private static string NormalizarCompacto(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(descompuesto.Length);
        foreach (var caracter in descompuesto)
        {
            // FormD separa "ò" en "o" + accent: l'accent és NonSpacingMark i es descarta
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            if (char.IsLetterOrDigit(caracter))
            {
                resultado.Append(char.ToUpperInvariant(caracter));
            }
        }
        return resultado.ToString();
    }

    /// <summary>Similitud 0-1 basada en la distància de Levenshtein (quantes lletres cal inserir, esborrar o canviar
    /// per passar d'un text a l'altre), dividida per la longitud del més llarg. 1 = idèntics.</summary>
    private static double Similitud(string primero, string segundo)
    {
        if (primero.Length == 0 && segundo.Length == 0)
        {
            return 1.0;
        }
        var distancia = Levenshtein(primero, segundo);
        return 1.0 - (double)distancia / Math.Max(primero.Length, segundo.Length);
    }

    /// <summary>Levenshtein clàssic amb dues files (memòria O(n) en lloc d'una matriu sencera).</summary>
    private static int Levenshtein(string primero, string segundo)
    {
        var filaAnterior = new int[segundo.Length + 1];
        var filaActual = new int[segundo.Length + 1];
        for (var columna = 0; columna <= segundo.Length; columna++)
        {
            filaAnterior[columna] = columna;
        }

        for (var fila = 1; fila <= primero.Length; fila++)
        {
            filaActual[0] = fila;
            for (var columna = 1; columna <= segundo.Length; columna++)
            {
                var coste = primero[fila - 1] == segundo[columna - 1] ? 0 : 1;
                filaActual[columna] = Math.Min(
                    Math.Min(filaAnterior[columna] + 1, filaActual[columna - 1] + 1),
                    filaAnterior[columna - 1] + coste);
            }
            (filaAnterior, filaActual) = (filaActual, filaAnterior);
        }

        return filaAnterior[segundo.Length];
    }
}
