# Validación Entidades ↔ BD — PARTE B: Auditoría de Funciones, Triggers y Vistas

> **Objetivo:** garantizar que **todos** los objetos de base de datos (funciones, triggers,
> vistas) que hoy existen en la BD local de pruebas tengan su **migración EF correspondiente**,
> de modo que al desplegar a producción (`Database__RunMigrations=true`) se creen automáticamente.
>
> **Fuente de verdad:** introspección de la BD local `sanmarinoapplocal` (`pg_proc`, `pg_trigger`,
> `information_schema`) cruzada con el contenido real de los archivos de migración en
> `ZooSanMarino.Infrastructure/Migrations`.
>
> **Fecha:** 2026-05-31 · **Estado historial EF local:** 55 migraciones aplicadas, tope
> `20260531034622_FixFnSeguimientoEngordeCortePorCierreAlimento`, 0 pendientes.

---

## 1. Resumen ejecutivo

| Categoría | Total en BD | Con migración | **SIN migración (falta crear)** |
|-----------|:-----------:|:-------------:|:-------------------------------:|
| Funciones SQL / PLpgSQL | 16 | 3 | **13** |
| Triggers (objetos `CREATE TRIGGER`) | 7 | 1 | **6** |
| Vistas (`vw_*`) | 3 | 0 | **3** |

> ⚠️ **Riesgo de producción confirmado:** la mayoría de funciones, triggers y vistas se
> crearon históricamente mediante **scripts SQL manuales** en `/backend/sql/` aplicados a mano,
> NO mediante migraciones EF. En un despliegue a una BD que no los tenga, **no se crearían** y la
> aplicación fallaría al invocarlos (p. ej. la liquidación de engorde llama
> `fn_seguimiento_diario_engorde`, que a su vez usa `weeknum_iso` y
> `fn_acumulado_entradas_alimento`, ninguna de las dos con migración).

**Acción:** crear UNA migración nueva idempotente
`AddMissingDbFunctionsTriggersAndViews` que registre todos los objetos faltantes (ver §5).

---

## 2. Auditoría de FUNCIONES

| # | Función | Args | Tipo | ¿Migración? | Acción |
|---|---------|------|------|-------------|--------|
| 1 | `fn_seguimiento_diario_engorde` | `p_lote_id int` | func | ✅ `20260520140828` (+fixes `…528212753`, `…530203013`, `…531034622`) | Ninguna |
| 2 | `fn_indicadores_pollo_engorde` | `p_lote_id int, p_peso_ajuste num, p_divisor_ajuste num` | func | ✅ `20260530211846` | Ninguna |
| 3 | `trg_lote_hist_desde_movimiento_pollo_engorde` | — | trigger-fn | ✅ `20260530203013` | Ninguna |
| 4 | `weeknum_iso` | `p_date date` | func | ❌ | **CREAR** (helper de `fn_seguimiento_diario_engorde`) |
| 5 | `dpr` | `x float8, n int` | func | ❌ | **CREAR** (redondeo, helper de `fn_indicadores_pollo_engorde`) |
| 6 | `fn_acumulado_entradas_alimento` | `p_lote_id int, p_hasta_id bigint` | func | ❌ | **CREAR** (helper de `fn_seguimiento_diario_engorde`) |
| 7 | `fn_espejo_huevo_produccion_upsert` | — | trigger-fn | ❌ | **CREAR** (usada por `tr_espejo_huevo_produccion_aiud`) |
| 8 | `fn_lote_ave_engorde_id_desde_ubicacion` | `p_farm_id int, p_nucleo_id varchar, p_galpon_id varchar` | func | ❌ | **CREAR** |
| 9 | `fn_tipo_evento_inventario` | `p_mt varchar` | func | ❌ | **CREAR** |
| 10 | `sp_recalcular_seguimiento_levante` | `l_lote_id text` | func | ❌ | **CREAR** |
| 11 | `trg_historico_lote_postura_levante` | — | trigger-fn | ❌ | **CREAR** (usada por `trg_hlp_lote_postura_levante`) |
| 12 | `trg_historico_lote_postura_produccion` | — | trigger-fn | ❌ | **CREAR** (usada por `trg_hlp_lote_postura_produccion`) |
| 13 | `trg_lote_hist_desde_inventario_gestion` | — | trigger-fn | ❌ | **CREAR** (usada por `trg_inventario_gestion_movimiento_lote_hist`) |
| 14 | `trg_lote_hist_mov_pollo_anulado` | — | trigger-fn | ❌ | **CREAR** (usada por `trg_movimiento_pollo_engorde_lote_hist_anula`) |
| 15 | `trg_lotes_sync_lote_postura_levante` | — | trigger-fn | ❌ | **CREAR** (usada por `trg_lotes_sync_lote_postura_levante`) |
| 16 | `trg_lote_postura_levante_cerrar_produccion` | — | trigger-fn | ❌ | **CREAR** (⚠️ su trigger está deshabilitado, ver `disable_trg_lpl_cerrar_produccion.sql` — se incluye la función por completitud) |

