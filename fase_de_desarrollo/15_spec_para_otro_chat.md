# 📋 Spec para handoff — Seguimiento Diario Pollo Engorde (Ecuador)

**ID:** 15
**Fecha origen:** 2026-05-28
**Módulo:** Seguimiento Diario Pollo Engorde + función SQL + endpoints Ecuador
**Repo:** `C:\Users\SAN MARINO\Desktop\App_SanMarino\`
**Stack:** .NET 9 + EF Core + PostgreSQL (port 5433 local, RDS en prod) + Angular 20
**BD local:** `sanmarinoapplocal` (schema `public`)

---

## ⚠️ INSTRUCCIONES PARA QUIEN RETOME ESTE PROMPT

**ANTES de programar absolutamente nada**, ejecuta el bloque **"Checklist de validación inicial"** más abajo. Por cada criterio de aceptación, verifica si ya está implementado:

- ✅ **Si TODO el criterio ya está cumplido** → no toques nada, déjalo así y pasa al siguiente. NO re-implementes lo que ya existe.
- ❌ **Si el criterio NO está cumplido o está parcial** → implementa lo que falta siguiendo la guía técnica de cada caso.

Al final del trabajo, vuelve a correr la checklist completa y confirma que TODOS los criterios pasan.

> Esta sesión cierra **5 fixes encadenados** que ya están implementados en este repo. Es muy probable que al revisar encuentres que la mayoría (o todos) ya están listos. Verifica antes, implementa solo si falta.

---

## Contexto general

El módulo `seguimiento_diario_aves_engorde` (tabla en BD) y su endpoint Ecuador `POST/PUT/DELETE /api/SeguimientoAvesEngordeEcuador` presentaban 5 problemas encadenados sobre el cálculo del **saldo de alimento** y la **afectación del inventario**.

### Archivos clave del módulo

| Archivo | Rol |
|---|---|
| `backend/sql/fn_seguimiento_diario_engorde.sql` | Función PostgreSQL que devuelve la tabla diaria |
| `backend/sql/migrate_recalcular_saldo_alimento_engorde.sql` | Script standalone para recalcular saldos persistidos |
| `backend/src/ZooSanMarino.Infrastructure/Migrations/20260528212753_FixFnSeguimientoEngordeYRecalcularSaldosMasivo.cs` | Migración EF Core que aplica función SQL + UPDATE masivo al deploy |
| `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngordeService.cs` | Servicio Colombia |
| `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngordeEcuadorService.cs` | Servicio Ecuador (el del fix #11) |
| `backend/src/ZooSanMarino.Application/DTOs/SeguimientoDiarioTablaFilaDto.cs` | DTO mapeado por `SqlQueryRaw` |
| `frontend/src/app/features/aves-engorde/services/seguimiento-aves-engorde.service.ts` | Service Angular |
| `frontend/src/app/features/aves-engorde/pages/tabs-principal-engorde/` | Componente tabla diaria |

### Tablas clave

- `seguimiento_diario_aves_engorde` — registros diarios por lote
- `lote_ave_engorde` — info del lote (`granja_id`, `nucleo_id`, `galpon_id`, `fecha_encaset`, `estado_operativo_lote`)
- `lote_registro_historico_unificado` — eventos (`INV_INGRESO`, `INV_TRASLADO_ENTRADA/SALIDA`, `INV_CONSUMO`, `VENTA_AVES`) scope: galpón
- `inventario_gestion_stock` — stock físico por (farm, nucleo, galpon, item_inventario_ecuador_id)

---

## 🔴 CASO 1 — Función SQL `fn_seguimiento_diario_engorde` con saldos incorrectos

### Requerimiento

La función `fn_seguimiento_diario_engorde(p_lote_id INT)` devuelve la tabla diaria que ve el usuario. Tiene 3 bugs:

1. **El primer ingreso de alimento del primer día de seguimiento no aparece** — la columna `ingreso_alimento_kg` está en 0 aunque haya un `INV_INGRESO` ese día.
2. **`consumo_bodega_kg` no refleja el consumo real** del seguimiento — viene de `INV_CONSUMO` del histórico que es ambiguo (puede pertenecer a otros lotes del mismo galpón).
3. **Saldos descuadrados al final** cuando hay `INV_TRASLADO_SALIDA` con `cantidad_kg` negativa.

### Causas raíz

- `rango_seg` devuelve `MIN/MAX(fecha)` como `TIMESTAMPTZ` con hora 12:00. La comparación `DATE(h.fecha_operacion) >= rs.fecha_min` interpreta DATE como 00:00 → falso para el mismo día.
- `consumo_bodega_kg` se alimentaba de `SUM(INV_CONSUMO)` del histórico unificado, que filtra por galpón (no por lote).
- Backend (`SeguimientoAvesEngordeService.cs:406`) usa `delta = -Math.Abs(kg)` para `INV_TRASLADO_SALIDA`. SQL sumaba el valor directo. Con datos sucios (un registro con kg negativo) los saldos divergen.

### Criterios de aceptación

- [ ] **CA1.1** `rango_seg` castea con `::DATE`: `MIN(s.fecha)::DATE AS fecha_min, MAX(s.fecha)::DATE AS fecha_max`.
- [ ] **CA1.2** En CTE `hist_alimento`, `INV_TRASLADO_SALIDA` usa `ABS(COALESCE(h.cantidad_kg, 0))`.
- [ ] **CA1.3** Nuevo CTE `apertura_alimento` que calcule el saldo de movimientos pre-rango_seg con: ingresos y traslados-entrada positivos, traslados-salida negativos con `ABS`.
- [ ] **CA1.4** En el SELECT FINAL, `saldo_alimento_kg` se calcula dinámicamente con: `GREATEST(0, apertura_kg + SUM(ingreso + tras_ent - tras_sal - consumo_dia) OVER w_ord)::FLOAT8` (no usar el valor persistido).
- [ ] **CA1.5** En el SELECT FINAL, `consumo_bodega_kg` se mapea a `se.consumo_dia_kg` (el consumo del seguimiento, no INV_CONSUMO del histórico).
- [ ] **CA1.6** **Test BD:** `SELECT * FROM fn_seguimiento_diario_engorde(5)` y verificar que el día `2026-02-27` muestre `ingreso_alimento_kg = 5000` con `documento = '005-001-000053977'`.
- [ ] **CA1.7** **Test BD:** `SELECT * FROM fn_seguimiento_diario_engorde(32)` y verificar que el día `2025-12-30` muestre `ingreso_alimento_kg = 6000`.

---

## 🔴 CASO 2 — `SeguimientoAvesEngordeEcuadorService` NO descuenta inventario

### Requerimiento (el caso más crítico del día)

El endpoint `POST /api/SeguimientoAvesEngordeEcuador` debe replicar el comportamiento del servicio Colombia (`SeguimientoAvesEngordeService`): al crear un seguimiento diario con consumo de alimento debe:

1. Validar que el lote existe y NO está cerrado (`estado_operativo_lote != 'Cerrado'`).
2. Descontar el alimento del `inventario_gestion_stock`.
3. Registrar un `INV_CONSUMO` en `lote_registro_historico_unificado` con referencia trazable: `"Seguimiento aves engorde #{id} {yyyy-MM-dd}"`.
4. Recalcular y persistir `saldo_alimento_kg` en TODAS las filas del lote.
5. Registrar retiro de aves (mortalidad + selección + error sexaje) vía `IMovimientoAvesService.RegistrarRetiroDesdeSeguimientoAsync`.
6. Construir snapshot `historico_consumo_alimento` con `saldo_inicial`, `consumo`, `saldo_final` por ítem.

Para `PUT`: aplicar diff entre metadata antigua y nueva. Si cantidad subió → consumo adicional con ref `"...(ajuste)"`. Si bajó → devolución `RegistrarIngresoAsync` con ref `"...(devolución)"`.

Para `DELETE`: (a) devolver el alimento al inventario con ref `"...(devolución por eliminación)"`, (b) marcar `anulado=true` en los `INV_CONSUMO` huérfanos del seguimiento eliminado, (c) devolver las aves al `inventario_aves`, (d) recalcular saldo del lote.

### Criterios de aceptación

- [ ] **CA2.1** Constructor del servicio inyecta: `IAlimentoNutricionProvider`, `IGramajeProvider`, `IMovimientoAvesService`, `IInventarioGestionService?` (las 4 ya registradas en `Program.cs`).
- [ ] **CA2.2** `CreateAsync` valida lote existente y no cerrado → en caso contrario lanza `InvalidOperationException` con mensaje claro (el controller lo mapea a HTTP 400).
- [ ] **CA2.3** `CreateAsync` parsea `dto.Metadata.itemsHembras`/`itemsMachos`, extrae `itemInventarioEcuadorId` + `cantidad` + `unidad`, llama `_inventarioGestionService.RegistrarConsumoAsync(...)` por cada ítem con `cantidad > 0`.
- [ ] **CA2.4** `CreateAsync` llama `RecalcularSaldoAlimentoPorLoteAsync(loteId, companyId)` después de persistir.
- [ ] **CA2.5** `UpdateAsync` capta `oldByItemId = ParseMetadataItemsToKg(ent.Metadata.RootElement)` antes de modificar; compara con `newByItemId`; aplica diff con `RegistrarConsumoAsync` (subió) o `RegistrarIngresoAsync` (bajó) con sufijo `(ajuste)` o `(devolución)`.
- [ ] **CA2.6** `DeleteAsync` antes de borrar: itera metadata, llama `RegistrarIngresoAsync` con ref `"Seguimiento aves engorde #{id} (devolución por eliminación)"`; luego marca `anulado=true` en todos los `INV_CONSUMO` huérfanos con `ref LIKE 'Seguimiento aves engorde #{id}%'`.
- [ ] **CA2.7** Todos los `try { ... } catch (Exception ex) { Console.WriteLine(...) }` deben dejar el seguimiento persistido aunque falle el inventario (resiliente).
- [ ] **CA2.8** **Test E2E:** `POST /api/SeguimientoAvesEngordeEcuador` con `loteId=<lote-abierto>, itemsHembras: [{ itemInventarioEcuadorId: X, cantidad: 100, unidad: "kg" }]` → respuesta HTTP 201; verificar BD: (a) fila en `seguimiento_diario_aves_engorde` con `saldo_alimento_kg` poblado, (b) fila en `lote_registro_historico_unificado` con `tipo_evento='INV_CONSUMO'`, `cantidad_kg=100`, (c) `inventario_gestion_stock.quantity` reducido en 100.
- [ ] **CA2.9** **Test E2E:** `POST` con `loteId=<lote-cerrado>` → HTTP 400 con mensaje "El lote está cerrado (liquidado). No se pueden agregar registros diarios."
- [ ] **CA2.10** **Test E2E:** `DELETE /api/SeguimientoAvesEngordeEcuador/{id}` → HTTP 204; verificar BD: (a) `INV_CONSUMO.anulado=true`, (b) nuevo `INV_INGRESO` con `referencia LIKE '%(devolución por eliminación)%'`, (c) stock restituido al valor original, (d) fila eliminada.

---

## 🔴 CASO 3 — Saldo de apertura hereda inventario del lote anterior

### Requerimiento

Cuando un galpón se reutiliza para un lote nuevo, el saldo del primer día del seguimiento NO debe incluir el alimento residual del lote anterior. Regla del negocio: al iniciar un lote el galpón se considera limpio (lo que quedó del lote previo se trasladó o descartó).

**Evidencia:** Lote `75` (= "2602" en `lote_nombre`) tiene `fecha_encaset = 2026-04-29`, primer seguimiento `2026-05-01` con ingreso de 5,600 kg y consumo de 320 kg. Saldo del día 1 debe ser **5,280 kg** (5,600 − 320), pero la función reportaba **137,557 kg** porque la apertura sumaba 132,277 kg de movimientos del galpón `G0042` desde 2025-12-27 (lote anterior).

### Causas raíz

- CTE `apertura_alimento` sumaba TODOS los movimientos pre-`fecha_min` del galpón sin filtrar por `fecha_encaset` del lote actual.
- En backend, `ComputeSaldoAperturaGalponAntesPrimerSeguimiento` igual.
- **Bug colateral:** el cálculo dinámico del saldo en SQL hacía `SUM(se.ingreso_alimento_kg) OVER w_ord`, pero `se.ingreso_alimento_kg` viene del `LEFT JOIN hist_alimento` que solo tiene fechas con seguimiento. Movimientos en días sin seguimiento se perdían.

### Criterios de aceptación

- [ ] **CA3.1** SQL: `apertura_alimento` agrega condición `AND (li.fecha_encaset IS NULL OR DATE(h.fecha_operacion) >= li.fecha_encaset::DATE)`.
- [ ] **CA3.2** Backend: `ComputeSaldoAperturaGalponAntesPrimerSeguimiento` acepta parámetro nuevo `DateTime? fechaEncaset = null`; si está presente, filtra movimientos con `ymd < FormatYmd(fechaEncaset.Value.Date)`. Aplicar en ambos servicios (Colombia + Ecuador).
- [ ] **CA3.3** Backend: call sites de `ComputeSaldoAperturaGalponAntesPrimerSeguimiento` pasan `lote.FechaEncaset`.
- [ ] **CA3.4** SQL: el cálculo del saldo dinámico en SELECT final usa subconsulta correlacionada sobre `hist_alimento`:
  ```sql
  GREATEST(0,
    (SELECT apertura_kg FROM apertura_alimento)
    + COALESCE((SELECT SUM(ha2.ingreso_kg + ha2.traslado_entrada_kg - ha2.traslado_salida_kg)
                FROM hist_alimento ha2 WHERE ha2.fecha <= se.fecha), 0)
    - SUM(se.consumo_dia_kg) OVER w_ord
  )::FLOAT8 AS saldo_alimento_kg
  ```
- [ ] **CA3.5** **Test BD:** `SELECT fecha, saldo_alimento_kg FROM fn_seguimiento_diario_engorde(75) ORDER BY fecha LIMIT 1` → `(2026-05-01, 5280)`.
- [ ] **CA3.6** **Test BD regresión:** lotes que NO heredaban deben seguir con los mismos saldos. Validar con `WITH fn AS (SELECT seg_id, saldo_alimento_kg AS fn_saldo FROM fn_seguimiento_diario_engorde(32)), p AS (SELECT id, saldo_alimento_kg::FLOAT8 AS p_saldo FROM seguimiento_diario_aves_engorde WHERE lote_ave_engorde_id = 32) SELECT COUNT(*) FILTER (WHERE ABS(fn_saldo - p_saldo) < 0.01) AS coinciden, COUNT(*) AS total FROM fn JOIN p ON p.id = fn.seg_id` → `coinciden = total`.

---

## 🔴 CASO 4 — Migración masiva de saldos persistidos

### Requerimiento

La columna `seguimiento_diario_aves_engorde.saldo_alimento_kg` tiene valores históricos calculados con la lógica vieja (con herencia indebida). Necesitamos un script SQL idempotente que:

1. Cree una tabla de backup con el estado actual.
2. Recalcule todos los saldos usando `fn_seguimiento_diario_engorde` como fuente de verdad.
3. Solo actualice filas donde difiere (idempotente).
4. Reporte resumen de cambios.
5. Valide que post-migración no quedan discrepancias.

El script debe poder re-ejecutarse sin efectos secundarios y debe permitir rollback restaurando desde la tabla de backup.

### Criterios de aceptación

- [ ] **CA4.1** Script `backend/sql/migrate_recalcular_saldo_alimento_engorde.sql` envuelto en `BEGIN; ... COMMIT;`.
- [ ] **CA4.2** Crea tabla persistente `_migracion_saldo_alimento_2026_05_28` con `seg_id, lote_id, fecha, saldo_antes, updated_at_antes, migrated_at`.
- [ ] **CA4.3** Solo procesa lotes con `deleted_at IS NULL` (usa `CROSS JOIN LATERAL fn_seguimiento_diario_engorde(l.lote_ave_engorde_id)`).
- [ ] **CA4.4** UPDATE solo actualiza filas con `p.saldo_alimento_kg IS NULL OR ABS(p.saldo_alimento_kg - n.saldo_nuevo) >= 0.001`.
- [ ] **CA4.5** Reporte final muestra: filas actualizadas, lotes afectados, max delta, validación post-migración.
- [ ] **CA4.6** **Test ejecución:** primera corrida actualiza N filas; segunda corrida actualiza 0 filas (idempotente).
- [ ] **CA4.7** **Test post-migración:** ejecutar para todos los lotes activos `SELECT COUNT(*) FILTER (WHERE ABS(fn.saldo_alimento_kg - p.saldo_alimento_kg::FLOAT8) >= 0.001) AS divergen FROM fn_seguimiento_diario_engorde(l.lote_ave_engorde_id) fn JOIN seguimiento_diario_aves_engorde p ON p.id = fn.seg_id` → `divergen = 0`.

---

## 🔴 CASO 5 — Deploy automático AWS + mostrar movs sin seguimiento

### Requerimiento

Dos cosas:

**5A) Deploy AWS:** Empaquetar todo lo anterior en una migración EF Core que se aplique automáticamente al arrancar el back en AWS (RDS prod). `RunMigrations=true` está activo por defecto en Production (ver `Program.cs:761`). Cuando se suba el back a producción, la función SQL debe actualizarse automáticamente.

**5B) Movs sin seguimiento:** La tabla diaria del frontend actualmente solo muestra fechas con seguimiento. Si el usuario registró un `INV_INGRESO` el 5 de mayo sin haber creado seguimiento ese día, la fila no aparece. **Cambio requerido:** mostrar también esas fechas con los campos del seguimiento en NULL/0 pero el movimiento (ingreso, traslado, documento, venta) visible. Para lotes Abiertos también deben verse movimientos post-último-seguimiento.

### Criterios de aceptación

- [ ] **CA5.1** Migración EF Core `Migrations/20260528212753_FixFnSeguimientoEngordeYRecalcularSaldosMasivo.cs`. En el `Up()`:
  1. `migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_seguimiento_diario_engorde(INT);", suppressTransaction: true);` ← **DROP explícito antes del CREATE** para garantizar limpieza ante cambios de signatura.
  2. `migrationBuilder.Sql(FN_V4_SQL, suppressTransaction: true);` ← embebe la función SQL v4 completa.
  3. `migrationBuilder.Sql(MIGRACION_MASIVA_SQL, suppressTransaction: true);` ← embebe snapshot + UPDATE masivo.

  En el `Down()`: restaurar saldos desde snapshot y volver a hacer DROP de la función.
