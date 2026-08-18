# K345 — los 15 días que viven en levante Y en producción

**Fecha:** 2026-08-18 · **Decisión del usuario:** producción manda desde el primer huevo (V25.6.1)

## 1. Enfoque y por qué

`K345A` (lote 13) y `K345B` (lote 14) tienen días registrados **en las dos tablas** de seguimiento.
Son los **únicos** de toda la base: el guard ya impide crear nuevos, estos son el residuo. Medido:

| | días | qué son |
|---|---|---|
| K345A 16→22-jul-2025 · K345B 19→25-jul-2025 | **14** | la semana de transición: **misma mortalidad en ambos lados** (1/1, 0/0, 2/2), levante aporta alimento y producción los huevos, que arrancan 33 → 1.595 |
| K345A **7-abr-2026** | **1** | no es traslape: la fila de levante está **vacía** (mort 0, kg 0,000) con `observaciones = 'pruebas sistemas'`, sobre un día real de 4.277 huevos |

La mortalidad está **contada dos veces**. Producción es el lado que se queda porque tiene los huevos:
en esos días el lote ya está poniendo, y ese es el hecho que define la etapa.

### ⚠️ Corrección al plan aprobado: el alimento NO hay que reasignarlo, y sí hay otra cosa que rescatar

La decisión se tomó asumiendo «retirar la fila de levante conservando el consumo de alimento,
reasignándolo a producción». Al medir, **el alimento ya está en producción**, con el mismo valor
(`927,7 = 927,7` · `1.259 = 1.259` …) o mayor (K345B 23-jul: producción 1.279,9 vs levante 1.259,9).
No hay nada que reasignar.

Pero **sí hay datos que solo viven en levante** y que un borrado perdería:

| Dato | Dónde | Producción | Veredicto |
|---|---|---|---|
| `sel_m` = **21** (K345A 16-jul, id 169) y **112** (K345B 19-jul, id 230) | solo levante | `sel_m = 0` | 🔴 **133 machos seleccionados** — rompe conservación |
| `cv_hembras` 6,2 / 6,5 · `cv_machos` 6,7 / 7,5 | solo levante | `NULL` | se pierde |
| `uniformidad_hembras` 88,1 / 89,3 · `uniformidad_machos` 85 / 85,8 | solo levante | `NULL` | se pierde |
| `metadata` con `itemsHembras`/`itemsMachos` (1 día) | solo levante | revisar | se pierde |
| `peso_prom_hembras` 3.341,4 / 3.307,2 | ambos | `peso_h` = 3341,40 / 3307,20 | ✅ ya está |
| consumo de alimento | ambos | igual o mayor | ✅ ya está |
| mortalidad | ambos | idéntica | ✅ ya está (es la duplicada) |

**No están preservados en otro lado.** `produccion_resultado_levante.ac_sel_m` llega a **8** cuando el
total de `sel_m` de levante del lote 13 es **241**, y el lote 14 **ni figura** en esa tabla ⇒ ese
acumulado no es una copia confiable. Queda anotado como defecto propio, fuera de alcance.

⇒ **El orden correcto es migrar lo único y DESPUÉS borrar.** Un `DELETE` pelado perdería 133 aves.

## 2. Archivos

- `backend/src/ZooSanMarino.Infrastructure/Migrations/<ts>_K345RetiroTraslapeLevante.cs` — data-only
- `backend/sql/correccion_traslape_levante_produccion.sql` — el mismo SQL, trazable
- `backend/tests/...` — el gate va por SQL (no hay cálculo puro nuevo)

## 3. Cambios de BD — dos pasos, en una transacción

**Paso 1 — rescatar lo único.** Para cada día traslapado, copiar a producción **solo donde producción
está en 0 o NULL** (jamás pisar un dato que producción ya tiene):
`sel_h`, `sel_m`, `cv_hembras`, `cv_machos`, `uniformidad` ← `uniformidad_hembras`,
`uniformidad_machos`, y `metadata` si producción no tiene.

**Paso 2 — retirar las filas de levante** de esos días. Alcance dinámico: el `JOIN` entre las dos
tablas por `(lote_id, fecha::date)`, sin nombrar lotes ni ids.

- `seguimiento_diario_levante` **no tiene soft-delete** ⇒ es un `DELETE` duro. Lo registra el trigger
  `trg_tombstone_seguimiento_diario_levante` en `sync_tombstones`, así que los clientes offline se
  enteran del borrado
- **Backup previo obligatorio**: `_backup_traslape_levante_k345_20260818` con las filas completas
- **Idempotente:** la 2ª corrida no encuentra traslape ⇒ 0 filas

## 4. Reglas de negocio

- Producción manda desde el primer huevo del día traslapado
- **Nada se pisa**: la migración solo rellena campos vacíos de producción
- La fila del 7-abr-2026 no necesita rescate (está vacía y es de prueba): se borra directo
- El alimento no se toca: ya está y producción es la fuente

## 5. Casos de prueba

1. **Antes/después**: la consulta de traslape pasa de **15 filas a 0**
2. **Conservación de la selección**: `SUM(sel_m)` de los días afectados, sumando ambas tablas, es
   **igual antes y después** (133 machos siguen contados, ahora en producción)
3. **Mortalidad**: deja de estar duplicada — el total por lote baja exactamente en lo que aportaba levante
4. **Alimento**: `SUM(cons_kg_h + cons_kg_m)` de producción **no cambia** (no se toca)
5. **Nada pisado**: ningún campo de producción que ya tenía valor cambia (`peso_h` sigue en 3341,40)
6. **Idempotencia**: 2ª corrida ⇒ 0 filas afectadas
7. **Simulación en transacción + `ROLLBACK`** antes de aplicar, con el antes/después a la vista
8. **No regresión**: ningún otro lote de la base tiene traslape ⇒ nada más se toca

## 6. Riesgos y qué NO hace

- 🔴 **`DELETE` duro sin soft-delete.** Mitigado con la tabla de backup y la simulación previa
- **No arregla `produccion_resultado_levante`**, cuyo `ac_sel_m` no refleja los totales de levante
  (8 vs 241). Es un defecto propio, medido acá y anotado en el tracker, con su propio alcance
- **No cambia el corte levante/producción a 25 semanas** — es la decisión hermana (V25.6.2) y va aparte
- No toca ningún otro lote ni ninguna otra empresa
