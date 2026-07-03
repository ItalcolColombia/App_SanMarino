# Inventario Colombia — Mapa de módulos, rutas, consumo y unificación

> Análisis solo-lectura (agente dev en paralelo). Complemento de `inventario_ecuador_mapa.md`.

## 0. Resumen ejecutivo (el dolor central)
Hay **dos modelos de datos de inventario paralelos y desconectados**, más un tercero (gastos) sobre el segundo:

| Modelo | País (datos) | Tablas | Endpoints back | Feature front | Ruta |
|---|---|---|---|---|---|
| **A — Colombia** (farm/catálogo legacy) | Colombia (pais 1) | `catalogo_items`, `farm_product_inventory`, `farm_inventory_movements` | `/api/farms/{id}/inventory/*`, `/api/catalogo-alimentos` | `features/inventario/` | `/inventario`, `/inventario-management` |
| **B — Ecuador/Panamá gestión** | Ecuador (2) + Panamá (3) | `item_inventario_ecuador`, `inventario_gestion_stock`, `inventario_gestion_movimiento` | `/api/inventario-gestion/*`, `/api/item-inventario-ecuador` | `features/gestion-inventario/` | `/gestion-inventario` |
| **C — Ecuador gastos** (sobre B) | Ecuador (2) | `inventario_gasto(_detalle/_auditoria)` (descuenta stock de B) | `/api/inventario-gastos/*` | `features/gastos-inventario/` | `/inventario-gastos` |

**La decisión "a qué país apunta el inventario" NO está en el código de rutas** (las 4 rutas están registradas para todos con solo `authGuard`). Se decide **100% por filas de menú en BD** (`company_menus` → `menus.route`) por empresa. Por eso el usuario tiene que "entrar a ver a qué apunta".

## 1. `features/inventario/` (modelo A = Colombia)
Servicio único `inventario.service.ts` → `${apiUrl}/farms/{farmId}/inventory/*` y `/catalogo-alimentos`. Modelo `catalogItemId`/`codigo`, ubicación = `location` string libre (SIN núcleo/galpón).
Componentes: `inventario-tabs` (contenedor), `inventario-list` (stock), `movimientos-unificado-form` (entrada/salida/traslado), `movimiento-alimento-form`, `traslado-form`, `ajuste-form`, `kardex-list`, `conteo-fisico`, `catalogo-alimentos-tab`, `page/inventario-managemen/` (duplica tabs, probable muerto), `pipe/inventario-filter`.
Backend: `FarmInventoryController` (`api/farms/{id}/inventory` → `farm_product_inventory`), `FarmInventoryMovementsController` (→ `farm_inventory_movements`), `CatalogoAlimentosController` (→ `catalogo_items`). Servicios: `FarmInventoryService`, `FarmInventoryMovementService`, `FarmInventoryReportService` (kardex/conteo), `CatalogoAlimentosService`.

## 2. Matriz de rutas — qué apunta a Ecuador vs Colombia (gating = menú BD)
| Ruta (menú BD) | Empresas | País | Servicio | Endpoint | Tablas |
|---|---|---|---|---|---|
| `/inventario` (menús 10 y 32) | Agroavicola Sanmarino(1), Demo(4) | **Colombia** | `InventarioService` | `/farms/{id}/inventory/*` | modelo A |
| `/gestion-inventario` (menú 50) | ItalcolEcuador(3), ItalcolPanama(5) | **Ecuador/Panamá** | `GestionInventarioService` | `/inventario-gestion/*` | modelo B |
| `/gestion-inventario/historial` (53) | ItalcolPanama(5) | Panamá | idem | `/inventario-gestion/movimientos` | B |
| `/inventario-gastos` (52) | ItalcolEcuador(3) | **Ecuador** | `InventarioGastosService` | `/inventario-gastos/*` | C |
| `/config/item-inventario-ecuador` (49) | Ecuador(3), Panamá(5) | Ec/Pa | catálogo | `/item-inventario-ecuador` | B |
| `/inventario-management` | **NINGUNA** (huérfano, solo URL) | — | mismo `InventarioTabsComponent` | idem A | A |

**Duplicados a limpiar:** `/inventario-management` (huérfano puro → eliminar); `/inventario` con DOS menús (10 y 32) para empresa 1 (menú duplicado en BD); `page/inventario-managemen/` (código muerto probable); y de fondo, A y B son **dos implementaciones completas del mismo dominio**.

**Datos por país (BD local):** `catalogo_items`=61(co1)+61(co4)+**3 Ecuador** (contaminación); `farm_product_inventory`=18 (todo Colombia); `farm_inventory_movements`=188 (todo Colombia); `item_inventario_ecuador`=136 (Ecuador); `inventario_gestion_movimiento`=5817 Ecuador + **1 fila Colombia** (bug §3).

