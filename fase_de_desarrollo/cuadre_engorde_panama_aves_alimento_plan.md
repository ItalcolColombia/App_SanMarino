# Plan — Cuadre de aves y alimento en pollo engorde (Panamá)

**Fecha:** 2026-07-29 · **Empresa:** ItalcolPanama (`companies.id = 5`) · **BD:** dump de producción restaurado en local

## Objetivo

Que en cada lote de pollo engorde de Panamá:

1. El **saldo de aves** de la tabla de seguimiento diario coincida con el widget **"Aves disponibles"**, y que
   los primeros 7 días (cruce de reproductora) descuenten correctamente.
2. El **saldo de alimento** de la tabla de seguimiento diario coincida con el stock del módulo
   **Gestión de inventario**, con **inventario compartido por galpón** cuando hay dos lotes en el mismo galpón.

El stock del inventario es la **fuente de verdad** (el usuario ya lo dejó en el valor físico real).

---

## Diagnóstico (medido sobre los 29 lotes con seguimiento)

### Aves — 25 de 29 lotes descuadrados

**Causa A1 (código) — doble descuento en "Aves disponibles".**
`LoteReproductoraAveEngordeService.GetAvesDisponiblesAsync` (línea ~548) parte de `lote.HembrasL/MachosL`
—el maestro, que **ya fue descontado** por `RetiroAvesEngordeAplicador.SincronizarAsync`— y le vuelve a
restar `mortSeg + sel + err`.

Verificado: el descuadre es **exactamente `bajas_aplicadas`** en los 26 lotes con los 7 días completos.
La fórmula era correcta cuando el maestro solo bajaba por ventas; al agregarse el descuento automático de
bajas (commit `04e4118`) quedó restando dos veces.

El maestro está **sano**: `hembras_l + machos_l = aves_encasetadas − bajas_aplicadas` se cumple exacto en
los 29 lotes. No hay que "arreglar datos" para el saldo — hay que arreglar la fórmula.

**Causa A2 (datos) — las bajas de los 7 días del cruce nunca llegaron al maestro.**
En **25 de 29 lotes** hay 0 filas `BAJA_SEGUIMIENTO` para los seguimientos con `origen_cruce = true`, y el
faltante coincide **exactamente** con las bajas de esos 7 días (77 a 698 aves por lote).
`RetiroAvesEngordeAplicador.SincronizarCruceAsync` existe pero nunca corrió para esa cohorte.
Los lotes 142, 179, 180 y 181 sí las tienen aplicadas (cargados después del fix).

**Causa A3 (transitorio, no es bug)** — lotes 179/180/181 aún dentro de los 7 días de reproductora:
el widget resta las aves asignadas y muestra 0. Es el diseño vigente (`sieteDiasCompletos`).

### Alimento — 17 de 25 galpones descuadrados

**Causa F1 (código) — el saldo mezcla dos scopes.**
Ingresos/traslados se leen con scope **galpón**, pero el consumo con scope **lote**. Con dos lotes en el
mismo galpón cada uno ve el 100 % de los ingresos y solo su propio consumo, así que **ambos inflan** su saldo.

Verificado exacto en G0490 (DOÑA MARIA): ingresos del galpón 97.729,6 kg.
– Lote 168 muestra 82.806,4 = 97.729,6 − 14.923,3 (solo su consumo)
– Lote 169 muestra 34.316,9 = 97.729,6 − 63.412,8 (solo su consumo)
– Saldo real compartido = 97.729,6 − 78.336,1 = **19.393,5** (stock 18.939,9)

El mismo bug está en los **tres** caminos:
- SQL `fn_seguimiento_diario_engorde` (CTE `consumo_por_fecha`, `WHERE s.lote_ave_engorde_id = p_lote_id`)
- C# `SeguimientoAvesEngordeService.RecalcularSaldoAlimentoPorLoteAsync` (hist por galpón, `segs` por lote)
- Front `computeSaldoAlimentoKgPorSeguimiento`

**Causa F2 (datos) — G0486 (MENDOZA) tiene los ingresos de G0485 cargados encima.**
Dos corridas de carga masiva el 2026-07-28 (20:53 y 20:56). Solo G0486 recibió filas de **ambas**.
La corrida B de G0486 (18 filas, **128.302,2 kg**) es idéntica en filas y kilos al total de G0485
(18 filas, **128.302,2 kg**). Anularla lleva el galpón de **+127.168,2** a **−1.134,0**.

**Causa F3 (visibilidad) — los ajustes de inventario no llegan al seguimiento.**
Los 44 `AjusteStock` del usuario entran al histórico como `tipo_evento = 'INV_OTRO'`, y ni la fn SQL ni el
C# ni el front miran ese tipo (solo `INV_INGRESO` / `INV_TRASLADO_*`). Además `Quantity = Math.Abs(delta)`
pierde el signo (recuperable del `reason`: "Anterior: X. Nuevo: Y.").

**Identidad verificada en los 24 galpones (0,0 de error):**
`stock = Σ Ingreso − Σ Consumo_inventario + Σ AjusteStock(con signo)`

**Causa F4 (operativa) — DAYLAND no fue ajustado.**
Los 5 galpones de DAYLAND (G0460, G0461, G0463, G0464, G0465) no tienen ningún `AjusteStock`. Su residuo
es el desfase `consumo_inventario − consumo_seguimiento` que en las demás granjas el usuario ya compensó.
Requiere que la operación confirme el stock físico real; no es derivable de los datos.

---

## Simulación de estrategias (ejercicio pedido por el usuario)

Criterio: galpones donde `saldo_seguimiento == stock` con tolerancia 1 kg, sobre 25 galpones.
Script: `scratchpad/sim_final.sql`.

