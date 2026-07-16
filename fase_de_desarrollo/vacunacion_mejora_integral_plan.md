# Plan — Mejora integral del módulo Vacunación (performance + UI/UX + reportería)

**Fecha:** 2026-07-16 · **Módulo:** `vacunacion` (front) / `Vacunacion*` (back)

## Objetivos (pedido del usuario)

1. **Velocidad**: los selects y las cargas de información están lentos → llevar el 100% de las
   consultas de LECTURA del módulo a **funciones SQL PostgreSQL** nombradas por módulo+función
   (`fn_vacunacion_<funcion>`): la BD filtra/agrupa, el backend orquesta (regla CLAUDE.md).
2. **UI/UX profesional** con los colores/tokens de la aplicación (paleta Italfoods centralizada,
   nada hardcodeado) y mejor usabilidad.
3. **Reportería** mejorada: KPIs globales, detalle ítem a ítem y export Excel multi-hoja.

## Diagnóstico (auditado con workflow multi-agente + lectura directa)

| # | Causa de lentitud | Evidencia |
|---|---|---|
| 1 | `GET /VacunacionCronograma/filter-data` = 5+ queries secuenciales: granjas como `FarmDto` completo (4 subqueries correlacionadas por granja + query extra a master lists) + 3 queries de lotes (levante/producción/engorde) + vacunas | `VacunacionCronogramaService.Filtros.cs`, `FarmService.cs:274-313` |
| 2 | Cero caché en front: las 3 páginas llaman `getFilterData()` en `ngOnInit` → 3 descargas del payload pesado por sesión | `vacunacion.service.ts` (sin `shareReplay`) |
| 3 | Reportes descarga lotes+vacunas que nunca usa (solo granjas) | `reportes-cumplimiento.page.ts:38-45` |
| 4 | `GET /VacunacionCronograma/por-lote` = ~6 round trips encadenados + franja calculada y ordenada en C# | `VacunacionCronogramaService.Consultas.cs` |
| 5 | `calcularEstadoVisual(item)` llamado 2× por fila por ciclo de CD (aloca objeto nuevo — patrón NG0103 del repo); sin `trackBy` | páginas cronograma/registro |
| 6 | `cargandoFiltros` existe pero NO se renderiza → selects vacíos sin feedback (percepción de lentitud) | cronograma page |
| 7 | `vacunacion_cronograma_item` sin índice por `company_id` (filtro primario de las fns) | Configuration (solo índices FK) |
| 8 | Colores hardcodeados `#e85c25`/`#2d7a3e` + azul fuera de paleta; verde usado como acción | 5 puntos en templates |
| 9 | Modal registro pide "usuario del sistema" tipeando un **ID numérico a mano** | `modal-registro-aplicacion` |

## Enfoque arquitectónico

### Backend — funciones SQL (patrón `fn_vacunacion_cumplimiento_lote`, ya en prod)

**Nuevas funciones (migración EF idempotente `CREATE OR REPLACE` + espejo en `/backend/sql/`):**

1. **`fn_vacunacion_filter_data(p_user_guid uuid, p_company_id int, p_pais_id int) RETURNS jsonb`**
   — UN round trip reemplaza los 5+: `{granjas, lotes, vacunas, usuarios}` como jsonb (claves
   camelCase 1:1 con el DTO).
   - `granjas`: `user_farms` ∩ `farms` (company, país vía `departamentos`, `deleted_at IS NULL`),
     proyección lite `{id, companyId, name}` orden `name` (el front solo usa esos campos — verificado).
   - `lotes`: `UNION ALL` de `lote_postura_levante`/`lote_postura_produccion`/`lote_ave_engorde`
     (mismos filtros que hoy: company + deleted_at + granjas del usuario; SIN filtrar estado de
     cierre = paridad), orden `fecha_encaset DESC NULLS LAST`.
   - `vacunas`: `item_inventario_ecuador` activo, `tipo_item ILIKE 'vacuna'`, orden `nombre` (paridad).
   - `usuarios` (NUEVO, aditivo): usuarios activos de la empresa (`user_companies` ∩ `users`) con
     cédula numérica → `{id: cedula::int, nombre}` — habilita el select "aplicado por usuario del
     sistema" (el int UserId del sistema ES la cédula: patrón TicketService.BuildNotaUserInfoAsync).
   - Wrapper C#: `SqlQueryRaw<string>` + parser **puro** en `Application/Calculos/VacunacionFilterDataJson.cs`
     (testeable con xUnit). Se elimina la dependencia a `IFarmService` (solo la usaba este método).

