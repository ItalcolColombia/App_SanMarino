# Tracker — Implementación empresa Santa Reyes (Colombia, postura comercial)

Plan: [fase_de_desarrollo/santa_reyes_implementacion_plan.md](fase_de_desarrollo/santa_reyes_implementacion_plan.md)

## Fase 0 — Levantamiento y diseño
- [x] Leer 3 Excel del requerimiento (Granja/Items/Lotes)
- [x] Mapear código: empresa/roles/usuarios/menús (hash PBKDF2 Identity, company_menus+role_menus por route)
- [x] Mapear código: granjas (form vivo = modal FarmList; patrón campos por país Panamá)
- [x] Mapear código: lotes + guía genética (bloqueo duro en LoteService + form; BD nullable)
- [x] Mapear código: clasificación huevos (11 columnas fijas, ~10 consumidores; metadata jsonb disponible)
- [x] Mapear código: traslados/edades (sin cohortes; cierre manual sem 26)
- [x] Mapear patrón multi-empresa (flags tipados en companies; company_menus)
- [x] Escribir plan maestro
- [x] Tracker actualizado

## Fase 1 — Empresa + estructura ERP + seeds + guía condicional
### Backend
- [x] Generar hash PBKDF2 de "123456789" (PasswordHasher<Login>) — V3 HMACSHA512 100k, verificado
- [x] Entidades/Configurations: Company.ManejaCodigosErpAvicola, Farm (6 campos ERP), Nucleo (bodega), Galpon (ubicación ERP), Lote (centro de costo), FarmSilo (nueva)
- [x] Migración #1 `20260725175311_AddInfraErpAvicolaSantaReyes` (columnas + farm_silos + defensivas menus, idempotente; aplicada en local + doble pasada psql OK)
- [x] Migración #2 `20260725190000_SeedEmpresaSantaReyes` (empresa+país+flag+regional+roles+permisos+menús 29+usuarios+granja+3 núcleos+38 galpones+39 silos+catálogo 310+inventario 45+10 lotes+espejos; idempotente con doble pasada y Down/Up simétrico)
- [x] DTOs + services: Company/Farm/Nucleo/Galpon/Lote pass-through campos nuevos (contrato camelCase confirmado)
- [x] Guía condicional: `GuiaGeneticaRequisitoCalculos` + uso en LoteService.Create/Update (con guía = byte a byte igual)
- [x] Tests xUnit `GuiaGeneticaRequisitoCalculosTests` (17 tests nuevos)
- [x] `dotnet build` + `dotnet test` verdes (660/660)
### Frontend
- [x] `ActiveCompanyConfigService` (flags empresa activa, caché TTL 5 min, invalida por `session$`, fail-closed)
- [x] Form granja (modal FarmList): sección Códigos ERP condicional al flag (+ bloque en el modal de detalle)
- [x] Gestión Granjas: bodega núcleo + ubicación ERP galpón condicionales
- [x] Form lote: centro de costo condicional + guía condicional (sin guía → raza libre opcional; con guía → selects required)
  - [x] `modal-create-edit-lote` (pedido en el plan)
  - [x] `features/lote/components/lote-list` ← **es el form VIVO** del menú `/config/lote-management`
- [x] Modelos TS actualizados (Farm/Nucleo/Galpon/Lote/Company)
- [x] `yarn build` verde (único warning: bundle budget preexistente)
### Validación
- [x] `dotnet ef database update` en BD local (:5433) sin error (schema + seed aplicadas; 661/661 tests)
- [x] Verificación SQL de seeds (conteos exactos + idempotencia re-run 0 cambios + fases de lotes: 9 Producción/1 Levante coherentes)
- [x] Smoke UI: login admin@santareyes.com OK → menú clonado sin engorde/Panamá → granja con campos ERP hidratados (B0601/830/B06) → 3 núcleos + 38 galpones → 10 lotes (9 Prod/1 Lev) → editar lote con raza libre + asignar núcleo/galpón guardado vía API (PUT 200)
- [x] FIX hallado en smoke (preexistente): `lote-list` borraba `loteNombre` al editar en granjas sin lote base (2 puntos: `filterBaseLotesByGranja` + `onBaseLoteChange`) → guardado con `this.editing`
- [x] FIX hallado en smoke (gap operativo): al asignar núcleo/galpón a un lote con producción abierta, el espejo `lote_postura_produccion` quedaba sin ubicación y el seguimiento no lo encontraba → `LoteService.UpdateAsync` rellena solo-si-vacío (build 0/0 + 685 tests verdes)

