# 🛠️ Guía de instalación y uso — DocFlow AI

> Guía para poner en marcha el proyecto desde cero, o montarlo en un PC nuevo.
> Cubre el **backend** (.NET, este repositorio) y el **frontend** (Angular, proyecto aparte).

---

## 1. Requisitos previos (software a instalar)

| Software | Versión | Para qué | Comprobar |
|---|---|---|---|
| **.NET SDK** | 10.0.3xx o superior | Compilar y ejecutar el backend | `dotnet --version` |
| **SQL Server LocalDB** | 2019+ | Base de datos (staging) | `sqllocaldb info` |
| **Node.js** | 20 o superior (probado con 24) | Compilar y servir el frontend | `node --version` |
| **Git** | cualquiera reciente | Clonar el repositorio | `git --version` |
| **dotnet-ef** (opcional) | 10.x | Solo si quieres aplicar migraciones a mano | `dotnet ef --version` |

- **LocalDB** viene con "SQL Server Express LocalDB" o con Visual Studio (carga de trabajo de datos).
  Alternativa: cualquier SQL Server; solo hay que ajustar la cadena de conexión (paso 3.3).
- Instalar `dotnet-ef` si no está: `dotnet tool install --global dotnet-ef`
- Además necesitarás **una clave de API de Groq** (gratuita en https://console.groq.com) **o** un
  servidor LLM local (LM Studio/Ollama). Ver paso 4.

---

## 2. Estructura del proyecto

El sistema son **dos repositorios independientes**:

- **Backend**: API .NET + pipeline de extracción/validación + BD.
  → https://github.com/eric270992/Backend_ClasificadorExtractorDocumentos
- **Frontend**: la interfaz web Angular.
  → https://github.com/eric270992/Frontend_ClasificadorExtractorDocumentos

> ⚠️ **Importante para un PC nuevo**: son dos repositorios. Clona **los dos**; el backend por sí solo
> te da la API pero no la web. Cada uno se pone en marcha por separado (backend en el paso 3, frontend
> en el paso 5), y el frontend habla con el backend por HTTP.

---

## 3. Puesta en marcha del backend

### 3.1. Clonar y restaurar

```bash
git clone https://github.com/eric270992/Backend_ClasificadorExtractorDocumentos.git DocFlowAI
cd DocFlowAI
dotnet restore
```

### 3.2. Compilar (comprueba que todo está bien)

```bash
dotnet build
```

### 3.3. Configurar el secreto del LLM (⚠️ PASO IMPRESCINDIBLE)

La **clave del LLM** no está en el repositorio (por seguridad, SPEC §6): se guarda en
`dotnet user-secrets`, que es **por usuario y máquina**. En un PC nuevo hay que ponerla:

```bash
cd src/ClassificadorExtractorDocumentos.Api

# Clave de API de Groq (proveedor LLM por defecto)
dotnet user-secrets set "Llm:Perfiles:Groq:ApiKey" "gsk_tu_clave_de_groq"

cd ../..
```

> **La cadena de conexión a la BD SÍ viaja con el repositorio** (está en `appsettings.json`), porque
> apunta a LocalDB con autenticación de Windows y no contiene ningún secreto. Solo hay que tocarla si
> usas otra base de datos (ver 3.4). La clave del LLM es lo único que hay que configurar por máquina.

### 3.4. Base de datos (se crea sola)

**No hay que hacer nada**: al arrancar en desarrollo, la API **crea la base de datos y aplica las
migraciones automáticamente**. Solo necesitas tener SQL Server LocalDB instalado.

- Si usas **otra base de datos** (no LocalDB), cambia la cadena `ConnectionStrings:DocFlowDb` en
  `appsettings.json` (o mejor, sobreescríbela con una variable de entorno / user-secret para no tocar
  el repo).
- Si prefieres aplicar las migraciones **a mano** (opcional): 
  `dotnet ef database update --project src/ClassificadorExtractorDocumentos.Infrastructure --startup-project src/ClassificadorExtractorDocumentos.Api`

### 3.5. Ejecutar la API

```bash
cd src/ClassificadorExtractorDocumentos.Api
dotnet run
```

La API queda escuchando en **http://localhost:5255**. En el arranque, el log indica el proveedor LLM
activo, p. ej.: `Proveedor LLM activo: Groq → https://api.groq.com/openai/v1 (...)`.

### 3.6. Comprobar que funciona

```bash
# Debe devolver [] o la lista de facturas (200 OK)
curl http://localhost:5255/facturas
```

---

## 4. Configuración del proveedor LLM

El sistema puede usar **Groq (nube)** o un **modelo local (LM Studio/Ollama)**. Se elige por perfil,
por orden de prioridad:

```bash
# 1. Por argumento al arrancar
dotnet run -- --llm Local
dotnet run -- --llm Groq

# 2. Por variable de entorno
#    Windows PowerShell:  $env:Llm__Proveedor="Local"; dotnet run
#    Linux/macOS:         Llm__Proveedor=Local dotnet run

# 3. Por appsettings.json:  "Llm": { "Proveedor": "Groq" }
```

- **Groq**: necesita la clave (paso 3.3) e internet. Rápido, ideal para demos.
- **Local**: necesita un servidor LM Studio/Ollama accesible. La URL y el modelo se configuran en
  `appsettings.json` → `Llm:Perfiles:Local` (por defecto apunta a `http://192.168.1.64:1234/v1`,
  modelo `qwen/qwen2.5-vl-7b`). No requiere clave. Ajusta la IP a tu red.

Ver comparativa en [`comparativa-llm-local.md`](comparativa-llm-local.md).

---

## 5. Puesta en marcha del frontend

Clona el repositorio del frontend (separado del backend) y arráncalo:

```bash
git clone https://github.com/eric270992/Frontend_ClasificadorExtractorDocumentos.git
cd Frontend_ClasificadorExtractorDocumentos
npm install
npm start
```

Se abre en **http://localhost:4200**. El servidor de desarrollo usa un **proxy** (`proxy.conf.json`)
que redirige `/api` hacia la API en `http://localhost:5255`, evitando problemas de CORS. Si la API
está en otra dirección, ajusta ese fichero.

> El backend debe estar en marcha (paso 3.5) para que el frontend tenga datos.

---

## 6. Uso del sistema

1. Abre **http://localhost:4200**.
2. **Subir una factura**: arrastra un PDF, JPEG o PNG a la zona de subida. El sistema la ingesta,
   extrae los datos con el LLM, la valida y la guarda. Aparece en la lista con su estado
   (Validada / Revisión humana / Rechazada) e incidencias.
3. **Consultar en lenguaje natural**: en el panel de chat, pregunta cosas como "¿cuánto hemos gastado
   por proveedor?". Responde con datos reales y muestra el SQL ejecutado.

También puedes usar la API directamente:
- `POST /documentos` (multipart, campo `pdf`) — subir factura (PDF/JPEG/PNG).
- `GET /facturas` — listar; `GET /facturas/{id}` — detalle.
- `POST /consultas` (JSON `{ "pregunta": "..." }`) — consulta en lenguaje natural.

---

## 7. Solución de problemas

| Síntoma | Causa probable | Solución |
|---|---|---|
| "Cannot connect / open database" | LocalDB no instalado o instancia no arrancada | Instala SQL Server LocalDB; prueba `sqllocaldb start MSSQLLocalDB` |
| "A network-related error..." al conectar | La cadena de `appsettings.json` no encaja con tu SQL Server | Ajusta `ConnectionStrings:DocFlowDb` a tu servidor |
| `dotnet ef` no se reconoce (solo si migras a mano) | Falta la herramienta | `dotnet tool install --global dotnet-ef` |
| La subida da error 401/403 del LLM | Clave de Groq ausente o inválida | Revisa `Llm:Perfiles:Groq:ApiKey` (paso 3.3) |
| La subida da error 429 del LLM | Límite de peticiones del plan gratuito de Groq | Espera unos segundos o usa `--llm Local` |
| LocalDB no existe | No está instalado | Instala SQL Server Express LocalDB, o usa otra BD y ajusta la cadena |
| El frontend no muestra datos | La API no está arrancada o el proxy apunta mal | Arranca la API (3.5); revisa `proxy.conf.json` |
| Puerto 5255/4200 ocupado | Otro proceso lo usa | Cambia el puerto (`--urls` en la API; `--port` en `ng serve`) |
| Con `--llm Local` no responde | LM Studio no accesible o modelo no cargado | Verifica la IP/puerto y que el modelo está cargado en LM Studio |

---

## 8. Checklist rápido para un PC nuevo

- [ ] Instalar .NET SDK 10, Node 20+, SQL Server LocalDB, Git.
- [ ] Clonar **los dos** repositorios (backend y frontend).
- [ ] `dotnet restore` en el backend.
- [ ] **user-secrets**: solo la clave de Groq (paso 3.3). La cadena de conexión ya viaja en el repo.
- [ ] `dotnet run` en la API (puerto 5255) — la BD se crea sola al arrancar.
- [ ] `npm install` + `npm start` en el frontend (puerto 4200).
- [ ] Abrir http://localhost:4200 y subir una factura de prueba.
