# Comparativa de proveedores LLM — Groq (cloud) vs LM Studio (local)

> Fecha: 2026-07-12 · Mismo prompt, mismo pipeline, mismo dataset con ground truth.
> El cambio de proveedor es SOLO configuración (`Llm:BaseUrl`, `Llm:Model`, `Llm:UsarResponseFormatJson`)
> gracias a `ILlmClient` / `OpenAiCompatibleLlmClient` — mitigación §7 del SPEC verificada en la práctica.

## Configuraciones probadas

| | Groq (cloud) | LM Studio (local) |
|---|---|---|
| Modelo | `meta-llama/llama-4-scout-17b-16e-instruct` | `qwen/qwen2.5-vl-7b` (Q4) |
| Hardware | Datacenter Groq | RTX 3060 12 GB (ERIC-PC, `192.168.1.64:1234`) |
| `UsarResponseFormatJson` | `true` (`json_object`) | `false` (no soporta `json_object`; prompt + parser tolerante) |
| ApiKey | user-secrets | ninguna (LAN) |

## Resultados sobre el dataset generado (30 facturas)

| Plantilla | Groq / Scout | Local / Qwen 7B |
|---|---|---|
| A | **10/10** | **10/10** |
| B | 10/10 obligatorios (base/IVA no impresos) | 10/10 obligatorios (mismo comportamiento honesto) |
| C | **10/10** | 8/10 — 2 fallos en `base` (3793,29 ≠ 3134,95 · 1744,60 ≠ 1474,60) |
| Tiempo medio | ~1,5 s/factura | ~16 s/factura |
| Coste | API de pago (rate limits en tier gratuito) | 0 € |
| Privacidad | Los PDFs salen a la nube | Todo en LAN |

## Observaciones cualitativas

- **Los 2 fallos de plantilla C del modelo local son misreadings de importes** (dígitos mal leídos),
  el tipo de error más peligroso. La red de seguridad funcionó: `CUADRE_TOTAL`/`CUADRE_LINEAS`
  detectan la incoherencia y la factura cae a `RevisionHumana` en lugar de colarse.
- En la factura real WooCommerce, el 7B local **no leyó los NIF** (VAT IE/ES → null) que Scout sí
  extrajo. En facturas reales densas el gap es mayor que en las plantillas generadas.
- LM Studio no soporta `response_format: json_object` → añadida opción `Llm:UsarResponseFormatJson`.
  Sin JSON nativo, 0 reintentos de parseo en 30 extracciones igualmente (prompt + parser robustos).

## Recomendación de uso

| Escenario | Proveedor |
|---|---|
| Demo en directo (E1-F5) | Groq — velocidad (1-2 s) y máxima precisión |
| Plan B de la demo si cae la API | Dataset preprocesado (según SPEC) o local |
| Batch nocturno / datos sensibles de cliente | Local — coste 0, privacidad total, la revisión humana absorbe el gap de precisión |
| Evals E2-S2 | Ambos — la comparativa Δnivel2 vs nivel3 debe medirse también por proveedor |
