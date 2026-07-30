# Plan — Que estos descuadres no se puedan repetir

**Fecha:** 2026-07-30 · **Origen:** los 5 commits de `e2a8a3d` a `3c4d3d0` (apertura, enganche, índice,
v12 y huecos del histórico). Costos validó el resultado; esto ataca las **causas**, no los síntomas.

## Contexto — qué falló realmente

| # | Causa | Evidencia |
|---|---|---|
| C1 | La coherencia del histórico dependía de **disciplina del C#**, no de la BD | `trg_inventario_gestion_movimiento_lote_hist` es **el único de los 8 triggers de la BD que es solo `INSERT`**. La tabla borra filas **físicamente** (3 sitios) y **no tiene anulación lógica**. Cuatro caminos deshacían movimientos; dos se olvidaban de anular el histórico |
| C2 | El descuadre lo detectó **un humano**, semanas después | No existe ninguna verificación automática del invariante |
| C3 | **Tres implementaciones** del mismo saldo divergieron | fn SQL + `SeguimientoAvesEngordeService` + `SeguimientoAvesEngordeEcuadorService` |
| C4 | Un fix validado en **una sola empresa** se desplegó y rompió otra | La ventana v9 se midió contra Panamá; Ecuador encadena ciclos por galpón. Regresión detectada a las 24 h |
| C5 | Datos capturados **fuera del ciclo** se aceptan en silencio | Kilometro 86 / G0040 recibió 182.630 kg fechados después de que su ciclo cerró |

---

## Punto 1 — Que el invariante lo garantice la BD

**Modelo a copiar: `movimiento_pollo_engorde`**, el módulo gemelo, que ya lo resolvió:

```sql
CREATE TRIGGER trg_movimiento_pollo_engorde_lote_hist_anula
AFTER UPDATE OF estado, deleted_at ON movimiento_pollo_engorde
FOR EACH ROW WHEN (new.estado = 'Anulado' OR new.deleted_at IS NOT NULL)
EXECUTE FUNCTION trg_lote_hist_mov_pollo_anulado()
```

**Qué se hace:** dos triggers sobre `inventario_gestion_movimiento` que anulan su fila del histórico:

1. `AFTER DELETE` → cubre los 3 borrados físicos y **cualquier `DELETE` futuro o manual**.
2. `AFTER UPDATE OF movement_type` cuando pasa a un tipo cancelado (`TrasladoInterGranjaRechazado`).

**Qué NO se hace, y por qué:** pasar la tabla a **borrado lógico** exigiría auditar todas las lecturas
(`GetStockAsync`, `GetMovimientosAsync`, `GetTrasladosAsync`, `GetIngresosAsync`,
`GetTransitosPendientesAsync`…) y una sola omitida **resucita** movimientos anulados. El trigger
`AFTER DELETE` cierra el agujero **igual de bien** para la correctitud del saldo, con una fracción del
riesgo. El borrado lógico aporta trazabilidad, no correctitud: queda anotado como trabajo aparte.

Con esto el C# de `AnularMovimientoHistoricoAsync` / `RechazarTransitoPendienteAsync` pasa a ser
redundante (se conserva: es idempotente y explícito), pero **ningún camino futuro puede olvidarse**.

## Punto 2 — El cuadre como verificación automática

`fn_cuadre_alimento_engorde()`: por galpón, el ciclo activo, su saldo, el stock físico, los movimientos
posteriores al último seguimiento y el descuadre. Es la misma query con la que se validaron los 5
commits (hoy: Ecuador 35/35, Panamá 25/25, error 0,0).

- Función SQL + migración idempotente.
- Endpoint de solo lectura, acotado por empresa activa, para que operación lo mire.
- **Clasificación pura y testeada** en `Application/Calculos/CuadreAlimentoEngordeCalculos.cs`
  (OK / descuadrado / saldo negativo / sin seguimiento), con tolerancia explícita.

## Punto 3 — De tres implementaciones a una

`RecalcularSaldoAlimentoPorLoteAsync` de **los dos** services pasa a delegar en
`SaldoAlimentoEngordeAplicador.RecalcularPorLoteAsync`, que escribe **desde la fn**. Así la columna
persistida es idéntica a la pantalla por construcción y no hay dos aritméticas que puedan separarse.

`SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento` **se conserva y se documenta como
especificación ejecutable** de la fórmula: sus tests son el contrato que la fn debe cumplir. No es
código muerto, es el oráculo.

## Punto 4 — Gate multipaís al tocar cálculo compartido

- `backend/sql/verificar_paridad_saldo_engorde.sql`: congela la salida de la fn para **todos** los
  lotes y la compara contra un snapshot previo, por empresa (filas, saldo, aves, ingreso, documento).
- Regla **vinculante en `CLAUDE.md`**: si el cambio toca `fn_seguimiento_diario_engorde` o cualquier
  `*SaldoAlimento*`, la validación obligatoria es comparación fila a fila **en todas las empresas**.

## Punto 5 — Avisar cuando el dato entra fuera de ciclo

Al registrar un ingreso o traslado en un galpón, si la fecha cae fuera del ciclo vigente se devuelve un
**aviso** (no se bloquea: retrofechar es legítimo y bloquearlo tiene costo operativo).