| Estrategia | Cuadran | Veredicto |
|---|---|---|
| **Op1** — solo fix F1, sin tocar datos | 8/25 (32 %) | Base correcta, insuficiente sola |
| **Op3** — anular duplicados exactos | 8/25 | ❌ **empeora** G0464 (−2.268 → −4.892) |
| **Op3b** — anular la corrida espuria de G0486 | 8/25 | ✅ resuelve el peor caso (+127.168 → −1.134) |
| **Op4** — propagar los `AjusteStock` al seguimiento | 2/25 | ❌ **doble descuento**: el ajuste ya está en el stock |
| **Op5** — Op3b + Op4 | 2/25 | ❌ peor |
| **Op2** — Op1 + Op3b + ajuste datado contra el stock | **25/25 (100 %)** | ✅ única que cumple |

**Por qué Op4 falla:** el ajuste del usuario compensa el desfase `consumo_inventario vs consumo_seguimiento`.
El stock ya lo incorpora; aplicarlo otra vez al seguimiento lo descuenta dos veces.

**Conclusión:** la única estrategia que llega al 100 % es la secuencia **F1 → F2 → ajuste datado del residuo**,
con el ajuste registrado como **movimiento auditable y reversible**, nunca como edición silenciosa de ingresos.

---

## ⚠️ Corrección del diagnóstico (2026-07-29, tras validar sobre el dump de producción)

**La premisa «el stock del inventario es la verdad» resultó FALSA.** El inventario **nunca descontó el
consumo de los 7 días del cruce de reproductora** — el mismo bug que afectaba a las aves: esos días los
escribe `fn_cruce_reproductora_a_engorde` por SQL directo, sin pasar por el service.

Verificado al decimal en **19 de 25 galpones**:
`consumo_seguimiento − consumo_inventario = consumo de los días de cruce`

Todo ese consumo es **AV. POLLITO PREINICIADOR (ítem 223)**, lo que comen los pollitos la primera semana.
Caso testigo G0460: el desfase de 7.484,4 kg son exactamente los 7 primeros días (10-16 jun), donde el
seguimiento registró consumo y el inventario no descontó nada; del 18 jun en adelante coinciden exacto.

**Consecuencia:** el **stock quedó inflado**, no el seguimiento. El saldo correcto es el **lógico**:
`ingresos − consumo real del seguimiento`.

La operación venía compensándolo a mano y en varios galpones dio **exacto** (G0490: llevó el ítem 223 de
8.935,862 a 0, y el consumo del cruce es 8.935,9; ídem G0469, G0470, G0472, GALPON). En otros el número
no cerró, y en DAYLAND no se ajustó nada.

**Esto invalida la Fase 3 original para DAYLAND**, que subía el seguimiento hasta el stock inflado
(G0460 a 14.151,5 cuando lo correcto es 6.667,2). Se reescribió: ahora se ajusta el **inventario**.

Decisiones del usuario: manda el **saldo lógico** y se ajusta el inventario · en G0461 se **registra el
ingreso faltante** de 6.622,5 kg (consumió sin ninguna llegada cargada).

---

## Implementación

### Fase 1 — Aves (código + datos)

1. `LoteReproductoraAveEngordeService.GetAvesDisponiblesAsync`: dejar de restar las bajas ya aplicadas al
   maestro. La resta debe contar solo las bajas **no aplicadas** (las que no tienen fila `BAJA_SEGUIMIENTO`),
   para no romper los lotes viejos que nunca descontaron el maestro.
2. Lógica pura nueva en `Application/Calculos/AvesDisponiblesEngordeCalculos.cs` + tests xUnit
   (flag OFF ⇒ comportamiento byte a byte idéntico para los lotes sin bajas aplicadas).
3. Migración idempotente: aplicar al maestro las bajas de los 7 días del cruce en los 25 lotes
   (equivale a correr `SincronizarCruceAsync`), creando su fila `BAJA_SEGUIMIENTO`.
4. Verificación: `saldo_tabla == disponibles_widget` en los 29 lotes.

### Fase 2 — Alimento, scope de galpón (código)

5. `fn_seguimiento_diario_engorde`: `consumo_por_fecha` pasa a scope **galpón** (todos los lotes del galpón).
   Nueva versión v10 por migración idempotente.
6. `RecalcularSaldoAlimentoPorLoteAsync`: `segs` pasa a scope galpón para el saldo (sin cambiar qué
   registros se persisten).
7. Front `computeSaldoAlimentoKgPorSeguimiento`: mismo criterio.
8. Tests del cálculo puro con el caso G0490 (dos lotes, saldo compartido).

### Fase 3 — Alimento, datos (previa confirmación del usuario)

9. Migración idempotente: anular (`anulado = true`) las 18 filas de la corrida espuria de G0486.
10. Migración de ajuste datado del residuo por galpón, con `reason` explicando el origen.
11. **DAYLAND queda fuera** hasta que la operación confirme el stock físico.

### Validación

- `dotnet build` 0 errores · `dotnet test` verde · `yarn build` 0 errores
- Cuadre de los 29 lotes (aves) y 20 galpones (alimento, sin DAYLAND) medido en la BD local
- Smoke UI: lote con dos lotes en el mismo galpón (G0490) mostrando el mismo saldo en ambos

---

## Riesgos

- El fix de "Aves disponibles" **cambia números que hoy se ven en producción** (a favor: hoy están mal).
- La fn `fn_seguimiento_diario_engorde` la comparten reportes de Ecuador y Colombia: el cambio de scope
  del consumo es **no-op** en galpones con un solo lote (la inmensa mayoría), pero hay que verificarlo.
- Nada de la Fase 3 se aplica sin OK explícito del usuario (toca datos de producción).