- [ ] **CA5.2** En la migración, si EF Core auto-generó `AddColumn` por desfase del `ModelSnapshot`, ELIMINAR esos `AddColumn` (las columnas ya existen en prod). Dejar solo `migrationBuilder.Sql(...)` con `suppressTransaction: true`.
- [ ] **CA5.3** El UPDATE masivo dentro de la migración debe ser idempotente: snapshot con `INSERT ... WHERE NOT EXISTS` (no `ON CONFLICT` para evitar requerir PK en BD ya existente).
- [ ] **CA5.4** SQL: nuevo CTE `fechas_universo` = `UNION ALL` de (a) fechas con seguimiento y (b) fechas con movimientos donde NO existe un seguimiento ese día (`NOT EXISTS` para evitar duplicados).
- [ ] **CA5.5** SQL: `rango_seg.fecha_max` retorna `NULL` para lotes ABIERTOS (no aplica tope superior) y `MAX(s.fecha)::DATE` para lotes CERRADOS.
- [ ] **CA5.6** SQL: `seg_enriquecido` parte de `fechas_universo` (no de `seguimiento_diario_aves_engorde`). Los campos `s.*` quedan como `LEFT JOIN seguimiento_diario_aves_engorde s ON s.id = fu.seg_id`.
- [ ] **CA5.7** SQL: window functions usan `ORDER BY se.fecha, COALESCE(se.seg_id, 0)` para orden estable cuando hay seg_id NULL.
- [ ] **CA5.8** DTO C#: `public long? SegId { get; set; }` (no `long`).
- [ ] **CA5.9** Frontend TS interface: `segId: number | null` (con doc-comment explicando).
- [ ] **CA5.10** Frontend `trackByDiarioFila` usa `f.segId ?? \`mov-${f.fecha}\`` como fallback único.
- [ ] **CA5.11** Frontend funciones `onViewDetailById`/`onEditById`/`onDelete` aceptan `number | null` con guard `if (id == null) return;`.
- [ ] **CA5.12** Frontend HTML: `<div class="btn-group" *ngIf="f.segId != null; else movSinSeg">...</div>` + `<ng-template #movSinSeg><span class="badge-mov">📦 mov.</span></ng-template>`.
- [ ] **CA5.13** **Test BD:** `SELECT COUNT(*) FILTER (WHERE seg_id IS NULL) FROM fn_seguimiento_diario_engorde(12)` → debe ser > 0 si el lote tiene movs en días sin seguimiento.
- [ ] **CA5.14** **Test E2E:** `GET /api/SeguimientoAvesEngordeEcuador/por-lote/12/tabla-diaria` → HTTP 200 con filas donde algunas tienen `"segId": null`.
- [ ] **CA5.15** **Test deploy local:** `dotnet ef database update` aplica la migración sin error; re-ejecutar → "Database is already up to date" (idempotente).

