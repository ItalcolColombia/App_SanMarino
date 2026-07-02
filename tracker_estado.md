# Tracker — Refactorización y optimización multi-país

**Plan:** [refactor_multipais_optimizacion_plan.md](./fase_de_desarrollo/refactor_multipais_optimizacion_plan.md)
**Rama:** `refactor/optimizacion-multipais` (main intocable)
**Modo:** loop iterativo — cada ciclo cierra 1 ítem: implementar → build back+front → validación visual → commit → marcar `[x]`.

---

## Fase 0 — Línea base
- [x] Rama `refactor/optimizacion-multipais` creada desde `main`
- [x] Build backend baseline: **0 errores, 6 advertencias** (SeguimientoDiarioService x3 null-refs, EmailQueueProcessorService x1)
- [x] Build frontend baseline (`yarn build`) ✅ OK (259 s, 0 errores)
- [x] `dotnet test` baseline: **26 pasan / 0 fallan** (1 Domain + 25 Application.Tests)
- [x] Docker arriba; **GOTCHA:** la BD local real es el **Postgres NATIVO de Windows en :5433** (95 tablas, `sanmarinoapplocal`), NO el contenedor `sanmarino-postgres` (mapea 5433→5432 pero su volumen está VACÍO; conflicto de puerto con el nativo). EF conecta al nativo.
- [x] Entorno local arriba para validación visual: backend :5002 (Development, BD local nativa, migraciones OK) + front :4200 vía preview. **Validación visual ciclo 1:** login renderiza, app carga, sin errores de consola propios (solo 401 esperado sin sesión). Swagger protegido por login propio → validación E2E de endpoints autenticados requiere credenciales del usuario.

## Fase 1 — Código muerto (bajo riesgo)
- [x] Eliminar `backend/.../Services/managerUser.cs` (namespace UserAdmin, 0 referencias) + build ✅ 0 errores
- [x] Eliminar `RolePermissionsController.cs` + `RoleService.cs` + `IRoleService.cs` — endpoint duplicado de `RoleController` (misma ruta `api/Role/{id}/permissions/*`), `IRoleService` nunca registrado en DI → habría dado 500; `RoleController` + `IRoleCompositeService` es la vía viva ✅ build 0 errores
- [ ] Eliminar `frontend/src/app/features/test/http-helper-test/` (sin ruta ni import) + `features/company/` (service huérfano, la vía viva es `core/services/company`) — esperar build baseline front
- [ ] DECISIÓN USUARIO: `features/test/company-admin-test` y `config/farm-management/company-test.component` SÍ están montados en la UI de farm-management (paneles "test" visibles) — quitarlos cambia UI
- [x] Barrido: servicios back sin registro DI → solo `RoleService` (eliminado); `TicketEmailTemplates` es estático y vivo
- [x] Barrido bruto: 45 clases TS sin referencias externas → reporte en scratchpad `front_sin_referencias.txt` (contiene falsos positivos; verificar 1×1)
- [x] Verificados y eliminados 70 archivos de código muerto front (−6.461 líneas). Falsos positivos restaurados: `is-ecuador.pipe`, `lote-seguimiento.service`. Quedan vivos (confirmado uso): `lazy-observe`, `show-if-ecuador`, `conteo-fisico`, `movimiento-alimento-form`, `tabla-indicadores-diarios`
- [x] Barrido DTOs backend: 182 archivos analizados → 5 huérfanos eliminados (CreateLoteWrapper, UpdateLoteWrapper, GalponDto, CreateSeguimientoProduccionRequest, SaveMapaPasosDto)
- [x] Clasificar `/backend/sql/` → `backend/sql/CLASIFICACION_SCRIPTS.md` (vivos / DDL histórico / one-shots / diagnóstico / cuarentena)

## Fase 2 — Unificación multi-país (un dominio por ciclo)
> Diagnóstico: `aves-engorde-panama` = clon de `aves-engorde` (33 de 45 archivos duplicados; `indicadores-diarios-engorde-compute.service.ts` byte-idéntico; modal seguimiento 2100 vs 2109 líneas con deriva). Riesgo actual: fix en un país no llega al otro.
- [ ] Front: `aves-engorde` vs `aves-engorde-panama` → funciones/modelos compartidos, orquestadores por país
  - [x] Creado `features/engorde-comun/` (README con convención); `indicadores-diarios-engorde-compute.service.ts` + models deduplicados (fuente única + shims re-export) — ciclo 5 `37528e1`
  - [x] Deduplicados los idénticos: tabla-indicadores, modal-detalle, graficas-indicadores (componentes completos) + saldo-alimento util → engorde-comun con shims (−1.626 líneas netas) — ciclo 6 `eb0a277`
  - [x] modal-cuadrar-saldos unificado con patrón `CuadrarSaldosEngordeApi` (abstracta + useExisting por país) — ciclo 7 `b10d39d` (−1.006 líneas netas)
  - [ ] Pares con deriva restantes (mismo patrón DI): seguimiento-aves-engorde-form (48 líneas diff), seguimiento-aves-engorde-list (79), modal-seguimiento-engorde (157), modal-liquidacion (201), tabs-principal (819 — dejar de último, tiene lógica Panamá propia: RegistroDiarioTablaFilaEngorde, agregados históricos)
