# Reporte Diario Costos Postura — el levante nunca salía + el lote base tiene que poder vivir en varias granjas

**Fecha:** 2026-08-07
**Módulo:** `Reporte Diario Área de Costos — Postura` (`/reporte-diario-costos-postura`)
**Antecedente:** [`reporte_diario_costos_postura_plan.md`](reporte_diario_costos_postura_plan.md) (v1, commit `3469004`)

---

## 1. Pedido del usuario

1. **El reporte no trae nada** para lotes con levante+producción, solo levante o solo producción
   (casos citados: granja **NIZA III** y la granja de pruebas de carga masiva).
2. Un **lote base puede quedar repartido en varias granjas**: en NIZA el levante se hizo en
   **NIZA III** y la fase de producción va a pasar a **NIZA I**. Filtrando por lote base y «todas las
   fases», el reporte debería **seguir al lote base a la granja donde realmente ocurrió cada fase** y
   **decir en qué granja pasó cada una**, para poder validar el ciclo completo desde Costos.

---

## 2. Diagnóstico (verificado contra el dump de producción, BD local `:5433`)

### 2.1 🔴 Causa raíz: la fn keyea el levante por una columna que en producción está vacía

`fn_reporte_diario_costos_postura`, CTE `lev_dedup`
([`backend/sql/fn_reporte_diario_costos_postura.sql:157-176`](../backend/sql/fn_reporte_diario_costos_postura.sql)):

```sql
FROM seguimiento_diario_levante s
WHERE s.lote_id_int IS NOT NULL          -- ← mata TODAS las filas
```

En producción **las 588 filas de `seguimiento_diario_levante` tienen `lote_id_int` NULL** (100 %).
La clave viva es `lote_id` (varchar, siempre numérica).

| Consulta | Resultado |
|---|---|
| `select count(*), count(*) filter (where lote_id_int is null) from seguimiento_diario_levante` | `588 / 588` |
| `grep -rn "LoteIdInt" backend/src` | **0 coincidencias** — ninguna línea de C# escribe esa columna |
| Único escritor | `fn_migracion_seguimiento_levante` (carga masiva), solo en su `INSERT` de filas nuevas |

**Consecuencia medida:**

| Filtro (empresa 1, Sanmarino) | Filas hoy | Filas Levante |
|---|---|---|
| NIZA III, ambas fases | 602 | **0** |
| NIZA III, fase = Levante | **0** | 0 |
| Toda la empresa, ambas fases | 602 | **0** |

Sanmarino tiene exactamente 6 lotes de postura:

| lote | nombre | granja | base | días levante | días producción |
|---|---|---|---|---|---|
| 13 | K345A | 5 NIZA III | 1 K345 | 176 | 301 |
| 14 | K345B | 5 NIZA III | 1 K345 | 175 | 301 |
| 116 | A374A | 20 LA ESMERALDA | 2 A374 | 144 | 0 |
| 114 | A374A | 20 LA ESMERALDA | 2 A374 | 38 | 0 |
| 115/117 | A374B | 20 LA ESMERALDA | 2 A374 | 0 | 0 |

⇒ **K345 salía mutilado** (solo producción) y **A374 salía vacío** (solo tiene levante). Es exactamente
lo que reporta el usuario.

Keyeando por `lote_id::text` se recuperan los **533 días** de levante de Sanmarino (y 42 de Demo).

> **Por qué pasó desapercibido en la validación de v1:** el lote de pruebas **S-369** se cargó en la BD
> local **por carga masiva**, y esa fn *sí* setea `lote_id_int` en sus `INSERT`. Los 15/15 testigos
> daban exacto sobre un dato que en producción no existe con esa forma.

### 2.2 🔴 Al arreglar el levante aparece el doble conteo del traslape

K345 tiene **15 días de julio-2025 registrados en las dos etapas** (8 en el lote 13, 7 en el 14), los dos
lados con consumo y bajas — el hallazgo que cerró el commit `3347fbf`. Ese commit puso el guard para
**altas nuevas** (`CorteEtapaPosturaCalculos` + `SeguimientoLoteLevanteService` + `ProduccionService`),
pero **las 15 filas siguen en la BD**. Con el levante arreglado y sin blindaje, el reporte sumaría dos
veces ~16.952 kg de alimento y 10 aves.

