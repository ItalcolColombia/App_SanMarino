# Plan — Unidad `qq` (quintal) en el alimento del seguimiento pollo engorde

## Objetivo
En el modal **Nuevo/Editar seguimiento (pollo engorde)** (`engorde-comun/modal-seguimiento-engorde`),
sección **📦 Alimento**, el selector de unidad hoy ofrece `kg` y `g`. Se pide:

1. **Agregar la unidad `qq` (quintal)** al selector, con su conversión a kilogramos.
2. **En Panamá, dejar `qq` como unidad por defecto** (primera/preseleccionada) de las filas de alimento.
3. **Mostrar debajo del campo** lo que se guardará en **consumo, siempre en kg** (preview vivo de la conversión).

## Enfoque arquitectónico (frontend-only, sin BD ni backend)
- El **factor de conversión ya existe en el código** como fuente de verdad:
  `ReporteIndicadorPanamaCalculos.KgPorQuintal = 45.36` (backend). **1 qq = 45.36 kg.** Se replica el
  mismo valor en el front para no divergir.
- El backend convierte consumo a kg con `MetadataEngordeCalculos.ToKg`, que **solo entiende `g`→/1000
  y asume kg para el resto**. Enviar `unidad:'qq'` haría que trate qq como kg (consumo errado). Por eso
  **el front normaliza `qq`→kg ANTES de enviar** (`construirItemsSeguimiento`): el DTO viaja en `kg`,
  el consumo se guarda **siempre en kg** y **no se toca el path validado de descuento de inventario**.
- Se respeta clean-code del módulo: la aritmética pura va en `funciones/inventario-calculos.funcion.ts`;
  el componente queda como orquestador delgado que delega.

## Archivos a modificar
1. `frontend/.../engorde-comun/funciones/inventario-calculos.funcion.ts`
   - `export const KG_POR_QUINTAL = 45.36;` (con comentario que referencia el constante backend).
   - Extender `toKg(cantidad, unidad)`: `qq`/`quintal`/`quintales` → `cantidad * KG_POR_QUINTAL`.
     (kg/g quedan idénticos → refactor no cambia comportamiento existente.)
2. `frontend/.../engorde-comun/funciones/mapear-seguimiento-dto.funcion.ts`
   - `construirItemsSeguimiento`: si la unidad de la fila es `qq`, convertir `cantidad` a kg con `toKg`
     y emitir `unidad:'kg'`. kg/g intactos.
3. `frontend/.../modal-seguimiento-engorde/modal-seguimiento-engorde.component.ts`
   - Helper `private unidadAlimentoPorDefecto(): 'qq'|'kg'` = `isPanama ? 'qq' : 'kg'`.
   - Usarlo como unidad inicial de las filas de alimento en `agregarItemHembras`/`agregarItemMachos`.
   - Método de preview `consumoKgDeFila(itemGroup): number` = `toKg(cantidad, unidad)` (devuelve número —
     sin alocar arrays/objetos por ciclo, sin riesgo NG0103).
4. `frontend/.../modal-seguimiento-engorde/modal-seguimiento-engorde.component.html`
   - Opción `qq (quintal)` en el `<select formControlName="unidad">` **solo cuando `isPanama`**
     (Colombia/Ecuador conservan kg/g exactos).
   - Hint bajo el campo Cantidad: **"Se guardará en consumo: X kg"** cuando `cantidad > 0` (siempre en kg);
     nota `(1 qq = 45.36 kg)` cuando la unidad es qq.
5. `frontend/.../engorde-comun/funciones/inventario-calculos.funcion.spec.ts` (nuevo) — tests puros.
6. Actualizar `funciones/README.md` (mención de `KG_POR_QUINTAL`/qq).

## Reglas de negocio
- `qq` disponible **solo en Panamá** (donde se usa); default `qq` en Panamá, `kg` en el resto.
- El **consumo persistido es siempre kg** (contrato backend intacto; sin migración).
- Conversión canónica **1 qq = 45.36 kg** (igual al backend).
- Validaciones existentes (`cantidadExcedeDisponible`, disponible en kg, máximo editar) siguen correctas
  porque usan `toKg`, que ahora entiende qq.

## Casos de prueba (puros)
- `toKg(1,'qq') = 45.36`; `toKg(2.5,'qq') = 113.4`; `toKg(0,'qq') = 0`.
- `toKg(x,'kg') = x`; `toKg(1000,'g') = 1` (sin cambios).
- `construirItemsSeguimiento([{catalogItemId:5,cantidad:2,unidad:'qq'}],…)` → item con `unidad:'kg'`,
  `cantidad = 90.72`.
- `construirItemsSeguimiento` con `unidad:'kg'`/`'g'` → cantidad/unidad idénticas al comportamiento previo.

## Validación
- `yarn build` (0 errores; único warning aceptado = bundle budget preexistente).
- `yarn test` sobre la spec pura (si el entorno lo permite).
