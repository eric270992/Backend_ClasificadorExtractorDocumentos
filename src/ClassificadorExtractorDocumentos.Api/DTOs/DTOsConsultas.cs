namespace ClassificadorExtractorDocumentos.Api.DTOs;

/// <summary>200 del consultor: resposta en llenguatge natural, el SQL que s'ha executat (sempre es retorna,
/// per transparència) i les dades. Filas té un diccionari columna → valor per fila.</summary>
public sealed record DTOConsultaRespuesta(
    string? Respuesta,
    string? Explicacion,
    string? Sql,
    int NumFilas,
    IReadOnlyList<string> Columnas,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Filas);

/// <summary>422 del consultor: no s'ha pogut generar o executar la consulta. SqlGenerado és nul si el
/// model ha declinat o l'ha bloquejat abans de tenir SQL.</summary>
public sealed record DTOConsultaRechazada(string? Motivo, string? SqlGenerado);
