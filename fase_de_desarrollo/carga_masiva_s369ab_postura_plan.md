# Plan — Archivos de carga masiva del lote S-369AB (postura: levante + producción + alimento)

**Fecha:** 2026-08-06
**Pedido:** a partir de los 3 informes de la granja MANGOS, construir los archivos `.xlsx` que consume el
módulo *Migraciones Masivas* para cargar el seguimiento diario completo del lote **S-369** (regional
Centro) en la granja de pruebas, de punta a punta (levante → producción) incluyendo el alimento.

> Esta tarea **no modifica código del repo**: produce datos (archivos de carga) y documenta el
> procedimiento operativo. El contrato del importador se tomó del código actual (regla «el código manda»).

---

## 1. Fuentes (`C:\Users\SAN MARINO\Documents\lote carga masiva pruebas`)

| Archivo | Contenido | Estado |
|---|---|---|
| `INFORME TECNICO LEVANTE S-369AB.xlsm` | 40 hojas: diario H/M por galpón y consolidado, registros semanales | **ZIP truncado** (sin *central directory*) — se reconstruyó entrada por entrada; solo se perdió `calcChain.xml` (caché de fórmulas) |
| `INFORME PRODUCCION S-369AB.xlsx` | `DIARIO A/B/GENERAL`, `SEMANAL *`, `GALPON 9..13`, `STANDARD` | OK |
| `CONSUMOS S369.xlsx` | 7 bloques de tipo de alimento × (ENTRADAS, consumo H, consumo M, SALDO, TRASLADO), 443 días | OK |

### 1.1 El lote son DOS sublotes

- **S-369A** — encaset **2025-08-30**, 10.167 H + 1.472 M (galpones 1, 2, 3A/4A)
- **S-369B** — encaset **2025-09-05**, 10.291 H + 1.521 M (galpones 1B, 3, 4)
- Juntos: **20.458 H + 2.993 M**

Las hojas `… general 369AB` consolidan **por EDAD (índice de día), no por fecha calendario**, y rotulan
con el calendario de B. Verificado: 0/175 discrepancias por índice contra 172/175 por fecha. Por eso el
consolidado correcto se arma **sumando A y B por fecha calendario**, no copiando la hoja *general*.

---

## 2. Conciliación de saldos (hecha antes de generar nada)

### Hembras — cierra exacto, sin ajustes
`20.458 − 669 mort − 614 error de sexaje − 157 descartes = 19.018` al **2026-02-19**, que es exactamente
con lo que arranca el informe de producción el 2026-02-20. Ninguna hoja tiene días inexplicados.

### Machos — 4 saltos no explicados por columnas
| Fecha | Hoja | Δ | Interpretación |
|---|---|---|---|
| 2026-02-07 | A −196 / B +196 | 0 | **traslado interno A→B**: se anula en el lote consolidado |
| 2026-02-09 | B | −150 | retiro real de machos |
| 2026-02-17 | B | −140 | retiro real de machos |
| 2026-02-23 | B | −20 | ya está en el informe de producción (`Entradas (+) M = −20`) |

`2.993 − 195 mort − 3 sexaje − 548 descartes − 290 retiros = 1.957` al 2026-02-19 = arranque de producción. ✔

### Producción — cierra exacto
`19.018 − 585 mort − 87 sel − 338 salidas = 18.008` · `1.957 − 64 − 129 − 130 = 1.634`. 0 días inexplicados.
Las 9 filas de «Entradas (+)» son todas **negativas** ⇒ son salidas de aves, no ingresos.

### Corte de fases
**Levante 2025-08-30 → 2026-02-19** (174 días) · **Producción 2026-02-20 → 2026-07-30** (161 días).
Las filas de levante posteriores al 2026-02-19 se descartan: están duplicadas en el informe de producción.

---

## 3. Contrato del importador (leído del código, no de la plantilla)

Fuente: `MigracionEsquemas.cs`, `MigracionService.Historicos.cs`, `.AlimentoEngorde.cs`,
`.AlimentoPostura.cs`, `.MovimientosAves.cs`, `.HuevosPostura.cs`, `.Comun.cs`.

**Reglas que condicionan el armado:**

1. Hoja de datos **`Datos`** obligatoria; `Alimento`, `Movimientos Aves`, `Movimientos Huevos` opcionales;
   nombres normalizados (si el nombre no matchea, la hoja se ignora **en silencio**).
2. Encabezados en **A1**, sin ninguna celda suelta arriba (se leen de `ws.Dimension.Start.Row`).
3. **Trampa principal:** cualquier *Advertencia* emitida dentro del bloque de una fila la **descarta en
   silencio** (`if (errores.Count > e0) continue;`), aunque el resultado global diga «Procesado».
4. ⇒ Si la fila trae `Alimento N H/M`, **`Consumo H/M (kg)` debe ir VACÍO** (si no, Advertencia ⇒ fila perdida).
5. ⇒ Si la fila trae las 11 categorías de huevo, **`Huevo Total` / `Huevo Incubable` van VACÍOS**
   (el sistema los deriva; un total que no cuadre exacto descarta la fila).
6. Una sola fila por fecha; ninguna fecha anterior a `lotes.fecha_encaset`; ninguna fecha futura.
7. Enteros puros en mortalidad/selección/sexaje/categorías de huevo. Decimales solo en consumos y pesos.
8. Hoja `Huevos`: **NO incluirla** — Sanmarino tiene `clasificacion_huevo_por_items = false` y la hoja
   con datos es un Error *fail-closed* que rechaza todo el archivo.
9. `Movimientos Aves` tipo **Salida** exige un lote contraparte existente en la misma fase ⇒ para los
   retiros sin contraparte se usa **Venta**.
