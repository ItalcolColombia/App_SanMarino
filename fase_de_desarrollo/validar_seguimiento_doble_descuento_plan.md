# Validar un seguimiento descuenta el alimento DOS veces (31-ago-2026)

Sale de la auditoría adversarial de los casos declarados resueltos: **dos auditores independientes**,
mirando tickets distintos (`TK-2026-000164` y `TK-2026-000166`), encontraron el mismo defecto con los
mismos 8 ids. Verificado después contra la BD y el código, punto por punto.

## 1. El defecto, medido

`POST /api/ValidacionSeguimiento/validar` aplica el consumo **dos veces** cuando entran dos llamadas
solapadas. Medido en la copia de producción, **8 pares vivos, todos de ItalcolPanama**:

| Referencia | Granja | Galpón | Ítem | kg × 2 |
|---|---|---|---|---|
| `#11993 2026-08-17 (validado)` | 105 | G0491 | 213 | 2.758,000 |
| `#12004 2026-08-11 (validado)` | 105 | G0492 | 213 | 2.041,000 |
| `#12635 2026-08-21 (validado)` | 105 | G0494 | 213 | 5.670,000 |
| `#12660 2026-08-25 (validado)` | 106 | G0482 | 213 | 1.361,000 |
| `#12666 2026-08-24 (validado)` | 107 | G0461 | 223 | 1.542,240 |
| `#12680 2026-08-21 (validado)` | 107 | G0471 | 223 | 2.268,000 |
| `#12681 2026-08-22 (validado)` | 107 | G0471 | 223 | 2.268,000 |
| `#12770 2026-08-26 (validado)` | 106 | G0481 | 213 | 1.769,000 |

**19.677,24 kg descontados de más en 7 galpones.** No son dos líneas de alimento distintas: cada
seguimiento tiene **una sola** fila en `seguimiento_reserva_alimento`, con el kg exacto de **un**
movimiento, y en estado `APLICADA`.

## 2. Causa raíz

`ValidacionSeguimientoService.Validar.cs`. El doc-comment promete *«Idempotente: validar dos veces no
descuenta dos veces»*, y **es falso para llamadas concurrentes**: las lecturas que sostendrían esa
idempotencia ocurren **fuera** de la transacción y sin bloqueo.

```
LeerEstadoAsync(...)                 ← fuera de la transacción
if (estado.Validado) return ...      ← fuera: las dos llamadas leen false
leer reservas WHERE Estado == Activa ← fuera: las dos leen la MISMA reserva
await using var tx = BeginTransaction ← recién acá
AplicarAlimentoAsync(...)            ← las dos aplican
```

Dos requests solapadas leen ambas `Validado = false` y la misma reserva activa, abren cada una su
transacción y cada una emite su `RegistrarConsumoAsync`. No hay bloqueo de fila, ni token de
concurrencia, ni unicidad en `inventario_gestion_movimiento.reference`.

**El disparador está a mano**: el botón ✓ de la grilla de engorde no se deshabilita mientras la
petición está en vuelo — `[disabled]="disableEditDelete"` es el `@Input` de «lote cerrado», no el
estado de carga. Doble clic, o el reintento del cliente, y el galpón pierde el doble de kilos sin un
solo mensaje de error. `ValidarEnBloque` llama a `ValidarAsync`, así que hereda tanto el defecto como
el arreglo.

## 3. Alcance real del daño (medido, no supuesto)

- **Kardex** (`inventario_gestion_movimiento`): 8 movimientos `Consumo` de más.
- **Histórico unificado**: **también se duplicó** — 8 filas `INV_CONSUMO` con `anulado = false`, que
  la tabla diaria de engorde sí lee (`hist_full`).
- **Stock** (`inventario_gestion_stock`): 19.677,24 kg de menos en 7 galpones.
- **Aves: NO afectadas.** `RetiroAvesEngordeAplicador` es idempotente por delta contra el histórico.
- **Des-validar no lo repara**: devuelve **una** vez sobre un consumo aplicado **dos**.

## 4. Qué se corrige

### A · Backend — cerrar la carrera (causa raíz)

Patrón **«tomar primero, aplicar después»**, el mismo que ya usa el repo para el stock atómico:

1. Abrir la transacción **antes** de decidir.
2. `TomarValidacionAsync`: `ExecuteUpdateAsync` **condicional** — `SET validado = true WHERE id = @id
   AND validado = false` (en reproductora la columna es `confirmado`). Postgres serializa las dos
   transacciones sobre esa fila; la segunda reevalúa el predicado tras el lock y afecta **0 filas**.
3. Si afectó 0 filas ⇒ otra instancia ganó ⇒ devolver `YaEstabaValidado: true` **sin aplicar nada**.
4. Solo la que afectó 1 fila lee las reservas **dentro** de la transacción y aplica.

El `if (estado.Validado) return` temprano se conserva como atajo barato, pero deja de ser lo que
garantiza la exclusión. `MarcarValidadoAsync` sigue existiendo para `DesvalidarAsync`.

### B · Frontend — el disparador

El botón ✓ se deshabilita mientras su propia petición está en vuelo.

### C · Test — `DuplicadosValidacionCalculos` (cálculo puro, xUnit)

La regla de remediación es lógica pura y por lo tanto testeable: de N movimientos con la misma
referencia, ítem y ubicación, **se conserva el de menor id** y se revierten los demás, acumulando los
kg a devolver por ubicación. Es exactamente lo que ejecuta la migración, y los tests la fijan.

### D · Datos — revertir los 8 duplicados, por migración

**Simulado antes en una transacción revertida**, que es lo que exige el repo antes de tocar datos.
Resultado de esa simulación, y por qué la migración tiene dos pasos y no uno:

- `DELETE` del movimiento duplicado ⇒ el trigger `trg_inventario_gestion_movimiento_lote_hist_del`
  **sí** deja su fila del histórico en `anulado = true`. ✅
- Pero el `DELETE` **NO devuelve el stock**: la fila de `inventario_gestion_stock` queda igual. ❌
  Es el mismo patrón de [[eliminar-ingreso-no-devolvia-el-stock]] — dos caminos para lo mismo, uno
  revierte y el otro no.

⇒ La migración hace las dos cosas en la misma transacción: borra el duplicado **y** suma los kg de
vuelta al stock de esa ubicación exacta.

**No se usa un Ingreso compensatorio** (que es como `DesvalidarAsync` devuelve): un ingreso suelto
aparecería en el histórico como entrada de alimento de ese día y le mentiría al cuadre del galpón.
Acá no hubo una entrada: hubo una salida que nunca debió existir.

Se identifican por **firma** (misma `reference` + ítem + ubicación + cantidad, con `count(*) > 1`), no
por ids literales: los ids de local y producción no tienen por qué coincidir.

## 5. Casos de prueba

1. `dotnet test` — los tests nuevos de `DuplicadosValidacionCalculos` en verde.
2. Migración: `Up()` dos veces en transacción revertida ⇒ la 2ª no encuentra duplicados y no mueve nada.
3. Post-`Up()`: `count(*) > 1` sobre la firma ⇒ **0 filas**; las 8 filas del histórico en
   `anulado = true`; el stock de los 7 galpones **+19.677,24 kg** repartidos exactamente.
4. Ninguna otra empresa tocada: la consulta de duplicados solo devuelve `company_id = 5`.
5. `dotnet build` y `yarn build` sin errores.
