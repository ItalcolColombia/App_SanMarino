# 24 — Permisos por botón en `movimientos-pollo-engorde`

> **Objetivo:** que los 4 botones de acción del listado (Descargar Excel, Validar, Corregir,
> Organizar Peso) queden **gated por permiso**. Cada botón tiene un permiso en la tabla
> `permissions`, con **referencia al módulo + acción** en el `key`. Una **migración EF** crea los
> permisos (idempotente). La asignación a roles se hará desde la pantalla de administración de
> permisos (decisión del usuario: *solo crear permisos*).
>
> **Mecanismo (ya existente, no se modifica):** `role_permissions` → login (`AuthService` arma
> `Permisos` = keys de los roles del usuario) → `session.user.permisos` en storage →
> `UserPermissionService` → directiva estructural `*appHasPermission`.

---

## 1. Mapa botón → método → permiso

| Botón (UI) | Handler | Key del permiso | Descripción |
|---|---|---|---|
| Descargar Excel | `descargarExcel()` | `movimientos_pollo_engorde.descargar_excel` | Descargar Excel del listado de ventas |
| Validar | `auditarVentas(true, false)` | `movimientos_pollo_engorde.validar_ventas` | Validar coherencia de ventas vs disponibilidad (sin cambios) |
| Corregir | `solicitarAuditoriaFix()` | `movimientos_pollo_engorde.corregir_ventas` | Validar y corregir sobreventas en estado Pendiente |
| Organizar Peso | `solicitarOrganizarPeso()` | `movimientos_pollo_engorde.organizar_peso` | Organizar/recalcular peso prorrateado por lote |

**Convención del `key`:** `modulo.accion` (dotted), igual que la documentada en la entidad
`Permission` (`// Ej: "user.create"`) y que el seed de tickets (`tickets.crear`, …). El prefijo
`movimientos_pollo_engorde` referencia el módulo; el sufijo, la acción. Longitud < 100 (límite de
la columna).

> Nota: los permisos *por fila* del módulo (`editar_registro`, `confirmar_despacho`,
> `eliminar_registro`) ya existen y NO se tocan. Aquí solo se agregan los 4 de la barra superior.

---

## 2. Backend — migración EF (solo crea permisos)

Patrón copiado de `20260604211428_SeedTicketsPermissionsAndMenu.cs` (seed sin cambios de schema):

- **Nombre:** `SeedPermisosBotonesMovimientosPolloEngorde`.
- Generada con `dotnet ef migrations add` (empty) → se rellena `Up`/`Down` con `migrationBuilder.Sql`.
- **`Up`** — `INSERT INTO public.permissions (key, description) SELECT … WHERE NOT EXISTS …`
  (idempotente; soporta re-runs y no choca con el índice único de `key`).
- **`Down`** — limpia `role_permissions`/`menu_permissions` que referencien esos keys y luego
  borra los permisos (espejo del Down de tickets, por si alguien los asignó).
- **No** inserta en `role_permissions` (decisión: la asignación se hace por la UI de administración).
- **Verificar** que el `Up()` autogenerado salga VACÍO de operaciones de schema (es seed puro); si
  EF intenta meter ALTERs, abortar y revisar sincronía snapshot↔modelo antes de continuar.

### Tabla afectada
- `public.permissions (id PK, key UNIQUE, description)` — confirmado en `PermissionConfiguration`.

---

## 3. Frontend — gating de los 4 botones

En `movimientos-pollo-engorde-list.component.html`, envolver cada uno de los 4 `<button>` con
`<ng-container *appHasPermission="'<key>'">…</ng-container>` (mismo patrón ya usado en el archivo
para `editar_registro`/`confirmar_despacho`/`eliminar_registro`).

La directiva `HasPermissionDirective` ya está en `imports` del componente → no se toca el `.ts`.

Comportamiento: si el usuario no tiene el permiso, el botón **no se renderiza** (la directiva
limpia el view container). Reacciona a cambios de sesión sin recargar.

---

## 4. Validación

1. `dotnet build` del backend → la migración compila.
2. Inspección manual: `Up()` solo contiene el `INSERT` idempotente (sin ALTERs inesperados).
3. (Si hubiera BD local arriba) `dotnet ef database update` y verificar 4 filas nuevas en
   `permissions`. **Nota:** en este entorno no hay BD local / psql disponibles; el apply ocurre en
   el deploy (ECS corre migraciones al arrancar, `Database__RunMigrations=true`).
4. `yarn build` del frontend → el HTML con `*appHasPermission` compila.
5. No dejar procesos/servidores vivos tras validar.

---

## 5. Post-merge (acción del usuario)
- En la pantalla de **Roles y Permisos**, asignar los 4 keys nuevos a los roles que deban ver los
  botones. Tras re-login, `session.user.permisos` los incluye y los botones aparecen.
- Acciones potentes (Corregir / Organizar Peso): conviene asignarlas solo a roles de administración.

## 6. Fuera de alcance
- Gating de la acción "Corregir completados" interna del modal de auditoría (5.º flujo). Se puede
  añadir después si se quiere (key sugerido: `movimientos_pollo_engorde.corregir_completados`).
- Validación de permisos en el backend (los endpoints de auditoría/organizar-peso). Hoy el gating
  es solo de UI; si se requiere defensa en profundidad, es un paso aparte.
