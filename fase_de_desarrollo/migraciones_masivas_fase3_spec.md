# Fase 3 — Migraciones Masivas: Ventas + Movimiento Aves + Movimiento Huevos (ESPECIFICACIÓN)

> **Estado: DOCUMENTADA, no implementada.** Fases 0-2 están completas y verificadas. Esta es la especificación mapeada para desarrollar la Fase 3 cuando se retome. Sigue el mismo patrón híbrido de Fase 2 (backend valida → función plpgsql inserta + recomputa). **Es la fase más riesgosa: doble conteo de inventario/espejo.** Leer entera antes de escribir código.

## 1. Alcance (decisión validada: Ventas = Aves + Huevos)
| Tipo migración | Qué carga | Tabla destino | TipoMovimiento/Operacion |
|---|---|---|---|
| **Ventas** | Venta de aves de descarte/saca **y** venta de huevos | `movimiento_aves` + `traslado_huevos` | `Venta` |
| **Movimiento Aves** | Traslados / retiros / ajustes (NO venta) | `movimiento_aves` | `Traslado`/`Retiro`/`Ajuste` |
| **Movimiento Huevos** | Traslados / ajustes de huevos (NO venta) | `traslado_huevos` | `Traslado` |

## 2. ⚠️ Riesgo central — DOBLE CONTEO (leer primero)
Los servicios vivos **auto-procesan** en el create (ajustan inventario + seguimiento + espejo). Una carga histórica **NO debe re-procesar** fila por fila. Regla: **insertar el registro en estado `Completado` (con su `numero_*` generado) y recomputar los agregados UNA sola vez al final.** Igual que Fase 2. Nunca llamar a los servicios `CreateAsync`/`Procesar` para históricos.

## 3. Lógica de negocio a estudiar ANTES de escribir SQL
- **Aves:** `Infrastructure/Services/MovimientoAves/Funciones/MovimientoAvesService.Procesamiento.cs`, `.Inventario.cs` (`AplicarMovimientoSalida/Entrada` sobre `inventario_aves`), `.SeguimientoDiario.cs` (cómo una Venta escribe `venta_aves_cantidad`/`venta_aves_motivo` en levante y `SelH`/`MortalidadM` en producción). Entender qué campos deja "Completado".
- **Huevos:** `Infrastructure/Services/TrasladoHuevosService.cs` (`ProcesarTrasladoAsync`) + `IEspejoHuevoProduccionSyncService.RecalcularEspejoHuevoProduccionAsync` (recomputa `*_dinamico = producción − movimientos Completado`). Ver `backend/sql/trigger_espejo_huevo_produccion_seguimiento_diario.sql` y `create_espejo_huevo_produccion.sql`.
- **Contrato numero_*:** `movimiento_aves.numero_movimiento` = `MOV-yyyyMMdd-{id:D6}`; `traslado_huevos.numero_traslado` = `HUE-yyyyMMdd-{id:D6}` (se generan post-insert con el id → la función debe hacer UPDATE del número tras el INSERT, o generarlo con currval).

## 4. Tablas y columnas REALES (esquema live, verificado 2026-07-12)
**`movimiento_aves`**: `id, numero_movimiento, fecha_movimiento, tipo_movimiento, inventario_origen_id, lote_origen_id, granja_origen_id, nucleo_origen_id, galpon_origen_id, inventario_destino_id, lote_destino_id, granja_destino_id, nucleo_destino_id, galpon_destino_id, cantidad_hembras, cantidad_machos, cantidad_mixtas, motivo_movimiento, observaciones, estado, usuario_movimiento_id, usuario_nombre, fecha_procesamiento, fecha_cancelacion, company_id, created_by_user_id, created_at, ..., planta_destino, descripcion, edad_aves, raza, placa, hora_salida, guia_agrocalidad, sellos, ayuno, conductor, total_pollos_galpon, peso_bruto, peso_tara`.
- `tipo_movimiento`: Traslado | Retiro | Venta | Ajuste | Liquidacion. `estado`: Pendiente | Completado | Cancelado.

