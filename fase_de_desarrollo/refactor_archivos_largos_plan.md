# Plan — Reducción de deuda técnica: archivos largos (backend + frontend)

> Misión: auditar `backend/src/**/*.cs` (excluyendo `Migrations/` y tests) y `frontend/src/**/*.ts`/`*.html` (excluyendo `*.spec.ts`), rankear por líneas, agrupar por módulo/bounded context y planificar la partición **sin cambiar comportamiento** (refactor ≠ cambio de comportamiento). Patrón de referencia obligatorio: módulo `movimientos-pollo-engorde` (ver `CLAUDE.md` § Clean Code).

## 1. Enfoque arquitectónico

- **Ningún archivo cambia de responsabilidad de negocio**, solo de organización física. Se preserva: firma pública de la interfaz (`IXxxService`), namespace, DI, rutas de controller, `Math.Round`/orden de operaciones, contratos de DTO.
- **Backend:** service largo → `partial class` por responsabilidad en `Funciones/<Modulo>Service.<Concern>.cs`; el archivo ancla original conserva usings, campos, ctor, constantes, helpers estáticos compartidos y la interfaz. Cálculo puro (sin `_ctx`/EF/estado) → `Application/Calculos/<Modulo>Calculos.cs` (`static class`) + tests xUnit de equivalencia. Filtrado/agregación pesada multipaís → empujar a SQL (vista/función en `/backend/sql/`), no a la partición en C#.
- **Controllers largos:** mismo patrón `partial class` en `Controllers/Funciones/<Controller>.<Concern>.cs` cuando el controller agrupa endpoints de concerns claramente distintos (ej. CRUD vs. operaciones de negocio vs. reportes). Si el controller es solo "muchos endpoints CRUD parecidos", evaluar mover lógica pesada al service antes de partir el controller (los controllers deben ser delgados; si ya lo son y solo son largos por volumen de endpoints, particionar igual por concern).
- **Frontend:** componente largo → extraer:
  - `models/<concepto>.model.ts` para tipos hoy inline (con re-export desde el componente si algo externo los importa).
  - `funciones/<accion>.funcion.ts` — funciones **puras** (sin `this`, sin DI) por acción/botón/cálculo/mapeo.
  - El componente queda como orquestador delgado: estado, inputs, llamadas HTTP, delega a `funciones/`.
  - Templates `.html` muy largos con secciones repetidas/condicionales grandes → extraer a subcomponentes de presentación (dumb components) cuando hay bloques cohesivos reutilizables (tabs, tablas, modales anidados); si es una sola tabla ancha sin repetición, no partir el HTML artificialmente (preferir mover lógica de columnas/formato a `funciones/` en vez de fragmentar markup sin necesidad — YAGNI).
- **No se toca la aritmética ni el orden de validaciones.** Cada partición se valida con `dotnet build`/`yarn build` + tests de equivalencia antes de avanzar al siguiente archivo.
- **Impacto multipaís:** varios de los servicios más grandes (`MovimientoAvesService`, `SeguimientoAvesEngordeService`, `IndicadorEcuadorService`, `InventarioGestionService`) tienen lógica condicional por país (`companyId`/`paisId`). Al particionar, agrupar por **concern de negocio**, no por país — evita duplicar branches y mantiene una sola fuente de verdad por regla.

## 2. Auditoría — ranking por líneas

### 2.1 Backend (`.cs`, excluye `Migrations/` y `tests/`)

**Total archivos `.cs` en `src/`:** 695. Umbral de intervención: **> 500 líneas** (41 archivos). Confirmado + corregido lo detectado previamente (algunos números reales difieren del estado previo):

