# Plan — Recepción de tránsito con distribución en varios galpones (Gestión de Inventario)

**Fecha:** 2026-07-26
**Módulo:** Gestión de Inventario → pestaña **Tránsito** (recepción inter-granja)
**Tracker:** [`tracker_estado.md`](../tracker_estado.md)

---

## 1. Problema / requerimiento

Hoy, al **recibir en destino** un traslado inter-granja de **alimento**, el receptor debe elegir **un solo**
núcleo + galpón y toda la cantidad entra ahí. En la operación real llegan (p. ej.) **1.000 kg** a la granja
y esos kg se reparten entre **varios galpones**.

**Requerido:** poder recibir la cantidad **una sola vez** pero **distribuirla entre N galpones** de la granja destino.

**Fuera de alcance (explícito del usuario):** ítems **no alimento** (y alimento en granjas que manejan
inventario **a nivel granja**, p. ej. Colombia) se reciben **a nivel granja** → **no aplica** distribución.
Ese camino queda **byte a byte idéntico** al actual.

---

## 2. Enfoque arquitectónico

- **Una sola recepción, N asientos.** El tránsito se cierra igual (un `TransferGroupId`), pero se generan
  **N movimientos `TrasladoInterGranjaEntrada`** (uno por galpón) y **N filas de stock**. Esto conserva la
  trazabilidad por ubicación en Histórico/Ingresos y hace que el stock quede correcto por galpón.
- **Aditivo en el contrato:** `Distribucion` es un campo **opcional** del request. Si no viene (o viene vacío),
  el flujo actual (un solo galpón / nivel granja) se ejecuta **sin cambios**, con los **mismos mensajes de error**.
- **Lógica pura en `Application/Calculos/`:** la resolución "¿a qué ubicaciones y con qué cantidades entra
  esto?" no toca EF → `InventarioGestionRecepcionDistribucionCalculos` (static) + tests xUnit.
  El service solo resuelve el flag de ubicación, delega y persiste.
- **Gate de ubicación reusado:** se mantiene `usaUbicacion = IsAlimento(item) && !EsInventarioNivelGranjaAsync(toFarmId)`
  (flag `farm.ManejaAlimentoPorGalpon ?? company.ManejaAlimentoPorGalpon`). La distribución **solo** existe
  cuando `usaUbicacion == true`.

### Efecto colateral detectado (bug latente que hay que arreglar sí o sí)

`InventarioGestionService.GetTrasladosAsync` construye el diccionario de entradas con
`.ToDictionaryAsync(x => x.TransferGroupId!.Value)`. Con **N entradas por grupo** eso lanza
`ArgumentException: An item with the same key has already been added` → **rompería la pestaña Traslados**.
Se corrige agrupando y tomando la entrada de menor `Id` (la lista de Traslados muestra el destino *sugerido*
guardado en la salida, así que la vista no cambia).

Resto de consumidores verificados y **compatibles** con N entradas:
`GetTransitosPendientesAsync` (usa `HashSet` de grupos con entrada), `ActualizarFechaTrasladoAsync` y
`EliminarTrasladoAsync` (operan sobre `ToListAsync()` de todo el grupo), `GetIngresosAsync` (fila por movimiento),
`EliminarIngresoAsync` / `AnularMovimientoHistoricoAsync` (por `movimientoId`).

---

## 3. Archivos a crear / modificar

### Backend

| Archivo | Cambio |
|---|---|
| `Application/DTOs/InventarioGestionDtos.cs` | **+** `InventarioGestionRecepcionDestinoDto(NucleoId, GalponId, Quantity)`; **+** parámetro opcional `Distribucion` en `InventarioGestionRecepcionTransitoRequest`; **+** `InventarioGestionRecepcionTransitoResultDto(Destinos, Movimientos)`. |
| `Application/Calculos/InventarioGestionRecepcionDistribucionCalculos.cs` | **NUEVO** — `static class` pura: `Resolver(distribucion, toNucleoId, toGalponId, usaUbicacion, cantidadTransito) → (destinos, error)`. |
| `Application/Interfaces/IInventarioGestionService.cs` | `RegistrarRecepcionTransitoAsync` pasa a devolver `InventarioGestionRecepcionTransitoResultDto`. |
| `Infrastructure/Services/InventarioGestionService.cs` | `RegistrarRecepcionTransitoAsync`: delega validación al cálculo, valida pertenencia de galpones (solo camino distribuido) y persiste N stocks + N movimientos. **Fix** `GetTrasladosAsync` (diccionario de entradas). |
| `API/Controllers/InventarioGestionController.cs` | Respuesta **aditiva**: `{ destino, movimiento, destinos, movimientos }` (los dos primeros = primer elemento → no rompe el contrato actual). |
| `tests/ZooSanMarino.Application.Tests/InventarioGestionRecepcionDistribucionCalculosTests.cs` | **NUEVO** — xUnit. |

### Frontend

