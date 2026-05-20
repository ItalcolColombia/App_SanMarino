# Memoria de Desarrollo — Tracker Activo

**Feature Actual:** Función DB Seguimiento Diario Engorde  
**Módulo:** `seguimiento_diario_aves_engorde_ecuador` — PostgreSQL function + migración EF Core + nuevo endpoint + refactor frontend  
**Archivo de tarea:** `fase_de_desarrollo/09_fn_seguimiento_diario_engorde.md`

> Feature anterior en espera: Deploy Cross-Platform (08) — `fase_de_desarrollo/08_deploy_cross_platform.md`  
> Historial de features completadas: `tracker_historico/`

---

## ⚠️ Correcciones críticas validadas (2026-05-20)

| Problema | Causa raíz | Solución |
|----------|-----------|---------|
| Migración `SplitByCountry` (20260517) **NO aplicada** en la BD | 30+ migraciones pendientes aplican solo vía SQL directo; EF no las ha ejecutado | Función SQL usa `seguimiento_diario_aves_engorde` (tabla original que SÍ existe) |
| Plan original asumía tabla `_ecuador` | Se creó con base en el estado EF, no en el estado real de la BD | Todos los archivos SQL usan `seguimiento_diario_aves_engorde` |
| Sin migración para la función | El plan no contemplaba migración EF Core | Migración `AddFnSeguimientoDiarioEngorde` creada; aplicar con `apply_fn_seguimiento_diario_engorde.sql` |
| `dotnet ef database update` no aplicable | 30+ migraciones pending crearían conflictos en tablas ya existentes | Usar script SQL directo + `INSERT INTO __EFMigrationsHistory` |

---

## Estado de Implementación

### BASE DE DATOS + MIGRACIÓN EF CORE

- [x] **DB-1** Crear archivo SQL `backend/sql/fn_seguimiento_diario_engorde.sql` con `CREATE OR REPLACE FUNCTION fn_seguimiento_diario_engorde(p_lote_id INT)` referenciando `seguimiento_diario_aves_engorde_ecuador`
- [x] **DB-2** Crear migración EF Core `AddFnSeguimientoDiarioEngorde`:
  - Archivo generado: `backend/src/ZooSanMarino.Infrastructure/Migrations/20260520140828_AddFnSeguimientoDiarioEngorde.cs`
  - `Up()` → `migrationBuilder.Sql(...)` con SQL embebido · `Down()` → `DROP FUNCTION IF EXISTS fn_seguimiento_diario_engorde(INT)`
  - Designer.cs generado automáticamente por EF Core tools ✓
- [ ] **DB-3** Aplicar función directamente en la BD objetivo:
  - Script todo-en-uno: `backend/sql/apply_fn_seguimiento_diario_engorde.sql`
  - Ejecutar en DBeaver/psql (crea la función + registra la migración en `__EFMigrationsHistory`)
  - ⚠️ Motivo: hay 30+ migraciones "Pending" en EF (aplicadas vía SQL directo pero sin registrar); `dotnet ef database update` no es viable sin riesgo
- [ ] **DB-4** Verificar función: `SELECT * FROM fn_seguimiento_diario_engorde(<loteId>)` — validar filas y valores vs. tabla actual del front
- [ ] **DB-5** Confirmar en `__EFMigrationsHistory` que `20260520140828_AddFnSeguimientoDiarioEngorde` quedó registrado

### BACKEND — C# (.NET 9)

- [x] **BE-1** Crear DTO `SeguimientoDiarioTablaFilaDto` en `ZooSanMarino.Application/DTOs/SeguimientoDiarioTablaFilaDto.cs`
- [x] **BE-2** Agregar método `GetTablaDiariaAsync(int loteId)` a `ISeguimientoAvesEngordeEcuadorService`
- [x] **BE-3** En `SeguimientoAvesEngordeEcuadorService` (reescrito esbelto 2026-05-20):
  - CRUD usa `_ctx.SeguimientoDiarioAvesEngorde` (entidad genérica → tabla física real `seguimiento_diario_aves_engorde`) ✓
  - `GetTablaDiariaAsync` llama directo `fn_seguimiento_diario_engorde` vía `SqlQueryRaw` sin pre-cálculo en C# ✓
  - `MapToDto` trabaja sobre `SeguimientoDiarioAvesEngorde` ✓
  - **PURGADO**: `RecalcularSaldoAlimentoPorLoteAsync` + todos los helpers `Ecu*` eliminados ✓
  - Build Infrastructure: 0 errores ✓
