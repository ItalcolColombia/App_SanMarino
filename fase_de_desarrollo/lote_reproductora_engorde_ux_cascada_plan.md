# Plan — UX cascada numerada en «Lote Reproductora Aves de Engorde»

**Fecha:** 2026-07-22
**Solicitud:** aplicar el diseño y estética del módulo **Seguimiento Diario Reproductora Pollo Engorde** al módulo **Lote Reproductora Aves de Engorde** (pantalla de creación/listado), en especial el panel «Selección de contexto» con los **badges numerados (1→2→3→4)** que muestran la secuencia de la cascada.

---

## Enfoque arquitectónico

- **Front-only, solo presentación.** Cero cambios de lógica, contratos, payloads ni backend. Los `[(ngModel)]`, handlers (`onGranjaChange`…), permisos (`*appHasPermission`), modales y llamadas HTTP quedan idénticos.
- **Fuente del diseño (referencia canónica):** `seguimiento-diario-lote-reproductora-list.component.{html,scss}` — se copian sus patrones: `ux-page/ux-main`, `ux-header` (icono + título + breadcrumb chips + acciones), `filter-card` con `filter-steps` numerados y conectores, `info-card` (bloque datos + bloque highlight), `table-card` con `data-table`, `empty-state`, `table-loading`.
- **Modales intactos:** los modales Registrar/Editar/Ver del módulo conservan sus clases `lrae-*` y estilos actuales (fuera de alcance).
- Tokens de color vía variables Italfoods centralizadas (`--ital-*`) — sin colores hardcodeados nuevos.

## Archivos a modificar

| Archivo | Cambio |
|---|---|
| `frontend/src/app/features/lote-reproductora-ave-engorde/pages/lote-reproductora-ave-engorde-list/lote-reproductora-ave-engorde-list.component.html` | Rediseño del shell de página: header `ux-header` (icono, título, subtítulo, breadcrumb de contexto, botón primario), filtros → `filter-card` «Selección de contexto» con pasos numerados 1-4 y conectores (estados activo/completado/deshabilitado), hint → `empty-state`, info del lote → `info-card` (dl + highlight aves disponibles), tabla → `table-card` + `data-table` (badges, status-badge, action-btn, empty-state con CTA, spinner). Modales sin tocar. |
| `...component.scss` | Reescritura: tokens + bloques de diseño copiados/adaptados del módulo seguimiento; se conservan íntegros los estilos de modales (`lrae-modal*`, `lrae-form-*`, `lrae-field/label/input`, `lrae-btn*`, `lrae-create-*`, `lrae-estado`, responsive de modales); se elimina CSS muerto (lrae-header, lrae-filters, lrae-table, lrae-bulk-*, etc.). |
| `...component.ts` | Solo 3 getters de solo-lectura para el breadcrumb (`selectedGranjaName`, `selectedNucleoNombre`, `selectedGalponNombre`) — devuelven strings (sin riesgo NG0103). Ninguna otra modificación. |

## Cambios de BD / SQL

**Ninguno.**

## Reglas de negocio (se preservan tal cual)

- Cascada Granja → Núcleo → Galpón → Lote Aves de Engorde con reseteo descendente (handlers existentes).
- `disabled` de cada select idéntico al actual (núcleo requiere granja, galpón requiere núcleo, lote requiere galpón). El estado visual `--disabled` del paso refleja la misma condición.
- Botón «Registrar lote reproductora» deshabilitado igual que hoy (`!selectedLoteAveEngordeId || !canCreateMore()`); se agrega tooltip explicativo (solo texto).
- Badges: Registros n/7 (ámbar al completar 7), Estado Vigente/Cerrado.

## Casos de prueba (validación)

1. `yarn build` (Node portable 22.23.1) → 0 errores; único warning aceptado: bundle budget preexistente.
2. Visual: pasos numerados 1-4 con conector que se pinta al avanzar; paso siguiente deshabilitado hasta elegir el anterior; badge 4 en acento naranja.
3. Flujo funcional intacto: seleccionar cascada completa → tabla carga; crear/editar/ver/eliminar abren los mismos modales; permisos siguen ocultando botones.
4. Responsive: pasos en 2 columnas ≤1100px, 1 columna ≤640px; breadcrumb oculto en móvil (igual que la referencia).
