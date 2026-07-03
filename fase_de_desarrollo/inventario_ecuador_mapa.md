# Mapa del Inventario Ecuador/Panamá (flujo TRÁNSITO) — para unificación con Colombia

> Análisis solo-lectura (agente dev en paralelo). Base para el plan de unificación.

## 0. Resumen ejecutivo
- **Tres módulos frontend** (`gestion-inventario`, `gastos-inventario`, `config/item-inventario-ecuador`) comparten el mismo catálogo (`item_inventario_ecuador`) y el mismo par de tablas (`inventario_gestion_stock` + `inventario_gestion_movimiento`).
- **Todo apunta a la MISMA BD.** Ecuador y Panamá viven en la misma instancia; el aislamiento es por `company_id` + `pais_id` (+ granjas asignadas), NO por base distinta.
- El módulo viejo **`/inventario`** (`features/inventario/`) es un sistema **separado** (usa `api/farms/...` y `api/catalogo-alimentos`), **no toca** las tablas de gestión Ecuador → **ignorar** para la unificación.
- **TRÁNSITO** es exclusivo de `gestion-inventario` (pestaña "Tránsito"): traslado **inter-granja** en 2 fases (salida→recepción), todo sobre `inventario_gestion_movimiento` filtrando `movement_type` + `transfer_group_id`. **No hay tabla de tránsito dedicada.**

## 1. Módulos / rutas / servicios / endpoints (front → back → tabla)
| Módulo front | Ruta | Servicio HTTP | Endpoints back | Controller/Service | Tablas |
|---|---|---|---|---|---|
| **gestion-inventario** (stock, ingresos, traslados, **tránsito**, histórico) | `/gestion-inventario`, `/gestion-inventario/historial` | `GestionInventarioService` | `api/inventario-gestion/*` (filter-data, historico-filtros, stock GET/PUT/DELETE, ingreso, traslado, consumo, movimientos GET/DELETE, **transito/pendientes**, **transito/recepcion**, **transito/rechazo**, traslados, ingresos) + `api/item-inventario-ecuador` | `InventarioGestionController` → `InventarioGestionService` (2147 líneas, monolítica) | `inventario_gestion_stock`, `inventario_gestion_movimiento`, `item_inventario_ecuador` |
| **gastos-inventario** (Ecuador: consumos por concepto no-alimento) | `/inventario-gastos` | `InventarioGastosService` | `api/inventario-gastos/*` | `InventarioGastosController` → `InventarioGastoService` (623) — **delega descuento a `InventarioGestionService.RegistrarConsumoAsync`** | `inventario_gasto(_detalle/_auditoria)` + muta stock/movimiento vía gestión |
| **item-inventario-ecuador** (catálogo, Config) | `/config/item-inventario-ecuador` | `ItemInventarioEcuadorService` | `api/item-inventario-ecuador/*` (+ carga-masiva-excel) | `ItemInventarioEcuadorController` | `item_inventario_ecuador` |
| ~~inventario (viejo)~~ | `/inventario`, `/inventario-management` | `InventarioService` | `api/farms/...`, `api/catalogo-alimentos` | (otro sistema) | **NO** toca tablas Ecuador → ignorar |

**Tabs UI `gestion-inventario`:** stock · ingresos · traslados · **transito** · historico · items. La ruta `/gestion-inventario/historial` es 2ª página (editar-fecha/eliminar).

## 2. Flujo TRÁNSITO (lo más importante)
Traslado entre **granjas distintas** (`fromFarmId != toFarmId`). El inter-granja pasa por estado intermedio "en tránsito" que la granja **destino** confirma.

Estados en `inventario_gestion_movimiento.movement_type` / `estado` (BD local, 5818 movs):
- `TrasladoInterGranjaSalida` → "Tránsito" (79) — aparece en pestaña Tránsito
- `TrasladoInterGranjaEntrada` → "Recibido desde tránsito" (80)
- legacy `TrasladoInterGranjaPendiente` → "Pendiente destino" (0 local, soportado)
- legacy `TrasladoInterGranjaRechazado` → "Rechazado destino" (0 local, soportado)

