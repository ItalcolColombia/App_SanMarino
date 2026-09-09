# Validación del año de guía genética en la ESCRITURA

## Objetivo

Impedir que se cargue en la guía genética un `anio_guia` que ningún consumidor puede usar. Hoy la
columna acepta cualquier texto y el defecto sólo se manifiesta mucho después, en el formulario de
alta de lote, como *«No se encontraron años disponibles para la raza X»*.

**No se corrigen datos existentes en este trabajo.** Ver §Lo que NO se toca.

## Evidencia (medida el 9-sep-2026 en `sanmarinoapplocal:5433`, copia de prod)

El desajuste es de tipos: la guía guarda el año como **texto libre** y todo consumidor necesita un
**entero**.

| Objeto | Tipo | Nulable |
|---|---|---|
| `guia_genetica_sanmarino_colombia.anio_guia` (tabla ancha) | `text` | **SÍ** |
| `guia_genetica_santa_reyes.anio_guia` (tabla reducida) | `varchar` | NO |
| `lotes.ano_tabla_genetica` (el destino real) | **`integer`** | SÍ |

`GuiaGeneticaService.ObtenerAnosDisponiblesAsync` filtra con `int.TryParse` — **correctamente**,
porque protege una columna `integer`. El que está mal es el lado de la escritura, que deja entrar un
valor que ese filtro después descarta **en silencio**.

### Inventario completo de `anio_guia` (ambas tablas, todas las empresas)

| tabla | empresa | valor | estado | filas |
|---|---|---|---|---|
| ancha | 1 Sanmarino | 2021 / 2022 / 2023 / 2026 | usable | 213 / 144 / 144 / 245 |
| ancha | 1 Sanmarino | **`G21`** | **NO USABLE** | **143** |
| ancha | 3 Ecuador | 2021 | usable | 15 |
| ancha | 4 Demo | 2026 | usable | 224 |
| reducida | 6 Santa Reyes | 2026 | usable | 615 |

- **`G21` es el único valor no usable de toda la BD.** Cero `NULL`, cero vacíos.
- **Ninguna raza queda hoy sin salida**: `AP` y `C500` (las dos que tienen `G21`) también tienen años
  numéricos, así que el selector nunca llega al callejón. El defecto es **latente**, no activo.

### 🔴 `G21` NO es basura ni un duplicado de `2021`

Medido fila a fila para `AP`: las 72 edades existen en los dos años, con **el mismo `peso_h`** y
**distinto `cons_ac_h`**.

| edad | `peso_h` 2021 | `peso_h` G21 | `cons_ac_h` 2021 | `cons_ac_h` G21 |
|---|---|---|---|---|
| 1 | 145 | 145 | 171 | **154** |
| 25 | 3080 | 3080 | 11556 | **11823** |
| 50 | 3965 | 3965 | 29345 | **29932** |

Es una **guía real y distinta** (misma línea genética, otra curva de consumo) que la aplicación
**nunca pudo usar** desde que se cargó: ningún lote puede apuntarle porque `ano_tabla_genetica` es
`integer`. Se ve en el grid de administración de la guía y en ningún otro lado.

## Regla de negocio (cálculo puro)

`AnioGuiaGeneticaCalculos` en `backend/src/ZooSanMarino.Application/Calculos/`:

```
EsAnioUsable(string? anio) -> bool
```

`true` sólo si, tras `Trim()`, el texto parsea a `int` **y** cae en **1900–2100**.

El rango no se inventa: es exactamente el que ya declara el `<input type="number" min="1900"
max="2100">` del formulario de lote cuando la empresa no tiene guía cargada
(`lote-list.component.html`). Se adopta ese, no otro, para que la regla sea **una sola** en el repo.

Acompaña un `MensajeAnioInvalido(string? anio)` que arma el texto de rechazo, para que los tres
escritores digan lo mismo palabra por palabra.

## Cambios (backend)

### 1. `Application/Calculos/AnioGuiaGeneticaCalculos.cs` — NUEVO
La regla pura, sin EF ni estado. Con doc-comment que explique **por qué** el rango y que el filtro
`int.TryParse` del lado de lectura es el que se está honrando.

