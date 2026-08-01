# Plan — Reporte Contable · sección "Movimientos de Huevos" dual-fuente (legacy + seguimiento_diario_produccion)

**Fecha:** 2026-08-01
**Problema:** `ReporteContableService.ObtenerReporteMovimientosHuevosAsync` lee los huevos por día SOLO desde
`_ctx.SeguimientoDiario` con `tipo_seguimiento='produccion'` (tabla legacy `seguimiento_diario_levante`). Desde la
Fase 3 la producción vive en `seguimiento_diario_produccion` (`SeguimientoProduccion`) y la legacy tiene **0 filas
de producción** (verificado en BD local: 0 filas globales con ese tipo; prod igual) ⇒ la sección sale siempre
vacía o lanza "No se encontraron registros de producción". El resto del reporte (~489-535) ya es dual-fuente.

## Hallazgos de la auditoría (BD local `sanmarinoapplocal:5433`)

| Lote | Topología | legacy (tipo=produccion) | seguimiento_diario_produccion | traslado_huevos |
|---|---|---|---|---|
| 13 (K345A) | PADRE con hijo 14 | 0 filas | 301 filas · 1.541.184 huevos (lote_id=13) | con `lote_id='13'` propios |
| 14 (K345B) | hijo de 13 | 0 filas | 301 filas · 2.091.450 huevos | `lote_id='14'` |
| 130 (LOTE NIZA E2E) | PADRE **sin hijos** (topología LPP) | 0 filas | 7 filas · 25.330 huevos (08–14 jun) | planta 2.900 + venta 2.000 (`lote_id='130'`) |

- El método actual **lanza** "No se encontraron sublotes" para el 130 (no tiene hijos) antes de leer nada.
- El padre 13 registra producción y traslados PROPIOS que el alcance "solo sublotes" pierde.
- El flujo `SemanaContable` (líneas ~1193-1199) ya usa padre+hijos para fechas; la fn
  `fn_indicadores_produccion_postura` (flujo legacy) resuelve "hijo en fase Produccion si existe, si no el propio
  lote". Incluir al padre es el patrón establecido.
- Sin filas legacy de producción en ningún entorno ⇒ la sección hoy está SIEMPRE vacía ⇒ el cambio es aditivo
  (no existe salida actual que pueda regresionar).

## Enfoque

**Criterio de merge = el canónico de las fns de producción** (`fn_indicadores_produccion_postura`,
Migrations/20260728160000, CTE `crudos` + `dedup`): UNION de ambas fuentes y `DISTINCT ON (día Bogotá) ORDER BY
día, ts` ⇒ por cada día calendario gana el registro de timestamp más temprano. Traducción C# (aquí hay N lotes):
dedup por `(LoteId, Fecha.Date)` eligiendo `OrderBy(ts).First()`; en empate exacto de ts gana la fila legacy
(desempate determinista; la fn no define orden en empate). Con Npgsql legacy behavior el `.Date` del DateTime
leído ES el día Bogotá para filas ancladas a mediodía (patrón del repo) — mismo supuesto que todo el C# vigente.

`fn_seguimiento_diario_produccion` (plan seguimiento_produccion_fn_canonica) AÚN NO EXISTE ⇒ merge en C#.

### Archivos

1. **`Application/Calculos/ReporteContableHuevosCalculos.cs` (NUEVO, puro, sin EF):**
   - `record struct FilaHuevosDia(int LoteId, DateTime Fecha, bool EsLegacy, int HuevoTot, … int HuevoOtro)` (13 campos de huevo).
   - `static List<FilaHuevosDia> MergeDualFuentePorDia(IEnumerable<FilaHuevosDia> filas)`:
     GroupBy (LoteId, Fecha.Date) → gana menor ts, empate → legacy → orden salida Fecha asc, LoteId asc.

2. **`Infrastructure/Services/ReporteContableService.cs` — SOLO `ObtenerReporteMovimientosHuevosAsync`:**
   - **Alcance de lotes = padre + sublotes** (consulta de seguimientos, traslados, diccionario de nombres,
     min/max de fechas). Se elimina el throw "No se encontraron sublotes" (el padre ya garantiza ≥1 lote;
     el throw "No se encontraron registros de producción" se conserva cuando AMBAS fuentes están vacías).
   - **Flujo SemanaContable**: `primeraFecha` = min(legacy, nueva) (ya usaba padre+hijos).
   - **Flujo sin fechas**: `primeraFecha`/`ultimaFecha` = min/max entre ambas fuentes.
   - **Consulta principal**: legacy EXACTA como está (mismo Where por timestamp crudo) + consulta a
     `_ctx.SeguimientoProduccion` con el patrón de rango del fallback dual existente
     (`s.Fecha.Date >= ini.Date && s.Fecha.Date <= fin.Date`, líneas ~493-496) → mapear ambas a
     `FilaHuevosDia` → `MergeDualFuentePorDia` → el GroupBy por fecha y todo lo demás queda IGUAL.

3. **`tests/ZooSanMarino.Application.Tests/ReporteContableHuevosCalculosTests.cs` (NUEVO):** solo-legacy passthrough,
   solo-nueva passthrough, gana ts más temprano (en ambos sentidos), empate ts → legacy, lotes distintos mismo día
   no se pisan, duplicado intra-fuente colapsa al más temprano (semántica DISTINCT ON), orden de salida.

### Sin cambios de BD ni de contrato
Cero migraciones. El DTO de salida (`ReporteMovimientosHuevosDto`) no cambia. Ningún otro método del servicio se toca.

## Casos de prueba / validación

1. `cd backend && dotnet build` — 0 errores, sin advertencias nuevas.
2. `dotnet test` — tests nuevos del cálculo + suite completa verde.
3. **Smoke HTTP antes/después** (backend local Dev, JWT + X-Secret-Up minteados, BD `sanmarinoapplocal`):
   - ANTES (código actual): `GET /api/ReporteContable/movimientos-huevos?lotePadreId=13` y `=130` → capturar
     (esperado: vacío/error — la sección está muerta).
   - DESPUÉS: lote 130 → 7 días, TotalPostura=25.330, TrasladoAPlanta=2.900 (10-jun), Venta=2.000 (12-jun);
     lote 13 → días de 13+14 con TotalPostura=3.632.634 (1.541.184+2.091.450) contrastado contra SQL directo.
   - Verificar por SQL que el merge no duplica: total del reporte == SUM(huevo_tot) de la fuente nueva
     (legacy vacío ⇒ la parte legacy no aporta filas, semántica "sin cambios" pedida).
4. Backend de smoke detenido al terminar (sin procesos huérfanos).