## Fase 2 — Clasificación de huevos por ítems (Primera/Pnc)
- [x] Flag `clasificacion_huevo_por_items` (migración `20260725200000_AddClasificacionHuevoPorItems` — renombrada a mano para ordenar DESPUÉS del seed; aplicada en local, Santa Reyes=true)
- [x] Backend: `huevoItems` en request POST/PUT /api/Produccion/seguimiento → valida catálogo huevo de la empresa dueña de la granja + flag; huevo_tot=suma, huevo_inc=0, 11 columnas=0, desglose a metadata.huevoItems conservando claves previas; semántica null=no tocar / []=quitar; 25 tests nuevos (685/685 + 1/1)
- [x] Front: flag `clasificacionHuevoPorItems` en `ActiveCompanyConfigService` (+ `Company` model) — fail-closed
- [x] Front: modal producción con filas dinámicas de ítems huevo (condicional al flag) — select agrupado Primera/Pnc desde `GET /api/catalogo-alimentos/filter?typeItem=huevo`, sin duplicados (ToastService), total memoizado, payload `huevoItems` + 11 columnas en 0, rehidratación desde `metadata.huevoItems` al editar; flag OFF = flujo actual intacto
- [x] Front: `produccion.service.ts` con `HuevoItemSeguimiento` + `huevoItems?` en el request + `leerHuevoItemsDeMetadata()` (lectura defensiva)
- [x] Validación: `yarn build` verde (único warning: bundle budget preexistente)
- [x] Smoke UI Santa Reyes: modal producción con "Clasificación de huevos (Primera/Pnc)" — 21 ítems reales en optgroups, total automático (100+5=105), POST /Produccion/seguimiento 201, BD verificada (huevo_tot=105, huevo_inc=0, 11 columnas=0, metadata.huevoItems completo); registro de prueba eliminado

## Fase 3 — Edades por cohorte + liquidación manual (EN CURSO — agentes back+front)
- [x] Flag `permite_traslado_aves_cross_etapa` en `Company` + Configuration + DTOs (Company/Create/Update) + TODAS las proyecciones (ToDto, Crud, CompanyResolver ×2, CompanyPaisService); migración `20260725210323_AddCohortesTrasladoCrossEtapa` (true solo Santa Reyes) — el timestamp `210000` ya lo tomó la migración `FnMoverUbicacionCopiaBodegaNucleo` de la otra sesión, así que se conservó el generado (igual corre después del seed `190000`)
- [x] Tabla `lote_aves_cohortes` + entidad `LoteAvesCohorte : AuditableEntity` (fechas `DateOnly` ↔ `date`) + `LoteAvesCohorteConfiguration` (FK lotes Restrict + 3 índices) + DbSet
- [x] Registro de cohorte en TODO traslado (misma transacción, ligada al `movimiento_aves_id`; `fecha_encaset` del lote origen → espejo → si ambas null NO crea cohorte y el traslado no falla)
- [x] Cross-etapa levante→producción por flag (decisión pura `LoteCohortesCalculos` + 34 tests; empresa por `farms.company_id` del origen, fail-closed, destino misma empresa; flag off = mensaje de bloqueo EXACTO)
- [x] Refactor `TrasladoAvesDesdeSegService` a partial (ancla + `Funciones/…Traslado.cs` + `…Cohortes.cs`) con patas Levante/Producción reutilizables — sin cambiar aritmética
- [x] Reversión: `MovimientoAvesService.EliminarMovimientoAsync` soft-deletea las cohortes del movimiento
- [x] `GET api/traslados/cohortes/{loteId}` (edad propia + cohortes con edad actual, scope por empresa de la granja)
- [x] Front: flag `permiteTrasladoAvesCrossEtapa` en `ActiveCompanyConfigService` + modelo `Company` (fail-closed, `=== true`)
- [x] Front: selector etapa destino en modal traslado (flag on + origen levante; flag off/origen producción = comportamiento actual exacto) + aviso de cohorte
- [x] Front: bloque "Edades en el lote" (`traslados-aves/components/edades-lote`, `models/` + `funciones/`) integrado en tab General de seguimiento Levante y Producción, con refresco por trigger tras traslado
- [x] Front: `TrasladosAvesService.getCohortesLote(loteId)` tipado (loteId = lote BASE)
- [x] Front: `yarn build` verde (único warning: bundle budget preexistente)
- [x] Build + tests verdes (0/0 warnings-errores; 719/719 + 1/1) + migración aplicada local + `has-pending-model-changes` limpio + doble pasada del SQL por psql sin cambios
- [x] Smoke local (harness temporal, revertido): cross-etapa Levante 143 → Producción 141 con las 2 patas (SALIDA en `seguimiento_diario_levante` + INGRESO en `seguimiento_diario_produccion`, acumulados y contrapartes correctas), cohorte con edad del ORIGEN (22 sem) distinta de la propia del receptor (57 sem); misma etapa Levante→Levante idéntica al comportamiento previo; bloqueo exacto con flag off; scope ajeno → 404; BD local restaurada al estado previo