Transiciones:
```
POST inventario-gestion/traslado (fromFarmId != toFarmId)
  → RegistrarTrasladoInterGranjaTransitoAsync: valida+DESCUENTA origen, crea 1 mov
    movement_type=TrasladoInterGranjaSalida, estado="Tránsito",
    farm_id=ORIGEN, from_farm_id=DESTINO (¡INVERTIDO!), transfer_group_id=new Guid
      → GET transito/pendientes lo lista
      → POST transito/recepcion → RegistrarRecepcionTransitoAsync:
          valida granja==from_farm_id(destino), SUMA stock destino, crea mov
          TrasladoInterGranjaEntrada estado="Recibido desde tránsito" (mismo grupo)
          → el grupo desaparece de pendientes
      → POST transito/rechazo (solo legacy Pendiente) → marca Rechazado, no toca stock
```
`GetTransitosPendientesAsync`: trae Pendiente OR Salida; excluye grupos que ya tienen Entrada (HashSet en memoria); filtra por `from_farm_id`=destino. `pendienteDespachoOrigen` distingue legacy (aún no descontó) del nuevo (ya descontó).
Recepción manda `{transferGroupId, toFarmId, toNucleoId, toGalponId}` (núcleo/galpón obligatorios solo si el ítem es alimento).
⚠️ **Trampa #1:** en la salida inter-granja `farm_id`=origen y `from_farm_id`=DESTINO (invertido vs el nombre). Frágil para la unificación.

## 3. Backend — cómputo pesado en memoria (candidatos a SQL)
`InventarioGestionService` = monolito 2147 líneas, 172 LINQ/loops. Viola CLAUDE.md (no partido en `Funciones/` partial, sin `Application/Calculos/`).
| Método | Problema | Fix |
|---|---|---|
| `GetStockAsync` | 2 queries extra de nombres núcleo/galpón con `.Contains` (mala traducción SQL) + Select en memoria | JOIN o vista SQL |
| `GetMovimientosAsync` | Take(3000) + 5 queries de nombres + switch estado/`MapTipoOperacionLabel` en memoria | Vista SQL con joins y etiquetas resueltas |
| `GetTrasladosAsync` | Take(2000) + entradas por dict + 3 queries nombres + agrupa salida↔entrada por grupo en memoria | Vista SQL self-join por `transfer_group_id` |
| `GetIngresosAsync` | Take + dicts + proyección en memoria | Igual |
| `GetTransitosPendientesAsync` | candidatos + set en memoria + `.Contains` | `NOT EXISTS`/`LEFT JOIN ... IS NULL` sobre `transfer_group_id` |
| `RegistrarConsumo/Ingreso/Traslado/Recepcion` | re-llaman `GetStockAsync` completo para 1 fila | proyectar DTO directo |
`InventarioGastoService.SearchAsync` también N+1 (subqueries por fila). **Norte:** GET-list → vistas SQL con filas ya enriquecidas.

## 4. Tablas de BD (public, snake_case; todas con company_id + pais_id)
- **`item_inventario_ecuador`** (catálogo, 136 local): PK `id`; `codigo, nombre, tipo_item, unidad, concepto, grupo, tipo_inventario_codigo, activo, ...`; único `(company_id,pais_id,codigo)`. **El discriminador real es `concepto`** (no `tipo_item`): "alimento"⇒stock Granja→Núcleo→Galpón; otro⇒stock solo Granja. `IsAlimento()` mira `concepto` primero.
- **`inventario_gestion_stock`** (133): PK `id`; FKs `farm_id, item_inventario_ecuador_id, company_id, pais_id`; `nucleo_id/galpon_id` (nullable, solo alimento), `quantity(18,3), unit`. Índice `(farm_id,item,nucleo_id,galpon_id)` = posición lógica de stock.
- **`inventario_gestion_movimiento`** (5818): FKs igual; `movement_type` (Ingreso, Consumo, TrasladoSalida/Entrada, TrasladoInterGranjaSalida/Entrada/Pendiente/Rechazado, AjusteStock, EliminacionStock), `estado`, `from_farm_id/from_nucleo_id/from_galpon_id` (contraparte; invertida en salida inter-granja), `reference, reason, transfer_group_id (Guid), created_at, created_by_user_id`. Índices: `(farm_id,item)`, `movement_type`, `transfer_group_id`, `company_id`, `pais_id`. **No hay soft-delete**: eliminar revierte stock/borra filas o crea AjusteStock/EliminacionStock.
- **`inventario_gasto` + `_detalle` + `_auditoria`** (0 local): cabecera consumo Ecuador + detalle (con snapshots `stock_antes/despues`) + bitácora JSON. El descuento real NO vive aquí: `CreateAsync` llama `RegistrarConsumoAsync` por línea; al eliminar llama `RegistrarIngresoAsync`.

