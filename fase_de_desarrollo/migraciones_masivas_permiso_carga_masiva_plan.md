# Plan — Permisos de acceso a Migraciones Masivas (Postura / Pollo Engorde)

## Objetivo
Hoy cualquier usuario autenticado ve habilitados TODOS los tiles de "Migraciones Masivas"
(Postura y línea Engorde en el mismo mosaico). Se pide un control de acceso **por línea**:
- Sin el permiso `carga_masiva_pollo_engorde` → los tiles de Pollo Engorde (Lotes/Seguimiento/Venta)
  quedan deshabilitados (gris) mostrando "Sin permisos".
- Sin el permiso `carga_masiva_postura` → los tiles de Postura (Granjas/Núcleos/Galpones/
  Seguimientos/Ventas/Movimientos) quedan deshabilitados (gris) mostrando "Sin permiso para carga masiva".

Es un control **interino** (pedido explícito del usuario): hoy son 2 permisos planos asignables
por rol desde la pantalla ya existente de Roles y Permisos; el diseño no cierra la puerta a un
esquema más granular futuro (ej. un permiso por tipo).

## Enfoque arquitectónico
Reutilizar el sistema RBAC **ya existente** (no crear nada nuevo):
- Backend: entidades `Permission`/`RolePermission` ya existen; el JWT ya emite un claim
  `"permission"` por cada key asignada al rol del usuario (`AuthService.cs`).
- Frontend: `UserPermissionService.has(key)` (síncrono, lee de `TokenStorageService`) y la
  directiva `*appHasPermission` ya existen y se usan en producción (`movimientos-pollo-engorde`).

**Precedente directo a copiar**: permiso `movimientos_pollo_engorde.vender_lotes_cerrados`
(migración `20260714112951_AddPermisoVentaLotesCerradosMovimientoPolloEngorde`, commit `62ede31`):
- Seed **idempotente** del catálogo de permisos vía migración EF (`INSERT ... WHERE NOT EXISTS`),
  con su `Down()` simétrico limpiando `role_permissions`/`menu_permissions` antes del delete.
- **Enforcement 100% en el frontend** (gray + disabled + texto); el backend NO valida este
  permiso en el controller. Igual criterio acá: el `MigracionController` no cambia.
- La asignación a roles se hace después, a mano, desde la pantalla de administración
  "Roles y Permisos" — la migración NO asigna el permiso a ningún rol.

## Permisos a crear (catálogo `permissions`, key plana — mismo estilo que `editar_registro`,
`confirmar_despacho`, `manage_users`; sin prefijo de módulo porque el usuario ya dio el nombre
exacto para engorde):
- `carga_masiva_pollo_engorde` — "Migraciones Masivas: acceso a la carga masiva de Pollo Engorde
  (Lotes, Seguimiento y Venta)"
- `carga_masiva_postura` — "Migraciones Masivas: acceso a la carga masiva de Postura (Granjas,
  Núcleos, Galpones, Seguimientos, Ventas y Movimientos)"

## Archivos a crear/tocar

**Backend**
- `backend/src/ZooSanMarino.Infrastructure/Migrations/<timestamp>_AddPermisosCargaMasivaMigracionesMasivas.cs`
  — migración de solo datos (sin cambios de schema), idéntica en forma a la de
  `vender_lotes_cerrados`, sembrando los 2 permisos de arriba.

**Frontend** (`frontend/src/app/features/migraciones-masivas/`)
- `funciones/agrupar-tipo-migracion.funcion.ts` — función pura `esTipoPolloEngorde(codigo)` que
  distingue la línea Engorde (`LotesPolloEngorde`/`SeguimientoPolloEngorde`/`VentaPolloEngorde`)
  del resto (Postura). Evita repetir el array mágico en el componente.
- `components/selector-tipo-migracion/selector-tipo-migracion.component.ts`:
  - Inyectar `UserPermissionService`.
  - Método `puedeAccederTipo(t): boolean` según el grupo (usa la función pura de arriba).
  - Tile: `[disabled]="!t.disponible || !puedeAccederTipo(t)"`, clase `tile--locked` cuando
    corresponde, click no emite si está bloqueado (defensa además del `disabled` nativo).
  - Badge nuevo junto al de "Próximamente": "Sin permisos" (engorde) / "Sin permiso para carga
    masiva" (postura), solo cuando el tipo SÍ está disponible pero falta el permiso.
  - Estilos: variante gris (`opacity`, `cursor: not-allowed`, sin hover) reusando el patrón visual
    de `.tile--soon`.

No se toca `migracion.service.ts`, el `MigracionController`, ni el panel de carga — al no poder
seleccionarse un tile bloqueado, el panel de plantilla/carga (paso 3) nunca se muestra para un
tipo sin permiso.

## Casos de prueba / validación
- `dotnet build` (Infrastructure/Application — API puede estar bloqueada por el backend en vivo
  del usuario, no bloqueante, ya ocurrió en el precedente).
- `dotnet ef database update` local (desde `ZooSanMarino.Infrastructure`, sin `--startup-project`,
  para no chocar con el bin del backend en vivo) → confirmar los 2 permisos por psql.
- `yarn build` (frontend) 0 errores.
- Smoke visual en el navegador: con el usuario actual (probablemente ya tiene ambos permisos si
  su rol los tiene asignados manualmente tras el deploy, o ninguno si aún no se asignaron) —
  confirmar que los tiles sin permiso quedan gris/disabled con el texto correcto y los que sí
  tienen permiso funcionan igual que antes.

## Riesgo operativo a comunicar
Como la migración **no asigna** los permisos a ningún rol, tras aplicarse (deploy o
`database update` local) **todos los tiles de Migraciones Masivas quedan bloqueados para
todos los usuarios** hasta que un admin entre a "Roles y Permisos" y asigne
`carga_masiva_pollo_engorde` / `carga_masiva_postura` a los roles que deban seguir usando el
módulo. Hay que avisarlo explícitamente al cerrar la tarea.