2. **`fn_vacunacion_cronograma_lote(p_company_id int, p_linea_productiva text, p_lote_id int) RETURNS TABLE`**
   — UN round trip reemplaza los ~6: resuelve el par Levante↔Producción, joins a
   `item_inventario_ecuador`/`farms`/tablas de lote/`vacunacion_registro_aplicacion` + nombres de
   usuario (join `users` por cédula) y calcula la franja en SQL con **la misma aritmética** de
   `VacunacionCalculos.CalcularFranja` (Semana: `encaset+(valor-1)*7`; Día: `encaset+valor`;
   Fecha: `fecha_objetivo`; ± rangos). Orden `fecha_inicio_franja, orden` (paridad).
   - Columnas snake_case (gotcha SqlQueryRaw + EFCore.NamingConventions), sin dígitos en nombres.
   - Franja NULL (sin encaset) → el wrapper C# lanza `InvalidOperationException` (paridad con el
     throw actual de `CalcularFranja`).
   - **Mejora aditiva**: llena `UsuarioRegistraNombre`/`AplicadoPorUserNombre` (hoy siempre null).
   - Mapeo fila→DTO como estático **puro** en `Application/Calculos/VacunacionCronogramaMapper.cs` (testeable).

3. **`fn_vacunacion_cumplimiento_detalle(...mismos 9 params que cumplimiento_lote...) RETURNS TABLE`**
   — reportería nueva: una fila por vacuna programada (granja, lote, línea, vacuna, programado,
   franja, estado, fecha aplicación, desviación, incumplido, motivo, aplicado por, registrado por).
   Nuevo endpoint `POST /api/VacunacionReportes/detalle` (permiso `vacunacion.reportes.ver`;
   la ruta no contiene "admin" → OK con WAF).

4. **Índices** (idempotentes, misma migración): `vacunacion_cronograma_item (company_id)` y
   `(company_id, pais_id)` no existe hoy → `CREATE INDEX IF NOT EXISTS`.

**Se mantienen intactos:** `fn_vacunacion_cumplimiento_lote` (firma y columnas congeladas — el Down
de su migración dropea por firma), los 5 endpoints de escritura (create/update/delete/aplicar/
no-aplicar quedan en EF: lookups puntuales + SaveChanges transaccional — no son el cuello), permisos
y menú (migraciones ya aplicadas).

**Scoping de seguridad del reporte (fix):** `GetCumplimientoAsync`/`GetCumplimientoDetalleAsync`
intersecan `req.GranjaIds` con las granjas asignadas al usuario (query liviana `user_farms`) antes
de invocar la fn — hoy un usuario con `vacunacion.reportes.ver` ve TODA la empresa aunque
filter-data sí restringe. Cambio de alcance visible: se anuncia en el resumen final.

### Frontend — rediseño con tokens del tema + caché

- **`vacunacion.service.ts`**: caché de `getFilterData()` con `shareReplay(1)` + `refrescarFilterData()`;
  nuevo `getCumplimientoDetalle()`. Navegar entre las 3 páginas ya no re-descarga nada.
- **`models/`**: `VacunacionUsuarioOpcionDto`, `VacunacionCumplimientoDetalleDto`; `usuarios` en
  `VacunacionFilterDataDto` (aditivo, `FarmDtoLite` ya es lite).
- **`funciones/`** (puras, + `README.md` de convención):
  - `construir-filas-cronograma.funcion.ts`: mapea items → filas `{item, estado}` UNA vez por carga
    (misma lógica visual de `calcular-estado-visual`, solo cambia CUÁNDO se evalúa) — fix CD/NG0103.
  - `calcular-kpis-cronograma.funcion.ts`: totales del lote (programadas, aplicadas a tiempo,
    tardías, incumplidas, no aplicadas, pendientes, % cumplimiento).
  - `exportar-cumplimiento-excel.funcion.ts` → multi-hoja (Resumen KPIs + Cumplimiento + Detalle)
    con `exportarMultiHojaExcel` del shared.
