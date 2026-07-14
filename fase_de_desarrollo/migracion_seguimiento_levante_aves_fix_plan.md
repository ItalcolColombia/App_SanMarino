# Plan — Fix: descuento de aves en migración masiva de Seguimiento Levante

## Contexto / diagnóstico

Validé el flujo de migración masiva → Seguimiento Levante contra el flujo manual (alta de un
seguimiento diario de levante desde la pantalla normal) para confirmar que una carga histórica
por Excel actualiza el historial de aves del lote (aves disponibles, mortalidad, etc.) igual que
lo haría el alta manual, registro por registro.

**Tabla destino coincide:** `fn_migracion_seguimiento_levante` (`backend/sql/fn_migracion_seguimiento.sql`)
inserta en `seguimiento_diario_levante`, la misma tabla que usa el alta manual
(`SeguimientoDiarioService`/`SeguimientoLoteLevanteService`), con la misma fórmula de descuento
(mortalidad + selección + error de sexaje). Hasta ahí, coincide.

**Bug A (crítico) — recálculo "desde cero" pisa traslados y movimientos de aves.**
Tras insertar las filas, la función RECALCULA `lote_postura_levante.aves_h_actual/aves_m_actual`
desde cero:
```
aves_h_actual = aves_h_inicial − Σmortalidad − Σselección − Σerror_sexaje
```
(`backend/sql/fn_migracion_seguimiento.sql:68-87`). Esa fórmula **ignora**:
- Traslados entre lotes de Levante (`lote_postura_levante.LevanteTrasladoIngreso/SalidaHembras/Machos`,
  actualizados por [`TrasladoAvesDesdeSegService.cs:182-190`](../backend/src/ZooSanMarino.Infrastructure/Services/TrasladoAvesDesdeSegService.cs)).
- Movimientos del módulo "Movimiento de Aves" (venta/traslado), que ajustan `AvesHActual`/`AvesMActual`
  **directo** sobre `lote_postura_levante` sin dejar ningún rastro en `seguimiento_diario_levante`
  (`MovimientoAvesService.Postura.cs`).

El alta manual, en cambio, nunca recalcula desde cero: **descuenta incrementalmente** sobre el valor
actual (`SeguimientoDiarioService.AplicarDescuentoLevanteAsync`, línea 357-384: `AvesHActual = AvesHActual + signo*delta`).
Esa es la semántica que hay que igualar.

**Impacto real (no cosmético):** `LotePosturaLevanteService.GetResumenCierreAsync` /
`CerrarLoteYCrearProduccionAsync` (líneas 307-348) usan `AvesHActual`/`AvesMActual` como "aves
disponibles" para **cerrar el lote de Levante y crear el de Producción**, capando el número de aves
que pasan a Producción. `ElegiblesHistoricosAsync` no excluye lotes con traslados/movimientos
previos, así que cualquier lote real (no solo uno recién creado) es candidato a migración → el bug
es alcanzable en uso normal.

**Bug B (medio) — filas "solo traslado" bloquean en silencio la fila del Excel para esa fecha.**
El `NOT EXISTS` de deduplicación (`fn_migracion_seguimiento.sql:57-63`) sólo mira
`tipo_seguimiento/lote_id/reproductora/fecha`: si ya existe una fila de traslado puro (creada por
`TrasladoAvesDesdeSegService`, `Ciclo='Traslado'`, `es_traslado=true`, mortalidad/consumo en 0) para
esa fecha, la trata como "ya existe" y **saltea la fila del Excel sin fusionar los datos ni avisar**.
El alta manual sí fusiona en ese caso ("Feature 13", `SeguimientoDiarioService.cs:206-241`).

## Alcance de este fix

Sólo **Levante** (lo que pidió el usuario). `fn_migracion_seguimiento_produccion` tiene el mismo
patrón de recálculo-desde-cero (ignora `ProduccionTrasladoIngreso/Salida`) — se deja fuera de este
fix a propósito; se reporta aparte para decidir si se ataca en una tarea siguiente.

## Cambios

