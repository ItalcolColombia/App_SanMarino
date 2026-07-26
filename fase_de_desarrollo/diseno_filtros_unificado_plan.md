# Plan — Diseño unificado de filtros «Selección de contexto» en TODOS los módulos

**Fecha:** 2026-07-25 · **Alcance:** frontend (solo presentación) · **Regla rectora:** refactor ≠ cambio de comportamiento — bindings, lógica, HTTP y estado quedan intactos; solo cambia el markup/clases de las zonas de filtrado.

## Objetivo

Aplicar en todo módulo que filtre información el diseño de referencia del módulo **Seguimiento diario pollo de engorde** (`aves-engorde/pages/seguimiento-aves-engorde-list`):

- Tarjeta `.filter-card` con header (icono + «Selección de contexto» + subtítulo).
- Cascada con pasos numerados `.filter-step` (1 Granja → 2 Núcleo → 3 Galpón → 4 Lote) con conectores y estados activo/hecho/deshabilitado.
- Filtros no-cascada (fechas, estado, búsqueda, año…) en la misma tarjeta con `.filter-fields` / `.filter-field` (sin número) y controles `.filter-step__select` / `.filter-step__input`.
- Selects/inputs con el estilo canónico (borde `--ital-green-100`, radio .75rem, focus naranja `--ital-orange`).

## Arquitectura de la solución

1. **Estilos globales (HECHO):** `frontend/src/styles/filter-context.scss` cargado desde `styles.scss` (junto al tema). Clases disponibles en cualquier template sin tocar el SCSS del componente. Extiende el diseño con `.filter-fields`/`.filter-field` (campos sin número), `input.filter-step__input` y `.filter-card__actions`.
2. **Cascadas granja→núcleo→galpón→lote:**
   - Consumidores de `app-filtro-select` (lote-levante): pasar `[variant]="'steps'"` (el componente ya trae la variante; comportamiento idéntico).
   - Fork de `lote-produccion/pages/filtro-select` (modelo LPP distinto — NO unificar lógica): portar solo la presentación `steps`.
   - `shared/components/hierarchical-filter`: rediseñar template/SCSS al filter-card/steps manteniendo API (arregla de una vez traslados-aves y migraciones-masivas).
   - Cascadas inline: envolver los selects existentes en el markup de pasos (bindings intactos).
3. **Filtros custom (fechas/estado/búsqueda/año/tabs):** envolver en `.filter-card` + `.filter-fields`, aplicar clases a los controles existentes. Nada de mover lógica.
4. **Header de página (ux-header con breadcrumb):** solo donde el módulo ya tiene un header equivalente y el cambio es de bajo riesgo; NO es gate de este plan.

## Reglas para los agentes ejecutores

- ❌ Prohibido: cambiar `ngModel`/`formControl`/eventos/orden de opciones/textos de opciones; tocar servicios o TS salvo para agregar propiedades PRESENTACIONALES triviales (ninguna esperada); getters nuevos que aloquen arrays por ciclo (NG0103).
- ✅ Obligatorio: usar las clases globales de `filter-context.scss` (no copiar CSS al componente); conservar `*appHasPermission`, `@if`/`@for` y accesibilidad (`label for`/`id`); si el componente ya tenía clases locales `filter-*`, dejar de duplicarlas solo si el resultado visual es idéntico.
- 🧪 Validación por batch: `yarn build` (Node portable 22.23.1 — anteponer `%USERPROFILE%\node-portable\node-v*` al PATH) con 0 errores; único warning aceptado: bundle budget preexistente.
- 🤝 Convivencia: otra sesión trabaja Santa Reyes Fase 3 (seguimiento levante/producción, modal traslados/movimientos-aves). Esos archivos van en el batch FINAL, releyendo el archivo justo antes de editar.

## Inventario de módulos (41) y batches

**Sin filtros de datos (6):** auth, engorde-comun, home, lesiones, profile, test.

**Ya conformes (3 pantallas):** `aves-engorde/seguimiento-aves-engorde-list` (filtro-select variant steps — referencia), `seguimiento-diario-lote-reproductora-list` (inline canónico), `lote-reproductora-ave-engorde-list` (inline duplicado conforme).