- **Las 3 páginas** rediseñadas SOLO con clases/tokens del tema global (`theme-italfoods.scss`:
  `.ux-card`, `.ux-table`, `.btn-primary` (naranja token), `.btn-ghost`, `.btn-danger`,
  `.form-label`, `.form-input`, `.icon-btn`, `.empty-state`, `.loading-overlay`, `.spinner`,
  `.card-italfoods`, variables `--ital-*`/`--success`/`--danger`):
  - Header de página consistente (título + subtítulo + acento de marca).
  - Estados de carga reales (selects disabled + spinner mientras cargan filtros; overlay en tablas).
  - Cronograma: buscador de lote (filtro de texto sobre la lista en caché), KPI chips del lote,
    tabla `ux-table` con scroll horizontal (`.ux-scroll`), badges de estado, acciones `icon-btn`,
    leyenda de estados. `trackBy` en todos los `*ngFor`.
  - Registro: igual + columnas nuevas Fecha aplicación / Aplicado por / Registrado por (los nombres
    ahora llegan del backend), botón Registrar NARANJA (acción — verde solo éxito).
  - Reportes: KPI cards globales calculadas de las filas, tabla resumen con barras usando tokens
    (`--success`/ámbar/`--danger`), pestaña/бloque "Detalle por vacuna" (endpoint nuevo), filtro
    adicional por lote (opcional, en cascada con granja), export multi-hoja.
- **Modales** (rediseño con `.modal-italfoods`/tokens + accesibilidad: `role="dialog"`,
  `aria-modal`, cierre ESC y click-backdrop, `aria-label` en ✕):
  - Ítem cronograma: buscador de vacuna (filtro texto), mismos campos.
  - Registro aplicación: **select de usuarios del sistema** (de `filterData.usuarios`) en lugar del
    input numérico de ID.
- **Colores**: eliminar TODOS los hex hardcodeados (`#e85c25`, `#2d7a3e`, `text-blue-600`).

## Archivos a crear / modificar

**Backend crear:** `Application/Calculos/VacunacionFilterDataJson.cs`,
`Application/Calculos/VacunacionCronogramaMapper.cs`,
`Application/DTOs/Vacunacion/VacunacionCronogramaItemRow.cs`,
`Application/DTOs/Vacunacion/VacunacionCumplimientoDetalleDtos.cs` (DTO + Row),
`Infrastructure/Migrations/<ts>_AddFnVacunacionConsultas.cs` (scaffold + Sql()),
`backend/sql/fn_vacunacion_filter_data.sql`, `backend/sql/fn_vacunacion_cronograma_lote.sql`,
`backend/sql/fn_vacunacion_cumplimiento_detalle.sql`,
`tests/ZooSanMarino.Application.Tests/VacunacionFilterDataJsonTests.cs`,
`tests/.../VacunacionCronogramaMapperTests.cs`.

**Backend modificar:** `VacunacionCronogramaService.cs` (ctor sin IFarmService),
`Funciones/VacunacionCronogramaService.Filtros.cs` (SQL fn + parser),
`Funciones/VacunacionCronogramaService.Consultas.cs` (SQL fn + mapper),
`VacunacionReportesService.cs` (detalle + scoping granjas asignadas),
`Application/DTOs/Vacunacion/VacunacionFilterDataDto.cs` (granja lite + usuarios),
`Application/Interfaces/IVacunacionReportesService.cs`, `VacunacionReportesController.cs` (detalle).

**Frontend crear:** `funciones/README.md`, `funciones/construir-filas-cronograma.funcion.ts`,
`funciones/calcular-kpis-cronograma.funcion.ts`.
**Frontend modificar:** `services/vacunacion.service.ts`, `models/vacunacion.model.ts`,
las 3 páginas (`.ts`+`.html`), los 2 modales, `funciones/exportar-cumplimiento-excel.funcion.ts`.

## Reglas de negocio que NO cambian

- Franja, estados, umbral incumplido, motivo obligatorio, "exactamente un" aplicado-por: intactos
  (escrituras siguen en EF con `VacunacionCalculos`).
- Contrato wire de los endpoints existentes: `filter-data` devuelve el mismo shape (granjas ahora
  lite — el front solo usa id/companyId/name; `usuarios` es aditivo); `por-lote` mismo DTO (ahora
  además llena los nombres de usuario, aditivo); `cumplimiento` intacto (+ scoping de granjas).
- Semántica del reporte congelada (tardío 1 sem vs 2+ usa el flag `incumplido` persistido;
  `total_pendiente` sigue agrupando Pendiente+Adelantado).

## Casos de prueba

- **VacunacionFilterDataJsonTests**: parse de jsonb completo (4 colecciones pobladas, orden
  preservado), colecciones vacías/null, campos null (fechaEncaset/estadoCierre), json vacío `{}`.
- **VacunacionCronogramaMapperTests**: fila con registro → DTO anidado con nombres; fila sin
  registro → `Registro = null`; franja null → `InvalidOperationException`; fila completa 1:1.