- Puro y testeado: `Application/Calculos/AvisoFechaFueraDeCicloCalculos.cs`.
- Casos: antes del primer seguimiento del ciclo activo · después del cierre del último ciclo · en el
  hueco entre dos ciclos · galpón sin ningún lote.

---

## Validación

- [ ] `dotnet build` 0/0 · `dotnet test` verde
- [ ] Punto 1: smoke en BD — `DELETE` directo por SQL anula el histórico **sin** pasar por el C#
- [ ] Punto 2: la fn devuelve hoy Ecuador 35/35 y Panamá 25/25 con error 0,0
- [ ] Punto 3: el saldo persistido no cambia (ya es igual a la fn) — comparación antes/después
- [ ] Punto 4: el script detecta una diferencia inyectada a propósito
- [ ] Punto 5: tests de los cuatro casos
- [ ] Migraciones idempotentes y con `Down`


---

# Ejecución — resultado de los 5 puntos

**Fecha:** 2026-07-30 · `dotnet build` 0/0 · `dotnet test` **1.417 verdes** (1.395 + 22).

## Punto 1 — El invariante lo garantiza la BD ✅

`backend/sql/trg_inventario_gestion_anular_historico.sql` + migración
`20260730160000_PrevencionDescuadresAlimentoEngorde`.

Dos triggers sobre `inventario_gestion_movimiento`, copiando el patrón que el módulo gemelo
`movimiento_pollo_engorde` ya usaba:

- `..._lote_hist_del` — `AFTER DELETE`, cubre los 3 borrados del C# **y cualquier DELETE manual**.
- `..._lote_hist_cancel` — `AFTER UPDATE OF movement_type` cuando pasa a `TrasladoInterGranjaRechazado`.

**Prueba que lo cierra:** un `DELETE` por SQL crudo, sin pasar por el C#, deja el histórico en
`anulado = t` y el saldo vuelve solo de 16.380 a 11.380.

**Lo que NO se hizo, a propósito:** pasar la tabla a borrado lógico. Obligaría a auditar todas las
lecturas y una sola omitida resucita movimientos anulados. El `AFTER DELETE` cierra el agujero igual de
bien para la correctitud del saldo, con una fracción del riesgo. El borrado lógico aporta trazabilidad,
no correctitud: queda anotado como trabajo aparte.

## Punto 2 — El cuadre como verificación ✅

- `fn_cuadre_alimento_engorde(company_id)` — el invariante por galpón.
- `CuadreAlimentoEngordeCalculos` (puro, 9 tests): tolerancia de 1 kg y prioridad
  **descuadre > saldo negativo** (el primero es defecto, el segundo es información).
- `GET /api/CuadreAlimentoEngorde?soloConProblemas=true` — empresa activa, **fail-closed**, y loguea
  `Warning` cuando hay descuadrados.

**Hoy: Ecuador 35/35 · Panamá 25/25 · 0,0 kg de error.**

> ⚠️ **Un bug encontrado al estrenar la función:** tomaba el saldo de la *última fila* de la tabla
> diaria y además restaba los movimientos posteriores — doble conteo, que daba 24/35 falsos. Tiene que
> ser el saldo **en el último día de seguimiento**. Corregido y anotado en el SQL.

## Punto 3 — De tres implementaciones a una ✅

Los dos services delegan en `SaldoAlimentoEngordeAplicador`, que escribe **desde la fn**. El service de
Ecuador pasó de **363 a 187 líneas**: se borraron los helpers que solo servían a la vieja aritmética.
Se conserva el alcance por empresa. Los llamadores ya hacían `Entry(ent).ReloadAsync()`, así que el DTO
sale con el valor nuevo.

`SeguimientoAvesEngordeCalculos` queda como **especificación ejecutable**: sus tests son el contrato que
la fn debe cumplir.

**Verificado:** persistido == grilla en las 5.495 filas, 0 discrepancias en las dos empresas.

## Punto 4 — Gate multipaís ✅

`backend/sql/verificar_paridad_saldo_engorde.sql`: el **mismo comando** las dos veces —la primera
congela la línea base, la segunda compara— sin flags ni modos.

**Probado que detecta:** con 3 diferencias inyectadas a propósito (saldo en Panamá, aves en Ecuador y
una fila borrada) las reporta por empresa e identifica el galpón exacto.

Regla **vinculante agregada a `CLAUDE.md`** en una sección nueva, *Invariantes que NO se pueden romper*,
junto con las otras cuatro aprendidas en este trabajo.

## Punto 5 — Aviso al capturar fuera de ciclo ✅

`AvisoFechaFueraDeCicloCalculos` (puro, 8 tests) + campo `AvisoFechaFueraDeCiclo` en
`InventarioGestionStockDto` (aditivo, nullable). Cableado en `RegistrarIngresoAsync` y en el traslado
dentro de la misma granja, **avisando en los dos galpones**.

**Avisa** cuando la fecha cae en un ciclo ya cerrado (identificando cuál y cuándo cerró) o en el hueco
entre ciclos. **No avisa** con fecha de hoy, dentro del ciclo vigente, ni en la ventana previa al
encaset —el preiniciador legítimo—. **No bloquea:** retrofechar es válido; lo que no puede pasar es
hacerlo sin enterarse.
