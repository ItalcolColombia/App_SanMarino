# Barrido del cuadre de alimento de ItalcolPanama

> **Estado: ENSAYADO, no ejecutado.** Todo lo de acá se midió sobre la copia de producción local del
> 25-ago-2026 y se probó en **transacción revertida**. Nada se aplicó a producción.
>
> **Requisito previo:** que esté desplegado el «Cuadrar galpón»
> (`fase_de_desarrollo/ecuador_cuadre_alimento_y_permisos_plan.md`, F2). Sin eso, seis de estos
> galpones **no tienen arreglo posible desde ninguna pantalla**.

---

## 0. El número real

Lo que muestra la pantalla **no** es lo que devuelve la función:

| | Galpones | kg |
|---|---:|---:|
| `fn_cuadre_alimento_engorde(5)` (crudo) | 12 | 55.866,5 |
| **Lo que ve el usuario** (crudo + reservado) | **11** | **63.668,2** |

La diferencia es la **doble validación**: el cuadre le suma lo *reservado* —consumo ya descontado en
la tabla diaria que todavía no salió del inventario—. Consecuencias concretas:

- **G0479** desaparece de la lista: su −907,0 es exactamente su reserva de 907,0 ⇒ en pantalla cuadra.
- **G0463** sube de 913,3 a **8.306,9**; **G0492** pasa de −1.497,0 a **+2.812,0**.

---

## 1. Los 6 que se cierran desde la pantalla

Causa común: **alguien corrigió el inventario a mano y la tabla diaria nunca se enteró**
(`AjusteStock` / `EliminacionStock` se espejan como `INV_OTRO`, que la fn no lee en ninguna de sus
5 CTE). El inventario es el que manda; la tabla se alinea.

Acción: **Gestión de Inventario → Cuadre de alimento → Cuadrar**, declarando estos kilos.

| Galpón | Granja | Descuadre hoy | **Kilos a declarar** | Evidencia |
|---|---|---:|---:|---|
| **G0475** | Doña María | +18.650,4 | **2.566,0** | `EliminacionStock` de 18.650,356 kg (07-ago) — coincide **al kilo** |
| **G0483** | Doña María | +12.500,0 | **5.363,6** | `EliminacionStock` de 12.500,000 kg (01-ago) — coincide **exacto** |
| **G0496** | Trofarello | −3.629,0 | **12.442,9** | Ajuste de 24.877,9 → 10.846,9 (14-ago) |
| **G0491** | Trofarello | +2.758,0 | **3.366,5** | 3 ajustes dentro del ciclo (6.352,5 kg) |
| **G0476** | Doña María | +2.496,0 | **7.853,0** | Sin ajustes registrados — ver §3 |
| **G0477** | Doña María | +544,0 | **4.207,0** | `AjusteStock` de 544,0 kg — coincide **exacto** |

**Resultado ensayado:** los 6 quedan en `descuadre = 0,0`. Panamá pasa de **12 a 6** descuadrados y
de **55.866,5 a 15.289,1 kg** — se cierra el **73 %**.

> 🟢 **Ningún galpón gana días en rojo** (16 antes, 16 después) y no se toca ningún galpón fuera de
> los 6. Verificado fila a fila; el script está en
> `backend/sql/verificar_barrido_cuadre_panama.sql`.

---

## 2. Los que NO se tocan, y por qué

### 🔴 G0495 (Trofarello) — **cuadrarlo es imposible**

```
stock 178,3 kg   ·   movimientos posteriores 2.786,0 kg   ⇒   objetivo = −2.607,7 kg
```

Declarar que el stock tiene razón exige que la tabla cierre en **−2.607,7 kg**, que no existe
físicamente. El ensayo lo confirmó: el saldo quedaba negativo y el galpón **ganaba un día en rojo
que hoy no tiene**.

**Lo que está mal es el inventario, no la tabla:** entraron 2.786,0 kg después del último
seguimiento y el stock quedó en 178,3. Hay que averiguar dónde fue ese alimento antes de tocar nada.

