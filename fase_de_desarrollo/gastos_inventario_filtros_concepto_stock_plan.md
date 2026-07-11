# Plan — Gastos de inventario: filtros solo Granja+Corrida y Concepto filtrado por stock

## Contexto
Capturas del usuario sobre el módulo `frontend/src/app/features/gastos-inventario/`:
1. Modal "Registrar gasto de inventario" → sección 2 (Concepto / Ítem): los dos campos están marcados como "grow" pero no reparten el ancho por igual (el select de Concepto queda angosto, el de Ítem ancho) — bug de CSS, no de contenido.
2. El select "Concepto" lista TODOS los conceptos de la compañía (p. ej. "Vacuna") aunque esa granja no tenga ningún ítem con stock para ese concepto → el usuario lo elige y el select de Ítem queda vacío. Debe listar solo conceptos con al menos un ítem con stock en la granja elegida.
3. El panel "Filtros" de la lista trae Granja, Núcleo, Galpón, Lote, Desde, Hasta, Concepto, Estado, Búsqueda. En el uso real solo importan **Granja** y **Corrida** (nombre del lote); el resto se omite siempre.

## Enfoque arquitectónico
Solo capa de presentación + un ajuste puntual de consulta (sin tocar reglas de negocio de creación/eliminación de gastos ni el cálculo de stock).

1. **Backend** (`InventarioGastoService.GetConceptosAsync`): agrega parámetro opcional `farmId`. Cuando viene, filtra a conceptos que tengan ≥1 ítem activo, no-alimento, con stock (`InventarioGestionStock.Quantity > 0`, `NucleoId/GalponId = null`) en esa granja — mismo criterio que ya usa `GetItemsWithStockAsync`. Sin `farmId`, comportamiento actual (todos los conceptos de la compañía). Controller: `GET /api/inventario-gastos/conceptos?farmId=`.
2. **Frontend service** (`InventarioGastosService.getConceptos`): agrega `farmId?` como query param opcional.
3. **Frontend componente** (`GastosInventarioPageComponent`):
   - Quita `loadConceptos()` global de `ngOnInit` (ya no hay filtro de Concepto en la lista).
   - Nuevo flujo en el modal: al cambiar la granja del formulario (`onFormFarmChange`) o al abrir el modal (`openCreate`), recarga `conceptos` scoped a `formFarmId` y resetea `formConcepto`.
   - Select de Concepto deshabilitado hasta elegir granja (`[disabled]="!formFarmId || !conceptos.length"`), igual patrón que el select de Ítem.
4. **Filtros de lista**: agrega `[showNucleoGalpon]="false"` al `app-filtro-select` de la lista (mismo flag que ya usa el modal desde el plan `33_gastos_inventario_galpon_y_liquidacion_fechas_plan.md` — dedupe por corrida incluido gratis). Elimina el bloque `filters-extra` (Desde/Hasta/Concepto/Estado/Búsqueda) del HTML, las propiedades TS asociadas (`fechaDesde/fechaHasta/conceptoFilter/estadoFilter/searchTerm`) y su uso en `refresh()/exportExcel()`. `limpiarFiltros()` pasa a limpiar Granja/Núcleo/Galpón/Lote (los únicos filtros que quedan).
5. **CSS alineación**: `.modal-row .ux-field.grow { flex: 1 1 0%; min-width: 0; }` y `.modal-row .ux-field:not(.grow) { flex: 0 0 auto; }` para que Concepto/Ítem repartan el ancho por igual sin depender del texto seleccionado.

## Archivos a tocar
- `backend/src/ZooSanMarino.Application/Interfaces/IInventarioGastoService.cs`
- `backend/src/ZooSanMarino.Infrastructure/Services/InventarioGastoService.cs`
- `backend/src/ZooSanMarino.API/Controllers/InventarioGastosController.cs`
- `frontend/src/app/features/gastos-inventario/services/inventario-gastos.service.ts`
- `frontend/src/app/features/gastos-inventario/pages/gastos-inventario-page/gastos-inventario-page.component.ts`
- `frontend/src/app/features/gastos-inventario/pages/gastos-inventario-page/gastos-inventario-page.component.html`
- `frontend/src/app/features/gastos-inventario/pages/gastos-inventario-page/gastos-inventario-page.component.scss`

## Reglas de negocio (NO tocar)
- Stock se descuenta a nivel granja; núcleo/galpón/lote son referencia (sin cambios).
- `GetItemsWithStockAsync` (criterio de stock por ítem) queda igual; el nuevo filtro de conceptos reutiliza el mismo criterio, no lo reemplaza.
- Sin filtro de Concepto/Estado/Búsqueda en la lista, `SearchAsync`/`ExportAsync` siguen aceptando esos parámetros (por si se reintroducen); el componente simplemente deja de enviarlos.

## Validación
- `dotnet build` (Application/Infrastructure/API) 0 errores.
- `yarn build` 0 errores.
- Visual en preview logueado `admin.ecuador@italcol.com` (clave `123456789`, BD local): Filtros de lista solo Granja+Lote; modal con Concepto/Ítem alineados; Concepto solo muestra conceptos con stock en la granja elegida.
