# funciones/ — company-management

Funciones puras del módulo: sin `this`, sin DI, sin efectos secundarios.

| Archivo | Responsabilidad |
|---|---|
| `logo.funcion.ts` | Validación y lectura de archivo de logo (512 KB, image/*) |
| `geo.funcion.ts` | Construcción de mapas geográficos y resolución código↔nombre |
| `roles.funcion.ts` | Filtrado de roles y permisos combinados |
| `paises.funcion.ts` | Diff de países asignados (add/remove) |

> Estas funciones son reutilizables multi-país (Colombia, Ecuador, Panamá) porque no asumen lógica de
> negocio específica por país — solo operan sobre los datos que reciben.
