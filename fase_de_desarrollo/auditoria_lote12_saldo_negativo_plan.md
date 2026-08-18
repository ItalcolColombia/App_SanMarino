# Auditoría del saldo negativo del lote 12 (KM 86 / G0040) — y los otros 7 que cierran igual

**Origen:** el pendiente *«El lote 12 arrastra apertura negativa (−9.020 kg): auditoría de datos
aparte»* del bloque «Lote cerrado que absorbe el ciclo siguiente (KM 86)» (07-ago-2026).
**Fecha:** 2026-08-17 · **Naturaleza: SOLO LECTURA.** No se corrige ni un dato.

---

## 1. Qué es realmente el −9.020

No es una **apertura** negativa: es el **saldo con el que TERMINA la serie** del lote 12, y de sus 63
días **21 cierran en rojo**. Su apertura es 0,0 (la fn no le pasó nada del ciclo anterior).

La aritmética cierra exacta contra la propia serie:

| concepto | kg |
|---|---|
| Ingresos en la ventana del lote | 123.940,0 |
| + Traslado de entrada | 4.000,0 |
| − Traslado de salida | 1.000,0 |
| **Entradas netas** | **126.940,0** |
| **Consumo declarado por los seguimientos** | **135.960,0** |
| **Diferencia** | **−9.020,0** |

**Causa: los ingresos de ese galpón son una RECONSTRUCCIÓN.** Los 19 `INV_INGRESO` del período llevan
la referencia *«Cuadre saldos Excel — Insertar ingreso d…»*: el historial de alimento se rearmó desde
una planilla, y esa reconstrucción quedó **9.020 kg corta** frente al consumo que los seguimientos
declaran. Es un problema de **datos**, no de fórmula: la serie es coherente con lo que tiene cargado.

**Qué haría falta para corregirlo:** las remisiones físicas del galpón entre feb y abr-2026. Sin ellas
cualquier ingreso que uno invente cuadra igual de bien — la misma conclusión a la que llegó V17 con los
lotes 161 y 142 de Panamá.

---

## 2. La buena noticia: NO se contagia al ciclo siguiente

El lote **73** (el ciclo que sigue en G0040, encaset 24-abr) abre con **apertura vacía y saldo
+5.280,0**. Las guardas de v11/v12 (`corte_apertura`) y el corte de v14 hacen su trabajo: el rojo del
lote 12 se queda en el lote 12. Por eso esto es una auditoría y no una urgencia.

---

## 3. El caso no es único: 8 lotes cierran su serie en negativo

| empresa · estado | lotes | **cierran negativo** | kg | peor |
|---|---|---|---|---|
| ItalcolEcuador · Abierto | 28 | **1** (el lote 12) | −9.020,0 | −9.020,0 |
| ItalcolEcuador · Cerrado | 90 | **4** | −7.741,0 | −3.920,0 |
| ItalcolPanama · Abierto | 41 | **3** | −7.392,8 | −4.446,0 |

Los 3 de Panamá ya tienen diagnóstico propio (V17: patrón B, fechas de una carga histórica). Los 4 de
Ecuador están **liquidados y congelados**: lotes 16 (−3.920), 7 (−3.220), 15 (−600) y 14 (−1,0).

---

## 4. 🔑 Lo que parecía una contradicción en la foto congelada y NO lo es

De esos 4 congelados, tres tienen una cabecera que **no coincide con su propio detalle**:

| lote | cabecera congelada | última fila de su serie | por qué |
|---|---|---|---|
| 15 | **+14.000,0** (13-may) | **−600,0** (16-may) | la cabecera es el saldo del **último día con SEGUIMIENTO**; la serie sigue tres días más con filas **solo-movimiento** |
| 7 | **+3.180,0** (14-may) | **−3.220,0** (15-may) | ídem |
| 14 | −1,0 | −1,0 | coinciden: no hubo movimientos posteriores |

**No es un defecto: es la misma convención del cuadre.** `LiquidacionCongeladaAplicador` toma el saldo
del último día de `seguimiento_diario_aves_engorde`, y `fn_cuadre_alimento_engorde` toma
explícitamente el saldo en `seg_max` y NO el de la última fila —su comentario lo dice: contarlo de las
dos formas duplicaría los movimientos posteriores—. El reporte de «liquidados con alimento sin
trasladar» (V16) ya resta esas salidas posteriores por separado, así que **lee bien** los 14.000 del
lote 15.

Queda escrito acá justamente para que nadie lo «arregle»: alinear la cabecera con la última fila
rompería el reporte de V16 y el cuadre a la vez.

---

## 5. Qué hacer con esto (nada, sin una decisión)

| opción | qué implica |
|---|---|
| **Dejarlo** | La serie del lote 12 sigue mostrando 21 días en rojo. No contagia al ciclo siguiente ni al inventario (Ecuador está en 0 descuadrados) |
| **Completar la reconstrucción** | Cargar los 9.020 kg que faltan **con su fecha real**, desde las remisiones físicas. Es la única corrección legítima, y necesita el papel |
| **Liquidar el lote 12 como está** | Congelaría −9.020 para siempre (ver V18: la foto no se reescribe). **No hacerlo antes de decidir** |

⚠️ Los lotes 2 y 12 siguen en estado `Abierto` desde abr-2026 — ése es el otro pendiente del mismo
bloque, y para el 12 **conviene resolver esto primero**.

---

## 6. Fuera de alcance, dicho

- **No se corrige ningún dato**: ni los 9.020 kg del lote 12, ni los 4 congelados de Ecuador, ni los 3
  de Panamá (esos ya los cubre V17).
- **No se toca ninguna fn ni el aplicador de la liquidación** (§4 explica por qué la convención actual
  es la correcta).
- No se cierra el lote 12 ni el 2: liquidar es una transacción de 5 pasos por pantalla y, para el 12,
  congelaría el negativo.
