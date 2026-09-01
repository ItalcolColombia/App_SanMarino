# `farm/funciones/`

Funciones **puras** del módulo de granjas: reciben parámetros y devuelven un resultado. Sin `this`,
sin DI, sin `HttpClient`, sin toasts y sin estado. Los componentes y páginas quedan como
orquestadores delgados que juntan el estado, llaman la función y manejan HTTP/UI.

Una función por responsabilidad / por «botón», un archivo por función.

| Archivo | Qué resuelve |
|---|---|
| `construir-payload-granja.funcion.ts` | Arma el payload de crear/actualizar granja desde el `getRawValue()` del formulario. **Es el punto único donde se decide qué campos viajan al backend**, y por eso lleva su test de regresión en `frontend/src/tests/construir-payload-granja.funcion.spec.ts`. |

## Por qué el payload se arma acá y no dentro del componente

`FarmService.UpdateAsync` asigna los campos opcionales del `UpdateFarmDto` **sin condicional**
(`entity.X = dto.X`): lo que el front no manda llega como `null` y se borra. Cuando el payload se
armaba inline dentro de `save()`, agregar un campo al backend y olvidarlo en el front no rompía
nada visible — simplemente empezaba a borrar el dato en cada edición. Con la función aparte, ese
contrato tiene un solo lugar y un test que lo vigila.
