# DB Studio — resumen seguro de migraciones

## Enfoque arquitectónico

- Conservar sin cambios el DB Studio completo para el rol `admin` y para
  `moiesbbuga@gmail.com`, con la decisión de privilegio tomada en el backend.
- Para cualquier otra identidad autenticada, exponer solamente dos endpoints de lectura:
  `GET access-mode` (bandera `fullAccess` para que el frontend elija la vista) y
  `GET migration-summary` (lista mínima con migración, fecha veraz o `null`, y estado). El
  resto de endpoints de DB Studio quedará denegado para ese perfil (403); el frontend nunca
  intentará cargarlos.
- El contrato de Swagger tampoco debe anunciar la superficie restringida: el controlador entero
  se marca `[ApiExplorerSettings(IgnoreApi = true)]` y solo `MigrationSummary` la revierte
  (`IgnoreApi = false`). Es cosmético para el contrato, no un control de acceso: la protección
  real son las guardas del controlador.
- La lista se construirá comparando las migraciones conocidas por EF Core con los IDs aplicados
  en `__EFMigrationsHistory`, sin ejecutar SQL arbitrario ni revelar metadatos de la base.
- `__EFMigrationsHistory` estándar solo tiene `MigrationId` y `ProductVersion`; no contiene la
  hora de aplicación. No se inferirá desde el ID de migración: la fecha se expondrá como `null`
  y la UI mostrará `No disponible` hasta que exista una columna de fecha real, sin migración ni
  cambio de datos para este alcance.

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

- Privilegiado = rol `admin` (case-insensitive) o email exactamente
  `moiesbbuga@gmail.com` (comparación ordinal case-insensitive para no depender de casing del claim).
- Usuario no privilegiado: solo resumen, sin scripts, conexiones, objetos, SQL, acciones ni
  información de estructura.
- Estado `aplicada` si EF marca el ID como aplicado; de lo contrario `pendiente`.

## Casos de prueba

- Admin y correo excepcional conservan acceso completo.
- Usuario normal recibe únicamente el resumen seguro y es rechazado en los endpoints completos.
- Migración aplicada, pendiente y fecha ausente se proyectan correctamente.
