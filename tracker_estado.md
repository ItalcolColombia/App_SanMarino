# Tracker: Mejoras Lote Reproductora Aves de Engorde
**Plan:** [lote_reproductora_mejoras_modal_tabla_plan.md](fase_de_desarrollo/lote_reproductora_mejoras_modal_tabla_plan.md)
**Fecha:** 2026-06-02  
**Estado:** ✅ IMPLEMENTADO — backend y frontend compilan sin errores

---

## Checklist de implementación

### Backend ✅
- [x] Entidad: añadir `CodigoReproductora` a `LoteReproductoraAveEngorde`
- [x] Config EF: mapear `codigo_reproductora` en `LoteReproductoraAveEngordeConfiguration`
- [x] DTOs: añadir `CodigoReproductora` a los 3 records (`Dto`, `CreateDto`, `UpdateDto`)
- [x] Service: `Map()` incluye `CodigoReproductora`
- [x] Service: `CreateAsync` asigna `CodigoReproductora`
- [x] Service: `CreateBulkAsync` asigna `CodigoReproductora`
- [x] Service: `UpdateAsync` asigna `CodigoReproductora`
- [x] Migración EF `AddCodigoReproductoraToLoteReproductoraAveEngorde` (idempotente: `ADD COLUMN IF NOT EXISTS`)

### Frontend ✅
- [x] Service: añadir `codigoReproductora?` a interfaces TS (LoteReproductoraAveEngordeDto, CreateDto)
- [x] Component TS: form control `codigoReproductora` (bulk row + edit form)
- [x] Component TS: getter `sieteDiasCompletosLote`
- [x] Component TS: `canCreateMore()` bloquea si 7 días completos
- [x] Component TS: patch value edit incluye `codigoReproductora`
- [x] Component TS: payload saveBulk incluye `codigoReproductora`
- [x] Component TS: payload save/edit incluye `codigoReproductora`
- [x] Component HTML: campo "Código reproductora" en modal crear (grid 4 columnas, Datos básicos)
- [x] Component HTML: campo en modal editar
- [x] Component HTML: campo en modal ver (readonly)
- [x] Component HTML: tabla — columnas Cód. Reprod. / H Encaset. / M Encaset. / H Actual / M Actual / Edad (días) / Registros (n/7)
- [x] Component HTML: alerta "lote no puede crear más…" cuando 7 días completos
- [x] Component SCSS: modal crear más ancho (1160px) y scroll más alto (480px / 60vh)
- [x] Component SCSS: modal editar más ancho (860px / 68vh)
- [x] Component SCSS: grid-4 columnas
- [x] Component SCSS: estilos alerta + badge registros
- [x] Component SCSS: tabla compacta para más columnas

## Pendiente (deploy)
- [ ] Deploy prod: migración se aplica automáticamente al arrancar ECS
