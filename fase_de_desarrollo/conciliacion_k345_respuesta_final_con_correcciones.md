# Respuesta final — Conciliación lote K345 (NIZA III), con la aplicación ya corregida

**Asunto sugerido:** RE: Conciliación lote K345 — análisis, correcciones aplicadas y nueva validación

---

Buen día,

Gracias por el ejercicio y por el nivel de detalle del cuadro. Desde el área de desarrollo
reconstruimos el lote día a día sobre la base de datos, revisamos punto por punto lo que reportaron y
**ya dejamos aplicadas las correcciones**. Les compartimos el resultado y lo que necesitamos de
ustedes para cerrar.

Aclaración de partida: **K345 no es un lote, son dos** — K345A (9.131 aves, encasetado el 29-ene-2025)
y K345B (12.587 aves, encasetado el 01-feb-2025), ambos en NIZA III. Todas las cifras que siguen son
la suma de los dos.

---

## 1. Lo primero: las cifras de la columna APLICACIÓN son las de la plataforma

Reprodujimos el cuadro contra la base de datos: **19 de las 26 celdas coinciden exacto**, incluidos
los 6 bimestres de alimento de producción (133.556,40 · 199.756,20 · 183.138 · 172.169 · 171.185 ·
31.809) y 5 de los 6 de mortalidad.

El reporte no está perdiendo ni alterando información: devuelve exactamente lo que se registró. Por
eso las diferencias hay que buscarlas en **el dato registrado y en el criterio de comparación**, y
ahí encontramos lo siguiente.

## 2. La columna de mortalidad cambia de criterio a mitad del cuadro

La plataforma guarda **mortalidad** y **selección** en campos separados. En el cuadro:

- **Enero y febrero** se tomaron con **mortalidad sola**.
- **Abril, mayo, junio y julio** se tomaron con **mortalidad + selección**.

Al homogeneizar el criterio, dos de las diferencias reportadas desaparecen:

| Mes | Diferencia del cuadro | Con criterio homogéneo |
|---|---:|---:|
| Enero | −4 | **0** (41 vs 41) |
| Febrero | −39 | **−1** (323 vs 324) |

En todo el levante el lote registra **660 de mortalidad, 1.089 de selección y 76 de error de sexaje**.
Sugerimos fijar por escrito si «mortalidad» incluye selección antes de volver a comparar: el criterio
por sí solo mueve la cifra en más de mil aves.

## 3. La diferencia de septiembre-octubre no es una pérdida de información

| Bimestre | Aplicación | ERP | Diferencia |
|---|---:|---:|---:|
| jul-ago | 133.556,40 | 143.997,00 | −10.440,60 |
| sep-oct | 199.756,20 | 189.636,00 | +10.120,20 |
| **Suma** | **333.312,60** | **333.633,00** | **−320,40 (−0,10 %)** |

Las dos diferencias grandes **se compensan entre sí**: son las mismas ~10,3 toneladas, asignadas a un
bimestre en la aplicación y al otro en el ERP. Es un **desfase de corte de periodo**, no un dato
faltante, y se cierra solo en el acumulado.

## 4. El +524 de mayo es el descarte final de machos

Corresponde a **un solo registro: K345B, 14-may-2026, 539 machos cargados como mortalidad**, un día
antes del cierre del lote. Es el descarte final del plantel, no mortalidad de operación.
Descontándolo, producción cierra en **1.492 vs 1.508 = −16 aves (−1,1 %)**.

Conviene definir con técnica si ese descarte se registra como mortalidad, venta o selección.

## 5. Foto del ciclo completo

| Concepto | Aplicación | ERP | Diferencia |
|---|---:|---:|---:|
| Alimento levante | 223.710,92 | 222.465,40 | +1.245,52 (+0,56 %) |
| Alimento producción | 891.613,60 | 896.514,05 | −4.900,45 (−0,55 %) |
| **Alimento ciclo** | **1.115.324,52** | **1.118.979,45** | **−3.654,93 (−0,33 %)** |
| Aves ciclo (criterio homogéneo) | 3.241 | 3.287 | −46 (−1,4 %) |

De los −4.900 kg de producción, **−4.534 están en el último bimestre**, cuando el lote se liquida el
15-may: alimento despachado por el ERP que la aplicación ya no alcanzó a registrar como consumido.

**El ciclo cierra en −0,33 % en alimento y −1,4 % en aves.** Las diferencias mensuales grandes son de
asignación de periodo y de criterio, no de información perdida.

---

## 6. QUÉ CORREGIMOS EN LA APLICACIÓN

