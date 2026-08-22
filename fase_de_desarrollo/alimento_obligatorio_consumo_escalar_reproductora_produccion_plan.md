# Alimento obligatorio: Reproductora y Producción ignoran el consumo escalar

> Origen: `fase_de_desarrollo/app_movil_italgranja_plan.md` §7 (hallazgos del smoke de la app móvil,
> 21-ago-2026). Los dos hallazgos salen del mismo módulo y se cierran juntos.

## 1. El problema

`SeparacionSeguimientoHelper.ValidarAlimentoObligatorio` recibe `kgHembrasDirecto` /
`kgMachosDirecto` **precisamente** para el cliente que manda el consumo como campo suelto en vez de
como ítems de inventario — lo dice su propio doc-comment. Tres de los cinco módulos se los pasan;
**Reproductora y Producción no**:

| Módulo | Llamada | Pasa los directos |
|---|---|---|
| Levante | `SeguimientoLoteLevanteService.Crud.cs:38` | ✅ |
| Engorde | `SeguimientoAvesEngordeService.Crud.cs:97` | ✅ |
| Engorde Ecuador | `…EngordeEcuadorService.Crud.cs:41` | ✅ |
| **Reproductora** | `SeguimientoDiarioLoteReproductoraService.cs:267` (alta) y `:384` (edición) | ❌ |
| **Producción** | `ProduccionService.Seguimiento.cs:238` (alta) y `:628` (edición) | ❌ |

`MetadataEngordeCalculos.ParseKgPorBloque` sólo suma ítems con `catalogItemId` /
`itemInventarioEcuadorId` **> 0**, así que un registro con `consumoHembras: 120` y sin ítems de
inventario cuenta como **cero kilos** y el backend responde 400 «no tiene alimento» sobre un
registro que **sí** trae alimento.

Sólo se dispara con `companies.requiere_validacion_seguimiento_diario = true`, que hoy tiene
únicamente **ItalcolPanama (id 5)**. Medido el 21-ago-2026: `POST
/api/SeguimientoDiarioLoteReproductora` con alimento escalar → 400; con el flag apagado en local el
mismo request creó el id 791. Afecta a la app móvil (`zootecnicoapp`), a la carga masiva por Excel
y a la PWA.

### 1.b El patrón que hay que copiar tiene un bug propio

Los tres services que **sí** pasan los directos escriben:

```csharp
(decimal)dto.ConsumoKgHembras, (decimal)dto.ConsumoKgMachos!
```

`ConsumoKgMachos` es `double?`. La conversión explícita `double? → decimal` **desenvuelve**: con
`null` lanza `InvalidOperationException("Nullable object must have a value.")` — verificado el
21-ago-2026 ejecutando la expresión con el SDK 10.0.301. El `!` sólo calla al compilador; no
protege nada. Y como los controllers traducen `InvalidOperationException` a **400**, el usuario ve
una validación que dice *«Nullable object must have a value»*.

`consumoKgMachos` es `null` de verdad: `CreateSeguimientoLoteLevanteRequest.ToDto` lo inicializa en
`alimentosMachos.Count > 0 ? … : null`, o sea que cualquier registro sin alimento de machos
—Panamá mixto, todo el tiempo— llega con `null`.

**Decisión:** copiar el patrón, no el bug. Las seis llamadas usan `(decimal)(x ?? 0)`. Con valor
presente el resultado es idéntico; con `null` pasa a valer 0 kg, que es lo que la regla ya asume
(sin flag, ese campo ni se mira). Se corrigen también los tres call sites existentes: es la misma
línea y el defecto muerde justo al cliente para el que se hace este trabajo.

### 1.c Producción no tiene `ConsumoKgHembras` en el request

`CrearSeguimientoRequest` trae `ConsumoH` / `ConsumoM` **con unidad** (`UnidadConsumoH`, que puede
ser `g`). El service ya normaliza a kilos en las variables locales `consumoKgH` / `consumoKgM`
—antes del punto donde se valida, en las dos rutas (alta y edición)—. Son ésas las que se pasan:
usar `request.ConsumoH` crudo mandaría gramos como si fueran kilos.

## 2. El segundo hallazgo: el duplicado sale como 500

