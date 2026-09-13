# Módulos de permisos por empresa — plan (12-sep-2026)

> Pedido: «identificar los permisos de postura y pollo engorde; que la empresa vea solo los permisos
> de los módulos que tiene; seleccionar desde la empresa a qué accede, porque al crear un rol aparecen
> todos sin control; tener un módulo de asignación de módulos».

## 0 · Decisiones del usuario (tomadas en sesión, 12-sep-2026)

| # | Decisión | Elegido |
|---|---|---|
| D1 | ¿Un permiso puede estar en varios módulos? | **Sí (M:N)**. `lote.corregir_aves` está en Postura **y** en Engorde. |
| D2 | ¿Qué pasa con la config actual de `company_permissions`? | **Medir y aplicar**: los módulos por empresa salen de sus menús reales; antes de aplicar se muestra quién pierde o gana. |
| D3 | ¿Prender un módulo prende también sus menús? | **No, solo permisos** (menús siguen por Empresas → Menús). Fase 2 si se pide. |
| D4 | ¿Se puede apagar un permiso suelto dentro de un módulo prendido? | **Sí: módulo + ajuste fino.** |
| D5 | Vacunación en Demo/Santa Reyes (ninguna empresa tiene sus menús) | **Se apaga** (pérdida nominal). |
| D6 | Siembra: pérdidas de engorde en postura + 8 ganancias de `seguimiento_*.validar` | **Aplicar pérdidas, bloquear ganancias** (regla S1). |
| D7 | Integración Panamá (Ecuador tiene el menú, Panamá no) | **Solo ItalcolPanama** (excepción a la semilla por menú, lookup por `companies.name` en la siembra). |

## 1 · Auditoría (medida contra `sanmarinoapplocal`, no asumida)

- `permissions` = **47 filas planas** (`key`, `description`), **sin noción de módulo** (+ `lote.corregir_fecha_encaset`, que la BD local todavía no tiene: faltan las migraciones `20260911*`).
- `company_permissions` **ya manda** en asignación (modal de rol), escritura (`RoleCompositeService` → 400) y runtime (`AuthService.PermisosEfectivosAsync`). El problema no es el gate: es que **se configura permiso por permiso** y nadie lo mantuvo.
- **Descontrol medido:** Santa Reyes y Demo tienen **solo menús de postura** y **16 / 14 permisos de engorde prendidos** (Santa Reyes incluso `sincronizacion_panama.*`). Sanmarino está limpio.
- **Por qué «aparecen todos» al crear un rol:** el wizard pide **Permisos (paso 2) antes que Empresas (paso 3)**; la lista sale de la empresa activa por defecto, que tiene casi todo prendido.
- **Overflow del tab Permisos** (captura del usuario): `.rm-fieldset__body--grid3` usa `repeat(3, 1fr)` y la descripción tiene `white-space: nowrap` ⇒ `1fr` no baja del min-content y la grilla se sale del modal.
- Uso real de las keys «dudosas» (grep de todo el repo, sin migraciones): `abrir_lote`, `liquidar_lote`, `confirmar_despacho` **solo** en pantallas de engorde (sin chequeo en backend); `cuadrar_ingresos_traslados_seguimiento` solo en `CuadreAlimentoEngordeController` y en el tab «Cuadre alimento» (de engorde). `lote.corregir_aves` en `lote-list` (postura) y `lote-engorde-list`. `guia_genetica.gestionar` en las 3 guías. `editar_registro`/`eliminar_registro` en engorde + Gestión de Inventario. `registros.fecha_retroactiva` en inventario, levante, producción y engorde.

## 2 · Clasificación (M:N)

