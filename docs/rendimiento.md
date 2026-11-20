# Rendimiento del pipeline de extracción — puntos detectados

> Fecha: 2026-11-19 · Rama `fix/rendiment`. Contexto: con el servidor de IA de EIO (`Llm:Proveedor=EIO`,
> LiteLLM → vLLM, alias `erp-assistant`) la extracción funciona bien pero tarda mucho en responder.
> Este documento recoge TODO lo detectado en la revisión, lo ya aplicado y lo pendiente, ordenado por impacto.

## Cómo medir antes de tocar nada

Phoenix ya recoge las trazas. Para cada factura, comparar:

- Duración del span `pdf.to_images` frente a `llm.chat` (casi todo el tiempo debería estar en `llm.chat`).
- `llm.token_count.completion` frente a la longitud real del JSON devuelto: si los tokens de salida son
  muchos más de los que ocupa el JSON, el modelo está "pensando" (punto 1).
- `llm.token_count.prompt` y `llm.input_images.bytes` (nuevo): tamaño de la entrada.

## Mediciones reales (Phoenix, 2026-11-19, perfil EIO)

| Documento | Total | `llm.chat` | Tokens entrada | Tokens salida | Notas |
|---|---|---|---|---|---|
| PDF 1 pág., thinking ACTIVO | 232 s | 229 s | 3.467 | **15.781** | JSON de solo 2.646 caracteres: ~95 % de los tokens eran razonamiento. Casi agota `MaxTokens` (16.384). |
| PDF 1 pág., thinking off | 14,3 s | 9,0 s | 3.019 | 562 | Primera petición tras arrancar: ~1,8 s de PdfPig en frío y ~2,3 s tras el LLM (validación + BD en frío). |
| Imagen (foto), thinking off | 9,0 s | 6,6 s | 2.510 | 398 | ~2,3 s ANTES del LLM sin span: normalización de la imagen + disco + JPEG. |
| PDF 2 pág. (30 líneas), thinking off | 43,9 s | 42,9 s | 6.595 | 2.889 | Todo es generación. JSON de 6.620 caracteres, 1.892 de ellos espacios/sangrado (compacto: 4.816). |

Conclusiones:

- La velocidad de generación del servidor es estable, **~65-70 tokens/s**. El tiempo de `llm.chat` ≈ tokens de salida / 67.
- Con el thinking desactivado, lo que manda es **la longitud del JSON de salida** (crece con el número de líneas de la factura).
- La lectura de imágenes/prompt (prefill) es pequeña en comparación: con 1 imagen, ~1-2 s.
- Siguiente mejora barata: pedir el JSON **compacto (sin sangrado ni saltos de línea)** → ~27 % menos caracteres en este caso; estimado ~15-25 % menos tokens de salida.

## Aplicado en `fix/rendiment`

