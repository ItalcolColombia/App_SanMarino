# Tracker — Alcance granular usuario-granja (núcleo/galpón/lote o global) + scope en todos los filtros

**Plan:** [fase_de_desarrollo/scope_ubicacion_usuario_granja_plan.md](fase_de_desarrollo/scope_ubicacion_usuario_granja_plan.md)

## Fase 0 — Exploración (agentes)
- [x] Lanzar A1 (sonnet): mapa backend scoping actual (UserFarm, catálogos, fns rekey)
- [x] Lanzar A2 (opus): barrido 60+ controllers → categorías A/B/C + puntos de inserción
- [x] Lanzar A3 (sonnet): front — asignación granjas, filtros cascada, pickers destino
- [x] Recibir y consolidar reportes de los 3 agentes (mapa se anexa al plan §7 al cierre)

## Fase 1 — BD + Dominio
- [x] Entidad `UserFarmScope` + `UserFarm.RestrictLocations` + colección `Scopes`
- [x] `UserFarmScopeConfiguration` + DbSet en `ZooSanMarinoContext`
- [x] Migración EF idempotente `20260726070730_UserFarmLocationScope` (columna + tabla + FKs CASCADE + CHECK + índices únicos parciales)
- [x] Aplicar migración en BD local (:5433) sin error

## Fase 2 — Backend core
- [x] `UserLocationScopeCalculos` (puro) en Application/Calculos
- [ ] Tests xUnit `UserLocationScopeCalculosTests` (global idéntico / niveles / cierre / fail-closed / lote fuera de granja)
- [x] DTOs scope + `IUserFarmScopeService` + `ILocationScopeResolver`
- [x] `UserFarmScopeService` (admin, valida pertenencia) + `LocationScopeResolver` (query única + cache request) + DI
- [x] Endpoints en `UserFarmController`: GET/PUT scope + GET locations-tree; `restrictLocations` en DTOs de listas (build backend OK)

## Fase 3 — Enforcement (choke points) — COMPLETADA (núcleo + agente B1 opus)
- [x] `NucleoService` → NucleosVisibles (Search/GetAll/GetByGranja+paraDestino/GetByFarmIds/GetDetail)
- [x] `GalponService` → GalponesVisibles (Search/GetAllDetail/GetAll+paraDestino/GetByGranja(+Nucleo)+paraDestino/GetByFarmIds/DetailByGranjaNucleo)
- [x] `LoteService` → LotesPermitidos (GetAll+alineación granjas asignadas+paraDestino / GetLotesLevante / Search / GetById)
- [x] Acceso directo por loteId (agente B1): seguimiento levante/producción/diario unificado, engorde CO+EC (por galpón), inventario aves, historial, vacunación por-lote — guards fail-closed
- [x] Reportes granja-completa (B1): costos engorde (recalcula totales + calc puro con 3 tests), indicador Panamá por-corrida, informe semanal, reporte contable filtros (poda por lotes visibles), vacunación reportes (post-filtro filas)
- [x] Movimiento aves Search: visible si origen O destino pasa el scope (B1)
- [x] LPL/LPP + LoteAveEngorde por predicado LoteId-preciso / galpón-núcleo
- [x] Excepciones destino verificadas por smoke: `paraDestino=true` devuelve catálogo completo
- [x] FIX descubierto en smoke: PUT scope moría con "Collection was modified" (bug latente de SetAuditFields del contexto → Attach dentro del foreach); reescrito el reemplazo con ExecuteDelete/Update + INSERT parametrizado en transacción (chip creado para arreglar el contexto aparte)

## Fase 4 — Frontend
- [x] Modelos + service TS (getScope / updateScope / getLocationsTree) en `core/services/user-farm`
- [x] Modal asignación granjas: botón + badge "Restringido (n)" + sub-modal `configurar-alcance-granja` (Global/Restringido + árbol checkboxes con cierre visual)
- [x] paraDestino en services front (nucleo levante/producción/canónico, galpón, lote, LPL, LPP, obtenerLotesProduccion) y en los 5 flujos de DESTINO: modal-traslado-aves-seguimiento, modal-traslado-lote, traslado-form (HierarchicalFilter [paraDestino]), modal-movimiento-aves (FiltroSelect [paraDestino])
- [x] `yarn build` OK (solo warning bundle budget preexistente)

## Fase 5 — Validación y cierre
- [x] `dotnet build` 0 errores 0 warnings + `dotnet test` 751/751 verde (750 Application + 1 Domain; incluye 20 de scope + 3 de costos engorde)
- [x] `yarn build` 0 errores (solo warning bundle budget preexistente)
- [x] Back :5002 + front :4200 levantados; smoke JWT completo: global idéntico ✔ / galpón G0010 ✔ / lote 13 preciso (14 → 404, LPL 2 excluido) ✔ / fail-closed 0 items ✔ / paraDestino catálogo completo ✔ / 400 sin persistir ✔ / guard por-lote ✔
- [x] Mapa final de módulos (A/B/C) anexado al plan §7
- [x] Commit feature `d492eed` (autor moisesmurillo, sin atribución)
- [x] QA final (code-reviewer fable sobre el diff): 0 críticos, 2 altos, 3 medios, 5 bajos, veredicto inicial NO APTO
- [x] Fixes QA aplicados y verificados en vivo: A1 gate admin en endpoints de scope (403 al auto-des-restringirse), A2 filtro en TODAS las lecturas de Movimiento Aves, M1 gates de mutación en LoteService, M2 guards resumen-mortalidad/historial, M3 SeguimientoDiario por id, B1 getters catálogo, B3 409 en carrera FK — build 0/0, tests 750/750, smoke verde
- [x] Commit de fixes QA (ver git log)
- Follow-ups bajos documentados en plan §8 (B2/B4/B5/P1) + chip pendiente: fix SetAuditFields del contexto