## 3. Consumo desde seguimientos diarios de Colombia
DTO `ItemSeguimientoDto` lleva **AMBOS** ids: `catalogItemId` (Colombia) y `itemInventarioEcuadorId` (Ec/Pa). Todo va a JSONB (`metadata`/`items_adicionales`); no hay tabla de líneas.
- **LEVANTE** (`SeguimientoLoteLevanteService.CreateAsync` 402-415): único descuento vía `RegistrarConsumoAsync` → tablas **Ecuador** (modelo B). El `try/catch` solo hace `Console.WriteLine` (silencioso).
- **PRODUCCIÓN** (`ProduccionService`): **NO llama inventario**. Solo JSONB. **No descuenta.**

### Conclusiones
- **Colombia NO descuenta de su propio inventario (A) desde seguimientos.** Model A se alimenta solo por UI manual. Stock A y consumo de seguimientos están **desconectados**.
- **🐞 BUG cross-país (raíz de la fila Colombia espuria en B):** `ParseMetadataItemsToKg` (694+) hace `if (id<=0) id = catalogItemId`. Para un lote Colombia (ítems con solo `catalogItemId`), levante intenta `RegistrarConsumoAsync` en el modelo **Ecuador** usando un id de catálogo Colombia como si fuera `item_inventario_ecuador_id`. Normalmente lanza y se traga el error; pero si un `catalogItemId` colisiona con un `item_inventario_ecuador.id` real con stock → **descuenta el stock del país equivocado en silencio**. **Fix de bajo riesgo:** no invocar `RegistrarConsumoAsync` cuando el ítem solo trae `catalogItemId`, o gatear por `pais_id` del lote.

## 4. Cómputo pesado en el back (candidatos a SQL)
- **Kardex Colombia** `FarmInventoryReportService` (56-82): trae todos los movimientos a memoria y acumula `saldo` con `foreach` → **window function** `SUM(delta) OVER (PARTITION BY catalog_item_id ORDER BY created_at)`.
- Conteo físico: `foreach` diferencia contra saldo → set-based.
- `InventarioGestionService` (B): `.Include`+proyección en memoria (ver `inventario_ecuador_mapa.md` §3).

## 5. Observaciones para la unificación
**Comparten A y B:** conceptos idénticos (stock por ubicación, movimientos Ingreso/Consumo/Traslado, `transfer_group_id` uuid en ambas, catálogo, `company_id`+`pais_id`). Ambos ids ya conviven en `ItemSeguimientoDto`.
**Difieren (riesgos):**
1. **Ubicación:** A = `farm_id` + `location` string; B = `farm_id` + `nucleo_id` + `galpon_id` estructurado (más rico). Unificar hacia B ⇒ migrar/rellenar núcleo/galpón para 188 mov + 18 stock Colombia.
2. **Catálogos separados:** `catalogo_items` (A) vs `item_inventario_ecuador` (B); ids **NO intercambiables** → requiere mapeo o catálogo único. El fallback del bug §3 demuestra el peligro.
3. **Descuento asimétrico:** Colombia (A) hoy NO descuenta desde seguimientos; Ecuador (B) SÍ. Unificar ⇒ decidir regla de negocio para Colombia (¿empezar a descontar? ¿solo registrar?) — cambia comportamiento y stock histórico → **requiere confirmación del usuario**.
4. **Gastos (C)** solo acoplado a B → si el destino es B, C se beneficia; si fuera A, hay que reescribir C.
5. **Datos espurios a limpiar:** 3 filas Ecuador en `catalogo_items`; 1 fila Colombia en `inventario_gestion_movimiento`.

**Recomendación (agente):** destino = **modelo B** (más rico, más volumen, ya tiene tránsito + descuento desde seguimientos + gastos). Consolidar a UNA ruta, elegir componente por país (menú/`is-ecuador`). Eliminar `/inventario-management` + menú duplicado. Fix inmediato del bug cross-país.

### Archivos clave
- Rutas: `frontend/src/app/app.config.ts` (264-295)
- Servicios: `features/inventario/services/inventario.service.ts` (A), `features/gestion-inventario/services/gestion-inventario.service.ts` (B), `features/gastos-inventario/services/inventario-gastos.service.ts` (C)
- Menú: `frontend/src/app/shared/services/menu.service.ts`
- Consumo levante (bug 402-415 y 694-737): `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoLoteLevanteService.cs`
- Producción (sin inventario): `backend/src/ZooSanMarino.Infrastructure/Services/ProduccionService.cs`
- Descuento Ecuador: `backend/src/ZooSanMarino.Infrastructure/Services/InventarioGestionService.cs` (`RegistrarConsumoAsync` 1102-1150)
- Kardex pesado: `backend/src/ZooSanMarino.Infrastructure/Services/FarmInventoryReportService.cs` (56-82)
