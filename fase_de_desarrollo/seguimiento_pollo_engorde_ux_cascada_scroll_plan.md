# Plan — UX cascada numerada + info colapsable + scroll único en «Seguimiento diario pollo de engorde»

**Fecha:** 2026-07-22
**Solicitud:** (1) aplicar el diseño del módulo Seguimiento Diario Reproductora (header con icono, «Selección de contexto» con pasos numerados) al módulo Seguimiento diario pollo de engorde; (2) hacer **colapsable** la sección «Información del lote y disponibilidad de aves» (hoy tapa la tabla/tabs); (3) reducir los **3 scrolls** actuales (tabla, contenedor principal, navegador) a **uno solo**.

---

## Enfoque arquitectónico

- **Front-only, solo presentación** + 1 flag de UI (`infoLoteOpen`). Sin cambios de contratos, HTTP ni lógica de negocio.
- **Diagnóstico del triple scroll:**
  - Shell: `.app-main` (app.component) es scroller con topbar **fija** de 3.25rem (padding-top). El módulo usa `.ux-page { height: 100vh }` → desborda 3.25rem → scrollbar externo («navegador»).
  - `.ux-main { overflow: auto }` → scroll interno del contenido («primera parte»).
  - `.tabs-content { max-height: 70vh; overflow-y: auto }` + `.ux-scroll--diario { max-height: min(70vh,720px) }` (SCSS compartido `tabs-principal` de levante, compilado por `@use` en la copia de engorde) → scroll de la tabla.
- **Fix:** `.ux-page` → `height: 100%` (encaja exacto en el content-box de `.app-main` → muere el scroll externo); overrides **scoped a engorde** en `tabs-principal-engorde.component.scss` (después del `@use`, no afecta a levante): `.tabs-content { max-height: none; overflow: visible }` y `.ux-scroll--diario { max-height: none }` (conserva `overflow` para el scroll **horizontal** de la tabla ancha). Resultado: **un único scroll vertical** en `.ux-main`.
  - Trade-off asumido: el `thead` sticky de la tabla deja de "pegarse" (su contenedor ya no scrollea verticalmente) — mismo comportamiento que el módulo de referencia.
- **Filtros con pasos numerados:** el filtro vive en el componente **compartido** `app-filtro-select` (lote-levante) usado por varios módulos → se agrega `@Input() variant: 'toolbar' | 'steps' = 'toolbar'` (opt-in). `'toolbar'` = markup actual intacto (cero cambios para los demás consumidores); `'steps'` = tarjeta «Selección de contexto» con pasos 1→4, conectores y estados activo/completado/deshabilitado (mismos bindings, mismos `disabled`, placeholders `disabled` conservados). Numeración dinámica: si `showNucleoGalpon=false`, Lote es paso 2.
- **Info del lote colapsable:** header clickeable (título + resumen «Aves disp.: N» + chevron), **colapsada por defecto**; las alertas (sin aves / lote cerrado / lote operativo cerrado) quedan **siempre visibles** (una línea, explican por qué se bloquea la creación); el cuerpo (Datos del lote + Aves disponibles con chips de género) solo al expandir, con el patrón `info-card` de la referencia.
- **Header:** patrón `ux-header` (icono reloj, título naranja, subtítulo, breadcrumb chips Granja › Núcleo › Galpón › Lote + edad + contador de registros + chip de lote cerrado, botones Liquidar/Abrir + Nuevo registro).
- **Tabs como pills** (look de la referencia) vía override scoped en el SCSS de tabs de engorde.
- **Fix de paso:** el template usa `$safeNavigationMigration(...)` (artefacto de migración que NO existe en ningún TS → TypeError en runtime al evaluar el chip de edad) → se reemplaza por el acceso directo `selectedLote?.fechaEncaset`. Los demás archivos con ese artefacto quedan fuera de alcance (se registra tarea aparte).

## Archivos a modificar

| Archivo | Cambio |
|---|---|
| `features/aves-engorde/pages/seguimiento-aves-engorde-list/…component.html` | Header `ux-header` + breadcrumb; `<app-filtro-select variant="steps">`; hint `empty-state` sin lote; info-card colapsable; fix `$safeNavigationMigration`. Modales y `app-tabs-principal-engorde` sin tocar. |
| `…component.ts` | Solo `infoLoteOpen = false`. |
| `…component.scss` | Reescritura: tokens + shell (`ux-page` 100%, `ux-main` único scroll) + header/breadcrumb/btn/info-card colapsable/empty-state; se elimina el CSS legado muerto (ux-modal, ux-form, ux-table, ux-toolbar, chips viejos, etc. — nada de eso lo usa este template). |
| `features/lote-levante/pages/filtro-select/filtro-select.component.{ts,html,scss}` | `@Input() variant` opt-in + markup variante steps + estilos de pasos (tokens Italfoods). Variante default intacta. |
| `features/aves-engorde/pages/tabs-principal-engorde/tabs-principal-engorde.component.scss` | Overrides scoped: sin max-height en tabs/tabla (scroll único) + tabs como pills. |

## Cambios de BD / SQL

**Ninguno.**

## Reglas de negocio (se preservan)

- Cascada y reseteos: mismos handlers/eventos del filtro compartido; `disabled` de selects idéntico.
- Permisos (`liquidar_lote`, `abrir_lote`, `cuadrar_…`, editar/eliminar) intactos.
- Alertas de bloqueo de seguimiento: mismas condiciones y textos (se elimina solo una rama de hint **muerta** — condición contradictoria `total === 0 && total > 0` que jamás renderiza — y se simplifica una condición redundante equivalente).

## Casos de prueba

1. `yarn build` (Node portable) → 0 errores; solo warning de bundle budget preexistente.
2. Un solo scroll vertical: página encaja en el shell (sin scrollbar de `.app-main`), tabs y tabla crecen; el scroll horizontal de la tabla ancha se conserva.
3. Pasos 1→4 numerados con estados y conectores; variante toolbar intacta en los demás módulos que usan `app-filtro-select` (levante, traslados, etc.).
4. Info del lote: colapsada por defecto con resumen «Aves disp.»; expande/colapsa; alertas siempre visibles.
5. Chip de edad ya no depende de `$safeNavigationMigration` (sin TypeError en consola al seleccionar lote).
