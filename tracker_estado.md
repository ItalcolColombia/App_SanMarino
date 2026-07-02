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
  - [x] seguimiento-aves-engorde-form unificado (`SeguimientoEngordeCrudApi` + token `ENGORDE_FORM_OPCIONES` con QQ condicional; providers por ruta) — ciclo 8 `3d43127` (−361 netas)
  - [x] modal-seguimiento-engorde unificado (superset Panamá; QQ gated por `isPanama` en template y payload) — ciclo 9 `d5842be` (−2.918 netas)
  - [ ] DECISIÓN USUARIO: `seguimiento-aves-engorde-list` — derivas de producto, no mecánicas: Colombia tiene tabla diaria de BD (`getTablaDiaria`/`fn_seguimiento_diario_engorde`), chips desglose por género y mensajería de reproductoras que Panamá no tiene. ¿Panamá debe recibir esas mejoras (unificar con flags) o su versión simple es intencional?
  - [x] modal-liquidacion analizado — **HALLAZGO CRÍTICO DE DERIVA**: la copia de Colombia es el superset y contiene los features de PANAMÁ (merma R1 de Costos + 6 insumos de liquidación `panamaDiasEnGranja/diasEngorde/avesFinalGranja/avesBeneficiada/produccionKiloPie/metrosCuadrados`, gated por `esPanama`, precarga vía `IndicadorEcuadorService.getReporteIndicadoresPanama`). La copia del módulo aves-engorde-panama es una versión VIEJA sin nada de eso → los usuarios de Panamá hoy NO ven los insumos de liquidación ni la merma al cerrar lote desde su módulo.
  - [x] **ANÁLISIS CORREGIDO (ciclo 21, tras leer el flujo completo + aclaración del usuario)**: la merma es SOLO Ecuador — Panamá nunca debió verla y el ciclo 10 lo interpretó mal. Mapa real: front `aves-engorde` consume el controller `SeguimientoAvesEngordeEcuador` (tabla compartida); el back `SeguimientoAvesEngorde` solo atiende filter-data/resultado/backfill/cuadrar-saldos. Fixes aplicados `612160f`: (1) gate merma `!esPanama`→`esEcuador`; (2) resumen Ecuador ahora devuelve merma guardada (precarga estaba SIEMPRE vacía); (3) URLs de cuadrar-saldos apuntaban al controller Ecuador sin esas rutas → **404 en prod**, corregidas a `SeguimientoAvesEngorde`.
  - [x] **PREGUNTA RESUELTA (validación con sesiones reales, ciclo 22)**: el menú `/daily-log/aves-engorde-panama` **NO existe en la BD** (solo `/daily-log/aves-engorde`, 11 roles, 2 empresas). Panamá Y Ecuador usan el módulo compartido → los 6 insumos esPanama SÍ les llegan. **El fork completo `aves-engorde-panama` (front) + `SeguimientoAvesEngordePanama` (controller/service) + tabla `_panama` es INALCANZABLE: desarrollo no lanzado.**
  - [ ] DECISIÓN USUARIO (nueva formulación): el fork `aves-engorde-panama` no lo usa nadie — ¿(a) eliminarlo como código muerto (recomendado si no hay plan de lanzarlo; ahorra ~30 archivos front + servicio/controller/tabla back), o (b) es un desarrollo en curso que se va a lanzar? Su botón "Cuadrar saldos" además apunta a un endpoint inexistente.
  - [ ] tabs-principal (819 líneas diff) — dejar de último; tiene lógica Panamá propia (RegistroDiarioTablaFilaEngorde, agregados históricos)
- [ ] Back: `SeguimientoAvesEngorde{,Ecuador,Panama}Service` → cálculo puro común en `Application/Calculos/` + parametrización país
  - [x] `LiquidacionEngordeCalculos` extraído (CalcularAvesInicio + CalcularAvesVivas, usados por Colombia y Ecuador) + 9 tests — ciclo 11 `648ddff`
  - [x] `SeguimientoEngordeCalculos` extraído (CalcularDerivados + CalcularSemana, byte-idénticos entre países) + 10 tests — ciclo 12 `babd852`
  - [x] `MetadataEngordeCalculos` extraído (ToKg + ParseMetadataItemsToKg + MergeMetadataWithPatch — la "diferencia" era solo formato + guarda defensiva de Ecuador, ahora aplicada a ambos) + 9 tests — ciclo 13 `c6acdb4`
  - [x] Recálculo saldo alimento comparado: cuerpo del método y helpers equivalentes salvo `YmdHistoricoEfectivo`. `TryGetHistDeltaAndOrd` extraído a `SaldoAlimentoEngordeCalculos` — ciclo 14
  - [ ] DECISIÓN USUARIO (recomendado SÍ): `YmdHistoricoEfectivo` — Colombia extrae fecha efectiva de la referencia del evento (regex seguimiento / INV_CONSUMO) con fallback a FechaOperacion; Ecuador usa FechaOperacion a secas → eventos tardíos caen en el día equivocado del recálculo de saldo en Ecuador (posible fuente de descuadres). ¿Adoptar la versión Colombia en Ecuador? Tras unificar, extraer también `ComputeSaldoAperturaGalponAntesPrimerSeguimiento` (idéntico) y el fold completo a Calculos.