---

## ✅ Checklist de validación inicial (ejecutar ANTES de tocar código)

### Paso 1 — Verificar archivos del repo

```bash
# Desde la raíz del repo (Desktop\App_SanMarino):
ls backend/sql/fn_seguimiento_diario_engorde.sql                                                    # → debe existir
ls backend/sql/migrate_recalcular_saldo_alimento_engorde.sql                                        # → debe existir
ls backend/src/ZooSanMarino.Infrastructure/Migrations/20260528212753_*.cs                           # → debe existir
grep -n "long? SegId" backend/src/ZooSanMarino.Application/DTOs/SeguimientoDiarioTablaFilaDto.cs    # → debe haber 1 match
grep -n "segId: number | null" frontend/src/app/features/aves-engorde/services/*.ts                 # → debe haber 1 match
grep -n "fechas_universo" backend/sql/fn_seguimiento_diario_engorde.sql                             # → debe haber 1+ match
grep -n "fecha_encaset::DATE" backend/sql/fn_seguimiento_diario_engorde.sql                         # → debe haber 1+ match
grep -n "DROP FUNCTION IF EXISTS fn_seguimiento_diario_engorde" backend/src/ZooSanMarino.Infrastructure/Migrations/20260528212753_*.cs  # → debe haber 1+ match
```