La regla ya tiene dueño y está testeada:
[`CorteEtapaPosturaCalculos.HayDobleConteo`](../backend/src/ZooSanMarino.Application/Calculos/CorteEtapaPosturaCalculos.cs) —
*hay doble conteo cuando las dos filas del mismo día aportan consumo o bajas; las filas de solo huevos
(arrastre legítimo), de solo pesaje o vacías no chocan*. El reporte **la aplica, no la reimplementa**.

### 2.3 🔴 La granja del reporte es la ACTUAL del lote, no la del día

Trasladar un lote a otra granja **NO crea un lote nuevo: reescribe `lotes.granja_id` del mismo lote**
(`LoteService.TrasladarLoteAsync:1157` y `fn_mover_lote`), y un trigger propaga el cambio a los espejos
de fase. Como la fn lee `l.granja_id`, **todo el histórico se re-atribuye a la granja nueva**.

Verificado en transacción revertida sobre el dump: moviendo K345 a NIZA I, la llamada con
`ARRAY[5]` (NIZA III) pasa de **953 filas a 0**, y `ARRAY[4]` se lleva los 351 días de levante y los 602
de producción que **nunca ocurrieron ahí**. Es el problema del usuario al revés: no solo se pierde el lote
del filtro, sino que el levante hecho en NIZA III aparecería como hecho en NIZA I.

`historial_traslado_lote` sí es un **hecho fechado** (`granja_origen_id`, `granja_destino_id`, `created_at`)
y hoy tiene **0 filas** (el traslado nunca se usó en producción). Pero solo lo escribe
`TrasladarLoteAsync`; **`fn_mover_lote` pisa la granja sin dejar rastro**.

- La fn recorta `lotes_scope` con `l.granja_id = ANY(p_granja_ids)`, y el service pone **una sola granja**
  cuando el usuario la elige ⇒ el lote trasladado a NIZA I quedaría fuera al filtrar por NIZA III.
- El catálogo del filtro se recorta en el front por `b.farmId === granjaId`
  ([`reporte-diario-costos-postura-main.component.ts:138-140`](../frontend/src/app/features/reporte-diario-costos-postura/pages/reporte-diario-costos-postura-main/reporte-diario-costos-postura-main.component.ts)),
  usando el `farm_id` del **catálogo**, no dónde están los lotes.
- **Ninguna de las tres pestañas ni las tres hojas del Excel muestra la granja por fila** — solo aparece
  agregada en el encabezado del Excel. Hoy es indistinguible en qué granja ocurrió cada día.

### 2.4 🟠 La edad y la semana de producción no cuadran con la pantalla de seguimiento

La fn recalcula `edad_dias`/`semana` con `l.fecha_encaset` del lote, pero la fn diaria **canónica** ancla
en `COALESCE(fecha_inicio_produccion, fecha_encaset DEL PADRE, fecha_encaset propio)`. En **K345B**
(`lote_padre_id = 13`, encaset propio 31-ene vs. padre 28-ene) eso desfasa **301 de 301 filas** 3 días y
cambia la **semana en 129** de ellas (22-jul-2025: el reporte decía 172/sem 25, la pantalla 175/sem 26).
Rompe la paridad 1:1 que la propia cabecera de la fn promete y las dos columnas se pintan en pantalla y
se exportan a Excel.

---

## 3. Decisiones