- [x] Back: `MovimientoPolloEngordePanama` verificado — YA sigue el patrón canónico (servicio delgado de 153 líneas que delega en `IMovimientoPolloEngordeService` compartido + `MovimientoPolloEngordeCalculos`). Sin dedup necesario — ciclo 15
- [ ] Liquidaciones Colombia/Ecuador → core común (sin tocar vistas Power BI)
- [ ] Validación visual de cada módulo unificado (datos idénticos pre/post)

## Fase 3 — Optimización BD (cómputo → funciones/vistas SQL)
- [x] Inventario de agregaciones pesadas en C# (ranking por GroupBy/ToListAsync en memoria, sin fn SQL) — ciclo 15:
  1. `MovimientoPolloEngordeService.ResumenDisponibilidad` (9 GroupBy / 13 ToList, 519 líneas) — candidato nº1 a `fn_resumen_disponibilidad_engorde`
  2. `ReporteContableService` (7 GroupBy / 12 ToList, 1.458 líneas) — candidato a `fn_reporte_contable`
  3. `ReporteTecnicoProduccionService` (6/22, 1.953) y `ReporteTecnicoService` (5/21, 3.110) — informes técnicos completos en C#
  4. `LoteReproductoraAveEngordeService` (7/5), `MovimientoPolloEngordeService.Auditoria` (5/9), `IndicadoresProduccionService` (4/5)
  > Referencia del patrón: `IndicadorEcuadorService` ya delega en `fn_indicadores_pollo_engorde` (3 SqlQueryRaw); `fn_seguimiento_diario_engorde` y `fn_informe_semanal_pollo_engorde` existentes.
- [x] Evaluado candidato nº1 (`ResumenDisponibilidad`): **NO migrar a SQL** — ya está batcheado (7 queries, GroupBy pesados se traducen a SQL) y es lógica de validación del flujo de VENTAS (path de escritura); duplicarla en PL/pgSQL crearía riesgo de deriva C#↔SQL en datos críticos — ciclo 16
- [ ] DECISIÓN USUARIO (priorización Fase 3): los objetivos reales son los reportes de solo lectura, cada uno es un esfuerzo dedicado: (a) `ReporteContableService` → `fn_reporte_contable`, (b) `ReporteTecnicoService`/`ReporteTecnicoProduccionService` → fn por informe. ¿Cuál duele más en prod para empezar?
- [ ] Por candidato: función SQL + migración EF idempotente + test de equivalencia numérica
- [x] Índices revisados en tablas calientes (seguimiento_diario_aves_engorde, lote_registro_historico_unificado, historial_lote_pollo_engorde, movimiento_pollo_engorde): **bien cubiertos** (compuestos lote+fecha DESC, company+fecha, parciales en FKs nullables, únicos de negocio). Sin cambios especulativos; para más, pedir `pg_stat_statements` de prod — ciclo 19

## Fase 4 — Normalización y limpieza BD
- [x] Cruce inicial `information_schema` local (95 tablas) vs entidades mapeadas. Candidatas a DROP (confirmar en prod antes): `_backup_cuadre_expected_2026_06_01`, `_migracion_saldo_alimento_2026_05_28`, `_migracion_saldo_alimento_2026_05_31`, `_migracion_saldo_alimento_m1_2026_05_31`, `guia_semana`, `user_paises` (las 2 últimas: sin entidad ni servicio, solo aparecen en la migración `AddMissingDbFunctionsTriggersAndViews`)
- [x] Investigado: `SeguimientoDiarioAvesEngordeEcuador` (entidad+DbSet+config) es artefacto MUERTO del modelo — ningún servicio la usa; Ecuador escribe en la tabla compartida `seguimiento_diario_aves_engorde`. La `_panama` SÍ la usa `SeguimientoAvesEngordePanamaService` pero la tabla no existe en BD local (creada por SQL crudo en prod) → módulo Panamá crashearía local.
- [ ] DECISIÓN USUARIO: eliminar entidad `SeguimientoDiarioAvesEngordeEcuador` del modelo (genera migración con DropTable → editar a no-op o `DROP IF EXISTS` según confirmes si la tabla existe en prod con datos)
- [x] Migración idempotente `EnsureSeguimientoDiarioAvesEngordePanamaTable` creada y aplicada en local (38 columnas + índices + FK guard; en prod será no-op porque la tabla ya existe) — ciclo 17 `877c07b`
- [ ] Confirmar en prod (solo lectura) los conteos de filas de las candidatas a DROP
- [x] Informe de columnas legacy → `backend/sql/INFORME_COLUMNAS_NO_MAPEADAS.md` (89 tablas analizadas; 11 con columnas fuera de EF: 6 vivas vía SQL crudo → recomendado mapearlas; 4 candidatas a DROP con verificación en prod) — ciclo 19
- [x] Scripts entregables creados — ciclo 16: `backend/sql/verificacion_tablas_sin_uso.sql` (solo lectura: conteos + dependencias, correr en PROD) y `backend/sql/propuesta_drop_tablas_sin_uso.sql` (DROPs comentados; ejecutar solo con OK + backup)

