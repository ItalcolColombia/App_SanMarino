# Disponible = stock − reservas activas (TK-2026-000168)

**Fecha:** 15ago26 · **Decisión del usuario:** agregar el campo `Disponible` al DTO y tocar el front,
**no** restarle la reserva a `Quantity`.

---

## 1. El problema

`IValidacionSeguimientoService.ReservadoPorItemAsync` y `ReservadoDeAvesAsync` están declarados e
implementados (`ValidacionSeguimientoService.Reservas.cs:121,143`) pero **no los llama nadie**. El
disponible que ven inventario y los formularios de los 4 seguimientos sigue siendo el stock completo,
ignorando lo que la doble validación ya tiene separado.

Importa porque **el mismo galpón alimenta a dos lotes**: sin restar las reservas activas, los dos ven
los mismos kilos y los dos creen tenerlos. Es exactamente el escenario que motivó la separación.

Afecta solo a empresas con `requiere_validacion_seguimiento_diario` encendido (hoy: ItalcolPanama).

---

## 2. Por qué NO se le resta a `Quantity`

`Quantity` es la **existencia física** del galpón: es el número que operación concilia contra el
conteo. Restarle una reserva —que es un compromiso, no una salida— haría que la pantalla de inventario
dejara de cuadrar contra la bodega sin que nada lo explique. La reserva se muestra **al lado**, no
adentro.

---

## 3. `DisponibleKg` tiene que ser DERIVADO, no un parámetro más

`InventarioGestionStockDto` ya declara `ReservadoKg = 0` y `DisponibleKg = 0` (los dejó el diseño de
V1, sin llenar). Dejar `DisponibleKg` como parámetro posicional es una trampa: hay **9 sitios** en
`InventarioGestionService` que construyen el DTO a mano para las respuestas de ingreso, traslado y
consumo, ninguno llega hasta ese parámetro, y todos quedarían devolviendo `disponible = 0` — el front
leería "no hay nada" sobre un galpón lleno.

Se convierte en **propiedad calculada**:

```csharp
public decimal DisponibleKg => Quantity - ReservadoKg;
```

Con eso los 9 sitios quedan correctos sin tocarlos (`ReservadoKg = 0` ⇒ `DisponibleKg = Quantity`), y
la fórmula tiene un solo dueño (CLAUDE.md §🛡️ *Una sola fórmula por número*).

---

## 4. La clave de la reserva incluye el SILO

`ReservadoPorItemAsync` agrupa solo por ítem y casa por granja/núcleo/galpón. En empresas con
`maneja_inventario_por_silo` el stock vive **por silo**, así que la reserva tiene que casar por
`(granja, núcleo, galpón, silo, ítem)` o un galpón con dos silos sumaría mal. Hoy no explota porque
ninguna empresa tiene los dos flags encendidos a la vez, pero es una bomba de tiempo: se hace bien
ahora.

---

## 5. El ciclo de DI es real

`ValidacionSeguimientoService` ya depende de `IInventarioGestionService`. Inyectar
`IValidacionSeguimientoService` dentro de `InventarioGestionService` cierra el ciclo y revienta al
resolver. Mismo problema que resolvió `SaldoAlimentoEngordeAplicador`, y misma salida: un **lector
estático** que recibe el `DbContext`.

`ReservaAlimentoLector` (static, Infrastructure) queda como **único** dueño de la consulta, y tanto
`GetStockAsync` como `ValidacionSeguimientoService.ReservadoPorItemAsync` delegan en él.

---

## 6. Archivos

| Archivo | Cambio |
|---|---|
| `Application/DTOs/InventarioGestionDtos.cs` | `DisponibleKg` deja de ser parámetro y pasa a propiedad calculada |
| `Application/Calculos/ReservaUbicacionCalculos.cs` | **nuevo**: clave normalizada `(farm, núcleo, galpón, silo, ítem)` + `Disponible()`. Puro, con tests |
| `Infrastructure/Services/ValidacionSeguimiento/ReservaAlimentoLector.cs` | **nuevo**: static, UNA consulta agrupada sobre `seguimiento_reserva_alimento` para N granjas |
| `Infrastructure/Services/InventarioGestionService.cs` | `GetStockAsync` llena `ReservadoKg` (gate por flag de empresa) |
| `Infrastructure/Services/ValidacionSeguimiento/Funciones/…Reservas.cs` | `ReservadoPorItemAsync` delega en el lector |
| `tests/…/ReservaUbicacionCalculosTests.cs` | **nuevo** |
| front `gestion-inventario/services/gestion-inventario.service.ts` | `reservadoKg` / `disponibleKg` en la interfaz |
| front · 4 modales de seguimiento | el mapa de saldos se arma con `disponibleKg`, no con `quantity` |
| front `gestion-inventario-page` | columna **Reservado**, visible solo si hay algo reservado |

**Sin cambios de BD.** La agregación va en la consulta (CLAUDE.md: *el backend orquesta, la BD filtra*).

---

## 7. Reglas de negocio

- **Flag OFF ⇒ idéntico**: sin reservas activas, `ReservadoKg = 0` y `DisponibleKg = Quantity`. Es el
  invariante que fijan los tests.
- `Quantity` **nunca** cambia de significado: sigue siendo la existencia física.
- `DisponibleKg` **puede quedar negativo** y no se recorta a cero: ese número es la señal de que dos
  lotes se pisaron sobre el mismo galpón. Recortarlo escondería justo lo que hay que ver.
- Solo se descuentan reservas en estado `ACTIVA`. Las `APLICADA` ya salieron del stock (doble
  descuento si contaran) y las `LIBERADA` no comprometen nada.

---

## 8. Casos de prueba

| # | Caso | Esperado |
|---|---|---|
| 1 | Sin reservas | `Disponible == Quantity` |
| 2 | Reserva activa de 250 sobre stock 1000 | `Reservado 250`, `Disponible 750` |
| 3 | Reserva mayor que el stock | `Disponible` NEGATIVO, sin recorte |
| 4 | Reservas `APLICADA` / `LIBERADA` | no cuentan |
| 5 | Mismo ítem en dos galpones | cada uno con su reserva, sin mezclarse |
| 6 | Mismo galpón, dos silos | cada silo con la suya |
| 7 | Núcleo/galpón `null` vs `""` vs `"  "` | misma clave (normalización) |
| 8 | Front, empresa sin el flag | la columna Reservado no se dibuja y el saldo es el de siempre |

---

## 9. Validación

- `dotnet build` 0 errores · `dotnet test` verde.
- `yarn build` sin errores de TS ni de plantilla.
- Smoke con el flag ON: crear seguimiento sin validar ⇒ `disponible` baja y `quantity` NO; validar ⇒
  `quantity` baja y `disponible` no se mueve otra vez.
- Smoke con el flag OFF: `disponible == quantity` en toda la lista.
- Base restituida al baseline y backend del usuario intacto.
