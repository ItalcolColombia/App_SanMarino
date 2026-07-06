# Plan — Sistema de diseño compartido (`shared/ui/`) + reducción de duplicación front

> **Fase:** planeación (2 agentes especializados ya corrieron el análisis). **Sin código funcional todavía.**
> **Decisión pendiente (gate):** enfoque de librería (ver §3). Todo lo demás depende de eso.

---

## 1. Diagnóstico (síntesis de los 2 agentes)

**El problema NO es falta de estilo — es falta de componentización.** El front ya tiene un design language Italcol maduro (vocabulario `.ux-*` en ~60k líneas de SCSS: tabla, modal, filtros, botones, badges, empty-state). Pero está **copiado a mano en 88 tablas y 163–166 modales** en vez de vivir en componentes parametrizables.

Escala medida (37 módulos, 171–177 componentes):
- **88 templates con `<table>`**, 4.850 tags de fila/celda, 566 loops de fila.
- **Paginación manual en 38 archivos**, **ordenamiento manual en 54**, empty-state en 71, spinner/loading en 115.
- **28 componentes `modal-*`** + 43 overlays a mano; **21 archivos usan `confirm()` nativo** (coexiste con el `confirmation-modal` que ya existe, adoptado por 19).
- **3 mecanismos de mensajes:** `ToastService` (21), `alert()` nativo (17), strings inline (23).
- **18 archivos con `xlsx`** (13–15 repiten el mismo armado header/hoja/nombre).
- **Filtro compartido `hierarchical-filter` existe pero solo lo usa 1 módulo**; 70 archivos rehacen la cascada empresa→granja→lote a mano.
- **Solo 1 de 37 módulos** (`movimientos-pollo-engorde`) cumple el patrón canónico completo (`funciones/`+`models/`+orquestador delgado). 89% no usa `funciones/`, 70% no usa `models/`.
- Componentes gigantes: `lote-levante/modal-create-edit` **2.220**, `engorde-comun/modal-seguimiento-engorde` **2.134**, `traslados-aves/inventario-dashboard` **1.659** (+2.387 scss), `lote/lote-list` **1.593** (3 sorts casi idénticos), `gestion-inventario-page` **1.565**.

**Potencial de reducción:** ~13–19k líneas en features (~18–26% del código de features), hasta ~40–60k incluyendo HTML+SCSS de todo el front. Sin tocar lógica de negocio.

---

## 2. Objetivo

Extraer los patrones repetidos a un **sistema de diseño compartido `shared/ui/`** de componentes genéricos parametrizables + helpers heredados, replicando el patrón canónico (`funciones/` puras + `models/` + orquestador delgado ≤ ~300 líneas), **preservando 100% el comportamiento, los contratos, la aritmética/redondeos y el rebrand Italcol** (refactor ≠ cambio de comportamiento).

---

## 3. DECISIÓN GATE — enfoque de librería

| Opción | Qué implica | Trade-off |
|---|---|---|
| 🥇 **A — Abstracción propia sobre `@angular/cdk`** (ya instalado, hoy usado en 1 archivo), envuelta en `shared/ui/`, reusando el `.ux-*` existente | El CDK aporta lo difícil y accesible (Overlay/Dialog con foco+ESC+scroll-lock+a11y, `cdk/table`+`DataSource`, `Sort`, virtual-scroll, Menu). Nosotros construimos `data-table`, `modal-shell`, etc. con nuestro SCSS/tokens | **Cero riesgo de rebrand, sin dependencias nuevas, control total.** Contra: construimos/mantenemos el data-table |
| 🥈 **B — PrimeNG 20** (`p-table` cubre las 88 tablas de fábrica: sort/paginación/filtro/lazy/virtual/export), tematizado a `#F5821F` vía design tokens + `tailwindcss-primeui` | Menos código inicial (la tabla viene lista) | Contra: reconciliar su CSS base con `.ux-*` (o migrar `.ux-*` a tokens PrimeNG), dependencia de vendor pesada, riesgo de choque visual |
| ❌ Material / ng-zorro / DaisyUI / Flowbite | — | Descartadas: choque de look (doble rebrand) o duplican lo que ya existe sin aportar comportamiento |

**Recomendación:** **Opción A** — mejor honra el estado del repo (design language maduro + metodología estricta de clean-code), riesgo mínimo de rebrand. PrimeNG queda documentado como escape si el data-table propio se vuelve pozo de mantenimiento.

---

## 4. Arquitectura objetivo

