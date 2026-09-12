# Resultado de levante (500) y reporte contable con varios registros por día

Fecha: 12-sep-2026 · Origen: validación end-to-end de Santa Reyes (bloque V-X1 / V-X2 del tracker).
Aprobado por el usuario: «sí, corregí los dos errores».

## Error 1 — `GET /api/SeguimientoLoteLevante/por-lote/{id}/resultado` = 500 para toda empresa

**Causa (doble).**
1. `SeguimientoLoteLevanteService.Consultas.cs:119` llama `sp_recalcular_seguimiento_levante({loteId})`
   con un `int`; el SP existe solo como `(l_lote_id text)` desde `20260531180558` ⇒ `42883`.
2. El modelo EF dice `produccion_resultado_levante.lote_id` **integer** (`Property<int>("LoteId")`), pero
   la columna es **text** (el SP escribe `l_lote_id`). Arreglando solo (1), el `where r.LoteId == loteId`
   compararía `text = integer` y la lectura de la columna tampoco materializaría.

**Arreglo.**
- Parámetro del SP como texto (`loteId.ToString()`).
- `ProduccionResultadoLevanteConfig`: `LoteId` con `HasConversion<string>()` (la entidad sigue `int`).
- Snapshot: `LoteId` pasa a `HasColumnType("text")`.
- Migración `20260912130000_ProduccionResultadoLevanteLoteIdTexto`: idempotente, convierte a `text`
  solo si la columna no lo es (en local y en prod ya lo es ⇒ no-op). `Down` no revierte el tipo
  (el SP escribe texto; volver a integer rompería el SP).

**Riesgo multiempresa.** El recálculo no corre desde may-2026 en ninguna empresa. Gate: correr el SP
para TODOS los lotes de levante de la BD local en una transacción revertida y contar errores.

## Error 2 — reporte contable semanal pierde registros del mismo día

**Causa.** `ReporteContableService.CalculoSemanal.cs:301-302` hace `FirstOrDefault(lote, fecha)` sobre las
filas CRUDAS de levante y producción. Con 2+ registros el día toma uno (el que devuelva la BD).

**Arreglo.** `Application/Calculos/ReporteContableSeguimientoDiaCalculos` (puro): agrupa por
`(LoteId, Fecha.Date)` ANTES del `FirstOrDefault`. Aditivos suman. En levante los campos son nullables:
si todos los registros del día traen `null`, el agregado queda `null` (preserva el
`levante?.MortalidadHembras ?? produccion?.MortalidadH` del día de transición levante→producción).

**Quién cambia (medido en la BD local).** Solo Demo: 3 días de levante con par manual + fila de traslado
(lotes 120/123/127). Hoy el reporte podía tomar la fila de traslado y mostrar mortalidad 0; con el
arreglo muestra la mortalidad real del manual. El traslado del reporte sale de `movimiento_aves`, no de
esas filas ⇒ no se duplica. Empresas con un registro por día: salida idéntica.

## Flag Santa Reyes por migración (pedido del usuario, 12-sep-2026)

Migración data-only `20260912140000_SeedFlagMultiplesSeguimientosSantaReyes`:
`UPDATE companies SET permite_multiples_seguimientos_diarios = true WHERE name = 'Santa Reyes' AND … IS
DISTINCT FROM true`. Designer = snapshot vigente (sin cambio de modelo). Ordena después de toda la serie
del 12-sep. `Down` vacío. La columna ya existía y `20260905015025` la encendía al crearla; esta migración
lo garantiza en el deploy que trae la fase completa.

Revalidación: clon con el flag de Santa Reyes **apagado** a propósito ⇒ el arranque del backend aplica la
migración ⇒ flag `true` ⇒ repetir el ciclo completo de doble registro (levante + producción: alta, grilla,
resultado, reporte, edición, borrado con reversión exacta) + contraprueba en una empresa sin flag.

## Casos de prueba
- Puro: 1 registro/día ⇒ igual; 2 registros ⇒ suma; todos null ⇒ null; null + valor ⇒ valor;
  días distintos no se mezclan; lotes distintos no se mezclan; producción suma.
- Smoke en clon: `/resultado` del lote 155 = 200 con fila 04/09 (mort 18, sel 1, cons 3.100);
  reporte contable levante 04/09 = 18 / 1 / 3.100 kg; producción 04/09 = 7 / 449 kg.
- `dotnet build` + `dotnet test`; gate del SP en todos los lotes.
