# La pantalla viva de granjas borra 2 campos que no muestra

**Fecha:** 1-sep-2026
**Pantalla:** `/config/farm-management` → pestaña «Granjas»
**Archivo del defecto:** `frontend/src/app/features/farm/components/farm-list/farm-list.component.ts`

---

## 1. El defecto

`save()` arma el payload del PUT campo por campo en `dtoBase` (~línea 666) y **omite dos campos**
que `UpdateFarmDto` declara opcionales con default `null`:

| Campo | Columna | Qué es |
|---|---|---|
| `codigoErpEngorde` | `farms.codigo_erp_engorde` | Correlativo ERP de engorde de **Panamá**. Los lotes nuevos lo capturan y avanza **+1** al cerrar todos los lotes del lote base en la granja (`LoteAveEngordeService`). |
| `manejaAlimentoPorGalpon` | `farms.maneja_alimento_por_galpon` | Override **por granja** del flag de empresa (patrón `farm ?? company`). `null` = hereda empresa · `true` = alimento sobre galpón · `false` = sobre granja. |

`FarmService.UpdateAsync` (`backend/.../Services/FarmService.cs:816-817`) los asigna **sin
condicional**:

```csharp
entity.ManejaAlimentoPorGalpon = dto.ManejaAlimentoPorGalpon;   // null = hereda empresa
entity.CodigoErpEngorde = NormalizeCodigoErpEngorde(dto.CodigoErpEngorde);
```

Como el front nunca los manda, el binder los deja en `null` y **cada edición de granja desde esa
pantalla los borra**: sin error, sin aviso, sin registro. Cambiar el nombre de una granja de Panamá
le borra el correlativo ERP; cualquier edición devuelve el override de alimento a «heredar la
empresa» y el inventario cambia de nivel en silencio.

## 2. Por qué el fix NO va en el backend

Para `codigoErpEngorde` se podría hacer «si viene `null`, conservar el actual». Para
`manejaAlimentoPorGalpon` **eso sería un bug nuevo**: `null` es un valor con significado propio
(«heredar de la empresa»), y con esa regla la granja quedaría sin forma de volver a heredar. Los dos
campos tienen que viajar **explícitos desde el front**. El backend queda intacto.

Es exactamente la defensa que este mismo archivo ya aplica a los códigos ERP avícolas (comentario
en `dtoBase`): *«Se envían siempre desde el form (hidratado con lo que devuelve el backend): así una
edición hecha con el flag apagado NO borra los códigos existentes.»* El bug es que esa defensa se
escribió para 6 campos y no se extendió a estos 2.

## 3. Enfoque

### 3.1 Extraer el armado del payload a `funciones/` (patrón canónico del repo)

Hoy el payload se arma inline dentro de `save()`, que además valida, decide create/update, hace HTTP
y maneja toasts. Se mueve **sin cambiar el resultado** a una función pura:

`frontend/src/app/features/farm/funciones/construir-payload-granja.funcion.ts`

```ts
export function construirPayloadGranja(raw: RawFormGranja): PayloadGranja
```

Sin `this`, sin DI, sin HTTP. `save()` queda como orquestador delgado. Esto es lo que hace testeable
la regresión: el test verifica que los 2 campos salen en el payload, que es literalmente el defecto.

### 3.2 Hidratar desde el backend al abrir la edición

`GET /api/Farm/{id}` ya devuelve los dos (`FarmDto` los declara y `FarmService.GetByIdAsync` los
proyecta), así que solo falta leerlos:

- `buildForm()`: agregar los controles `codigoErpEngorde` (validador `^\d*$`, `maxLength(18)`, igual
  que el form viejo y que `NormalizeCodigoErpEngorde` en el backend) y `manejaAlimentoPorGalpon`.
- `openModal(farm)` rama **edición**: `codigoErpEngorde: farmData.codigoErpEngorde ?? ''` y
  `manejaAlimentoPorGalpon: farmData.manejaAlimentoPorGalpon ?? null`.
- `openModal()` rama **creación**: `''` y `null` (mismos defaults que hoy manda el backend ⇒ crear
  una granja no cambia de comportamiento).

Los controles viven en el `FormGroup` **aunque el template no los pinte** — igual que los códigos
ERP avícolas cuando el flag está apagado. Así una empresa que no sea Panamá tampoco borra el
correlativo de una granja que lo tenga.

### 3.3 Exponerlos en el formulario

