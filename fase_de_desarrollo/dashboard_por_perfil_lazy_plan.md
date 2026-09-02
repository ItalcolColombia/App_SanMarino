# Dashboard por perfil, con datos reales y carga perezosa

> **Pedido (1-sep-2026):** organizar el dashboard para que muestre información y gráficas de todo lo
> que tenemos, con **carga perezosa por panel**, y que lo que se ve dependa del **perfil** (admin,
> técnico, administrativo), de los **permisos**, del **usuario** y de la **empresa**.
>
> **Decisión del usuario (tomada antes de escribir este plan):**
> 1. **Sin permisos nuevos** — se reusa el modelo de acceso que ya existe.
> 2. **Los 4 paneles** de la primera fase: Postura, Pollo engorde, Alimento e inventario,
>    Cumplimiento y pendientes.

---

## 0. Lo que se midió antes de planear (no es opinión)

Tres hechos verificados contra el código y la copia local de la BD. Los tres cambian el encuadre del
pedido, así que van primero.

### 0.1 🔴 El dashboard de hoy no muestra un solo dato real

Los **8 endpoints** de `backend/src/ZooSanMarino.API/Controllers/DashboardController.cs` devuelven
números inventados:

| Endpoint | Qué devuelve realmente |
|---|---|
| `estadisticas-generales` (`:50`) | Constantes: `TotalUsuarios = 25`, `TotalGranjas = 8`, `TotalLotes = 45`, `TotalInventarioAves = 12500` |
| `produccion-por-granja` (`:83`) | Granjas reales, cifras con **`new Random()`** (`:90`) |
| `registros-diarios` (`:118`) | **`new Random()`** (`:127`) |
| `actividades-recientes` (`:160`) | 6 filas fijas con nombres inventados: «Juan Pérez», «María García», «Carlos López» |
| `estadisticas-mortalidad` (`:237`) | 1 fila fija: `LOTE001` / «Granja Principal» |
| `distribucion-lotes` (`:267`) | Granjas reales y **todos los conteos en 0** con `// TODO: Implementar cuando esté disponible` |
| `estadisticas-inventario` (`:301`) | Constantes |
| `metricas-rendimiento` (`:329`) | Constantes |

**Consecuencia para este trabajo:** reorganizar la visual de esto sólo hace la mentira más linda. El
pedido dice «que muestre información de **todo lo que tenemos**» ⇒ hay que **construir el backend
real**. No es alcance agregado por gusto: sin eso el entregable no sirve.

### 0.2 🔴 Los filtros no filtran, y el día que haya datos reales eso fuga entre empresas

El front arma y manda `companyId`, `userId`, `farmIds`
(`dashboard.component.ts:288`, `currentFilters()`), pero **ninguna acción del controller declara esos
parámetros** y **`ICurrentUser` no está inyectado**. Hoy no fuga nada porque no consulta nada; el
primer `SELECT` real sin scope fuga datos de otra empresa. Es exactamente el anti-patrón que la
regla multi-tenant del repo prohíbe (§🏢 punto 3: empresa efectiva **por datos, fail-closed**).

### 0.3 🔴 Nadie ve el dashboard

```sql
SELECT r.name FROM role_menus rm JOIN roles r ON r.id=rm.role_id WHERE rm.menu_id=3;  -- 0 filas
SELECT c.name FROM company_menus cm ... WHERE cm.menu_id=3;                            -- 0 filas
```

El menú **Dashboard** (id 3, route `/dashboard`) no está asignado a **ningún rol ni ninguna empresa**.
Se llega sólo tecleando la URL. Sea cual sea el resultado de este trabajo, si no se siembra el menú
el usuario no lo ve.

### 0.4 Lo que SÍ está y se reusa (nada de esto se reinventa)