- [ ] Back: `SeguimientoAvesEngorde{,Ecuador,Panama}Service` → cálculo puro común en `Application/Calculos/` + parametrización país
- [ ] Back: `MovimientoPolloEngorde` vs `MovimientoPolloEngordePanama` → compartir core
- [ ] Liquidaciones Colombia/Ecuador → core común (sin tocar vistas Power BI)
- [ ] Validación visual de cada módulo unificado (datos idénticos pre/post)

## Fase 3 — Optimización BD (cómputo → funciones/vistas SQL)
- [ ] Inventariar endpoints con agregaciones pesadas en C# (candidatos: indicadores, liquidaciones, informes semanales)
- [ ] Por candidato: función SQL + migración EF idempotente + test de equivalencia numérica
- [ ] Revisar índices para filtros frecuentes (lote, fecha, company_id, pais_id)

## Fase 4 — Normalización y limpieza BD
- [x] Cruce inicial `information_schema` local (95 tablas) vs entidades mapeadas. Candidatas a DROP (confirmar en prod antes): `_backup_cuadre_expected_2026_06_01`, `_migracion_saldo_alimento_2026_05_28`, `_migracion_saldo_alimento_2026_05_31`, `_migracion_saldo_alimento_m1_2026_05_31`, `guia_semana`, `user_paises` (las 2 últimas: sin entidad ni servicio, solo aparecen en la migración `AddMissingDbFunctionsTriggersAndViews`)
- [x] Investigado: `SeguimientoDiarioAvesEngordeEcuador` (entidad+DbSet+config) es artefacto MUERTO del modelo — ningún servicio la usa; Ecuador escribe en la tabla compartida `seguimiento_diario_aves_engorde`. La `_panama` SÍ la usa `SeguimientoAvesEngordePanamaService` pero la tabla no existe en BD local (creada por SQL crudo en prod) → módulo Panamá crashearía local.
- [ ] DECISIÓN USUARIO: eliminar entidad `SeguimientoDiarioAvesEngordeEcuador` del modelo (genera migración con DropTable → editar a no-op o `DROP IF EXISTS` según confirmes si la tabla existe en prod con datos)
- [ ] Crear migración idempotente `CREATE TABLE IF NOT EXISTS seguimiento_diario_aves_engorde_panama` para alinear local con el código (el código manda)
- [ ] Confirmar en prod (solo lectura) los conteos de filas de las candidatas a DROP
- [ ] Informe de columnas legacy no mapeadas
- [ ] Script `DROP IF EXISTS` propuesto (NO ejecutar sin OK explícito)

## Fase 5 — Segunda pasada del loop
- [ ] Re-barrido de mejoras sobre lo refactorizado
- [ ] Resolver las 6 advertencias baseline del backend
- [ ] Resumen final: diff vs `main` + checklist de regresión visual

## Registro de ciclos cerrados
| # | Ítem | Commit | Validación |
|---|---|---|---|
| 1 | Código muerto back (managerUser, RoleService/IRoleService/RolePermissionsController) + front (features/company, http-helper-test) | `7c14080` | dotnet build 0 err · yarn build OK · visual: app carga y login OK |
| 2 | Fase 0 cerrada: tests baseline (26 ✅), entorno local arriba (back :5002 + front :4200), barrido bruto front (45 candidatos) | (tracker) | backend arranca sin conflictos de ruta; login renderiza sin errores de consola |
| 3 | 70 archivos de código muerto front eliminados (−6.461 líneas), 2 falsos positivos restaurados | `e1e28a4` | ng build OK (122 s) · app recargada y login OK en :4200 |
| 4 | Clasificación SQL (`CLASIFICACION_SCRIPTS.md`) + 5 DTOs huérfanos back eliminados | `19a6999` | dotnet build 0 err/0 warn · backend re-levantado :5002 |
| 5 | `engorde-comun/` creado; compute service + models deduplicados (Colombia/Panamá) con shims | `37528e1` | ng build OK (90 s) · app sin errores nuevos de consola |
| 6 | 3 componentes idénticos + util movidos a engorde-comun (−1.626) | `eb0a277` | ng build OK (87 s) · visual OK |
| 7 | modal-cuadrar-saldos unificado con `CuadrarSaldosEngordeApi` + DI por país (−1.006) | `b10d39d` | ng build OK (100 s) · visual OK |
