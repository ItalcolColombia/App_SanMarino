# Plan — Traslado de aves desde seguimiento: fechas puras ancladas a MEDIODÍA

**Fecha:** 2026-07-31 · **Tipo:** refactor de correctitud de fechas (sin cambio de cantidades ni lógica de negocio)

## Problema

`TrasladoAvesDesdeSegService` (camino vivo del traslado UI, Levante y Producción) persiste fechas puras a
**MEDIANOCHE**:

- `Fecha = fechaDate` (`dto.FechaSeguimiento.Date`) en las filas nuevas de `seguimiento_diario` (levante) y
  `seguimiento_diario_produccion` — `AplicarSalidaLevanteAsync`, `AplicarIngresoLevanteAsync`,
  `AplicarSalidaProduccionAsync` (también `FechaTraslado`), `AplicarIngresoProduccionAsync`.
- `FechaMovimiento = dto.FechaSeguimiento` en la auditoría `movimiento_aves`.

Con `Npgsql.EnableLegacyTimestampBehavior=true` (Program.cs), un `DateTime` Kind=Unspecified a medianoche se
guarda como `00:00 UTC` y se RELEE convertido a hora local (`19:00-05` del día **ANTERIOR** en Bogotá):
cualquier comparación por día calendario (`.Date` en C#, `::date` en SQL con TZ de sesión) ve otro día. Este
mismo bug duplicó movimientos en la carga masiva (commit `3453b09`, fix en
`MigracionService.MovimientosAves.cs`: ancla a mediodía, patrón `ResolveMovimientoCreatedAt`).

Además el match de fila existente usa comparación **exacta** (`s.Fecha == fechaDate`): solo encuentra filas
con el MISMO instante. Las filas reales del día pueden estar a cualquier hora:

| Escritor | Instante que persiste |
|---|---|
| Traslado UI (este servicio, hoy) | medianoche (`.Date`) |
| Carga masiva Movimientos Aves (post-3453b09) | mediodía (`fecha.Date.AddHours(12)`) |
| fn SQL de migración | `::timestamptz` (TZ de la sesión de BD) |
| Alta manual levante (`SeguimientoDiarioService`) | el instante UTC que mandó el front (`fechaNorm`) |
| Alta producción (`ProduccionService`) | `request.FechaRegistro` crudo (y matchea por `RangoDiaUtc`) |
| Alta producción legacy (`SeguimientoProduccionService`) | medianoche (`dto.Fecha.Date`) |

⇒ hoy un traslado UI en un día que ya tiene fila manual/de migración crea una **segunda fila** en vez de
extenderla, y la idempotencia de la carga masiva (que compara `FechaMovimiento.Date`) no detecta el traslado
UI del mismo día.

## Enfoque

Copiar el patrón ya validado de `MigracionService.MovimientosAves.cs` (`AplicarMovimientoAveMigracionAsync`):

1. **Ancla a mediodía todo lo que se ESCRIBE**: `fechaAncla = FechasPuras.AnclarMediodiaUtc(dto.FechaSeguimiento)`
   para `Fecha` de filas nuevas (levante y producción), `FechaTraslado` (pata salida producción) y
   `FechaMovimiento` de `movimiento_aves`.
2. **Match de fila existente por DÍA CALENDARIO**: rango ±1 día en la consulta
   (`s.Fecha >= dia.AddDays(-1) && s.Fecha < dia.AddDays(2)`) + recorte en memoria
   (`s.Fecha.Date == dia`) — mismo patrón `FechasYaCargadasAsync`. Encuentra filas a medianoche (viejas),
   a mediodía (carga masiva) y a cualquier hora (alta manual). Los demás filtros de cada consulta quedan
   **idénticos** (no se agrega filtro de `ReproductoraId` ni cambia ningún criterio de negocio).
3. `RegistrarCohorteDestinoAsync` no cambia: `FechaIngreso` es `DateOnly` (sin TZ).

## Archivos

- `backend/src/ZooSanMarino.Infrastructure/Services/Funciones/TrasladoAvesDesdeSegService.Traslado.cs` — único archivo de código.
  - Orquestador: `fechaAncla` reemplaza a `fechaDate`; `FechaMovimiento = fechaAncla`.
  - 4 métodos `Aplicar*`: parámetro `fechaDate` → `fechaAncla`; adentro `fechaDia = fechaAncla.Date` para el
    match por rango; filas nuevas con `Fecha = fechaAncla`; `FechaTraslado = fechaAncla`.

Sin cambios de BD/SQL. Sin cambios de DTOs ni contratos.

## Reglas de negocio preservadas

- Cantidades, acumulados, clamps, observaciones y validaciones: byte a byte iguales.
- El traslado sigue EXTENDIENDO la fila del día si existe (ahora la encuentra aunque esté a otra hora — ese
  es el fix, mismo contrato que la carga masiva) y creando una si no.

## Casos de prueba

- `dotnet build` 0 errores / sin advertencias nuevas · `dotnet test` verde (FechasPuras ya tiene tests; no
  hay cálculo puro nuevo — el rango ±1 día replica el patrón inline ya validado de la carga masiva).
- **Smoke local** (receta de la sesión 3453b09: backend propio con `ASPNETCORE_ENVIRONMENT=Development`,
  JWT + X-Secret-Up minteados, lote 115 / LPL 7 empresa Sanmarino, xlsx generado con
  `frontend/node_modules/xlsx`):
  1. Traslado UI (levante→levante) fecha D → verificar `seguimiento_diario.fecha` y
     `movimiento_aves.fecha_movimiento` a **12:00 UTC**.
  2. Reimport carga masiva con hoja Movimientos Aves (misma fecha D, mismas cantidades, Salida) →
     **omitido** por idempotencia (antes duplicaba: la fecha del TSD releída caía en otro día).
  3. Sentido inverso: fila diaria creada por la carga masiva en fecha D′ → traslado UI mismo día →
     **extiende** esa fila (no crea una segunda).
  4. Restaurar BD al snapshot y matar el backend del smoke (sin procesos huérfanos).