**`traslado_huevos`**: `id, numero_traslado, fecha_traslado, tipo_operacion, lote_id (varchar!), granja_origen_id, granja_destino_id, lote_destino_id, tipo_destino, motivo, descripcion, cantidad_{limpio,tratado,sucio,deforme,blanco,doble_yema,piso,pequeno,roto,desecho,otro}, estado, usuario_traslado_id, usuario_nombre, fecha_procesamiento, ..., company_id, created_by_user_id, lote_postura_produccion_id`.
- `tipo_operacion`: Venta | Traslado. `lote_id` es varchar (legacy).

**`espejo_huevo_produccion`** (PK `lote_postura_produccion_id`): `company_id, huevo_{tipo}_historico, huevo_{tipo}_dinamico` (para tot, inc, limpio, tratado, sucio, deforme, blanco, doble_yema, piso, pequeno, roto, desecho, otro), `historico_semanal (jsonb)`.
- `*_dinamico` = `*_historico (producción)` − Σ(movimientos Completado). Recomputar tras insertar traslados.

**`inventario_aves`** (stock de aves): `id, lote_id, granja_id, nucleo_id, galpon_id, cantidad_hembras, cantidad_machos, cantidad_mixtas, estado, company_id, ...`.
- Una Venta/Traslado/Retiro de aves **decrementa** este stock. **Decisión abierta**: ¿el histórico recomputa `inventario_aves` o solo inserta el registro de movimiento? (ver §8).

## 5. Funciones plpgsql propuestas (mismo patrón que `fn_migracion_seguimiento_*`)
Registrar vía migración EF (`CREATE OR REPLACE`, idempotente). Todas: `jsonb_to_recordset` → INSERT `WHERE NOT EXISTS` → recompute agregado.

1. **`fn_migracion_movimiento_aves(p_company_id int, p_usuario int, p_rows jsonb)`** → inserta en `movimiento_aves` (estado `Completado`, `fecha_procesamiento = fecha_movimiento`), genera `numero_movimiento` (`MOV-<fecha>-<id>` con UPDATE post-insert usando el id). Dedup sugerido: `(company_id, tipo_movimiento, lote_origen_id, fecha_movimiento, cantidad_*)` o un `numero_movimiento` provisto. **Recompute `inventario_aves`** del/los lote(s) (ver §8).
2. **`fn_migracion_ventas_aves(...)`** → igual pero `tipo_movimiento='Venta'`, sin destino, `motivo_movimiento` = motivo de venta.
3. **`fn_migracion_movimiento_huevos(p_company_id int, p_usuario int, p_rows jsonb)`** → inserta en `traslado_huevos` (`Completado`), genera `numero_traslado`, dedup por `(lote_postura_produccion_id, fecha_traslado, tipo_operacion, cantidades)`. **Recompute `espejo_huevo_produccion.*_dinamico`** de los LPP afectados (mirror del `RecalcularEspejoHuevoProduccionAsync`, o llamar a la lógica del trigger).
4. **`fn_migracion_ventas_huevos(...)`** → igual con `tipo_operacion='Venta'`.
> "Ventas" en el front invoca ventas_aves **y** ventas_huevos según el tipo de fila (columna Tipo=Aves/Huevos en la plantilla), o dos sub-plantillas.

## 6. Elegibilidad (`/elegibles`, resolver en BD)
- **Ventas / Mov. Aves:** lotes con `inventario_aves` (o LPL/LPP) en la empresa. La venta de aves aplica sobre lotes con stock.
- **Ventas huevos / Mov. Huevos:** lotes con `lote_postura_produccion` (deleted_at null) y espejo existente.
- Reusar el patrón `ElegiblesHistoricosAsync` de `MigracionService.Historicos.cs`.

