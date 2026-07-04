# Tracker — Refactor UX/Visual PRO del Front (paleta Italcol naranja/dorado/blanco)

> Plan: [`fase_de_desarrollo/refactor_ux_pro_front_plan.md`](fase_de_desarrollo/refactor_ux_pro_front_plan.md) · Paleta: [memoria paleta-marca-italcol]
> Modo: autónomo hasta terminar · Piloto primero (login/registro + shell) · BD tocada+probada+cumple → migración.

## Fase 0 — Fundación / Piloto
- [x] Tokens de marca en `tailwind.config.js` (ADITIVOS: primary/accent/success/warning/danger/info/ink/muted/line/canvas/surface) — legacy intacto
- [x] **Login** migrado a la paleta: gradientes dorado→naranja (sin rojo), sombras limpias, fondo blanco cálido, footer minimal, link/checkbox naranja — recompila OK, sin cambio de comportamiento
- [x] **Registro**: N/A — no hay registro público (usuarios se crean en config); auth = login + recuperar (ambos migrados)
- [x] **Menú/sidebar PRO** (agente): estado activo naranja, header con acento naranja→dorado, avatar gradiente, focus WCAG, hover 150ms, label "NAVEGACIÓN", scrollbar prolija, logout en rojo-peligro. Routing/árbol de menú INTACTO. Validado: renderiza pro, nav funciona, 0 verde, consola limpia.
- [x] **Auditoría color residual** (agente): ~25 archivos — greens de marca/naranja legacy en `.ts` de charts (2 series Real/Guía), tokens `--*-green*` stale, hovers Tailwind arbitrarios. PRESERVÓ verdes semánticos (éxito/ok/activo → `#16A34A`) y paletas categóricas de charts. `yarn build` 0 err.
- [x] **VALIDACIÓN FINAL**: `yarn build` completo 0 errores (ambos agentes + trabajo previo) · menú pro validado en vivo · 0 verde residual · consola limpia.
- [x] **Recuperar contraseña** migrado (consistente con login; verde solo "éxito" → `#16A34A`) — recompila OK, screenshot validado
- [x] **Rebrand GLOBAL verde→naranja (toda la app de una)**:
  - `styles/theme-italfoods.scss`: `--ital-green*`→naranja, bordes verdes→neutro, `.btn-danger` (bug: era verde)→rojo `--danger`, +tokens `--success/--danger`
  - `tailwind.config.js`: legacy `ital-green*`→naranja (242 clases `*-ital-green`), `ital-orange`/`brand-red` afinados al palette exacto
  - Sweep de hex hardcodeado en 31 archivos (#2d7a3e/#1e5c2a/#3d9b52…→naranja); semántico `.diferencia-ok`→verde `--success`
  - `yarn build` OK · gestion-inventario validado (header verde→naranja `#F5821F`, consola limpia) · 0 verde de marca restante
- [ ] Pulido por módulo (contraste texto celda→neutro, spacing, charts .ts con verde) — Fase 1
- [ ] **Shell** afinado: sidebar/topbar (ya naranja) + CSS vars globales
- [ ] UI kit `shared/ui` (Button, Input, Card, Table, Modal, Toast, Tabs, Badge, EmptyState…)

## Fase 1 — Alto tráfico (paso PRO por módulo: UX/IX + botones/transiciones + reducción de peticiones + código)
### gestion-inventario (en progreso)
- [x] Rebrand validado (0 verde, header naranja)
- [x] **Auditoría de peticiones**: carga inicial = 3 req (lean, 0 duplicadas). Hallazgo: cambiar de tab re-fetcheaba `historico-filtros` (meta ESTÁTICA) en cada visita.
- [x] **Optimización**: cache de sesión en `loadHistoricoMeta` (guard `if(this.historicoMeta) return`) → secuencia Histórico→Stock→Histórico pasó de 5→4 req; `movimientos` sigue refrescando (datos vivos). Validado + consola limpia.
- [x] **Perf: paginación client-side del histórico** (100/página, 30 págs) → DOM de 3000→100 filas (30× menos nodos); export CSV sigue exportando todo; controles Anterior/Siguiente. Validado (página 2 de 30, consola limpia).
- [x] gestion-inventario ITERACIÓN PRO COMPLETA (rebrand + cache 5→4 + paginación). Sin errores.
- [ ] (2ª pasada opcional: skeletons de carga · dirty-flag stock · validar Colombia/Panamá)

### Loop → siguiente módulo
- [ ] Auditar+optimizar: dashboard/inventario · lotes (levante/produccion/engorde/reproductora) · seguimientos · movimientos-aves · movimientos-pollo-engorde · traslados

## Fase 2 — Resto (config, profile, clientes, farm, galpon, nucleo, catalogo-alimentos, gastos-inventario, aves-engorde, engorde-comun, indicador-ecuador, informe-semanal-engorde, lesiones, mapas, tickets, db-studio, reporte-contable, reporte-tecnico-*, reportes-tecnicos)
- [ ] (checklist por módulo al llegar)

## Definition of Done por módulo
levantar → auditar (UX+clean-code) → refactor (sin cambiar comportamiento) → `yarn build` + preview + responsive + consola limpia → probar en web → (si BD: migración idempotente) → cerrar + commit → siguiente.

## Evidencia
- Fase 0: tailwind tokens OK; login recompila limpio (bundle complete), screenshot validado (paleta aplicada, sin rojo de marca).
- Rebrand global: `yarn build` completo (0 err); gestion-inventario header `#2d7a3e`→`#F5821F` (inspect).
- **Recorrido de validación (loop, admin Ecuador) — cada opción: 0 verde residual + consola limpia:**
  - login · recuperar-contraseña (screenshots)
  - home/inicio · gestion-inventario (header naranja) · config/farm-management (galpones, screenshot) · daily-log/produccion · config/role-management · movimiento-pollo-engorde/venta (screenshot) · profile · daily-log/seguimiento (levante)
  - Escáner DOM `rgb(45,122,62)` = 0 en todas · `preview_console_logs` = sin errores en todas.

## MAPA COMPLETO — 37 módulos (secuencia + estado real tras auditoría de código)
> Hallazgo clave de la auditoría: **la app ya está bien construida** — 26 componentes de lista YA paginan (`pageSize`/`slice`); peticiones lean (filter-first, 1-2 req/carga); el rebrand es global. No hay backlog grande de perf; las "gaps" restantes son listas acotadas que no requieren paginación.
>
> Leyenda: R=rebrand (global ✅ para todos) · P=peticiones · Pag=paginación
>
> **Fase 1 (alto tráfico)**
> - gestion-inventario — R✅ · P✅(3→lean, cache 5→4) · Pag✅(histórico 3000→100) · **ITERACIÓN PRO COMPLETA**
> - lote-levante (seguimiento) — R✅ · P✅(filter-first, 1 req) · Pag✅(ya tenía)
> - lote-produccion — R✅ · P✅(1 req) · Pag✅(ya tenía)
> - lote-engorde / aves-engorde — R✅ · Pag: lista acotada (no requiere)
> - lote-reproductora / -ave-engorde — R✅ · Pag✅(ya tenía)
> - seguimiento-diario-lote-reproductora — R✅ · Pag✅(ya tenía)
> - movimientos-aves — R✅ · Pag✅(ya tenía)
> - movimientos-pollo-engorde — R✅ · P✅(filter-first, 1 req) · Pag✅(ya tenía)
> - traslados-aves / traslados-huevos — R✅ · Pag✅(ya tenían)
> - inventario (dashboard/kardex) — R✅ · P✅(2 req) · Pag✅(kardex ya tenía)
>
> **Fase 2 (resto)**
> - config: farm-management✅ · role-management✅ · company-management✅ · clientes✅(pag) · item-inventario-ecuador✅(pag) · guia-genetica✅(pag) · catalogo-alimentos✅(pag)
> - farm · galpon · nucleo(pag) — R✅
> - reportes: reportes-tecnicos · reporte-tecnico-produccion · reporte-contable · indicador-ecuador(pag) · informe-semanal-engorde(pag) — R✅ (tablas de reporte acotadas por período)
> - tickets(pag) · mapas · db-studio · lesiones · engorde-comun · profile · home — R✅
>
> **Por país:** el rebrand + paginación son transversales (no dependen de país). Validado en vivo con Ecuador (company 3) y Colombia (company 1). Panamá: mismo código, sin diferencia de UI.
>
> **Pendiente real (marginal, 2ª pasada):** skeletons de carga homogéneos · texto de celda neutro (requiere sweep de componentes con tabla propia para no partir consistencia) · virtual-scroll donde una lista acotada creciera.
