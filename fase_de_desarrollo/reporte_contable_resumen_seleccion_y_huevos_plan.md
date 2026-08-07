# Plan — Reporte Contable: Selección en la hoja RESUMEN + hoja de Movimientos de Huevo

**Origen:** hallazgos 3 y 4 del correo de conciliación del lote K345
(ver `conciliacion_lote_k345_niza_iii_analisis.md` §8).

## Enfoque arquitectónico

Los dos defectos son **de exportación**, no de cálculo: el dato ya existe y ya se muestra en
pantalla. No se toca el `ReporteContableService` ni la BD; el cambio queda confinado al
`ReporteContableExcelService` y al controller que lo orquesta.

- **Sin migración.** Ningún campo nuevo en BD.
- **Sin cambio de contrato.** Los endpoints existentes responden igual; el único cambio de firma es
  un parámetro **opcional** en `GenerarExcel`.
- **Sin cambio de comportamiento en lo existente.** Las hojas semanales y los valores actuales del
  resumen quedan idénticos: solo se agrega una columna y una hoja.

## Archivos a modificar

| Archivo | Cambio |
|---|---|
| `backend/src/ZooSanMarino.Infrastructure/Services/ReporteContableExcelService.cs` | Columna **Selección** en la hoja RESUMEN · nueva hoja **MOVIMIENTOS HUEVOS** |
| `backend/src/ZooSanMarino.API/Controllers/ReporteContableController.cs` | `ExportarExcel` resuelve además los movimientos de huevo y se los pasa al servicio |
| `backend/src/ZooSanMarino.Application/Calculos/ReporteContableResumenCalculos.cs` *(nuevo)* | Acumulado puro del resumen semanal (sin EF, sin EPPlus) |
| `backend/tests/ZooSanMarino.Application.Tests/ReporteContableResumenCalculosTests.cs` *(nuevo)* | xUnit del acumulado |

## Cambio 1 — Columna «Selección» en la hoja RESUMEN

**Hoy** (`ReporteContableExcelService.cs:143-156`) el resumen consolida
`Semana · Período · Mortalidad · Traslados · Ventas · Alimento · Agua · Medicamento · Vacuna ·
Otros · Total General`. **Falta Selección**, aunque el DTO ya la trae
(`ReporteContableDto.cs:117-119`) y la hoja semanal sí la escribe (`:384`).

**Queda:** `Semana · Período · Mortalidad · Selección · Traslados · Ventas · Alimento (kg) ·
Agua (L) · Medicamento · Vacuna · Otros · Total General` — 12 columnas.

- Selección va **inmediatamente después de Mortalidad**, igual que en la sección AVES de la hoja
  semanal (Mortalidad → Selección → Ventas → Traslados). Es el orden que ya conoce el usuario.
- El escritor se vuelve **data-driven** (lista de columnas con su formato) para que agregar una
  columna no exija reindexar a mano el bloque de formatos ni la fila de totales — que es
  exactamente donde vive el riesgo de off-by-one del código actual.
- El acumulado sale a `ReporteContableResumenCalculos` (cálculo puro, testeable).

## Cambio 2 — Hoja «MOVIMIENTOS HUEVOS»

El dato existe en `GET /api/ReporteContable/movimientos-huevos`
(`ReporteMovimientosHuevosDto`) y se ve en la pestaña *Movimientos de Huevos*, pero
`ReporteContableExcelService` no escribe ni un campo de huevo.

- `GenerarExcel(reporte, movimientosHuevos = null)` → parámetro **opcional**; si es `null` o sin
  filas, **no se agrega la hoja** (el Excel de Levante sigue saliendo idéntico al de hoy).
- El controller resuelve los movimientos con el mismo request que ya arma y los pasa.
- Columnas **espejo de la pantalla**, para que el Excel y la tabla digan lo mismo:
  `Día · Fecha · Lote ·` **Producción**: `POSTURA · HVTO FÉRTIL · HVO COMERCIAL · HUEVO DESECHO ·`
  **Movimientos**: `ENTRADA · CAPTURA INFO · VENTA · SALIDA · TRASLADO A PLANTA · DESCARTE`,
  más fila de **TOTALES** con los `Total*` que ya calcula el DTO.
- La hoja se inserta **después de RESUMEN** y antes de las hojas semanales.

## Reglas de negocio

1. La hoja RESUMEN **no cambia ningún valor existente**: solo agrega una columna.
2. `Total General` sigue siendo el que trae el DTO — la Selección es un conteo de aves, **no** entra
   al total de consumos (que es dinero/insumos). No se recalcula nada.
3. Fase Levante: sin movimientos de huevo ⇒ el libro sale exactamente como hoy.
4. Los totales de la hoja de huevo son los del DTO, no un recálculo del Excel (una sola fórmula por
   número).

## Casos de prueba

| # | Caso | Esperado |
|---|---|---|
| 1 | Resumen con N semanas | Cada fila trae Selección = `SeleccionHembrasSemanal + SeleccionMachosSemanal` |
| 2 | Fila TOTAL GENERAL | Cada acumulado = suma de sus semanas (test xUnit del cálculo puro) |
| 3 | Reporte sin semanas | Totales en 0, sin excepción |
| 4 | `GenerarExcel` sin huevos (`null`) | El libro tiene RESUMEN + N hojas semanales, **sin** hoja de huevo |
| 5 | `GenerarExcel` con huevos | Aparece «MOVIMIENTOS HUEVOS» en posición 2, con una fila por día + TOTALES |
| 6 | Export de Levante | Idéntico al actual (regresión) |
| 7 | Export de Producción de un lote real (K345 / S-369) | Selección ≠ 0 y huevo fértil/comercial/desecho cuadran contra la BD |

## Validación

- `cd backend && dotnet build` (0 errores, sin advertencias nuevas)
- `cd backend && dotnet test` (incluye los tests nuevos del acumulado)
- Smoke: exportar el Excel de un lote con producción y verificar hojas y totales contra la BD.
