# Plan — El criterio «esto es alimento» vuelve a la columna, y el clamp de paginación deja de degradar en silencio

**Fecha:** 2026-08-08
**Continúa:** [`reporte_contable_bultos_sin_tope_paginacion_plan.md`](reporte_contable_bultos_sin_tope_paginacion_plan.md) (commit `92cd918`)
**Pedido del usuario:** implementarlo en todo, encontrar **el factor donde sucede** y mejorarlo para
que no vuelva a pasar.

---

## 1. Los dos defectos, y por qué son el mismo

### Defecto A — el Reporte Contable lee un campo que ya nadie llena

`ReporteContableService.ObtenerDatosBultosAsync` decide qué producto es alimento con
`metadata->>'type_item' == "alimento"` (comparación **sensible a mayúsculas**, en memoria).

Pero `catalogo_items.item_type` es una **columna propia, `NOT NULL`, con tres índices**, y nació
justamente para reemplazar a ese metadata: [`backend/sql/add_item_type_catalogo.sql`](../backend/sql/add_item_type_catalogo.sql)
la creó, copió los valores desde el jsonb y la puso `NOT NULL`. El metadata es el modelo **viejo**.

Medido en la BD local:

| Fuente | Estado |
|---|---|
| `catalogo_items.item_type` (columna) | **0 nulos** de 435 filas; taxonomía completa (alimento, vacuna, medicamento, insumo, desinfectante, empaque, huevo, combustible, materia_prima, mantenimiento) |
| `catalogo_items.metadata->>'type_item'` (jsonb) | **NULL en el 80 %** (solo 34 filas lo tienen) |
| `farm_inventory_movements.item_type` | poblada en el **100 %** de las filas |

**`ReporteContableService` es el ÚNICO lector del metadata en todo el backend.** El resto del sistema
—y el propio frontend, con `item.itemType || item.metadata?.type_item`— ya usa la columna. Todos los
demás sitios que comparan contra `"alimento"` lo hacen con `ToLower()` / `OrdinalIgnoreCase`; el
Reporte Contable es el único case-sensitive, y por eso además pierde los ítems escritos `Alimento`.

**Causa raíz de que se repita:** `CatalogItemService.CreateAsync` escribe **la columna** (default
`"alimento"`) y **no escribe el metadata**. Todo ítem creado desde la UI moderna nace invisible para el
reporte. No es un dato que alguien olvidó cargar: es un campo que ya no se llena por diseño.

**Impacto (movimientos que el reporte reconoce como alimento):**

| Granja | Hoy (metadata) | Con la columna |
|---|---|---|
| 20 | 0 | **236** |
| 5 | 58 | **77** |
| 87 | 0 | **2** |
| 1 / 3 / 4 | 3 / 5 / 3 | 3 / 5 / 3 (sin cambio) |

**257 movimientos aparecen.** Incluso la granja 5, ya arreglada en `92cd918`, gana 19 más («PAVO
INICIADOR» y «POLLA LEVANTE», con columna `alimento` y metadata vacío).

### Defecto B — EL FACTOR: el clamp de paginación degrada al MÍNIMO

El bug de `92cd918` no era exclusivo del Reporte Contable. El patrón está repetido y **sigue vivo**:

| Servicio | Clamp | Estado |
|---|---|---|
| `FarmInventoryMovementService:447` | `PageSize > 200 ⇒ 20` | ya sin consumidor abusivo (`92cd918`) |
| **`CatalogItemService:23`** | `pageSize > 200 ⇒ 20` | 🔴 **BUG ACTIVO** |
| `RoleCompositeService:143` | `pageSize > 200 ⇒ 50` | mismo patrón, sin abuso conocido |

`CatalogItemService.GetAsync` tiene **7 pantallas del front pidiéndole 1.000 o 2.000 ítems y
recibiendo 20**:

- `inventario/services/inventario.service.ts:203` `getCatalogo(pageSize = 1000)` → usado por
  `ajuste-form`, `conteo-fisico`, `kardex-list`, `traslado-form`
- `lote-levante/.../modal-create-edit.component.ts:1533` → `getCatalogo('', 1, 2000)`
- `lote-produccion/.../modal-seguimiento-diario.component.ts:1413` → `getCatalogo('', 1, 2000)`

Y esos componentes hacen `.filter(x => x.activo)` **sobre los 20 recibidos**. Es decir: el selector de
alimento del seguimiento diario de levante y producción muestra 20 ítems de un catálogo de 310
(Santa Reyes) o 61 (Sanmarino/Demo).

**El defecto de diseño es que pedir de más devuelve el MÍNIMO.** Pedir 1.000 y recibir 20 no es un
tope: es una pérdida silenciosa de datos. Lo correcto es que pedir de más devuelva **el máximo
permitido**.

---

## 2. Enfoque

### A. Una sola definición de «esto es alimento»

Nace `Application/Calculos/ItemInventarioTipoCalculos.cs` (puro, static):

```csharp
public const string TipoAlimento = "alimento";
public static bool EsTipoAlimento(string? tipo);                 // trim + case-insensitive
public static string? TipoEfectivo(string? delMovimiento, string? delCatalogo);  // patrón vigente
```

`TipoEfectivo` replica el criterio que el propio módulo de inventario ya usa en
`FarmInventoryMovementService:457` (`m.ItemType ?? m.CatalogItem.ItemType`): **manda el tipo grabado en
el movimiento y el catálogo es el respaldo**. Así un ítem que cambie de tipo no reescribe la historia.

