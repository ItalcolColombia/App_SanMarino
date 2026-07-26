# Plan — Alcance granular por usuario-granja (núcleo / galpón / lote o global)

**Fecha:** 2026-07-26 · **Alcance:** global (todas las empresas/países) · **Estado BD:** aditivo, retro-compatible

## 1. Objetivo

Al asignar una granja a un usuario (módulo Usuarios → asignación de granjas), poder además restringir el acceso DENTRO de la granja a ciertos **núcleos, galpones y/o lotes**, o dejarlo **global por granja** (comportamiento actual). Ese alcance debe aplicarse en **todos los filtros/catálogos y lecturas de datos operativos** de la app, con excepciones de negocio: módulos que **envían a otras granjas** (traslado de aves, movimientos/traslados de inventario) mantienen el selector de **DESTINO sin restricción**; el **ORIGEN sí** queda restringido.

## 2. Modelo de datos (migración EF idempotente, aditiva)

### 2.1 `user_farms` — flag explícito de modo
- Nueva columna: `restrict_locations boolean NOT NULL DEFAULT false`.
- `false` (default) = acceso **global** a la granja → comportamiento previo **byte a byte idéntico** para todo usuario existente.
- `true` = acceso restringido a la unión de filas en `user_farm_scopes`. **Fail-closed:** flag ON + 0 filas ⇒ no ve nada de esa granja.
- Razón del flag (vs "0 filas = global"): los CASCADE (borrar núcleo/galpón/lote o `fn_rekey_*`) pueden eliminar filas de scope; sin flag, el usuario caería a global (fail-open). Con flag queda MÁS restringido (fail-closed) hasta que un admin re-asigne.

### 2.2 Nueva tabla `user_farm_scopes` (una fila = un permiso en un nivel)
| Columna | Tipo | Nota |
|---|---|---|
| id | int identity PK | |
| user_id | uuid NOT NULL | junto con farm_id → FK `user_farms` ON DELETE CASCADE |
| farm_id | int NOT NULL | |
| nucleo_id | varchar(64) NULL | FK compuesta `(nucleo_id, farm_id)` → `nucleos(nucleo_id, granja_id)` ON DELETE CASCADE |
| galpon_id | varchar(64) NULL | FK → `galpones(galpon_id)` ON DELETE CASCADE |
| lote_id | int NULL | FK → `lotes(lote_id)` ON DELETE CASCADE |
| created_at | timestamptz NOT NULL default now | |
| created_by_user_id | uuid NOT NULL | |

- CHECK: exactamente UNO de (nucleo_id, galpon_id, lote_id) no nulo.
- 3 índices únicos parciales anti-duplicado por nivel.
- **No se tocan las `fn_rekey_*`:** al mover/renombrar núcleo o galpón (copy+delete) el CASCADE elimina los grants de ese lugar → fail-closed; re-asignar desde admin. Lote movido de granja (UPDATE, PK estable): el resolver junta `lotes.granja_id = scope.farm_id`, por lo que la fila muerta no otorga nada.

### 2.3 Semántica de resolución (cierre)
- **Grant de núcleo** ⇒ todos sus galpones y lotes.
- **Grant de galpón** ⇒ todos sus lotes; su núcleo queda **visible** (navegación).
- **Grant de lote** (tabla `lotes` — reproductora levante/producción) ⇒ ese lote; su galpón y núcleo quedan **visibles**.
- Conjuntos resultantes: `NucleosVisibles: Set<string>`, `GalponesVisibles: Set<string>`, `LotesPermitidos: Set<int>`.
- **Módulos engorde y postura** (tablas `lote_ave_engorde`, `lote_postura_levante/produccion`) no tienen FK a `lotes`: se gobiernan por `GalponesVisibles`/`NucleosVisibles` (el nivel lote del scope aplica a la tabla `lotes`). Documentado en UI/QA.

## 3. Backend

### 3.1 Application
- `Application/Calculos/UserLocationScopeCalculos.cs` (static, puro, sin EF): computa el cierre (sets) desde filas proyectadas. **Tests xUnit obligatorios** en `tests/ZooSanMarino.Application.Tests/UserLocationScopeCalculosTests.cs`: flag OFF ⇒ global idéntico; grants por nivel; mixtos; ON + vacío ⇒ sets vacíos; lote fuera de la granja excluido.
- DTOs: `UserFarmScopeItemDto` (level + ids + nombres display), `UserFarmScopeConfigDto { RestrictLocations, Items[] }`, `UpdateUserFarmScopeDto`.
- Interfaces: `IUserFarmScopeAdminService` (get/replace config) e `ILocationScopeResolver` (`GetScopeAsync(Guid userId, int farmId)` → resultado con IsGlobal + sets; caché per-request).

### 3.2 Infrastructure
- Entidad `UserFarmScope` + `UserFarmScopeConfiguration` + DbSet; `UserFarm.RestrictLocations` + colección `Scopes`.
- `UserFarmScopeService` (admin CRUD, valida pertenencia de cada item a la granja — 400 si no) y `LocationScopeResolver` (query única por (user,farm), joins con catálogos, cierre vía Calculos, cache request-scoped). DI en Program.cs.