| Módulo (`key`) | Permisos |
|---|---|
| **Postura** (`postura`) | `carga_masiva_postura`, `seguimiento_levante.validar/desvalidar`, `seguimiento_produccion.validar/desvalidar`, `lote.corregir_aves`\*, `lote.corregir_fecha_encaset`\*, `guia_genetica.gestionar`\*, `registros.fecha_retroactiva`\* |
| **Pollo Engorde** (`pollo_engorde`) | `lote_base_pollo_engorde.ver/crear/editar/eliminar`, `lote_reproductora_engorde.editar/eliminar`, `movimientos_pollo_engorde.corregir_ventas/descargar_excel/organizar_peso/validar_ventas/vender_lotes_cerrados`, `seguimiento_engorde.validar/desvalidar`, `seguimiento_reproductora_engorde.confirmar/eliminar`, `carga_masiva_pollo_engorde`, `confirmar_despacho`, `abrir_lote`, `liquidar_lote`, `liquidacion.aplicar_correccion`, `cuadrar_ingresos_traslados_seguimiento`, `lote.corregir_aves`\*, `lote.corregir_fecha_encaset`\*, `guia_genetica.gestionar`\*, `editar_registro`\*, `eliminar_registro`\*, `registros.fecha_retroactiva`\* |
| **Integración Panamá** (`integracion_panama`) | `sincronizacion_panama.ver/ejecutar` |
| **Gestión de Inventario** (`inventario`) | `editar_registro`\*, `eliminar_registro`\*, `registros.fecha_retroactiva`\* |
| **Vacunación** (`vacunacion`) | `vacunacion.cronograma.ver/administrar`, `vacunacion.plantillas.ver/administrar`, `vacunacion.registro.aplicar`, `vacunacion.reportes.ver` |
| **Tickets e ItalJira** (`tickets`) | `tickets.crear/gestionar/admin/indicadores` |
| **Administración** (`administracion`) | `usuarios.gestionar`, `usuarios.revocar_sesion`, `roles.gestionar`, `menus.gestionar` |

\* compartido entre módulos.

**Semilla de módulos por empresa** (D2) — por `menus.route` habilitado en `company_menus`, nunca por id:
postura ⇐ `/config/lote-management`, `/lote-reproductora`, `/daily-log/seguimiento`, `/daily-log/produccion`, `/daily-log/seguimiento-diario-lote-reproductora`, `/traslados-huevos/lista`, `/reportes-tecnicos`, `/reporte-tecnico-produccion`, `/reporte-tecnico-semanal`, `/reporte-diario-costos-postura`, `/config/guia-genetica`, `/config/guia-genetica-santa-reyes` ·
pollo_engorde ⇐ `/config/lote-engorde`, `/config/lote-reproductora-ave-engorde`, `/daily-log/aves-engorde`, `/daily-log/seguimiento-diario-lote-reproductora_pollo_engorde`, `/movimiento-pollo-engorde/lista`, `/indicador-ecuador`, `/informe-semanal-engorde`, `/reporte-diario-costos-engorde`, `/config/guia-genetica-ecuador` ·
integracion_panama ⇐ `/migraciones/sincronizacion-panama` · inventario ⇐ `/gestion-inventario%`, `/inventario-gastos` ·
vacunacion ⇐ `/vacunacion/%` · tickets ⇐ `/tickets%`, `/italjira/%`, `/gerencia/panel` · administracion ⇐ `/config/users`, `/config/role-management`.
Empresa **sin** `company_menus` (fail-open de `fn_menu_usuario`, regla D2 de esa fn) ⇒ **todos los módulos** (hoy recibe el catálogo completo).

## 3 · Arquitectura

**Principio: `company_permissions` sigue siendo la verdad materializada del runtime.** Los módulos son la
forma de **escribirla**; `AuthService.PermisosEfectivosAsync`, el gate de `RoleCompositeService` y
`fn_menu_usuario` **no se tocan** ⇒ el login y los 403 no cambian de mecanismo.

### 3.1 BD

Migración **schema** idempotente `AddModulosDePermisos` (SQL crudo `IF NOT EXISTS`, entidades en EF + ModelSnapshot):

```sql
permission_modules            (id serial PK, key varchar(60) UNIQUE NOT NULL, nombre varchar(120) NOT NULL,
                               descripcion text NULL, orden int NOT NULL DEFAULT 0, is_active bool NOT NULL DEFAULT true)
permission_module_permissions (module_id FK→permission_modules CASCADE, permission_id FK→permissions CASCADE,
                               PK (module_id, permission_id), INDEX (permission_id))
company_permission_modules    (company_id FK→companies CASCADE, module_id FK→permission_modules CASCADE,
                               is_enabled bool NOT NULL DEFAULT true, PK (company_id, module_id), INDEX (module_id))
```

