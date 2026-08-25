# Columna «Estado» de la grilla diaria de producción — celda faltante en el cuerpo

> Defecto **PREEXISTENTE** detectado al medir el ancho de las tablas en X18.4.1 (barrido de machos).
> No lo introdujo ese trabajo: en `f49012b^` la tabla ya estaba en `th=38 / td=37`.

## Síntoma

`frontend/src/app/features/lote-produccion/pages/tabs-principal/tabs-principal.component.html`,
tabla `.ux-table--seguimiento` («Registros Diarios»):

- El `<thead>` declara la columna **«Estado»** gateada por `@if (requiereValidacion)` (línea 265).
- La fila de datos del `<tbody>` **no tiene su `<td>`**: después de `observacionesPesaje` (línea 355)
  pasa directo a `<td class="sticky-actions text-right">` (Acciones).

Con la doble validación **encendida** el encabezado queda con una columna más que el cuerpo y la
fila se corre desde ahí: el header «Acciones» cae sobre la celda de Estado inexistente y la columna
sticky de acciones deja de coincidir con su encabezado. Con el flag **apagado** no se ve nada
(38 th / 37 td es el desbalance de fondo, pero ninguna de las dos columnas gateadas se renderiza).

## Enfoque

Copiar la celda que **levante ya tiene** y está balanceada
(`lote-levante/pages/tabs-principal/tabs-principal.component.html:408-415`), con el mismo gate
`@if (requiereValidacion)` y en la misma posición relativa: **después de observaciones, antes de
Acciones**. No se inventa nada:

- Los 4 helpers que usa la celda (`claseBadgeValidacion`, `tooltipValidacionFila`,
  `estadoValidacionFila`, `etiquetaValidacionFila`) **ya existen en el TS de producción**
  (`tabs-principal.component.ts:400-435`), byte a byte iguales a los de levante — se agregaron con
  la columna del `<thead>` y quedaron sin consumidor.
- Las clases `.badge-validacion*` son **globales** (`frontend/src/styles.scss:64-78`), no del SCSS
  del componente.
- `CommonModule` ya está en los `imports` del standalone (línea 20) ⇒ `ngClass` disponible; de
  hecho la fila ya lo usa para `claseFilaValidacion(s.id)` (línea 292).

## Archivos

| Archivo | Cambio |
|---|---|
| `lote-produccion/pages/tabs-principal/tabs-principal.component.html` | **+8 líneas**: `<td>` de Estado gateado por `requiereValidacion`, antes de `sticky-actions` |

Sin cambios de TS, SCSS, modelo, payload, backend ni BD.

## Reglas de negocio

- **Flag OFF ⇒ cero cambios visibles** (la celda no se renderiza, igual que hoy).
- **Flag ON ⇒** la fila muestra el badge de estado (`Validado` / `Pendiente` / `En retraso`) en su
  propia columna y el resto de la grilla vuelve a alinearse con el encabezado.
- No se toca la lógica de validación: la celda **lee** el mapa `estadoValidacionPorId` que el
  contenedor ya inyecta; no dispara acciones (el botón ✓ sigue en la celda de Acciones).

## Casos de prueba

1. `requiereValidacion = false` → `thead` y `tbody` conservan el conteo actual; la grilla se ve
   idéntica a antes del cambio (empresas sin doble validación: Demo, Sanmarino…).
2. `requiereValidacion = true` → conteo `th == td`; la columna «Estado» del encabezado cae sobre el
   badge, y «Acciones» sobre los botones.
3. Los 3 estados del badge se pintan con su color (`--validado` verde, `--pendiente` gris,
   `--retraso` rojo) y su tooltip.
4. Combinado con `ocultaMachosEnPostura` ON/OFF y `clasificacionHuevoPorItems` ON/OFF: el delta
   cabecera↔cuerpo se mantiene en 0 en las 4 combinaciones (las otras dos gateadas están duplicadas
   correctamente en ambos lados).

## Validación

- `cd frontend && yarn build` → 0 errores (único warning aceptado: el *bundle budget* preexistente).
- Conteo mecánico de `<th>` vs `<td>` de la tabla en los dos estados del flag (mismo método que
  X18.4.1): deben dar iguales.
