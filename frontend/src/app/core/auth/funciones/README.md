# `core/auth/funciones/`

Funciones **puras** con las reglas de sesión y de empresa activa: reciben parámetros y
devuelven un resultado. Sin `this`, sin inyección de dependencias, sin `HttpClient`, sin
toasts, sin storage. Los servicios de `core/auth/` las orquestan.

Siguen la convención del repo (ver la sección *CLEAN CODE* de `CLAUDE.md`, módulo canónico
`features/movimientos-pollo-engorde`). Cada archivo lleva su `.spec.ts` al lado.

| Archivo | Qué decide |
|---|---|
| `politica-sesion.funcion.ts` | Si corresponde cerrar la sesión por tiempo (`evaluarFinDeSesion`) y si una navegación puede seguir con el token vencido (`evaluarAccesoOffline`) |
| `marcas-del-token.funcion.ts` | Qué dice el JWT guardado: si venció y cuándo fue el último contacto seguro con el servidor |
| `debe-cerrar-sesion-por-401.funcion.ts` | Si un 401 significa que la sesión terminó |
| `resolver-empresa-activa.funcion.ts` | Qué empresa/país/logo corresponden al nombre elegido |

## Por qué estas reglas viven acá y no adentro del servicio

Son las decisiones más delicadas de la app offline-first, y comparten una propiedad
incómoda: **el camino que expulsa al usuario es el mismo que borra su
almacenamiento local**. Un error acá no se ve como un bug, se ve como trabajo de campo que
desapareció. Aisladas y con tests, el borde exacto (el minuto 5, la hora 16, el 401 que sí
y el que no) es verificable sin levantar la app.

## Reglas al agregar una función nueva

- **Pura de verdad.** Si necesita la hora actual, se recibe como parámetro (`ahora: number`),
  no se llama a `Date.now()` adentro. Así el test fija el instante en vez de dormir.
- **Fail-closed.** Ante datos incompletos o ambiguos, devolver `null` / `false` / "no cambiar
  nada" antes que adivinar. `resolverEmpresaActiva` es el ejemplo: prefiere no cambiar de
  empresa a dejar el id y el nombre apuntando a empresas distintas.
- **Los mensajes al usuario son parte del contrato.** Si un mensaje existía antes, el test lo
  fija byte a byte: refactor ≠ cambio de comportamiento.
- **Tolerar el contrato real, no el ideal.** El backend devuelve `companyPaises` sin
  normalizar (camelCase o PascalCase) y el cuerpo de un error puede venir como string, objeto
  o vacío. Las funciones lo contemplan explícitamente en vez de asumir la forma feliz.
