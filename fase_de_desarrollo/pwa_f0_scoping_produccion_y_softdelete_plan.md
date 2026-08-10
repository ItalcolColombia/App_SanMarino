# Plan — F0: scoping de empresa en seguimiento de producción + cierre de A5 (soft delete)

**Fecha:** 2026-08-10
**Contexto:** continuación de la PWA. F1 (`8ecb7c6`) y F2 (`3e9f…`, consulta offline) están entregadas;
**F3 (captura offline) sigue bloqueada** por F0.A y F0.B. La auditoría del 09-ago dejó F0.A en 8 de 10,
con **A4** y **A5 (2ª parte)** abiertos.

Este plan **no ejecuta el plan viejo a ciegas**: aplica la regla que costó la auditoría anterior —
*antes de ejecutar un ítem de un plan de más de una semana, verificarlo contra la BD y el código de
hoy*. Al hacerlo apareció algo que el plan no menciona y que pesa más que los dos ítems restantes.

---

## 0. Lo que la verificación encontró (medido, no supuesto)

### 🔴 S1 — `SeguimientoProduccionService` no filtra por empresa en NINGÚN método

`backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoProduccionService.cs`, registrado en
`Program.cs:260` y expuesto por `Controllers/SeguimientoProduccionController.cs`:

| Método | Predicado real | Consecuencia |
|---|---|---|
| `GetAllAsync():21` | `_ctx.SeguimientoProduccion.Select(...)` — sin filtro | `GET /api/SeguimientoProduccion` devuelve los seguimientos de **todas las empresas** |
| `GetByLoteIdAsync():45` | `Where(x => x.LoteId == loteId)` | lee el seguimiento de cualquier lote de cualquier empresa |
| `CreateAsync():76` | valida el lote con `LoteId + Fase + DeletedAt`, **sin `CompanyId`** | acepta un `LoteId` ajeno; y en `:119` **borra** la fila "vacía" de esa otra empresa |
| `UpdateAsync():157` | `FindAsync(dto.Id)` por PK | edita la fila de cualquier empresa |
| `DeleteAsync():208` | `FindAsync(id)` por PK | **borra** la fila de cualquier empresa |
| `FilterAsync():250` | `AsQueryable()` sin filtro | idem lectura |

No es un endpoint anónimo: `Program.cs:462-465` fija `FallbackPolicy = RequireAuthenticatedUser`, así
que **exige token válido**. Lo que falta es la **autorización por empresa**: cualquier usuario
autenticado de la empresa A opera sobre las filas de la B pasando el id.