| Líneas | Archivo | Módulo/bounded context | Nota |
|---|---|---|---|
| **3110** | `Infrastructure/Services/ReporteTecnicoService.cs` | Reportes técnicos | **El más largo del repo**, no estaba en el radar previo |
| **2507** | `Infrastructure/Services/MovimientoAvesService.cs` | Movimientos de aves | Confirmado |
| **2296** | `Infrastructure/Services/InventarioGestionService.cs` | Inventario (gestión general) | No estaba en el radar previo |
| **1953** | `Infrastructure/Services/ReporteTecnicoProduccionService.cs` | Reportes técnicos (producción) | No estaba en el radar previo |
| **1884** | `Infrastructure/Services/SeguimientoAvesEngordeService.cs` | Seguimiento aves engorde | Confirmado |
| 1458 | `Infrastructure/Services/ReporteContableService.cs` | Reportes contables | — |
| 1185 | `Infrastructure/Services/IndicadorEcuadorService.cs` | Indicadores Ecuador | Confirmado (1185 exacto) |
| 1183 | `Infrastructure/Services/TicketService.cs` | Tickets/soporte | — |
| 1142 | `Infrastructure/Services/ProduccionService.cs` | Lote/Producción | — |
| 1089 | `Infrastructure/Services/LoteService.cs` | Lote | — |
| 1087 | `Infrastructure/Services/SeguimientoAvesEngordeEcuadorService.cs` | Seguimiento aves engorde (Ecuador) | Confirmado (1087 exacto) |
| 1019 | `API/Controllers/MovimientoAvesController.cs` | Movimientos de aves | Confirmado (1019 exacto) |
| 938 | `Infrastructure/Services/TrasladoHuevosService.cs` | Traslados huevos | — |
| 921 | `Infrastructure/Services/FarmService.cs` | Granjas | — |
| 853 | `Infrastructure/Services/ReporteTecnicoExcelService.cs` | Reportes técnicos (export) | — |
| 848 | `API/Program.cs` | Startup/DI | **Excluido del refactor `partial`** (ver §4 nota) |
| 840 | `Infrastructure/Services/SeguimientoDiarioService.cs` | Seguimiento diario | — |
| 837 | `Infrastructure/Services/SeguimientoLoteLevanteService.cs` | Lote levante | Confirmado (837 exacto) |
| 794 | `Infrastructure/Services/MovimientoPolloEngorde/Funciones/…Crud.cs` | Movimientos pollo engorde | Ya particionado (referencia); candidato a sub-dividir si crece más |
| 790 | `Infrastructure/Services/RoleCompositeService.cs` | Roles/permisos | — |
| 757 | `Infrastructure/Services/ExcelImportService.cs` | Importación Excel | — |
| 733 | `Infrastructure/Services/AuthService.cs` | Auth | — |
| 706 | `Infrastructure/Services/GuiaGeneticaEcuadorService.cs` | Guía genética Ecuador | — |
| 672 | `Infrastructure/Services/ProduccionAvicolaRawService.cs` | Producción (raw/SQL) | — |
| 667 | `Infrastructure/Services/GalponService.cs` | Galpón | — |
| 643 | `Infrastructure/Services/LoteAveEngordeService.cs` | Lote aves engorde | — |
| 640 | `Infrastructure/Services/ReporteContableExcelService.cs` | Reportes contables (export) | — |
| 612 | `Infrastructure/Services/LiquidacionTecnicaEcuadorService.cs` | Liquidación técnica Ecuador | — |
| 590 | `Infrastructure/Services/InventarioAvesService.cs` | Inventario aves | — |
| 580 | `Infrastructure/Services/UserService.cs` | Usuarios | — |
| 572 | `Infrastructure/Services/ReporteTecnicoProduccionExcelService.cs` | Reportes técnicos producción (export) | — |
| 570 | `Infrastructure/Services/ExportacionExcelService.cs` | Exportación Excel genérica | — |
| 562 | `Infrastructure/Services/LoteReproductoraAveEngordeService.cs` | Lote reproductora/engorde | — |
| 550 | `Infrastructure/Services/InventarioGastoService.cs` | Gastos de inventario | — |
| 535 | `Infrastructure/Services/CorreccionAvesDisponiblesEngordeService.cs` | Seguimiento aves engorde | — |
| 526 | `API/Controllers/AuthController.cs` | Auth | — |
| 525 | `Infrastructure/Services/FarmInventoryMovementService.cs` | Inventario granja | — |
| 519 | `Infrastructure/Services/MovimientoPolloEngorde/Funciones/…ResumenDisponibilidad.cs` | Movimientos pollo engorde | Ya particionado |
| 519 | `API/BackgroundServices/EmailQueueProcessorService.cs` | Email/notificaciones | — |
| 518 | `Infrastructure/Services/LiquidacionTecnicaComparacionService.cs` | Liquidación técnica | — |
| 516 | `Infrastructure/Services/MapaService.cs` | Mapa/geografía | — |