- **VacunacionCalculosTests existentes** (franja/estados): deben seguir verdes sin tocar.
- Front: `yarn build` 0 errores; smoke visual con dev server si el entorno lo permite.

## Validación

`cd backend && dotnet build && dotnet test` · migración probada local (`dotnet ef database update`
vía DesignTimeDbContextFactory, BD :5433) · `cd frontend && yarn build` (solo warning de bundle
budget preexistente) · sin procesos vivos al terminar.

---

## Estado final (2026-07-16) — checklist ejecutado

> Nota: `tracker_estado.md` fue tomado por la sesión paralela (Matriz Verenice) mientras corría esta
> tarea; el checklist de ESTA feature vive acá para no pisar esa planilla.

**Fase 1 — Funciones SQL** ✔
- [x] `backend/sql/fn_vacunacion_filter_data.sql` (jsonb granjas lite + lotes 3 líneas + vacunas + usuarios)
- [x] `backend/sql/fn_vacunacion_cronograma_lote.sql` (1 round trip, franja en SQL, nombres por cédula)
- [x] `backend/sql/fn_vacunacion_cumplimiento_detalle.sql` (reportería ítem a ítem)
- [x] Migración `20260716053139_AddFnVacunacionConsultas` (CREATE OR REPLACE + índice company/país IF NOT EXISTS)
- [x] Aplicada en local (:5433) sin error; funciones probadas con datos reales (insert con ROLLBACK):
      franja SQL = franja C# (encaset 2025-01-28 + Semana 4 → 2025-02-18 a 2025-02-24)

**Fase 2 — Servicios** ✔
- [x] DTOs: `VacunacionGranjaOpcionDto` (lite), `VacunacionUsuarioOpcionDto`, rows cronograma/detalle
- [x] Parser puro `VacunacionFilterDataJson` + mapper puro `VacunacionCronogramaMapper` (Application/Calculos)
- [x] `Filtros.cs` y `Consultas.cs` reescritos sobre las fns (IFarmService eliminado del ctor)
- [x] `VacunacionReportesService` partido en ancla + `Funciones/Consultas.cs`; detalle + scoping por granjas asignadas
- [x] `POST /api/VacunacionReportes/detalle` (permiso `vacunacion.reportes.ver`)
- [x] `dotnet build` 0 errores (API compilado a output alterno: bin bloqueado por el backend corriendo)

**Fase 3 — Tests** ✔
- [x] `VacunacionFilterDataJsonTests` (8) + `VacunacionCronogramaMapperTests` (5)
- [x] `dotnet test`: 392/392 verdes (VacunacionCalculosTests intactos)

**Fase 4/5 — Frontend** ✔
- [x] Caché `shareReplay(1)` + TTL 5 min + `refrescarFilterData()`; `getCumplimientoDetalle()`
- [x] Funciones puras nuevas: filas con estado precalculado (fix NG0103), KPIs cronograma, KPIs
      cumplimiento ponderados, export multi-hoja; `estadoVisualDe` núcleo compartido; README de convención
- [x] 3 páginas rediseñadas con tokens del tema (ux-card/ux-table/btn-primary/form-input/empty-state/
      loading-overlay), headers pro, KPI cards, buscador de lote, trackBy, ux-scroll, leyenda de estados
- [x] Modales: tokens + role=dialog/aria + ESC + click-backdrop; buscador de vacuna memoizado;
      select de usuarios del sistema (reemplaza el input de ID numérico)
- [x] Cero hex hardcodeados (#e85c25/#2d7a3e/azul eliminados)
- [x] `yarn build` 0 errores (solo warning bundle budget preexistente)

**Pendiente / notas**
- [ ] Smoke E2E en navegador: no se pudo en esta sesión — el backend local corriendo (:5002) es del
      usuario con código viejo y el login exige credenciales; validar tras levantar back+front con
      `dev-back.ps1`/`dev-front.ps1` (la migración local ya está aplicada).
- ⚠ Cambio de alcance visible: los reportes de cumplimiento ahora se acotan a las **granjas
  asignadas** al usuario (antes, cualquier usuario con `vacunacion.reportes.ver` veía toda la
  empresa). Igual criterio que filter-data.
- El contrato wire de `filter-data` adelgazó `granjas` a `{id, companyId, name}` (el front solo usaba
  eso) y agregó `usuarios` (aditivo). `por-lote` ahora llena `usuarioRegistraNombre`/`aplicadoPorUserNombre`.
