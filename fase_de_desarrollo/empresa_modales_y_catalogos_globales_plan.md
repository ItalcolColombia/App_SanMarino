# Empresas: modales que se desbordan · Roles: los catálogos globales quedan solo para Admin

**Fecha:** 15ago26 · **Pedido del usuario:** validar el módulo Empresa, organizar sus modales y
opciones (se desbordan), y ocultar los tabs **Permisos** y **Menús** del módulo de Roles para
todo el que no sea el administrador de la aplicación con perfil `Admin`.

---

## Caso 1 — El wizard de Empresa se desborda y sus opciones están amontonadas

### Diagnóstico (leído del código, no supuesto)

`company-management.component.html:434` — el contenedor del wizard es:

```html
<div class="bg-white rounded-2xl shadow-2xl w-full max-w-5xl p-6 relative">
```

**No tiene alto máximo ni scroll propio.** Está dentro de un overlay
`fixed inset-0 flex items-center justify-center`, así que cuando el contenido supera la altura de la
ventana el modal se centra igual y **se sale por arriba y por abajo**: el encabezado y —lo grave— el
footer con «Guardar Empresa» quedan fuera de pantalla y no hay forma de scrollear hasta ellos.

**Por qué apareció ahora.** El paso 2 creció: el 15ago26 (bloque V2 del tracker) se sumaron los **14
flags de comportamiento** en 4 grupos. Hoy el paso 2 apila, en la MISMA columna derecha
(`lg:col-span-6`): permisos resultantes → detalle del rol → permisos de módulos → 14 flags →
alimento previo al encaset. La columna izquierda solo tiene la lista de roles. El resultado es una
columna de ~1.300 px contra una de ~400 px: el modal es alto, angosto y desbalanceado.

Los otros tres modales del módulo (ver menú, asignar menú, permisos de empresa) **sí** tienen
`max-h-[85vh] flex flex-col` + cuerpo `overflow-auto`, pero están escritos con Tailwind suelto y
repetido, y sus overlays no tienen padding: en pantallas chicas el modal toca los bordes.

### Solución

1. **Primitivas `cm-modal*` en el SCSS del módulo**, calcadas de las `rm-modal*` que ya existen en
   Roles (`role-management.component.scss:782`): overlay con padding, caja `max-height: 92vh` en
   `flex-direction: column`, header y footer `flex-shrink: 0`, cuerpo `flex: 1; overflow-y: auto`.
   Es el patrón que el repo ya usa y ya está probado — no se inventa un primitivo nuevo.
2. **Los 4 modales del módulo pasan a esa estructura.** El wizard deja de crecer sin techo: el
   header con el stepper queda fijo arriba, el footer con Volver/Siguiente/Guardar queda fijo abajo
   y **solo el cuerpo scrollea**.
3. **Reorganizar las opciones del paso 2** (esto es la mitad del pedido, no solo el desborde):
   - Fila 1, dos columnas: **Roles de la empresa** | **Permisos** (resultantes + detalle del rol).
   - Fila 2, ancho completo: **Accesos y módulos** (permisos de módulos + acceso móvil).
   - Fila 3, ancho completo: **Comportamiento del sistema** — los 14 flags en grilla de hasta 3
     columnas, agrupados como ya están (Inventario · Postura · Engorde · Operación).
   - Fila 4, ancho completo: **Alimento previo al encaset**.

   Es la misma información y los mismos controles: cambia la distribución, no el formulario.
4. **Corregir el `finalize` que cierra el modal cuando falla el guardado**
   (`company-management.component.ts:509`). `finalize` corre también en el camino de error, así que
   hoy un fallo de red o una validación del backend **cierra el modal y borra todo lo cargado**,
   dejando solo un toast rojo. El cierre pasa al `next`; en el `error` el modal queda abierto.

### Fuera de alcance (queda anotado, no se toca)

`filteredRoles`, `selectedRolesPermissions` y `previewRolePermissions` son getters de template que
devuelven un array nuevo por ciclo de change detection — el patrón que CLAUDE.md prohíbe y que ya
tiene memoria propia (`ng0103-getters-arrays-nuevos.md`). No rompe nada visible hoy; memoizarlos es
un refactor aparte y se registra como caso abierto.

---

## Caso 2 — Los catálogos globales de Permisos y Menús están al alcance de cualquiera

### Diagnóstico

El módulo Roles tiene tres tabs de primer nivel (`role-management.component.html:35-61`): **Roles**,
**Permisos**, **Menús**. Los dos últimos NO administran el rol: administran los **catálogos globales
del sistema** — crear/editar/borrar keys de permiso y crear/editar/borrar/reordenar ítems del menú
de toda la aplicación. Hoy los ve **cualquiera** que tenga el módulo de Roles y Permisos, que es
mucha gente (administradores de empresa, líderes de implementación, soporte…).

