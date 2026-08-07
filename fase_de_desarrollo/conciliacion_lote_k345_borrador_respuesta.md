# Borrador de respuesta — Conciliación lote K345 (NIZA III)

**Asunto:** RE: Conciliación lote K345 — validación desde la plataforma

---

Buen día,

Gracias por el ejercicio y por el nivel de detalle del cuadro. Desde el área de desarrollo
reconstruimos el lote día a día directamente sobre la base de datos de producción y les compartimos
los resultados.

Aclaración de partida: **K345 no es un lote, son dos** — K345A (9.131 aves, encasetado el 29-ene-2025)
y K345B (12.587 aves, encasetado el 01-feb-2025), ambos en NIZA III. Todas las cifras que siguen son
la suma de los dos.

## 1. Las cifras de la columna APLICACIÓN son las de la plataforma

Reprodujimos el cuadro contra la base: **19 de las 26 celdas coinciden exacto**, incluidos los
6 bimestres de alimento de producción (133.556,40 · 199.756,20 · 183.138 · 172.169 · 171.185 ·
31.809) y 5 de los 6 de mortalidad. El reporte no está perdiendo ni alterando información: devuelve
exactamente lo que se registró. Por eso el análisis de las diferencias hay que hacerlo sobre **el dato
registrado y el criterio de comparación**, y ahí encontramos lo siguiente.

## 2. La columna de mortalidad cambia de criterio a mitad del cuadro

La plataforma guarda **mortalidad** y **selección** por separado. En el cuadro:

- **Enero y febrero** se tomaron con **mortalidad sola**.
- **Abril, mayo, junio y julio** se tomaron con **mortalidad + selección**.

Al homogeneizar el criterio, dos de las diferencias reportadas desaparecen:

| Mes | Diferencia del cuadro | Con criterio homogéneo |
|---|---:|---:|
| Enero | −4 | **0** (41 vs 41) |
| Febrero | −39 | **−1** (323 vs 324) |

En todo el levante el lote registra 660 de mortalidad, 1.089 de selección y 76 de error de sexaje.
**Sugerimos fijar por escrito si «mortalidad» incluye selección antes de volver a comparar**: el
criterio por sí solo mueve la cifra en más de 1.000 aves.

## 3. La diferencia de septiembre-octubre no es una pérdida de información

| Bimestre | Aplicación | ERP | Diferencia |
|---|---:|---:|---:|
| jul-ago | 133.556,40 | 143.997,00 | −10.440,60 |
| sep-oct | 199.756,20 | 189.636,00 | +10.120,20 |
| **Suma** | **333.312,60** | **333.633,00** | **−320,40 (−0,10 %)** |

Las dos diferencias grandes **se compensan entre sí**: son las mismas ~10,3 toneladas, asignadas a un
bimestre en la aplicación y al otro en el ERP. Es un **desfase de corte de periodo**, no un dato
faltante, y se cierra solo en el acumulado.

## 4. Sí encontramos un defecto nuestro: el traslape de julio

El seguimiento de levante llega hasta la semana 25, pero el de producción arranca antes, con el primer
huevo. Resultado: **14 días de julio-2025 quedaron registrados en las dos etapas con el mismo
consumo** — 7 días en K345A (16 al 22-jul) y 7 en K345B (19 al 25-jul), **16.952 kg y 10 aves
duplicados**. Esto hace que julio no sea comparable tal como está y explica buena parte del descuadre
de ese mes. **Lo asumimos y lo corregimos**: el corte entre etapas quedará único y excluyente.

## 5. El +524 de mayo es el descarte final de machos

Corresponde a **un solo registro: K345B, 14-may-2026, 539 machos cargados como mortalidad**, un día
antes del cierre del lote. Es el descarte final del plantel, no mortalidad de operación. Descontándolo:

| Concepto | Aplicación | ERP | Diferencia |
|---|---:|---:|---:|
| Mortalidad producción | 1.492 | 1.508 | **−16 (−1,1 %)** |

Conviene definir con técnica si ese descarte se registra como mortalidad, venta o selección.

## 6. Foto del ciclo completo

| Concepto | Aplicación | ERP | Diferencia |
|---|---:|---:|---:|
| Alimento levante | 223.710,92 | 222.465,40 | +1.245,52 (+0,56 %) |
| Alimento producción | 891.613,60 | 896.514,05 | −4.900,45 (−0,55 %) |
| **Alimento ciclo** | **1.115.324,52** | **1.118.979,45** | **−3.654,93 (−0,33 %)** |
| Aves ciclo (criterio homogéneo) | 3.241 | 3.287 | −46 (−1,4 %) |

De los −4.900 kg de producción, **−4.534 están en el último bimestre**, cuando el lote se liquida el
15-may: alimento despachado por el ERP que la aplicación ya no alcanzó a registrar como consumido.

En resumen: **el ciclo cierra en −0,33 % en alimento y −1,4 % en aves.** Las diferencias mensuales
grandes son de asignación de periodo y de criterio, no de información perdida.

## 7. Un punto de método sobre este lote

La trazabilidad muestra que **el histórico de producción de K345 (602 días, jul-2025 a may-2026) se
cargó en bloque el 11-jul-2026**, después de cerrado el ciclo, y que en levante hubo 11 días que se
completaron en abril-2026. El levante sí se capturó día a día durante la operación.

Lo mencionamos porque cambia lo que mide el ejercicio: en producción la conciliación está midiendo la
calidad de una carga histórica, no la captura diaria de la plataforma. **Para validar la plataforma
sugerimos repetir el ejercicio sobre un lote capturado día a día**; este sirve muy bien para validar
la carga histórica, que es otra cosa.

## 8. Sobre los dos hallazgos de reportes: confirmados, entran a corrección

- **Hoja Resumen sin selección — confirmado.** El resumen consolida mortalidad, traslados y ventas,
  pero **no selección**; el dato ya existe y de hecho sale en las hojas semanales, solo falta subirlo
  al resumen. En este lote la selección pesa 1.089 aves en levante y 11.919 en producción, así que la
  omisión no es menor. Corrección acotada.
- **Movimiento de huevo en el reporte contable — confirmado con un matiz.** La información **sí
  existe** en el módulo: hay una pestaña «Movimientos de Huevos» con huevo fértil, comercial y de
  desecho. Lo que falta es que **se exporte al Excel**. En este lote el ciclo registra 3.632.634
  huevos, de los cuales 3.484.872 fértiles y 18.083 de desecho. Corrección acotada.

Ambas quedan en el plan de trabajo; les confirmamos fecha de entrega.

## 9. Lo que necesitamos de ustedes

1. **El archivo con el que armaron levante de marzo (27.720,30), mayo (44.962,60) y julio
   (39.008,02).** Son las 3 celdas que no reproducen contra la base y queremos cerrar el porqué.
2. **Definición del criterio de conciliación**: si «mortalidad» incluye selección y error de sexaje, y
   si se compara **consumo** (aplicación) contra **despacho** (ERP) — buena parte de las diferencias
   mensuales son desfase entre esos dos conceptos.
3. **Los registros técnicos de Verenice**, para conciliarlos campo por campo contra lo registrado.

Quedamos atentos y proponemos una reunión corta para cerrar los puntos 1 y 2, que son los que hoy
generan la mayor parte de las diferencias del cuadro.

Cordial saludo,