```
frontend/src/app/shared/ui/
├── README.md                       # convención + inventario + regla de tamaño
├── data-table/                     # orquestador delgado OnPush
│   ├── models/ (column-def, row-action, table-config)
│   └── funciones/ (ordenar-filas, paginar-filas, filtrar-filas)   # PURAS
├── modal-shell/                    # overlay+head+body+foot sobre cdk/dialog
│   └── models/ (dialog-config)
├── dialog.service.ts               # open<T>() → ref (reemplaza isOpen/backdrop/ESC a mano)
├── confirm-dialog/                 # migra confirmation-modal a dialog.service (misma API)
├── filter-bar/  + entity-filter/   # generaliza hierarchical-filter + cascada empresa→granja→lote
│   └── models/ (filter-def)
├── search-box/                     # input "Buscar…" unificado (~30 hoy a mano)
├── button/ · form-field/ · page-header/ · empty-state/ · badge/
├── export-button/                  # orquestador que llama al helper de excel
├── icons/                          # set FontAwesome centralizado (reemplaza 184 SVG inline)
└── index.ts                        # barrel
frontend/src/app/shared/utils/
├── excel/exportar-tabla-excel.funcion.ts (+ export-multi-sheet) + models/excel-column
└── format.ts                       # fecha/número/date-stamp (reemplaza ~50 helpers duplicados)
```

- **data-table:** inputs `columns: ColumnDef<T>[]` (key, header, sortable, align, width, formatter, cellClass), `rows` (referencia ESTABLE), `rowActions`, `pagination`, `sortable`, `loading`, `emptyMessage`. Sort/paginación/filtro = **funciones puras**; filas visibles vía `computed()`/signals (compatible OnPush + NG0103). Slots `#cellTemplate` para casos especiales.
- **modal-shell/dialog.service:** el CDK da foco-trap, ESC, scroll-lock, `aria-modal` (mejora a11y que hoy falta). `confirm-dialog` consume el service preservando `ConfirmationModalData`.
- Cada `ui-*` = OnPush, ≤ ~300 líneas ts / ≤ ~250 html; lógica grande → `funciones/`.

---

## 5. Fases de trabajo (por ROI + riesgo)

### Fase 0 — Quick wins de adopción (bajo riesgo, ya existe) 
Máxima ganancia/mínimo riesgo: adoptar lo que YA existe pero no se usa.
- Migrar los **21 `confirm()` nativos → `confirmation-modal`**.
- Migrar **17 `alert()` + 23 mensajes inline → `ToastService`**.
- Centralizar **`shared/utils/format.ts`** (fecha/número/date-stamp) — reemplaza ~50 helpers + 44 date-stamps.
- Centralizar **`exportToExcel` helper** (+ `exportMultiSheet`) — colapsa 13–15 exports; `readExcel` aparte para los 3 de lectura.

### Fase 1 — Fundaciones `shared/ui/` + piloto medible
- Construir `data-table` + `modal-shell`/`dialog.service` + `filter-bar`/`search-box` (con `funciones/`+`models/`+tests de equivalencia).
- **Piloto:** migrar 1 componente gigante end-to-end (candidato: `lote/lote-list` 1.593 o `traslados-aves/inventario-dashboard` 1.659) → medir líneas ahorradas, validar comportamiento idéntico + rebrand + build.

### Fase 2 — Migración masiva por módulo
- Rodar los genéricos por los 37 módulos, **un módulo por PR**, partiendo los gigantes en `funciones/`+`models/` hasta caer bajo el límite. Prioridad: top-12 gigantes primero.
- `entity-filter` + `search-box` + `page-header` + set de iconos en el camino.

### Fase 3 — Consolidación
- Barrer SCSS muerto (los .scss gigantes que estilaban tablas/modales ahora centralizados), README/convención, y si se puede una regla de lint de tamaño de archivo.

---

## 6. Reglas (vinculantes)
- **Refactor ≠ cambio de comportamiento:** UI, contratos, lógica, aritmética/redondeos idénticos.
- **Tests de equivalencia** antes de reemplazar cada consumidor: sort, paginación, export Excel (contenido/orden), redondeos, formato.
- **OnPush** en todos los `ui-*`; **referencias estables** (regla NG0103 — no getters que alocan por ciclo).
- **Un módulo por PR**, `yarn build` (node portable 22.23.1) + `yarn test` en cada paso, sin procesos huérfanos.
- No romper imports externos: mover tipos a `models/` y re-exportar desde el barrel.

## 7. Casos de prueba
- Sort/paginación de `data-table` == comportamiento previo (mismos datos, mismo orden).
- Export Excel: hoja/headers/nombre-archivo/valores idénticos al export original.
- Redondeos/formato numérico idénticos (`format-decimal`).
- Modal: foco atrapado, ESC cierra, restaura foco (a11y) sin cambiar el flujo.
- Rebrand: smoke visual por módulo (naranja/dorado, verde solo éxito, rojo solo peligro).
- Build 0 errores + tests verdes por cada módulo migrado.

## 8. Riesgos
1. Romper rebrand (bajo con A, alto con B) → con A reusamos SCSS; smoke visual por módulo.
2. NG0103/change-detection → OnPush + `computed()` + auditar consumidores.
3. Equivalencia funcional (sort/export/redondeo) → tests antes de migrar.
4. Volumen (88 tablas + 163 modales + 37 módulos) → incremental, 1 módulo/PR, gigantes primero.
5. a11y de modales → migrar a CDK Dialog mejora, pero validar teclado.
