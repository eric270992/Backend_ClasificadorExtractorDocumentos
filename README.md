# 📄 DocFlow AI — Ingesta inteligente de facturas

Sistema multiagente que **lee facturas de compra en PDF de cualquier formato, extrae los datos con
un LLM multimodal, los valida con reglas de negocio y los guarda** en una base de datos intermedia (staging).
Además, permite hacer **consultas en lenguaje natural** sobre los datos (text-to-SQL seguro, solo lectura).

Pensado para pymes que reciben facturas de proveedores con plantillas visualmente diferentes y que hoy las
teclean a mano en su ERP.

> **Estado**: Etapa 1 (demo vertical) completa y funcional. Etapa 2 (consolidación) pendiente. Véase
> [hoja de ruta](#-estado-y-hoja-de-ruta).

> **🔐 Login**: la aplicación **no tiene sistema de autenticación** (no aplica usuario/contraseña de prueba).
> Es de uso libre en cuanto se levanta con Docker.

---

## 🚀 Despliegue: probarlo en 1 minuto (Docker, sin código)

Si solo quieres **usar** la aplicación, **no hace falta descargar el código**. Con Docker se levanta todo
(base de datos + API + frontend) a partir de imágenes ya publicadas — esta es toda la información de
despliegue del proyecto.

1. Instala **[Docker](https://www.docker.com/products/docker-desktop/)**.
2. Descarga el fichero **[`docker-compose.deploy.yml`](docker-compose.deploy.yml)** de este repositorio.
3. A su lado, crea un fichero **`.env`**. Un **único bloque** con todos los campos; según el proveedor LLM
   que elijas, rellenas unos u otros (los comentarios explican cuáles):
   ```env
   # Contraseña de SQL Server (obligatoria). Complejidad: mayúsculas, minúsculas, número y símbolo.
   MSSQL_SA_PASSWORD=UnaClaveFuerte123!

   # Proveedor del LLM: "Groq" (nube, por defecto), "Nvidia" (nube, límite por petición no por
   # token) o "Local" (LM Studio / Ollama).
   LLM_PROVIDER=Groq

   # Solo si LLM_PROVIDER=Groq: tu clave (gratis en https://console.groq.com).
   # Deja esta línea vacía o bórrala si usas otro proveedor.
   GROQ_API_KEY=gsk_tu_clave

   # Modelo de Groq (debe soportar visión). Groq deprecia modelos de vez en cuando
   # (https://console.groq.com/docs/deprecations) — cámbialo aquí si el actual deja de existir.
   GROQ_MODEL=qwen/qwen3.6-27b

   # Solo si LLM_PROVIDER=Nvidia: tu clave gratis (sin tarjeta) en https://build.nvidia.com.
   # Límite de 40 peticiones/minuto en vez de tokens/minuto — mejor si Groq se queda corto con
   # imágenes grandes. OJO: el Free Endpoint solo admite 1 imagen por petición (facturas de 1 página).
   # Deja vacía si usas otro proveedor.
   NVIDIA_API_KEY=nvapi-tu_clave
   NVIDIA_MODEL=nvidia/llama-3.1-nemotron-nano-vl-8b-v1

   # Solo si LLM_PROVIDER=Local: dónde escucha tu servidor LLM y qué modelo cargar. qwen2.5-vl-7b
   # es el modelo de visión que hemos probado en LM Studio; puedes cargar otro modelo multimodal.
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

**Reiniciar desde cero** (por ejemplo, si cambias `MSSQL_SA_PASSWORD` en el `.env`: la contraseña de SQL
Server solo se fija al crear el volumen la primera vez, cambiarla después no basta con un `down`/`up`):
```bash
docker compose -f docker-compose.deploy.yml down -v
docker compose -f docker-compose.deploy.yml up -d
```
`-v` borra también los volúmenes (base de datos y ficheros): recrea todo limpio en el siguiente `up`.

### Groq, Nvidia (nube) o LLM local (LM Studio / Ollama)

El proveedor se elige con `LLM_PROVIDER` en el **mismo `.env` de arriba** (un solo fichero, un solo bloque):

- **`LLM_PROVIDER=Groq`** (por defecto): rellena `GROQ_API_KEY`. Es lo más rápido para probar, pero
  su capa gratuita limita por **tokens/minuto** — con imágenes de factura a alta resolución, una
  sola página puede agotar el límite (ver [comparativa-llm-local.md](docs/comparativa-llm-local.md)).
- **`LLM_PROVIDER=Nvidia`**: rellena `NVIDIA_API_KEY` (gratis, sin tarjeta, en
  [build.nvidia.com](https://build.nvidia.com)). Su límite gratuito es por **petición** (40/min), no
  por token — mejor opción si Groq da error 413 "Request too large". Usa
  `nvidia/llama-3.1-nemotron-nano-vl-8b-v1`, un modelo especializado en "document intelligence"
  (OCR/extracción estructurada). **Límite del Free Endpoint: 1 imagen por petición** — vale para
  facturas de 1 página; con varias páginas el comportamiento no está garantizado (mejor usar
  Groq o Local en ese caso).
- **`LLM_PROVIDER=Local`**: apunta `LLM_LOCAL_BASEURL` / `LLM_LOCAL_MODEL` a tu servidor y **deja
  las claves de Groq/Nvidia vacías**. Probado con **`qwen2.5-vl-7b`** (modelo de visión) en LM
  Studio — hay que activar **"Serve on Local Network"** (que escuche en `0.0.0.0`), o el contenedor
  no llegará hasta él. Cualquier otro modelo multimodal servido con una API compatible OpenAI
  también funciona.

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
6. Permite **aprobar manualmente** una factura en Revisión humana y **eliminarla** (lógicamente, nunca
   se borra físicamente) para poder reprocesar el mismo proveedor+número si hace falta.

## 🏗️ Arquitectura

Clean Architecture con 4 capas (.NET). Todas las dependencias apuntan al **Domain**, que no depende de nada:

```
Api  →  Application  →  Domain  ←  Infrastructure
(REST)   (agentes,       (reglas,     (EF Core, cliente LLM,
          orquestador)   contratos)    PDF→imagen, Dapper)
```

📖 **Explicación detallada (para quien viene de arquitectura N-capas)**: [`docs/arquitectura.md`](docs/arquitectura.md)
— incluye diagramas Mermaid y un glosario DDD ↔ N-capas.

## 📁 Estructura del proyecto

```
ClassificadorExtractorDocumentos/
├── src/
│   ├── ClassificadorExtractorDocumentos.Domain/          # Núcleo: entidades, contratos, reglas (sin dependencias)
│   │   ├── Entities/                                     # FacturaStaging, FacturaLinea, Proveedor...
│   │   ├── Contracts/                                    # Interfaces (IDAO/IService) + DTOs
│   │   ├── Validacion/Reglas/                             # Las 9 reglas de negocio (Strategy)
│   │   ├── ValueObjects/ · Parsers/                       # Nif, FechaParser, NumeroParser
│   │   │
│   ├── ClassificadorExtractorDocumentos.Application/      # Casos de uso: agentes + orquestador
│   │   ├── Extraccion/ · Validacion/ · Consultor/         # Agentes Extractor, Validador, Consultor
│   │   ├── Ingesta/ (+ Ingesta/Maf/)                      # Orquestador manual y Workflow MAF
│   │   ├── Llm/ · Prompts/                                # Prompts versionados (.md)
│   │   │
│   ├── ClassificadorExtractorDocumentos.Infrastructure/   # Detalles técnicos: EF Core, LLM, PDF→imagen
│   │   ├── Persistence/ (+ Configurations/)               # DbContext, repositorios, mapeo EF
│   │   ├── Llm/ · Pdf/ · Imagen/ · Storage/ · Consultor/
│   │   │
│   └── ClassificadorExtractorDocumentos.Api/              # Entrada REST (Controllers, Program.cs)
│       └── Controllers/                                  # DocumentosController, FacturasController, ConsultasController
│
├── tests/
│   └── ClassificadorExtractorDocumentos.UnitTests/        # 157 tests xUnit (reglas, parsers, SQL-guard...)
│
├── docs/                                                  # Documentación, dataset de prueba y presentación
│   ├── installation-guide.md · arquitectura.md · ...
│   ├── datasets/                                          # 43 PDFs de prueba (generados + reales)
│   └── DocFlowAI-Presentacion.pptx                        # Slides del proyecto
│
├── docker-compose.yml · docker-compose.deploy.yml · Dockerfile
└── README.md
```

> El **frontend Angular** vive en un repositorio separado:
> [Frontend_ClasificadorExtractorDocumentos](https://github.com/eric270992/Frontend_ClasificadorExtractorDocumentos).

## 🧰 Stack tecnológico

| Ámbito | Tecnología |
|---|---|
| Backend | .NET 10 · Minimal hosting · Controllers REST |
| Persistencia | SQL Server (LocalDB en dev) · EF Core (escritura) · Dapper (consultas del Consultor) |
| LLM | API compatible OpenAI, intercambiable por configuración: **Groq** (Qwen3.6 27B), **Nvidia** (Nemotron Nano VL) o **local** (LM Studio / Qwen2.5-VL) |
| PDF → imagen | PDFtoImage / Pdfium (stack .NET nativo, sin microservicio Python) |
| Frontend | Angular 21 (standalone) · PrimeNG 21 |
| Tests | xUnit (157 tests unitarios) |

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
| `POST` | `/facturas/{id}/aprobar` | Aprobación manual: solo válida si está en Revisión humana → pasa a Validada |
| `DELETE` | `/facturas/{id}` | Eliminación lógica (soft delete): deja de listarse, pero no se borra físicamente |
| `POST` | `/consultas` | Pregunta en lenguaje natural → respuesta + SQL ejecutado |

## 🧪 Tests

```bash
dotnet test
```

Tres capas previstas (SPEC §5): **unitarios** (E1, ✅ 157 tests), **integración** con LLM mockeado (E2) y
**evals** contra ground truth con LLM real (E2). El dataset de prueba está en `docs/datasets/`.

## 📊 Estado y hoja de ruta

**Etapa 1 — demo vertical (✅ completa):**
- [x] Esqueleto, ingesta PDF→imagen, BD completa
- [x] Agente Extractor (nivel 3 genérico) + parsers + evaluación sobre dataset
- [x] Agente Validador (9 reglas) + orquestador transaccional + endpoints
- [x] Agente Consultor (text-to-SQL seguro)
- [x] Frontend Angular + PrimeNG (subida, lista, chat) + guion de demo
- [x] Aprobación manual y eliminación lógica de facturas

**Etapa 2 — consolidación (pendiente):**
- [ ] Migración a Microsoft Agent Framework (orquestación)
- [ ] Few-shot por proveedor (nivel 2) + evaluaciones automáticas
- [ ] Pantalla de revisión humana editable
- [ ] Integrador ERP simulado + tests de integración

## 🎬 Presentación

- **Slides**: [`docs/DocFlowAI-Presentacion.pptx`](docs/DocFlowAI-Presentacion.pptx) — qué se ha construido,
  cómo, stack, dificultades encontradas y despliegue.

## 📚 Documentación

| Documento | Contenido |
|---|---|
| [`docs/installation-guide.md`](docs/installation-guide.md) | **Guía de instalación y uso** (montar en un PC nuevo, paso a paso) |
| [`docs/de-n-capas-a-clean-architecture.md`](docs/de-n-capas-a-clean-architecture.md) | **Resumen rápido**: tu vocabulario IDAO/DAO/IService → ficheros reales |
| [`docs/arquitectura.md`](docs/arquitectura.md) | Guía de arquitectura con diagramas (para no expertos en DDD) |
| [`docs/guio-demo.md`](docs/guio-demo.md) | Guion de demo de 10 min + plan B |
| [`docs/comparativa-llm-local.md`](docs/comparativa-llm-local.md) | Groq vs LLM local: precisión, velocidad, coste |

---

> Proyecto desarrollado con metodología **Spec-Driven Development (SDD)**: una especificación completa
> guía el desarrollo; el código implementa contra ella.
