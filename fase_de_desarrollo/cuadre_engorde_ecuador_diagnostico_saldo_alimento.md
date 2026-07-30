# Diagnóstico — Saldo de alimento en pantalla ≠ stock (ItalcolEcuador)

**Fecha:** 2026-07-29 · **Empresa:** ItalcolEcuador (`companies.id = 3`) · **Estado:** diagnóstico CERRADO, sin tocar código ni datos
**Reporte del usuario:** *«tenemos una diferencia en el alimento desde el primer día… nos ingresó 12000 − 480 de
consumo, deberíamos tener 11520 pero el aplicativo nos está mostrando 3560, pero solo es en lo visual porque en
el stock sí tenemos lo correcto»*
**Caso testigo:** Kilometro 22 · N1 · Galpon-2 · lote **2603** (`lote_ave_engorde_id = 98`, ERP `316-40202603`)
**Antecedente:** [`cuadre_engorde_ecuador_requerimiento.md`](cuadre_engorde_ecuador_requerimiento.md)

> Medido sobre el dump de producción restaurado en `sanmarinoapplocal:5433` el 2026-07-29.
> La fn local es **v10 con ventana v9** — la misma que corre en producción (verificado en `pg_proc`).

---

## 1. Veredicto

**El usuario tiene razón en los dos puntos: el dato guardado está bien y lo que está mal es la pantalla.**

| Fuente | Saldo día 1 (16-jun) | Saldo última fila (28-jul) |
|---|---:|---:|
| Esperado por la operación (12.000 − 480) | **11.520** | — |
| `seguimiento_diario_aves_engorde.saldo_alimento_kg` (persistido) | **11.520** ✅ | **11.380** ✅ |
| Stock real de `inventario_gestion_stock` (G0036) | — | **11.380** ✅ |
| `fn_seguimiento_diario_engorde(98)` ← **lo que pinta la grilla** | **3.560** ❌ | **3.420** ❌ |

El stock de inventario de G0036 hoy (10.180 DORADO + 1.200 SUPER POLLO ENGORDE + 0 + 0 + 0 = **11.380 kg**)
coincide **al kilo** con el saldo persistido. La grilla se desvía **−7.960 kg exactos en las 43 filas**, desde la
primera hasta la última: no es un error que se acumule, es un **corrimiento constante del saldo de apertura**.

---

## 2. Causa raíz

### 2.1 La aritmética

`fn_seguimiento_diario_engorde` no lee la columna persistida: **recalcula** el saldo como
`saldo(f) = apertura + ingresos(≤f) − consumo_del_galpón(≤f)` (CTE `apertura_alimento` → `pt_calc`, salida `se.pt`).

Para el lote 98 la apertura le da **−7.960 kg**, y `12.000 − 480 − 7.960 = 3.560`. Los cuatro movimientos que la
componen (ventana `2026-06-04 … 2026-06-15`, galpón G0036):

| Fecha | Evento | Ítem | Delta | Dueño |
|---|---|---|---:|---|
| 2026-06-05 | `INV_INGRESO` | AV0410 DORADO | +160 | lote **65** (ciclo anterior, «2602») |
| 2026-06-06 | `INV_TRASLADO_SALIDA` | AV0410 DORADO | −7.520 | lote **65** |
| 2026-06-07 | `INV_TRASLADO_SALIDA` | AV0410 DORADO | −440 | lote **65** |
| 2026-06-07 | `INV_TRASLADO_SALIDA` | AV0410 DORADO | −160 | lote **65** |
| | | **Apertura** | **−7.960** | |

**Ninguno es del lote 2603.** Son la *cola de cierre del ciclo anterior*: el lote 65 terminó su seguimiento el
2026-06-01 y entre el 5 y el 7 de junio se vació su bodega hacia otros galpones. El lote 2603 se encasetó el
**14-jun**, una semana después.

### 2.2 Por qué se cuela la cola del ciclo anterior

La ventana de alimento previo al encaset (**v9**, commit `36a8bab`, 2026-07-28) movió el corte de
`fecha_encaset` a `fecha_encaset − dias_alimento_previo_encaset` (10 días en Ecuador). El objetivo era legítimo:
en engorde el preiniciador llega antes que los pollitos y cortar en el encaset descartaba alimento propio del lote.

El efecto colateral: en Ecuador los galpones **encadenan ciclos sucesivos** (3-4 lotes uno detrás de otro, ya
anotado como diferencia **D4** en el requerimiento), así que retroceder 10 días mete la ventana **dentro del ciclo
anterior**.

### 2.3 Por qué queda negativa y no en cero

Porque el filtro de devoluciones es **asimétrico**: descarta las entradas pero conserva las salidas.

El 2026-06-05 volvieron al galpón **9.000 kg** (5.600 + 3.400) como `INV_INGRESO` con referencia
`Seguimiento aves engorde #… (devolución por eliminación)`. Tanto la fn como el C# los excluyen a propósito —
son el asiento de reversión de un consumo que ya no existe, y contarlos inflaría el saldo. Pero los
**8.120 kg de traslados de salida** que sacaron ese mismo alimento del galpón **sí entran**.

```
entra 9.000 (excluido)  +160 (contado)  −8.120 (contado)   ⇒   apertura = −7.960
```

Físicamente el neto de la limpieza fue ≈ +1.040 kg; el cálculo ve **−7.960**.

### 2.4 Por qué el dato guardado sí está bien

Hay **dos implementaciones del saldo** y la ventana v9 se aplicó solo a una:

| Implementación | Corte de la apertura | Resultado |
|---|---|---|
| [`fn_seguimiento_diario_engorde`](../backend/sql/fn_seguimiento_diario_engorde.sql) (grilla) | `fecha_encaset − 10 d` | **3.560** ❌ |
| [`SeguimientoAvesEngordeService`](../backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngorde/Funciones/SeguimientoAvesEngordeService.SaldoAlimento.cs) (carga masiva) | `fecha_encaset − 10 d` | ❌ mismo sesgo |
| [`SeguimientoAvesEngordeEcuadorService`](../backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngordeEcuador/Funciones/SeguimientoAvesEngordeEcuadorService.SaldoAlimento.cs) (formulario diario, el que **persiste** en Ecuador) | `fecha_encaset` a secas | **11.520** ✅ |

`SeguimientoAvesEngordeEcuadorService.SaldoAlimento.cs:273` llama
`ComputeSaldoAperturaGalponAntesPrimerSeguimiento(hist, firstSegDate, lote.FechaEncaset)` **sin** el parámetro
`diasAlimentoPrevio`, así que conservó el corte viejo. Por eso la columna persistida quedó sana y solo la grilla
—que recalcula— muestra el número corrido.

> Coincide con el «esto se puede ver recién hoy» del reporte: la ventana v9 se desplegó el **28-jul**, el usuario
> lo detectó el **29-jul**. Es una **regresión de un día**, no un problema histórico de datos.

---

## 3. Alcance medido

### 3.1 Grilla vs. valor persistido — 103 lotes de Ecuador con seguimiento

| Situación | Lotes | Kg |
|---|---:|---:|
| Coinciden | 63 | 0 |
| **La grilla muestra DE MENOS** (la queja) | **26** | **98.506** |
| La grilla muestra de más | 14 | −78.501 |

Los 12 lotes más afectados:

| id | Lote | Granja | Galpón | Persistido | Grilla | Dif |
|---:|---|---|---|---:|---:|---:|
| 20 | 2601 | Kilometro 22 | G0035 | 0 | 37.880 | −37.880 |
| 66 | 2602 | Kilometro 22 | G0035 | 20 | 11.519 | −11.499 |
| 11 | 2602 | Sacachun 3b | G0047 | 1.005 | −8.495 | +9.500 |
| 10 | 2602 | Sacachun 3b | G0048 | 495 | −9.005 | +9.500 |
| 12 | 2601 | Kilometro 86 | G0040 | 0 | −9.020 | +9.020 |
| **98** | **2603** | **Kilometro 22** | **G0036** | **11.380** | **3.420** | **+7.960** |
| 5 | 2602 | Sacachun 3b | G0050 | 0 | −7.810 | +7.810 |
| 80 | 2603 | Kilometro 61 | G0037 | 5.040 | −2.480 | +7.520 |
| 104 | 2603 | Kilometro 86 | G0039 | 5.820 | −1.590 | +7.410 |
| 46 | 2602 | SAN GUILLERMO | G0033 | 0 | −5.160 | +5.160 |
| 81 | 2603 | Kilometro 61 | G0038 | 0 | −5.040 | +5.040 |
| 107 | 2604 | Kilometro 61 | G0037 | 2.360 | 7.320 | −4.960 |

### 3.2 Aperturas fantasma por empresa

| Empresa | Lotes con apertura **negativa** | Kg fantasma | Lotes con apertura positiva | Kg |
|---|---:|---:|---:|---:|
| **ItalcolEcuador** | **26** | **−98.692** | 11 | +43.387 |
| ItalcolPanama | **0** | 0 | 9 | +71.799 |

Panamá **no tiene ni un caso negativo**: sus 9 aperturas positivas son justamente el escenario que v9 vino a
resolver (preiniciador que llega antes del encaset). Por eso el fix de Panamá se validó limpio y esto no apareció
allá — **el daño es exclusivo de Ecuador y viene de los ciclos encadenados por galpón**.

---

## 4. Conclusión y opciones (NO aplicadas — pendientes de decisión)

El error **no está en los datos**: el inventario, el stock y la columna persistida son correctos y coherentes
entre sí. Está en el **recálculo de la apertura** de `fn_seguimiento_diario_engorde` (+ el gemelo de carga masiva).

Direcciones posibles, en orden de menor a mayor riesgo:

1. **Acotar la ventana al ciclo propio.** Que la apertura no retroceda más allá del último movimiento/seguimiento
   del ciclo anterior en ese galpón: `corte = MAX(fecha_encaset − N días, fin del ciclo anterior + 1)`. Conserva
   intacto el caso de Panamá (galpón sin ciclo previo dentro de la ventana ⇒ no-op) y anula los 26 negativos.
2. **Simetrizar el filtro de devoluciones.** Si se excluye la entrada `(devolución por eliminación)`, excluir
   también el traslado de salida que la compensa. Solo mueve la apertura de −7.960 a +1.040: **no alcanza** para
   dejar el testigo en 11.520.
3. **Que la grilla lea la columna persistida** en vez de recalcular. Es la que ya cuadra al kilo contra el stock,
   pero deja dos fórmulas conviviendo y no arregla la carga masiva.

⚠️ Antes de tocar: la opción 1 exige **regresión fila a fila de Panamá con 0 diferencias** (criterio de
aceptación vigente) y volver a correr los 1.341 tests. Y hay que decidir qué pasa con los **14 lotes donde la
grilla muestra de más** — no todos tienen la misma causa (el lote 20 arrastra +37.880 de una apertura positiva de
19.880 kg que también hay que auditar).