Los dos puntos que reportaron quedaron corregidos, y aprovechamos para cerrar dos más que salieron
del análisis. **Todo está desplegable y validado**; cuando entren van a ver la aplicación distinta.

### 6.1 Hoja «Resumen» del informe contable — CORREGIDO

Tenían razón: el resumen consolidaba mortalidad, traslados y ventas pero **no la selección**, aunque
el dato ya salía en las hojas semanales. **Ya aparece la columna Selección**, ubicada justo después
de Mortalidad, con su total.

No es un detalle menor en este lote: la selección pesa **1.089 aves en levante y 11.919 en
producción**. El resumen y el detalle ahora cuadran.

> **Cómo validarlo:** Reporte Contable → exportar Excel de un lote de producción → hoja **RESUMEN**.
> Deben ver 12 columnas y la de **Selección** con valores distintos de cero.

### 6.2 Movimiento de huevo en el reporte contable — CORREGIDO

Con un matiz que vale la pena aclarar: la información **ya existía** en el módulo (hay una pestaña
«Movimientos de Huevos» con huevo fértil, comercial y de desecho). Lo que faltaba era que **se
exportara al Excel**. Ya se exporta, en una hoja nueva **«MOVIMIENTOS HUEVOS»**, con las mismas
columnas que ven en pantalla y su fila de totales.

En este lote el ciclo registra **3.632.634 huevos**, de los cuales **3.484.872 fértiles** y **18.083
de desecho**.

> **Cómo validarlo:** mismo Excel, hoja **MOVIMIENTOS HUEVOS** (segunda pestaña del libro).

### 6.3 Doble conteo entre levante y producción — CORREGIDO (y era nuestro)

Esto lo encontramos nosotros y lo asumimos. El seguimiento de levante llega hasta la semana 25, pero
el de producción arranca antes, con el primer huevo. Resultado: **15 días de julio-2025 de este lote
quedaron registrados en las dos etapas con el mismo consumo — 16.952 kg y 10 aves duplicados**. Es lo
que hace que julio no sea comparable tal como está.

**La aplicación ya no lo permite:** un mismo día no puede registrar consumo ni bajas en levante y en
producción a la vez, y si se intenta, el sistema lo explica y no lo guarda.

Revisamos toda la base: **el traslape existe únicamente en este lote**; los demás están limpios.

### 6.4 Coeficiente de variación en levante — CORREGIDO

Al validar contra los informes técnicos apareció que la columna **C.V.%** del reporte semanal de
levante salía vacía en los lotes cargados por Excel: la plantilla de carga masiva no tenía esa
columna (el registro por pantalla sí la tenía). **Ya la tiene**, junto con consumo de agua, pH, ORP,
temperatura y observaciones de pesaje.

---

## 7. Sobre los registros técnicos de Verenice

Aprovechamos sus informes del lote **S-369AB** para una validación independiente: comparamos **día a
día, por sublote y por métrica**, el informe contra lo que tiene la aplicación.

**2.800 celdas comparadas · 1 sola diferencia, de 0,20 kg.**

Es decir: sobre lo que la plataforma captura, **el dato del informe técnico llega íntegro**.

Sí encontramos una diferencia de **criterio**: el informe llama «levante» a 175 días (25 semanas) y en
la aplicación ese lote cerró levante en 168 (24 semanas). Los 7 días de diferencia están guardados,
pero del lado de producción. **Mueve ~17.332 kg entre etapas** en una conciliación que compare
«levante contra levante», aunque en el acumulado del ciclo no cambia nada.

---

## 8. Lo que necesitamos de ustedes para cerrar

1. **El archivo con el que armaron levante de marzo (27.720,30), mayo (44.962,60) y julio
   (39.008,02).** Son las 3 celdas que no reproducen contra la base y queremos cerrar el porqué.
2. **La definición del criterio de conciliación**: si «mortalidad» incluye selección y error de
   sexaje, y si se compara **consumo** (aplicación) contra **despacho** (ERP). Buena parte de las
   diferencias mensuales son desfase entre esos dos conceptos.
3. **La definición del corte de etapa**: si levante son 24 o 25 semanas, para cerrar todos los lotes
   con el mismo criterio.
4. **Una nueva validación de su lado** con los reportes ya corregidos (puntos 6.1 y 6.2), para
   confirmar que la hoja Resumen y el movimiento de huevo les sirven como los necesitan.

Proponemos una reunión corta para cerrar los puntos 2 y 3, que son los que hoy generan la mayor parte
de las diferencias del cuadro.

Quedamos atentos.

Cordial saludo,