**Controllers adicionales relevantes (300–500 líneas, no urgentes pero en el radar):** `DbStudioController.cs` (449), `ReporteTecnicoController.cs` (426), `TrasladoNavigationController.cs` (419), `InventarioAvesController.cs` (405), `ProduccionController.cs` (390), `UserFarmController.cs` (382), `ReporteTecnicoProduccionController.cs` (365), `DashboardController.cs` (351), `UsersController.cs` / `InventarioGestionController.cs` (344), `MovimientoPolloEngordeController.cs` (337). No se atacan en esta ronda (umbral 500); quedan en el tracker como backlog si el gate baja el umbral.

### 2.2 Frontend (`.ts` sin `*.spec.ts`, `.html`)

**Total `.ts` no-spec:** 373. **Total `.html`:** 162. Umbral de intervención: **> 600 líneas** (componentes con lógica) / **> 800 líneas** (templates).

**TypeScript (componentes/servicios), > 600 líneas:**

| Líneas | Archivo | Módulo |
|---|---|---|
| 2200 | `features/lote-levante/pages/modal-create-edit/modal-create-edit.component.ts` | Lote levante |
| 2135 | `features/engorde-comun/pages/modal-seguimiento-engorde/modal-seguimiento-engorde.component.ts` | Engorde común |
| 1663 | `features/traslados-aves/pages/inventario-dashboard/inventario-dashboard.component.ts` | Traslados aves |
| 1593 | `features/lote/components/lote-list/lote-list.component.ts` | Lote |
| 1565 | `features/gestion-inventario/pages/gestion-inventario-page/gestion-inventario-page.component.ts` | Gestión inventario |
| 1381 | `features/lote-produccion/pages/modal-seguimiento-diario/modal-seguimiento-diario.component.ts` | Lote producción |
| 1245 | `features/lote-levante/pages/graficas-principal/graficas-principal.component.ts` | Lote levante |
| 1128 | `features/indicador-ecuador/pages/indicador-ecuador-list/indicador-ecuador-list.component.ts` | Indicador Ecuador |
| 1109 | `features/movimientos-pollo-engorde/pages/movimientos-pollo-engorde-list/movimientos-pollo-engorde-list.component.ts` | Mov. pollo engorde (referencia — ya tiene `funciones/`, ver si esta lista puede delegar más) |
| 1078 | `features/lote-levante/pages/seguimiento-lote-levante-list/seguimiento-lote-levante-list.component.ts` | Lote levante |
| 1047 | `features/lote-levante/components/liquidacion-tecnica/liquidacion-tecnica.component.ts` | Lote levante |
| 1045 | `features/movimientos-aves/components/modal-movimiento-aves/modal-movimiento-aves.component.ts` | Movimientos aves |
| 984 | `features/config/role-management/role-management.component.ts` | Config/roles |
| 973 | `features/lote-produccion/pages/lote-produccion-list/lote-produccion-list.component.ts` | Lote producción |
| 935 | `features/lote-reproductora/pages/lote-reproductora-list/lote-reproductora-list.component.ts` | Lote reproductora |
| 914 | `features/reportes-tecnicos/services/reporte-tecnico.service.ts` | Reportes técnicos |
| 895 | `features/dashboard/dashboard.component.ts` | Dashboard |
| 767 | `features/movimientos-pollo-engorde/components/modal-movimiento-pollo-engorde/modal-movimiento-pollo-engorde.component.ts` | Mov. pollo engorde |
| 766 | `features/lote-levante/pages/tabs-principal/tabs-principal.component.ts` | Lote levante |
| 763 | `features/farm/components/farm-list/farm-list.component.ts` | Granjas |
| 731 | `features/lote-engorde/components/lote-engorde-list/lote-engorde-list.component.ts` | Lote engorde |
| 717 | `features/traslados-huevos/components/modal-traslado-huevos/modal-traslado-huevos.component.ts` | Traslados huevos |
| 715 | `features/traslados-aves/services/traslados-aves.service.ts` | Traslados aves |
| 715 | `features/config/user-management/components/modal-create-edit/modal-create-edit.component.ts` | Config/usuarios |
| 692 | `features/lote/components/modal-create-edit-lote/modal-create-edit-lote.component.ts` | Lote |
| 684 | `features/aves-engorde/pages/seguimiento-aves-engorde-list/seguimiento-aves-engorde-list.component.ts` | Aves engorde |
| 675 | `features/config/company-management/company-management.component.ts` | Config/empresas |
| 629 | `features/lote-produccion/pages/graficas-principal/graficas-principal.component.ts` | Lote producción |
| 619 | `features/lote-levante/pages/tabla-lista-indicadores/tabla-lista-indicadores.component.ts` | Lote levante |