1. **`backend/sql/fn_migracion_seguimiento.sql`** — reescribir `fn_migracion_seguimiento_levante`:
   - Paso 1 (merge): si para `lote_id+fecha` ya existe una fila `es_traslado=true` sin datos
     manuales (mortalidad/sel/error/consumo en 0), completarla con los datos históricos del Excel
     (sin tocar columnas `traslado_*`) — igual criterio que el merge manual.
   - Paso 2 (insert): igual que hoy, sólo si NO existe ninguna fila para esa fecha.
   - Paso 3 (descuento de aves): **incremental**, no recálculo total — restar sobre
     `COALESCE(aves_h_actual, aves_h_inicial, hembras_l, 0)` sólo el delta de mortalidad+sel+error
     de las filas efectivamente insertadas/actualizadas en esta corrida (igual semántica que
     `AplicarDescuentoLevanteAsync`). Así conserva cualquier ajuste previo por traslados o
     movimientos de aves que ya estuviera reflejado en el campo.
   - Mantener idempotencia: reimportar el mismo archivo no debe duplicar ni volver a descontar.
2. **Migración EF** nueva (`CREATE OR REPLACE FUNCTION` vía `dotnet ef migrations add`), mismo
   patrón que las migraciones previas de esta función (`AddFnMigracionSeguimiento`).
3. Sin cambios en C# (`MigracionService.Historicos.cs` sigue mandando el mismo JSON; el contrato de
   la función no cambia — sigue devolviendo `integer` = filas procesadas).

## Casos de prueba (smoke manual contra BD local :5433)

- Lote con traslado previo (ingreso) → migrar histórico de mortalidad → `aves_h_actual` final =
  `aves_h_inicial − mortalidad_migrada` **+ el ingreso por traslado que ya tenía** (no se pierde).
- Reimportar el mismo archivo → sin duplicados, sin descuento doble (`v_insertados` no cuenta las
  filas ya existentes sin cambios).
- Fecha con fila "solo traslado" preexistente + fila del Excel para esa misma fecha con mortalidad →
  la fila se completa (merge), no se saltea, y el descuento de esa mortalidad sí se aplica.
- `dotnet build` 0 errores (no debería tocar C#, es sólo SQL).

## Archivos

- `backend/sql/fn_migracion_seguimiento.sql` (editar `fn_migracion_seguimiento_levante` solamente)
- Nueva migración EF en `backend/src/ZooSanMarino.Infrastructure/Migrations/`

---

## Addendum — mismo fix para Producción (`fn_migracion_seguimiento_produccion`)

Pedido explícito del usuario tras el fix de Levante. Mismo patrón de bug, con dos diferencias
relevantes encontradas al auditar el código real (no asumidas por simetría con Levante):

1. **El manual real es `SeguimientoProduccionService`** (no la rama "produccion" de
   `SeguimientoDiarioService`, que es legacy/no usada por este flujo) — tabla canónica
   `seguimiento_diario_produccion`, mismo patrón de descuento incremental
   (`AplicarDescuentoLppAsync`) y de merge sobre fila traslado-only ("Feature 14") que Levante.
2. **Las filas de traslado de Producción (`TrasladoAvesDesdeSegService`, rama
   Producción↔Producción) NO setean `lote_postura_produccion_id`** en la fila que crean — el
   dedup/merge original de la función de migración matcheaba por ese FK, así que NUNCA
   encontraba esas filas (peor que Levante: ahí sí se encontraban, solo se manejaban mal). Fix:
   matching por `lote_id` crudo + `fecha_registro::date`, no por el FK.
3. Columna de fecha real: `fecha_registro` (no `fecha` como en Levante) — mismo problema de
   representación horaria entre el alta manual (`.Date`, mismo código compartido con Levante en
   `TrasladoAvesDesdeSegService`) y el cast `f.fecha::timestamptz`; mismo fix (`::date`).

Cambios: mismo patrón en `fn_migracion_seguimiento_produccion` (temp tables con `DROP IF EXISTS`
defensivo, merge + insert + descuento incremental) + migración EF nueva.

**Validación:** smoke test SQL (transacción con `ROLLBACK`) sobre el único LPP activo en local
(`lote_postura_produccion_id=6`, `lote_id=14`), simulando un traslado de ingreso de 300 hembras
+ una fila solo-traslado real (sin `lote_postura_produccion_id`, igual que el bug real) — 3 casos
igual que Levante, los 3 correctos. **No se corrió el harness de backend real** (a diferencia de
Levante): el lote de prueba disponible en la BD local no cumple los prerequisitos de elegibilidad
de Producción (Levante cerrado + liquidado) y fabricarlos habría significado mutar datos
compartidos fuera del patrón seguro de transacción-con-rollback — se evitó por guardrail de
seguridad del propio entorno. La ruta C# (`ProcesarSeguimientoProduccionAsync`) es estructuralmente
idéntica a la de Levante (mismo wrapper `EjecutarHistoricoAsync`) ya validada end-to-end, así que el
riesgo residual es bajo, pero queda pendiente si se quiere el mismo nivel de evidencia.
