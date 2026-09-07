# DB Studio — resumen seguro de migraciones

## Enfoque arquitectónico

- Conservar sin cambios el DB Studio completo **solo** para quien pase la **doble validación**:
  ser admin (rol `admin`/`administrador`, superadmin o permiso `db_studio.admin`) **y además**
  llevar el correo `moiesbbuga@gmail.com`. Falta cualquiera de los dos ⇒ vista restringida. La
  decisión de privilegio se toma en el backend.
- Para cualquier otra identidad autenticada —admins incluidos si no llevan ese correo—, exponer
  solamente dos endpoints de lectura: `GET access-mode` (bandera `fullAccess` para que el frontend
  elija la vista) y `GET migration-summary` (lista mínima: nombre de migración y si ya se ejecutó).
  El resto de endpoints de DB Studio queda denegado (403); el frontend nunca los intenta cargar.
- El contrato de Swagger tampoco debe anunciar la superficie restringida: el controlador entero
  se marca `[ApiExplorerSettings(IgnoreApi = true)]` y solo `MigrationSummary` la revierte
  (`IgnoreApi = false`). Es cosmético para el contrato, no un control de acceso: la protección
  real son las guardas del controlador.
- La lista se construye comparando las migraciones conocidas por EF Core con los IDs aplicados
  en `__EFMigrationsHistory`, sin ejecutar SQL arbitrario ni revelar metadatos de la base. El
  contrato es solo `{ migrationId, status }` (`aplicada` / `pendiente`): sin fecha —
  `__EFMigrationsHistory` estándar no la guarda y no se infiere del ID.

## Archivos previstos

- Backend: `DbStudioAuthorization` (+ `EnsureFullAccessAsync` / `EnsureMigrationSummaryAccessAsync`,
  se elimina `EnsureModuleAccessAsync`), `DbStudioController`, `DbStudioService`, DTOs e interfaces;
  lógica pura en `Application/Calculos/DbStudioMigrationCalculos.cs` con sus tests xUnit.
- Frontend: contrato del servicio, página de DB Studio y su plantilla para bifurcar la experiencia
  completa de la vista reducida segura.
- Seguimiento: este plan y el bloque propio en `tracker_estado.md`.

## Base de datos / SQL

No se crearán ni ejecutarán migraciones, DDL ni SQL operativo. La consulta de historial será
estrictamente de lectura y parametrizada/encapsulada en el servicio existente.

## Reglas de negocio

- Privilegiado = correo exactamente `moiesbbuga@gmail.com` (trim + comparación ordinal
  case-insensitive) **Y** admin (rol `admin`/`administrador` case-insensitive, superadmin o
  permiso `db_studio.admin`). Es `AND`, no `OR`: un admin sin ese correo, o el correo sin ser
  admin, caen a la vista restringida.
- Usuario no privilegiado: solo resumen, sin scripts, conexiones, objetos, SQL, acciones ni
  información de estructura.
- Estado `aplicada` si EF marca el ID como aplicado; de lo contrario `pendiente`.

## Casos de prueba

- Correo autorizado + admin (por rol / superadmin / permiso) ⇒ acceso completo.
- Admin sin el correo ⇒ solo resumen y 403 en los endpoints completos.
- Correo autorizado sin ser admin ⇒ solo resumen.
- Migración aplicada y pendiente se proyectan correctamente (solo nombre + estado).