| Archivo | Cambio |
|---|---|
| `features/gestion-inventario/services/gestion-inventario.service.ts` | **+** `InventarioGestionRecepcionDestino`; **+** `distribucion?` en el request; respuesta tipada aditiva. |
| `.../pages/gestion-inventario-page/gestion-inventario-page.component.ts` | Estado `recepcionDistribuir` + `recepcionDestinos[]`, alta/baja de filas, totales, validación espejo del backend, envío del payload. |
| `.../gestion-inventario-page.component.html` | Toggle **“Recibir todo en un galpón / Distribuir entre galpones”** + tabla de filas (Núcleo, Galpón, Cantidad, quitar) + contador `distribuido / total / faltante`. |
| `.../gestion-inventario-page.component.scss` | Estilos de la tabla de distribución (tokens existentes, sin colores hardcodeados nuevos). |

**Sin migración de BD:** el modelo ya soporta N movimientos por `TransferGroupId` y N filas de stock por
(granja, núcleo, galpón, ítem).

---

## 4. Reglas de negocio

1. **Solo alimento por galpón.** Si `usaUbicacion == false` y llega `Distribucion` con filas →
   `"La distribución por galpón solo aplica a alimento manejado por galpón. Esta recepción es a nivel granja."`
2. **Sin distribución = comportamiento actual**, con los mensajes actuales intactos:
   - falta núcleo/galpón con `usaUbicacion` → `"Para alimento debe indicar Núcleo y Galpón de recepción en la granja destino."`
   - núcleo/galpón presentes sin `usaUbicacion` → `"La recepción es solo a nivel granja (sin Núcleo/Galpón)."`
3. **Cada fila** debe traer núcleo **y** galpón → `"Cada destino de la distribución debe indicar Núcleo y Galpón."`
4. **Cantidades > 0** → `"Las cantidades de la distribución deben ser mayores a cero."`
5. **Sin galpones repetidos** → `"No repita el mismo galpón en la distribución (galpón {id})."`
6. **La suma debe igualar la cantidad en tránsito** (tolerancia `0.0001`) →
   `"La suma de la distribución ({suma}) debe ser igual a la cantidad en tránsito ({cantidad})."`
   No se permite recepción parcial (el tránsito se cierra completo, como hoy).
7. **Pertenencia:** cada (núcleo, galpón) debe existir en la **granja destino** → error explícito.
   Se valida **solo** en el camino distribuido (no se endurece el camino de un galpón para no cambiar comportamiento).
8. **Filas vacías se ignoran** (núcleo/galpón en blanco y cantidad 0) → una tabla con filas sueltas sin llenar
   no rompe el envío.
9. **Descuento en origen sin cambios:** solicitudes antiguas (`TrasladoInterGranjaPendiente`) descuentan origen
   **una sola vez** al recibir, independientemente de en cuántos galpones se distribuya.
10. **Reason por asiento:** con una sola ubicación se conserva `"Recepción traslado inter-granja"`;
    distribuido → `"Recepción traslado inter-granja (distribución i/N)"`.

---

## 5. Casos de prueba

### xUnit (cálculo puro)

- [ ] Sin distribución + `usaUbicacion` + núcleo/galpón → 1 destino con la cantidad total.
- [ ] Sin distribución + `usaUbicacion` + falta galpón → error con el **mensaje actual**.
- [ ] Sin distribución + nivel granja + sin núcleo/galpón → 1 destino `(null, null, total)`.
- [ ] Sin distribución + nivel granja + con núcleo/galpón → error con el **mensaje actual**.
- [ ] Distribución en 3 galpones que suma exacto → 3 destinos, cantidades normalizadas (trim de ids).
- [ ] Distribución cuya suma **no** cuadra (por exceso y por defecto) → error con suma y total.
- [ ] Distribución con galpón repetido → error.
- [ ] Distribución con cantidad 0 o negativa → error.
- [ ] Distribución con fila sin núcleo o sin galpón → error.
- [ ] Distribución en granja a **nivel granja** → error (no aplica).
- [ ] Filas totalmente vacías → se ignoran (si quedan 0 filas, cae al camino clásico).
- [ ] Diferencia dentro de la tolerancia (`0.00005`) → válido.

### Smoke funcional (manual, local)

- [ ] Traslado inter-granja de alimento (1.000 kg) → Tránsito → recibir **distribuyendo** 400/350/250 en 3 galpones:
      stock por galpón correcto, 3 filas en Ingresos/Histórico, el tránsito desaparece de la lista.
- [ ] Recepción **clásica** en un galpón → idéntico a hoy.
- [ ] Recepción de ítem **no alimento** → sigue a nivel granja, sin UI de distribución.
- [ ] Pestaña **Traslados** carga sin error después de una recepción distribuida (regresión del `ToDictionary`).
- [ ] Reintentar la recepción de un tránsito ya recibido → `"Este traslado ya fue recibido en destino."`

### Validación de build

- [ ] `cd backend && dotnet build` (0 errores, sin advertencias nuevas)
- [ ] `cd backend && dotnet test`
- [ ] `cd frontend && yarn build` (solo el warning preexistente de bundle budget)