## Fase 5 — Segunda pasada del loop
- [x] Re-barrido final: dotnet build 0 err/0 warn · 54/54 tests · ng build OK (194 s) · app carga y login renderiza — ciclo 20
- [x] Resueltas TODAS las advertencias baseline del backend → `dotnet build` 0 err / **0 warn** (SeguimientoDiarioService ×3, EmailQueueProcessorService, TrasladoHuevosService var muerta) — ciclo 18 `074d5b6`
- [x] Resumen final — **LOOP CERRADO 2026-07-01**: 20 ciclos, 32 commits, `main..HEAD` = 160 archivos, +15.867/−18.304 (−2.437 netas). El loop se reactiva cuando el usuario resuelva las DECISIÓN USUARIO pendientes (buscar "DECISIÓN USUARIO" en este tracker).
- [ ] ⏸️ EN PAUSA (esperan OK del usuario): unificar modal-liquidacion (deriva crítica Panamá), YmdHistoricoEfectivo Ecuador, seguimiento-aves-engorde-list, tabs-principal, paneles test farm-management, DROP 6 tablas + 4 grupos de columnas, priorización de reportes → fn SQL, entidad SeguimientoDiarioAvesEngordeEcuador

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
| 8 | form seguimiento engorde unificado (QQ condicional por token; providers por ruta) (−361) | `3d43127` | ng build OK (98 s) · visual OK |
| 9 | modal-seguimiento-engorde unificado (−2.918); list marcado DECISIÓN USUARIO | `d5842be` | ng build OK (85 s) · visual OK |
| 10 | Análisis modal-liquidacion: deriva CRÍTICA detectada (Panamá sin merma ni insumos de liquidación que Colombia sí tiene gated por esPanama) → DECISIÓN USUARIO; mapa de métodos back Colombia/Ecuador para unificación | (tracker) | solo análisis, sin cambios de código |
| 11 | `LiquidacionEngordeCalculos` (cálculo puro compartido back Colombia+Ecuador) + 9 tests | `648ddff` | dotnet build 0 err · 34/34 tests verdes |
| 12 | `SeguimientoEngordeCalculos` (CalcularDerivados/CalcularSemana dedup) + 10 tests | `babd852` | dotnet build 0 err · 44/44 tests verdes |
| 13 | `MetadataEngordeCalculos` (ToKg/ParseMetadataItemsToKg/MergeMetadataWithPatch dedup) + 9 tests | `c6acdb4` | dotnet build 0 err · 53/53 tests verdes |
| 14 | `SaldoAlimentoEngordeCalculos` (TryGetHistDeltaAndOrd dedup) + hallazgo divergencia `YmdHistoricoEfectivo` (Ecuador sin fecha efectiva → DECISIÓN USUARIO) | `86bf085` | dotnet build 0 err · 53/53 tests verdes |
| 15 | MovimientoPolloEngordePanama verificado (ya canónico) + inventario Fase 3 con ranking de candidatos a fn SQL | (tracker) | análisis, sin cambios de código |
| 16 | ResumenDisponibilidad evaluado (NO migrar: write-path crítico ya batcheado) + scripts Fase 4 (verificación + DROP propuesto) | `f8ee8d5` | scripts solo lectura / DROPs comentados |
| 17 | Migración idempotente tabla `seguimiento_diario_aves_engorde_panama` (código manda) | `877c07b` | ef database update OK local (38 cols) · backend arranca sin errores |
| 18 | Todas las advertencias baseline resueltas (6 → 0) | `074d5b6` | dotnet build 0 err / 0 warn · 54/54 tests |
| 19 | Índices revisados (OK, sin cambios) + informe columnas no mapeadas (11 tablas) | `a40babd` | análisis contra BD local alineada a migraciones |
| 21 | Análisis merma corregido (solo Ecuador) + 3 fixes: gate esEcuador, merma en resumen Ecuador (precarga rota), URLs cuadrar-saldos (404 en prod) | `612160f` | dotnet build 0 err · ng build OK · E2E requiere sesión |
| 22 | **Validación E2E con sesiones reales (EC/PA/CO)**: login 3 países OK · Ecuador ve Merma y precarga funciona (lote 2602: 5 uds / 10,66 kg desde BD) · resumen devuelve mermaUnidades/mermaKilos · menú Panamá usa módulo compartido (fork inalcanzable, sin menú en BD) · Colombia postura (lote, levante, producción) carga sin errores nuevos | (validación) | sesiones admin.ecuador / admin.panama / solangyramirez |