| # | Decisión | Motivo |
|---|---|---|
| **D1** | **Levante se keyea por `lote_id` (varchar)**, no por `lote_id_int`. Se agrega `tipo_seguimiento = 'levante'` y `reproductora_id` vacío como guardas. | Es la clave que la aplicación escribe siempre. `lote_id_int` es legado y nadie la llena. |
| **D2** | El traslape **se muestra y no se suma dos veces**: las dos filas siguen visibles, la de **levante queda marcada** y **no aporta a los totales** (producción manda), y la pantalla avisa cuántos días están duplicados. | Costos necesita *ver* el conflicto para corregirlo en origen, pero el total del ciclo tiene que ser correcto. Mismo patrón que el aviso ya existente de «la partición de huevo no cuadra». |
| **D3** | **El lote base manda sobre la granja.** Al elegir un lote base, la granja (y la regional) pasan a ser punto de entrada: el alcance se expande a **todas las granjas asignadas al usuario** donde ese lote base tenga lotes. Sin lote base, la granja sigue siendo un muro. | Es el pedido literal: buscar por NIZA III y que igual traiga la producción hecha en NIZA I. **La expansión se hace en el service, contra las granjas asignadas** ⇒ el fail-closed se conserva intacto. |
| **D3b** | **La granja de cada fila es la VIGENTE ESE DÍA**, resuelta contra `historial_traslado_lote` (origen del primer traslado posterior a la fecha; si no hubo, la actual). Y el filtro `p_granja_ids` matchea la granja actual **o cualquiera por la que el lote pasó**. | Sin esto la columna Granja sería cosmética: `lotes.granja_id` es un escalar pisable y mover el lote reatribuiría el levante de NIZA III a NIZA I. Con 0 traslados registrados el resultado es **idéntico** al de hoy. |
| **D3c** | `fn_mover_lote` **registra el traslado** en `historial_traslado_lote` cuando cambia de granja (lo que ya hacía `TrasladarLoteAsync`). | Es el único camino que pisaba la granja sin dejar rastro; sin él D3b sería ciego a la mitad de los traslados. Es un INSERT de auditoría: no cambia el movimiento en sí, y mover de galpón dentro de la misma granja no escribe nada. |
| **D4** | Se agrega la columna **Granja** a las tres pestañas y a las tres hojas del Excel, más un bloque **«Dónde se hizo cada fase»** (fase · granja · lote base · lotes · rango · días). | Sin ella, seguir el lote base entre granjas sería ilegible. |
| **D7** | **Producción toma `edad_dias`/`semana` de la fn canónica** (ya vienen en el LATERAL); levante conserva el cálculo sobre su `fecha_encaset`. | «Una sola fórmula por número» + la paridad 1:1 que la fn promete. Levante no tiene fn canónica y su pantalla usa ese mismo ancla. |
| **D8** | Lo excluido por traslape **se cuantifica** (`TotalesExcluidos`) y se muestra en el aviso. | El duplicado no es simétrico: en K345 la fila de levante trae **133 machos de selección** que producción no registró. Excluir la fila es correcto, esconder esas aves no. |
| **D5** | El catálogo del filtro «Lote base» pasa a listarse por **dónde están los lotes**, no por `lote_postura_base.farm_id`. Endpoint propio del reporte, scoped al usuario. | Si el lote se traslada, la base tiene que seguir apareciendo bajo la granja donde se hizo el levante. |
| **D6** | La aritmética nueva (marcado de duplicados) vive en `Application/Calculos`, delegando la regla en `CorteEtapaPosturaCalculos`. La fn SQL sigue devolviendo el dato crudo. | «Una sola fórmula por número»: el dueño del corte de etapa ya existe. |

---

## 4. Cambios

### 4.1 BD / SQL — `fn_reporte_diario_costos_postura` v2

`backend/sql/fn_reporte_diario_costos_postura.sql` (espejo) + migración EF idempotente
`AddFnReporteDiarioCostosPosturaV2LevantePorLoteIdText` con el `.sql` embebido verbatim
(`CREATE OR REPLACE FUNCTION` — misma firma, sin `DROP`).

1. `lev_dedup`: `DISTINCT ON (s.lote_id, día)` sobre `s.lote_id` (texto);
   `WHERE s.tipo_seguimiento = 'levante' AND COALESCE(s.reproductora_id,'') = ''`
   y `EXISTS (... lotes_scope ls WHERE ls.lote_id::text = s.lote_id)`.
   Se elimina el filtro `s.lote_id_int IS NOT NULL`.
