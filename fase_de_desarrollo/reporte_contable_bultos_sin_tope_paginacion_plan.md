# Plan — Reporte Contable (postura): el kardex de BULTOS se estrangula en 20 movimientos

**Fecha:** 2026-08-08
**Origen:** QA del feature «ingreso inicial del ciclo» (commit `801b14f`, plan
[`ingreso_alimento_fecha_real_ingreso_inicial_ciclo_plan.md`](ingreso_alimento_fecha_real_ingreso_inicial_ciclo_plan.md)),
sección *Hallazgo BLOQUEANTE aparte* del tracker.
**Alcance:** backend, un solo método. Sin migración, sin cambios de front, sin DDL.

---

## 1. El bug

`ReporteContableService.ObtenerDatosBultosAsync`
([`ReporteContableService.cs:801-808`](../backend/src/ZooSanMarino.Infrastructure/Services/ReporteContableService.cs))
pide el kardex de alimento de la granja así:

```csharp
var query = new MovementQuery { Type = null, Page = 1, PageSize = 10000 };
var movimientos = await _inventoryMovementService.GetPagedAsync(granjaId, query, ct);
```

pero `FarmInventoryMovementService.GetPagedAsync`
([`FarmInventoryMovementService.cs:447`](../backend/src/ZooSanMarino.Infrastructure/Services/FarmInventoryMovementService.cs))
clampa el tamaño de página:

```csharp
var size = (q.PageSize <= 0 || q.PageSize > 200) ? 20 : q.PageSize;
```

`10000 > 200` ⇒ **cae al default 20**, ordenado por `created_at DESC`. El reporte contable de postura
solo ve los **20 movimientos de inventario más recientes de la granja** — de cualquier ítem, porque el
filtro por `type_item = 'alimento'` se aplica **en memoria y DESPUÉS** de paginar. Un movimiento de
vacunas o medicamentos consume cupo del kardex de alimento.

### Evidencia medida (BD local, dump tipo-prod, granja 5 / company 1 / lote padre 13 «K345A»)

| Escenario | Entradas | Retiros | Ventana |
|---|---|---|---|
| **Lo que ve HOY el reporte** (top-20 de la granja → filtro alimento) | **0 movimientos, 0 kg** | 5 movimientos, 6.025,500 kg | solo 2026-04-10 |
| **Universo real** (ítems `type_item='alimento'` de la granja) | **4 movimientos, 112.000,000 kg** = **2.800 bultos** | 54 movimientos, 20.528,900 kg | 2025-10-16 → 2026-04-10 |

Es decir: de los 58 movimientos de alimento de la granja, el reporte ve 5. Las **cuatro entradas
históricas desaparecen enteras** (incluida la del 2025-10-16 de 1.250 bultos), y con ellas el saldo de
bultos del reporte arranca y se mantiene en 0 aunque hubo 112 toneladas ingresadas.

El fix C1 de `801b14f` (fila solo-bultos para fechas con kardex y sin dato del lote) funciona para el
alimento **reciente**, que es lo que entra en el top-20; todo lo histórico sigue estrangulado aguas
arriba.

---

## 2. Enfoque arquitectónico

**Decisión: `ObtenerDatosBultosAsync` deja de pasar por `GetPagedAsync` y consulta
`_ctx.FarmInventoryMovements` directo.**

Por qué, y no «subir el clamp de `GetPagedAsync` a 10.000»:

- `GetPagedAsync` es el endpoint **paginado de la UI de inventario**; el tope de 200 es su contrato y
  su defensa. Subirlo para acomodar a un consumidor que no quiere paginar degrada el endpoint para
  todos (regla del repo: *el backend orquesta, la BD filtra*).
- El reporte no necesita paginación: necesita **el kardex completo de la ventana del reporte**. Pedirlo
  por un DTO paginado obliga además a filtrar por ítem **en memoria** después de traer filas de
  cualquier tipo.
- Consultando directo, los tres filtros que hoy viven repartidos (granja+empresa+país en el service de
  inventario; ítem de alimento en memoria; ventana de fechas en el cálculo puro, aguas abajo) se
  resuelven **en una sola query traducida a SQL**.