### 3.3 Puntos de aplicación (choke points)
1. **Catálogos** (cubren la cascada granja→núcleo→galpón→lote de TODOS los filtros de contexto):
   - `NucleoService` (list/por granja) → `NucleosVisibles`.
   - `GalponService` (por granja/núcleo) → `GalponesVisibles`.
   - `LoteService` (listados/search/getById) → `LotesPermitidos`.
2. **Datos con acceso directo por loteId** (seguimientos, indicadores, historial…): validar lote ∈ scope (fail-closed vacío/403) — según mapa del barrido.
3. **Reportes/consultas granja-completa**: restringir el conjunto de lotes/galpones cuando restricted.
4. **Módulos engorde/postura**: filtrar por `GalponesVisibles` (+ núcleos) en sus servicios.
5. **Excepciones (destino en otra granja):** catálogo de DESTINO de traslados de aves / movimientos-traslados de inventario NO se restringe; origen sí. Módulos exentos (admin/catálogos globales) sin cambio.
> El mapa módulo-por-módulo (A aplica / B parcial / C exento) sale del barrido con agentes y se anexa al final de este plan (§7) + reporte de QA.

### 3.4 API (mismo módulo UserFarm)
- `GET  api/UserFarm/user/{userId}/farm/{farmId}/scope` → config actual (flag + items con nombres).
- `PUT  api/UserFarm/user/{userId}/farm/{farmId}/scope` → reemplazo transaccional.
- `GET  api/UserFarm/farm/{farmId}/locations-tree` → árbol núcleos→galpones→lotes para el modal de administración.
- `UserFarmDto`/listas: exponer `restrictLocations` (+ conteo de items) para la UI.

## 4. Frontend (Angular 22)
- Service TS: métodos `getScope`, `updateScope`, `getLocationsTree` en el service de user-farm existente; modelos en `models/`.
- Modal de asignación de granjas (módulo usuarios): por granja, selector **Global / Restringido**; si Restringido → árbol con checkboxes (núcleo→galpón→lote) con estados propagados; guardar = PUT.
- Filtros de contexto de los módulos: **sin cambios** (consumen los catálogos ya restringidos por el back). Verificar que pantallas con DESTINO usen los endpoints no restringidos.
- Toast/Confirm con primitivas compartidas; colores por tokens.

## 5. Validación
- `cd backend && dotnet build` + `dotnet test` (calculos + regresión).
- `cd frontend && yarn build`.
- Migrar BD local (5433) con dotnet-ef 10 (`~/.dotnet/tools-ef10`).
- Levantar back (:5002) + front (:4200) vía launch.json; smoke con JWT minteado:
  1. Usuario global → todo idéntico (regresión).
  2. Usuario restringido a galpón/lote → catálogos y datos solo de ese lugar.
  3. Restringido + 0 items → nada (fail-closed).
  4. Traslados/inventario: DESTINO sigue mostrando todas las granjas/ubicaciones; ORIGEN restringido.
- QA final: agente code-reviewer sobre el diff + barrido de filtros por módulo; commit sin atribución (autor moisesmurillo).

## 6. Riesgos
- **Regresión de visibilidad:** default false + flag OFF garantiza comportamiento idéntico; tests lo cubren.
- **Caídas de grants por CASCADE** (mover núcleo/galpón): fail-closed deliberado; documentado.
- **Multi-tabla de lotes:** nivel lote solo aplica a `lotes`; engorde/postura por galpón/núcleo (documentado en UI y QA).
- **Rendimiento:** resolver 1 query por (user,farm) por request + cache; usuarios globales no pagan costo (no filtro).

## 7. Mapa de módulos — dónde aplica el scope y dónde NO (barrido 79 controllers)

**Leyenda categoría:** A = scope aplica completo · B = parcial (ORIGEN sí, DESTINO libre) · C = exento (admin/catálogo global).
**Estado:** ✅ aplicado en este cambio · 🔁 heredado (consume catálogos/servicios ya scoped) · ⏳ granja-level (gap preexistente documentado, scope granular no aplicado aún) · — sin cambio (exento).