| Pieza | Dónde | Para qué la uso |
|---|---|---|
| `ICurrentUser` | `Application/Interfaces/ICurrentUser.cs` | `CompanyId`, `UserId`, `UserGuid`, `PaisId`, `Permissions` |
| `ILocationScopeResolver` + `UserLocationScopeCalculos` | `Application/Calculos/` | Cierre de visibilidad granja/núcleo/galpón/lote, **fail-closed** |
| `VacunacionScopeSqlParams` | `Infrastructure/Services/Vacunacion/` | Patrón de cómo bajar ese cierre a SQL (4 arrays) |
| `UserPermissionService` / `HasPermissionDirective` (55 usos) / `permissionGuard` | `core/auth/` | Gating en front |
| `session.menu: MenuItem[]` + `user.permisos` | `core/auth/auth.models.ts` | **Ya viajan en la sesión**: el gating no cuesta un request |
| `ActiveCompanyConfigService` (caché 5 min, fail-closed) | `core/services/company-config/` | Flags de empresa |
| `chart.js ^4` + `ng2-charts ^5` | `frontend/package.json` | Gráficas (ya son dependencia) |
| 15 `fn_*` de indicadores + `vw_indicadores_diarios_engorde` | BD | **Los datos reales** |

Y una carencia real que el pedido nombra bien: **`@defer` tiene 0 usos en todo el front**. Lo que el
dashboard llama hoy «lazy loading» (`dashboard.component.ts:392-420`) es una cola con `setTimeout`
que **igual carga todo**, más un `interval(30000)` que **repite las 8 llamadas cada 30 s**.

---

## 1. Enfoque arquitectónico

### 1.1 Cómo se decide qué ve cada quien — sin permisos nuevos

El usuario eligió **reusar lo que existe**. El modelo de acceso del repo ya tiene **dos** señales, y
las dos son por rol **y** por empresa:

- **`role_menus` ∩ `company_menus`** (lo que `fn_menu_usuario` resuelve y viaja en `session.menu`)
  = **a qué módulos accede esta persona en esta empresa**. Es la señal de *perfil*.
- **`role_permissions`** (45 keys, viaja en `session.user.permisos`) = **qué acciones puede ejecutar
  dentro de un módulo**. Es la señal de *nivel* (operador vs. administrador del módulo).

> **Por qué el menú y no sólo los permisos.** Hay 68 menús y 45 permisos: la mayoría de los módulos
> **no tiene permiso propio**. Gatear sólo por permisos dejaría los 4 paneles invisibles para casi
> todos — el costo que se advirtió al elegir esta opción. Usando las dos señales, un perfil que
> consulta (p. ej. `Consulta`, `Gerencia Granja`, `Director`) ve el panel de los módulos que tiene en
> el menú, y los bloques que exponen acciones sensibles piden además su permiso ya existente.

**Regla de oro (heredada del repo):** el mapeo panel→módulo se localiza **por `route`**, jamás por id
de menú — *«localizando menús por `route`, jamás por id fijo (ids difieren local↔prod)»* (CLAUDE.md
§🏢 punto 5).

Los tres perfiles del pedido **no se codifican como enum**: emergen del cruce.

| Perfil | Cómo se reconoce (sin nombrarlo en código) | Qué ve |
|---|---|---|
| **Técnico** | tiene menús de seguimiento diario (`/daily-log/*`) | Postura y/o Engorde, con foco en producción, mortalidad y consumo |
| **Administrativo** | tiene menús de inventario/gastos/reportes (`/gestion-inventario`, `/inventario-gastos`, `/reportes*`) | Alimento e inventario, descuadres, gastos |
| **Admin** | tiene los menús de `Configuración` **y** permisos de administración (`usuarios.gestionar`, `*.administrar`) | Todo lo anterior + el bloque de operación (usuarios activos, sesiones, salud de datos) |

Nadie declara «soy técnico»: si tenés el módulo, tenés el panel.

### 1.2 Los tres ejes de recorte del pedido («permisos, usuario y empresa»)