| Cambio | Detalle |
|---|---|
| Quitado `Console.WriteLine` del cuerpo de la petición | Serializaba la petición entera (imágenes base64 incluidas, varios MB) a consola/logs en cada llamada. |
| Imágenes al LLM en JPEG (configurable) | `Llm:Perfiles:*:FormatoImagen` (`jpeg` por defecto, `png` para volver atrás) y `CalidadJpeg` (90). En disco siguen guardándose PNG. Medido con 15 páginas reales de `App_Data`: **~30 % menos bytes** (3,6 MB → 2,5 MB), no 5-10x como se estimó al principio: las páginas de PDF digital ya comprimen bien en PNG. Las dimensiones no cambian, así que **los tokens de imagen son los mismos**; la ganancia es solo de subida por red. Comparación visual a 2x: texto idéntico. |
| Fotos de tiquets (JPEG/PNG) | El backend ya las aceptaba; ahora el frontend también. `SkiaImagenNormalizer` aplica la orientación EXIF (fotos de móvil giradas), reduce a máx. ~4 MP (una foto de 12 MP pasa a ~4 MP → muchos menos tokens) y aplana la transparencia sobre blanco. Regla 14 añadida al prompt: un tiquet/factura simplificada SÍ es factura y puede no tener receptor. |
| Desactivar thinking | Confirmado por el equipo: el modelo de `erp-assistant` tiene thinking y se puede desactivar en la llamada. Nueva opción por perfil `DesactivarThinking` (activada en el perfil EIO) → envía `chat_template_kwargs: { enable_thinking: false }`. Si el servidor devuelve `reasoning_content`, su longitud queda en la traza (`llm.reasoning.length`) para detectar si el flag no llega. |
| Límite de imágenes por perfil | El servidor de EIO devolvía 400 (`At most 4 image(s) may be provided in one prompt`): el vLLM está arrancado con un máximo de 4 imágenes. Nueva opción `MaxImagenesPorPeticion` (4 en el perfil EIO): se envían las primeras páginas y se registra un warning + tag `llm.input_images.descartadas`. El tope de 5 de `ExtractorAgent` (límite de Groq) se mantiene. |
| JSON compacto | El prompt pide el JSON en una sola línea, sin sangrado. Medido: el sangrado ocupaba ~27 % de los caracteres de la salida; como el tiempo de `llm.chat` es casi todo generación, se espera ~15-25 % menos de tiempo en facturas largas. El parser ya aceptaba JSON compacto. |
| Spans nuevos en Phoenix | `ingesta.documento`, `imagen.normalizar`, `storage.guardar`, `storage.leer`, `extraccion` (con evento si hay reintento), `validacion`, `bd.existe_factura`, `bd.guardar_factura`. Fuentes nuevas: `PipelineTracing`, `DocumentosTracing`, `PersistenciaTracing` (registradas en `Program.cs`). Sin paquetes NuGet nuevos (ActivitySource es de .NET). |
| Límite de páginas por documento | `Ingesta:MaxPaginasPorDocumento` (4). Un PDF con más páginas se rechaza (422) ANTES de extraer texto, renderizar o llamar al modelo, en lugar de recortar páginas sin avisar (en facturas largas los totales suelen estar en la última página). El frontend pide los límites a `GET /configuracion` al abrir la pantalla y los muestra; si recibe un 422, los vuelve a pedir. Al arrancar se avisa en el log si el límite supera `MaxImagenesPorPeticion` del perfil LLM. |

## Pendiente (revisar)

1. ~~Thinking del modelo~~ → aplicado (ver tabla). Pendiente solo verificar en Phoenix que `llm.token_count.completion` baja y `llm.reasoning.length` no aparece.
2. **Menos páginas cuando hay texto del PDF (impacto medio-alto).** `ExtractorAgent` envía hasta 5 páginas
   además del texto. Con `textoPdf` disponible, 1-2 páginas suelen bastar para la estructura → muchos menos
   tokens de entrada. Hacerlo configurable.
3. **Bajar DPI cuando hay texto (impacto medio).** 150 DPI → ~2.500-3.000 tokens/página. Con texto PDF como
   referencia, 110-120 DPI probablemente basta. Validar contra el dataset con ground truth antes de cambiarlo.
4. **Reintento de JSON = llamada completa (impacto medio, solo al fallar).** Medir cuántas extracciones acaban
   con `Reintentos = 1`; si son muchas, corregir el prompt en vez de pagar la segunda llamada.
5. **Consultor: 2 llamadas secuenciales (impacto medio en consultas).** Generar SQL + redactar respuesta.
   Con thinking, ambas lentas. Opción: devolver la tabla primero y redactar después / de forma opcional.
6. **`CancellationToken` no llega al Extractor en el workflow MAF.** `ExtractorExecutor` llama a
   `ExtraerAsync` sin token: si el usuario cancela o vence el timeout, la petición sigue ocupando la GPU. Si el
   servidor es compartido (`erp-assistant`), retrasa a los demás. Pasar el token en el mensaje del workflow.
7. **Menores (impacto bajo):** las páginas se escriben a disco y se vuelven a leer justo después
   (`ObtenerImagenesAsync`) — podrían viajar en `DocumentoIngestado`; codificación PNG de páginas (~100-300 ms);
   polling del frontend cada 4 s (consulta SQL ligera, no afecta al LLM).
