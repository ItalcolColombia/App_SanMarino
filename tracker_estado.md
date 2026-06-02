# Tracker: Seguimiento Diario Reproductora — Optimización UI + Inventario
**Plan:** [seguimiento_diario_reproductora_optimizacion_plan.md](fase_de_desarrollo/seguimiento_diario_reproductora_optimizacion_plan.md)  
**Fecha:** 2026-06-01

---

## Checklist

### Req 1 — UI Modal (modal-create-edit.component.html) ✅
- [x] Eliminar sección "Machos — alimento e ítems"
- [x] Renombrar bloque hembras a "Alimento e Ítems del Día"
- [x] Eliminar sección "Agregar ítems" (3 botones)
- [x] Ocultar fila Uniformidad H/M y CV H/M en compare-grid

### Req 2 — Columnas tabla ✅
- [x] Ya implementadas en iteración anterior (Fecha, Mort H/M, Sel H/M, Err H/M, Tipo alimento, Consumo)

### Req 3 — Backend Inventario (SeguimientoDiarioLoteReproductoraService.cs) ✅
- [x] Inyectar IInventarioGestionService (parámetro opcional)
- [x] Agregar helper ParseMetadataItemsToKg + ToKg + GetLoteUbicacionAsync
- [x] CreateAsync: descuento inventario post-save
- [x] UpdateAsync: ajuste delta inventario (consumo + devolución)
- [x] DeleteAsync: devolución inventario pre-delete
- [x] Build backend: 0 errores