### Paridad de filtros (lo que hay que replicar de `GetPagedAsync`, sin inventar nada)

| Filtro | Hoy | Después |
|---|---|---|
| Granja | `m.FarmId == farmId` | idéntico |
| Empresa | `m.CompanyId == _current.CompanyId` (si > 0) | `m.CompanyId == companyId` — el método **ya exige** `companyId > 0` y retorna vacío si no (línea 781-782), así que el filtro pasa de condicional a **incondicional**: fail-closed, igual que hoy en la práctica |
| País | `m.PaisId == _current.PaisId` (si > 0) | ídem, condicional sobre `_currentUser.PaisId` |
| Ítem = alimento | en memoria, sobre la página ya truncada | `productosAlimento.Contains(m.CatalogItemId)` **en la query** |
| Ventana de fechas | no existía en la query (la aplicaba `GeneraFilaSoloBultos` aguas abajo) | `m.CreatedAt >= Desde && m.CreatedAt < Hasta+1d` en la query |
| Orden / tope | `OrderByDescending(CreatedAt).Take(20)` | **sin tope** (el filtro es la ventana) |

**La ventana no cambia comportamiento, solo acota la consulta.** Es la misma
`ReporteContableBultosCalculos.Ventana(...)` que el reporte ya calcula y ya aplica aguas abajo:
- fuera de la ventana y **sin** dato del lote ⇒ hoy la fila la rechaza `GeneraFilaSoloBultos`;
- fuera de la ventana y **con** dato del lote ⇒ imposible, porque `Desde` = `max(encaset − N, fechaInicio)`
  y `Hasta` = `fechaFin`, y las filas del lote viven exactamente en ese rango.

El corte superior se hace **exclusivo al día siguiente** (`< Hasta.AddDays(1)`) y sin `.Date` sobre la
columna, porque `created_at` es `timestamptz` y `date_trunc` usaría la zona de la **sesión**
(gotcha ya documentado en el módulo de huevos: `[[huevos-levante-semana14-arrastre]]`).

### Lo que NO cambia (deliberado)

- **`created_at` sigue siendo la fecha del kardex**, no `fecha_movimiento`. Es la fecha operativa real:
  `ActualizarFechaIngresoAsync` pisa `created_at` cuando el usuario corrige la fecha de un ingreso.
  Cambiar de columna sería otro fix, con su propio gate.
- **El criterio de «producto de alimento» sigue siendo `catalogo_items.metadata->>'type_item'`**
  resuelto en memoria sobre el catálogo de la empresa (no `farm_inventory_movements.item_type`).
  Mismo conjunto de ítems que hoy.
- **Los 4 buckets** (`Entry`/`TransferIn` → entradas, `TransferOut` → traslados, `Exit` → retiros) y la
  conversión kg→bultos quedan idénticos. `ConsumoSeguimiento`/`DevolucionSeguimiento` siguen fuera.
- La agrupación por `CreatedAt.Date` y la forma de la tupla devuelta no se tocan.

### Limpieza al pasar (regla de clean code del repo)

- El parámetro `loteIds` de `ObtenerDatosBultosAsync` **no se usa** en el cuerpo → se elimina.
- `IFarmInventoryMovementService` queda **sin ningún uso** en `ReporteContableService` → se eliminan el
  campo y el parámetro del constructor. El service se resuelve por DI (`Program.cs:341`), no hay `new`
  manual en ningún lado ⇒ sin impacto.

---

## 3. Archivos

| Archivo | Cambio |
|---|---|
| `backend/src/ZooSanMarino.Infrastructure/Services/ReporteContableService.cs` | `ObtenerDatosBultosAsync`: query directa + ventana; firma sin `loteIds`; ctor sin `IFarmInventoryMovementService` |
| `backend/src/ZooSanMarino.Application/Calculos/ReporteContableBultosCalculos.cs` | + `RangoConsulta(ventana)` (puro): traduce la ventana a `[desde, hastaExclusivo)` para la query |
| `backend/tests/ZooSanMarino.Application.Tests/ReporteContableBultosCalculosTests.cs` | tests del rango de consulta |

