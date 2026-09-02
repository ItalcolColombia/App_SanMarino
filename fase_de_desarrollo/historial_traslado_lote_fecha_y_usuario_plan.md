# Historial de traslados de lote: la Fecha y el Usuario siempre decian «—»

**Fecha:** 1-sep-2026
**Ruta afectada:** `/traslados-aves/dashboard` → solapa «📦 Lotes» del historial
**Tambien afectada:** `/traslados-aves/registros-traslados` (mismo DTO, mismo defecto)

---

## 1. Diagnostico (medido, no supuesto)

### 1.1 El mismatch de nombres

| Capa | Fecha | Usuario |
|---|---|---|
| Backend `HistorialTrasladoLoteDto` | *(no expone nada)* → solo `CreatedAt` | `CreatedByUserName` |
| Front `HistorialTrasladoLoteDto` (`traslados-aves.service.ts:411-419`) | `fechaTraslado: Date` | `usuarioNombre?: string` |

Ninguno de los dos nombres del front existe en el JSON ⇒ ambos llegan `undefined`.
`formatearFecha(undefined)` devuelve `'—'` por su guarda `if (!fecha)`, y `usuarioNombre || '—'`
cae al guion. **Las dos columnas estan muertas desde que se escribio la tabla.**

### 1.2 La fecha correcta NO es `createdAt`

La entidad ya tiene la columna buena: `HistorialTrasladoLote.FechaTraslado` (`DateOnly?`,
migracion `20260831170000_FechaTrasladoLote`), que es **el dia real en que el lote se movio**,
distinto de `created_at` —el instante en que alguien lo digito—. El write path ya la guarda
(`LoteService.Traslado.cs:175`) y el modal ya la pide. Lo unico que falta es **exponerla en el
DTO de lectura**. Renombrar el front a `createdAt` seria mostrar el dato equivocado.

### 1.3 El nombre de usuario es un literal con un TODO

`LoteService.Traslado.cs:236-238`:

```csharp
// TODO: Si se necesita el nombre del usuario, se podría crear una tabla de mapeo...
var nombreUsuario = $"Usuario ID: {h.CreatedByUserId}";
```

El TODO se basa en que `CreatedByUserId` es `int` y `User.Id` es `Guid`. **La relacion existe y
esta resuelta en otros dos services**: `CreatedByUserId` (int) == `users.cedula` (varchar).
Patron canonico: `LoteBaseEngordeService.ResolverCreadoresAsync` (linea 276) y
`HistoriaService.NombresPorCedulaAsync` (ItalJira, linea 73).

### 1.4 El tercer camino tira la fecha a la basura

`inventario-dashboard.component.ts:1256-1271` arma el `TrasladoLoteRequest` **sin** `fechaTraslado`,
aunque `ModalTrasladoLoteComponent` la emite (`modal-traslado-lote.component.ts:255`). Y la interfaz
`TrasladoLoteRequest` de `traslados-aves.service.ts:387` tampoco declara el campo — la de
`lote.service.ts:244` **si**. O sea: por el dashboard todo traslado queda fechado **hoy**; por
`lote-list` la fecha se respeta. Referencia de como se hace bien: `lote-list.onConfirmarTraslado`
(linea 1700).

---

## 2. Enfoque arquitectonico

- **La verdad la manda el backend.** Se agrega `FechaTraslado` al DTO de lectura y el front se
  alinea con los nombres reales del wire (`createdByUserName`, `createdAt`), en vez de inventar
  alias que nadie emite.
- **Logica pura → `Application/Calculos/`** con tests xUnit (gate de CI), como manda CLAUDE.md.
  Lo puro aca es el mapeo cedula↔int y la composicion del nombre; la consulta queda en el service.
- **Sin N+1 nuevo.** Los nombres de usuario se resuelven en **una sola** consulta por lote
  (`WHERE cedula IN (...)`), no una por fila. Los lookups de nucleo/galpon que ya estaban en el
  bucle **no se tocan**: refactorearlos seria cambiar codigo que no es el defecto.
- **La fecha pura no se corre de dia.** `DateOnly` viaja como `"2026-09-01"`; `new Date("2026-09-01")`
  la parsea como **medianoche UTC** y `toLocaleDateString` la pinta en local ⇒ en Colombia (UTC-5)
  mostraria **31/08**. Por eso se usa `fechaCortaSinTz` de `shared/utils/format.ts`, que ya existe
  para exactamente este problema, y **no** `formatearFecha` (que ademas imprime hora, que una fecha
  pura no tiene).
