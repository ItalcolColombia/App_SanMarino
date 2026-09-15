# Plan — CompanyId en la auditoría TSD-* de traslados desde Seguimiento + backfill + gate de Cancelar/Eliminar

**Fecha:** 14-sep-2026 · **Base:** `main` 6e4083b · **Rama:** `claude/agitated-chatelet-fbe87b`

> Port a mano del arreglo escrito el 25-jul-2026 en el worktree `intelligent-volhard-0b73ea`
> (rama `claude/intelligent-volhard-0b73ea`), que **nunca se commiteó**. `main` cambió mucho desde
> entonces (servicio partido en `Funciones/`, cohortes, `ResolverCompanyIdDeGranjaAsync`, D3,
> `LoteDestinoId` en el TSD), así que se re-auditó todo contra el código de hoy. El worktree viejo
> **no se toca**.

## Problema (verificado contra `main` 6e4083b)

`Funciones/TrasladoAvesDesdeSegService.Traslado.cs` crea el `MovimientoAves` de auditoría
(`TSD-*`) **sin `CompanyId` ni `CreatedByUserId`**. `SetAuditFields` solo completa
`CreatedAt/UpdatedAt` ⇒ la fila queda con `company_id = 0`. Consecuencias:

1. **Invisible para `MovimientoAvesService`**: todas sus consultas filtran
   `CompanyId == _currentUser.CompanyId` (listados, GetById, Cancelar, Eliminar…).
2. **Fuera del saldo de producción**: `fn_seguimiento_diario_produccion` suma `movimiento_aves`
   con `m.company_id = lpp.company_id`. Su propio changelog (v2) asume que el traslado TSD «entra
   por `movimiento_aves`, que sí lo audita»: con `company_id = 0` no entra. Un TSD que toca un lote
   en producción (incluido el cross-etapa Levante→Producción de Santa Reyes) no descuenta del
   origen ni acredita al destino en el saldo canónico, y ese saldo se persiste como caché en
   `lote_postura_produccion.aves_*_actual`.

BD local (dump de prod de mediados de agosto): **12 TSD, los 12 con `company_id = 0`**; MOV-* y
MGA-* sí tienen empresa. Los 12 son de lotes de levante (sin LPP) ⇒ localmente el saldo de
producción no cambia; en prod puede haber TSD de producción/cross-etapa posteriores al dump.

## Lectores de `movimiento_aves` y efecto del backfill

| Lector | ¿Filtra empresa? | Efecto de estampar la empresa |
|---|---|---|
| `MovimientoAvesService` (listados, GetById, Search, estadísticas, Cancelar/Eliminar) | Sí | Los TSD pasan a verse en Movimientos (deseado para auditoría) y a ser alcanzables ⇒ gate abajo. |
| `fn_seguimiento_diario_produccion` (y sus consumidores: grilla LPP, reporte diario de costos, caché `aves_*_actual`) | Sí (`lpp.company_id`) | El TSD de un lote en producción empieza a contar en `mov_out`/`mov_in` ⇒ el saldo pasa a ser el correcto. Se mide con `verificar_backfill_company_id_tsd.sql`. |
| `MovimientoAvesService.LoteInfo`, `DisponibilidadLoteService`, `ReporteTecnico*`, `IndicadorEngorde*`, idempotencia de la carga masiva | No | Ninguno. |
| Saldo de levante (`GetMortalidadResumenAsync`, fns semanales) | — (lee `seguimiento_diario`) | Ninguno. |

## Auditoría de reversión con el código de hoy → el gate sigue siendo necesario

Efectos reales de un traslado desde Seguimiento (Levante y Producción, incluido cross-etapa):
acumulados `*TrasladoSalida*` y `AvesActual` del espejo origen; acumulados `*TrasladoIngreso*` y
`AvesActual` del espejo destino; fila SALIDA (origen) e INGRESO (destino) en
`seguimiento_diario`/`seguimiento_diario_produccion`; auditoría `movimiento_aves` (hoy **con**
`LoteDestinoId`); cohorte en `lote_aves_cohortes` ligada por `movimiento_aves_id`. No toca
`inventario_aves`.

**Cancelar** (`CancelarMovimientoAsync` → `DevolverAvesAlInventarioAsync`):
- `inventario_aves`: suma en el origen / resta en el destino **si existe fila** ⇒ efecto espurio
  (el TSD nunca lo tocó).
- `DevolverAvesEnSeguimientoDiarioAsync`: revierte **solo el lado ORIGEN** (fila SALIDA +
  acumulados de salida), eligiendo la tabla por la semana del lote a la fecha, no por la etapa real
  del traslado. **La fila INGRESO del destino y sus `*TrasladoIngreso*` quedan intactos** ⇒ el
  saldo de levante del destino (`GetMortalidadResumenAsync` suma `traslado_ingreso_*`) sigue
  contando las aves que volvieron al origen: **aves duplicadas**.
- `RevertirAvesActualesEnPosturaAsync`: con `LoteDestinoId` presente ahora sí resta en el destino,
  pero con la fase re-determinada al momento de cancelar.
- `AnularCohortesDeMovimientoAsync`: anula la cohorte (esto sí estaría bien).

**Eliminar**: D3 ya bloquea cualquier `Completado` y un TSD nace `Completado`; sin gate propio el
usuario recibe «cancélelo para revertir sus efectos», que lo manda a una ruta que tampoco sirve.