10. Hoja `Alimento`: columna `Origen` **vacía** (con `granja`/`bodega` el ingreso falla siempre, porque
    el código nunca envía `OrigenFarmId` para un Ingreso). `Granja/Núcleo/Galpón` **vacías** ⇒ el
    movimiento va a la posición de stock del lote, que en Sanmarino es `(granja, null, null)`.
11. **Gate de stock:** el archivo se rechaza ENTERO si el consumo por ítems supera
    `stock actual + entradas del archivo`, por posición e ítem. Comparación total contra total (no cronológica).
12. Idempotencia: `Datos` por `(lote_id, fecha)`; `Alimento` por
    `Movimiento|Farm|Nucleo|Galpon|Item|fecha|kg(0.000)|Referencia` ⇒ dos ingresos iguales el mismo día
    necesitan `Referencia` distinta.
13. Tope 5.000 filas por hoja y 10 MB por request (174 y 161 filas: sobra).

---

## 4. Estado de la BD local (solo lectura)

- Empresa **Sanmarino = `companies.id 1`**: `maneja_alimento_por_galpon = false` (stock a nivel **granja**),
  `clasificacion_huevo_por_items = false`, `captura_huevos_en_levante = true`.
- Granja de pruebas viva: **`farms.id = 44` «Pruebas Moises»** (la 47 está *soft-deleted*).
  Única ubicación utilizable: **núcleo `883195` («Nt») + galpón `G0443` («galpon pruebas»)**.
- **No existe la raza «ROSS AP»** para Sanmarino. La guía genética de postura es
  `guia_genetica_sanmarino_colombia` con **raza `AP`**; año más completo **2026** (semanas 1..97).
- Regional **«Centro» = `master_list_options.id 57`**. La granja 44 tiene `regional_id = 27`, que es un id
  **huérfano** (no existe esa opción).
- La granja 44 tiene **0 filas** en `inventario_gestion_stock` ⇒ el alimento debe entrar por la hoja `Alimento`.
- No existe ningún lote `S-369`/`S369`: sin riesgo de duplicado.

### Mapeo de alimentos → catálogo (`item_inventario`, company 1)
Se usa el **código**, no el nombre: hay **tres ítems distintos con el nombre idéntico**
`PRODUCCION III REPRODUCTORA PESADA`.

| Bloque en `CONSUMOS` | Código | Nombre en catálogo |
|---|---|---|
| INICIACION | `000691` | POLLITA INICIACION REPRODUCTORA PES |
| LEVANTE | `000464` | POLLA LEVANTE REPRODUCTORA PESADA |
| PREPOSTURA | `001560` | PREPOSTURA REPRODUCTORA PESADA |
| PREPICO (FASE 1) | `026657` | PREPICO REPRODUCTORA PESADA MED H |
| MACHOS | `003401` | MACHOS REPRODUCTORES |
| PRODUCCION II (FASE 2) | `000490` | PRODUCCION II REPRODUCTORA PESADA |
| PRODUCCION III (FASE 3) | `006417` | PRODUCCION III REPRODUCTORA PESADA |

---

## 5. Reparto del alimento por sexo

Las columnas `H`/`M` de `CONSUMOS` **no son fiables por bloque** (en `PRODUCCION II` el consumo de hembras
está escrito bajo la columna `M`). La regla verificada contra las magnitudes diarias (g/ave) es:

- `INICIACION`, `LEVANTE` → reparten por sus propias columnas H y M (en levante ambos sexos comen lo mismo)
- `PREPOSTURA`, `PREPICO`, `PRODUCCION II`, `PRODUCCION III` → **100 % hembras**
- `MACHOS` → **100 % machos**

**Nunca hay más de 2 tipos de alimento por sexo en un día** (verificado 0/335 días) ⇒ entra en los 4 slots
de la plantilla.

El **kg diario por sexo es el del informe técnico** (autoritativo, es el que produce el g/ave de los
reportes); el desglose por tipo se **prorratea** con las proporciones de `CONSUMOS`. Así el total del
sistema coincide con el informe y la mezcla de alimentos coincide con la planilla de consumos.

---

## 6. Entregables

1. `Carga_Masiva_Levante_S-369AB.xlsx` — hojas `Datos` (174), `Alimento` (ingresos ≤ 2026-02-19),
   `Movimientos Aves` (2 retiros de machos), `Instrucciones`.
2. `Carga_Masiva_Produccion_S-369AB.xlsx` — hojas `Datos` (161), `Alimento` (ingresos ≥ 2026-02-20),
   `Movimientos Aves` (9 salidas), `Instrucciones`.
3. Ficha de creación del lote (valores exactos para el alta previa).

## 7. Secuencia operativa (el orden importa)

1. Crear el lote **S-369AB** en granja 44 / núcleo `883195` / galpón `G0443`, fase Levante,
   encaset **2025-08-30**, 20.458 H + 2.993 M, raza `AP`, año tabla `2026`.
2. Validar e importar **`Carga_Masiva_Levante_S-369AB.xlsx`**.
3. **Cerrar y liquidar el levante** por pantalla (crea `lote_postura_produccion`).
   Sin este paso el lote **no aparece** en el desplegable de Seguimiento Producción
   (elegibilidad = LPL `Cerrado` + liquidación + LPP vivo).
4. Validar e importar **`Carga_Masiva_Produccion_S-369AB.xlsx`**.

## 8. Casos de prueba / cuadre esperado

| Verificación | Esperado |
|---|---|
| Levante: aves al cierre | 19.018 H / 1.957 M |
| Producción: aves al 2026-07-30 | 18.008 H / 1.634 M |
| Huevos totales producidos | 2.213.852 |
| Saldo proyectado de alimento (advertencia del dry-run) | ≥ 0 en los 7 ítems |
| Reimport del mismo archivo | 0 procesadas, todo omitido |