**Omitidos deliberados (documentar, no tocar):** `sincronizacion-panama` zona de parámetros (ya tiene diseño propio por pasos numerados; solo se estiliza el mini-filtro del historial), `lote/components/filtro-lotes` + `lote/page/lote-list` (huérfanos sin ruta), `traslados-aves/components/traslado-navigation-list` (huérfano), `shared/components/company-selector` (componente compartido con variantes propias — no se rediseña por dentro).

| Batch | Modelo | Archivos (zona de filtro) | Acción |
|---|---|---|---|
| **A** | sonnet | gastos-inventario-page · traslados-huevos-list (+2ª zona tabla) · traslado-huevos-form · reporte-tecnico-administrativo-main (+controles propios) · reporte-tecnico-produccion-main (+fechas/radios) | Flip `[variant]="'steps'"` + envolver controles custom adyacentes en la misma tarjeta |
| **B** | opus | `shared/components/hierarchical-filter` (consume: migraciones-masivas, traslados-aves movimientos-list/inventario-dashboard/traslado-form) | Rediseñar template/SCSS al filter-card/steps manteniendo API y comportamiento |
| **C** | sonnet | `lote-produccion/pages/filtro-select` (fork LPP) | Portar SOLO presentación: `@Input variant` + bloque steps con clases globales; lógica LPP intacta |
| **D1** | sonnet | farm-list · nucleo-list · galpon-list · inventario-historial-page | Reemplazar filters-card/grid por filter-card (cascadas → steps; resto → fields) |
| **D2a** | sonnet | config: country-list · role-management · item-inventario-list · guia-genetica-list · guia-genetica-ecuador-page · tabla-lista-registro (users) · farm-management · company-management | Tarjeta filter-card + clases de controles |
| **D2b** | sonnet | dashboard (header selects) · db-studio · cliente-list · catalogo-alimentos-list | Idem (dashboard/db-studio: solo estilo de controles si la tarjeta no aplica) |
| **D3a** | sonnet | inventario: conteo-fisico · inventario-list · kardex-list · implementacion: planes-list · plan-detail · mapas: ejecutar-modal · ejecutar-placeholder | Tarjeta filter-card + fields |
| **D3b** | sonnet | vacunacion ×3 (cronograma · registro-aplicacion · reportes-cumplimiento) · tickets ×3 (admin · gestion · mis) · sincronizacion-panama (solo historial) | Idem; vacunación: mismo bloque duplicado — aplicar consistente |
| **E** | opus | reporte-contable-main · reportes-tecnicos main · reporte-diario-costos-engorde-main · informe-semanal-engorde-list | Pantallas L: cascada→steps + fechas/radios→fields, lógica intacta |
| **F** | opus | indicador-ecuador-list · lote-engorde-list (reemplaza pasos `le-*`) · movimientos-pollo-engorde-list · aves-engorde tabs-principal (filtros secundarios) · lote-reproductora-list | Pantallas L del dominio engorde/lotes |
| **G** | opus | gestion-inventario-page (6 tabs) · traslados-aves: inventario-dashboard · historial-trazabilidad · registros-traslados | Pantallas L inventario/traslados (NO tocar modal-traslado-aves-seguimiento) |
| **H** | sonnet | graficas-principal (levante y produccion) · modal-calculos (levante y produccion) | Paneles de parámetros de gráficas → filter-card compacta |
| **FINAL** | fable (esta sesión) | seguimiento-lote-levante-list · lote-produccion-list · lote-list · movimientos-aves-list · modal-movimiento-aves · modal-create-edit-lote | Archivos que la sesión Santa Reyes Fase 3 puede estar tocando: releer + git check justo antes |

**Rondas de ejecución (secuencial; agentes de una ronda en paralelo sobre archivos disjuntos):**
R1 = A+B+C → build · R2 = D1+D2a+D2b → build · R3 = D3a+D3b+H → build · R4 = E+F → build · R5 = G → build · R6 = FINAL → build + verificación visual en preview.

## Casos de prueba (por batch)

1. `yarn build` verde.
2. Pantalla con cascada: seleccionar granja→núcleo→galpón→lote carga registros igual que antes (misma data, mismos eventos).
3. Pantalla con fechas/búsqueda: filtrar produce los mismos resultados; controles muestran focus naranja y estilo canónico.
4. Pantallas ya conformes (aves-engorde, seguimiento-diario-reproductora, lote-reproductora-ave-engorde): sin cambio visual.
5. Responsive: <1100px pasos en 2 columnas; <640px una columna.
