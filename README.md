# 📄 DocFlow AI — Ingesta inteligente de facturas

Sistema multiagente que **lee facturas de compra en PDF de cualquier formato, extrae los datos con
un LLM multimodal, los valida con reglas de negocio y los guarda** en una base de datos intermedia (staging).
Además, permite hacer **consultas en lenguaje natural** sobre los datos (text-to-SQL seguro, solo lectura).

Pensado para pymes que reciben facturas de proveedores con plantillas visualmente diferentes y que hoy las
teclean a mano en su ERP.

> **Estado**: Etapa 1 (demo vertical) completa y funcional. Etapa 2 (consolidación) pendiente. Véase
> [hoja de ruta](#-estado-y-hoja-de-ruta).

---

## 🚀 Probarlo en 1 minuto (Docker, sin código)

Si solo quieres **usar** la aplicación, **no hace falta descargar el código**. Con Docker se levanta todo
(base de datos + API + frontend) a partir de imágenes ya publicadas.

1. Instala **[Docker](https://www.docker.com/products/docker-desktop/)**.
2. Descarga el fichero **[`docker-compose.deploy.yml`](docker-compose.deploy.yml)** de este repositorio.
3. A su lado, crea un fichero **`.env`**. Un **único bloque** con todos los campos; según el proveedor LLM
   que elijas, rellenas unos u otros (los comentarios explican cuáles):
   ```env
   # Contraseña de SQL Server (obligatoria). Complejidad: mayúsculas, minúsculas, número y símbolo.
   MSSQL_SA_PASSWORD=UnaClaveFuerte123!

   # Proveedor del LLM: "Groq" (nube, por defecto) o "Local" (LM Studio / Ollama).
   LLM_PROVIDER=Groq

   # Solo si LLM_PROVIDER=Groq: tu clave (gratis en https://console.groq.com).
   # Si usas el LLM local, deja esta línea vacía o bórrala.
   GROQ_API_KEY=gsk_tu_clave

   # Solo si LLM_PROVIDER=Local: dónde escucha tu servidor LLM y qué modelo cargar.
   # host.docker.internal = el mismo PC que Docker. Si el LLM está en otra máquina de la red,
   # pon su IP, p. ej. http://192.168.1.64:1234/v1
   LLM_LOCAL_BASEURL=http://host.docker.internal:1234/v1
   LLM_LOCAL_MODEL=qwen/qwen2.5-vl-7b
   ```
4. Levántalo:
   ```bash
   docker compose -f docker-compose.deploy.yml up -d
   ```
5. Abre **http://localhost:8080** en el navegador → ya puedes **subir facturas** (PDF/JPEG/PNG) y **hacer
   consultas en lenguaje natural**.

Docker se descarga las imágenes (`ghcr.io/eric270992/docflow-ai-api` y `…-web`), arranca SQL Server, crea
la base de datos sola y sirve el frontend. Para pararlo: `docker compose -f docker-compose.deploy.yml down`.

### Groq (nube) o LLM local (LM Studio / Ollama)

El proveedor se elige con `LLM_PROVIDER` en el **mismo `.env` de arriba** (un solo fichero, un solo bloque):

- **`LLM_PROVIDER=Groq`** (por defecto): rellena `GROQ_API_KEY`. Es lo más rápido para probar.
- **`LLM_PROVIDER=Local`**: apunta `LLM_LOCAL_BASEURL` / `LLM_LOCAL_MODEL` a tu servidor y **deja
  `GROQ_API_KEY` vacía**. En LM Studio hay que activar **"Serve on Local Network"** (que escuche en
  `0.0.0.0`), o el contenedor no llegará hasta él.

> Guía completa (build desde el código, despliegue, publicación de imágenes):
> **[docs/installation-guide.md](docs/installation-guide.md)** §10.

---

## ✨ Qué hace

1. **Ingiere** un PDF (lo convierte a imágenes, una por página).
2. **Extrae** los datos estructurados con un LLM multimodal (sin OCR clásico ni fine-tuning): emisor,
   receptor, número, fechas, líneas, IVA, totales… nunca inventa (campo ausente → null).
3. **Valida** con 9 reglas de negocio (cuadres, coherencia de IVA, reverse charge, NIF, duplicados…) y
   asigna un estado: `Validada`, `Revisión humana` o `Rechazada`.
4. **Guarda** la factura, líneas e incidencias de forma transaccional.
5. **Responde preguntas en lenguaje natural** sobre las facturas, generando SQL seguro (solo `SELECT`,
   whitelist de tablas, `TOP 1000` forzado).

## 🏗️ Arquitectura

Clean Architecture con 4 capas (.NET). Todas las dependencias apuntan al **Domain**, que no depende de nada:

```
Api  →  Application  →  Domain  ←  Infrastructure
(REST)   (agentes,       (reglas,     (EF Core, cliente LLM,
          orquestador)   contratos)    PDF→imagen, Dapper)
```

📖 **Explicación detallada (para quien viene de arquitectura N-capas)**: [`docs/arquitectura.md`](docs/arquitectura.md)
— incluye diagramas Mermaid y un glosario DDD ↔ N-capas.

## 🧰 Stack tecnológico

| Ámbito | Tecnología |
|---|---|
| Backend | .NET 10 · Minimal hosting · Controllers REST |
| Persistencia | SQL Server (LocalDB en dev) · EF Core (escritura) · Dapper (consultas del Consultor) |
| LLM | API compatible OpenAI, intercambiable por configuración: **Groq** (Llama 4 Scout) o **local** (LM Studio / Qwen2.5-VL) |
| PDF → imagen | PDFtoImage / Pdfium (stack .NET nativo, sin microservicio Python) |
| Frontend | Angular 21 (standalone) · PrimeNG 21 |
| Tests | xUnit (133 tests unitarios) |

## 🚀 Cómo ejecutarlo

> 📖 **Guía completa paso a paso (instalación, PC nuevo, resolución de problemas)**:
> **[docs/installation-guide.md](docs/installation-guide.md)**

### Requisitos
- .NET 10 SDK · Node.js 20+ · SQL Server LocalDB
- Una clave de API de Groq **o** un servidor LLM local (LM Studio/Ollama)

### Backend

```bash
cd src/ClassificadorExtractorDocumentos.Api

# 1. Clave del LLM (el único secreto — nunca en el repo). La cadena de conexión ya está en appsettings.json.
dotnet user-secrets set "Llm:Perfiles:Groq:ApiKey" "<tu-clave>"

# 2. Arrancar (en dev la BD se crea y migra sola). Proveedor LLM por defecto: Groq
dotnet run
#   escucha en http://localhost:5255

# Alternativa: usar el LLM local en lugar de Groq
dotnet run -- --llm Local
```

### Frontend

El frontend vive en un repositorio separado
([Frontend_ClasificadorExtractorDocumentos](https://github.com/eric270992/Frontend_ClasificadorExtractorDocumentos)).
El proxy de dev redirige `/api` hacia el backend (evita CORS):

```bash
npm install
npm start
#   abre http://localhost:4200
```

## 🔌 Configuración del proveedor LLM

Perfiles con nombre en `appsettings.json` (`Llm:Perfiles`). El perfil activo se elige, por prioridad:

1. Argumento: `dotnet run -- --llm Local`
2. Variable de entorno: `Llm__Proveedor=Local`
3. `appsettings.json`: `"Llm": { "Proveedor": "Groq" }`

Añadir un proveedor nuevo es añadir un bloque a `Perfiles` — cero código. Véase la
[comparativa Groq vs local](docs/comparativa-llm-local.md).

## 🌐 API REST

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/documentos` | Sube un PDF y ejecuta el pipeline completo (ingesta → extracción → validación → staging) |
| `POST` | `/documentos/{id}/extraccion` | Extracción sin persistir (depuración/evaluación) |
| `GET` | `/facturas` | Lista de facturas con estado |
| `GET` | `/facturas/{id}` | Detalle con líneas e incidencias |
| `POST` | `/consultas` | Pregunta en lenguaje natural → respuesta + SQL ejecutado |

## 🧪 Tests

```bash
dotnet test
```

Tres capas previstas (SPEC §5): **unitarios** (E1, ✅ 133 tests), **integración** con LLM mockeado (E2) y
**evals** contra ground truth con LLM real (E2). El dataset de prueba está en `docs/datasets/`.

## 📊 Estado y hoja de ruta

**Etapa 1 — demo vertical (✅ completa):**
- [x] Esqueleto, ingesta PDF→imagen, BD completa
- [x] Agente Extractor (nivel 3 genérico) + parsers + evaluación sobre dataset
- [x] Agente Validador (9 reglas) + orquestador transaccional + endpoints
- [x] Agente Consultor (text-to-SQL seguro)
- [x] Frontend Angular + PrimeNG (subida, lista, chat) + guion de demo

**Etapa 2 — consolidación (pendiente):**
- [ ] Migración a Microsoft Agent Framework (orquestación)
- [ ] Few-shot por proveedor (nivel 2) + evaluaciones automáticas
- [ ] Pantalla de revisión humana editable
- [ ] Integrador ERP simulado + tests de integración

## 📚 Documentación

| Documento | Contenido |
|---|---|
| [`docs/installation-guide.md`](docs/installation-guide.md) | **Guía de instalación y uso** (montar en un PC nuevo, paso a paso) |
| [`docs/SPEC.md`](docs/SPEC.md) | Especificación completa (fuente de verdad, SDD) |
| [`docs/de-n-capas-a-clean-architecture.md`](docs/de-n-capas-a-clean-architecture.md) | **Resumen rápido**: tu vocabulario IDAO/DAO/IService → ficheros reales |
| [`docs/arquitectura.md`](docs/arquitectura.md) | Guía de arquitectura con diagramas (para no expertos en DDD) |
| [`docs/guio-demo.md`](docs/guio-demo.md) | Guion de demo de 10 min + plan B |
| [`docs/comparativa-llm-local.md`](docs/comparativa-llm-local.md) | Groq vs LLM local: precisión, velocidad, coste |
| [`docs/resultados-e1-f2.md`](docs/resultados-e1-f2.md) | Resultados de extracción sobre el dataset |

---

> Proyecto desarrollado con metodología **Spec-Driven Development (SDD)**: `docs/SPEC.md` es la fuente de
> verdad; el código implementa contra la especificación.