Peor: **el backend tampoco lo impide.**

- `PermissionController` **no tiene un solo `[Authorize]`**: solo lo cubre la `FallbackPolicy`, que
  pide token válido y nada más. Cualquier sesión puede `POST`/`PUT`/`DELETE` sobre el catálogo de
  permisos.
- `MenuController` y los endpoints de menú de `RoleController` usan la policy `CanManageMenus`, pero
  en `Program.cs:482` esa policy está definida como `p.RequireAuthenticatedUser()` — con un `TODO`
  de seguridad escrito al lado. Es decir: hoy no filtra nada.

Ocultar el tab sin tocar el backend sería teatro: el endpoint seguiría abierto.

### Solución

**Front (fail-closed).**
- Función pura `funciones/catalogos-globales.funcion.ts`: `esAdminDeAplicacion(roles)` +
  `puedeVerTab(tab, esAdmin)`. `isAdminUser` arranca en `false` y solo se enciende si la sesión trae
  el rol; error o sesión ausente ⇒ `false` ⇒ tabs ocultos.
- Los tabs **Permisos** y **Menús**, sus botones de acción, sus cuerpos de tabla, sus filtros y sus
  dos modales CRUD quedan bajo `@if (esAdminApp)`.
- `irATab(tab)` reemplaza al `activeTab='…'` inline: si el tab no está permitido, no cambia. Un tab
  sensible no se puede activar ni por código ni por un estado viejo.

**Criterio de «perfil admin».** Se conserva el que el módulo ya usaba: nombre de rol exactamente
`admin` o `administrador` (case-insensitive). En la base local (refresh de prod) eso es **un solo
rol: `Admin` (id 1, 2 usuarios)**. No matchean `Admin Panama`, `Admin Demo`,
`Ecuador Administrador`, `Santa Reyes Administrador` ni `ADMINISTRADOR DE GRANJA` — que es
exactamente lo pedido: el administrador de la aplicación, no los administradores de empresa.

**Backend (lo que de verdad cierra la puerta).**
- Cálculo puro `Application/Calculos/CatalogoGlobalAutorizacionCalculos.cs` (`static`, sin EF) con la
  misma regla + tests xUnit.
- Policy nueva `AdminAplicacion` en `Program.cs`, resuelta con ese cálculo sobre los claims de rol.
- Se aplica **solo a las escrituras**: `POST`/`PUT`/`DELETE` de `PermissionController`,
  `MenuController` y los `menus/*` de `RoleController`.
- **Las lecturas quedan como están** (`GET /api/Permission`, `GET /api/Menu/tree`): un usuario no
  admin las necesita para asignar permisos a un rol y para que la columna «Menús» de la tabla de
  roles muestre etiquetas en vez de ids. Endurecerlas rompería el módulo para todos.

### Casos de prueba

| # | Caso | Esperado |
|---|---|---|
| 1 | Sesión con rol `Admin` | Ve los 3 tabs; crea/edita/borra permisos y menús |
| 2 | Sesión con rol `Admin Panama` | Ve **solo** Roles; sigue creando y editando roles normalmente |
| 3 | Sesión sin roles / `session$` falla | Ve solo Roles (fail-closed) |
| 4 | No admin ⇒ `POST /api/Permission` por API | **403** |
| 5 | No admin ⇒ `POST /api/Menu` por API | **403** |
| 6 | No admin ⇒ `GET /api/Permission` y `GET /api/Menu/tree` | **200** (no se rompe el módulo) |
| 7 | Rol con nombre `ADMIN` en mayúsculas | Cuenta como admin (comparación case-insensitive) |

---

## Caso 3 — Registro en ItalJira

Migración **data-only** `20260815160000_SeedTicketEmpresaModalesYCatalogosGlobales`, con el patrón
del repo: Designer clonado, ModelSnapshot intacto, identidad por email, idempotente
(`WHERE NOT EXISTS`), fail-open si el usuario no existe en el entorno.

- 1 **historia** en `LISTO`.
- 2 **casos en `CERRADO`** (lo pedido explícitamente), con `fecha_solucion`,
  `fecha_cierre_solicitante` y `cerrado_por_user_id`, sus tareas en `LISTO` y las horas imputadas.
- 1 **caso abierto** por el hallazgo que esta entrega NO resuelve (los getters que alocan por ciclo).
  Marcarlo cerrado sería mentir.

---

## Validación

- `cd backend && dotnet build` (0 errores) + `dotnet test` (incluye los tests nuevos del cálculo).
- `cd frontend && yarn build` (0 errores; único warning aceptado: bundle budget preexistente).
- Migración aplicada en la base local y corrida dos veces seguidas para probar idempotencia.