| Eje | Dónde se aplica | Garantía |
|---|---|---|
| **Empresa** | Backend: `ICurrentUser.CompanyId` en **cada** consulta. Nunca del header crudo (`ActiveCompanyMiddleware` ya lo validó) | Fail-closed: sin empresa resoluble ⇒ vacío, nunca «todo» |
| **Usuario** | Backend: `ILocationScopeResolver` → 4 arrays a SQL (patrón `VacunacionScopeSqlParams`) | Usuario sin restricción ⇒ arrays vacíos ⇒ comportamiento clásico. Con restricción y 0 grants ⇒ no ve nada |
| **Permisos/menú** | Front: función pura sobre `session.menu` + `session.user.permisos`. Back: el endpoint del panel exige lo mismo | El front oculta; **el back también corta** (ocultar no es proteger) |

Y un cuarto que el pedido implica: **flags de empresa** (`ActiveCompanyConfigService`) para que no se
dibuje lo que a esa empresa no le aplica (p. ej. el bloque de silos si `manejaInventarioPorSilo=false`).

### 1.3 Carga perezosa de verdad

- **Un endpoint por panel**, no un mega-endpoint. Así el panel que no se dibuja **no se pide**.
- Cada panel es un **componente standalone propio** ⇒ su propio chunk.
- En la página: `@defer (on viewport)` + `@placeholder` (esqueleto) + `@loading` + `@error`.
  Primer uso de `@defer` del repo.
- Se **elimina** el `interval(30000)` que repite todo cada 30 s. Refresco **manual**, por panel.

### 1.4 Dónde vive el cálculo

Regla del repo: **la BD filtra, el backend orquesta**. Nada de traer filas y agrupar en C# — eso es
lo que cuelga los endpoints multipaís. Cada panel se resuelve con una función SQL que ya existe o una
nueva `fn_dashboard_*`, y **toda `fn_*` nueva entra por migración en el mismo commit** (gate
`backend/scripts/verificar-sql-llega-por-migracion.js`, corta el CI).

La lógica pura (qué paneles corresponden, cómo se arma cada tarjeta) va a
`Application/Calculos/DashboardCalculos.cs` con xUnit, y en el front a `funciones/` con `.spec.ts`.

---

## 2. Estructura de archivos

### 2.1 Frontend — patrón canónico del repo (`movimientos-pollo-engorde`)

```
frontend/src/app/features/dashboard/
├── models/
│   ├── dashboard-panel.model.ts          # PanelId, DefinicionPanel, EstadoPanel
│   └── dashboard-metricas.model.ts       # DTOs espejo de los endpoints
├── funciones/
│   ├── README.md
│   ├── resolver-paneles-visibles.funcion.ts   # PURA: (menu, permisos, flags) → PanelId[]
│   ├── resolver-paneles-visibles.spec.ts
│   ├── construir-serie-tiempo.funcion.ts      # PURA: filas → ChartData<'line'>
│   ├── construir-serie-tiempo.spec.ts
│   ├── construir-distribucion.funcion.ts      # PURA: filas → ChartData<'doughnut'>
│   └── construir-distribucion.spec.ts
├── components/
│   ├── panel-postura/                     # 1 componente por panel (chunk propio)
│   ├── panel-engorde/
│   ├── panel-alimento-inventario/
│   ├── panel-cumplimiento/
│   ├── tarjeta-kpi/                       # primitivo compartido
│   └── panel-esqueleto/                   # @placeholder
├── pages/
│   └── dashboard-page/                    # orquestador DELGADO: filtros + @defer
└── services/
    └── dashboard-paneles.service.ts       # 1 método por panel
```

- `changeDetection: ChangeDetectionStrategy.Eager` **explícito** en todos (hay `subscribe` y estado
  mutable). El gate `verificar-change-detection.js` lo exige.