## Fase 4 — Demo lista para evaluación (flag off, flujo clásico)
- [x] Auditoría Demo: flags SR en false (aislamiento OK), Colombia, 25 menús completos, 3 usuarios activos (admin.demo@zootecnico.com), 7 lotes (1 Producción), guía 2026 cargada (224 filas → flujo clásico con selects obligatorios), catálogo 61 y master lists en paridad. Faltan: item_inventario alimento (1 con typo vs 61) y sobra menú "Integración Panamá". Seguimientos en 0 = esperado (evaluador registra)
- [x] Migración `20260725230000_AlistarDemoParaPruebas`: 61 ítems alimento company 1→Demo (NOT EXISTS por company+codigo, todas las columnas + pais_id, Demo 1→62), fix typo "Alimneto ERP"→"Alimento ERP", menú Panamá fuera de Demo (company_menus 4→3 y role_menus 14→12, solo roles exclusivos de Demo; company 1 intacta). Aplicada en local + doble pasada psql (INSERT 0 / UPDATE 0 / DELETE 0) + `has-pending-model-changes` limpio
- [x] Smoke Demo (login admin.demo@zootecnico.com, hash estampado SOLO en BD local): menú sin "Integración Panamá", modal granja SIN sección ERP, form lote clásico (select raza obligatorio AP/APN/C500 de la guía 2026, sin texto libre, sin centro de costo); ítems alimento 62 + clasificadora clásica verificados por BD + mismo mecanismo de flag validado en UI

## Fase 5 — Reportes levante/producción adaptados a SR
- [x] Auditoría de reportes/indicadores — nada truena para SR (huevo_tot/%postura/HTAA correctos; divisiones guardadas; levante y liquidaciones sin dependencia de huevos). En ceros a adaptar: fila "Clasificadora semana" (tabla-lista-indicadores), columnas H.* (tabs-principal), Rep. Técnico Producción pestaña Clasificación (menú retirado GLOBAL desde 2026-07-06, no es tema SR) y 2 columnas de Rep. Contable mov. huevos (HVO COMERCIAL / HUEVO DESECHO)
- [x] fn SQL desglose Primera/Pnc desde metadata.huevoItems (`backend/sql/fn_clasificacion_huevo_items_produccion.sql`, misma resolución de lote/UNION/DISTINCT ON y misma fórmula de semana que `fn_indicadores_produccion_postura` → semanas 1:1) + `IndicadoresProduccionService.ObtenerClasificacionHuevoItemsAsync` (SqlQueryRaw, resolución de lote compartida con indicadores) + endpoint `POST /api/Produccion/clasificacion-huevo-items` → `List<ClasificacionHuevoItemSemanaDto>` `{semana,tipoHuevo,codigo,nombre,cantidad}`; migración `20260725220000_AddFnClasificacionHuevoItemsProduccion` (data-only, Designer clonado) aplicada en local; smoke SQL en transacción con ROLLBACK (2 ítems Primera + 1 Pnc, agregación por semana, filtros, lote inexistente = 0 filas sin error) + verificación del mapeo EF→JSON camelCase
- [x] Front: 4 pantallas gateadas por flag — indicadores con bloque "Clasificación por ítem (Primera/Pnc)" por semana (funciones puras en `funciones/`, validadas por ejecución), grilla Seguimiento con columnas Primera/Pnc por fila desde metadata (11 columnas + Huevos Inc. ocultas), pestaña Clasificación oculta en Rep. Técnico Producción (sin request), columnas HVO COMERCIAL/HUEVO DESECHO ocultas en Rep. Contable; flag off = byte a byte; `yarn build` verde
- [x] Smoke doble UI: Demo intacto (ver Fase 4) / SR: seguimiento con ítems 201 (200 Primera + 12 Pnc), grilla con columnas Primera/Pnc (H.Limpio y Huevos Inc. ocultas), pestaña Indicadores con bloque "Clasificación por ítem (Primera/Pnc)" alimentado por `POST /api/Produccion/clasificacion-huevo-items → 200` con detalle real por ítem, y bloque "Edades en el lote" visible; registro de prueba eliminado
- [ ] Follow-up chico anotado: exports Excel de indicadores/seguimiento siguen con las 11 columnas (en 0 con flag on) — desglose Primera/Pnc en .xlsx si se pide

