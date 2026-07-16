# 📄 DocFlow AI — Ingesta intel·ligent de factures

Sistema multiagent que **llegeix factures de compra en PDF de qualsevol format, n'extreu les dades amb
un LLM multimodal, les valida amb regles de negoci i les desa** en una base de dades intermèdia (staging).
A sobre, permet fer **consultes en llenguatge natural** sobre les dades (text-to-SQL segur, només lectura).

Pensat per a pimes que reben factures de proveïdors amb plantilles visualment diferents i que avui les
tecleixen a mà al seu ERP.

> **Estat**: Etapa 1 (demo vertical) completa i funcional. Etapa 2 (consolidació) pendent. Vegeu
> [full de ruta](#-estat-i-full-de-ruta).

---

## ✨ Què fa

1. **Ingesta** un PDF (el converteix a imatges, una per pàgina).
2. **Extreu** les dades estructurades amb un LLM multimodal (sense OCR clàssic ni fine-tuning): emissor,
   receptor, número, dates, línies, IVA, totals… mai inventa (camp absent → null).
3. **Valida** amb 9 regles de negoci (cuadres, coherència d'IVA, reverse charge, NIF, duplicats…) i
   assigna un estat: `Validada`, `Revisió humana` o `Rebutjada`.
4. **Desa** la factura, línies i incidències de forma transaccional.
5. **Respon preguntes en llenguatge natural** sobre les factures, generant SQL segur (només `SELECT`,
   whitelist de taules, `TOP 1000` forçat).

## 🏗️ Arquitectura

Clean Architecture amb 4 capes (.NET). Totes les dependències apunten al **Domain**, que no depèn de res:

```
Api  →  Application  →  Domain  ←  Infrastructure
(REST)   (agents,        (regles,     (EF Core, client LLM,
          orquestrador)   contractes)   PDF→imatge, Dapper)
```

📖 **Explicació detallada (per a qui ve d'arquitectura N-capes)**: [`docs/arquitectura.md`](docs/arquitectura.md)
— inclou diagrames Mermaid i un glossari DDD ↔ N-capes.

## 🧰 Stack tecnològic

| Àmbit | Tecnologia |
|---|---|
| Backend | .NET 10 · Minimal hosting · Controllers REST |
| Persistència | SQL Server (LocalDB en dev) · EF Core (escriptura) · Dapper (consultes del Consultor) |
| LLM | API compatible OpenAI, intercanviable per configuració: **Groq** (Llama 4 Scout) o **local** (LM Studio / Qwen2.5-VL) |
| PDF → imatge | PDFtoImage / Pdfium (stack .NET natiu, sense microservei Python) |
| Frontend | Angular 21 (standalone) · PrimeNG 21 |
| Tests | xUnit (133 tests unitaris) |

## 🚀 Com executar-ho

> 📖 **Guia completa pas a pas (instal·lació, PC nou, resolució de problemes)**:
> **[docs/installation-guide.md](docs/installation-guide.md)**

### Requisits
- .NET 10 SDK · Node.js 20+ · SQL Server LocalDB
- Una clau d'API de Groq **o** un servidor LLM local (LM Studio/Ollama)

### Backend

```bash
cd src/ClassificadorExtractorDocumentos.Api

# 1. Clau del LLM (l'únic secret — mai al repo). La cadena de connexió ja és a appsettings.json.
dotnet user-secrets set "Llm:Perfiles:Groq:ApiKey" "<la-teva-clau>"

# 2. Arrencar (en dev la BD es crea i migra sola). Proveïdor LLM per defecte: Groq
dotnet run
#   escolta a http://localhost:5255

# Alternativa: usar el LLM local en lloc de Groq
dotnet run -- --llm Local
```

### Frontend

El frontend viu en un repositori separat
([Frontend_ClasificadorExtractorDocumentos](https://github.com/eric270992/Frontend_ClasificadorExtractorDocumentos)).
El proxy de dev redirigeix `/api` cap al backend (evita CORS):

```bash
npm install
npm start
#   obre http://localhost:4200
```

## 🔌 Configuració del proveïdor LLM

Perfils amb nom a `appsettings.json` (`Llm:Perfiles`). El perfil actiu es tria, per prioritat:

1. Argument: `dotnet run -- --llm Local`
2. Variable d'entorn: `Llm__Proveedor=Local`
3. `appsettings.json`: `"Llm": { "Proveedor": "Groq" }`

Afegir un proveïdor nou és afegir un bloc a `Perfiles` — zero codi. Vegeu la
[comparativa Groq vs local](docs/comparativa-llm-local.md).

## 🌐 API REST

| Mètode | Ruta | Descripció |
|---|---|---|
| `POST` | `/documentos` | Puja un PDF i executa el pipeline complet (ingesta → extracció → validació → staging) |
| `POST` | `/documentos/{id}/extraccion` | Extracció sense persistir (depuració/avaluació) |
| `GET` | `/facturas` | Llista de factures amb estat |
| `GET` | `/facturas/{id}` | Detall amb línies i incidències |
| `POST` | `/consultas` | Pregunta en llenguatge natural → resposta + SQL executat |

## 🧪 Tests

```bash
dotnet test
```

Tres capes previstes (SPEC §5): **unitaris** (E1, ✅ 133 tests), **integració** amb LLM mockejat (E2) i
**evals** contra ground truth amb LLM real (E2). El dataset de prova és a `docs/datasets/`.

## 📊 Estat i full de ruta

**Etapa 1 — demo vertical (✅ completa):**
- [x] Esquelet, ingesta PDF→imatge, BD completa
- [x] Agent Extractor (nivell 3 genèric) + parsers + avaluació sobre dataset
- [x] Agent Validador (9 regles) + orquestrador transaccional + endpoints
- [x] Agent Consultor (text-to-SQL segur)
- [x] Frontend Angular + PrimeNG (pujada, llista, xat) + guió de demo

**Etapa 2 — consolidació (pendent):**
- [ ] Migració a Microsoft Agent Framework (orquestració)
- [ ] Few-shot per proveïdor (nivell 2) + avaluacions automàtiques
- [ ] Pantalla de revisió humana editable
- [ ] Integrador ERP simulat + tests d'integració

## 📚 Documentació

| Document | Contingut |
|---|---|
| [`docs/installation-guide.md`](docs/installation-guide.md) | **Guia d'instal·lació i ús** (muntar en un PC nou, pas a pas) |
| [`docs/SPEC.md`](docs/SPEC.md) | Especificació completa (font de veritat, SDD) |
| [`docs/de-n-capas-a-clean-architecture.md`](docs/de-n-capas-a-clean-architecture.md) | **Resum ràpid**: el teu vocabulari IDAO/DAO/IService → fitxers reals |
| [`docs/arquitectura.md`](docs/arquitectura.md) | Guia d'arquitectura amb diagrames (per a no experts en DDD) |
| [`docs/guio-demo.md`](docs/guio-demo.md) | Guió de demo de 10 min + pla B |
| [`docs/comparativa-llm-local.md`](docs/comparativa-llm-local.md) | Groq vs LLM local: precisió, velocitat, cost |
| [`docs/resultados-e1-f2.md`](docs/resultados-e1-f2.md) | Resultats d'extracció sobre el dataset |

---

> Projecte desenvolupat amb metodologia **Spec-Driven Development (SDD)**: `docs/SPEC.md` és la font de
> veritat; el codi implementa contra l'especificació.
