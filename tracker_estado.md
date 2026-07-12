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
- [x] `SeguimientoAvesEngordeService.cs` (1884) → partials `Funciones/` (Consultas/Crud/SaldoAlimento/Metadata/CuadrarSaldos) + `Application/Calculos/SeguimientoAvesEngordeCalculos.cs` (aritmética pura de saldo de alimento, fechas efectivas, cuadre). Build Infra 0W/0E; 176 tests verdes. Plan: [refactor_seguimiento_aves_engorde_service_plan.md](fase_de_desarrollo/refactor_seguimiento_aves_engorde_service_plan.md)
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

## Frontend — priorización confirmada (ver plan §2.3, umbral 400 líneas, 58 archivos `.ts`)
- [x] Priorizar archivos frontend a refactorizar — orden final documentado en plan §2.3 (Etapas 6/7/8/8b)

## Etapa 6 — Frontend: riesgo alto (cálculo de negocio / dominio recién tocado en backend)
- [ ] `lote-levante/modal-create-edit.component.ts` (2200) — INICIAR
- [ ] `engorde-comun/modal-seguimiento-engorde.component.ts` (2135) — INICIAR (espejo `SeguimientoAvesEngordeService`)
- [ ] `indicador-ecuador/indicador-ecuador-list.component.ts` (1128) — INICIAR (espejo `IndicadorEcuadorService`)
- [ ] `lote-levante/liquidacion-tecnica.component.ts` (1047) — INICIAR (cálculo financiero, zona frágil — ver memoria `liquidacion-engorde-ecuador-descuadre`)
- [ ] `yarn build` + `yarn test` verdes; verificación manual golden path (modal abre/guarda, indicadores calculan igual)

## Etapa 7 — Frontend: listados/dashboards grandes (componente + template)
- [ ] `traslados-aves/inventario-dashboard` (1663 ts / 1827 html)
- [ ] `lote/lote-list` (1593 ts / 1345 html)
- [ ] `gestion-inventario-page` (1565 ts / 1236 html)
- [ ] `lote-produccion/modal-seguimiento-diario.component.ts` (1381)
- [ ] `lote-levante/graficas-principal.component.ts` (1245)
- [ ] `lote-levante/seguimiento-lote-levante-list.component.ts` (1078)
- [ ] `movimientos-aves/modal-movimiento-aves.component.ts` (1045)
- [ ] `yarn build` + `yarn test` verdes; verificación manual golden path

## Etapa 8 — Frontend: medianos (600–1000) + continuación de módulos ya iniciados
- [ ] `movimientos-pollo-engorde-list.component.ts` (1109) — CONTINUAR (ya tiene `funciones/`+`models/`)
- [ ] `config/role-management` (984 ts / 1085 html)
- [ ] `lote-produccion-list.component.ts` (973)
- [ ] `lote-reproductora-list` (935 ts / 1068 html)
- [ ] `reportes-tecnicos/reporte-tecnico.service.ts` (914)
- [ ] `dashboard.component.ts` (895)
- [ ] `movimientos-pollo-engorde/modal-movimiento-pollo-engorde.component.ts` (767) — CONTINUAR
- [ ] `lote-levante/tabs-principal.component.ts` (766)
- [ ] `farm/farm-list.component.ts` (763)
- [ ] `lote-engorde-list` (731 ts / 819 html)
- [ ] `yarn build` + `yarn test` verdes; verificación manual golden path

## Etapa 8b — Frontend: backlog 400–720 líneas (batch por módulo)
- [ ] `traslados-huevos/modal-traslado-huevos.component.ts` (717)
- [ ] `traslados-aves/traslados-aves.service.ts` (715)
- [ ] `config/user-management/modal-create-edit.component.ts` (715)
- [ ] `lote/modal-create-edit-lote.component.ts` (692)
- [ ] `aves-engorde/seguimiento-aves-engorde-list.component.ts` (684) — CONTINUAR
- [ ] `config/company-management.component.ts` (675) — CONTINUAR
- [ ] `lote-produccion/graficas-principal.component.ts` (629)
- [ ] `lote-levante/tabla-lista-indicadores.component.ts` (619)
- [ ] `seguimiento-diario-lote-reproductora-list.component.ts` (614)
- [ ] `reporte-contable-main.component.ts` (597)
- [ ] `aves-engorde/modal-liquidacion-lote-engorde.component.ts` (587) — CONTINUAR
- [ ] `movimientos-aves-list.component.ts` (545)
- [ ] `lote-produccion/produccion.service.ts` (536)
- [ ] `catalogo-alimentos-list.component.ts` (519)
- [ ] `guia-genetica-ecuador-page.component.ts` (509)
- [ ] `config/geography/country-list.component.ts` (503)
- [ ] `lote-levante/indicadores-diarios-compute.service.ts` (498)
- [ ] `gestion-inventario.service.ts` (496)
- [ ] `movimiento-pollo-engorde.service.ts` (491) — CONTINUAR
- [ ] `lote-reproductora-ave-engorde-list.component.ts` (487)
- [ ] `lote-levante/filtro-select.component.ts` (487)
- [ ] `guia-genetica-admin/guia-genetica-form.component.ts` (473)
- [ ] `modal-seguimiento-reproductora.component.ts` (472)
- [ ] `lesiones/lesion-tab.component.ts` (470)
- [ ] `db-studio/data/db-studio.service.ts` (469) — CONTINUAR
- [ ] `nucleo/nucleo-list.component.ts` (468)
- [ ] `clientes/cliente-list.component.ts` (460)
- [ ] `traslados-huevos-list.component.ts` (446)
- [ ] `galpon/galpon-list.component.ts` (430)
- [ ] `config/farm-management.component.ts` (425)
- [ ] `reporte-tecnico-produccion.service.ts` (422)
- [ ] `core/auth/encryption.service.ts` (416) — CORE, tratar aparte (seguridad transversal, no patrón de feature)
- [ ] `gastos-inventario-page.component.ts` (411)
- [ ] `lote-produccion/filtro-select.component.ts` (408)
- [ ] `inventario/movimientos-unificado-form.component.ts` (408)
- [ ] `traslados-aves/traslado-form.component.ts` (406)
- [ ] `yarn build` + `yarn test` verdes tras cada archivo; commits pequeños por archivo

**Excluido:** `app.config.ts` (430) — bootstrap (providers/rutas), no aplica patrón `funciones/`+`models/`; fuera de alcance salvo pedido explícito.

## Etapa 9 — Validación final
- [ ] `dotnet build` + `dotnet test` completos
- [ ] `yarn build` + `yarn test` completos
- [ ] Re-rankear líneas y confirmar que ningún archivo tocado supera el umbral acordado
- [ ] Documentar excepciones justificadas (`Program.cs`)
- [ ] Actualizar `knowledge/architecture.md` si cambió la forma de encender servicios
- [ ] Cerrar tracker
