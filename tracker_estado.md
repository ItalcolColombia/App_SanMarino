# Tracker — Tab «Huevos» en Seguimiento Diario Levante (semana 14+) y arrastre automático a Producción al liquidar

**Plan:** [fase_de_desarrollo/huevos_levante_semana14_arrastre_produccion_plan.md](fase_de_desarrollo/huevos_levante_semana14_arrastre_produccion_plan.md)

**Decisiones del usuario:** flag por empresa (`captura_huevos_en_levante`) · sin huevos en reportes de levante en esta fase · «Huevos iniciales (al cerrar)» pasa a readonly con el total calculado.

## Fase 0 — Análisis
- [x] Exploración exhaustiva (workflow 8 agentes + síntesis) del esquema, CRUD levante/producción, liquidación, front de ambos modales, fórmulas de semana, impacto en reportes
- [x] Verificación directa de los 4 hallazgos críticos: las 13 columnas de huevos YA existen en `seguimiento_diario_levante`; el mapper de levante manda 15 `null`; `MovimientoAvesCalculos.SemanaDesdeEncaset` es la fórmula canónica; `SeguimientoLoteLevante` es letra muerta (sin DbSet)
- [x] Plan escrito en `fase_de_desarrollo/`

## Fase 1 — Backend: cálculo puro + tests (gate CI)
- [x] `Application/Calculos/HuevosLevanteCalculos.cs` (semana 14, `HuevosClasificacion`, Sumar/Delta, peso ponderado, marca en metadata)
- [x] `tests/ZooSanMarino.Application.Tests/HuevosLevanteCalculosTests.cs` — 42/42 verdes
- [x] `dotnet test` verde — 825/825 en toda la suite

## Fase 2 — Backend: flag de empresa
- [x] `Company.CapturaHuevosEnLevante` + `CompanyConfiguration` (`captura_huevos_en_levante`, default false)
- [x] Propagado en las 5 proyecciones (`CompanyDto`, `CompanyService.ToDto`, `CompanyService.Crud`, `CompanyResolver`, `CompanyPaisService`) + CreateCompanyDto/UpdateCompanyDto
- [x] Migración `20260726231137_AddCapturaHuevosEnLevanteCompany` (idempotente, `ADD COLUMN IF NOT EXISTS`)
- [x] Migración data-only `20260726231200_SeedCapturaHuevosEnLevante` (Sanmarino, `IS DISTINCT FROM`)
- [x] Aplicadas en BD local :5433 y verificadas (Sanmarino=t, resto=f)

## Fase 3 — Backend: captura de huevos en levante (semana 14+)
- [x] DTOs: `CreateSeguimientoLoteLevanteRequest` + `SeguimientoLoteLevanteDto` (13 campos opcionales al final)
- [x] `Mapeos.cs`: destapados los 15 `null` (Create + Update) y lectura en `MapToLevanteDto`
- [x] `Crud.cs`: gate semana 14 + flag por empresa (fail-closed por `farms.company_id`), `null` = conservar en Update
- [x] Blindaje de pérdida silenciosa en `SeguimientoDiarioService` (`teneManualExist`, `FilaSinContenido`, `MergearManualSobreTrasladoAsync`)
- [x] `dotnet build` 0 errores / 0 warnings nuevos

## Fase 4 — Backend: arrastre al liquidar + SUMA el mismo día
- [x] `IArrastreHuevosLevanteService` + `ArrastreHuevosLevanteService` (suma en BD, upsert con delta por marca, espejo)
- [x] Enganche transaccional en `LotePosturaLevanteService.CerrarLoteYCrearProduccionAsync`
- [x] Total de huevos en `GetResumenCierreAsync` (`CierreLoteLevanteResumenDto`)
- [x] Rama de merge en `ProduccionService.CrearSeguimientoAsync` (sólo filas marcadas ⇒ el 400 actual intacto)
- [x] DI en `Program.cs`
- [x] `dotnet build` + `dotnet test` verdes (0/0 warnings · 825/825)

## Fase 5 — Frontend
- [x] `capturaHuevosEnLevante` en `CompanyFlags` + `FLAGS_APAGADOS` (fail-closed)
- [x] `funciones/` puras: `totales-huevos-levante`, `semana-vida-levante` + `README.md`; `models/huevo-levante.model.ts`
- [x] `modal-create-edit`: 3er tab «Huevos», 12 controles, auto-cálculo memoizado, payload, rehidratación, reset
- [x] `seguimiento-lote-levante-list`: `[fechaEncaset]` bindeado, total readonly en el modal de cierre, toast en el error
- [x] `modal-detalle-seguimiento`: sección «Huevos»
- [x] `services/seguimiento-lote-levante.service.ts`: campos de huevos en los DTOs TS
- [x] `yarn build` 0 errores (sólo el warning de bundle budget preexistente)

## Fase 6b — Bug encontrado durante la validación (bloqueaba el merge)
- [x] `date_trunc('day', timestamptz)` trunca en la zona de la SESIÓN de la BD ⇒ `x.Fecha.Date == fecha.Date` en LINQ **nunca casaba** con una sesión no-UTC (la local es `America/Bogota`): el chequeo de duplicado de producción no encontraba la fila y el merge no se disparaba (creaba una 2ª fila del mismo día)
- [x] `FechasPuras.RangoDiaUtc` (puro, 4 tests nuevos) + aplicado en los 2 chequeos de duplicado de `ProduccionService` y en el lookup de `ArrastreHuevosLevanteService` — rango semiabierto UTC, correcto en cualquier zona y sargable
- [x] Efecto colateral (mejora): en una BD con sesión no-UTC el 400 por duplicado de producción ahora **sí** se detecta (antes se colaba una fila por día repetida)

## Fase 6 — Validación y cierre
- [x] Smoke API local con JWT minteado: gate semana 13/14, GET de vuelta, PUT conserva, merge de fila sólo-huevos, liquidación sin/con huevos, **SUMA el mismo día (200, una sola fila)**, 400 preservado, doble liquidación, reapertura, idempotencia con edición intermedia, flag OFF sin cambios — **58/58 verdes** (20 captura en levante + 38 liquidación/arrastre)
- [x] Smoke UI en dev server con sesión inyectada: tab oculto en semana 13 / visible en 14, totales readonly, cambio de fecha oculta el tab, modal de cierre con el total, 0 errores de consola, sin NG0103 — **verificado**: borde exacto día 90 (oculto) vs día 91 (visible); totales 900 inc + 90 = 990 (91%); guardado real desde la UI persistido (427/340/peso 59.25); modal de cierre readonly «Totales 2.092 · Incubables 1.890»; **flag OFF ⇒ tab oculto en semana 41** (cero cambios visibles); consola sin errores
- [x] Servidores detenidos (sin procesos huérfanos) + BD local restaurada al estado original (38 filas / 0 huevos en el lote 114, lotes 6 y 7 «Abierto», sin LPP de prueba)
- [ ] Commit — **pendiente de tu confirmación** (46 archivos listos, working tree sin commitear; tu commit paralelo `14a8bfa` no toca ninguno de estos archivos)
- [x] Verificado contra la regla nueva de `CLAUDE.md` (Angular 22 = OnPush por defecto): los 3 componentes tocados ya declaran `ChangeDetectionStrategy.Eager` explícito