Migración **data-only** `SeedModulosDePermisos` (Designer clonado, snapshot intacto, idempotente, lookups por `key`/`route`/`name`):
1. Los 7 módulos (`WHERE NOT EXISTS`).
2. Clasificación §2 (join por `permissions.key`; key ausente en el entorno ⇒ se saltea sola).
3. Módulos por empresa (semilla §2).
4. **Materializar `company_permissions` SIN que nadie gane nada** (regla S1, sólo en la siembra):
   - permiso clasificado en ≥1 módulo prendido de la empresa: fila existente ⇒ **se respeta** (apagado explícito = ajuste fino que ya existía); fila ausente ⇒ se inserta **prendido sólo si ningún rol de esa empresa lo tiene asignado**, si no **apagado** (evita resucitar huérfanos, p. ej. `seguimiento_*.validar`).
   - permiso clasificado pero en **ningún** módulo prendido ⇒ `is_enabled = false` (`IS DISTINCT FROM`).
   - permiso **sin clasificar** ⇒ intacto.
5. Menú `/config/permission-modules` («Módulos y permisos», bajo Configuración). `role_menus` heredado de la route `/config/companies` + rol `Admin`. **Sin `company_menus`** (igual que `/config/companies`: lo ve el super admin por la regla D5 de `fn_menu_usuario`).

Orden de timestamps: schema → seed, ambos **después** de `20260912140000_SeedFlagMultiplesSeguimientosSantaReyes`.

### 3.2 Backend

- **Domain:** `PermissionModule`, `PermissionModulePermission`, `CompanyPermissionModule` (+ navegaciones en `Permission` y `Company`).
- **Application/Calculos/`PermisoModuloCalculos`** (static, pura) — **el dueño de la regla**; la migración es su espejo y los tests el contrato:
  - `ResolverAlCambiarModulos(clasificacion, modulosAntes, modulosDespues, habilitadosAntes)` → habilitados después.
    Módulo que **se prende** ⇒ todos sus permisos ON. Módulo que **se apaga** ⇒ sus permisos OFF **salvo** que sigan cubiertos por otro módulo prendido (y se conserva su estado fino). Módulos sin cambio ⇒ ajuste fino intacto. Sin clasificar ⇒ intacto.
  - `ResolverNoPermitidosPorModulo(solicitados, clasificacion, modulosEmpresa)` → keys que se intentan **prender** fuera de todo módulo prendido (gate del ajuste fino, D4). Sin clasificar ⇒ permitido.
  - `ResolverSiembra(...)` → la regla S1 de §3.1.4 (para que el SQL tenga especificación ejecutable).
