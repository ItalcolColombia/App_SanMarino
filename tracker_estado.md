# Tracker — Refactor archivos largos (backend + frontend)

Plan: [refactor_archivos_largos_plan.md](fase_de_desarrollo/refactor_archivos_largos_plan.md)

**Estado: Etapa 0 completa (auditoría + plan). Pendiente OK explícito del usuario antes de tocar código (umbral, orden de etapas, alcance de `Program.cs`).**

## Etapa 0 — Auditoría y plan
- [x] Rankear `backend/src/**/*.cs` (excl. `Migrations/`, tests) por líneas
- [x] Rankear `frontend/src/**/*.ts` (excl. `*.spec.ts`) y `**/*.html` por líneas
- [x] Confirmar tamaños previamente detectados (MovimientoAvesService 2507 ✓, SeguimientoAvesEngordeService 1884 ✓, IndicadorEcuadorService 1185 ✓, SeguimientoAvesEngordeEcuadorService 1087 ✓, MovimientoAvesController 1019 ✓, SeguimientoLoteLevanteService 837 ✓)
- [x] Sumar archivos faltantes al radar (ReporteTecnicoService 3110, InventarioGestionService 2296, ReporteTecnicoProduccionService 1953 — backend; y todo el frontend, no auditado antes)
- [x] Documentar plan staged en `fase_de_desarrollo/refactor_archivos_largos_plan.md`
- [ ] **OK explícito del usuario** sobre umbral, orden de etapas y alcance de `Program.cs`

## Etapa 1 — Backend: Indicadores/Liquidaciones (cálculo puro, mayor riesgo aritmético)
- [ ] Revisar `Application/Calculos/IndicadorEcuadorCalculos.cs` existente antes de mover nada (evitar duplicar)
- [ ] `IndicadorEcuadorService.cs` (1185) → partials `Funciones/` + cálculo puro
- [ ] `SeguimientoAvesEngordeService.cs` (1884) → partials `Funciones/`
- [ ] `SeguimientoAvesEngordeEcuadorService.cs` (1087) → partials `Funciones/`
- [ ] `CorreccionAvesDisponiblesEngordeService.cs` (535) → revisar solapamiento con los anteriores
- [ ] Tests de equivalencia xUnit por cálculo movido
- [ ] `dotnet build` + `dotnet test` verdes

## Etapa 2 — Backend: Movimientos de aves
- [ ] `MovimientoAvesService.cs` (2507) → partials `Funciones/` (patrón `MovimientoPolloEngorde` como referencia)
- [ ] `MovimientoAvesController.cs` (1019) → partials por concern (Consulta/Validación/CRUD/Operaciones/Estadísticas)
- [ ] Test de integración: Create → Procesar → GetById; EjecutarVenta/EjecutarTraslado
- [ ] `dotnet build` + `dotnet test` verdes

## Etapa 3 — Backend: Reportes
- [ ] `ReporteTecnicoService.cs` (3110)
- [ ] `ReporteTecnicoProduccionService.cs` (1953)
- [ ] `ReporteContableService.cs` (1458)
- [ ] `ReporteTecnicoExcelService.cs` (853) / `ReporteContableExcelService.cs` (640) / `ReporteTecnicoProduccionExcelService.cs` (572)
- [ ] Validación manual: mismo reporte antes/después, diff de totales/bultos/consolidado
- [ ] `dotnet build` + `dotnet test` verdes

## Etapa 4 — Backend: Inventario y Traslados
- [ ] `InventarioGestionService.cs` (2296)
- [ ] `InventarioAvesService.cs` (590) / `InventarioGastoService.cs` (550) / `FarmInventoryMovementService.cs` (525)
- [ ] `TrasladoHuevosService.cs` (938)
- [ ] `dotnet build` + `dotnet test` verdes

## Etapa 5 — Backend: resto (Lote/Producción/Auth/Tickets/Farm)
- [ ] `ProduccionService.cs` (1142) / `LoteService.cs` (1089) / `SeguimientoDiarioService.cs` (840) / `SeguimientoLoteLevanteService.cs` (837)
- [ ] `TicketService.cs` (1183)
- [ ] `AuthService.cs` (733) + `AuthController.cs` (526)
- [ ] `FarmService.cs` (921) / `GalponService.cs` (667) / `MapaService.cs` (516)
- [ ] `RoleCompositeService.cs` (790) / `UserService.cs` (580)
- [ ] `GuiaGeneticaEcuadorService.cs` (706)
- [ ] `LoteAveEngordeService.cs` (643) / `LoteReproductoraAveEngordeService.cs` (562)
- [ ] `LiquidacionTecnicaEcuadorService.cs` (612) / `LiquidacionTecnicaComparacionService.cs` (518)
- [ ] `ExcelImportService.cs` (757) / `ExportacionExcelService.cs` (570)
- [ ] `EmailQueueProcessorService.cs` (519)
- [ ] `dotnet build` + `dotnet test` verdes

## Etapa 6 — Frontend: modales con lógica de negocio
- [ ] `lote-levante/modal-create-edit.component.ts` (2200)
- [ ] `engorde-comun/modal-seguimiento-engorde.component.ts` (2135)
- [ ] `lote-produccion/modal-seguimiento-diario.component.ts` (1381)
- [ ] `movimientos-aves/modal-movimiento-aves.component.ts` (1045)
- [ ] `yarn build` + `yarn test` verdes; verificación manual golden path

## Etapa 7 — Frontend: listados grandes (componente + template)
- [ ] `traslados-aves/inventario-dashboard` (1663 ts / 1827 html)
- [ ] `lote/lote-list` (1593 ts / 1345 html)
- [ ] `gestion-inventario-page` (1565 ts / 1236 html)
- [ ] `indicador-ecuador-list` (1128 ts / 1487 html)
- [ ] `lote-reproductora-list` (935 ts / 1068 html)
- [ ] `config/role-management` (984 ts / 1085 html)
- [ ] `lote-engorde-list` (731 ts / 819 html)
- [ ] `yarn build` + `yarn test` verdes; verificación manual golden path

## Etapa 8 — Frontend: resto (gráficas, servicios, listados medianos)
- [ ] `lote-levante/graficas-principal` (1245) / `lote-produccion/graficas-principal` (629)
- [ ] `seguimiento-lote-levante-list` (1078) / `liquidacion-tecnica.component.ts` (1047)
- [ ] `lote-produccion-list` (973) / `reporte-tecnico.service.ts` (914) / `dashboard.component.ts` (895)
- [ ] Backlog 400–600 líneas (ver plan §2.2) según prioridad
- [ ] `yarn build` + `yarn test` verdes

## Etapa 9 — Validación final
- [ ] `dotnet build` + `dotnet test` completos
- [ ] `yarn build` + `yarn test` completos
- [ ] Re-rankear líneas y confirmar que ningún archivo tocado supera el umbral acordado
- [ ] Documentar excepciones justificadas (`Program.cs`)
- [ ] Actualizar `knowledge/architecture.md` si cambió la forma de encender servicios
- [ ] Cerrar tracker
