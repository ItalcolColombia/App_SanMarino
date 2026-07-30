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

---

# Parte 2 — Validación de cierre lote por lote, ciclo por ciclo, galpón por galpón

**Fecha:** 2026-07-29 · **Alcance:** los 103 lotes de engorde de ItalcolEcuador con seguimiento,
repartidos en **35 galpones** y **4 corridas** (2601=ciclo 1 … 2604=ciclo 4). Sigue sin tocarse nada.

## 1. Método

La bodega de alimento vive en el **galpón** y es continua entre ciclos, así que se validó en tres niveles,
cada uno anclado en un hecho duro distinto:

| Nivel | Prueba | Ancla |
|---|---|---|
| Galpón (toda su historia) | `ingresos + entradas − salidas − consumo == stock de inventario` | stock físico |
| Traspaso entre ciclos | `apertura(i+1) == cierre(i) + movimientos del galpón en el hueco` | fechas, sin atribución |
| Ciclo activo | `saldo de la última fila == stock de hoy − movimientos posteriores al último seguimiento` | stock físico |

Filtros espejo de la fn en los tres (`'Seguimiento aves engorde #%'` **y** `'%devolución por eliminación%'`).

> ⚠️ **`lote_ave_engorde_id` del histórico NO sirve como clave de ciclo.** Es internamente consistente
> (0 movimientos apuntando a un lote de otro galpón) pero hay movimientos **atribuidos a un lote y
> fechados dentro del ciclo siguiente**: 10 ciclos de la corrida 1 (469.760 kg, CAROLINA y SAN GUILLERMO)
> y 4 de la corrida 2 (420.705 kg, Kilometro 61, Sacachun 2, Sacachun 3b) no tienen ingresos propios
> porque su alimento quedó cargado contra el ciclo vecino. La app usa **galpón + fecha**, así que esto
> no la afecta; pero cualquier auditoría que agrupe por `lote_ave_engorde_id` va a medir mal.

## 2. Resultado — cierre del galpón (toda su historia)

**29 de 35 galpones cierran EXACTO (0,0 kg) contra el stock de inventario.** Los 6 con descuadre:

| Granja | Galpón | Saldo lógico | Stock | Descuadre |
|---|---|---:|---:|---:|
| Kilometro 22 | G0035 | 52.230 | 14.350 | **+37.880** |
| Kilometro 86 | G0040 | −320 | 8.700 | **−9.020** |
| CAROLINA | G0060 | 3.680 | 0 | +3.680 |
| CAROLINA | G0057 | 3.240 | 0 | +3.240 |
| Kilometro 86 | G0039 | 7.420 | 5.820 | +1.600 |
| Sacachun 2 | G0051 | −580 | 0 | −580 |

Total **36.799 kg** — muy lejos de los ~490.000 kg que sugería el requerimiento a nivel granja. La
diferencia es que aquel número incluía la **bodega de granja** (ingresos sin galpón), que no pertenece
al libro mayor de ningún galpón. **El inventario por galpón de Ecuador está mucho más sano de lo que se
creía.**

## 3. Resultado — traspaso entre ciclos consecutivos

**68 traspasos · 54 cuadran · 14 no** (61.139 kg). Por corrida que abre:

| Abre corrida | Traspasos | Cuadran | Descuadran | Kg |
|---|---:|---:|---:|---:|
| 2602 | 33 | 25 | 8 | 34.419 |
| 2603 | 29 | 24 | 5 | 25.500 |
| 2604 | 6 | 5 | 1 | 1.220 |

Los descuadres grandes son del traspaso **2601→2602** (Sacachun 2 G0051 +6.580, G0055 +6.000,
Sacachun 3b G0050 +5.000, Kilometro 61 G0037 +4.800, Sacachun 3b G0049 +4.000) y corresponden a la
carga retroactiva del §1, no a la operación de hoy.

## 4. Resultado — CICLO ACTIVO (lo que la operación ve hoy) ⭐

Descontando los movimientos posteriores al último seguimiento —que la grilla no puede mostrar y **no son
un error**—, los 35 galpones quedan así:

| Veredicto | Galpones | Kg error grilla |
|---|---:|---:|
| ✅ **OK** (dato guardado y grilla cuadran con el stock) | **25** | 0 |
| 🔴 **Solo la GRILLA mal** — bug de la ventana v9 | **7** | 28.330 |
| 🔴 **AMBOS mal** — descuadre real de datos | **2** | 10.840 |
| ⚠️ Solo el dato guardado | 1 | 0 |

### 4.1 Los 7 del bug de la ventana (el dato guardado está bien, se arregla con código)

