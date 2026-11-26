using ClassificadorExtractorDocumentos.Domain.Grounding;

namespace ClassificadorExtractorDocumentos.Application.Grounding;

/// <summary>
/// Un camp de la factura que volem localitzar dins el document, amb el valor que ja tenim desat.
/// <para>
/// Només un dels tres valors (<see cref="Texto"/>, <see cref="Numero"/>, <see cref="Fecha"/>) ve informat:
/// és el que decideix com es compara amb les paraules del PDF. No s'ha fet un enum de "tipus de camp"
/// perquè el tipus ja queda clar per quin valor està informat, i així no hi ha dues coses a mantenir quadrades.
/// </para>
/// </summary>
/// <param name="Campo">Clau estable del camp, amb la mateixa nomenclatura que <c>confidencePorCampo</c> del prompt
/// d'extracció: "emisor.nif", "totales.total", "lineas[0].importeLinea"... El frontend l'usa per relacionar
/// cada caixa amb el seu camp del formulari.</param>
/// <param name="Etiqueta">Nom llegible per mostrar a la llegenda del visor.</param>
/// <param name="ValorMostrado">El valor tal com es mostra a la llegenda (imports amb punt decimal, dates ISO).</param>
/// <param name="Texto">Valor de text que s'ha de trobar exactament (ignorant espais, punts, guions i majúscules).</param>
/// <param name="Numero">Import que s'ha de trobar (es compara el número, no el text: "1.234,56 €" = 1234.56).</param>
/// <param name="Fecha">Data que s'ha de trobar en qualsevol format ("05/07/2026", "5 de julio de 2026"...).</param>
/// <param name="Aproximado">Només per a <see cref="Texto"/>: true = es tolera alguna diferència (noms d'empresa,
/// descripcions de línia), perquè el model sovint corregeix majúscules, abreviatures o lletres mal llegides.</param>
/// <param name="PreferirUltimo">Si el valor surt diverses vegades, quedar-se amb l'última aparició del document.
/// Pensat per als totals: el mateix import pot sortir en una línia i al peu, i el que volem és el del peu.</param>
/// <param name="MismaFilaQue">Si s'informa, entre els candidats es prefereix el que és a la mateixa fila que la caixa
/// ja trobada per a aquest altre camp. Exemple: l'import d'una línia ha de ser a la fila de la seva descripció.</param>
/// <param name="DespuesDe">Si s'informa, només es busca després (en ordre de lectura) de la caixa trobada per a aquest
/// altre camp. Exemple: la línia 3 ha de ser després de la línia 2.</param>
public sealed record CampoBuscado(
    string Campo,
    string Etiqueta,
    string ValorMostrado,
    string? Texto = null,
    decimal? Numero = null,
    DateOnly? Fecha = null,
    bool Aproximado = false,
    bool PreferirUltimo = false,
    string? MismaFilaQue = null,
    string? DespuesDe = null);

/// <summary>On s'ha trobat un camp. Si no s'ha trobat, <see cref="Pagina"/> i <see cref="Caja"/> són null
/// (el frontend el mostra a la llegenda com "no localitzat", que també és informació útil).</summary>
/// <param name="Puntuacion">1.0 = coincidència exacta; menys = coincidència aproximada (noms, descripcions).
/// Null quan la caixa la dona el model de visió: no tenim cap mesura objectiva de com n'està de segur.</param>
/// <param name="NumCandidatos">Quantes zones del document coincidien amb el valor. Més d'1 vol dir que s'ha
/// hagut de triar (p. ej. un import que surt a una línia i al total): és on més s'equivocarà el mètode híbrid.
/// Null per al mètode LLM.</param>
public sealed record CampoLocalizado(
    string Campo,
    string Etiqueta,
    string Valor,
    int? Pagina,
    CajaNormalizada? Caja,
    double? Puntuacion,
    int? NumCandidatos);

/// <summary>Resposta de GET /facturas/{id}/grounding.</summary>
/// <param name="Metodo">"hibrido" o "llm" (vegeu <see cref="GroundingService.MetodoHibrido"/>).</param>
/// <param name="NumPaginas">Pàgines que té el document a disc: el visor les necessita per paginar.</param>
/// <param name="DuracionMs">Temps de càlcul. Serveix per comparar el cost dels dos mètodes.</param>
/// <param name="Avisos">Limitacions trobades (document sense capa de text, pàgines girades...), per mostrar-les.</param>
public sealed record ResultadoGrounding(
    int FacturaId,
    string Metodo,
    int NumPaginas,
    IReadOnlyList<CampoLocalizado> Campos,
    long DuracionMs,
    IReadOnlyList<string> Avisos);