### ⏸️ G0463 (+8.306,9) y G0492 (+2.812,0) — **no son kilos faltantes**

**7.393,7 y 4.309,0 kg** respectivamente son **reservas activas**: seguimientos cargados y sin
validar. Se cierran **validando esos seguimientos**, no ajustando el galpón. Ajustarlos escribiría
una corrección por kilos que sí existen.

### ⏸️ G0460 (−6.667,2) — **problema de FECHA, no de kilos**

Tres ingresos de julio (`LLEG-17`, `LLEG-18`, `LLEG-19`, **18.018,2 kg**) están fechados **antes** de
que arrancara el ciclo (12-ago). El alimento existe y está bien registrado; la tabla del ciclo no lo
cuenta porque cae fuera de su ventana. Decisión de operación: re-fechar esas llegadas o aceptarlas
como apertura.

### ⏸️ G0461 (+317,5) — **prematuro**

Ciclo del 16 al 22-ago, con **5.119,8 de 9.796,7 kg** (más de la mitad del stock) entrados después
del último seguimiento. Cuando carguen los días siguientes, el `mov_post` se absorbe solo. Cuadrarlo
ahora sería corregir algo que se corrige a sí mismo.

---

## 3. Lo que NO pude cerrar, dicho como tal

**La aritmética de la causa no cierra en 5 de los 6 del barrido.** Reconstruí el delta con signo de
cada ajuste manual parseando su motivo (`Anterior: X → Nuevo: Y`), y solo **G0477** cuadra exacto:

| Galpón | Descuadre | Σ ajustes con signo | Resto sin explicar |
|---|---:|---:|---:|
| G0477 | +544,0 | −544,0 | **0,0** ✅ |
| G0483 | +12.500,0 | −24.500,0 | −12.000,0 |
| G0475 | +18.650,4 | −25.862,5 | −7.212,2 |
| G0496 | −3.629,0 | −20.109,2 | −23.738,2 |
| G0491 | +2.758,0 | −5.987,5 | −3.229,5 |
| G0476 | +2.496,0 | 0,0 | +2.496,0 |

Los ajustes suman **más** que el descuadre porque parte ya fue absorbida por la apertura del ciclo o
por el corte del ciclo anterior. **Que la causa sea clara no es lo mismo que tenerla atribuida al
kilo**, y en G0476 directamente no hay ajustes registrados.

🔴 **Por eso esto necesita una confirmación humana antes de ejecutarse en producción**, sobre todo
para G0475 y G0483 (**31.150 kg entre los dos**). La pregunta para costos/operación es una sola y no
la puede contestar el código:

> **¿El inventario de esos galpones es confiable hoy?**

Si la respuesta es sí —alguien borró ese stock a propósito porque el alimento no estaba—, el barrido
es correcto y son seis clics. Si es no, el arreglo va para el otro lado y hay que reponer stock.

---

## 4. Cómo ejecutarlo (día del deploy)

1. Confirmar con costos/operación la pregunta de §3.
2. Verificar la línea base: `psql ... -f backend/sql/verificar_cuadre_alimento_engorde.sql`.
3. Para cada galpón de §1: **Cuadre de alimento → Cuadrar**, declarar los kilos de la tabla y poner
   un motivo que nombre la evidencia (ej. *«eliminación de stock del 07-ago de 18.650,356 kg»*).
   El motivo queda en la auditoría del movimiento.
4. Verificar: Panamá debe quedar en **6 descuadrados / 15.289,1 kg** y **16 galpones con días en
   rojo** — el mismo número que antes. **Si aparece un galpón nuevo con días en rojo, parar.**
5. Abrir el pendiente de G0495 (§2), que es el único que señala un problema real de inventario.

⚠️ Quien ejecute necesita `cuadrar_ingresos_traslados_seguimiento` **y** haber vuelto a iniciar
sesión después del deploy.