**HTML, > 800 líneas:**

| Líneas | Archivo | Módulo |
|---|---|---|
| 1827 | `features/traslados-aves/pages/inventario-dashboard/inventario-dashboard.component.html` | Traslados aves |
| 1487 | `features/indicador-ecuador/pages/indicador-ecuador-list/indicador-ecuador-list.component.html` | Indicador Ecuador |
| 1345 | `features/lote/components/lote-list/lote-list.component.html` | Lote |
| 1236 | `features/gestion-inventario/pages/gestion-inventario-page/gestion-inventario-page.component.html` | Gestión inventario |
| 1085 | `features/config/role-management/role-management.component.html` | Config/roles |
| 1068 | `features/lote-reproductora/pages/lote-reproductora-list/lote-reproductora-list.component.html` | Lote reproductora |
| 819 | `features/lote-engorde/components/lote-engorde-list/lote-engorde-list.component.html` | Lote engorde |

(36 archivos `.html` superan 400 líneas y 58 `.ts` superan 400; se listan solo los prioritarios. El resto queda en backlog del tracker si se decide bajar el umbral en una fase posterior.)

## 3. Orden de ataque (staged, cada etapa deja el sistema desplegable)

Criterio de prioridad: **tamaño × superficie de impacto × riesgo de regresión en cálculo**. Los servicios con aritmética de negocio (indicadores, liquidaciones, movimientos) van con doble red (tests de equivalencia) antes que los de solo CRUD/UI.

**Etapa 0 — Preparación (sin código funcional):**
- Este plan + tracker en `tracker_estado.md`.
- Confirmar con el usuario el umbral final (500 backend / 600–800 frontend) antes de tocar el primer archivo.

**Etapa 1 — Backend, cálculo puro de mayor riesgo (indicadores/liquidaciones):**
1. `IndicadorEcuadorService.cs` (1185) → extraer cálculo puro a `Application/Calculos/IndicadorEcuadorCalculos.cs` (¡ya existe el archivo, ver §5 gotcha!) + partials `Funciones/IndicadorEcuadorService.{Consolidado,Liquidacion,PorLotePadre}.cs`.
2. `SeguimientoAvesEngordeService.cs` (1884) + `SeguimientoAvesEngordeEcuadorService.cs` (1087) + `CorreccionAvesDisponiblesEngordeService.cs` (535) — mismo bounded context, revisar duplicación entre EC y genérico antes de partir.
3. Tests de equivalencia xUnit para cada cálculo movido.

**Etapa 2 — Backend, Movimientos de aves (mayor archivo de dominio):**
4. `MovimientoAvesService.cs` (2507) → partials por concern (Consulta/Búsqueda, Creación/Validación, Ejecución Venta/Traslado, Estadísticas), siguiendo el patrón `MovimientoPolloEngorde/Funciones/`.
5. `MovimientoAvesController.cs` (1019) → partials por concern (Consulta, CRUD, Operaciones de negocio) — ver endpoints listados en §5.
6. Validación: tests de integración de movimientos existentes + smoke manual de los flujos de traslado/venta.

**Etapa 3 — Backend, Reportes (el bounded context con más volumen total):**
7. `ReporteTecnicoService.cs` (3110, el más grande del repo) + `ReporteTecnicoProduccionService.cs` (1953) + `ReporteContableService.cs` (1458) + sus `*ExcelService` (853/640/572) → agrupar por sub-reporte (ej. Bultos, Consolidado, por lote) en `Funciones/`. Alto riesgo de romper Excel exportado — regresión visual/numérica a validar contra un reporte real conocido.

**Etapa 4 — Backend, Inventario y Traslados:**
8. `InventarioGestionService.cs` (2296), `InventarioAvesService.cs` (590), `InventarioGastoService.cs` (550), `FarmInventoryMovementService.cs` (525), `TrasladoHuevosService.cs` (938).

