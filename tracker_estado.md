# Tracker — Unificar inventario Colombia en el módulo nuevo + migración de datos

> Plan: [`fase_de_desarrollo/unificacion_inventario_colombia_plan.md`](fase_de_desarrollo/unificacion_inventario_colombia_plan.md)
> Fase: análisis hecho. Bloqueado por decisiones (DB target + alcance) antes de escribir/correr migración.
> (El refactor de diseño `shared/ui` queda pausado: su plan + memoria preservados.)

## Análisis ✅
- [x] Mapear módulos viejo (`inventario`/farm_product_inventory) vs nuevo (`gestion-inventario`/inventario_gestion_stock)
- [x] Estado real de datos (BD prod en local :5433): Colombia sigue en el viejo (20 stock, 61 ítems); nuevo casi sin Colombia
- [x] Confirmado: **ítems Colombia NO migrados** al catálogo nuevo (item_inventario_ecuador = 0 co1); esquema destino YA alineado (no DDL)

## Bloqueos ✅ RESUELTOS
- [x] **DB target:** la data está en **PG 17 = :5433** (nativo; no hay docker). El :5432 es PG 13 viejo sin data. ⚠️ appsettings apunta a :5432 → para ver la migración local, apuntar la app a :5433.
- [x] **Alcance:** COMPLETO = ítems + stock + movimientos + re-cablear consumo (código)
- [x] Colombia = nivel granja (nucleo/galpon NULL) — confirmado (maneja_alimento_por_galpon=false)

## Migración de datos
- [x] Paso 1 — ítems `catalogo_items`(co1,61) → `item_inventario_ecuador`(co1,pais1): **INSERT 61, idempotente** (`backend/sql/migracion_inventario_colombia_01_items.sql`)
- [x] Paso 2 — stock `farm_product_inventory`(co1,20) → `inventario_gestion_stock`: **INSERT 20, saldos 20/20 idénticos** (`backend/sql/migracion_inventario_colombia_02_stock.sql`)
- [x] Paso 3 — movimientos `farm_inventory_movements`(co1,323) → `inventario_gestion_movimiento`: **INSERT 323, kardex reconcilia 21/21 con stock** (`backend/sql/migracion_inventario_colombia_03_movimientos.sql`) · Entry→Ingreso, Exit→Consumo, Transfer→Traslado
- [x] Backend arranca limpio contra :5433 (migraciones up-to-date) → data migrada consistente con EF
- [ ] **Re-cablear consumo** seguimiento diario levante/producción → módulo nuevo (CÓDIGO, riesgoso — "Slice 2b")
- [ ] Empaquetar como migración EF idempotente (auto-aplica en deploy) o script backfill; **confirmar antes de prod**

## Menú ✅
- [x] Quitados menus 10 y 32 (viejo `/inventario`) de company_menus/role_menus de Colombia. Queda solo menu 50 (nuevo `/gestion-inventario`) + 49 (Ítems). Verificado. (`backend/sql/migracion_inventario_colombia_04_menu.sql`) · sin borrar código del viejo aún

## Consumo ✅ (ya estaba en código; lo desbloqueó la migración de ítems)
- [x] Re-cableo YA implementado (Fase 3 paso 2): `SeguimientoLoteLevanteService` + `ProduccionService` usan `_colombiaConsumoB` (`ColombiaInventarioConsumoService`) para Colombia (`ModeloBNivelGranja`) → descuentan de `inventario_gestion_stock` nivel granja + movimiento `Consumo`. El viejo `_farmInventoryConsumo` quedó "sin uso".
- [x] Estaba inerte porque `ResolverItemsBPorCatalogItemAsync` (mapeo catalogItemId→item_inventario_ecuador por código) no encontraba ítems Colombia → **el backfill del paso 1 lo habilita** (mapeo verificado 323/323 y 20/20).
- [ ] Test E2E: un seguimiento Colombia con consumo → verificar que baja `inventario_gestion_stock` + crea movimiento `Consumo` (requiere back en :5433 + login). Pendiente opcional.

## Entrega a prod ✅
- [x] Migración EF idempotente `20260705194156_MigrarInventarioColombiaAModeloB` (4 backfills embebidos, date-fix). `Down` no-op.
- [x] Validada en fresco (PG17): aplica vía `dotnet ef database update`; fechas originales; saldos 20/20; kardex 21/21; idempotente.
- [x] `dotnet build` 0/0 · `dotnet test` 122 verdes · E2E UI (stock+histórico+menú) OK
- [x] Commit `35caab8` (sin trailer Claude) → push `origin/main` (ff) → **PR #23 mergeado** a `main-produccion` (cd113cf)
- [x] Backups rollback en origin: `rollback/{main,prod}-pre-inventario-20260705`
- [x] Deploy OK y verificado: ECS backend `sanmarino-back-task:112` + front `:111`, rollout COMPLETED, imagen `cd113cf`; migración aplicada en RDS prod (rollout COMPLETED con RunMigrations=true); front 200 / api 401.
