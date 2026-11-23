namespace ClassificadorExtractorDocumentos.Domain.Grounding;

/// <summary>
/// Caixa (rectangle) d'una zona del document, en coordenades NORMALITZADES 0-1 sobre la pàgina:
/// (0,0) és la cantonada de dalt a l'esquerra i (1,1) la de baix a la dreta.
/// <para>
/// Per què normalitzades i no en píxels o en punts PDF: la mateixa caixa ha de servir per pintar-la sobre
/// la imatge de 150 DPI que tenim a disc, sobre la imatge redimensionada que ha vist el model i, en el futur,
/// sobre una imatge a un altre DPI. Amb valors 0-1 n'hi ha prou de multiplicar per l'amplada/alçada real.
/// </para>
/// </summary>
public sealed record CajaNormalizada(double X1, double Y1, double X2, double Y2)
{
    /// <summary>Caixa mínima que conté les dues: serveix per ajuntar les paraules d'un valor que ocupa
    /// diverses paraules ("1.234,56 €", "5 de julio de 2026", el nom d'una empresa...).</summary>
    public CajaNormalizada Unir(CajaNormalizada otra) => new(
        Math.Min(X1, otra.X1),
        Math.Min(Y1, otra.Y1),
        Math.Max(X2, otra.X2),
        Math.Max(Y2, otra.Y2));

    /// <summary>Alçada de la caixa (0-1). Es fa servir per decidir si dues paraules són de la mateixa fila.</summary>
    public double Alto => Y2 - Y1;

    /// <summary>Centre vertical de la caixa (0-1).</summary>
    public double CentroY => (Y1 + Y2) / 2;

    /// <summary>Construeix una caixa assegurant que els valors queden dins 0-1 i ben ordenats (X1 ≤ X2, Y1 ≤ Y2).
    /// Els models de visió de tant en tant retornen coordenades girades o lleugerament fora de la pàgina:
    /// millor corregir-ho aquí que pintar una caixa negativa al frontend.</summary>
    public static CajaNormalizada Crear(double x1, double y1, double x2, double y2)
    {
        static double Limitar(double valor) => Math.Clamp(valor, 0.0, 1.0);

        return new CajaNormalizada(
            Limitar(Math.Min(x1, x2)),
            Limitar(Math.Min(y1, y2)),
            Limitar(Math.Max(x1, x2)),
            Limitar(Math.Max(y1, y2)));
    }
}

/// <summary>Una paraula de la capa de text del PDF amb la seva posició a la pàgina.</summary>
public sealed record PalabraDocumento(string Texto, CajaNormalizada Caja);

/// <summary>Totes les paraules d'una pàgina.</summary>
/// <param name="Pagina">Comença per 1, igual que les imatges page-1.png, page-2.png... que es desen a disc.</param>
/// <param name="RotacionGrados">Rotació declarada al PDF (0, 90, 180, 270). Si no és 0, les caixes no quadraran
/// amb la imatge (limitació de la prova) i el servei ho avisa.</param>
/// <param name="Palabras">Paraules de la pàgina, en l'ordre que les retorna el lector de PDF.</param>
public sealed record PaginaPalabras(int Pagina, int RotacionGrados, IReadOnlyList<PalabraDocumento> Palabras);