2. `lev`: join `ls.lote_id::text = v.lote_txt`.
3. Columna nueva `dia_en_ambas_etapas BOOLEAN` — la fn **solo informa el hecho crudo**
   (existe fila de la otra etapa ese día para ese lote); la **decisión** de si eso es doble
   conteo la toma C# con `CorteEtapaPosturaCalculos`.
4. Cabecera del archivo: nota de por qué la clave es `lote_id` y no `lote_id_int`.

### 4.2 Backend C#

| Archivo | Cambio |
|---|---|
| `Application/DTOs/ReporteDiarioCostosPosturaDtos.cs` | `ReporteDiarioCostosPosturaFilaDto`: + `DiaEnAmbasEtapas`, + `ExcluidoDelTotal`. `ReporteDiarioCostosPosturaReporteDto`: + `DiasDuplicados`, + `AlcanceExpandidoPorLoteBase`, + `Granjas`. `ReporteDiarioCostosPosturaRow`: + `DiaEnAmbasEtapas`. |
| `Application/Calculos/ReporteDiarioCostosPosturaCalculos.cs` | + `MarcarDuplicados(filas)` (usa `CorteEtapaPosturaCalculos.HayDobleConteo`; marca la fila **de levante** del día en conflicto). `ConstruirTotales` ignora las filas `ExcluidoDelTotal`. |
| `Infrastructure/Services/ReporteDiarioCostosPostura/ReporteDiarioCostosPosturaService.cs` | Alcance D3: si `LotePosturaBaseId` viene, `granjaIds` = todas las asignadas y `regional` = null; se reporta la expansión. Aplica `MarcarDuplicados` antes de totalizar. |
| `Application/Interfaces/IReporteDiarioCostosPosturaService.cs` + `API/Controllers/ReporteDiarioCostosPosturaController.cs` | + `GET /api/ReporteDiarioCostosPostura/lotes-base` → lotes base con las granjas donde tienen lotes, scoped al usuario (D5). |

### 4.3 Frontend

| Archivo | Cambio |
|---|---|
| `models/reporte-diario-costos-postura.model.ts` | Espeja los campos nuevos; `LotePosturaBaseOpcion` + `granjaIds: number[]`. |
| `services/reporte-diario-costos-postura.service.ts` | `lotesBase()` apunta al endpoint nuevo del reporte. |
| `pages/.../reporte-diario-costos-postura-main.component.ts` | Cascada por `granjaIds.includes(granjaId)`; aviso de días duplicados; nota de alcance expandido; `granja` en las filas precalculadas. |
| `pages/.../reporte-diario-costos-postura-main.component.html` | Columna **Granja** en Aves / Alimento / Huevos; banda de aviso del traslape; nota «el lote base se siguió a N granjas». Filas duplicadas con estilo atenuado. |
| `funciones/construir-aoa-costos-postura.funcion.ts` | Columna **Granja** en las tres hojas + marca de fila excluida del total. |
| `funciones/expandir-filas-alimento.funcion.ts` | Propaga `granja` y la marca de duplicado. |

### 4.4 Tests

`backend/tests/ZooSanMarino.Application.Tests/ReporteDiarioCostosPosturaCalculosTests.cs`:

- `MarcarDuplicados` marca **solo** la fila de levante cuando las dos aportan consumo/bajas.
- Fila de producción de **solo huevos** (arrastre) ⇒ **no** marca nada (regla de `CorteEtapaPosturaCalculos`).
- Fila de levante sin consumo ni bajas ⇒ no marca nada.
- Días sin contraparte ⇒ intactos.
- `ConstruirTotales` excluye las filas marcadas: kg, bajas y alimento por referencia.
- Los huevos **no** se ven afectados (el arrastre es legítimo).
- Sin duplicados, los totales quedan **byte a byte** como antes (no regresión de v1).

---

## 5. Casos de prueba — RESULTADO (contra el dump de producción, `:5433`)

