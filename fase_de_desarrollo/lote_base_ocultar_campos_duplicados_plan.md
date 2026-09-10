# Lote Base de postura: ocultar los campos que ya se capturan en el Lote

## Decisión del usuario (9-sep-2026)

1. **El LOTE es el único punto de captura**; en el **Lote Base** se ocultan los campos duplicados.
2. **`fechaEncaset` y las cantidades del Lote Base son el dato PLANIFICADO/declarado, NO un
   duplicado** — el base declara lo que se espera, el Lote registra lo que pasó. **No se tocan.**

## Evidencia (medida el 9-sep-2026 en `sanmarinoapplocal:5433`, copia de prod)

Los dos formularios viven en el mismo componente (`lote-list.component`) y piden lo mismo sin ningún
vínculo entre sí: al elegir un lote base, `onBaseLoteChange` **sólo autogenera el nombre**.

Resultado: **los datos ya divergieron**.

| Dato | Coinciden | Difieren |
|---|---|---|
| `raza` (23 pares lote↔base) | 10 | **13** |
| `fecha_encaset` (23 pares) | 0 | **23** |

- 30 bases (sólo **14 con raza**), 25 lotes (23 con base, 2 sin base).
- `lotes.raza` alimenta guía genética, edad, indicadores y reportes.
  `lote_postura_base.raza` / `tipo_linea` **no los lee ningún otro servicio**: sólo
  `LotePosturaBaseService` los guarda y los devuelve a su propia pantalla. La copia del base no manda
  en nada y en más de la mitad de los casos ya dice otra cosa.
- La divergencia del **100 %** en `fecha_encaset` es coherente con la decisión 2: no es un error, son
  dos hechos distintos (planificado vs. real).

## Alcance

**Se ocultan del formulario de Lote Base exactamente 2 campos:**

| Campo | Por qué |
|---|---|
| `raza` | Duplicado real. En el Lote es un `<select>` validado contra la guía genética; en el base es un `<input>` de texto libre sin validar. |
| `tipoLinea` | Duplicado real, mismo caso. |

**NO se tocan** (y el plan lo dice explícito para que nadie los sume después):

| Campo | Por qué se queda |
|---|---|
| `fechaEncaset`, `cantidadHembras`, `cantidadMachos` | Dato **planificado** — decisión 2 del usuario. Es otro hecho, no un duplicado. |
| `farmId` | Es estructural: filtra qué bases se ofrecen al elegir granja en el Lote (`filterBaseLotesByGranja`). |
| `loteNombre` | Es el nombre del **base**, del que se deriva el del lote con sufijo A-F. No es duplicado. |
| `codigoErp`, `descripcionErp`, `erpCreate` | Sólo existen en el base. |

## 🔴 La regla que no se puede violar

> **Quitar un campo de la vista no puede borrar un dato.**

Hay **14 bases con `raza` cargada**. Si el payload de edición manda `null` porque el control ya no
está en pantalla, se pisa ese histórico en la primera edición de cada base.

**El precedente exacto ya existe en este archivo** (TK-2026-000024, `cantidadMixtas`): el control se
sacó del `baseForm` y del template, y en `saveBase()` el valor se toma **del registro que se está
editando**, no del formulario:

```ts
cantidadMixtas: Number(this.editingBase?.cantidadMixtas ?? 0) || 0,
```

Se copia ese patrón para `raza` y `tipoLinea` (`this.editingBase?.raza ?? null`). En un base **nuevo**
van `null`, que es correcto: 16 de 30 bases ya tienen `raza` nula.

## Cambios (frontend, `features/lote/`)

### 1. `funciones/payload-lote-base.funcion.ts` — NUEVO (función pura + spec)

`payloadLoteBaseConCamposPreservados(valoresDelForm, baseEnEdicion)` → el objeto del payload,
tomando de `baseEnEdicion` los tres campos que ya no están en pantalla (`cantidadMixtas`, `raza`,
`tipoLinea`) y del formulario todo lo demás.

> **Bonus que corrige una mentira del código:** `lote-list.component.ts:629` ya dice *«lo arma
> `payloadBaseConMixtasPreservadas`»* — **esa función no existe**, el comentario quedó apuntando al
> vacío. Al crear la función real, el comentario pasa a ser cierto; actualizarlo al nombre nuevo.

### 2. `components/lote-list/lote-list.component.ts`
- `initBaseForm()`: quitar los controles `raza` y `tipoLinea` (igual que se quitó `cantidadMixtas`).
- `openBaseModal`/`editBase` (~L1326-1331): quitar `raza`/`tipoLinea` del `patchValue`.
- `saveBase()` (~L1362-1377): delegar en la función pura nueva.
- Comentario en `initBaseForm` explicando por qué salieron, con el mismo tono que el de mixtas.

### 3. `components/lote-list/lote-list.component.html`
Quitar del modal de Lote Base los dos bloques (`🐔 Raza`, ~L599-600; `Tipo de línea`, ~L604-605).
**El modal de DETALLE del base sigue mostrando `raza`/`tipoLinea` si el registro los tiene** — mostrar
de sólo lectura un dato guardado es honesto; lo que se elimina es la *captura*, no la consulta.

## Lo que NO se toca

- **Backend: cero cambios.** `LotePosturaBaseDto`, la entidad y el service conservan `Raza`/`TipoLinea`
  — siguen viajando y guardándose. Sin migración, sin DDL.
- **El formulario de Lote**: intacto. Sigue siendo el dueño de raza y año.
- **Los datos existentes**: las 13 razas divergentes y las 14 razas del base **no se corrigen ni se
  borran** en este trabajo.

## Casos de prueba (`payload-lote-base.funcion.spec.ts`, Karma/Jasmine)

- **Editando** un base que tiene `raza='BABCOK BROWN'`, `tipoLinea='ROJA'`, `cantidadMixtas=5`: el
  payload devuelve esos tres valores **aunque el form no los traiga** (la regla anti-borrado).
- **Creando** (sin `baseEnEdicion`): los tres salen `null`/`0`, no `undefined`.
- Los campos que sí siguen en pantalla se toman **del formulario**, no del registro: editar
  `cantidadHembras` de 100 a 250 tiene que devolver 250.
- Normalización preservada byte a byte: `codigoErp: '  '` → `null`; `loteNombre: '  X  '` → `'X'`.

## Validación

- `cd frontend && yarn build` — 0 errores (único warning aceptado: el de bundle budget preexistente).
- `ng test` del spec nuevo, verde.
- Smoke manual: crear un base (sin raza en pantalla) y **editar uno de los 14 que ya tienen raza**,
  confirmando en BD que `raza` **no** quedó en `null`.

## Riesgo

Bajo. Es frontend puro, sin backend ni migración, y el único riesgo real —pisar las 14 razas
cargadas— está cubierto por la función pura y su test. Reversible quitando el commit.
