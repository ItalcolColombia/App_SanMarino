# Plan: Fix Producción 405 Method Not Allowed — Backend Traslados

**Fecha:** 2026-05-26
**Severidad:** Bloqueante (producción no recibe el código nuevo)
**Síntoma reportado:** `PUT /api/LotePosturaBase/2` → `405 Method Not Allowed, allow: GET, OPTIONS`

---

## 1. Diagnóstico (causa raíz)

### 1.1 ECS está corriendo código viejo
- **Servicio:** `sanmarino-back-task-service-75khncfa` en cluster `devSanmarinoZoo`.
- **TaskDef activa:** `sanmarino-back-task:90` → imagen `backend:d268d488...` (commit del 21/may).
- **TaskDef nueva (registrada hoy):** `sanmarino-back-task:92` → imagen `backend:20260526-0924`.
- El `make deploy-backend` registró la 92, intentó arrancar **3 tareas** (09:38, 09:43, 09:49), todas terminaron con `ExitCode 139 (SIGSEGV)`. ECS marcó `deployment failed: tasks failed to start` y **rolló back a la 90** a las 09:51.
- Por eso producción no tiene `PUT /api/LotePosturaBase/{id}` — ese endpoint sí está en la imagen nueva, pero la imagen nueva nunca arrancó.

### 1.2 El SIGSEGV viene de migraciones EF rotas
La task definition de producción pasa `Database__RunMigrations=true`. La app intenta correr migraciones pendientes al arrancar. Hay **8 migraciones pendientes en RDS prod**:

| Migración | Estado prod |
|---|---|
| 20260521100000_AddFechaAlistamientoLoteEngorde | pendiente |
| 20260521110000_AddPesosRealesMovimientoEngorde | pendiente |
| 20260524143526_AddTrasladoAvesFieldsToSeguimientoLevante | pendiente |
| 20260524143554_AddTrasladoAvesFieldsToProduccionSeguimiento | pendiente (vacía, OK) |
| 20260524180000_AddFarmIdErpCreateToLotePosturaBase | pendiente |
| 20260524214316_AddTrasladoAcumuladosLPL | pendiente |
| 20260524223050_AddTrasladoSplitsToSeguimientoDiarioLev | pendiente |
| 20260525031337_AddUpdatedByUserIdSeguimientoDiarioLev | pendiente |
| 20260525041719_AddTrasladoAcumuladosLPPandSeguimiento | **pendiente y rota** ⚠ |
| 20260525131406_RenameTrasladoColumnsPerFase | pendiente |

Última aplicada en prod: `20260520140828_AddFnSeguimientoDiarioEngorde`.

### 1.3 La migración bloqueante
`20260525041719_AddTrasladoAcumuladosLPPandSeguimiento` ejecuta:
```sql
ALTER TABLE public.produccion_seguimiento ADD COLUMN IF NOT EXISTS ...
```
Pero **`produccion_seguimiento` no existe en RDS prod**. La tabla real es `seguimiento_diario_produccion_reproductoras` (renombrada por la migración `20260507181055`). La entidad `ProduccionSeguimiento` en código mapea a un nombre que no existe en la BD de producción → `ALTER TABLE` falla → migración revienta → proceso muere.

### 1.4 Columnas faltantes confirmadas en RDS prod
| Tabla | Columnas faltantes |
|---|---|
| `lote_postura_base` | `farm_id`, `erp_create` |
| `seguimiento_diario_levante_reproductoras` | `es_traslado`, `traslado_lote_contraparte_id`, `traslado_granja_contraparte_id`, `traslado_direccion`, `traslado_ingreso_hembras/machos`, `traslado_salida_hembras/machos`, `updated_by_user_id` |
| `lote_postura_levante` | `levante_traslado_ingreso_hembras/machos`, `levante_traslado_salida_hembras/machos` |
| `lote_postura_produccion` | `produccion_traslado_ingreso_hembras/machos`, `produccion_traslado_salida_hembras/machos` |
| `produccion_seguimiento` | **toda la tabla no existe** |