---

## 3. Auditoría de TRIGGERS

| # | Trigger | Tabla | Evento | Función | ¿Migración? | Acción |
|---|---------|-------|--------|---------|-------------|--------|
| 1 | `trg_movimiento_pollo_engorde_lote_hist` | `movimiento_pollo_engorde` | AFTER INS/UPD | `trg_lote_hist_desde_movimiento_pollo_engorde` | ✅ `20260530203013` | Ninguna |
| 2 | `tr_espejo_huevo_produccion_aiud` | `seguimiento_diario_levante_reproductoras` | AFTER INS/UPD/DEL | `fn_espejo_huevo_produccion_upsert` | ❌ | **CREAR** |
| 3 | `trg_hlp_lote_postura_levante` | `lote_postura_levante` | AFTER INS/UPD | `trg_historico_lote_postura_levante` | ❌ | **CREAR** |
| 4 | `trg_hlp_lote_postura_produccion` | `lote_postura_produccion` | AFTER INS/UPD | `trg_historico_lote_postura_produccion` | ❌ | **CREAR** |
| 5 | `trg_inventario_gestion_movimiento_lote_hist` | `inventario_gestion_movimiento` | AFTER INS | `trg_lote_hist_desde_inventario_gestion` | ❌ | **CREAR** |
| 6 | `trg_lotes_sync_lote_postura_levante` | `lotes` | AFTER INS/UPD | `trg_lotes_sync_lote_postura_levante` | ❌ | **CREAR** |
| 7 | `trg_movimiento_pollo_engorde_lote_hist_anula` | `movimiento_pollo_engorde` | AFTER UPD (WHEN anulado/borrado) | `trg_lote_hist_mov_pollo_anulado` | ❌ | **CREAR** |

---

## 4. Auditoría de VISTAS

| # | Vista | ¿Migración? | Acción |
|---|-------|-------------|--------|
| 1 | `vw_seguimiento_pollo_engorde` | ❌ | **CREAR** |
| 2 | `vw_indicadores_diarios_engorde` | ❌ | **CREAR** |
| 3 | `vw_liquidacion_ecuador_pollo_engorde` | ❌ | **CREAR** |

---

## 5. Plan de migración propuesto

Crear **una migración única** `AddMissingDbFunctionsTriggersAndViews`, sólo SQL crudo
(`migrationBuilder.Sql(...)`), **100% idempotente** para que sea segura tanto en BD nuevas como
en BD que ya tengan los objetos (caso de producción, donde se aplicaron por script manual):

1. **Funciones** (13): `CREATE OR REPLACE FUNCTION …` — idempotente por naturaleza.
   Orden: helpers (`weeknum_iso`, `dpr`, `fn_acumulado_entradas_alimento`) → resto de funciones →
   trigger-functions. (El orden no afecta la creación en PLpgSQL, pero se respeta por claridad.)
2. **Vistas** (3): `CREATE OR REPLACE VIEW …`.
3. **Triggers** (6): `DROP TRIGGER IF EXISTS <t> ON <tabla>;` seguido de `CREATE TRIGGER …`
   (PostgreSQL no soporta `CREATE OR REPLACE TRIGGER` en todas las versiones → patrón drop+create).
4. **`Down()`**: `DROP TRIGGER IF EXISTS …`, `DROP VIEW IF EXISTS …`,
   `DROP FUNCTION IF EXISTS …` en orden inverso.

El DDL exacto se extrae de la BD con `pg_get_functiondef` / `pg_get_viewdef` /
`pg_get_triggerdef` (estado actual real), no de los scripts `.sql` históricos (que pueden estar
desactualizados respecto a los últimos `fix_*`).

