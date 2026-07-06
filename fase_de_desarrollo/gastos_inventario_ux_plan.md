# Plan — Mejora UX módulo Gastos de inventario (Ecuador · No alimentos · stock granja)

## Contexto
Módulo `frontend/src/app/features/gastos-inventario/` (una página + servicio). Consumos por concepto (no alimentos); descuenta stock a nivel granja. Usuario: `admin.ecuador@italcol.com`. Validado en visual local (stack contra BD `:5433`, 2 registros reales).

## Enfoque arquitectónico
Refactor **visual/UX sin cambiar comportamiento** (mismos endpoints, mismo payload, misma aritmética). Solo HTML/SCSS + ajustes mínimos de TS (nada de lógica de negocio ni de cálculo). Se alinea al sistema de diseño (tokens Italfoods/SanMarino, `ux-*`, ToastService ya en uso).

## Archivos a tocar
- `pages/gastos-inventario-page/gastos-inventario-page.component.html`
- `pages/gastos-inventario-page/gastos-inventario-page.component.scss`
- `pages/gastos-inventario-page/gastos-inventario-page.component.ts` (mínimo: helper `limpiarFiltros`, nada de negocio)

## Hallazgos / cambios
1. **BUG tabla Registros:** `thead` tiene 9 columnas pero `tbody` 10 (falta `Granja`) → encabezados corridos (la granja "Kilometro 86" aparece bajo "Núcleo", etc.). **Fix:** agregar `<th>Granja</th>` tras Fecha; colspan del empty-state 9 → 10.
2. **Modal cortado:** header sticky del modal queda detrás de la topbar (z-index). **Fix:** subir `z-index` de `.modal-shell` por encima de la topbar; header con botón cerrar visible.
3. **Detalle del gasto:** de `<p>` planos → cabecera con chips (fecha/granja/lote/estado como pill) + tabla con fila de total.
4. **Modal crear:** pulir header (título + cerrar), resumen de líneas (total ítems/cantidad), estados vacíos claros. Sin tocar validaciones ni `save()`.
5. **Filtros:** botón "Limpiar filtros"; alineación consistente.

## Reglas de negocio (NO tocar)
- Stock se descuenta a nivel granja; núcleo/galpón/lote son referencia.
- No hay edición de gasto (crear/ver/eliminar) — se mantiene.
- `formatNum` (toFixed 3) se conserva (firma distinta al central `formatearNumero` → no migrar, cambiaría salida).

## Validación
- `yarn build` 0 errores.
- Visual en preview (HMR) logueado admin.ecuador: tabla alineada, modal completo, detalle nuevo, filtros.
- Revertir `appsettings.Development.json` a `:5432` antes de cualquier commit. No commitear sin OK.
