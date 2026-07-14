# Tracker — Módulo Vacunación (cronogramas por lote/granja/galpón)

Plan: [vacunacion_cronograma_plan.md](fase_de_desarrollo/vacunacion_cronograma_plan.md)

Rama: `feature/modulo-vacunacion`

---

## Fase 0 — Verificaciones antes de migrar

- [x] Verificar en `sanmarinoapplocal` qué tan poblado está `LoteId` en `lote_postura_levante` (8/8) / `lote_postura_produccion` (2/5, no confiable) / `lote_ave_engorde` (columna `lote_id` NO existe, solo `lote_erp`) → **confirmado: ancla de 3 FKs + `LineaProductiva` es necesaria, no opcional**
- [x] Confirmar filtro de `ItemInventario` (tabla física `item_inventario_ecuador`, 17 columnas, `tipo_item` string libre) — ya hay 19 filas con `tipo_item='Vacuna'` en local, **pero el casing es inconsistente** (`Vacuna`/`Medicamento` vs `medicamento` minúscula) → el filtro del módulo de vacunación debe ser case-insensitive (`ILIKE`/`.ToLower()`), no `==` exacto como el existente `ItemInventarioService.cs:41`
- [x] Confirmar tabla de configuración por empresa: no existe ninguna reusable (`pg_tables ILIKE '%config%'` solo trae catálogos de Postgres) → se crea `vacunacion_configuracion` dedicada, como estaba previsto

## Backend — modelo de datos

- [x] Entidad `VacunacionCronogramaItem` (`Domain/Entities/Vacunacion/`)
- [x] Entidad `VacunacionRegistroAplicacion` (1:1 con `VacunacionCronogramaItem`)
- [x] Entidad `VacunacionConfiguracion` (umbral configurable por empresa/país, PK compuesta company_id+pais_id)
- [x] EF Configurations (`Infrastructure/Persistence/Configurations/Vacunacion/`), snake_case + check constraints (línea/unidad válida, un solo FK de lote, motivo obligatorio, aplicado-por coherente)
- [x] Migración EF `AddVacunacionModule` (3 tablas nuevas, generada con `dotnet ef migrations add` — no requiere `IF NOT EXISTS` a mano, es el patrón real del repo para tablas nuevas, confirmado contra 18 migraciones previas)
- [x] Migración de datos `AddPermisosVacunacion` (`vacunacion.cronograma.ver/administrar`, `vacunacion.registro.aplicar`, `vacunacion.reportes.ver`), idempotente `WHERE NOT EXISTS`
- [x] `backend/sql/add_vacunacion_menu.sql` (grupo "Vacunación" + 3 hijos; **gotcha**: el patrón viejo de otros `*.sql` del folder no tiene `key`/`is_group`/`sort_order` — esquema real de `menus` los exige, corregido y verificado con `\d menus`; sin `role_menus` automático, a asignar por UI de Roles)
- [x] Probar local: `dotnet ef database update` sin error contra `sanmarinoapplocal` — 3 tablas + 4 permisos + 4 filas de menú confirmados por `psql`

## Backend — cálculo puro

- [x] `Application/Calculos/VacunacionCalculos.cs`: cálculo de franja (fecha inicio/fin dado unidad+valor+fechaEncaset+rango), genérico Semana/Dia/Fecha
- [x] `VacunacionCalculos`: cálculo de estado+desviación (a tiempo/tardío/adelantado/no aplicado, incumplido según umbral configurable)
- [x] Tests xUnit `tests/ZooSanMarino.Application.Tests/VacunacionCalculosTests.cs` — 10/10 verde (franjas semana/día/fecha, los 5 estados, umbral configurable). Suite completa: 346/346 verde (sin regresiones)

## Backend — servicios y endpoints

- [x] `VacunacionCronogramaService` (ancla + interfaz) + `Funciones/Crud.cs` + `Funciones/Filtros.cs` + `Funciones/Consultas.cs` (reusa `IFarmService.GetAssignedFarmsForCompanyAsync`; `GetCronogramaLoteAsync` encadena Levante↔Producción vía `LotePosturaProduccion.LotePosturaLevanteId`)
- [x] `VacunacionRegistroService` (ancla + interfaz) + `Funciones/Registrar.cs` (fecha server-side `DateTime.UtcNow.Date`, motivo obligatorio condicional, aplicado-por FK/texto libre exactamente uno)
- [x] `VacunacionReportesService` (invoca `fn_vacunacion_cumplimiento_lote` vía `SqlQueryRaw` + `NpgsqlParameter` tipados, patrón de `InformeSemanalPolloEngordeService`)
- [x] DTOs (`Application/DTOs/Vacunacion/`)
- [x] `VacunacionCronogramaController` (policy `vacunacion.cronograma.administrar`/`.ver`)
- [x] `VacunacionRegistroController` (policy `vacunacion.registro.aplicar`)
- [x] `VacunacionReportesController` (policy `vacunacion.reportes.ver`)
- [x] DI registrado en `Program.cs`; build completo (Domain+Application+Infrastructure+API) 0 errores/0 warnings (API se compiló a carpeta temporal porque el backend del usuario estaba corriendo y bloqueaba `bin/`)
- [ ] Tests de integración: cronograma por línea (Levante/Producción/Engorde), registrar a tiempo/tarde/no aplicada, motivo obligatorio, `FechaAplicacion` no aceptada desde el request

