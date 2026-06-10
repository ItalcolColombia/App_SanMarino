# Plan: Optimización Seguimiento Diario Reproductora
**Fecha:** 2026-06-01  
**Módulo:** Seguimiento Diario Reproductora (aves de engorde)

---

## Alcance

### Req 1 — Reestructuración UI Modal "Nuevo Registro"
**Archivo:** `frontend/.../lote-levante/pages/modal-create-edit/modal-create-edit.component.html`

- Eliminar bloque **"Machos — alimento e ítems"** (section completa con `modal-lev-box--machos`)
- Renombrar bloque Hembras → **"Alimento e Ítems del Día"** (panel unificado)
- Eliminar sección **"Agregar ítems"** con sus 3 botones (+ Ítem hembras, + Ítem machos, + Ítem general)
- Ocultar filas **Uniformidad (%)** y **Coeficiente de Variación (CV)** en Peso y Uniformidad
- `resetForm()` ya pre-agrega una fila con `tipoItem: 'alimento'` via `agregarItemHembras()` → sin cambio TS

### Req 2 — Columnas tabla "Registros de Seguimiento"
**Estado:** YA IMPLEMENTADO en el HTML del list component. No se requieren cambios.

### Req 3 — Descuento automático de inventario (backend)
**Archivo:** `backend/.../Services/SeguimientoDiarioLoteReproductoraService.cs`

**Patrón:** mismo que `SeguimientoAvesEngordeService` (referencia confirmada)

- Inyectar `IInventarioGestionService?` (ya registrado en DI como Scoped)
- Agregar helper `ParseMetadataItemsToKg` (copia exacta)
- `CreateAsync`: tras `SaveChangesAsync`, iterar items del metadata y llamar `RegistrarConsumoAsync(FarmId, NucleoId, GalponId, itemId, qty, "kg", ref, null)`
  - FarmId/NucleoId/GalponId se obtienen trazando: `LoteReproductora.LoteAveEngordeId → LoteAveEngorde`
- `UpdateAsync`: calcular delta old→new por itemId, llamar `RegistrarConsumoAsync` (si diff > 0) o `RegistrarIngresoAsync` (si diff < 0)
- `DeleteAsync`: iterar items del metadata antes de eliminar y llamar `RegistrarIngresoAsync` para restituir stock

---

## Archivos a modificar

| Archivo | Tipo de cambio |
|---|---|
| `modal-create-edit.component.html` | UI: remover machos, agregar ítems, ocultar Unif/CV |
| `SeguimientoDiarioLoteReproductoraService.cs` | Backend: descuento inventario Create/Update/Delete |
