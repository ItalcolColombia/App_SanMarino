# Tracker — Mejora UX módulo Gastos de inventario (Ecuador)

> Plan: [`fase_de_desarrollo/gastos_inventario_ux_plan.md`](fase_de_desarrollo/gastos_inventario_ux_plan.md)
> Refactor visual/UX **sin cambiar comportamiento**. Validado en local contra BD `:5433` como `admin.ecuador`.
> (Fase 0 sistema de diseño quedó DESPLEGADA a prod — ver memoria/PR #25 y #26.)

## Checklist
- [x] Diagnóstico en visual real (login admin.ecuador, 2 registros)
- [x] **BUG:** tabla Registros — agregado `<th>Granja</th>` + colspan empty-state 9→10 (headers ahora alineados: verificado en preview)
- [x] **Modal:** z-index `.modal-shell` 71→1100 (header/Cerrar visible en crear y detalle — verificado top=1)
- [x] **Modal medio centrado (crear + detalle):** de full-screen (100dvh) → `min(960px, 100%)` centrado, alto `min(90vh,860px)`, esquinas redondeadas + backdrop oscuro (antes sin estilo) + click-fuera cierra. Mejor para PC. Verificado en preview.
- [x] **Detalle del gasto:** chips (fecha/lote) + pill estado + tabla stock antes/después (verificado)
- [x] **Modal crear:** header completo + Guardar deshabilitado sin líneas (verificado)
- [x] **Filtros:** botón "Limpiar" (secundarios) agregado
- [x] **Extra (alinea reglas):** confirm propio → `ConfirmDialogService` (modal compartido, cancel no muta — verificado con "2 registro(s)" intactos)
- [x] `yarn build` 0 errores
- [x] Verificado en preview (HMR) logueado admin.ecuador
- [x] appsettings en `:5432` (no commiteado; pendiente OK del usuario para subir/desplegar)

## Fase 2 — Lógica de lista a la BD (el back orquesta, la BD arma)
- [x] `fn_inventario_gastos_search` (joins granja/núcleo/galpón/lote + agregación líneas/total/unidad + filtros/concepto/búsqueda, orden fecha/id) en `backend/sql/` + migración `AddFnInventarioGastosSearch` (idempotente, no altera snapshot)
- [x] `InventarioGastoService.SearchAsync`: de ~130 líneas (diccionarios + N round-trips en C#) → ~25 (SqlQueryRaw + mapeo). Row `InventarioGastoListRow` (props PascalCase → columnas snake_case por naming convention)
- [x] **Golden test**: salida de la función == salida del C# (2 registros idénticos) + filtros estado/fecha/concepto/farm verificados
- [x] Backend `dotnet build` 0 errores; `dotnet test` 122/122 (Application 121 + Domain 1)
- [x] Verificado en vivo (backend :5002 → BD :5433 → front): lista "2 registro(s)" idéntica, servida por la función
- [x] appsettings revertido a `:5432`

## Fase 3 — Ítems consumidos inline en la tabla (sin abrir el detalle)
- [x] `fn_inventario_gastos_search` devuelve `items` (JSON: codigo/nombre/cantidad/unidad por línea, orden por nombre)
- [x] Backend: DTO `InventarioGastoLineaResumenDto` + campo `Items` en la lista; `SearchAsync` deserializa el JSON (`ParseItems`)
- [x] Front: DTO `items` + columna ancha **"Ítems (consumo)"** (badge código + nombre completo con wrap + cantidad), se quitó la columna "Líneas" (redundante)
- [x] Verificado en vivo: muestra AV0316 · AV. CILINDRO DE GAS INDUSTRIAL 15KG 15% · 10.000 kg
- [x] Front build 0 err; backend build 0 err; tests 121/121

## Estado
Fases 1 (UX) + 2 (lista a la BD) + 3 (ítems inline) hechas y validadas en local → **a desplegar a prod** (main → produccion-main).
Pendiente opcional: mover también `ExportAsync`/`GetByIdAsync` a funciones de BD (mismo patrón).