**Etapa 5 — Backend, resto (Lote/Producción/Auth/Tickets/Farm):**
9. `ProduccionService.cs` (1142), `LoteService.cs` (1089), `SeguimientoDiarioService.cs` (840), `SeguimientoLoteLevanteService.cs` (837), `TicketService.cs` (1183), `AuthService.cs`+`AuthController.cs` (733+526), `FarmService.cs` (921), `RoleCompositeService.cs` (790), `GuiaGeneticaEcuadorService.cs` (706), `GalponService.cs` (667), `LoteAveEngordeService.cs` (643), `LiquidacionTecnicaEcuadorService.cs`+`LiquidacionTecnicaComparacionService.cs` (612+518), `UserService.cs` (580), `LoteReproductoraAveEngordeService.cs` (562), `ExcelImportService.cs`(757)/`ExportacionExcelService.cs`(570), `MapaService.cs`(516), `EmailQueueProcessorService.cs`(519).

**Etapa 6 — Frontend, componentes de mayor riesgo (modales con lógica de negocio):**
10. `modal-create-edit.component.ts` (lote-levante, 2200), `modal-seguimiento-engorde.component.ts` (2135), `modal-seguimiento-diario.component.ts` (lote-produccion, 1381), `modal-movimiento-aves.component.ts` (1045) → extraer `models/` + `funciones/` (validación de formulario, mapeo DTO, cálculos de indicadores en vivo).

**Etapa 7 — Frontend, listados grandes (componente + template):**
11. Pares componente+template que superan ambos umbrales (mismo módulo, alto acoplamiento): `traslados-aves/inventario-dashboard` (1663 ts / 1827 html), `lote/lote-list` (1593 ts / 1345 html), `gestion-inventario-page` (1565 ts / 1236 html), `indicador-ecuador-list` (1128 ts / 1487 html), `lote-reproductora-list` (935 ts / 1068 html), `config/role-management` (984 ts / 1085 html), `lote-engorde-list` (731 ts / 819 html).

**Etapa 8 — Frontend, resto (gráficas, servicios, listados medianos):**
12. `graficas-principal` (lote-levante 1245 / lote-produccion 629), `seguimiento-lote-levante-list` (1078), `liquidacion-tecnica.component.ts` (1047), `lote-produccion-list` (973), `reporte-tecnico.service.ts` (914), `dashboard.component.ts` (895), y el resto del backlog 400–600.

**Etapa 9 — Validación final:**
13. `dotnet build` + `dotnet test` completos (backend), `yarn build` + `yarn test` completos (frontend).
14. Re-correr el ranking de líneas y confirmar que ningún archivo tocado quedó por encima del umbral acordado; documentar excepciones justificadas (ej. `Program.cs`).
15. Cerrar tracker y actualizar `knowledge/architecture.md` si cambió la forma de encender los servicios.

> Cada paso 1–12 es una migración independiente: build+test verdes antes de pasar al siguiente archivo. Ningún paso bloquea el release — el sistema es desplegable después de cada uno.

## 4. Archivos/servicios a crear o modificar (resumen técnico)

- **Backend, por cada servicio de Etapas 1–5:** nuevo directorio `Infrastructure/Services/<Modulo>/Funciones/` con `partial class` por concern + archivo ancla que retiene interfaz/ctor/campos. Nuevo/actualizado `Application/Calculos/<Modulo>Calculos.cs` para lógica pura. Nuevos tests en `tests/ZooSanMarino.Application.Tests/<Modulo>CalculosTests.cs`.
- **Backend, controllers de Etapa 2:** `API/Controllers/Funciones/<Controller>.<Concern>.cs` (mismo patrón partial, namespace plano `ZooSanMarino.API.Controllers`).
- **`Program.cs` (848 líneas) — excluido del patrón `partial`:** es composición de DI/middleware, no lógica de dominio. Si se decide reducirlo, la vía correcta es extraer `AddXxxServices(this IServiceCollection)` / `UseXxxMiddleware(this IApplicationBuilder)` como extension methods en `API/Extensions/`, no partir la clase `Program`. Se deja fuera de esta ronda salvo pedido explícito.
- **Frontend, por cada componente de Etapas 6–8:** `features/<modulo>/models/<concepto>.model.ts` (si aplica) + `features/<modulo>/funciones/<accion>.funcion.ts` por acción/cálculo extraído + `README.md` de convención (copiar de `movimientos-pollo-engorde/funciones/README.md`).
- **Frontend, templates de Etapa 7:** evaluar caso por caso extracción a subcomponentes de presentación; no partir por partir.

## 5. Reglas de negocio y gotchas detectados durante la auditoría

