# Análisis: Migración Producción (produccion_lotes + produccion_diaria) → LPP + seguimiento_diario

## 1. Objetivo

Migrar los lotes que en la **versión anterior** pasaron a producción (semana 26) desde las tablas legacy al nuevo flujo:

1. **lote_postura_produccion (LPP)**: Se crean **dos** registros por lote: uno para hembras (**-H**) y otro para machos (**-M**), con las aves actuales que trae el cierre de levante.
2. **seguimiento_diario**: Los registros diarios que hoy están en **produccion_diaria** pasan a la tabla unificada **seguimiento_diario** (tipo = `produccion`) vinculados al LPP correspondiente.

---

## 2. Tablas legacy (versión anterior)

| Tabla | Uso |
|-------|-----|
| **produccion_lotes** | Un registro por lote al pasar a producción (26 semanas). `lote_id` (varchar o int según BD), `fecha_inicio_produccion`, `hembras_iniciales` / `machos_iniciales` (o aves_iniciales_h/m), granja, núcleo, etc. |
| **produccion_diaria** | Seguimiento diario de producción: mortalidad, consumo, huevos, etc. `lote_id` (int FK a lotes o varchar) + `fecha_registro`. |

---

## 3. Tablas destino (nuevo flujo)

| Tabla | Uso |
|-------|-----|
| **lote_postura_levante (LPL)** | Origen de las aves: al cerrar (semana 26) se usan `aves_h_actual` y `aves_m_actual`. |
| **lote_postura_produccion (LPP)** | Dos registros por cierre de levante: **LPP-H** (solo hembras) y **LPP-M** (solo machos), con `lote_postura_levante_id` y aves iniciales/actuales. |
| **seguimiento_diario** | Registros diarios con `tipo_seguimiento = 'produccion'` y `lote_postura_produccion_id` apuntando al LPP. |

---

## 4. Flujo de migración (ej. lote 13)

### Precondición

- El lote 13 debe tener ya migrado **levante**: existe LPL para `lote_id = 13` con `aves_h_actual` y `aves_m_actual` calculados (script `migracion_seguimiento_levante_to_diario_lote_13.sql`).

### Pasos

1. **Obtener LPL** del lote 13 y aves de cierre (`aves_h_actual`, `aves_m_actual`). Si no hay LPL, se pueden usar como respaldo `produccion_lotes.hembras_iniciales` / `machos_iniciales` para ese lote.
2. **Crear LPP-H y LPP-M** (idempotente):
   - LPP-H: nombre `{lote_nombre}-H`, aves solo hembras, `lote_postura_levante_id` = LPL del 13.
   - LPP-M: nombre `{lote_nombre}-M`, aves solo machos, mismo `lote_postura_levante_id`.
3. **Cerrar LPL** si sigue `Abierto` (`estado_cierre = 'Cerrado'`).
4. **Copiar produccion_diaria → seguimiento_diario**:
   - Filtrar `produccion_diaria` por lote (ej. `lote_id = 13`).
   - Por cada fila: INSERT en `seguimiento_diario` con `tipo_seguimiento = 'produccion'`, `lote_postura_produccion_id` = **LPP-H** (el seguimiento legacy es del lote completo; se asocia a un solo sublote para no duplicar totales).
   - Mapear columnas: fecha_registro → fecha, mortalidad_hembras/machos, sel_h/sel_m, consumo, huevos, tipo_alimento, etapa, pesaje, etc.

### Asignación del seguimiento diario a H o M

En legacy había **un solo** lote en producción y un solo conjunto de registros diarios. En el nuevo modelo hay **dos** LPP (H y M). Criterio de migración:

- **Asignar todo el seguimiento diario legacy al LPP-H** (hembras), de modo que reportes y totales sigan siendo coherentes y no se dupliquen huevos/consumos. El LPP-M queda con aves pero sin historial diario migrado (opcionalmente se puede documentar o ajustar después).

---

## 5. Resumen de datos

| Dato | Origen | Destino |
|------|--------|---------|
| Aves al inicio producción (H/M) | LPL.aves_h_actual, aves_m_actual (o produccion_lotes) | LPP-H: aves_h_*; LPP-M: aves_m_* |
| Registros diarios producción | produccion_diaria (lote_id = 13) | seguimiento_diario (tipo=produccion, lote_postura_produccion_id = LPP-H) |

---

## 6. Script asociado

- **`sql/migracion_produccion_lote_13_lpp_seguimiento_diario.sql`**: Para lote 13 (o parametrizable): crea LPP-H y LPP-M desde LPL, cierra LPL si aplica, copia `produccion_diaria` → `seguimiento_diario` vinculado a LPP-H.
