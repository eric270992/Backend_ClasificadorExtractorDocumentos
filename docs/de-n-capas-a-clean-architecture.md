# 🧭 De "N capas (IDAO/DAO/IService/Service)" a este proyecto

> Guía rápida para orientarte si vienes de arquitectura clásica en capas. **No necesitas saber DDD.**
> Traducimos tu vocabulario (IDAO, DAO, IService, Service, DTO, Entity, Controller) a los ficheros
> reales del proyecto. Ten este documento abierto mientras programas.
>
> (La explicación larga y con diagramas está en [`arquitectura.md`](arquitectura.md); esto es el resumen práctico.)

---

## 1. El ÚNICO cambio respecto a lo que ya haces

En tu mundo, la `IDAO` y la `DAO` vivían **en el mismo proyecto** (una capa de datos). Aquí es
idéntico… pero **partido en dos proyectos**:

```
      TU MUNDO (N capas)                  ESTE PROYECTO
   ┌──────────────────────┐        ┌───────────────────────────┐
   │  IFacturaDAO   ◄──────┼────────┤  la INTERFAZ va a          │
   │  (interfaz)           │        │  → Domain/Contracts        │
   ├──────────────────────┤        ├───────────────────────────┤
   │  FacturaDAO           │        │  la IMPLEMENTACIÓN va a    │
   │  (implementación EF)  │        │  → Infrastructure          │
   └──────────────────────┘        └───────────────────────────┘
```

**Ese es todo el "truco".** La `I` (el contrato) se va a `Domain`; la clase que toca la base de datos
se va a `Infrastructure`. Lo demás (Service, DTO, Controller) es exactamente como lo tienes.

**¿Por qué partirlo?** Para que la lógica de negocio (en `Domain`/`Application`) dependa del **contrato**,
no de la base de datos. Así puedes cambiar EF por otra cosa, o testear sin BD, sin tocar el negocio.

---

## 2. Tu vocabulario → ficheros reales

| Lo que tú dices | Aquí se llama | Fichero real |
|---|---|---|
| **Entity** (tabla) | Igual | `Domain/Entities/FacturaStaging.cs` (y `Proveedor`, `FacturaLinea`, `ValidacionIncidencia`) |
| **DTO** | Igual | `FacturaResumen`/`FacturaDetalle` dentro de `Domain/Contracts/IFacturaConsultaService.cs`; `FacturaExtraida` en `Domain/Contracts/` |
| **IDAO** (interfaz de datos) | Contrato en Domain | `Domain/Contracts/IFacturaConsultaService.cs`, `IFacturaStagingRepository.cs` |
| **DAO** (implementación EF) | Adaptador en Infrastructure | `Infrastructure/Persistence/FacturaConsultaService.cs`, `FacturaStagingRepository.cs` |
| **IService / Service** (lógica) | "Agentes" y orquestador | `Application/Extraccion/ExtractorAgent.cs`, `Application/Validacion/ValidadorAgent.cs`, `Application/Ingesta/IngestaOrquestador.cs` |
| **Controller** | Igual | `Api/Controllers/FacturasController.cs` |

> ⚠️ **Cuidado con el sufijo "Service"**: `FacturaConsultaService` lleva "Service" para que te resulte
> familiar, pero **funcionalmente es una DAO de lectura** (toca la BD). No te fijes en el sufijo;
> fíjate en *qué hace* (accede a datos) y *dónde vive* (interfaz en Domain, implementación en
> Infrastructure).

---

## 3. Los cuatro proyectos, en una frase cada uno

| Proyecto | Qué contiene, en tus términos |
|---|---|
| **Domain** | Las **interfaces** (IDAO, IService), las **entidades**, los **DTOs** y las reglas de negocio puras. Son las *definiciones*. No conoce EF, ni HTTP, ni el LLM. |
| **Application** | Los **Services** (lógica: los agentes y el orquestador). Coordina, usando las interfaces de Domain. |
| **Infrastructure** | Las **DAO** (implementaciones con EF/Dapper), el cliente del LLM, el guardado en disco. Todo lo "técnico". |
| **Api** | Los **Controllers**. La puerta REST. No tiene lógica de negocio. |

Regla de oro: **todas las flechas de dependencia apuntan a `Domain`**. `Application` e `Infrastructure`
conocen `Domain`, pero no se conocen entre sí (se conectan por interfaces + inyección de dependencias
en `Api/Program.cs`).

---

## 4. Ejemplo concreto: `GET /facturas` paso a paso

Es el mismo recorrido que ya conoces (Controller → IService → Service → DTO):

```
1. FacturasController              (Api)            ← recibe la petición HTTP
        │ llama a
        ▼
2. IFacturaConsultaService         (Domain)         ← tu "IDAO/IService" (el contrato)
        │ la inyección de dependencias pone la implementación real
        ▼
3. FacturaConsultaService          (Infrastructure) ← tu "DAO" (hace la consulta con EF)
        │ devuelve
        ▼
4. FacturaResumen (lista)          (Domain)         ← tu "DTO"
        │
        ▼
   El controller devuelve el DTO como JSON
```

El controller **nunca** ve EF ni el `DbContext`: solo conoce la interfaz `IFacturaConsultaService`.
Quién decide qué implementación se usa es `Api/Program.cs` (la "raíz de composición"):

```csharp
services.AddScoped<IFacturaConsultaService, FacturaConsultaService>();
//                 ▲ interfaz (Domain)      ▲ implementación (Infrastructure)
```

---

## 5. ¿Dónde pongo una cosa nueva?

| Quiero añadir… | Va a… |
|---|---|
| Una tabla nueva | `Domain/Entities/` |
| Un DTO nuevo | `Domain/Contracts/` |
| Una interfaz de datos (IDAO) | `Domain/Contracts/` |
| La implementación que toca la BD (DAO) | `Infrastructure/` (+ registrarla en `Api/Program.cs` vía `AddInfrastructure`) |
| Un servicio de lógica de negocio | `Application/` |
| Un endpoint (Controller) | `Api/` |

---

## 6. Caso especial que puede despistar: el Consultor (text-to-SQL)

Casi todo sigue el patrón de arriba (consulta fija que tú escribes → DAO tipada → DTO). La **única
excepción** es el Consultor:

- La consulta SQL **no la escribes tú**: la **genera el LLM** en tiempo de ejecución a partir de una
  pregunta en lenguaje natural.
- Por eso su ejecutor (`IConsultaSqlEjecutor` / `DapperConsultaSqlEjecutor`) recibe un `string` SQL y
  devuelve una **tabla genérica** (columnas + filas como diccionarios), no un DTO tipado: no se sabe
  la forma del resultado hasta ejecutar.
- Es el **único** sitio con SQL dinámico, y por eso va blindado por `SqlGuard` (solo SELECT, whitelist
  de tablas, `TOP 1000`) antes de tocar la BD.

Para todo lo demás, usa el patrón normal (Service/DAO con consultas fijas y DTOs tipados).