| Módulo | Cat. | Estado | Cómo queda |
|---|---|---|---|
| Catálogo Núcleos (`NucleoController`) | A | ✅ | Search/GetAll/GetByGranja/GetByFarmIds/Detail filtran `NucleosVisibles`; `paraDestino` en GetAll y GetByGranja |
| Catálogo Galpones (`GalponController`) | A | ✅ | 7 métodos filtran `GalponesVisibles`; `paraDestino` en GetAll/GetByGranja/GetByGranjaAndNucleo |
| Lotes (`LoteController`) | A | ✅ | GetAll/GetLotesLevante/Search: + alineación a granjas asignadas (cierra gap histórico solo-CompanyId) + `LotesPermitidos`; GetById fail-closed; `paraDestino` en GetAll |
| Lote Postura Levante (LPL) | A | ✅ | GetAll (+`paraDestino`) y GetByLoteId con predicado LoteId-preciso (fallback galpón/núcleo p/ legacy) |
| Lote Postura Producción (LPP) | A | ✅ | Ídem LPL (LoteId heredado del levante) |
| Lote Ave Engorde | A | ✅ | GetAll/Search/GetById por galpón/núcleo visibles (engorde no referencia `lotes` ⇒ nivel lote no aplica) |
| Filter-data Levante / Producción / Engorde / Reproductora / Rep. Ave Engorde / Mov. Pollo Engorde / Rep. Técnico Levante | A | 🔁 | Componen granjas+núcleos+galpones+lotes desde los servicios ya scoped (Farm asignadas + catálogos filtrados) |
| Seguimiento Diario Levante (por-lote, indicadores, resultado) | A | ✅ (agente B1) | Guard `PermiteLoteAsync` en lecturas por loteId |
| Producción (ListarSeguimiento, lotes-produccion) | A | ✅ (B1) | Guard por lote + filtro en listado (con `paraDestino` para modal destino) |
| Seguimiento Diario unificado | A | ✅ (B1) | Filtro componible por lote en granjas restringidas |
| Seguimiento Aves Engorde (+ Ecuador) | A | ✅ (B1) | Guard por galpón/núcleo del lote engorde resuelto |
| Inventario de Aves + Historial Inventario | A | ✅ (B1) | Predicado LoteId-preciso + fallback ubicación en listados/único; resumen por ubicación recortado |
| Vacunación cronograma por-lote | A | ✅ (B1) | Guard `PermiteLoteAsync` antes de la fn |
| Vacunación reportes (cumplimiento/detalle) | A | ✅ (B1) | `p_lote_ids` de las fns recibe `LotesPermitidos` cuando la granja está restringida |
| Vacunación filter-data (`fn_vacunacion_filter_data`) | A | ⏳ | El join `user_farms` vive en SQL (granja-level); extender la fn a scope granular queda para una migración posterior |
| Reporte Diario Costos Engorde | A | ✅ (B1) | Filas/columnas post-filtradas por `GalponesVisibles` |
| Reporte Indicador Panamá por-corrida | A | ✅ (B1) | Ídem por galpón |
| Informe Semanal Pollo Engorde | A | ✅ (B1) | `p_granja_ids` intersecado + post-filtro por galpón en restringidas |
| Reporte Contable filtros-disponibles | A | ✅ (B1) | Árbol granja→núcleo→galpón podado por scope |
| Reporte Técnico Levante/Producción por-lote | A | 🔁/⏳ | Filter-data heredado; endpoints directos `/{loteId}` siguen granja-level (guard granular pendiente) |
| Liquidaciones (Técnica, Ecuador, Comparación, Cierre Levante) | A | ⏳ | Acceso directo por loteId sin granja (gap preexistente); en UI siempre se llega vía filtros ya scoped |
| Lesiones, FarmInventory kardex, Dashboard (mock), LoteSeguimiento/LoteGalpon legacy, ProduccionDiaria/ProduccionLote/SeguimientoProduccion by-lote | A | ⏳ | Granja-level o CompanyId-only preexistente; UI llega vía filtros scoped. Guard granular pendiente (baja exposición) |
| **Traslados de aves / huevos / desde seguimiento** | B | ✅ | ORIGEN: filter-data y catálogos scoped. DESTINO: `Farm/traslado-seguimiento-diario` (sin scope, por diseño) + cascada núcleo/galpón/LPL/LPP/lotes-produccion con `paraDestino=true` |
| **Traslado de lote (mover/trasladar)** | B | ✅ | Modal destino: núcleos/galpones fresh con `paraDestino=true` |
| **Movimiento de Aves** | B | ✅ (B1) | Listados: fila visible si origen O destino pasan el scope; destino del modal con FiltroSelect `paraDestino` |
| **Movimiento Pollo Engorde (+ Panamá)** | B | 🔁 | Origen vía filter-data scoped; destino = otro lote del mismo catálogo del padre; ventas a cliente sin cambio |
| **Inventario Gestión (EC/PA)** | B | 🔁 | Ya separa FarmsOrigen (asignadas) / FarmsDestino (empresa completa); origen hereda catálogos scoped |
| **Farm Inventory Movements (transfer)** | B | ⏳ | Sin validación user_farms en ninguna dirección (gap preexistente, documentado) |
| Usuarios/Roles/Permisos/Menús, Empresas/País, Geografía, Clientes, Master lists, Guía genética (+raw), Catálogos alimentos/ítems, Configuration, Service tokens, DB Studio, Migraciones masivas, Excel import, Puente Panamá, Implementación, Tickets, Mapas | C | — | Exentos: administración/catálogos globales/carga masiva. `UserFarmController` es donde se ADMINISTRA el scope |

**Reglas trasversales aplicadas:** el scope granular se aplica incluso a roles admin (una restricción explícita por usuario-granja gana al bypass de rol); granjas sin `restrict_locations` no pagan ningún costo (cero filtros nuevos); `paraDestino=true` solo omite el scope granular — el scoping por granja asignada/empresa se mantiene.