### Paso 2 — Verificar BD local

```bash
PGPASSWORD=123456789 psql -h localhost -p 5433 -U postgres -d sanmarinoapplocal -c "
SELECT migration_id FROM \"__EFMigrationsHistory\"
WHERE migration_id = '20260528212753_FixFnSeguimientoEngordeYRecalcularSaldosMasivo';
"
# → debe devolver 1 fila (migración aplicada)

PGPASSWORD=123456789 psql -h localhost -p 5433 -U postgres -d sanmarinoapplocal -c "
SELECT seg_id, fecha, saldo_alimento_kg
FROM fn_seguimiento_diario_engorde(75) ORDER BY fecha LIMIT 1;
"
# → debe devolver (3234, 2026-05-01, 5280)  → CA3.5

PGPASSWORD=123456789 psql -h localhost -p 5433 -U postgres -d sanmarinoapplocal -c "
WITH t AS (
  SELECT fn.seg_id, fn.saldo_alimento_kg AS fn_s, p.saldo_alimento_kg::FLOAT8 AS p_s
  FROM lote_ave_engorde l
  CROSS JOIN LATERAL fn_seguimiento_diario_engorde(l.lote_ave_engorde_id) fn
  JOIN seguimiento_diario_aves_engorde p ON p.id = fn.seg_id
  WHERE l.deleted_at IS NULL AND fn.seg_id IS NOT NULL
)
SELECT COUNT(*) AS total,
       COUNT(*) FILTER (WHERE ABS(fn_s - p_s) < 0.001) AS coinciden,
       MAX(ABS(fn_s - p_s)) AS max_diff FROM t;
"
# → coinciden = total, max_diff < 0.001  → CA4.7
```

