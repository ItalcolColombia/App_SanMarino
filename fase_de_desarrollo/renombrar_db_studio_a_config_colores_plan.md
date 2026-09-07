# Renombrar DB Studio → "Configuración de colores" (señuelo, solo lo visible)

> Pedido del usuario (7-sep-2026): que el módulo DB Studio no sea fácil de identificar en la
> aplicación. Se le pone una identidad-señuelo de "configuración de colores". Hallazgo de auditoría
> §2 (`respuesta_auditoria_ciberseguridad_2026-09.md`): el string `/api/DbStudio` está en el bundle JS.

## Enfoque arquitectónico

- **Alcance = "solo lo visible".** Se renombra todo lo que aparece en el bundle, en las respuestas
  de red, en las rutas y en la UI. **NO** se tocan: clases/servicios/interfaces del backend, la
  sección de config `DbStudio:` de appsettings, el `application_name` del pool, las tablas
  `dbstudio_audit` / `dbstudio_object_grant`, ni el permiso `db_studio.admin`. Son server-side,
  invisibles en la app.
- **Sin cambio de comportamiento.** Mismas guardas de autorización (doble validación), mismo gate de
  plataforma, mismo `[ApiExplorerSettings(IgnoreApi = true)]`. Solo cambian strings de ruta/label.
- **Identidad nueva:**
  | Qué | Antes | Después |
  |---|---|---|
  | Label del menú (`menus.label`) | `db_studio` | `Configuración de colores` |
  | Ruta SPA (`menus.route` + `app.config.ts`) | `/config/db-studio` | `/config/config-colores` |
  | `menus.key` | `db-studio` | `config-colores` |
  | `menus.icon` | `warehouse` | `palette` |
  | Ruta API (`[Route]` + `const API`) | `api/DbStudio` | `api/ConfigColores` |
  | Carpeta / módulo / chunk del front | `features/db-studio` · `DbStudioModule` | `features/config-colores` · `ConfigColoresModule` |
  | Título de ruta hija (`data.title`) | `DB Studio` | `Configuración de colores` |
  | Token en la lista de no-cacheables offline | `dbstudio` | `configcolores` |
- **`fn_menu_usuario` NO hardcodea la ruta** (solo la nombra en un comentario) → renombrar la fila de
  `menus` alcanza; la función arma el menú genéricamente desde `menus`/`role_menus`/`company_menus`.

## Archivos

### Backend — 1 archivo
- `Controllers/DbStudioController.cs`: `[Route("api/[controller]")]` → `[Route("api/ConfigColores")]`.
  La clase sigue siendo `DbStudioController` (fuera de alcance). Comentario del `<summary>` actualizado.

### Backend — migración de datos (nueva, data-only)
- `Migrations/<ts>_RenombraMenuDbStudioAConfigColores.cs` (+ `.Designer.cs` clonado, **sin tocar
  ModelSnapshot** — no hay cambio de esquema):
  ```sql
  -- Up: idempotente por route (los ids difieren local↔prod)
  UPDATE public.menus
     SET label = 'Configuración de colores',
         route = '/config/config-colores',
         key   = 'config-colores',
         icon  = 'palette',
         updated_at = NOW()
   WHERE route = '/config/db-studio';
  -- Down: reversa exacta WHERE route = '/config/config-colores'
  ```
  `role_menus` / `company_menus` no se tocan (referencian `menu_id`, la fila es la misma).

### Frontend — carpeta renombrada + refs
- `git mv features/db-studio features/config-colores` y dentro:
  - `config-colores.module.ts` (`ConfigColoresModule`), `config-colores-routing.module.ts`
    (`ConfigColoresRoutingModule`, `data.title` → `'Configuración de colores'`, quitar el `'DB Studio'`)
  - `data/config-colores.service.ts` (`ConfigColoresService`, `const API = '/api/ConfigColores'`)
  - `models/config-colores.models.ts`, `funciones/config-colores.funciones.ts`
  - `pages/config-colores-main/config-colores-main.component.{ts,html,scss}`
    (`ConfigColoresMainComponent`, `selector: 'app-config-colores-main'`, `changeDetection` explícito
    se conserva — hoy es `Eager`)
  - actualizar todos los imports intra-carpeta
- `app.config.ts` (~L556-561): `path: 'config-colores'`, `import('./features/config-colores/config-colores.module').then(m => m.ConfigColoresModule)`, comentario.
- `shared/offline/funciones/decidir-cacheable.funcion.ts` (~L147): `'dbstudio'` → `'configcolores'`
  (y el comentario ~L96). `decidir-cacheable.funcion.spec.ts` (L32-33): `/api/DbStudio/query` →
  `/api/ConfigColores/query`.

### Doc
- `respuesta_auditoria_ciberseguridad_2026-09.md` §2: nota de que la ruta se renombró a
  `/api/ConfigColores` como endurecimiento adicional (el string `DbStudio` ya no está en el bundle).

## Reglas de negocio
Ninguna cambia. Autorización, gate de plataforma, contrato de los endpoints (shapes, métodos),
ocultamiento de Swagger: idénticos. Solo cambian los strings de ruta y las etiquetas.

## Casos de prueba / validación
1. `cd backend && dotnet build` 0/0 · `dotnet test` verde (los `DbStudioMigrationCalculosTests` no se
   tocan y siguen pasando).
2. `dotnet ef migrations add RenombraMenuDbStudioAConfigColores` → `dotnet ef database update` local
   sin error; `SELECT label,route,key,icon FROM menus WHERE route='/config/config-colores'` devuelve
   los valores nuevos; re-correr `database update` (idempotencia: 0 filas afectadas la 2ª vez).
3. `cd frontend && yarn build` 0 errores · `yarn test` verde · `node scripts/verificar-change-detection.js`
   OK · `node scripts/verificar-lista-cacheable.js` OK (el gate de la lista cacheable).
4. `grep -rn "db-studio\|DbStudio\|db_studio\|DB Studio" frontend/src` → **0 resultados** (salvo, si
   quedara alguno, en tests de otras features ajenas).
5. Smoke navegador (back :5002 + front :4200, sesión del usuario):
   - el ítem de menú aparece como **"Configuración de colores"** con ícono de paleta, ruta
     `/config/config-colores`.
   - abrir el módulo → carga igual que antes (resumen de migraciones para no-privilegiados; consola
     completa para el correo autorizado + admin).
   - Network: las requests van a `/api/ConfigColores/...`; no queda ninguna a `/api/DbStudio`.
   - el chunk lazy en el build se llama `config-colores-module`, no `db-studio-module`.

## Residuales (fuera de alcance — "solo lo visible")
- Backend interno: `DbStudioController`, `IDbStudioService`, `DbStudioService`, `DbStudioOptions`,
  `DbStudioMigrationCalculos`, sección `DbStudio:` de appsettings, DI en `Program.cs`,
  `ApplicationName = "DbStudio"` (visible en `pg_stat_activity`).
- BD: tablas `dbstudio_audit` / `dbstudio_object_grant`, permiso `db_studio.admin` (visible solo en
  la pantalla admin de permisos).
- Registros históricos (ItalJira / bitácora / migraciones viejas) que nombran "DB Studio": **no se
  tocan** — son historia, reescribirlos sería falsear el registro.
