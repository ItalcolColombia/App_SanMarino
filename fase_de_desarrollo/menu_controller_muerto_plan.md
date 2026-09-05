# Plan — Borrar `MenuController` (código muerto que 500ea) + limpiar `IMenuService`/`MenuService`

**Origen:** hallazgo F.1 del bloque «Gate de Roles y Menús»
([`gate_roles_y_menus_plan.md`](gate_roles_y_menus_plan.md) §4, tracker sección F). Ese trabajo dejó
el hallazgo **reportado y sin tocar** a propósito; este plan lo resuelve.

---

## 1. El defecto

Los **6** endpoints de `backend/src/ZooSanMarino.API/Controllers/MenuController.cs` responden **500**
en runtime:

```
Unable to resolve service for type 'ZooSanMarino.Application.Interfaces.IMenuService'
while attempting to activate 'ZooSanMarino.API.Controllers.MenuController'
```

Medido el 5-sep-2026 con el backend local (`GET /api/Menu/tree` → 500 con ese cuerpo). El controller
pide `IMenuService` por constructor, la interfaz existe y `Infrastructure/Services/MenuService.cs` la
implementa — pero **`Program.cs` nunca la registra en el contenedor**. No es un olvido reciente: el
módulo se migró a `IRoleCompositeService` y el controller quedó colgado. La huella está escrita en el
propio código:

- `Infrastructure/Services/AuthService.cs:25` → `private readonly IRoleCompositeService _acl; // ← reemplaza a IMenuService`
- `Infrastructure/Services/AuthService.cs:544` → `// Menú desde el orquestador (antes venía de IMenuService)`

El 500 ocurre al **activar** el controller, o sea antes de que corra ningún action filter: ni la
policy ni el `[RolesGestionFilter]` llegan a ejecutarse.

## 2. El gemelo VIVO

`RoleController` expone los mismos 6 endpoints contra `IRoleCompositeService`, que **sí** está
registrado, y es al que apunta el front:

| Muerto (`MenuController`) | Vivo (`RoleController`) | Implementación |
|---|---|---|
| `GET /api/Menu/tree` | `GET /api/Roles/menus/tree` | `RoleCompositeService.Menus_GetTreeAsync` |
| `GET /api/Menu/me` | `GET /api/Roles/menus/me` | `Menus_GetForUserAsync` |
| `GET /api/Menu/user/{id}` | `GET /api/Roles/menus/user/{id}` | `Menus_GetForUserAsync` |
| `POST /api/Menu` | `POST /api/Roles/menus` | `Menus_CreateAsync` |
| `PUT /api/Menu/{id}` | `PUT /api/Roles/menus/{id}` | `Menus_UpdateAsync` |
| `DELETE /api/Menu/{id}` | `DELETE /api/Roles/menus/{id}` | `Menus_DeleteAsync` |

## 3. Auditoría de clientes (medida, no supuesta)

Ningún cliente llama `/api/Menu`. Verificado por grep sobre **todo** el repo, distinguiendo
`/api/Menu` de `/api/Roles/menus` (rutas distintas; sólo la segunda está viva):

- **Front (`frontend/src/`)** — `core/services/menu/menu.service.ts:24` declara
  `` private readonly base = `${environment.apiUrl}/Roles/menus` ``. Es el único servicio que hace el
  ABM del árbol global (lo consumen `role-management` y `company-management`). **Cero** ocurrencias de
  `api/Menu`, `/Menu/tree`, `/Menu/me` o `/Menu/user` en `.ts`/`.html`.
- **App móvil (`zootecnicoapp/`, Flutter)** — usa `GET /api/Auth/menu` (`lib/core/api/auth_api.dart:41`),
  que es de `AuthController` y resuelve por `IRoleCompositeService`. No toca `/api/Menu`.
- **Scripts / integraciones / SQL** — las únicas ocurrencias de la cadena `api/Menu` en el repo son
  **comentarios y documentación**: `Program.cs:568`, `CatalogoGlobalAutorizacionCalculos.cs:15`, la
  migración `20260815160000_...`, y `empresa_modales_y_catalogos_globales_plan.md`. Ninguna es una
  llamada.

## 4. Decisión: BORRAR, no registrar

⛔ **Registrar `IMenuService` para «arreglar el 500» sería un retroceso de seguridad**, no un arreglo:
abriría 6 endpoints hoy inalcanzables, entre ellos `GET /api/Menu/user/{userId}` — que devuelve el
menú de **otro** usuario y cuelga de la policy `CanManageUsers`, que sigue siendo
`RequireAuthenticatedUser()` (`Program.cs:559-560`), o sea token válido y nada más.

Se borra el controller. Efectos:
- Swagger deja de publicar 6 endpoints que sólo saben devolver 500.
- Los atributos `[RolesGestionFilter]` y `[CatalogoMenusLectura]` que `85eba2c` le puso a `GetTree`
  —para que naciera cerrado el día que alguien lo reviviera— se van con él. **Está bien:** los dos
  atributos siguen vivos y en uso en `RoleController` (clase y `MenusTree` respectivamente), que es
  el que sí atiende.

## 5. Archivos a borrar / tocar

| Archivo | Acción | Por qué |
|---|---|---|
| `API/Controllers/MenuController.cs` | **borrar** | Muerto: 500 en runtime, cero clientes. |
| `Application/Interfaces/IMenuService.cs` | **borrar** | Su único consumidor era `MenuController`. |
| `Infrastructure/Services/MenuService.cs` | **borrar** | Único implementador de la interfaz; nadie lo instancia ni lo registra. Su lógica vive hoy en `RoleCompositeService.Menus_*`. |
| `API/Program.cs` (comentarios) | editar | Dice «policies usadas por los controllers (Menu/Role/…)» y nombra `GET /api/Menu/tree` como la lectura que necesita la tabla de roles — la real es `GET /api/Roles/menus/tree`. |
| `Application/Calculos/CatalogoGlobalAutorizacionCalculos.cs` (doc-comment) | editar | Mismo `/api/Menu/tree` mal nombrado en el `<remarks>`. |
| `API/Controllers/RoleController.cs:230` (comentario) | editar | «el mismo que MenuController» queda apuntando a la nada. |

**No se tocan** ni `MenuItemDto`/`CreateMenuDto`/`UpdateMenuDto` (los usa `RoleController`), ni
`ICompanyMenuService`/`CompanyMenuService` (otra cosa: menús POR EMPRESA, registrado en
`Program.cs:224` y vivo), ni las migraciones ya aplicadas (son registro histórico de lo que era
cierto en su fecha).

## 6. Casos de prueba

1. `dotnet build` — 0 errores, sin advertencias nuevas. Prueba que nadie más referenciaba lo borrado.
2. `dotnet test` — verde. En particular `RolesAutorizacionCalculosTests` (el gate de `85eba2c`) no se
   toca.
3. Smoke con el backend levantado: la app **arranca** (si el DI o Swagger quedaran rotos, no lo hace),
   `GET /api/Menu/tree` pasa de **500** a **404**, y `GET /api/Roles/menus/tree` sigue publicado en
   Swagger y contestando. Backend apagado y puerto libre al terminar.