### Paso 3 — Build y type-check

```bash
cd backend && dotnet build src/ZooSanMarino.API/ZooSanMarino.API.csproj
# → 0 errors

cd frontend && yarn tsc --noEmit -p tsconfig.app.json
# → 0 errors
```

### Paso 4 — Test E2E con backend corriendo

```bash
cd backend/src/ZooSanMarino.API && ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build &

# Esperar a que arranque (≈15s), luego:
TOKEN="<JWT-ECUADOR>"
SECRET="<x-secret-up>"

# CA5.14:
curl -X GET "http://localhost:5002/api/SeguimientoAvesEngordeEcuador/por-lote/12/tabla-diaria" \
  -H "Authorization: Bearer $TOKEN" \
  -H "x-active-company-id: 3" \
  -H "x-active-pais: 2" \
  -H "x-secret-up: $SECRET" -w "\n%{http_code}\n"
# → HTTP 200, JSON con filas, algunas con "segId": null

# CA2.9:
curl -X POST "http://localhost:5002/api/SeguimientoAvesEngordeEcuador" \
  -H "Authorization: Bearer $TOKEN" -H "x-active-company-id: 3" -H "x-active-pais: 2" -H "x-secret-up: $SECRET" \
  -H "Content-Type: application/json" \
  -d '{"loteId": 14, "fechaRegistro":"2026-05-30T12:00:00", "mortalidadHembras":0, "mortalidadMachos":0, "selH":0, "selM":0, "errorSexajeHembras":0, "errorSexajeMachos":0, "tipoAlimento":"INI", "consumoKgHembras":1, "ciclo":"Normal"}' \
  -w "\n%{http_code}\n"
# → HTTP 400 (lote 14 está cerrado)
```