`SeguimientoDiarioLoteReproductoraController.Create` no tiene el
`catch (DbUpdateException … 23505)` que sí tiene `SeguimientoAvesEngordeEcuadorController`: la
violación del índice único (mismo lote y día) cae en el `catch (Exception)` genérico y vuelve como
**500** con el mensaje crudo de Postgres. Se copia el catch, con el mismo texto.

⚠️ El `catch` específico va **antes** del `catch (Exception)`: C# evalúa en orden y el genérico se
lo comería.

## 3. Enfoque arquitectónico

La combinación «kilos del metadata **vs** kilos del campo suelto» es lógica **pura**, hoy escrita
inline en Infrastructure. Por la regla del repo (*math/lógica pura → `Application/Calculos/`*) y
porque `ZooSanMarino.Application.Tests` **sólo referencia Application** —no puede ver
`SeparacionSeguimientoHelper`—, se extrae a `AlimentoObligatorioCalculos.Capturado(...)` y el
helper delega. Sin eso, el fix no es testeable por el gate del repo.

Se conserva el **MÁXIMO, no la suma** (cuando el registro trae las dos cosas son el mismo alimento
expresado dos veces; sumarlas duplicaría los kg) y `KgGenerales` sigue saliendo sólo del metadata.

## 4. Archivos

**Modificar**
- `src/ZooSanMarino.Application/Calculos/AlimentoObligatorioCalculos.cs` — nuevo `Capturado(...)`.
- `src/ZooSanMarino.Infrastructure/Services/ValidacionSeguimiento/SeparacionSeguimientoHelper.cs` — delega.
- `src/ZooSanMarino.Infrastructure/Services/SeguimientoDiarioLoteReproductoraService.cs` — 2 llamadas.
- `src/ZooSanMarino.Infrastructure/Services/Funciones/ProduccionService.Seguimiento.cs` — 2 llamadas.
- `…/SeguimientoLoteLevante/Funciones/SeguimientoLoteLevanteService.Crud.cs` — null-safe.
- `…/SeguimientoAvesEngorde/Funciones/SeguimientoAvesEngordeService.Crud.cs` — null-safe.
- `…/SeguimientoAvesEngordeEcuador/Funciones/SeguimientoAvesEngordeEcuadorService.Crud.cs` — null-safe.
- `src/ZooSanMarino.API/Controllers/SeguimientoDiarioLoteReproductoraController.cs` — catch 23505.

**Crear**
- `tests/ZooSanMarino.Application.Tests/AlimentoObligatorioConsumoEscalarTests.cs`.

**Sin cambios de BD.** No hay migración: es lógica de aplicación.

## 5. Reglas de negocio

1. Un registro cumple si hay kilos en el bloque exigido, **vengan del metadata o del campo suelto**.
2. Metadata + campo suelto se combinan con **MAX**, nunca con suma.
3. `itemsGenerales` sigue sin contar (si contara, un registro con sólo vitaminas pasaría).
4. Con `requiere_validacion_seguimiento_diario = false` no se evalúa nada: `separa` queda en
   `false` y el método corre exactamente como antes.
5. Los mensajes de rechazo **no cambian** ni una letra.

## 6. Casos de prueba (xUnit, `Application.Tests`)

| # | Caso | Espera |
|---|---|---|
| 1 | Reproductora: metadata con ítems sin id + `consumoHembras: 120` escalar | **cumple** (era el 400) |
| 2 | Producción: ídem | **cumple** |
| 3 | Metadata `null` + escalar > 0 | cumple |
| 4 | Sin consumo ni ítems | rechaza, **mismo texto de hoy** (comparación literal) |
| 5 | Sólo `itemsGenerales` + directos en 0 | rechaza y el motivo nombra «otros ítems» |
| 6 | Ítems con id **y** escalar del mismo alimento | MAX, no suma (no infla los kg) |
| 7 | `ParseKgPorBloque` sobre el payload escalar | devuelve `(0,0,0)` — deja escrita la causa raíz |
| 8 | Flag OFF | `SeparaAlGuardar(false) == false` ⇒ no se valida; `DescuentaAlGuardar` sigue siendo su complemento |
| 9 | Machos `null` (`?? 0`) | no lanza; decide igual que con 0 |
| 10 | Sólo machos escalar | cumple en postura/reproductora |

## 7. Verificación

- `dotnet build` — 0 errores, sin advertencias nuevas.
- `dotnet test` — toda la suite verde (no sólo los tests nuevos).
- Sin backend local levantado: el cambio es lógica pura + un catch; los tests lo cubren.
