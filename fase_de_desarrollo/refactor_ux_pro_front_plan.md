# Plan — Refactor UX/Visual PRO del Frontend (Angular) · Paleta Italcol naranja/rojo/blanco

> **Estado:** PLANIFICACIÓN — no iniciado. Ejecutar con loop módulo-por-módulo cuando se confirmen las decisiones (§9).
> **Alcance:** SOLO front (Angular 20 standalone). No cambia contratos/lógica de back salvo cuando un módulo explícitamente requiera BD (→ migración). Nota: el stack es **Angular**, no React ("react angular" se interpreta como "todo el front Angular, 100% integrado con el mismo sistema de diseño").

## 0. Principios rectores
- **Refactor ≠ cambio de comportamiento** (CLAUDE.md): se preservan contratos API, lógica, aritmética/redondeos y textos funcionales. Solo cambia lo visual/estructural.
- **Clean code front:** componentes delgados que delegan en `funciones/` + `models/` (patrón canónico `movimientos-pollo-engorde`); sin código muerto.
- **NG0103 (memoria):** nada de getters/métodos de template que alocan arrays/objetos por ciclo → refs estables (`listaEstable`).
- **A11y + responsive:** foco visible, contraste WCAG AA, mobile/tablet/desktop.
- **Multipaís/permisos intactos:** no romper EC/PA/CO, rutas ni guards.

## 1. Sistema de diseño (design tokens) — fuente única de verdad
**Paleta DERIVADA DEL LOGO real (2026-07-03):** el logo Italcol es naranja con degradado **dorado→naranja** y "italcol" en blanco. **No tiene rojo.** Por eso el rojo queda LIBRE para peligro (sin ambigüedad CTA vs destructivo). Migrar `tailwind.config.js` de la paleta actual (naranja + **verde** + crema) a tokens **semánticos**:
- **primary (CTA)** = naranja `#F5821F` · hover `#E86F12` · pressed `#C85A0E` · tinte `#FFF4E6`.
- **accent** = dorado `#FBB040` (degradado del logo `#FDB813→#F5821F`) — highlights, badges, gráficos.
- **surface** = blanco `#FFFFFF` / fondo cálido `#FAFAF9`.
- **neutral** = grises cálidos: texto `#1C1917` · secundario `#57534E` · muted `#A8A29E` · borde `#E7E5E4`.
- **semánticos** = success `#16A34A` · warning `#D97706` · **danger `#DC2626` (rojo, ahora libre)** · info `#2563EB`.
- Definir además: tipografía + escala, spacing, radios, sombras, estados (hover/focus/disabled/loading), `focus-visible` accesible.
- Sustituir clases legacy (`ital-green`, `brand-red`, `warm-gray-*`) por los tokens nuevos, con capa de compatibilidad temporal.

## 2. Librería de componentes compartida (`app/shared/ui/`)
Estandarizar primitivas para que los módulos las **compongan** (menos CSS por módulo, consistencia, menos LOC):
Button (variants/size/loading), Input/Select/Checkbox/FormField, Card, Table (sticky header, empty/loading), Modal/Dialog, Toast, Tabs, Badge/Chip, EmptyState, Skeleton, Pagination, PageHeader.

## 3. Inventario de módulos (37) + priorización
- **Fase 0 — Fundación (PILOTO):** design tokens + UI kit + shell/layout/sidebar/topbar + **login** + **registro**. Fija el sistema visual antes de escalar.
- **Fase 1 — Alto tráfico:** dashboard · home · gestion-inventario · inventario · lote / lote-levante / lote-produccion / lote-engorde / lote-reproductora / lote-reproductora-ave-engorde · seguimiento-diario-lote-reproductora · movimientos-aves · movimientos-pollo-engorde · traslados-aves · traslados-huevos.
- **Fase 2 — Resto:** config · profile · clientes · farm · galpon · nucleo · catalogo-alimentos · gastos-inventario · aves-engorde · engorde-comun · indicador-ecuador · informe-semanal-engorde · lesiones · mapas · tickets · db-studio · reporte-contable · reporte-tecnico-administrativo · reporte-tecnico-produccion · reportes-tecnicos · (test = descartar/limpiar).

## 4. El LOOP — "Definition of Done" por módulo
Ciclo por módulo (auto-avanza al siguiente cuando queda en verde):
1. **LEVANTAR** — front + preview del módulo; screenshot base del estado actual.
2. **AUDITAR** — UX (jerarquía, consistencia, estados vacíos/carga/error, a11y, responsive) + clean-code (componentes delgados, `funciones/`+`models/`, refs estables, código muerto).
3. **REFACTOR** — aplicar design system + reestructurar SIN cambiar comportamiento/contratos.
4. **VALIDAR** — `yarn build` + preview (snapshot/inspect) + responsive (mobile/tablet/desktop) + consola limpia.
5. **PROBAR EN WEB** — recorrer flujos clave con credenciales; 0 failed requests, 0 errores consola.
6. **BD (si aplica)** — si el módulo requiere tocar BD y se prueba y cumple → **migración EF idempotente** (regla del repo) para dejar alineado; nunca romper historial EF.
7. **CERRAR** — marcar tracker + commit del módulo + pasar al siguiente. **Si algo falla → diagnosticar, corregir, re-validar; no avanzar en rojo.**

## 5. Modelo de autonomía del loop (DECISIÓN §9)
- **Opción A (recomendada) — supervisado con auto-avance:** el loop completa un módulo (DoD), reporta evidencia (screenshots + build/test) y auto-avanza; el usuario puede intervenir en cualquier checkpoint. Menor riesgo/costo, revisable.
- **Opción B — autónomo hasta terminar:** corre los 37 sin checkpoints intermedios (mayor costo/riesgo, cambios grandes sin revisión).
- Mecánica: `/loop` self-paced o `ScheduleWakeup`/cron para reanudar · `TaskList` para el estado · agentes UI/UX (`frontend-developer`) por módulo cuando se ejecute (solo si el usuario lo pide).

## 6. Guardrails (no violar)
- Refactor ≠ comportamiento (contratos, lógica, aritmética, textos funcionales).
- NG0103 refs estables · build verde (+ tests si aplica) antes de avanzar cada módulo.
- BD: solo migraciones idempotentes; confirmar DDL en prod; el deploy las aplica solo.
- No romper i18n/multipaís, permisos, rutas ni guards.

## 7. Tracking
`tracker_estado.md` = checklist de los 37 módulos por fase + evidencia (build/test/screenshots/migración) por módulo.

## 8. Riesgos
- **Rojo como primario** choca con la convención "rojo = peligro" → obligatorio separar rojo-marca (CTA) de rojo-danger (destructivo) en tonos distintos.
- Volumen (37 módulos) → el loop debe ser incremental y reversible (commit por módulo).
- Componentes con lógica pesada (seguimientos, inventario) → refactor visual sin tocar la lógica validada de descuento/consumo.

## 9. Decisiones a confirmar antes de arrancar el loop
1. **Rol del rojo:** ¿rojo = CTA primario (bold, marca) o naranja primario + rojo de acento? + confirmar rojo-danger separado.
2. **Autonomía:** A (supervisado, recomendado) vs B (autónomo hasta terminar).
3. **Piloto:** fijar el sistema en login/registro + shell antes de escalar (recomendado).