**Sin BD/SQL**: no hay migración, no hay DDL, no hay cambio de schema.

---

## 4. Reglas de negocio que el fix debe preservar

1. El kardex de bultos es de **granja**, se imputa al **lote padre**.
2. Un movimiento **fuera de la ventana** del reporte no genera fila propia (regla vigente de
   `GeneraFilaSoloBultos`, intacta).
3. El saldo de bultos se acumula **cronológicamente** con piso 0 al publicar
   (`AcumularSaldos`, intacto).
4. **Fail-closed multiempresa**: sin `companyId` resoluble ⇒ kardex vacío, nunca el de otra empresa.

---

## 5. Casos de prueba

### Unitarios (xUnit, `Application.Tests`)
- `RangoConsulta` devuelve `[Desde 00:00, Hasta+1d 00:00)` — el último día del reporte entra completo.
- Ventana de un solo día ⇒ rango de 24 h.
- Los 16 tests existentes de `ReporteContableBultosCalculos` deben seguir verdes sin tocarlos.

### Gate obligatorio antes/después (HTTP real contra BD local)
Capturar el JSON completo de `GET /api/ReporteContable/generar` para **6 combinaciones lote×fase** con
el código actual, aplicar el fix, recapturar y **comparar campo a campo**:

| Lote | Granja | Fase | Rol en el gate |
|---|---|---|---|
| 13 «K345A» | 5 | Levante | **caso del bug** — debe ganar las 4 entradas históricas |
| 13 «K345A» | 5 | Produccion | ídem, otra fase |
| 114 «A374A» | 20 | Produccion | **control negativo** — granja sin ítems `type_item='alimento'` |
| 115 «A374B» | 20 | Produccion | control negativo |
| 116 «A374A» | 20 | Levante | control negativo |
| 117 «A374B» | 20 | Levante | control negativo |

**Criterio de aceptación:**
- Controles negativos: **0 diferencias**, byte a byte.
- Lote 13: las únicas diferencias admisibles son **columnas de bultos** (`entradasBultos`,
  `trasladosBultos`, `retirosBultos`, `saldoBultos*` y sus totales/consolidados) y **filas solo-bultos
  nuevas**, todas trazables a movimientos de alimento reales que antes quedaban fuera del tope de 20.
  Cualquier diferencia en columnas de **aves** (entradas, mortalidad, selección, ventas, traslados,
  saldos) o de **huevos** es una regresión y bloquea el merge.

### Validación de plataforma
- `cd backend && dotnet build` — 0 errores, sin advertencias nuevas.
- `cd backend && dotnet test` — suite completa verde.
- Sin procesos huérfanos: el backend del smoke se detiene al terminar.

---

## 6. Riesgos y mitigación

| Riesgo | Mitigación |
|---|---|
| El reporte cambia números en muchos lotes | Es el objetivo del fix; el gate exige justificar **cada** diferencia y prohíbe que toquen aves/huevos |
| Traer el kardex completo hace lenta la consulta | La ventana acota en BD; el universo real por granja es de decenas a cientos de filas (máx. medido: 236 en toda la granja 20), y la query es un índice por `farm_id` |
| `Desde` recortado por `fechaInicio` deja el saldo sin apertura | Comportamiento **ya vigente**: `AcumularSaldos` siempre arranca en 0, no hay saldo inicial. Sin cambio |
| Otra sesión toca el mismo archivo | Bloque propio al final de `tracker_estado.md`; el cambio es un método y un ctor |

---

## 7. Hallazgo aparte detectado al medir (NO se toca en este plan)

Los ítems de alimento de la **granja 20** (85 «POLLA LEVANTE REPRODUCTORA PESADA», 89, 98, 99, 100)
tienen `metadata->>'type_item'` **NULL** ⇒ el Reporte Contable no los reconoce como alimento y no los
cuenta **ni antes ni después de este fix** (236 movimientos invisibles). Es un problema de **datos de
catálogo**, no de este código, y por eso esos lotes sirven como control negativo del gate. Requiere su
propia auditoría: decidir si se saneia el metadata del catálogo o si el criterio pasa a
`farm_inventory_movements.item_type`.