- **Infrastructure/Services/PermissionModule/** (patrón partial class):
  `PermissionModuleService.cs` (ancla, `: IPermissionModuleService`) + `Funciones/PermissionModuleService.Catalogo.cs` (CRUD módulos + clasificación) + `Funciones/PermissionModuleService.AsignacionEmpresa.cs` (módulos de la empresa → materializa `company_permissions` en **una transacción**).
- `CompanyPermissionService.SetPermissionsForCompanyAsync`: valida con `ResolverNoPermitidosPorModulo` ⇒ `PermisoFueraDeModuloException : InvalidOperationException` → **400** por el handler global (mismo patrón que `PermisoNoHabilitadoException`).
- `CompanyPermissionItemDto` + campo aditivo `Modulos: string[]` (keys) — contrato compatible.
- `CompanyService.CreateAsync`: además de `SembrarCatalogoCompletoSiVaciaAsync`, sembrar **todos los módulos** (preserva el comportamiento de hoy).
- **Endpoints** (todos en `PermissionModuleController`, para no tocar `CompanyController`):
  | Verbo | Ruta | Gate |
  |---|---|---|
  | GET | `api/PermissionModule` (módulos + keys) | lectura abierta (el modal de rol la usa) |
  | POST/PUT/DELETE | `api/PermissionModule[/{id}]` (DELETE ⇒ 409 si alguna empresa lo tiene prendido) | `AdminAplicacion` |
  | PUT | `api/PermissionModule/{id}/permissions` (clasificación; recalcula todas las empresas con módulos) | `AdminAplicacion` |
  | GET | `api/PermissionModule/company/{companyId}` (módulos con `isEnabled` + conteos) | lectura abierta (igual que `Company/{id}/permissions`) |
  | PUT | `api/PermissionModule/company/{companyId}` (`moduleIds[]`) | `AdminEmpresas` |
- **Sin `is_active` en módulos** (simplificación al implementar): un módulo se borra sólo si ninguna empresa lo tiene prendido.
- **Modal 🔑 de Empresas:** los módulos NO se prenden ahí (una sola puerta: la pantalla nueva); el modal agrupa y hace ajuste fino.

### 3.3 Frontend

- **Página nueva** `features/config/permission-modules/` (ruta `config/permission-modules` en `app.config.ts`), `changeDetection: Eager`, `ToastService`/`ConfirmDialogService`:
  - Tab **Catálogo**: lista de módulos (crear/editar/orden/activo) y, por módulo, checkboxes de permisos (M:N). Bloque «Sin clasificar».
  - Tab **Por empresa**: matriz **empresa × módulo** (toggle). Al apagar, confirma con el preview «se apagan N permisos, usados por M roles» (`funciones/resolver-cambio-modulos.funcion.ts`, espejo de la fn pura).
  - `models/permission-module.model.ts`, `funciones/agrupar-permisos-por-modulo.funcion.ts` (+ specs), `core/services/permission-module/permission-module.service.ts`.
- **Modal 🔑 de Empresas**: agrupado por módulo (encabezado con toggle del módulo + contador `n/m`); permisos de módulos apagados se ven deshabilitados con «Prendé el módulo»; ajuste fino dentro de los prendidos.
- **Modal de Rol**:
  1. Orden **General → Empresas → Permisos** (sin empresa elegida no hay nada que ofrecer).
  2. Permisos **agrupados por módulo** (los de las empresas del rol; compartidos una sola vez bajo su primer módulo con chip de los demás).
  3. Fix overflow: `grid-template-columns: repeat(3, minmax(0, 1fr))` + `min-width: 0` en `.rm-check-item`.
  `resolverPermisosAsignables` **no cambia**; se agrega `agruparPermisosPorModulo` sobre su salida.

## 4 · Reglas de negocio

- R-M1 **Una sola fórmula:** la regla vive en `PermisoModuloCalculos`; SQL de siembra = espejo; tests = contrato.
- R-M2 **Apagar un módulo no borra `role_permissions`** (queda huérfano, igual que hoy — R5 de `CompanyPermissionCalculos`).
- R-M3 **Compartido sobrevive** mientras cualquier módulo que lo contiene esté prendido.
- R-M4 **Ajuste fino sólo dentro de módulos prendidos** (400 fuera); sin clasificar se gestiona suelto.
- R-M5 **Siembra sin ganancias** (S1). Las pérdidas se aceptan sólo si se mostraron y aprobaron (D2).
- R-M6 **Localizar por `key`/`route`**, jamás por id (difieren local↔prod).
- R-M7 **Permiso nuevo = se siembra con su módulo** en la misma migración (si no, cae en «Sin clasificar» y no lo arrastra ningún módulo).

## 5 · Medición de impacto (D2)

Script de solo lectura `backend/sql/verificar_modulos_permisos_impacto.sql` (exento del gate de migración por prefijo `verificar_`): secciones `1_modulos`, `2_catalogo_se_apaga`, `3_catalogo_se_prende`, `4_usuarios_PIERDEN`, `5_usuarios_GANAN`.

**Local (12-sep):**
- Módulos: Sanmarino / Demo / Santa Reyes = administracion, inventario, postura, tickets · Ecuador = + pollo_engorde, integracion_panama · Panamá = + pollo_engorde.
- **Pérdidas (todas en permisos de pantallas que esas empresas no tienen):** Sanmarino `abrir_lote` 15, `cuadrar_ingresos_traslados_seguimiento` 15, `liquidar_lote` 11, `confirmar_despacho` 4 · Demo 17 keys de engorde/vacunación (1–3 usuarios c/u) · Santa Reyes 24 keys de engorde/Panamá/vacunación (2 usuarios c/u).
- **Ganancias con la regla ingenua:** `seguimiento_levante.validar` + `seguimiento_produccion.validar` a 8 usuarios (Sanmarino 3, Demo 3, Santa Reyes 2) ⇒ la regla S1 las **bloquea**.
- **Copia de producción del 12-sep (`sanmarino_medicion_0912`): resultado IDÉNTICO** (124 filas, mismas keys y conteos). Prod tampoco tiene aún `20260911*` ⇒ `lote.corregir_fecha_encaset` llega en el mismo deploy, antes de la siembra por timestamp.

**Prueba de la migración real (12-sep, BEGIN … ROLLBACK sobre la copia de prod, schema + seed extraídos de los `.cs`, dos pasadas):**
- Atrapó un **42804**: `NULL` sin tipo dentro de `SELECT DISTINCT` se infiere `text` contra `company_menus.parent_menu_id` ⇒ `NULL::integer`.
- Con el fix: 2ª pasada con conteos idénticos (7 módulos / 53 clasificaciones / 35 filas de empresa / 115 permisos prendidos de 190) ⇒ **idempotente**.
- `verificar_modulos_permisos_impacto.sql`: PIERDEN = exactamente la lista aprobada (D5/D6), **GANAN = 0**; Santa Reyes 0 permisos sólo-engorde; Integración Panamá sólo en ItalcolPanama.
- 53 = 55 filas del VALUES − 2 de `lote.corregir_fecha_encaset` (prod aún no la tiene; llega por `20260911110000` antes que la siembra).

## 6 · Casos de prueba

**xUnit `PermisoModuloCalculosTests`:** prender módulo ⇒ todos ON · apagar ⇒ OFF · compartido con otro módulo ON sigue ON · compartido apagado a mano sigue OFF al apagar el otro · módulo sin cambio conserva ajuste fino · sin clasificar intacto · gate: prender permiso fuera de módulo ⇒ rechazado; sin clasificar ⇒ permitido · siembra: fila existente respetada, ausente sin roles ⇒ ON, ausente con roles ⇒ OFF, fuera de módulos ⇒ OFF · keys case-insensitive · entradas nulas/vacías.
**Front (Karma):** `agrupar-permisos-por-modulo` (compartido, sin clasificar, orden) · `resolver-cambio-modulos` (preview).
**Smoke (backend aislado :5501 con content root propio contra `sanmarino_medicion_0912`):** migraciones aplican limpias · `verificar_*` antes/después = lo aprobado · `GET api/Company/{SantaReyes}/permissions` sin engorde habilitado · `PUT permission-modules` apagar/prender Engorde en Demo ⇒ conteos · `PUT permissions` prender `abrir_lote` en Santa Reyes ⇒ 400 · no admin ⇒ 403 en escrituras.
**Pantalla:** modal de rol en Santa Reyes (sólo postura/inventario/tickets/admin, sin desborde) y en Panamá (sin postura); abrir/cerrar dos veces (CD).

## 7 · Hallazgos fuera de alcance (anotados, no tocados)

- H1 Los 4 permisos de validación de postura no están habilitados en ninguna empresa y 6 roles los tienen: la doble validación en postura hoy no le funciona a nadie por permiso. La siembra no los resucita; decidir aparte.
- H2 El tab «Cuadre alimento» (engorde) se muestra en Gestión de Inventario a empresas de postura.
- H3 En local, el menú «Integración Panamá» está habilitado para **Ecuador** y no para Panamá (verificar en prod).
- H4 Fase 2 posible (D3): que el módulo agrupe también rutas de menú.

## 8 · Validación y cierre

`dotnet build` (0 err, sin warnings nuevos) + `dotnet test` · `yarn build` + `yarn test` · `node backend/scripts/verificar-sql-llega-por-migracion.js` · smoke + pantalla · apagar backend y liberar puerto · `DROP DATABASE sanmarino_medicion_0912` · commit **sin push ni deploy** (el deploy aplica la siembra en prod: requiere OK explícito).