> **Por qué `CREATE OR REPLACE` y no `IF NOT EXISTS`:** en producción estos objetos ya existen
> (creados a mano). `CREATE OR REPLACE` los deja en la versión correcta del código sin error;
> en una BD nueva los crea. Es el patrón idempotente recomendado por `CLAUDE.md`.

---

## 6. Desalineaciones detectadas (para revisar)

### 6.1 ℹ️ Separación de seguimiento por país (Ecuador/Panamá) — DESCARTADA (intencional)
**Decisión del negocio (2026-05-31): la separación de seguimiento por país NO se necesita.**
Por eso las tablas `seguimiento_diario_aves_engorde_ecuador` / `_panama` **fueron dropeadas
manualmente** y no existen en la BD. Su ausencia es **intencional**, no un error.

**Decisión sobre el código (2026-05-31): solo documentar, NO tocar código** por ahora. Quedan
estos restos del intento abandonado, que conviene tener presentes:

- **Código aún cableado** (no es código muerto): entidades `SeguimientoDiarioAvesEngordeEcuador`/
  `Panama` + configs + `DbSet` (`ZooSanMarinoContext.cs:87-88`) + servicios registrados en DI
  (`Program.cs:226-227`) + controllers `SeguimientoAvesEngordeEcuadorController` /
  `SeguimientoAvesEngordePanamaController` + menú (`20260517131727_AddMenu_SeguimientoAvesEngordePanama`).
- **Caveat 1 (runtime):** esos controllers consultan las tablas inexistentes → **fallarán (500)
  si se invocan**. Aceptable mientras nadie use esos endpoints/menús.
- **Caveat 2 (deploy a BD nueva):** la migración `20260517104629_SplitSeguimientoDiarioAvesEngordeByCountry`
  hace `CreateTable` de ambas tablas. En una BD de producción **nueva** (historial vacío), EF
  ejecutaría esa migración y **recrearía** las tablas. En la prod actual (si la migración ya
  figura aplicada) no se recrean. Es decir, el estado "sin tablas" no está garantizado por las
  migraciones; depende del historial de cada entorno.
- **Limpieza futura (si se decide):** eliminar entidades/configs/DbSets/servicios/controllers/
  menú de Ecuador/Panamá y añadir una migración que haga `DROP TABLE IF EXISTS` de ambas, para
  que el backend quede consistente con "sin separación por país" en todos los entornos.

### 6.2 🟡 Tablas huérfanas en BD (sin entidad ni `DbSet` en el código)
- `user_paises` y `guia_semana` existen en la BD pero **no tienen entidad, configuración ni
  `DbSet`** en el backend. Probablemente remanentes de trabajo previo.
- No afectan el arranque, pero conviene decidir si se eliminan o se documentan como tablas
  legacy. EF no las gestiona.

### 6.3 🟡 FKs múltiples / redundantes sobre `granja_id` y `nucleo_id`
- Varias tablas (`galpones`, `lotes`, `lote_ave_engorde`, `produccion_lotes`) tienen **FKs
  duplicadas** de `granja_id` apuntando a la vez a `farms.id` y a `nucleos.(granja_id|nucleo_id)`,
  y de `nucleo_id` apuntando a múltiples columnas de `nucleos`. Es herencia del modelo
  núcleo↔granja. No bloquea, pero genera ambigüedad en el grafo de relaciones (ver Parte A).

### 6.4 ℹ️ Tablas auxiliares no productivas
- `_backfill_factura_id_2026_05_30`, `_migracion_saldo_alimento_2026_05_28`, `dbstudio_audit`,
  `_ignored_produccion_diaria`: tablas temporales/de auditoría, sin entidad. Ignorables.

---

## 7. Verificación post-migración (local)

1. `dotnet ef database update` aplica la nueva migración sin error (los `CREATE OR REPLACE`
   reemplazan los objetos existentes en local de forma idempotente).
2. Confirmar que las 13 funciones, 6 triggers y 3 vistas siguen presentes tras aplicar.
3. **No** ejecutar contra RDS prod desde la máquina; el deploy de ECS aplica las migraciones al
   arrancar (ver `CLAUDE.md` → CI/CD).
