# Plan — Unificación de inventarios (criterio estable, por fases)

> Base: `inventario_ecuador_mapa.md` + `inventario_colombia_mapa.md`.
> Norte: reducir fricción/duplicación, empujar cómputo a la BD (que devuelva datos organizados/optimizados),
> SIN romper datos existentes ni cambiar reglas de negocio sin OK explícito.
> Convivencia obligatoria con lo que ya existe (Ecuador 5817 mov, Colombia 188 mov + 18 stock).

## Contexto (resumen del diagnóstico)
- **Modelo A (Colombia)**: `catalogo_items` / `farm_product_inventory` / `farm_inventory_movements`, ruta `/inventario`, ubicación por `location` string.
- **Modelo B (Ecuador/Panamá)**: `item_inventario_ecuador` / `inventario_gestion_stock` / `inventario_gestion_movimiento`, ruta `/gestion-inventario`, ubicación estructurada Granja→Núcleo→Galpón, **tiene flujo TRÁNSITO** y descuento desde seguimientos. Modelo C (gastos) vive sobre B.
- Gating por país = **menú en BD** (`company_menus`→`menus.route`), no en código de rutas.
- 🐞 **Bug crítico**: `SeguimientoLoteLevanteService.ParseMetadataItemsToKg` usa fallback `catalogItemId`→`item_inventario_ecuador_id`; para lotes Colombia puede descontar stock del país equivocado en silencio.

## FASE 1 — Estable / autónoma (NO cambia datos ni comportamiento de negocio)

### S1 — Bugfix: cerrar descuento cross-país silencioso (backend)
- En `SeguimientoLoteLevanteService` (consumo, 402-415 y `ParseMetadataItemsToKg` 694-737): descontar del modelo B **solo** cuando el ítem trae `itemInventarioEcuadorId > 0`; NO usar el fallback `catalogItemId` como id de inventario Ecuador. Gatear además por `pais_id`/país del lote (Ecuador/Panamá).
- Mantener el `try/catch`, pero loggear con nivel real (no silencioso mudo).
- Tests unitarios del parseo (que un ítem solo-Colombia NO genere consumo Ecuador).
- La fila espuria en `inventario_gestion_movimiento` (pais 1) → script de limpieza documentado, ejecutar solo con OK.
- **Riesgo: bajo.** No cambia el comportamiento correcto (Ecuador sigue descontando); cierra un descuento erróneo.

### S2 — Código muerto / duplicado (frontend, sin tocar menús BD)
- Eliminar ruta huérfana `/inventario-management` (mismo `InventarioTabsComponent`, sin menú) de `app.config.ts`.
- Eliminar `features/inventario/page/inventario-managemen/` (duplica el tabs, sin ruta viva) si se confirma sin referencias.
- Documentar (no ejecutar) el menú duplicado en BD (ids 10 y 32 → `/inventario`, empresa 1) para limpieza posterior con OK.
- **Riesgo: bajo.** Solo quita accesos/código sin uso; `yarn build` valida.

### S3 — Cómputo → SQL (el norte)
- **Kardex Colombia** (`FarmInventoryReportService` 56-82: saldo por `foreach` en memoria) → función/vista SQL con `SUM(delta) OVER (PARTITION BY catalog_item_id ORDER BY created_at, id)`; el servicio delega vía `SqlQueryRaw`. **Equivalencia numérica** contra el cálculo actual (golden). Migración idempotente hecha a mano (patrón C1/C2).
- (Opcional, si el validador lo aprueba) GET-lists de `InventarioGestionService` (`GetMovimientos/GetTraslados/GetIngresos/GetTransitosPendientes`) → vistas SQL que ya devuelvan nombres de granja/núcleo/galpón + estado resueltos y el self-join salida↔entrada por `transfer_group_id` (respetando la semántica invertida de `from_farm_id`). Reduce los Take(2000-3000)+dicts en memoria.
- **Riesgo: medio** (aritmética/orden) → mitigado por equivalencia.

### S4 — Reducir duplicación front SIN tocar rutas/menús
- Extraer la lógica común A/B (tipos de movimiento, formularios de ingreso/traslado/ajuste, tablas) a un módulo/servicio compartido que **despache por país** (`is-ecuador`/company), manteniendo las rutas `/inventario` y `/gestion-inventario` vigentes (los menús BD siguen válidos → sin romper navegación por empresa).
- Objetivo: menos archivos duplicados; una sola fuente de la UI de inventario, parametrizada por país.
- **Riesgo: medio** (UI) → mitigado por validación visual por perfil.

## FASE 2 — Requiere DECISIÓN DE NEGOCIO (NO autónoma; documentada)
- Unificar modelos A→B: migrar `farm_product_inventory`/`farm_inventory_movements`/`catalogo_items` a `inventario_gestion_*` + `item_inventario_ecuador` (mapeo de catálogos por `codigo`, rellenar núcleo/galpón para las 188+18 filas Colombia). Renombrar tablas/columnas a neutro (`item_inventario`) es transversal.
- ¿**Colombia empieza a descontar** stock desde seguimientos? (hoy no lo hace; activarlo cambia comportamiento y stock histórico).
- Consolidar a **una sola ruta** de inventario + actualizar menús BD (prod) — con OK.

## Validación por slice (Fase 1)
- `cd backend && dotnet build` (0/0) + `dotnet test`; `cd frontend && yarn build`.
- E2E del perfil afectado (EC/PA/CO) con las credenciales reales.
- Migraciones idempotentes; sin DDL en prod sin OK; sin procesos huérfanos.

## QA final (todas las facetas, por perfil EC/PA/CO)
Validar estable + cumple requerimiento: catálogo, stock, ingresos, traslados (misma granja e inter-granja), **flujo tránsito** (salida→recepción→rechazo), consumo desde seguimientos diarios (levante/producción), kardex/histórico, gastos (Ecuador). Sin regresiones vs comportamiento actual.

## Orden de ejecución (autónomo, un slice por commit, con build/test/E2E)
S1 (bugfix) → S2 (dead code) → S3 (kardex→SQL, luego vistas gestión) → S4 (dedup front por país). Fase 2 queda para decisión del usuario.
