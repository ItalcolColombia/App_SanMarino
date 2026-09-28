# Plan — Reporte Contable: Selección de MACHOS en fase Producción

**Origen:** novedad del área contable de Sanmarino — el reporte muestra la mortalidad de machos
pero la **selección de machos sale 0** en fase Producción (postura), aunque el dato existe.

## Diagnóstico (validado en código)

En fase Producción el reporte contable **descarta la selección de machos**. El dato de origen sí
existe: `produccion_diaria` (entidad `SeguimientoProduccion`) tiene la columna `sel_m`
(`SeguimientoProduccion.cs:15`), y el **Reporte Diario Costos Postura** ya la lee y la muestra
(`fn_reporte_diario_costos_postura` → `sel_m`; front `seleccionM`). El contable la pierde en 3 puntos:

| Punto | Archivo | Qué pasa hoy |
|---|---|---|
| Record de producción | `ReporteContableSeguimientoDiaCalculos.cs:15` | Tiene `MortalidadM` pero **no** tiene `SelM` |
| Consulta principal | `ReporteContableService.CalculoSemanal.cs:217` | Trae `s.SelH` de `produccion_diaria`, **omite** `s.SelM` |
| Armado de la fila diaria | `ReporteContableService.CalculoSemanal.cs:332` | `SeleccionMachos = levante?.SelM ?? 0` — sin fallback a producción |

La mortalidad de machos **sí** se lee de producción (`:217` `MortalidadM = s.MortalidadM`, `:330`),
de ahí la asimetría "muestra mortalidad pero no selección" en machos.

## Enfoque arquitectónico

Corrección de correctitud, **no** refactor: que la selección de machos de producción se lea igual
que la mortalidad de machos. Cambio mínimo y simétrico con `MortalidadM`.

- **Sin migración, sin SQL.** El dato ya está en `produccion_diaria.sel_m`. No se toca la BD ni la fn.
- **Sin cambio de contrato.** El DTO ya expone `SeleccionMachos*` (hoy en 0); los endpoints responden igual.
- **Alcance:** el menú del Reporte Contable está habilitado **solo en Agroavícola Sanmarino** (reproductoras
  → sí hay machos en postura). Es un cálculo puro por lote/día, **no** una fn compartida multipaís.
- **Byte a byte para lo demás:** cualquier lote con `sel_m = 0` en producción da idéntico a hoy
  (hembras, levante, bultos, huevos y consumos no se tocan).

## Archivos a modificar

| Archivo | Cambio |
|---|---|
| `backend/src/ZooSanMarino.Application/Calculos/ReporteContableSeguimientoDiaCalculos.cs` | `record SeguimientoProduccionContableFila` gana `int SelM` (tras `SelH`); `AgruparProduccionPorLoteDia` suma `SelM` |
| `backend/src/ZooSanMarino.Infrastructure/Services/Funciones/ReporteContableService.CalculoSemanal.cs` | Proyección principal (`:217`) y fallback (`:244`) traen `SelM`; ambos constructores lo pasan; `:332` `SeleccionMachos = levante?.SelM ?? produccion?.SelM ?? 0` |
| `backend/tests/ZooSanMarino.Application.Tests/ReporteContableSeguimientoDiaCalculosTests.cs` | Actualizar los 4 constructores existentes (+ arg `SelM`) y agregar test de suma de `SelM` |

## Reglas de negocio

1. **Selección de machos en producción = `SUM(produccion_diaria.sel_m)` del lote en la semana** (mismo
   patrón que `MortalidadM` y que el reporte de Costos Postura).
2. **Prioridad de fuente idéntica a mortalidad:** `levante?.SelM ?? produccion?.SelM ?? 0` (el día de
   transición gana levante, igual que el resto de campos de aves).
3. **El saldo de machos ahora resta la selección de machos** (`saldoFinM = ... - seleccionM - ...`,
   `:862`): en lotes que seleccionaron gallos, el saldo de machos vivos **baja** (queda correcto; hoy
   está sobreestimado). Es el único número que cambia.
4. **Fallback `seguimiento_diario` tipo=produccion** (`:244`): también lee `SelM` (`SeguimientoDiario.SelM`
   es `int?` → `?? 0`), para no reintroducir el bug por el otro camino.

## Casos de prueba

| # | Caso | Esperado |
|---|---|---|
| 1 | `AgruparProduccionPorLoteDia` con 2 registros mismo día con `SelM` | El día suma los `SelM` (xUnit nuevo) |
| 2 | 1 registro por día | Devuelve el mismo registro (regresión, ya cubierto) |
| 3 | Producción con `sel_m = 0` | Nada cambia vs. comportamiento previo |
| 4 | Lote real de Sanmarino con `sel_m > 0` en producción (BD local) | `SeleccionMachosSemanal` = `SUM(sel_m)` de la semana; saldo machos baja en esa cantidad |

## Validación

- `cd backend && dotnet build` (0 errores, sin advertencias nuevas).
- `cd backend && dotnet test` (suite Application + tests nuevos verdes).
- **BD local (`sanmarinoapplocal` @ 127.0.0.1:5433):** localizar un lote base de Sanmarino en postura
  con `produccion_diaria.sel_m > 0`; congelar el `SUM(sel_m)` por semana; levantar el backend, generar
  el reporte de ese lote y confirmar que `SeleccionMachosSemanal` coincide y que el saldo de machos
  bajó exactamente esa cantidad. Apagar el backend y liberar el puerto al terminar.
