# Plan — Rename neutro del módulo de Inventario (Ecuador → neutro, multipaís)

> **Estado:** PROPUESTA — requiere OK del usuario en el esquema de nombres y en la profundidad del cambio (BD/rutas) ANTES de tocar nada.
> Tracker: [`tracker_estado_inventario_rename.md`](../tracker_estado_inventario_rename.md) (propio; NO uso `tracker_estado.md`, en uso por la feature "alimentos múltiples por género").

## 0. Contexto y objetivo

El módulo de inventario nació solo para **Ecuador**; hoy lo comparten **Ecuador, Panamá y Colombia** (modelo B unificado). El naming `…Ecuador…` quedó como deuda. Objetivo:

1. **Nombres neutros** en código (C#/TS), y — según OK — en rutas y BD.
2. **Parametrización empresa/granja/país** para mostrar/ocultar campos (agua solo EC/PA; ubicación núcleo/galpón según `ManejaAlimentoPorGalpon`), usando los primitivos existentes (`CountryFilterService`, `ShowIfEcuadorPanamaDirective`, flag `ManejaAlimentoPorGalpon`). **No hardcodear "ecuador".**

**Regla rectora:** refactor ≠ cambio de comportamiento. EC/PA y Colombia deben seguir funcionando idéntico (mismo descuento de inventario, mismos redondeos, mismos contratos efectivos).

---

## 1. Las CUATRO capas de naming (riesgo muy distinto) — decisión clave

El string "ecuador" del inventario vive en 4 capas independientes. **NO son lo mismo** y se tratan por separado:

| Capa | Qué es | ¿Se puede renombrar? | Riesgo |
|---|---|---|---|
| **A. Claves jsonb persistidas** | `itemInventarioEcuadorId`, `catalogItemId` dentro de `metadata` / `items_adicionales` (jsonb) de MILES de `seguimiento_diario` (`itemsHembras[]`/`itemsMachos[]`/`itemsGenerales[]`). Modeladas por `ItemSeguimientoDto` con `[JsonPropertyName("itemInventarioEcuadorId")]` / `[JsonPropertyName("catalogItemId")]`. | ❌ **NUNCA.** Congeladas. | 🔴 Romper = no cargan registros históricos al editar + reportes rotos. |
| **B. Contrato API (wire)** | Claves camelCase de los DTOs `InventarioGestion*Dto` (`itemInventarioEcuadorId`, …) y rutas `/api/item-inventario-ecuador`, `/api/inventario-gestion/*`. **No** persistidas; sí consumidas por ~30 usos de front. | ⚠️ Sí, pero rompe contrato → coordinar front+back mismo deploy **o** mantener alias (`[JsonPropertyName]` + ruta compat). | 🟠 Romper sin coordinar = front 400/undefined. |
| **C. Esquema BD** | Tabla `item_inventario_ecuador`, columna FK `item_inventario_ecuador_id` (en `inventario_gestion_stock` y `inventario_gestion_movimiento`), índices `ix_item_inventario_ecuador_*`. | ⚠️ Sí, con migración EF idempotente + regen snapshot + actualizar TODO el SQL crudo/funciones/vistas que la nombran. DDL en prod solo con OK explícito. | 🔴 FKs, vistas (`vw_validacion_alimento_engorde`), scripts `/backend/sql/`; incidente raíz del proyecto = migraciones mal marcadas → SIGSEGV. |
| **D. Símbolos de código** | Clases/props/DTOs/servicios/interfaces/variables C# + tipos/props/vars TS. Interno, sin efecto en datos ni contrato **si** las capas A/B se pinnean. | ✅ Libre. | 🟢 Solo build. |

> **La capa A es el gotcha #1.** Renombrar la propiedad C# `ItemInventarioEcuadorId` de `ItemSeguimientoDto` es seguro **solo si** se conserva `[JsonPropertyName("itemInventarioEcuadorId")]`. Igual `catalogItemId`. Ídem las claves que escribe `BuildMetadata`/`BuildItemsAdicionales` en `CreateSeguimientoLoteLevanteRequest.cs` (literales `itemInventarioEcuadorId = …`, `catalogItemId = …`): **no tocar los literales de clave.**

---

## 2. Auditoría de consumidores (código manda — no planes viejos)

### Backend — catálogo de ítems (`item_inventario_ecuador`)
- `Domain/Entities/ItemInventarioEcuador.cs` — entidad.
- `Infrastructure/Persistence/Configurations/ItemInventarioEcuadorConfiguration.cs` — `ToTable("item_inventario_ecuador")`, índices.
- `Infrastructure/Services/ItemInventarioEcuadorService.cs` — CRUD + carga masiva.
- `Application/Interfaces/IItemInventarioEcuadorService.cs`.
- `Application/DTOs/ItemInventarioEcuadorDtos.cs` — `ItemInventarioEcuadorDto`, `…CreateRequest`, `…UpdateRequest`, `…CargaMasivaRow`, `…CargaMasivaResult`.
- `API/Controllers/ItemInventarioEcuadorController.cs` — ruta `api/item-inventario-ecuador`, `[Tags(...)]`.
- `Infrastructure/Persistence/ZooSanMarinoContext.cs` — `DbSet ItemInventarioEcuador` (1 ref).
- `API/Program.cs` — registro DI `IItemInventarioEcuadorService` (1 ref).

### Backend — stock/movimientos (`inventario_gestion_*`, YA es neutro salvo la FK)
- `Domain/Entities/InventarioGestionStock.cs`, `InventarioGestionMovimiento.cs` — propiedad `ItemInventarioEcuadorId` + navegación `ItemInventarioEcuador`.
- `Persistence/Configurations/InventarioGestionStockConfiguration.cs`, `InventarioGestionMovimientoConfiguration.cs` — `HasColumnName("item_inventario_ecuador_id")`, índices, FK.
- `Infrastructure/Services/InventarioGestionService.cs` — **120 refs** (navegaciones, joins, DTOs).
- `Application/Interfaces/IInventarioGestionService.cs`, `Application/DTOs/InventarioGestionDtos.cs` — props `ItemInventarioEcuadorId` en 6 DTOs + filtro.
- `API/Controllers/InventarioGestionController.cs` — query param `itemInventarioEcuadorId`.

### Backend — consumo Colombia + gate + parser
- `Infrastructure/Services/ColombiaInventarioConsumoService.cs` — resuelve `catalogItemId → item_inventario_ecuador.id`; strings en mensajes de error.
  - ⚠️ El archivo `Application/Calculos/ColombiaInventarioIdResolutionCalculos.cs` que menciona la tarea **NO existe**; la lógica está inline en el service. (Código manda.)
- `Application/Interfaces/IColombiaInventarioConsumoService.cs`.
- `Application/Calculos/InventarioConsumoGate.cs` — comentarios + enum docs; sin símbolos "ecuador".
- `Application/Calculos/MetadataEngordeCalculos.cs` — parser `catalogItemId → itemInventarioEcuadorId` (2 refs). **Capa A: no cambiar claves de lectura.**
- `Application/DTOs/CreateSeguimientoLoteLevanteRequest.cs` + `CreateSeguimientoDiarioLoteReproductoraRequest.cs` — `ItemSeguimientoDto` (capa A).
- Servicios de seguimiento que descuentan: `SeguimientoLoteLevanteService.cs`, `SeguimientoDiarioLoteReproductoraService.cs`, `SeguimientoAvesEngordeService.cs`, `SeguimientoAvesEngordeEcuadorService.cs`, `ProduccionService.cs` — usan `itemInventarioEcuadorId` desde metadata.

### Frontend
- `features/gestion-inventario/services/gestion-inventario.service.ts` — `ItemInventarioEcuadorDto`, `InventarioGestion*Dto` con `itemInventarioEcuadorId`, rutas `/item-inventario-ecuador`.
- `features/gestion-inventario/pages/gestion-inventario-page/*` — 29+6 refs.
- `features/config/item-inventario-ecuador/**` — módulo config entero (routing, module, service, list, form) — **carpeta + archivos con "ecuador".**
- Consumidores del `itemInventarioEcuadorId` (wire) en seguimiento: `lote-levante/pages/modal-create-edit/*` (⚠️ **cambios sin commitear de OTRA feature — no revertir**), `lote-produccion/*`, `seguimiento-diario-lote-reproductora/*`, `engorde-comun/*`, `aves-engorde/services/*`, `lote-levante/services/*`, `lote-produccion/services/*`.
- `gastos-inventario/*` — usa `item-inventario-ecuador` para catálogo.

### SQL / migraciones (NO editar las Designer.cs históricas)
- Vistas/funciones a auditar si se renombra tabla: `backend/sql/vw_validacion_alimento_engorde.sql` (view), `item_inventario_ecuador_table.sql`, `inventario_gestion_tables.sql`, `fase3_migracion_stock_co_a_b.sql`, `cuadre_inventario_expected_m0_2602.sql`, `migracion_inventario_colombia_0{1,2,3}*.sql`, `create_lote_registro_historico_unificado.sql`.
- `Migrations/*.Designer.cs` + `ZooSanMarinoContextModelSnapshot.cs`: **historia congelada**; solo el snapshot se regenera al crear migración nueva. No editar a mano las viejas.
- `db\data` (dump) y `fase_de_desarrollo/*.md` (docs): informativos, no runtime.

---

## 3. Esquema de nombres neutros propuesto (requiere OK)

| Actual | Neutro propuesto |
|---|---|
| `ItemInventarioEcuador` (entidad) | `ItemInventario` |
| `ItemInventarioEcuadorConfiguration` | `ItemInventarioConfiguration` |
| `ItemInventarioEcuadorService` / `IItemInventarioEcuadorService` | `ItemInventarioService` / `IItemInventarioService` |
| `ItemInventarioEcuadorDto` / `…CreateRequest` / `…UpdateRequest` / `…CargaMasivaRow` / `…CargaMasivaResult` | `ItemInventarioDto` / `ItemInventarioCreateRequest` / … |
| `DbSet<ItemInventarioEcuador> ItemInventarioEcuador` | `DbSet<ItemInventario> ItemInventario` |
| Propiedad `ItemInventarioEcuadorId` (stock, movimiento, DTOs, filtros) | `ItemInventarioId` |
| Navegación `ItemInventarioEcuador` (stock, movimiento) | `ItemInventario` |
| `ItemSeguimientoDto.ItemInventarioEcuadorId` (C#) | `ItemInventarioId` **+ `[JsonPropertyName("itemInventarioEcuadorId")]`** (capa A: wire/persistido intacto) |
| Ruta `/api/item-inventario-ecuador` | `/api/inventario/items` (**+ alias viejo** o coordinar deploy) |
| Front folder `config/item-inventario-ecuador/` | `config/item-inventario/` |
| Front `ItemInventarioEcuadorDto` (TS) | `ItemInventarioDto` |
| Front prop `itemInventarioEcuadorId` (wire) | ver decisión capa B |
| Front `itemsEcuadorPanama`, `cargarStockEcuadorPanama`, `aplicarConsultaInventarioEcuadorPanama`, `itemEcuadorToCatalogItem`, `conceptosEcuadorPanama`, `cargarCatalogEcuadorPanama` (modal levante) | `itemsInventario`, `cargarStockInventario`, `aplicarConsultaInventario`, `itemInventarioToCatalogItem`, `conceptosInventario`, `cargarCatalogInventario` — ⚠️ **coordinar con la otra sesión** (modal levante tiene cambios sin commitear) |

`InventarioGestion*` (stock/movimiento/service/controller/DTOs) **ya es neutro** → se mantiene; solo cambia la propiedad/columna `…Ecuador…`.

---

## 4. Decisiones que requieren OK (bloqueantes)

- **D1 — Profundidad BD (capa C):**
  - (a) **Solo código** (recomendado este pase): tabla y columnas quedan `item_inventario_ecuador` / `item_inventario_ecuador_id` (internas, no visibles al usuario). Cero DDL, cero riesgo. La entidad neutra mapea a la tabla vieja vía `ToTable`/`HasColumnName`.
  - (b) **Rename físico completo**: migración EF idempotente (tabla + columnas FK + índices) + regen snapshot + actualizar vistas/funciones/SQL crudo + probar local + OK de DDL prod. Mayor valor cosmético, mayor riesgo.
- **D2 — Contrato/rutas (capa B):**
  - (a) **Mantener wire estable** (recomendado): neutralizar C#/TS internos pero conservar claves wire (`itemInventarioEcuadorId`) vía `[JsonPropertyName]` y **agregar** ruta neutra como alias sin quitar la vieja. Sin deploy coordinado.
  - (b) **Romper y coordinar**: renombrar wire + rutas y desplegar front+back juntos.
- **D3 — Parametrización:** confirmar alcance (solo ocultar/mostrar por país+`ManejaAlimentoPorGalpon` con los primitivos actuales, sin nueva config de BD) vs. una config nueva por empresa/granja de qué campos mostrar.

**Recomendación:** D1(a) + D2(a) + D3 con primitivos actuales → máximo valor (código y UI neutros y parametrizados), mínimo riesgo (cero DDL prod, cero ruptura de contrato). El rename físico de tabla queda como fase opcional posterior con su propio OK.

---

## 5. Fases de ejecución (build+tests en cada una)

- **F1 — Backend catálogo (capa D):** renombrar entidad/config/service/interface/DTOs/controller/DI del catálogo. Mapear a tabla/columnas actuales (D1a) o migrar (D1b). Ruta neutra + alias (D2a). `dotnet build` 0/0 + `dotnet test`.
- **F2 — Backend stock/movimiento (capa D):** renombrar prop `ItemInventarioEcuadorId`→`ItemInventarioId` y navegación en entidades/config/DTOs/service/controller/interface. Pinnear wire con `[JsonPropertyName]` (D2a). Build+test.
- **F3 — Backend seguimiento/consumo (capa A intacta):** neutralizar C# de `ItemSeguimientoDto` conservando `[JsonPropertyName]`; NO tocar literales de clave en `BuildMetadata`/parser. Test de equivalencia del parser (`MetadataEngordeCalculos`) verde. Build+test.
- **F4 — (opcional, con OK) Migración BD (capa C):** EF idempotente rename tabla+columnas+índices, regen snapshot, actualizar vistas/funciones SQL, **probar local**. Sin DDL prod sin OK.
- **F5 — Frontend catálogo + servicio:** renombrar carpeta `config/item-inventario`, tipos TS, rutas del service. Coordinar con rutas back (alias). `ng build` 0 err.
- **F6 — Frontend consumidores + parametrización:** neutralizar símbolos en gestion-inventario y seguimiento (coordinando el modal-levante con la otra sesión); aplicar `CountryFilterService`/`ShowIfEcuadorPanamaDirective`/`ManejaAlimentoPorGalpon` para visibilidad de campos. `ng build`.

## 6. Casos de prueba (gate)
- Parser metadata (`MetadataEngordeCalculos`): registro histórico con `itemInventarioEcuadorId`/`catalogItemId` sigue parseando idéntico (xUnit existente verde + caso de lectura hacia atrás).
- Consumo EC/PA (galpón) y Colombia (nivel granja) descuentan idéntico (mismos ids, mismo stock) — no cambia el gate ni el id-resolution.
- CRUD catálogo + carga masiva Excel responden igual (ruta nueva y alias vieja).
- Front: stock, ingreso, traslado, histórico, y seguimiento diario (levante/producción/engorde) cargan y guardan igual; campos de agua solo EC/PA; ubicación núcleo/galpón según flag.
