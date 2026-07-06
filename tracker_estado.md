# Tracker — Sistema de diseño compartido (`shared/ui/`) · Fase 0

> Plan: [`fase_de_desarrollo/design_system_shared_ui_plan.md`](fase_de_desarrollo/design_system_shared_ui_plan.md)
> Decisión: **A) abstracción propia sobre `@angular/cdk`** (no 3rd-party). Referencia canónica: `movimientos-pollo-engorde`.

## Fase 0 — Quick wins de adopción (bajo riesgo)
- [x] **Fundación `shared/utils/format.ts`** (formatearNumero/fechaCorta/ymdToIsoUtcNoon/dateStampCompact/sanitizeFileName + re-export formatDecimalTrim) — DESPLEGADO (PR #25)
- [x] **Fundación `shared/utils/excel/exportar-tabla-excel.funcion.ts`** + spec — DESPLEGADO (PR #25)
- [x] **7 exports migrados** al helper (aoa + objetos) — DESPLEGADO (PR #25)
- [x] **Helper Excel: variantes aoa pre-armado** (`exportarAoaExcel` + `exportarAoaMultiHojaExcel` con anchos de columna + filename custom)
- [x] **4 exports Excel COMPLEJOS migrados** (byte-equivalente): indicador-ecuador (multi-hoja consolidado + N lotes), informe-semanal (multi-hoja por semana), lote-levante "Seguimiento" (cabecera compleja + `!cols`), auditoria plantilla (`!cols` + filename fijo). Único XLSX crudo restante: `modal-cuadrar-saldos-engorde` (IMPORTA/parsea Excel subido, no exporta → fuera de alcance).
- [x] **17 `alert()` nativos → `ToastService`** (~60 llamadas: 33 error, 12 warning [validaciones], 2 success [confirmaciones]).
- [x] **`ConfirmDialogService` promise-based nuevo** (monta `ConfirmationModalComponent` dinámico, `await ask(): Promise<boolean>`) + **26 `confirm()` nativos → modal** en 20 archivos (métodos → `async`).
- [x] **Adopción `format.ts` (subset seguro)**: 6 componentes cuyo `formatearNumero` era idéntico al central (`Intl es-CO` / `toLocaleString('es-CO')` sin decimales/null) → delegan a `fmtNumero`. Patrón canónico.

## Pendiente (cola de 1×1, MENOR valor / requiere verificación individual — NO en este deploy)
- [ ] `formatearNumero`/`fechaCorta` con **firma distinta** (null→'0.00'/'-', decimales por parámetro, 0 decimales): `modal-movimiento-aves`, `indicador-ecuador-list`, `inventario-dashboard`. Adoptar el central **cambiaría la salida** → requiere variantes en `format.ts` o se dejan.
- [ ] Cola amplia de otros helpers de formato duplicados (`formatDMY`, `formatDate`, `formatearPorcentaje`, sellos de fecha en exports…): ~decenas de archivos, cada uno 1×1 verificando equivalencia numérica (riesgo de regresión silenciosa en reportes/liquidaciones sin test runtime).

## Nota
Frentes YA desplegados a prod en sesiones previas: unificación inventario Colombia, normalización de menú, paleta SanMarino, reorganización modal producción, Fase 0 fundación (PR #25).