El form viejo (`farm-form.component`, ruteado pero sin acceso desde ningún menú) sí los tiene. Se
replican en la pantalla viva con el mismo criterio de país que ya usa el modal:

- **`codigoErpEngorde`** → dentro del bloque `@if (isPanama)`, junto a «Certificado GAB». Es un
  campo de Panamá y hoy no hay ninguna pantalla viva donde cargarlo.
- **`manejaAlimentoPorGalpon`** → select de 3 estados (Heredar de la empresa / Sobre galpón / Sobre
  granja), visible siempre: el override no es de un país, es de configuración de inventario. Que sea
  visible es parte del fix — un valor que la UI no muestra es un valor que la UI puede borrar sin que
  nadie lo note.

## 4. Archivos

| Archivo | Cambio |
|---|---|
| `frontend/src/app/features/farm/funciones/construir-payload-granja.funcion.ts` | **nuevo** — función pura que arma el payload |
| `frontend/src/app/features/farm/funciones/README.md` | **nuevo** — convención de la carpeta |
| `frontend/src/app/features/farm/components/farm-list/farm-list.component.ts` | 2 controles nuevos, hidratación en las 2 ramas de `openModal`, `save()` delega |
| `frontend/src/app/features/farm/components/farm-list/farm-list.component.html` | select de manejo de alimento + input de código ERP engorde |
| `frontend/src/tests/construir-payload-granja.funcion.spec.ts` | **nuevo** — test de regresión del payload |
| `backend/tests/ZooSanMarino.Application.Tests/UpdateFarmDtoBindingTests.cs` | **nuevo** — test del contrato de alambre (sólo test) |

**Backend de producción: sin cambios.** Lo único que se agrega del lado .NET es un test que
deserializa el JSON que ahora manda la pantalla y comprueba que los dos campos llegan al
`UpdateFarmDto` — el eslabón que el spec de Angular no puede ver.

## 5. Reglas de negocio

1. `manejaAlimentoPorGalpon` es **tri-estado**: `null` (hereda empresa) / `true` (galpón) /
   `false` (granja). `null` NO es «no informado», es una opción. El `<select>` usa `[ngValue]` para
   no degradar los tres estados a strings.
2. `codigoErpEngorde`: trim; vacío ⇒ `null`; solo dígitos, máx. 18 (el avance +1 al cerrar el ciclo
   exige un código numérico). El front valida con el mismo patrón con el que el backend rechaza.
3. **Nada más cambia de valor.** El resto de `dtoBase` se mueve carácter por carácter.

## 6. Casos de prueba

Unitarios (`construir-payload-granja.funcion.spec.ts`):

| # | Caso | Esperado |
|---|---|---|
| 1 | Granja de Panamá con `codigoErpEngorde: '4001017'` | el payload lo lleva igual — **es el defecto** |
| 2 | `manejaAlimentoPorGalpon: true` / `false` / `null` | los 3 llegan tal cual; `false` **no** se convierte en `null` |
| 3 | `codigoErpEngorde: '  4001017  '` | `'4001017'` (trim) |
| 4 | `codigoErpEngorde: ''` / `null` / `undefined` | `null` |
| 5 | `manejaAlimentoPorGalpon: undefined` | `null` (hereda), no `undefined` |
| 6 | Payload completo (resto de campos) | idéntico al que armaba `save()` inline: strings vacíos a `null`, numéricos a `Number`, `status` normalizado a `'A'`/`'I'` |

Manual (smoke):

1. Granja de Panamá con código ERP cargado y `maneja_alimento_por_galpon = true`.
2. Editar **solo el nombre** desde la pestaña «Granjas» y guardar.
3. `SELECT name, codigo_erp_engorde, maneja_alimento_por_galpon FROM farms WHERE id = <id>;`
   → los dos sobreviven. Antes del fix quedaban en `NULL`.
4. Cambiar el select a «Heredar de la empresa» y guardar → `maneja_alimento_por_galpon` = `NULL`
   (borrado **explícito**, que es lo que se pidió).

## 7. Validación

- `cd frontend && yarn build` (0 errores; único warning aceptado: bundle budget preexistente)
- `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless` (salida directo a archivo, sin
  pipe: por un pipe a `grep` la suite queda muda)
- `cd backend && dotnet build && dotnet test`
- Smoke manual de la sección 6 contra la BD local (exige backend levantado + sesión viva; ver la
  receta de smoke HTTP local)
