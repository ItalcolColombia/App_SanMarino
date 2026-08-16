# `funciones/` — role-management

Una función **pura** por archivo: recibe parámetros, devuelve resultado. Sin `this`, sin DI, sin
servicios/toasts/estado. El componente queda como orquestador delgado que junta estado, llama la
función y maneja HTTP/UI.

| Archivo | Qué resuelve |
|---|---|
| `filtrar-permisos-empresa.funcion.ts` | Qué permisos puede llevar un rol según las empresas a las que pertenece (`company_permissions`): fail-closed, intersección multi-empresa y detección de asignaciones huérfanas. |
| `catalogos-globales.funcion.ts` | Quién ve los tabs **Permisos** y **Menús**, que administran catálogos compartidos por todas las empresas: solo el rol `Admin` de la aplicación (comparación exacta, fail-closed). |

## Espejo del backend — mantener en sincronía

`filtrar-permisos-empresa.funcion.ts` replica
`backend/src/ZooSanMarino.Application/Calculos/CompanyPermissionCalculos.cs` (método
`ResolverAsignables`). Las dos implementaciones tienen que dar el mismo resultado: la del front decide
lo que se **ofrece**, la del back decide lo que **vale** (el gate de runtime en `AuthService`
intersecta los permisos efectivos del usuario en el login). Si cambiás una regla, cambiá las dos y
actualizá `CompanyPermissionCalculosTests`.

`catalogos-globales.funcion.ts` replica
`backend/src/ZooSanMarino.Application/Calculos/CatalogoGlobalAutorizacionCalculos.cs`. El front decide
qué se **muestra**; el back, con la policy `AdminAplicacion`, decide qué se **puede** — las
escrituras de `PermissionController`, `MenuController` y `RoleController/menus` devuelven 403 a
cualquiera que no sea el admin de la aplicación, aunque llame la API a mano. Tests:
`src/tests/catalogos-globales.funcion.spec.ts` y `CatalogoGlobalAutorizacionCalculosTests`.