- [x] **BE-4** Agregar endpoint `GET /api/SeguimientoAvesEngordeEcuador/por-lote/{loteId}/tabla-diaria` en `SeguimientoAvesEngordeEcuadorController` → `IReadOnlyList<SeguimientoDiarioTablaFilaDto>`

### FRONTEND — Angular 20

- [x] **FE-1** Agregar interfaz `SeguimientoDiarioTablaFilaDto` en `seguimiento-aves-engorde.service.ts` (o modelo compartido)
- [x] **FE-2** Agregar método `getTablaDiaria(loteId: number): Observable<SeguimientoDiarioTablaFilaDto[]>` en `SeguimientoAvesEngordeService` → `GET .../por-lote/{loteId}/tabla-diaria`
- [x] **FE-3** En `SeguimientoAvesEngordeListComponent.onLoteChange()`: llamar `getTablaDiaria(loteId)` y asignar resultado a `tablaFilas[]`; pasar como `@Input()` al `TabsPrincipalEngordeComponent`
- [x] **FE-4** En `TabsPrincipalEngordeComponent`:
  - Agregar `@Input() tablaFilas: SeguimientoDiarioTablaFilaDto[] = []`
  - Eliminar: `buildDiarioFilas()`, `aggregateHistoricoPorFecha()`, `computeSaldoAlimentoKgPorSeguimiento()`, `computeSaldoAperturaGalponAntesPrimerSeguimiento()`, `computeVentasAvesPorFecha()`, `avesInicialesLote()`
  - Mantener: `diarioFilasFiltradas` (computed sobre `tablaFilas`), filtros client-side, `diaCorto` (Intl browser), `tipoAlimentoCorto`, export Excel
  - `ngOnChanges`: eliminar llamada a `buildDiarioFilas()`, usar `tablaFilas` directamente
- [x] **FE-5** Actualizar template HTML de `TabsPrincipalEngordeComponent`: bindings apuntan a `f.edadDia`, `f.saldoAves`, `f.saldoAlimentoKg`, `f.ingresoAlimentoKg`, `f.despachoHembras`, `f.despachoMachos`, etc. — build dev OK (0 errores)

### QA

- [ ] **QA-1** Comparar valores antes/después: `saldoAves`, `saldoAlimentoKg`, `edadDia`, `semana`, `acumConsumoKg` deben ser idénticos
- [ ] **QA-2** Lote CERRADO: `saldo_aves` llega a 0 y `avesIniciales = Σ salidas`
- [ ] **QA-3** Lote ABIERTO: `aves_iniciales = aves_encasetadas`, `saldo_aves` baja correctamente cada día
- [ ] **QA-4** Eventos ANULADOS y devoluciones por eliminación excluidos del histórico
- [ ] **QA-5** Lote sin registros: retorna array vacío sin error 500

---

## Contexto Clave

- Tabla Ecuador: `seguimiento_diario_aves_engorde` (migración `20260517` — activa)
- DbSet correcto: `_ctx.SeguimientoDiarioAvesEngordeEcuador` (entidad `SeguimientoDiarioAvesEngordeEcuador`)
- `_ctx.SeguimientoDiarioAvesEngorde` apunta a tabla **eliminada** — NO usar en Ecuador service
- `saldo_alimento_kg` persiste en BD por `RecalcularSaldoAlimentoPorLoteAsync`; la función SQL lo lee directamente
- Scope alimento: `farm_id + nucleo_id + galpon_id` (no lote_id, que puede ser NULL en triggers)
- Scope ventas: `lote_ave_engorde_id` (solo `VENTA_AVES`)
- Excluir: `anulado=true`, ref `%devolución por eliminación%`, `INV_INGRESO` ref `Seguimiento aves engorde #%`
- Filtros que se mantienen en frontend (NO migrar a DB): `diaCorto` (Intl browser), `tipoAlimentoCorto`, filtros de fecha/semana/tipo, export Excel
