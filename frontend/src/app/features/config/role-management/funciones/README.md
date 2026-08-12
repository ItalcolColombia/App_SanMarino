# `funciones/` — role-management

Una función **pura** por archivo: recibe parámetros, devuelve resultado. Sin `this`, sin DI, sin
servicios/toasts/estado. El componente queda como orquestador delgado que junta estado, llama la
función y maneja HTTP/UI.

| Archivo | Qué resuelve |
|---|---|
| `filtrar-permisos-empresa.funcion.ts` | Qué permisos puede llevar un rol según las empresas a las que pertenece (`company_permissions`): fail-closed, intersección multi-empresa y detección de asignaciones huérfanas. |

## Espejo del backend — mantener en sincronía

`filtrar-permisos-empresa.funcion.ts` replica
`backend/src/ZooSanMarino.Application/Calculos/CompanyPermissionCalculos.cs` (método
`ResolverAsignables`). Las dos implementaciones tienen que dar el mismo resultado: la del front decide
lo que se **ofrece**, la del back decide lo que **vale** (el gate de runtime en `AuthService`
intersecta los permisos efectivos del usuario en el login). Si cambiás una regla, cambiá las dos y
actualizá `CompanyPermissionCalculosTests`.