---

## 2. Estrategia de remediación

### Opción A — Fix mínimo y seguro (recomendada)
1. **Deshabilitar `Database__RunMigrations` en producción** (la BD se gestiona manualmente con scripts SQL — política del proyecto per CLAUDE.md).
2. **Aplicar scripts SQL manualmente** en RDS prod (en orden, idempotentes):
   - `add_farm_id_erp_create_to_lote_postura_base.sql`
   - `046_add_traslado_acumulados_lote_postura_levante.sql`
   - `047_add_es_traslado_to_seguimiento_diario.sql`
   - `048_add_traslado_columns_seguimiento_diario.sql`
   - `049_add_updated_by_user_id_seguimiento_diario.sql`
   - `050_add_traslado_acumulados_lote_postura_produccion.sql`
   - `051_add_traslado_columns_produccion_seguimiento.sql` (⚠ requiere ajuste: cambiar nombre de tabla a `seguimiento_diario_produccion_reproductoras` o crear la `produccion_seguimiento`)
   - `052_rename_traslado_columns_per_fase.sql`
3. **Marcar las migraciones EF como aplicadas** en `__EFMigrationsHistory` (INSERT manual) para que cuando se vuelva a habilitar `RunMigrations` no las re-aplique.
4. **Resolver el desajuste de nombre de tabla** (`produccion_seguimiento` vs `seguimiento_diario_produccion_reproductoras`):
   - Opción 4a: cambiar el `ToTable("produccion_seguimiento")` en `ProduccionSeguimientoConfiguration.cs` a `"seguimiento_diario_produccion_reproductoras"`.
   - Opción 4b: renombrar la tabla en RDS (RENAME TABLE seguimiento_diario_produccion_reproductoras TO produccion_seguimiento).
5. Volver a desplegar (`make deploy-backend` o vía GitHub Actions ya arreglado).

### Opción B — Forzar deploy sin migraciones
Solo desactivar `Database__RunMigrations=false` en la TaskDef de producción y re-deploy. La app arrancará pero **los endpoints que usen `farm_id`, `erp_create` o tablas de traslado fallarán en runtime** con 500. No resuelve nada, solo mueve el problema.

### Opción C — Rollback
No tocar nada y dejar producción con el código viejo (`d268d488`). Las features de traslado quedan inaccesibles en prod hasta que se haga el work de Opción A.

---

## 3. Archivos / componentes afectados

- `backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/ProduccionSeguimientoConfiguration.cs` — `ToTable("produccion_seguimiento")` apunta a tabla inexistente en prod.
- `backend/sql/` — scripts SQL idempotentes a aplicar.
- `backend/src/ZooSanMarino.Infrastructure/Migrations/__EFMigrationsHistory` (tabla en BD) — pendiente sincronizar.
- TaskDef ECS `sanmarino-back-task` — env var `Database__RunMigrations`.

---

## 4. Test plan post-fix

1. **Smoke schema**: `SELECT 1 FROM lote_postura_base WHERE farm_id IS NULL LIMIT 1;` debe ejecutar sin error.
2. **Health endpoint**: `GET /health` 200.
3. **Endpoint problemático**: `PUT /api/LotePosturaBase/{id}` con el body del usuario → 200/204 (no 405).
4. **Traslados levante**: `POST /api/Traslados/...` con un movimiento simple → 200.
5. **Traslados producción**: idem en módulo producción.
6. **Verificar deployment ECS**: `aws ecs describe-services ... → deployments[0].rolloutState == COMPLETED` y `taskDefinition == :NN` (la nueva).

---

## 5. Riesgos

- **Producción está corriendo y sirviendo tráfico**. Cualquier cambio de schema debe ser idempotente y reversible.
- Los scripts usan `IF NOT EXISTS` pero el script 051 referencia `produccion_seguimiento` que no existe → requiere ajuste antes de ejecutar.
- Marcar migraciones como aplicadas manualmente en `__EFMigrationsHistory` debe hacerse con el `product_version` correcto (9.0.6) para no romper futuros runs de EF.