`ObtenerDatosBultosAsync` filtra con eso **dentro de la query**: desaparece el paso de traer todo el
catálogo a memoria (hoy 310 filas por llamada en Santa Reyes) y el filtro cae sobre columnas indexadas
(`ix_catalogo_items_company_type_activo`, `ix_farm_inventory_movements_item_type`).

⚠️ La comparación case-insensitive es **solo de lectura**: no normaliza ni escribe datos, así que no
puede crear duplicados de catálogo (la regla de `[[concepto-inventario-duplicado-capitalizacion]]`
sigue intacta).

### B. El clamp degrada al máximo, no al mínimo

`Application/Calculos/PaginacionCalculos.cs` (puro):

```csharp
public static int NormalizarPageSize(int pedido, int maximo, int porDefecto);
// pedido <= 0        ⇒ porDefecto   (no especificó: comportamiento histórico)
// pedido >  maximo   ⇒ maximo       (pidió de más: se le da el tope, NUNCA el default)
// si no              ⇒ pedido
```

Los tres servicios pasan a usarla. **Nadie vuelve a recibir 20 filas por haber pedido 10.000.**

Y el **catálogo sube su tope a 2.000**: es una tabla maestra acotada (máximo real hoy: 310 ítems en
Santa Reyes, margen 6×), no un log de movimientos que pueda crecer sin límite. Con eso las 7 pantallas
reciben el catálogo completo **sin tocar una línea de frontend**.

| Servicio | Tope | Default | Razón del tope |
|---|---|---|---|
| `CatalogItemService` | **2.000** | 20 | catálogo maestro acotado; el front lo usa como selector |
| `FarmInventoryMovementService` | 200 | 20 | log de movimientos, crece sin techo |
| `RoleCompositeService` | 200 | 50 | listado de administración |

---

## 3. Archivos

| Archivo | Cambio |
|---|---|
| `Application/Calculos/ItemInventarioTipoCalculos.cs` | **NUEVO** — criterio único de tipo de ítem |
| `Application/Calculos/PaginacionCalculos.cs` | **NUEVO** — normalización del tamaño de página |
| `Infrastructure/Services/ReporteContableService.cs` | `ObtenerDatosBultosAsync` filtra por columna, en SQL |
| `Infrastructure/Services/CatalogItemService.cs` | clamp al máximo + tope 2.000 |
| `Infrastructure/Services/FarmInventoryMovementService.cs` | clamp al máximo |
| `Infrastructure/Services/RoleCompositeService.cs` | clamp al máximo |
| `backend/sql/add_item_type_catalogo.sql` | nota: la columna es la fuente de verdad; el metadata queda vestigial |
| `tests/.../ItemInventarioTipoCalculosTests.cs` · `PaginacionCalculosTests.cs` | **NUEVOS** |

**Sin migración y sin tocar datos**: la columna ya está bien poblada. Sanear el metadata por migración
se **descarta** — no previene nada (el próximo ítem nace igual de invisible) y obligaría a mantener dos
copias sincronizadas del mismo dato, justo lo que la regla *«una sola fórmula por número»* prohíbe.

---

## 4. Casos de prueba

### Unitarios
- `EsTipoAlimento`: `"alimento"`, `"Alimento"`, `"  ALIMENTO  "` ⇒ true; `"vacuna"`, `null`, `""` ⇒ false.
- `TipoEfectivo`: manda el del movimiento; cae al del catálogo si viene null/vacío; null si ambos faltan.
- `NormalizarPageSize`: **pedir de más da el tope, no el default** (el test que blinda este bug);
  0 y negativos dan el default; dentro de rango pasa igual; caso real `1000 → 2000` del catálogo.

### Gate antes/después (HTTP real)
| Lote | Granja | Empresa | Fase | Rol |
|---|---|---|---|---|
| 13 | 5 | 1 | Levante / Produccion | gana 19 movimientos |
| 114, 115 | 20 | 1 | Produccion | **de 0 a 236 movimientos** |
| 116, 117 | 20 | 1 | Levante | ídem |
| 124, 127, 128 | 90/91/92 | 4 | según fase | **controles negativos** (granjas con 0 movimientos) |

**Aceptación:** controles negativos con 0 diferencias; en el resto, toda diferencia debe ser de columnas
de bultos o filas nuevas con las columnas de aves en cero; el invariante de aves no se mueve; y el
kardex resultante debe coincidir **fila a fila con el SQL** del criterio nuevo.

### Smoke del factor
- `GET /api/catalogo-alimentos?page=1&pageSize=1000` debe devolver **el catálogo completo**, no 20.
- `GET /api/farms/5/inventory/movements?pageSize=10000` debe devolver **200** (el tope), no 20.

---

## 5. Riesgos

| Riesgo | Mitigación |
|---|---|
| Los números del reporte cambian en muchos lotes | Es la corrección buscada; el gate exige justificar cada diferencia y prohíbe que toquen aves |
| Subir el tope del catálogo a 2.000 pesa | Tabla maestra: 310 filas en el peor caso real, proyección liviana, ya indexada |
| Devolver 200 donde antes iban 20 rompe alguna UI | El front recibe `Total`/`PageSize` en el `PagedResult` y ya pagina con ellos; ninguna pantalla asume 20 |
| El metadata vestigial confunde a futuro | Nota en el `.sql` y en el doc-comment del cálculo puro |