- **`Application/Calculos/IndicadorEcuadorCalculos.cs` ya existe.** Antes de mover cálculo desde `IndicadorEcuadorService.cs`, leer ese archivo primero — puede que parte del trabajo de extracción de Etapa 1 ya esté hecho o que haya que **fusionar/reutilizar**, no duplicar (Constitución #9: nunca duplicar una capacidad existente).
- **`MovimientoPolloEngordeService` ya está particionado** (`Funciones/{Crud,Auditoria,OrganizarPeso,ResumenDisponibilidad,VentaGranja}.cs` + `MovimientoPolloEngordeFilterDataService.cs` aparte). Es la referencia canónica citada en `CLAUDE.md` — copiar exactamente esta forma para `MovimientoAvesService` (mismo dominio, servicio hermano no unificado).
- **`MovimientoAvesController.cs`** ya tiene endpoints agrupables por concern reconocible: Consulta (`GetAll`, `Search`, `GetById`, `GetByNumero`, `GetPendientes`, `GetByLote`, `GetByUsuario`, `GetRecientes`, `GetUltimoNumeroDespacho`, `GetInformacionLote`), Validación (`ValidarFechaSeguimiento`, `ValidarMovimiento`), CRUD (`Create`, `Update`, `Eliminar`, `Cancelar`, `Procesar`), Operaciones de negocio (`TrasladoRapido`, `EjecutarVenta`, `EjecutarTraslado`, `EjecutarTrasladoCierreLevante`), Estadísticas (`GetEstadisticas`).
- **Reportes (Etapa 3) tienen mayor riesgo de regresión silenciosa:** Excel/PDF generado no tiene test automatizado exhaustivo hoy; según memoria del proyecto, el fix de inventario/alimento reciente ya tocó "Reporte Contable sección Bultos" — coordinar para no pisar cambios en curso ahí. Validar manualmente contra un reporte de referencia conocido antes y después.
- **Multipaís:** `IndicadorEcuadorService`, `SeguimientoAvesEngordeEcuadorService`, `InventarioGestionService` tienen variantes EC-específicas ya separadas de las genéricas (naming `*Ecuador*`); no fusionar durante el refactor — es una decisión de dominio ya tomada, fuera de alcance de esta misión.
- **BD local compartida entre branches/worktrees** (memoria `bd-compartida-entre-branches-worktrees`): este refactor no toca schema, así que no hay riesgo de migración cruzada, pero si algún split requiere mover un DTO a `Domain`, verificar que no rompe otras ramas activas del mismo repo.

## 6. Casos de prueba

**Backend (por cada servicio partido):**
- `dotnet build` sin errores ni warnings nuevos.
- `dotnet test` — suite existente sigue en verde.
- Para cálculo movido a `Application/Calculos/`: test de equivalencia xUnit `[Theory]` con inputs reales/representativos comparando output **antes vs. después** del move (mismo `Math.Round`, mismo orden de residuos).
- Para `MovimientoAvesService`/`MovimientoAvesController`: request real end-to-end de `Create` → `Procesar` → `GetById`, y de `EjecutarVenta`/`EjecutarTraslado` (según memoria: flujos de traslado/venta son sensibles a orden de validaciones).
- Para Reportes: generar el mismo reporte (mismo lote/periodo) antes y después del split, diff de valores clave (totales, bultos, consolidado).

**Frontend (por cada componente partido):**
- `yarn build` — 0 errores; único warning aceptado: bundle budget preexistente.
- `yarn test` (Karma) — specs existentes en verde.
- Verificación manual en navegador del golden path del componente (abrir modal, guardar, listar, exportar Excel si aplica) — no reemplazable por build/test según regla de UI de `CLAUDE.md`.
- Revisar que ningún getter/método de template pasó a devolver arrays/objetos nuevos por ciclo (memoria `ng0103-getters-arrays-nuevos`: bug recurrente de change detection en este repo).

## 7. Confirmación pendiente antes de ejecutar

Este documento es **solo auditoría + plan**; no se ha movido código todavía. Antes de arrancar Etapa 1 se necesita OK explícito del usuario sobre:
1. Umbral final de intervención (propuesto: backend > 500 líneas, frontend `.ts` > 600, `.html` > 800).
2. Orden de etapas propuesto (Indicadores/Liquidaciones → Movimientos → Reportes → Inventario/Traslados → resto backend → Frontend modales → Frontend listados → resto frontend).
3. Si `Program.cs` se aborda en esta misión (propuesta: fuera de alcance, solo extension methods si se pide explícitamente).