## 5. Consumo desde seguimientos (descuento de stock)
Punto único: **`InventarioGestionService.RegistrarConsumoAsync`** (línea 1102): valida stock, resta `quantity`, crea mov `Consumo`. Requiere Núcleo+Galpón si alimento.
Servicios que lo invocan (inyectan `IInventarioGestionService?` **opcional** → si null, no descuenta; así el mismo servicio sirve Colombia sin inventario Ecuador):
- `SeguimientoLoteLevanteService` (414-541)
- `SeguimientoAvesEngordeEcuadorService` (262-486)

`ParseMetadataItemsToKg` (705-748): recorre `itemsHembras/itemsMachos/itemsGenerales` del Metadata JSON; id = `itemInventarioEcuadorId` (fallback `catalogItemId`); `cantidad`→kg (`ToKg`, gramos/1000); acumula por id; llama `RegistrarConsumoAsync` con ubicación del lote. Update: diff old/new (>0 consumo, <0 devolución). Delete: repone todo. Legacy: `BuildSyntheticMetadataForLegacyRowAsync` reconstruye metadata por `codigo==tipo_alimento`.

## 6. Gating por país/empresa
- **Back** (aislamiento real): `GetEffectiveCompanyIdAsync` (header empresa activa → `CompanyId`), `GetEffectivePaisIdAsync` (`PaisId` o derivado de granja), + granjas asignadas (`user_farms`). Sin "es Ecuador" explícito: el módulo es genérico, se aísla por company+pais+granjas.
- **Front** (solo UX): `CountryFilterService` (Ecuador=2, Panamá=3) desde `TokenStorageService`; `IsEcuadorPipe`, `ShowIfEcuador(Panama)Directive`. Rutas Ecuador solo con `authGuard` (restricción por país es de menú, no de routing).

## 7. Observaciones para la unificación con Colombia
**Genérico (reutilizable tal cual):**
- El trío `inventario_gestion_stock/_movimiento` + `item_inventario_ecuador` **no tiene nada intrínsecamente ecuatoriano salvo el nombre**; ya lleva company+pais y filtra por granjas → sirve a Colombia sin cambio de esquema.
- El flujo TRÁNSITO (salida→recepción por `transfer_group_id`) es genérico → aplicable a Colombia directo.
- El consumo desde seguimientos (`RegistrarConsumoAsync` + `ParseMetadataItemsToKg`) es país-agnóstico; Colombia solo debe poblar el mismo `Metadata.items*` con el id de ítem.

**Ecuador-específico (deuda de nombres):** todo lleva "Ecuador" (`item_inventario_ecuador`, `ItemInventarioEcuadorId` propagado). Unificar ⇒ o renombrar a neutro (`item_inventario`) con migración `RENAME` transversal (grande), o dejar el nombre físico y re-etiquetar solo en UI (menos invasivo).

**Riesgos:**
1. **`from_farm_id` invertido** en salida inter-granja (guarda destino) — trampa #1.
2. Monolito 2147 líneas viola convención → partir en partial + Calculos al unificar.
3. Cómputo en memoria masivo (GET-list traen 2-3k filas) → migrar a vistas SQL (trabajo central).
4. Estados legacy (Pendiente/Rechazado) coexisten con nuevos (Salida) → queries de tránsito deben manejar ambos (`OR`).
5. **Doble camino de consumo**: alimento→gestion-inventario (Núcleo/Galpón); no-alimento→gastos-inventario→también `RegistrarConsumoAsync` (nivel granja). Unificar debe preservar ambos.
6. Re-fetch de stock completo tras cada mutación → costo innecesario.
7. No confundir con `/inventario` viejo (farm-centric, sistema separado).

**Datos BD local `:5433`:** movimiento=5818 (Salida/Tránsito=79, Entrada=80), stock=133, item=136, inventario_gasto*=0.
