# Tracker — Sistema de diseño compartido (`shared/ui/`) · Fase 0 (retomada)

> Plan: [`fase_de_desarrollo/design_system_shared_ui_plan.md`](fase_de_desarrollo/design_system_shared_ui_plan.md)
> Decisión: **A) abstracción propia sobre `@angular/cdk`** (no 3rd-party). Referencia canónica: `movimientos-pollo-engorde`.

## Fase 0 — Quick wins de adopción (bajo riesgo)
- [x] **Fundación `shared/utils/format.ts`** (formatearNumero/fechaCorta/ymdToIsoUtcNoon/dateStampCompact/sanitizeFileName + re-export formatDecimalTrim)
- [x] **Fundación `shared/utils/excel/exportar-tabla-excel.funcion.ts`** (`exportarTablaExcel` + `exportarMultiHojaExcel` + `construirAoaExcel`) + spec
- [x] Piloto: export de `movimientos-pollo-engorde` migrado al helper (mismo `.xlsx`) · build 0 err
- [x] **Helper Excel completo**: `exportarTablaExcel` (aoa) + `exportarMultiHojaExcel` (aoa multi) + `exportarObjetosExcel` (json) + `exportarObjetosMultiHojaExcel` (json multi)
- [x] **7 exports migrados** al helper (build 0 err): movimientos-pollo-engorde, aves-engorde/tabs-principal-engorde, lote-levante/tabs-principal (reporteSemana), traslados-huevos, lote-produccion/tabs-principal, lote-levante/tabla-lista-indicadores (2 hojas), lote-produccion/tabla-lista-indicadores (2 hojas)
- [ ] 3 exports Excel COMPLEJOS pendientes (indicador-ecuador ×2 + informe-semanal: multi-hoja dinámico/loop) + el "Seguimiento" de lote-levante/tabs-principal (cabecera compleja) → necesitan variante aoa-pre-armado; menor valor/mayor riesgo
- [ ] Migrar 21 `confirm()` nativos → `confirmation-modal` (ya existe)
- [ ] Migrar 17 `alert()` + 23 mensajes inline → `ToastService`
- [ ] Adoptar `format.ts` en los ~50 archivos con helpers de formato duplicados

## Estado
- Fundación (format.ts + excel + canónico) hecha, **sin commitear** (excluida de los deploys de inventario/menú/paleta/modal a propósito). Se deploya cuando Fase 0 avance más.

## Nota
Frentes YA desplegados a prod en sesiones previas: unificación inventario Colombia, normalización de menú, paleta SanMarino, reorganización modal producción.