## Transversal
- [x] CLAUDE.md: sección "🏢 Features por EMPRESA (multi-tenant) — patrón OBLIGATORIO" con parámetros de tests por módulo
- [ ] Fix `fn_mover_nucleo` (corre en sesión aparte — chip)

## Despliegue
- [ ] Commit/merge con gates verdes (sin atribución en commits)
- [ ] Push a main-produccion → migraciones se aplican solas
- [ ] Verificación post-deploy + smoke Santa Reyes

---

# Tracker — Diseño unificado de filtros «Selección de contexto» (SESIÓN PARALELA)

Plan: [fase_de_desarrollo/diseno_filtros_unificado_plan.md](fase_de_desarrollo/diseno_filtros_unificado_plan.md)
> Sección propia en convivencia con la de Santa Reyes (arriba) — pedido explícito del usuario: varias sesiones comparten este tracker; NO borrar contenido ajeno.

## Fundación
- [x] Inventario completo 41 módulos (3 agentes Explore)
- [x] `styles/filter-context.scss` global (filter-card/steps + fields + inputs + compact) cargado en `styles.scss`
- [x] Modificador `:host(.filtro-compact)` en filtro-select levante
- [x] Colisión `.filter-field` de module-styles.scss neutralizada (reglas escopadas a `.filter-card`)
- [x] `yarn build` verde con la fundación
- [x] Plan formal escrito

## Rondas de ejecución
- [x] R1: Batch A (flips filtro-select, sonnet) + Batch B (hierarchical-filter, opus) + Batch C (fork LPP, sonnet) + build verde 21:49
- [x] R2: D1 estructura (farm/nucleo/galpon/historial) + D2a config ×8 + D2b (dashboard/db-studio/clientes/catalogo) + build verde 22:10
- [x] R3: D3a (inventario/implementacion/mapas) + D3b (vacunacion/tickets/sync-historial) + H (graficas/modal-calculos) + build verde 22:39
- [x] R4: E reportes L (opus) + F engorde/lotes L (opus) + build verde 23:05
- [x] R5: G inventario/traslados L (opus) — gestion-inventario 4 tabs + dashboard/historial/registros traslados
- [x] R6: FINAL archivos compartidos con sesión Santa Reyes (git check previo; mtimes viejos) — seguimiento levante/produccion flips, fechas produccion, movs-aves lista+modal, modal-create-edit-lote, lote-list cascada completa; fix anidado ux-card__filters
- [x] Build final verde 23:27 (único warning: bundle budget preexistente)
- [x] Validación: clases del diseño confirmadas en el bundle compilado (`dist/browser/styles-*.css`) + arnés visual con CSS real entregado (`harness-filtros.html`); screenshot del pane no disponible en esta sesión (pane sin componer) — smoke en app viva pendiente de usuario o sesión con login