- **Fallback honesto para lo viejo:** `fecha_traslado` es nullable. La migracion hizo backfill, pero
  si alguna fila quedara en NULL se muestra `createdAt` en vez de `'—'`.

---

## 3. Archivos

### Backend

| Archivo | Cambio |
|---|---|
| `Application/DTOs/Lotes/HistorialTrasladoLoteDto.cs` | + `DateOnly? FechaTraslado`; `CreatedByUserName` pasa a `string?` |
| `Application/Calculos/HistorialTrasladoLoteCalculos.cs` | **NUEVO** — `NombresPorCedula(...)` y `ResolverNombre(...)`, puros |
| `Infrastructure/Services/Funciones/LoteService.Traslado.cs` | resolucion real del nombre (1 query batch) + pasa `FechaTraslado` |
| `tests/ZooSanMarino.Application.Tests/HistorialTrasladoLoteCalculosTests.cs` | **NUEVO** — xUnit |

**Sin migracion:** la columna, el indice y el backfill ya existen (`20260831170000`). Esto es
solo lectura.

### Frontend

| Archivo | Cambio |
|---|---|
| `traslados-aves/services/traslados-aves.service.ts` | interfaz `HistorialTrasladoLoteDto` alineada al wire; `TrasladoLoteRequest` + `fechaTraslado` |
| `traslados-aves/funciones/inventario-dashboard-formato.funcion.ts` | **NUEVA** funcion pura `fechaTrasladoHistorialLote(h)` |
| `traslados-aves/funciones/inventario-dashboard-formato.funcion.spec.ts` | **NUEVO** — spec de la funcion pura |
| `inventario-dashboard.component.ts` | delega en la funcion; `procesarTrasladoLote` acepta y reenvia `fechaTraslado` |
| `inventario-dashboard.component.html` | columnas Fecha y Usuario |
| `registros-traslados.component.ts` / `.html` | mismas 2 columnas (mismo DTO ⇒ obligatorio para que compile) |

---

## 4. Reglas de negocio

1. La columna **Fecha** muestra el **dia del traslado** (`fechaTraslado`), no cuando se digito.
   Si esta en NULL (fila anterior a la migracion que no alcanzo el backfill) → `createdAt`.
2. La columna **Usuario** muestra `firstName + surName` del usuario cuya `cedula` == `created_by_user_id`.
   Si ninguno matchea (p.ej. el id es un hash y no una cedula, caso ya visto en tickets) → `'—'`.
   El `createdByUserId` crudo sigue viajando en el DTO para diagnostico.
3. Un traslado hecho desde el dashboard **respeta la fecha elegida en el modal**; si no viene, hoy
   (mismo contrato que ya cumple `lote-list`).

---

## 5. Casos de prueba

### xUnit (`HistorialTrasladoLoteCalculosTests`)
- cedula numerica que matchea → nombre compuesto `"Nombre Apellido"`
- cedula no numerica (alfanumerica) → se ignora, no revienta
- id 0 / lista vacia → diccionario vacio, sin consulta
- nombre en blanco o solo espacios → no entra al mapa (⇒ `null`, no `" "`)
- id sin usuario → `ResolverNombre` devuelve `null`
- cedulas duplicadas → una sola entrada, sin excepcion

### Karma (`inventario-dashboard-formato.funcion.spec.ts`)
- `"2026-09-01"` → **01/09/2026**, no 31/08 (el bug de zona)
- `fechaTraslado` NULL → cae a `createdAt`
- ambos ausentes → `'—'`

### Manual (pantalla)
- Lote con traslados: las dos columnas dejan de decir «—»
- Traslado nuevo desde el dashboard con fecha de ayer ⇒ el historial dice **ayer**
- Traslado sin tocar la fecha ⇒ hoy (comportamiento previo intacto)

---

## 6. Fuera de alcance (se deja anotado, no se toca)

`getHistorialTrasladosLotesPorGranja` pega a `GET /Lote/historial-traslados/granja/{id}`, ruta que
**no existe en el backend** (el unico endpoint es `{loteId}/historial-traslados`) ⇒ la seccion de
lotes de `registros-traslados` siempre cae al `catch`. Es un defecto distinto —falta un endpoint,
no un rename— y crearlo excede lo pedido.