| # | Caso | Esperado | Real |
|---|---|---|---|
| P1 | fn, NIZA III, ambas fases | 953 filas (351 lev + 602 prod) | ✅ **953 / 351 / 602** |
| P2 | fn, NIZA III, fase = Levante | 351 (hoy 0) | ✅ **351** |
| P3 | fn, LA ESMERALDA (20) — solo levante | 182 (hoy 0) | ✅ **182** |
| P4 | fn, empresa completa | Levante 533 + Producción 602 | ✅ **1.135 (533 + 602)** |
| P5 | Traslape K345 | 15 días en las dos etapas, 16.952 kg | ✅ **15 días · 16.952,00 kg**; C# marca **14** (el 15º tiene la fila de levante vacía y la regla no lo considera choque) |
| P6 | Gate de paridad de producción vs. v1, **las 5 empresas** | 0 diferencias salvo edad/semana | ✅ **0 / 0** en companies 1, 3, 4, 5, 6 |
| P7 | Traslado simulado de K345A a NIZA I (transacción revertida) | cada fase conserva su granja; el lote no se pierde del filtro | ✅ Levante **175 días en NIZA III** + 1 en NIZA I; Producción 16 en NIZA III + 285 en NIZA I. Filtrando por NIZA III **o** NIZA I lo encuentra |
| P8 | Array de granjas vacío / granja de otra empresa | 0 filas | ✅ **0 / 0** (fail-closed intacto) |
| P9 | Empresa Demo | recupera su levante | ✅ **35 filas de levante** (antes 0) |
| P10 | Edad/semana de producción vs. fn canónica (K345B) | 0 diferencias | ✅ **0 dif. edad / 0 dif. semana** (antes 301 y 129) |
| P11 | Smoke API (backend :5002, JWT + X-Secret-Up minteados) | los dos endpoints responden y cuadran | ✅ `lotes-base` → `K345 · granjas [NIZA III] · 2 lotes`; `generar` → 953 filas, `diasDuplicados: 14`, `totalesExcluidos` = **16.952 kg · 143 aves (133 de selección)**, huevo total **3.632.634** — el mismo testigo del Reporte Contable |
| P12 | Smoke UI (front :4200) | tablas cuadradas, avisos, sin errores | ✅ Aves 14/14/14 · Alimento 8/8/8 · Huevos 9/9/9 columnas; 14 filas atenuadas; bloque «Dónde se hizo cada fase»; **0 errores de consola** |
| P13 | `dotnet build` + `dotnet test` + `yarn build` | verde | ✅ 0 errores · **2.004 tests** en verde · front OK (solo el warning de bundle preexistente) |

---

## 6. Riesgos

- **El reporte cambia de números** (hacia el correcto): los totales de aves y alimento suben al incorporar
  el levante. Es lo esperado; hay que avisarlo a Costos junto al deploy.
- **Gate multipaís:** la fn es exclusiva de postura Sanmarino/Colombia, pero `seguimiento_diario_levante`
  la comparten Demo y el resto. El cambio de clave **suma** filas que antes se perdían; no altera ninguna
  aritmética existente. Se verifica P9 antes de mergear.
- **`fn_seguimiento_diario_produccion` no se toca** ⇒ la rama de producción sale idéntica (P6 lo prueba).

---

## 7. Queda pendiente (detectado, NO tocado)

| Hallazgo | Por qué no se tocó |
|---|---|
| El trigger `trg_lotes_sync_lote_postura_levante` copia `granja_id`/`nucleo_id`/`galpon_id` de `lotes` a `lote_postura_levante` en **cualquier** UPDATE, sin guarda por fase. Anula el comentario de `LoteService.cs:1166` («no tocar LPL, queda en granja de origen como historial»). | Es un trigger compartido por todo el módulo de lotes. El reporte ya no depende de esos espejos: resuelve la granja del día contra `historial_traslado_lote`. Arreglarlo es un cambio propio, con su propio gate. |
| La carga masiva (`fn_migracion_seguimiento_levante`) **sí escribe** `lote_id_int`; EF nunca. La tabla queda con dos poblaciones. | Con la fn keyeando por `lote_id` ya no importa para este reporte. Rellenar la columna o quitarla es una limpieza aparte. |
| Otros lectores de `seguimiento_diario_levante` que keyeen por `lote_id_int` tendrían el mismo bug. | Fuera del alcance de este arreglo; el censo quedó hecho en la auditoría. |