### 2. `Infrastructure/Services/ProduccionAvicolaRawService.cs`
`CreateAsync` (~L35) y `UpdateAsync` (~L120): validar antes de escribir y lanzar
`ArgumentException` con `MensajeAnioInvalido`. El controller (`ProduccionAvicolaRawController`) ya
traduce `ArgumentException` a `400` — verificarlo y, si no lo hace, agregar el `catch` siguiendo el
patrón de `GuiaGeneticaSantaReyesController`.

⚠️ `CodigoGuiaGenetica = Raza + AnioGuia + Edad` (~L402): no se toca. Para un año válido el código
sale idéntico al de hoy ⇒ delta cero.

### 3. `Infrastructure/Services/ExcelImportService.cs`
**Rechazo POR FILA, nunca abortando el archivo.** Ya existe el idioma exacto (~L97):
`errors.Add($"Fila {row}: …")`. Una fila con año inválido se salta con su mensaje y el resto del
archivo se importa. Abortar el archivo entero sería un cambio de comportamiento mucho más duro que
el defecto que se está arreglando.

### 4. `Infrastructure/Services/GuiaGeneticaSantaReyes/GuiaGeneticaSantaReyesService.cs`
Misma regla en `CreateAsync`/`UpdateAsync` y en `ImportarExcelAsync` (que ya tiene su lista
`Errores`). Va aunque hoy esa tabla no tenga valores malos: si la regla vive en un solo módulo, el
otro vuelve a abrir la puerta.

## Lo que NO se toca (explícito)

- **Las 143 filas `G21`.** Son dato real del cliente, no un error de tipeo. Borrarlas o renombrarlas
  a `2021` sin que el cliente lo confirme sería inventar o destruir una guía. Queda como decisión
  suya, con la evidencia de arriba.
- **Ninguna migración de BD.** Sin DDL, sin `CHECK`, sin backfill. La validación es de aplicación.
  (Un `CHECK` en la columna es la evolución natural, pero exige decidir antes qué pasa con `G21`.)
- **El lado de LECTURA.** `ObtenerAnosDisponiblesAsync`, `ObtenerAniosCrudoAsync` y el
  `int.TryParse` se quedan como están: ya hacen lo correcto.
- **El selector de razas.** Se evaluó filtrarlo por «tiene año usable» y se **descartó**: haría
  desaparecer una raza sin explicación, que es peor que el aviso amarillo que ya se muestra desde
  `0a690ad`.

## Casos de prueba (xUnit, `tests/ZooSanMarino.Application.Tests/AnioGuiaGeneticaCalculosTests.cs`)

- Usables: `"2026"`, `"1900"`, `"2100"`, `" 2021 "` (con espacios).
- No usables: `"G21"` (el caso real), `null`, `""`, `"   "`, `"2026 AP"`, `"2.026"`, `"2026.0"`,
  `"20,26"`, `"1899"`, `"2101"`, `"-2021"`.
- **Invariante de delta cero:** los 4 valores que hoy existen en la BD (`2021`, `2022`, `2023`,
  `2026`) tienen que dar `true`, en las dos tablas y las 5 empresas. Ningún dato vigente puede
  quedar del lado inválido.

## Validación

- `dotnet build` 0 errores, sin advertencias nuevas.
- `dotnet test` verde, incluidos los tests nuevos.
- Smoke: `POST`/`PUT` a `/api/ProduccionAvicolaRaw` con `anioGuia="G21"` ⇒ **400** con el mensaje;
  con `anioGuia="2026"` ⇒ **201/200** igual que hoy.
- Sin `yarn build`: no hay cambios de frontend en este trabajo.

## Riesgo

Bajo y acotado a la escritura. El único cambio de comportamiento observable es que una carga que
antes se aceptaba en silencio y quedaba inservible, ahora se rechaza diciendo por qué. Ningún dato
existente cambia y ninguna lectura cambia de resultado.