| Granja | Galpón | Corrida | Stock | Guardado | Grilla | Error grilla |
|---|---|---|---:|---:|---:|---:|
| Kilometro 22 | G0036 | 2603 | 11.380 | 11.380 ✅ | 3.420 | **−7.960** |
| Kilometro 86 | G0039 | 2603 | 5.820 | 5.820 ✅ | −1.590 | **−7.410** |
| Kilometro 61 | G0038 | 2604 | 12.760 | 12.760 ✅ | 16.960 | **+4.200** |
| Sacachun 3b | G0048 | 2604 | 13.960 | 3.960 ✅ | 800 | **−3.160** |
| Sacachun 2 | G0051 | 2603 | 0 | 720 ✅ | 3.360 | **+2.640** |
| Sacachun 3b | G0047 | 2604 | 14.030 | 5.560 ✅ | 3.200 | **−2.360** |
| Sacachun 2 | G0052 | 2603 | 0 | 560 ✅ | 1.160 | **+600** |

### 4.2 Los 3 con error PERSISTENTE de datos (hay que corregir la BD, no el código)

| Granja | Galpón | Corrida | Stock | Esperado | Guardado | Grilla | Err. guardado | Err. grilla |
|---|---|---|---:|---:|---:|---:|---:|---:|
| Kilometro 61 | G0037 | 2604 | 12.360 | 12.360 | 2.360 | 7.320 | **−10.000** | −5.040 |
| Kilometro 86 | G0040 | 2603 | 8.700 | 8.700 | 6.300 | 2.900 | **−2.400** | −5.800 |
| CAROLINA | G0058 | 2603 | 0 | 0 | 480 | 0 | **+480** | 0 |

**Kilometro 61 G0037** es el más claro: los 10.000 kg del documento 63020 (28-jul, `SM0178`) entraron el
mismo día del último seguimiento y **el saldo guardado nunca los recogió**.

## 5. Hallazgo nuevo — el dato guardado se queda VIEJO

`RecalcularSaldoAlimentoPorLoteAsync` solo corre al **crear o editar un seguimiento**. Un ingreso
registrado *después* del último día cargado no actualiza `saldo_alimento_kg`. Casos vivos:
Sacachun 3b **G0047** (8.470 kg el 29-jul) y **G0048** (10.000 kg el 29-jul), ambos con último
seguimiento el 28-jul.

⇒ **Las dos fuentes están rotas por lados opuestos, y por eso ninguna sirve sola:**

| Fuente | Fortaleza | Debilidad |
|---|---|---|
| Columna persistida | Sin apertura fantasma (usa el corte viejo) | Se congela: no ve el alimento que entra después del último seguimiento |
| Grilla (`fn`, recalcula) | Siempre al día | Arrastra la apertura fantasma de la ventana v9 |

Esto **descarta la opción 3** de la Parte 1 («que la grilla lea la columna persistida»): dejaría la
pantalla congelada. La corrección tiene que ir por **acotar la ventana al ciclo propio** (opción 1),
que conserva el recálculo en vivo y elimina la apertura fantasma.

## 6. Sobre la hipótesis de Costos — confirmada

> *«costos validó los reportes y predijo que está correcto los lotes de la corrida 01 y 02, y esto de la
> corrida 3 o 4 tiene esta falla»*

**Correcto, y se puede afirmar con números.** Los 10 galpones con problema en el ciclo activo son:

- **corrida 2603:** Kilometro 22 G0036 · Kilometro 86 G0039 · Kilometro 86 G0040 · Sacachun 2 G0051 · Sacachun 2 G0052 · CAROLINA G0058 → **6**
- **corrida 2604:** Kilometro 61 G0037 · Kilometro 61 G0038 · Sacachun 3b G0047 · Sacachun 3b G0048 → **4**
- **corridas 2601 y 2602: CERO.**

La razón es estructural, no casual: para que la ventana de 10 días alcance a la limpieza del ciclo
anterior **tiene que existir un ciclo anterior en ese galpón**. La corrida 1 no lo tiene, y la corrida 2
en su mayoría heredó galpones que quedaron vacíos sin traslados en la ventana. **El bug solo puede
manifestarse desde el tercer ciclo en adelante** — exactamente lo que reportó Costos.

## 7. Qué queda por decidir

- [ ] Corregir la **ventana** (opción 1) → arregla los 7 galpones de §4.1 sin tocar datos
- [ ] Corregir los **3 descuadres de datos** de §4.2 (Kilometro 61 G0037, Kilometro 86 G0040, CAROLINA G0058)
- [ ] Decidir si el **saldo persistido debe recalcularse al registrar un movimiento de inventario** (§5)
- [ ] Los 6 galpones con descuadre histórico (§2) **no afectan lo que ve la operación hoy** — Kilometro 22
      G0035 arrastra +37.880 en su historia pero su ciclo activo cuadra exacto. Decidir si se sanean o se dejan