- Los tokens de color salen de `theme-italfoods.scss` — **prohibido** el `CORPORATE_COLORS` hardcodeado
  que hay hoy (`dashboard.component.ts:100-110`), que además usa amarillo/rojo/gris contra la regla de
  marca (naranja = acción, verde = éxito, rojo = peligro).
- `ToastService` / `ConfirmDialogService` para todo mensaje (hay un `showNotification` que fabrica un
  `div` a mano en `dashboard.component.ts:846` — se borra).

### 2.2 Backend — `partial class` por concern

```
backend/src/ZooSanMarino.Application/
├── Calculos/DashboardCalculos.cs                  # PURO + tests
├── DTOs/Dashboard/*.cs                            # DTOs por panel
└── Interfaces/IDashboardPanelService.cs

backend/src/ZooSanMarino.Infrastructure/Services/Dashboard/
├── DashboardPanelService.cs                       # ANCLA: usings, ctor, interfaz, helpers
└── Funciones/
    ├── DashboardPanelService.Postura.cs
    ├── DashboardPanelService.Engorde.cs
    ├── DashboardPanelService.Inventario.cs
    └── DashboardPanelService.Cumplimiento.cs

backend/sql/fn_dashboard_resumen_postura.sql       # espejo
backend/sql/fn_dashboard_resumen_engorde.sql
backend/sql/fn_dashboard_resumen_inventario.sql
backend/sql/fn_dashboard_resumen_cumplimiento.sql
backend/tests/ZooSanMarino.Application.Tests/DashboardCalculosTests.cs
```

Namespace **plano** (`ZooSanMarino.Infrastructure.Services`) en todos los partial; la interfaz sólo
en el ancla.

---

## 3. Los 4 paneles — qué muestra cada uno y de dónde sale

Todos: recortados por empresa (`ICurrentUser.CompanyId`) + alcance del usuario (4 arrays) + flags.

### 3.1 Postura (levante + producción)

**Se muestra si** el menú tiene `/daily-log/seguimiento` **o** `/daily-log/produccion`.

| Bloque | Fuente real |
|---|---|
| KPI: lotes activos, aves vivas (H/M), mortalidad acumulada % | `lotes` + `fn_seguimiento_diario_produccion` |
| Gráfica: % producción diario (últimos 30 d) vs. guía genética | `fn_indicadores_produccion_postura` |
| Gráfica: huevos por tipo | `seguimiento_diario_produccion` (+ `metadata->'huevoItems'` si `clasificacionHuevoPorItems`) |
| Gráfica: mortalidad diaria por granja | `fn_seguimiento_diario_produccion` |
| Tabla: lotes con seguimiento **sin validar** (si la empresa usa doble validación) | `seguimiento_diario_produccion.confirmado` |

Bloque de acciones (validar/desvalidar) sólo con `seguimiento_produccion.validar` / `.desvalidar`.
Si `ocultaMachosEnPostura` ⇒ no se dibuja la serie de machos (regla SR-DEF-1, ya respetada hoy).

### 3.2 Pollo engorde

**Se muestra si** el menú tiene `/daily-log/aves-engorde`.

| Bloque | Fuente real |
|---|---|
| KPI: lotes activos, aves, edad promedio | `lote_ave_engorde` |
| Gráfica: peso real vs. guía | `fn_indicadores_pollo_engorde` |
| Gráfica: conversión alimenticia y mortalidad diaria | `vw_indicadores_diarios_engorde` |
| KPI: ventas del período | `movimientos_pollo_engorde` |

