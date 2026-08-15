# Tickets pendientes de agosto 2026 — plan por caso

Cinco casos abiertos de **Agroavicola Sanmarino**, todos con imagen adjunta. Cada uno se resuelve y
se commitea **por separado**, con su propia migración de cierre del ticket (estado `SOLUCIONADO` +
`solucion_descripcion` para el usuario, sin correos), igual que TK-2026-000019.

| Ticket | Prioridad | Qué pide | Estado |
|---|---|---|---|
| TK-2026-000024 | MEDIA | Sacar los campos de **aves Mixtas** de reproductoras | T24 |
| TK-2026-000022 | MEDIA | Indicadores de levante: no dice si es **H o M**; sobra **Eficiencia** | T22 |
| TK-2026-000023 | MEDIA | Producción: consumos **duplicados**, sobran **Unif./CV**, fórmula de **dif. mortalidad** | T23 |
| TK-2026-000021 | ALTA | Levante: **totales H/M por separado**, falta **Unif./CV**, sobran **huevos** | T21 |
| TK-2026-000020 | ALTA | S369: la carga masiva llega a la **semana 24** y no deja cerrar el lote | T20 |

---

## T24 — TK-2026-000024 · Aves Mixtas no existen en reproductoras

**Pedido:** «Eliminar los campos de aves Mixtas, ya que es un concepto que se maneja en pollo de
engorde, no en reproductoras. Nunca se va a diligenciar y causa distracción y confusión al usuario.»
**Imagen:** el bloque `Cantidad hembras * / Cantidad machos * / Cantidad mixtas *` del formulario.

**Verificación previa (dump de producción):** `mixtas`/`cantidad_mixtas` distinto de 0 en
`lote_postura_levante` **0/22**, `lote_postura_base` **0/30**, `lotes` **0/22**,
`lote_postura_produccion` **0/6**. El usuario tiene razón: nunca se diligenció. Quitar el campo no
esconde ningún dato real.

**Alcance — SOLO postura/reproductoras. Engorde no se toca** (ahí «mixtas» es un concepto legítimo:
`movimientos-aves`, `aves-engorde`, `lesiones`).

1. `lote/components/lote-list` (formulario VIVO del **lote base**): sacar el input
   `Cantidad mixtas *`, la columna `Mixtas` de la tabla y la línea del detalle. El control sale del
   `FormGroup` (era `Validators.required`, o sea **obligaba a llenar un campo que no aplica**).
2. `lote-reproductora/pages/lote-reproductora-list`: sacar los dos inputs `Mixtas` (alta individual
   y alta masiva por incubadora) y las columnas/detalle de `Mixtas` y `Peso Mixto`.

**Regla que NO se rompe:** la columna sigue en la BD y en el contrato de la API. Al **editar** un
registro, el payload conserva el valor que ya tenía (`editingBase?.cantidadMixtas ?? 0`), no manda 0:
quitar un campo de la pantalla no puede pisar un dato histórico. Al **crear** va 0.

**Casos de prueba:** crear y editar un lote base sin el campo (el form ya no exige mixtas); alta
individual y masiva de lote reproductora; el valor previo de un registro con mixtas ≠ 0 sobrevive a
una edición (test unitario de la función de payload).

---

## T22 — TK-2026-000022 · Indicadores de levante no diferencian H/M · Eficiencia

**Pedido:** «En indicadores los parámetros planteados aparecen solo para un grupo de aves y no
identifica si se refieren a hembras o machos. Al final hay un parámetro de Eficiencia que no
manejamos, ¿de dónde se tomó?»
**Imagen:** tabla *Indicadores Semanales de Levante* con las columnas CONSUMO / GANANCIA / PESO /
UNIFORMIDAD / MORTALIDAD & SELECCIÓN y una última columna **EFICIENCIA** (0.81, 0.80, 0.65…).

Pendiente de análisis en el código: de dónde sale `EFICIENCIA` y qué sexo está mostrando cada bloque.

---

## T23 — TK-2026-000023 · Producción: duplicados, Unif./CV y dif. de mortalidad

**Pedido:** «aparecen los consumos de machos y hembras dos veces como kg diarios. Al final y en el
excel aparecen Uniformidad y CV que no se manejan en producción, solo en levante. En Indicadores hay
un error en el cálculo de diferencia de mortalidad respecto a guía, ya que es diferencia directa, no
porcentaje diferencial.»
**Imágenes:** el seguimiento con `CONS. H (KG) / CONS. M (KG)` y otra vez `CONS. ORIG H / CONS. ORIG M`;
y los indicadores con `DIF MORT H = -80.05 %`, `+2212.10 %` (porcentaje relativo, no la resta).

---

## T21 — TK-2026-000021 · Levante: totales por sexo, Unif./CV y huevos

**Pedido:** «Seguimiento levante no totaliza salidas de hembras y aparte machos, no aparece
uniformidad ni CV, debe separar saldo de aves en machos y hembras. Huevos se muestran en producción.
Igualmente debe reflejarse en el excel que genera.»
**Imagen:** el seguimiento diario de levante con `Saldo aves vivas` **sin desglose por sexo**, sin
columnas de uniformidad ni CV, y con `Huevos total / Huevos incubables` al final.

Relacionado con lo ya sabido: `cv_hembras` viene en **0 filas** de los lotes históricos porque la
plantilla de carga masiva no traía la columna hasta `d299a8a`, y **por pantalla el CV de levante
sigue sin poder cargarse**.

---

## T20 — TK-2026-000020 · S369 llega a la semana 24 y no cierra

**Pedido:** «La carga de información de levante del lote S369 con la plantilla para carga masiva
llega hasta la semana 24, con lo cual no se puede cerrar el lote y pasar a producción.»

**Estado real en el dump:** el lote S369A (galpón G0336, `lote_postura_levante` 34) tiene **168
registros** de levante (29/08/2025 → 12/02/2026 = 24 semanas exactas), `estado_cierre = 'Abierto'`,
`etapa = Produccion`, y **no existe ninguna fila en `lote_postura_produccion`** para S369. O sea: el
lote todavía no pasó a producción, tal como reporta el usuario.

Hipótesis a verificar en el código: la liquidación/cierre de levante trabaja contra la **semana 25**
(`LiquidacionCierreLoteLevanteService`: recorta seguimientos a `encaset + 175 días` y busca la fila
de guía genética de la semana 25), así que un lote que solo llegó al día 168 no encuentra su punto de
corte. Falta confirmar si el bloqueo es ese, si es la guía genética faltante, o si la plantilla cortó
la carga.
