# Fix: fechas muestran un día menos — módulo pollo engorde (lotes, reproductoras y seguimientos)

**Fecha:** 2026-07-21 · **Reporte:** al crear lote pollo engorde / lotes reproductora y al registrar seguimiento (engorde y reproductora), la fecha mostrada en tablas/detalle es un día menor a la digitada.

---

## 1. Causa raíz (auditada en código)

Cadena del corrimiento (usuario en UTC-5 · Colombia/Ecuador/Panamá):

| Etapa | Qué pasa | Evidencia |
|---|---|---|
| Envío (front) | El `YYYY-MM-DD` del `<input type="date">` se convierte con `new Date(ymd).toISOString()` o `new Date(ymd+'T00:00:00Z')` → **medianoche UTC** | `lote-engorde-list.component.ts:716-717` · `lote-reproductora-ave-engorde.service.ts:134` (`toIso`) · `modal-seguimiento-reproductora.component.ts:435` |
| BD | Columnas `timestamp with time zone` (snapshot EF) + `Npgsql.EnableLegacyTimestampBehavior=true` (`Program.cs:127`) → al LEER, Npgsql devuelve `Kind=Local` (tz del server) y la API serializa **con offset** (`+00:00` en ECS, `-05:00` en dev local) | snapshot líneas 3231-3233, 4689-4691 |
| Display (front) | `\| date:'dd/MM/yyyy'` (DatePipe) y `toLocaleDateString('es')` convierten a **zona local**: medianoche UTC − 5h = 19:00 del día anterior → **-1 día** | `lote-engorde-list.component.html:207,654` · `lote-reproductora...list.component.ts:482` · `seguimiento-diario-lote-reproductora-list.component.html:271,432` |

Además hay extractores literales `slice(0,10)`/`substring(0,10)` sobre strings **con offset** que fallan contra el backend local (`-05:00`): `lote-reproductora...list.component.ts:364`, `seguimiento-diario-lote-reproductora-list.component.ts:155`, `modal-seguimiento-engorde.component.ts:601`, `toYMD`/`computeDefaultFecha` de `engorde-comun/funciones/fecha.funcion.ts` (rama ISO extrae fecha local).

El modal de seguimiento engorde **ya** envía anclado a mediodía (`ymdToIsoAtNoon`) → esas filas se ven bien; el resto de flujos no.

## 2. Regla canónica del fix ("fecha pura")

- **Enviar**: `YYYY-MM-DD` → **mediodía UTC** (`YYYY-MM-DDT12:00:00Z`) con el helper **ya existente** `ymdToIsoUtcNoon` de `shared/utils/format.ts`. A ±12 h de cualquier medianoche, el día no cruza en ninguna tz relevante.
- **Interpretar/mostrar**: extraer la fecha intencional **sin conversión a zona local**: string sin tz → literal (primeros 10 chars); string con `Z`/offset → fecha **UTC** del instante (`toISOString().slice(0,10)`). Esto renderiza bien los datos viejos (medianoche UTC) **y** los nuevos (mediodía), tanto contra prod (server UTC) como dev local (server Bogotá). **No requiere backfill de BD.**
- **Backend**: anclar esos campos a mediodía UTC al escribir (defensa ante bundles front cacheados) sin tocar los demás usos.

## 3. Cambios

### Frontend (Angular)
1. `shared/utils/format.ts`: agregar `ymdSinTz(iso)` (extractor intencional) y `fechaCortaSinTz(iso)` (variante de `fechaCorta` sin corrimiento). **No** se modifica `fechaCorta` existente (regla design system: variantes con nombre propio).
2. `lote-engorde/components/lote-engorde-list/`:
   - `.ts`: enviar `fechaEncaset/fechaAlistamiento` con `ymdToIsoUtcNoon` (líneas 716-717); poblar form de edición con `ymdSinTz` (600-601); `calcularEdadDias` por días de calendario sobre `ymdSinTz`.
   - `.html`: `\| date:'dd/MM/yyyy':'UTC'` en fechas de tabla/detalle (líneas 207, 654, 868).
3. `lote-reproductora-ave-engorde/`:
   - `services/...service.ts` `toIso`: anclar a mediodía UTC (ymd directo o extrayendo `ymdSinTz` de ISO con hora).
   - `pages/...list.component.ts`: dejar de pre-convertir en el componente (pasar el `YYYY-MM-DD` crudo; el service ancla) (262, 427, 461); poblar edición con `ymdSinTz` (364); `formatDate` sobre `ymdSinTz` conservando salida `toLocaleDateString('es')` (482).
4. `seguimiento-diario-lote-reproductora/`:
   - `modal-seguimiento-reproductora.component.ts`: enviar `fecha` con `ymdToIsoUtcNoon` (435); poblar con `ymdSinTz` (370).
   - `...list.component.ts/.html`: pipes con `:'UTC'` (271, 432); `slice(0,10)` → `ymdSinTz` (155); `calcularEdad` por días de calendario (595+).
5. `engorde-comun/funciones/fecha.funcion.ts`: `toYMD` rama ISO → sin tz literal / con tz vía UTC; `computeDefaultFecha` usa `toYMD` (hoy hace `substring(0,10)` literal). `ymdToIsoAtNoon` se mantiene (comportamiento vigente correcto del modal).
6. `engorde-comun/pages/modal-seguimiento-engorde.component.ts:601`: `substring(0,10)` → `toYMD`.
7. `aves-engorde/pages/seguimiento-aves-engorde-list.component.ts` (`formatDMY`/`toYMD` privados) y `aves-engorde/components/tab-reproductora-engorde` (`formatFechaUtc`): alinear al extractor sin corrimiento.

### Backend (.NET) — sin migraciones, sin DDL
8. `Application/Calculos/FechasPuras.cs` (nuevo, puro): `AnclarMediodiaUtc(DateTime?)`: `Unspecified→.Date` literal, `Utc→.Date`, `Local→ToUniversalTime().Date`, luego `+12h` `Kind=Utc`.
9. Aplicar al escribir: `LoteAveEngordeService` (216-217, 347-348 — reemplaza `ToUniversalTime()`), `LoteReproductoraAveEngordeService` (198, 285, 339), `SeguimientoDiarioLoteReproductoraService` (179, 264), `SeguimientoAvesEngordeService.Crud` (119, 268) y equivalente Ecuador si aplica el mismo patrón.
10. Revisar en esos services búsquedas por fecha (igualdad exacta de instante o rangos `<= hasta` a medianoche) y pasarlas a comparación por día `[inicioDía, díaSiguiente)` en UTC para que filas a mediodía no queden fuera. Solo dentro de los services tocados.

### Reglas de negocio preservadas
- La fecha digitada es la fecha almacenada/mostrada (semántica de fecha pura, sin hora significativa).
- Datos existentes (medianoche UTC) se muestran correctos sin backfill.
- `LiquidadoAt`, `CreatedAt`, mermas y demás timestamps reales NO se tocan.

## 4. Casos de prueba
- xUnit `FechasPurasTests`: Unspecified/Utc/Local → mismo día anclado 12:00 UTC; null → null; idempotencia (anclar dos veces).
- Build front (`yarn build`) y back (`dotnet build`) + `dotnet test` verdes.
- Smoke manual (dev local, backend tz Bogotá — peor caso): crear lote engorde con fecha X → tabla y detalle muestran X; editar y reabrir → form muestra X; ídem reproductora; registrar seguimiento reproductora fecha X → tabla muestra X; seguimiento engorde sigue mostrando X (regresión).