⚠️ `fn_indicadores_pollo_engorde(p_lote_id …)` es **por lote**. Para el resumen por empresa hace falta
una `fn_dashboard_resumen_engorde(p_company_id, …)` que agregue **en la BD** (no un bucle en C#).

### 3.3 Alimento e inventario

**Se muestra si** el menú tiene `/gestion-inventario` **o** `/inventario-gastos`.

| Bloque | Fuente real |
|---|---|
| KPI: stock total por granja, ítems bajo mínimo | `fn_kardex_farm_inventory` |
| 🔴 **Descuadres de alimento** (galpones con `descuadre_kg` ≠ 0) | `fn_cuadre_alimento_engorde(p_company_id)` |
| Gráfica: consumo vs. ingreso (últimos 30 d) | `fn_acumulado_entradas_alimento` |
| KPI/tabla: gastos del mes por concepto | `fn_inventario_gastos_existencias` |

⚠️ **El descuadre se muestra separando las dos señales** (`descuadre_kg` = kilos que faltan/sobran;
`filas_negativas` = días que cerraron en rojo). Mezclarlas en un solo número es el error que CLAUDE.md
documenta (§🛡️ «El cuadre se mira, no se espera»: daba 23 galpones cuando los que tenían kilos eran 8).

### 3.4 Cumplimiento y pendientes

**Se muestra si** el menú tiene `/vacunacion/*`, `/cuadres-offline` o `/implementacion/*`.
Cada bloque aparece sólo si su módulo está en el menú.

| Bloque | Fuente real |
|---|---|
| Vacunación pendiente (próx. 7 d) y vencida | `fn_vacunacion_pendientes` (**ya trae el alcance granular**) |
| % cumplimiento de vacunación | `fn_vacunacion_cumplimiento_lote` |
| Cuadres offline sin resolver | `sync_operaciones` / bandeja `Sync/cuadres` |
| Firmas / tareas de implementación pendientes | tablas de `implementacion` (los paneles del home ya lo hacen) |

---

## 4. Reglas de negocio (vinculantes)

1. **Ningún dato inventado.** Se borran los `new Random()` y las constantes. Un panel sin fuente real
   **no se dibuja**; no se rellena con un placeholder que parezca un dato.
2. **Fail-closed en los 3 ejes.** Sin empresa resoluble ⇒ vacío. Con restricción de ubicación y 0
   grants ⇒ vacío. Sin menú/permiso ⇒ el panel no existe (ni se pide).
3. **Ocultar no es proteger.** Todo panel oculto en el front tiene su corte en el endpoint.
4. **La BD agrega, el backend orquesta.** Prohibido traer filas y agrupar en memoria.
5. **Una sola fórmula por número.** Si un KPI ya lo calcula una `fn_*` que un reporte usa, el
   dashboard llama **esa misma**; no se reimplementa la aritmética.
6. **Toda `fn_*` nueva entra por migración en el mismo commit** (el `.sql` es el espejo).
7. **Refactor ≠ cambio de comportamiento** en lo que ya andaba: los flags de empresa que el dashboard
   respeta hoy (`ocultaMachosEnPostura`) se conservan idénticos.

---

## 5. Casos de prueba

### 5.1 `DashboardCalculos` (xUnit) y `resolver-paneles-visibles` (Karma)

| # | Caso | Esperado |
|---|---|---|
| 1 | Usuario con `/daily-log/seguimiento` en el menú | Panel Postura visible |
| 2 | Usuario sin ningún `/daily-log/*` | Panel Postura **ausente** (no oculto: no está en la lista) |
| 3 | Usuario con `/gestion-inventario` y nada más | Sólo panel Alimento e inventario |
| 4 | Usuario con los 4 módulos | Los 4 paneles, en orden estable |
| 5 | Menú **vacío** (rol sin `role_menus`) | 0 paneles, sin excepción, sin panel «por las dudas» |
| 6 | Route con barra final / mayúsculas (`/Daily-Log/Seguimiento/`) | Matchea igual (normalización) |
| 7 | Menú con hijos anidados 3 niveles | Encuentra la route en cualquier nivel |
| 8 | Empresa con `manejaInventarioPorSilo = false` | Bloque de silos ausente del panel de inventario |
| 9 | `clasificacionHuevoPorItems = false` | Gráfica de huevos usa las 11 columnas fijas, no `metadata` |
| 10 | Usuario con menú de seguimiento pero **sin** `seguimiento_produccion.validar` | Ve el panel; **no** ve el bloque de acciones |
| 11 | Flags no resueltos (error del servicio) | Se asume `false` en todos (fail-closed), el panel base se dibuja |
| 12 | Serie de tiempo con días faltantes | La gráfica no inventa puntos; el hueco queda como hueco |

### 5.2 Backend por endpoint

| # | Caso | Esperado |
|---|---|---|
| 13 | Usuario de empresa A pide el panel | 0 filas de la empresa B (verificado con 2 empresas en la BD local) |
| 14 | Usuario con `restrict_locations = true` y grant de 1 galpón | Sólo ese galpón |
| 15 | Usuario con `restrict_locations = true` y 0 grants | Vacío (no «todo») |
| 16 | Usuario sin el menú del panel | 403, no un 200 con datos |
| 17 | Empresa sin datos del período | 200 con estructura vacía, no 500 |
| 18 | Descuadre de alimento | `descuadre_kg` y `filas_negativas` en **columnas separadas** |

### 5.3 Verificación en pantalla (smoke)

19. Los 4 paneles pintan datos reales y **coinciden con el reporte del módulo** (mismo lote, mismo día).
20. `@defer`: en la Network tab, el panel que no se scrollea **no dispara su request**.
21. Abrir y cerrar dos veces: sin spinner colgado (gate de change detection).
22. Login con un rol de sólo consulta ⇒ ve los paneles de sus módulos y **ningún botón de acción**.

---

## 6. Fases (cada una cierra con su verificación)

| Fase | Qué entrega | Verificación |
|---|---|---|
| **F1 · Cimientos** | `resolver-paneles-visibles` pura + spec; `DashboardCalculos` + xUnit; página nueva con `@defer` y esqueletos; `DashboardController` reescrito con `ICurrentUser` + scope; **se borran los `Random()`** | `yarn build`, `ng test`, `dotnet build`, `dotnet test`, gates |
| **F2 · Postura** | Endpoint + `fn_dashboard_resumen_postura` + migración + panel | Cruce contra Reporte Técnico Producción del mismo lote |
| **F3 · Engorde** | Ídem para engorde | Cruce contra Informe Semanal Pollo Engorde |
| **F4 · Alimento e inventario** | Ídem; descuadre con las 2 señales separadas | Cruce contra `verificar_cuadre_alimento_engorde.sql` |
| **F5 · Cumplimiento** | Ídem; reusa `fn_vacunacion_pendientes` tal cual | Cruce contra la bandeja de vacunación |
| **F6 · Que se vea** | Migración data-only: `role_menus` + `company_menus` del menú Dashboard, **por `route`**, `INSERT … WHERE NOT EXISTS` | Login con 2 roles distintos y ver el menú |

---

## 7. Riesgos y decisiones abiertas

- **`- [!]` A quién se le asigna el menú en F6.** Sembrarlo para todos los roles es una decisión de
  producto (hoy no lo ve nadie). Se propone: todos los roles que ya tengan al menos un módulo de los
  4 paneles. **Requiere OK explícito antes de la migración.**
- **`- [i]` El dashboard viejo queda vivo hasta que F1 lo reemplace.** No se borra a mitad de camino.
  Como nadie lo tiene en el menú, el riesgo de que alguien lea números inventados hoy es bajo, pero
  **existe** para quien teclee la URL.
- **`- [i]` `fn_indicadores_pollo_engorde` es por lote.** El resumen por empresa necesita función nueva;
  llamarla en bucle desde C# sería justo el anti-patrón que cuelga los endpoints multipaís.
- **`- [i]` Un backend local vivo en `:5002`** (PID 25236) y front en `:4200` al empezar esta sesión.
  Probablemente de otra ventana. **No se mata**: si hay que compilar, va `--artifacts-path` (CLAUDE.md §🔌.4).