Esto viola de frente la regla 3 de *Features por EMPRESA* de `CLAUDE.md` (*"empresa efectiva SIEMPRE
por datos, fail-closed… nunca fugar datos de otra empresa"*), y es el mismo defecto que ya se corrigió
en `InventarioCatalogoScopeCalculos` y en los movimientos TSD.

**Por qué pertenece a este plan y no a otro:** es exactamente el ítem **B4** de la Fase 0
(*"llevar a server-side los gates de escritura hoy front-only"*). La PWA no lo crea, pero lo
**multiplica por N dispositivos**: un outbox que reproduce operaciones contra un servidor que no
verifica la empresa es la forma más barata de escribir en la empresa equivocada con 200 OK.

**Evidencia de que el camino sin identidad ya corrió:** hay **1 fila con `company_id = 0`** en
`seguimiento_diario_produccion` (`CreateAsync:137` hace `_current?.CompanyId ?? 0`). Mismo patrón que
la deuda ya documentada de los movimientos TSD.

**Radio de rotura acotado (medido):** el front solo consume `/SeguimientoProduccion/filter-data`
(`lote-produccion-list.component.ts:69`), que va por `ILoteProduccionFilterDataService` y **ya tiene
scoping**. Los seis métodos sin scoping **no los llama ninguna pantalla**. Restringirlos no rompe UI.

**El patrón correcto ya existe en el repo**, sobre la misma tabla:
`ProduccionDiariaService.cs:253-257` resuelve la empresa **por join a `Lotes`**, no por la columna
`company_id` de la fila. Es lo correcto: la fila puede traer `0`, el lote es el dato maestro.

### A5 (2ª parte) — el soft delete NO se hace; la columna a medio poner SÍ se cierra

Medido contra la BD y el código vivos:

- De las 4 tablas operativas, **solo `seguimiento_diario_produccion` tiene `deleted_at`**. Levante,
  aves de engorde e inventario **ni siquiera tienen la columna**.
- La propiedad existe (`SeguimientoProduccion : AuditableEntity`) y **está mapeada**
  (`SeguimientoProduccionConfiguration.cs:256`).
- **`HasQueryFilter` no aparece ni una sola vez en todo el backend.** No hay filtro global de
  soft delete en ninguna parte del proyecto.
- **Nadie escribe esa columna**: los 17 borrados de esas tablas son `DELETE` físico.
- 🔴 **`fn_seguimiento_diario_produccion` —la fn diaria canónica— NO filtra `sp.deleted_at`.** Sí
  filtra el de `lotes`, `lote_postura_produccion`, `lote_postura_levante` y `movimiento_aves`; el de
  la tabla de la que saca los seguimientos, no.

**Decisión: no se implementa soft delete.** Igual que A6, la medición manda:

1. El requisito que el plan invoca para A5 —*"un cursor `updated_at` no puede transportar una fila que
   ya no existe"*— **ya está cubierto** por las lápidas de `sync_tombstones` (`60d3125`).
2. Sin un solo `HasQueryFilter` en el proyecto, "agregar soft delete" no es agregar una columna: es
   tocar cada consulta de esas entidades a mano, o introducir filtros globales que cambian el
   comportamiento de **todas**. Es cambio de comportamiento masivo a cambio de nada que hoy falte.
3. Y mientras la fn canónica no filtre, un soft delete haría que **las filas borradas sigan contando
   en el saldo de aves y huevos**. La columna está a medio poner: es una **bomba armada**, no una
   funcionalidad incompleta.

**Lo que sí se hace: desarmar la bomba.** `fn_seguimiento_diario_produccion` pasa a filtrar
`sp.deleted_at IS NULL`. Hoy hay **0 filas borradas** en esa tabla ⇒ el cambio es un **no-op
verificable** sobre los datos existentes (gate de paridad en 0), y deja de haber una forma silenciosa
de inflar el saldo.

### A4 — medido, y el número no se toca en esta sesión

`aves_h_actual`/`aves_m_actual` del LPP tienen **15 escritores incrementales** en 6 archivos, más
2 absolutos; el `SaveChangesAsync` de `ProduccionService.Consultas.cs:174-184` recalcula desde la fn y
**cura** la columna en cada lectura de la ficha.

Medición sobre la BD local (refresh del dump de prod): **4 LPP vivos, 0 difieren** de la fn.
La deriva que el plan supone **no se observa** — aunque el propio self-heal la enmascara, así que el 0
prueba que el refactor sería un no-op verificable, no que los incrementales sean correctos.

A4 queda **fuera de esta sesión** por alcance: es aritmética de saldos de aves con 15 puntos de
escritura, y su valor (que la columna no dependa de que alguien abra la ficha) es menor que el de S1.
Queda documentado con el mapa completo para ejecutarlo con su propio gate.

---

## 1. Alcance de esta sesión

| # | Cambio | Riesgo |
|---|---|---|
| **S1** | Scoping de empresa en los 6 métodos de `SeguimientoProduccionService` | Bajo — restringe, fail-closed; ninguna pantalla los usa |
| **S2** | `fn_seguimiento_diario_produccion` filtra `sp.deleted_at IS NULL` | Bajo — 0 filas borradas hoy ⇒ no-op verificable con gate |
| **S3** | Tests xUnit del cálculo puro del scoping | — |

**Fuera de alcance:** A4 (documentado arriba), soft delete (descartado con medición), F0.B restante
(B1, B5, B6, B8, B10).

---

## 2. Diseño

### S1 — scoping por join a `Lotes`, fail-closed

Se copia el patrón vivo de `ProduccionDiariaService.cs:253-257`: la empresa **la dicta el lote**, no
la columna `company_id` de la fila (que puede valer `0`).

```csharp
// Un único punto de verdad para "los seguimientos que esta empresa puede ver".
private IQueryable<SeguimientoProduccion> BaseQuery() =>
    from s in _ctx.SeguimientoProduccion
    join l in _ctx.Lotes on s.LoteId equals l.LoteId
    where l.CompanyId == CompanyIdActual && l.DeletedAt == null
    select s;
```

- `CompanyIdActual` resuelve `_current?.CompanyId`. **Fail-closed**: si no hay identidad, la consulta
  devuelve vacío y las escrituras se rechazan — nunca degrada a "todas las empresas".
- `DeleteAsync`/`UpdateAsync` dejan de usar `FindAsync(id)` y pasan por `BaseQuery()`: una fila de otra
  empresa se comporta como **inexistente** (404/`false`), no como prohibida. No filtra la existencia.
- `CreateAsync` valida el lote **con `CompanyId`**, y el borrado de la fila "vacía" de `:119` queda
  dentro del mismo alcance.
- `EnsureLoteProduccionAbiertoAsync` recibe el mismo filtro.

La regla de "qué empresa manda y qué pasa si no hay identidad" se extrae a **cálculo puro** en
`Application/Calculos/` con tests, según CLAUDE.md.

### S2 — el filtro en la fn

`CREATE OR REPLACE FUNCTION` con la **misma firma de 2 argumentos** (agregar un parámetro con
`DEFAULT` crearía una **sobrecarga**, no un reemplazo, y las llamadas existentes quedarían ambiguas —
lección de `20260810035730`). Cambio de una línea en el CTE `crudos`:

```sql
FROM seguimiento_diario_produccion sp
WHERE sp.deleted_at IS NULL          -- ← nuevo
  AND ( … el predicado actual, intacto … )
```

Va por **migración EF idempotente** + actualización del espejo `backend/sql/…` en el mismo commit
(un `.sql` cambiado sin migración queda muerto; y el espejo desincronizado ya mordió antes).
Verificado antes de tocar: el cuerpo desplegado y el del repo son **idénticos** (13.881 caracteres).

---

## 3. Casos de prueba

**S1 — cálculo puro (xUnit):**
1. Sin identidad (`null`) ⇒ **no autorizado** (fail-closed), nunca "todas".
2. `CompanyId = 0` ⇒ tratado como ausencia, igual que en `claveParticion` del front.
3. Empresa presente ⇒ esa y solo esa.
4. Fila de otra empresa ⇒ se comporta como inexistente.

**S2 — gate de paridad multipaís (obligatorio, CLAUDE.md):**
- `verificar_paridad_seguimiento_produccion.sql` **antes** (congela) y **después** (compara).
- Toda empresa con producción debe salir con **0 en todas las columnas**.
- Línea base ya congelada: **604 filas** (Sanmarino 602 · Demo 2), control en 0.

**Transversal:**
- `dotnet build` 0 errores · `dotnet test` sin regresión (línea base **2.181** verdes).
- Cuadre de alimento de engorde **61 filas / 1 descuadrado**, sin moverse.
- Conteo por empresa antes/después de `GET /api/SeguimientoProduccion`.

---

## 4. Criterio de cierre

- Ningún método de `SeguimientoProduccionService` alcanza una fila cuyo lote no sea de la empresa activa.
- La fn canónica no cuenta filas borradas, y la paridad da 0 en todas las empresas.
- F0.A queda en **9 de 10** (A4 medido y documentado, pendiente con su propio gate).