### Interpretación del resultado de la checklist

- **Todos los pasos pasan → trabajo COMPLETO**. No tocar nada. Reportar al usuario "Validación completa, todo implementado correctamente, no se requiere desarrollo adicional".
- **Algunos pasos fallan → trabajo PARCIAL**. Identificar qué caso (1-5) tiene el criterio fallido. Implementar siguiendo la guía técnica de ese caso. Re-correr la checklist al final.

---

## Comandos de aplicación (si hay que implementar desde cero)

```bash
# 1. Aplicar la función SQL en local (test fuera de migración)
PGPASSWORD=123456789 psql -h localhost -p 5433 -U postgres -d sanmarinoapplocal \
  -f backend/sql/fn_seguimiento_diario_engorde.sql

# 2. Aplicar migración EF Core (esto hace DROP + CREATE de la función + UPDATE masivo)
cd backend/src/ZooSanMarino.API
dotnet ef database update \
  --project ../ZooSanMarino.Infrastructure --startup-project . \
  --context ZooSanMarinoContext

# 3. Re-aplicar (idempotente):
dotnet ef database update ...
# → "Database is already up to date"
```

### En producción (AWS)

`Database__RunMigrations=true` está activo en la TaskDef de ECS prod. Al hacer deploy del nuevo container:

1. EF Core ejecuta `ctx.Database.MigrateAsync()` al arrancar.
2. Aplica la migración `20260528212753_FixFnSeguimientoEngordeYRecalcularSaldosMasivo`.
3. Esa migración hace:
   - `DROP FUNCTION IF EXISTS fn_seguimiento_diario_engorde(INT);`
   - `CREATE OR REPLACE FUNCTION fn_seguimiento_diario_engorde(...)` con la v4
   - Snapshot persistente `_migracion_saldo_alimento_2026_05_28`
   - `UPDATE seguimiento_diario_aves_engorde SET saldo_alimento_kg = ...` para todos los lotes activos
4. La aplicación queda lista con la lógica nueva.

**No requiere intervención manual** ni scripts SQL aparte. Todo se ejecuta automáticamente al deploy.

---

## Definición de "DONE" para esta sesión

- [ ] Checklist de validación inicial pasa completa (paso 1, 2, 3, 4)
- [ ] Build backend: 0 errores
- [ ] Type-check frontend: 0 errores
- [ ] Migración EF Core aplica sin error y es idempotente
- [ ] Test E2E POST/PUT/DELETE en lote abierto: 201/200/204 con afectación visible en BD
- [ ] Test E2E POST en lote cerrado: 400
- [ ] Función SQL devuelve filas para movs sin seguimiento (`seg_id IS NULL` cuando aplica)
- [ ] 0 divergencias entre `saldo_alimento_kg` persistido y la función SQL
- [ ] Tabla `_migracion_saldo_alimento_2026_05_28` existe con backup (para rollback)
- [ ] Migración con DROP + CREATE explícito de la función SQL

---

## Notas finales para el chat receptor

- El proyecto **activo** vive en `C:\Users\SAN MARINO\Desktop\App_SanMarino\`. NO trabajar en otra carpeta.
- Si encuentras una carpeta gemela en `Documents\App_SanMarino_intalcol\App_SanMarino\` con `_OBSOLETO_USAR_DESKTOP.md` → ignorar, esa es la versión vieja.
- BD local: `localhost:5433`, user `postgres`, password `123456789`, db `sanmarinoapplocal`.
- Los planes individuales detallados (10–14) están en `fase_de_desarrollo/` como referencia técnica adicional.
