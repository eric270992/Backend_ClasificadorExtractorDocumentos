# Grounding: d'on surt cada dada (prova)

Branca: `feature/grounding` (backend i frontend).

## Objectiu

Mostrar, sobre la imatge del document, la zona d'on s'ha tret cada camp de la factura (NIF, número, data, totals i línies), pintada per colors. És el primer pas de la idea del "mapa" de procedència. Més endavant l'usuari podrà corregir una caixa i aquesta correcció servirà d'exemple per al proveïdor (few-shot o àncores).

## Dos mètodes, per comparar-los

| | Híbrid (`metodo=hibrido`) | LLM (`metodo=llm`) |
|---|---|---|
| Com funciona | Agafa els valors ja extrets i els busca entre les paraules de la capa de text del PDF (PdfPig), que porten coordenades exactes | El model de visió rep les imatges i la llista de valors i retorna una `bbox_2d` per a cada camp |
| Precisió | Exacta (és la posició real de les paraules) | Aproximada |
| Cost | Cap crida al model (uns ms) | Una crida al model sencera |
| Documents | Només PDFs amb capa de text | També fotos i escanejats |
| Valor que no surt al document | Queda "no localitzat": és un senyal d'al·lucinació o de valor calculat | El model pot inventar-se'n la posició |

Tots dos busquen exactament els mateixos camps (`CamposDeFactura`). Així la comparació entre l'un i l'altre és justa.

## Peces

**Backend**

- `Domain/Grounding/PalabrasDocumento.cs`: `CajaNormalizada` (coordenades 0-1, origen a dalt a l'esquerra), `PalabraDocumento` i `PaginaPalabras`.
- `IPdfTextExtractor.ExtraerPalabrasAsync` (implementat a `PdfPigTextExtractor`): paraules amb la caixa normalitzada respecte de la CropBox, que és la mateixa zona que renderitza PDFium.
- `IDocumentStorage`: tres mètodes nous, `ObtenerImagenPaginaAsync`, `ContarPaginasAsync` i `ObtenerOriginalAsync`.
- `Application/Grounding/`:
  - `CamposDeFactura`: quins camps es busquen i amb quines pistes.
  - `LocalizadorHibrido`: agrupa les paraules en files, genera n-grames i compara cada valor segons el seu tipus:
    - número: per valor;
    - data: qualsevol format que entengui `FechaParser`;
    - text exacte: ignora puntuació i majúscules;
    - text aproximat: Levenshtein, amb un llindar de 0,85.
  - `GroundingLlmAgent`: prompt `Prompts/grounding.md` i lectura tolerant de la resposta.
  - `GroundingService`: ho orquestra tot. Es calcula **sota demanda**, no es desa a la BD i no cal cap migració.
- `FacturasController`:
  - `GET /facturas/{id}/grounding?metodo=hibrido|llm`
  - `GET /facturas/{id}/paginas/{n}` (retorna el PNG de la pàgina)

**Frontend**

- `features/documentos/visor-documento/`: visor de la pàgina amb les caixes pintades en percentatges damunt la imatge, un selector de mètode i una llegenda. Si fas clic en un camp de la llegenda, es ressalta la caixa i s'hi salta de pàgina; si fas clic en una caixa, es ressalta el camp.
- S'obre des del detall de la factura amb el botó **"D'on surt cada dada"**.

## Com es trien les caixes quan un valor surt més d'una vegada

- **Totals** (`PreferirUltimo`): s'agafa l'última aparició, perquè els totals són al peu.
- **Import de línia** (`MismaFilaQue`): ha de ser a la mateixa fila que la descripció de la línia.
- **Descripció de línia** (`DespuesDe`): s'agafa la primera aparició posterior a la línia anterior.
- La resposta inclou `numCandidatos`. Si és més gran que 1, vol dir que s'ha hagut de triar, i és on més fàcilment es pot equivocar el mètode.

## Comprovació feta

S'ha passat el localitzador híbrid sobre les 35 factures de `docs/datasets/factures_generades_variades` amb els valors de `resum_factures_generades.json`:

- NIF, nom, número, data i total: s'han localitzat a totes.
- Base i quota d'IVA: no es troben a les 10 factures de la plantilla B, i la base tampoc a les 5 de la plantilla D. Això és correcte, perquè aquestes plantilles no imprimeixen aquests imports. És exactament el senyal que volem: *aquest valor no surt al document*.
- Temps: menys de 30 ms per factura.

## Limitacions conegudes (és una prova)

- Les pàgines amb `/Rotate` diferent de 0 tenen les caixes girades. El servei ho avisa a `avisos`.
- El mètode LLM demana coordenades 0-1000, que és el format natiu de Qwen3-VL. Amb Qwen2.5-VL (el perfil `Local`), que treballa en píxels absoluts, les caixes sortirien malament.
- Només es localitzen les primeres 40 línies (`CamposDeFactura.MaxLineas`).
- El mètode LLM no es desa: cada vegada que es tria al visor es torna a cridar el model.
- L'id del document es dedueix de la carpeta de `RutaPdfOriginal`. Si la idea es consolida, cal afegir una columna `DocumentoId` a `FacturasStaging`.

## Possibles passos següents

1. Calcular l'híbrid durant la ingesta i desar les caixes a la BD. Un camp no localitzat podria ser una regla més del Validator.
2. Correcció de caixes: l'usuari arrossega un rectangle, es llegeix el text que hi ha a sota i es desa com a correcció per proveïdor (amb el camp, la caixa i el valor).
3. Fer servir aquestes correccions com a exemples few-shot o àncores de posició per al nivell 2 d'extracció.