## 7. Plantillas (EPPlus, por lote — reusar helpers de `Plantillas.cs`)
- **Ventas.xlsx**: Fecha, Tipo (Aves|Huevos, dropdown), Cantidad H, Cantidad M *(aves)*, 11 tipos de huevo *(huevos)*, Motivo, Observaciones. Referencias: disponibilidad del lote.
- **MovimientoAves.xlsx**: Fecha, Tipo (Traslado/Retiro/Ajuste, dropdown), Granja/Núcleo/Galpón destino *(si traslado)*, Cantidad H/M/Mixtas, Motivo, Observaciones.
- **MovimientoHuevos.xlsx**: Fecha, Tipo (Traslado/Ajuste), Destino (Granja/Planta), 11 tipos de huevo, Motivo, Observaciones.

## 8. Decisiones abiertas a confirmar con el dominio (Verenice/usuario)
1. **Inventario de aves en históricos:** ¿la carga histórica recomputa `inventario_aves` (stock = inicial ± Σmovimientos) o solo inserta los registros de movimiento sin tocar stock? (Coherente con Fase 2, propuse **no** recomputar stock salvo pedido; pero para aves el stock es central → confirmar.)
2. **Espejo de huevos:** confirmar que recomputar `*_dinamico` una vez (post-carga) es suficiente y equivalente a `RecalcularEspejoHuevoProduccionAsync`.
3. **Ventas unificada vs dos sub-plantillas** (Aves/Huevos en una hoja con columna Tipo, o dos plantillas separadas).
4. **Efecto en seguimiento:** el service de venta de aves escribe `venta_aves_cantidad` en `seguimiento_diario_levante`. ¿El histórico también debe reflejarlo ahí, o basta el registro en `movimiento_aves`?

## 9. Wiring backend (mecánico, mirror de Fase 2)
- Nuevo partial `MigracionService.VentasMovimientos.cs`: `ProcesarVentasAsync`, `ProcesarMovimientoAvesAsync`, `ProcesarMovimientoHuevosAsync` (parse+valida+invoca fn con `SqlQueryRaw`), + elegibilidad + plantillas.
- Extender el `switch` de `MigracionService.Operaciones.cs` (GetElegibles/GenerarPlantilla/Procesar) con los 3 tipos.
- Flip `Disponible=true` para Ventas/MovimientoAves/MovimientoHuevos en `Application/DTOs/Migracion/TipoMigracion.cs`.
- Frontend: **sin cambios** — la UI genérica (filtro jerárquico + panel) ya los maneja al estar `Disponible=true`.

## 10. Checklist de implementación
- [ ] Confirmar las 4 decisiones de §8 con el dominio.
- [ ] Estudiar §3 (procesamiento aves/huevos + espejo sync).
- [ ] Escribir `backend/sql/fn_migracion_movimientos.sql` (aves + huevos, ventas incluidas) + migración EF `CREATE OR REPLACE`.
- [ ] **Verificar por psql** cada función en tx con ROLLBACK (insert + idempotencia + recompute espejo/inventario), como se hizo con levante.
- [ ] `MigracionService.VentasMovimientos.cs` + extender `Operaciones.cs` + flip `Disponible`.
- [ ] Tests xUnit de coerción adicional si aplica; smoke por psql de cada fn.
- [ ] `dotnet build` 0/0 + `dotnet test` verdes + `yarn build`.
- [ ] E2E: descargar plantilla → llenar → importar → verificar `movimiento_aves`/`traslado_huevos` + `espejo_huevo_produccion` recomputado + idempotencia (re-import = 0).

## 11. Referencias de código a reusar
- Patrón función SQL: `backend/sql/fn_migracion_seguimiento.sql` + migración `20260712182227_AddFnMigracionSeguimiento`.
- Patrón backend histórico: `MigracionService.Historicos.cs` (runner `EjecutarHistoricoAsync`, helpers `EnteroNoNeg/DecimalNoNeg/DobleOpc`, `SqlQueryRaw`).
- Coerción: `Application/Calculos/MigracionCalculos.cs`.
- Espejo: `IEspejoHuevoProduccionSyncService`, `create_espejo_huevo_produccion.sql`, `trigger_espejo_huevo_produccion_seguimiento_diario.sql`.