## Backend — SQL de reportes

- [x] `backend/sql/fn_vacunacion_cumplimiento_lote.sql` (% a tiempo/tardío bajo-o-sobre-umbral/no aplicado, promedio días atraso; filtros granja/núcleo/galpón/lote/línea/fecha; replica en SQL el cálculo de franja de `VacunacionCalculos` para poder filtrar por fecha)
- [x] Migración `AddFnVacunacionCumplimientoLote` (CREATE OR REPLACE, sincronizada con el `.sql`) aplicada y probada con `psql` — sin datos aún, 0 filas sin error
- [x] **Decisión de alcance**: se descartó la función "comparativa" separada del plan original — `fn_vacunacion_cumplimiento_lote` ya devuelve una fila por lote para cualquier lista de `p_lote_ids`/`p_granja_ids`, así que sirve para comparar lotes sin duplicar lógica
- [ ] Validar la función con datos reales de prueba (cronograma + registros cargados) una vez exista UI para cargarlos

## Frontend

- [x] `features/vacunacion/models/vacunacion.model.ts` (todos los tipos en un archivo, alineados 1:1 con los DTOs backend)
- [x] `features/vacunacion/funciones/` — `calcular-estado-visual.funcion.ts` (presentación de badges, regla de marca verde/rojo/ámbar) + 3 exports Excel vía `exportar-tabla-excel.funcion.ts`
- [x] `components/modal-item-cronograma` (admin: alta/edición de un ítem, unidad Semana/Dia/Fecha)
- [x] `components/modal-registro-aplicacion` (operador, motivo obligatorio condicional, aplicado-por FK/libre, `ConfirmDialogService`/`ToastService`)
- [ ] `components/graficas-cumplimiento` (ng2-charts) — **simplificado a barras de % con Tailwind directo en `reportes-cumplimiento.page.html`** en vez de un componente de gráfica aparte, por tiempo; queda pendiente si se quiere la versión con ng2-charts
- [x] `pages/cronograma-administracion.page.ts` (+ html)
- [x] `pages/registro-aplicacion.page.ts` (+ html)
- [x] `pages/reportes-cumplimiento.page.ts` (+ html)
- [x] `services/vacunacion.service.ts` — **un solo servicio** (no 3 separados como decía el plan original) para cronograma+registro+reportes; mismo comportamiento, menos archivos
- [x] `vacunacion-routing.module.ts` + registrado en `app.config.ts` (`/vacunacion` con `authGuard`) + entrada de menú (`add_vacunacion_menu.sql`, ver Backend)
- [x] Selector de vacunas: resuelto en el backend (`GetFilterDataAsync` con `ILIKE`), no en el front
- [ ] Tests Karma de `funciones/` puras
- [x] `yarn build` — **0 errores**, solo el warning preexistente de bundle budget aceptado por `CLAUDE.md`; confirmados los chunks lazy de `vacunacion` en `frontend/dist/browser/`. **Dos gotchas encontrados y corregidos** vía `ng serve` (el primer `yarn build` en background compiló un estado intermedio de los archivos que no los detectó): (1) `appHasPermission` es directiva ESTRUCTURAL, se escribió mal como atributo plano en 5 lugares → corregido a `*appHasPermission="'...'"`; (2) eso a su vez generó NG5002 en `registro-aplicacion.page.html` (dos directivas `*` en el mismo `<button>` junto a `*ngIf`) → envuelto en `<ng-container *ngIf="...">`. Re-verificado con `ng serve` (rebuild limpio) y navegación real en el Browser pane (`/vacunacion/cronograma` → redirige a login vía `authGuard`, sin errores de consola) — build de producción final corriendo para confirmación definitiva

## Validación end-to-end

- [ ] Flujo completo local: crear cronograma (Levante por semana, Engorde por día, ítem con fecha fija) → registrar aplicación a tiempo/tardía/no aplicada → ver reflejado en reportes de cumplimiento → exportar los 3 Excel — **no ejecutado**: requiere credenciales de login reales que el asistente no tiene: cargar datos vía UI necesita sesión autenticada
- [ ] Verificar visibilidad: usuario con 1 granja asignada vs usuario con varias — mismo bloqueo (requiere login)
- [x] `dotnet build` + `dotnet test` en verde (346/346)
- [x] `ng serve` rebuild limpio + navegación real en Browser pane (`/vacunacion/cronograma` → redirige a login por `authGuard`, sin errores de consola) — confirma que el compilador Angular no tiene errores en el estado final de los archivos
- [x] `yarn build` (producción) final — **confirmado exit 0**, mismo único warning preexistente de bundle budget, sin errores (tardó ~110s por carga del sistema, varias sesiones en paralelo del usuario)
- [x] Preview server de verificación (`frontend-node22-4300`) detenido al terminar — sin procesos huérfanos de este agente
- [x] Reportado al usuario (ver resumen de cierre)

## Pendiente explícito (no alcanzado en esta sesión)
- Tests de integración backend (cronograma por línea, registrar aplicación, motivo obligatorio)
- Tests Karma de `funciones/` puras del frontend
- `components/graficas-cumplimiento` con ng2-charts (se simplificó a barras CSS)
- Nada de esto está commiteado a git todavía (rama `feature/modulo-vacunacion`, todo en working tree)
