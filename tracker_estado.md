# Tracker — Traslado de aves desde seguimiento: fechas puras a MEDIODÍA + match por día calendario

**Plan:** [`fase_de_desarrollo/traslado_aves_seg_fechas_mediodia_plan.md`](fase_de_desarrollo/traslado_aves_seg_fechas_mediodia_plan.md)
**Fecha:** 2026-07-31

Objetivo: `TrasladoAvesDesdeSegService` deja de escribir fechas puras a medianoche (Npgsql legacy las relee
en el día ANTERIOR en Bogotá) — ancla a mediodía (patrón carga masiva `3453b09`) y matchea la fila diaria
existente por día calendario (rango ±1 día + recorte en memoria) para que traslado UI y carga masiva del
mismo día se detecten mutuamente. Refactor de correctitud de fechas: cero cambios de cantidades/lógica.

## Código

- [x] Orquestador `EjecutarTrasladoDesdeSegAsync`: `fechaAncla = FechasPuras.AnclarMediodiaUtc(...)` reemplaza a `fechaDate`; `FechaMovimiento = fechaAncla`
- [x] `AplicarSalidaLevanteAsync`: match por rango de día calendario (helper `RangoDiaCalendario`) + `Fecha = fechaAncla` en fila nueva
- [x] `AplicarIngresoLevanteAsync`: ídem
- [x] `AplicarSalidaProduccionAsync`: ídem + `FechaTraslado = fechaAncla`
- [x] `AplicarIngresoProduccionAsync`: ídem

## Validación

- [x] `cd backend && dotnet build` — 0 errores, 0 advertencias
- [x] `cd backend && dotnet test` — verde (1.480 Application + 1 Domain)
- [x] Smoke local 1 (backend :5002 Dev, JWT + X-Secret-Up minteados, LPL 7→6 Sanmarino, fecha enviada a MEDIANOCHE UTC): `movimiento_aves.fecha_movimiento` = **12:00 UTC** exacto; `seguimiento_diario_levante.fecha` = mediodía anclado (17:00 UTC = 12:00-05 por la TZ Bogotá de la sesión local; en prod, sesión UTC ⇒ 12:00 UTC — mismo almacenamiento que ya producen las filas de la carga masiva por el mismo mapeo EF; el día calendario queda a salvo en cualquier TZ)
- [x] Smoke local 1b: SEGUNDO traslado el mismo día → EXTIENDE las filas existentes (500/50 acumulado, 0 filas nuevas)
- [x] Smoke local 2: import carga masiva (hoja Movimientos Aves, Salida misma fecha/cantidades que el traslado UI) → **filasOmitidas=1** (la idempotencia detecta el TSD; antes del fix duplicaba) y la fila del día nuevo sí aplica (MGA a 12:00 UTC, LPL descontado exacto)
- [x] Smoke local 3: traslado UI sobre el día con fila creada por la carga masiva → EXTIENDE esa fila (111+50=161 H / 11+5=16 M, count=1 fila del día)
- [x] BD local restaurada al snapshot (LPL 6/7/8 idénticos, segs 114:38/116:144/115:0, movimiento_aves max id 18, cohortes 0, migracion_masiva max 164, espejo producción intacto) + backend del smoke detenido (puerto 5002 libre)
- [x] Commit acotado (sin footer de atribución)