**Cohortes:** el comentario de `RegistrarCohorteDestinoAsync` espera revertir la cohorte «cuando
ese movimiento se elimina», pero con `company_id = 0` eso nunca fue alcanzable y con D3 un TSD
tampoco se puede eliminar. Bloquear Cancelar/Eliminar deja la cohorte viva, que es lo coherente:
las aves no volvieron. **El gate se levanta solo el día que Cancelar revierta el traslado completo**
(SALIDA + INGRESO + acumulados de ambos espejos + cohorte, sin tocar `inventario_aves`).

**Decisión:** bloquear `TSD-*` en **Cancelar** y en **Eliminar** (antes de D3) con un mensaje que
indica la vía correcta: traslado inverso desde Seguimiento Diario. `Procesar` y `Actualizar` ya
exigen `Pendiente`.

## Cambios

### Application
1. `Calculos/MovimientoAvesCalculos.cs` — `const PrefijoTrasladoDesdeSeguimiento = "TSD-"` +
   `EsTrasladoDesdeSeguimiento(string?)` (prefijo ordinal). Lógica pura.

### Infrastructure
2. `Services/Funciones/TrasladoAvesDesdeSegService.Traslado.cs` — el TSD nace con
   `CompanyId = ResolverCompanyIdDeGranjaAsync(origen.GranjaId) ?? companyId efectivo` (mismo
   criterio que la cohorte y que el backfill) y `CreatedByUserId = usuarioId`. El número se arma con
   la constante del prefijo (misma salida).
3. `Services/MovimientoAvesService.cs` (ancla) — `MensajeTrasladoDesdeSegNoReversible`.
4. `Services/MovimientoAves/Funciones/MovimientoAvesService.Procesamiento.cs` — gate en
   `CancelarMovimientoAsync` tras «no encontrado».
5. `Services/MovimientoAves/Funciones/MovimientoAvesService.Crud.cs` — gate en
   `EliminarMovimientoAsync` antes de D3.

Los dos gates devuelven `ResultadoMovimientoDto(Success=false)` ⇒ el controller responde 400 con el
mensaje, como hoy con D3.

### BD — migración EF data-only
6. `Migrations/20260914120000_BackfillCompanyIdMovimientosTsd.cs` (+ `.Designer.cs` clonado del
   snapshot: 4 líneas distintas; snapshot sin tocar). Timestamp posterior a la última de `main`
   (`20260913170000`).
   ```sql
   UPDATE movimiento_aves ma SET company_id = f.company_id
     FROM farms f
    WHERE ma.numero_movimiento LIKE 'TSD-%' AND ma.company_id = 0
      AND ma.granja_origen_id = f.id AND f.company_id > 0;
   ```
   Idempotente por construcción; `Down()` no-op documentado.
7. `backend/sql/verificar_backfill_company_id_tsd.sql` — diagnóstico de solo lectura (prefijo
   `verificar_`, exento del gate de espejos): TSD a estampar / que quedan en 0 / sin lote destino,
   UPDATE + 2.ª pasada y **diferencia fila a fila del saldo de producción de TODOS los LPP**, todo en
   `BEGIN … ROLLBACK`. Correrlo contra un dump fresco de prod antes de desplegar.

### Tests
8. `tests/ZooSanMarino.Application.Tests/MovimientoAvesCalculosTests.cs` — `EsTrasladoDesdeSeguimiento`
   (TSD real y prefijo solo → true; MOV/MGA/MPE, minúsculas, espacio inicial, `XTSD-`, sin guión,
   vacío, null → false) y el valor del prefijo (contrato con la migración y los datos guardados).

## Reglas de negocio
- Empresa del TSD = dueña de la granja del lote ORIGEN (`farms.company_id`); si no resuelve, la
  efectiva de la sesión (el traslado ya exige que los lotes estén en esa empresa).
- Un TSD **no** se cancela ni elimina desde Movimientos; se deshace con un traslado inverso.

## Fuera de alcance (anotado)
- **TSD viejos sin `lote_destino_id`** (10 de 12 en local, anteriores al fix que lo agregó): tras el
  backfill el origen en producción descuenta bien, pero el destino sigue sin acreditar en la fn. Se
  podría derivar de `traslado_lote_contraparte_id` de la fila SALIDA; no se hace acá.
- **MGA-* (carga masiva)** ya tiene empresa y es `Completado` ⇒ hoy Cancelar lo alcanza con el mismo
  defecto de reversión parcial (modelo unilateral). Merece su propio análisis.
- **Traslado inverso y cohortes:** el inverso crea una cohorte en el lote original y deja viva la del
  destino original; «propias del lote» puede recortar a 0. Informativo, no altera saldos.
- `created_by_user_id` de los TSD existentes queda como está (el backfill solo corrige la empresa).

## Validación
- `dotnet build ZooSanMarino.sln` 0 errores / sin warnings nuevos · `dotnet test` verde.
- `node backend/scripts/verificar-sql-llega-por-migracion.js` OK.
- `verificar_backfill_company_id_tsd.sql` en la BD local: estampado esperado, 2.ª pasada `UPDATE 0`,
  0 diferencias de saldo en los LPP sin TSD, `ROLLBACK` (la BD queda igual).
- No se levanta backend: no hay contrato HTTP nuevo y la BD local es compartida entre sesiones.
