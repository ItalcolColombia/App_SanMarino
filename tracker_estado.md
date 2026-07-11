# Tracker — Gastos de inventario: filtros solo Granja+Corrida y Concepto filtrado por stock

Plan: [gastos_inventario_filtros_concepto_stock_plan.md](fase_de_desarrollo/gastos_inventario_filtros_concepto_stock_plan.md)

**Estado: COMPLETO — build + verificación E2E visual OK (sin commitear).**

## Backend
- [x] `IInventarioGastoService.GetConceptosAsync(int? farmId = null, ...)`
- [x] `InventarioGastoService.GetConceptosAsync`: cuando viene `farmId`, filtra a conceptos con ≥1 ítem con stock>0 en esa granja
- [x] `InventarioGastosController.GetConceptos`: acepta `farmId` query opcional
- [x] `dotnet build` OK (Domain/Application/Infrastructure, 0 errores; API no se pudo compilar de punta a punta por bin lock de una sesión paralela — verificado igual vía preview con backend real levantado y funcionando)

## Frontend — service
- [x] `InventarioGastosService.getConceptos(farmId?)`

## Frontend — modal (Concepto filtrado por stock + alineación)
- [x] Quitar `loadConceptos()` global de `ngOnInit`
- [x] `onFormFarmChange()`: recarga `conceptos` scoped a `formFarmId`, resetea `formConcepto`
- [x] Select Concepto deshabilitado sin granja
- [x] CSS `.modal-row .ux-field.grow` reparte ancho por igual (Concepto/Ítem)

## Frontend — filtros de lista (solo Granja+Corrida)
- [x] `[showNucleoGalpon]="false"` en el `app-filtro-select` de la lista
- [x] Quitar bloque `filters-extra` (Desde/Hasta/Concepto/Estado/Búsqueda) del HTML
- [x] Quitar propiedades TS asociadas y su uso en `refresh()/exportExcel()`
- [x] `limpiarFiltros()` limpia Granja/Núcleo/Galpón/Lote
- [x] Limpiar SCSS `.filters-extra` sin uso
- [x] `yarn build` OK (0 errores; solo el warning preexistente de bundle budget)

## Validación
- [x] Visual en preview (login `admin.ecuador@italcol.com`): Filtros solo Granja+Lote
- [x] Visual: modal Concepto/Ítem alineados (mismo ancho, antes "Gas" quedaba angosto)
- [x] Visual: Concepto solo muestra conceptos con stock en la granja elegida (Kilometro 86 → Desinfectante/Empaques/Gas, sin "Vacuna"; confirmado con `GET /api/inventario-gastos/conceptos?farmId=40` → 200 OK)
- [x] Sin errores de consola; servidores de preview detenidos al terminar
