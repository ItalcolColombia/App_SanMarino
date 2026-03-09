# Análisis: Fase del lote y unificación Lote / Producción

## Objetivo

1. **Guardar la fase del lote** (Levante, Producción).
2. **Simplificar la lógica**: hoy hay dos tablas (`lotes` + `produccion_lotes`) y se valida “si el lote está en produccion_lotes” para el seguimiento diario de producción.
3. **Permitir 1 o N lotes de producción por lote padre**: desde un lote en Levante puedo pasar a producción con todo el saldo en un solo lote, o dividir en varios lotes de producción (ej. 3 lotes con nombres distintos, todos atados al mismo lote principal).

---

## Estado actual (resumen)

| Tabla | Rol |
|-------|-----|
| **lotes** | Lote “principal”: nombre, granja, hembras_l, machos_l, fecha encaset, etc. Estado inicial = Levante. Tiene `lote_padre_id` (para consolidación). |
| **lote_etapa_levante** | Historial Levante: aves con que inicia y con que termina (al pasar a producción). |
| **produccion_lotes** | Una fila por lote que “entra” a producción: `lote_id` (FK conceptual al lote principal), hembras_iniciales, machos_iniciales, huevos_iniciales, tipo_nido, nucleo_p, etc. **Índice único en lote_id** → hoy solo 1 producción por lote. |
| **produccion_seguimiento** | Seguimientos diarios de producción; FK a `produccion_lotes.id`. |
| **seguimiento_diario** | Tabla unificada de seguimiento (levante / producción / reproductora) por tipo. |

Flujo hoy:

- Creas **un lote** (Levante) → 1 fila en `lotes` + 1 en `lote_etapa_levante`.
- Al pasar a producción → se cierra `lote_etapa_levante` y se crea **una** fila en `produccion_lotes` con el mismo `lote_id`.
- Seguimiento diario producción → se valida que exista `produccion_lotes` para ese `lote_id` y se guarda contra `produccion_lotes.id`.

Limitación actual: **un solo lote de producción por lote principal** (por el único en `lote_id`).

---

## Opciones de diseño

### Opción A: Tabla “Lote Fase” (solo fase + historial, mantener Lote y ProduccionLote)

**Idea:** Nueva tabla `lote_fase` que representa “fases” del lote con nombre y aves.

- **lote_fase**: id, lote_id (FK a lotes), nombre (ej. "Levante", "Prod-1", "Prod-2"), fase (enum: Levante | Produccion), aves_inicio_hembras, aves_inicio_machos, fecha_inicio, fecha_fin, aves_fin_hembras, aves_fin_machos.
- **lotes**: sin cambio; sigue siendo el “dueño” del ciclo.
- **produccion_lotes**: se relaciona por `lote_fase_id` en lugar de (o además de) `lote_id`; así un mismo lote puede tener varias filas en `produccion_lotes` (una por fase de producción).

**Pros:** Introduce la fase de forma explícita; permite N fases de producción por lote.  
**Contras:** Siguen existiendo dos conceptos (Lote vs ProduccionLote); más tablas y migraciones (produccion_lotes, produccion_seguimiento, etc.).

---

### Opción B: Un solo concepto “Lote” con fase y lote_padre (unificar en tabla lotes)

**Idea:** Todo es un **lote**; la fase se guarda en el mismo registro.

- **lotes**: se agregan:
  - `fase` (Levante | Produccion) — o equivalente.
  - Campos de producción que hoy están en `produccion_lotes`: hembras_iniciales_prod, machos_iniciales_prod, fecha_inicio_produccion, huevos_iniciales, tipo_nido, nucleo_p, ciclo, fecha_fin_produccion, aves_fin_hembras, aves_fin_machos (nullable, usados cuando fase = Produccion).
  - `lote_padre_id` ya existe: el lote “principal” es el de Levante; los de producción son **hijos** (fase = Produccion, lote_padre_id = id del lote Levante).

Flujo:

- Creas el lote principal → 1 fila en `lotes` (fase = Levante).
- Al pasar a producción puedes:
  - **Opción B1:** Una sola “pasada”: creas **un** lote hijo (fase = Produccion) con todo el saldo de Levante.
  - **Opción B2:** Varias “pasadas”: creas **varios** lotes hijos (fase = Produccion), cada uno con su nombre y su parte de aves (ej. Prod-1, Prod-2, Prod-3), todos con el mismo `lote_padre_id`.

Seguimiento diario:

- **Levante:** por `lote_id` (el lote que tiene fase = Levante).
- **Producción:** por `lote_id` (cada lote con fase = Produccion tiene su propio id); ya no hace falta “validar si existe en produccion_lotes” porque el mismo registro de lote indica fase y tiene los campos de producción.

**Pros:** Una sola tabla de “lote”; una sola validación (“este lote está en fase Producción”); soporta 1 o N lotes de producción por padre; menos tablas.  
**Contras:** Migración de datos desde `produccion_lotes` hacia `lotes` (crear filas hijas o rellenar campos); `produccion_seguimiento` tendría que apuntar a “algo” que identifique el lote de producción (hoy es ProduccionLoteId → pasaría a ser LoteId del hijo).

---

### Opción C: Mantener ProduccionLote pero permitir N por lote (relación 1:N)

**Idea:** No unificar tablas; solo cambiar la relación Lote ↔ ProduccionLote de 1:1 a 1:N.

- **produccion_lotes**: se quita el índice único de `lote_id`; se agrega `nombre` (ej. "Prod-1", "Galpón A") para distinguir.
- Un mismo **lote** (id de `lotes`) puede tener varias filas en `produccion_lotes` (varias “pasadas” a producción con distintos nombres y aves).
- Seguimiento diario producción sigue igual: por `ProduccionLoteId` (cada registro de producción tiene su id).

**Pros:** Cambio acotado (índice + nombre); no tocar estructura de `lotes`; reutiliza toda la lógica de ProduccionLote y ProduccionSeguimiento.  
**Contras:** Siguen dos tablas; la “fase” no queda explícita en `lotes` (se infiere: “tiene filas en produccion_lotes” = en producción).

---

## Comparación rápida

| Criterio | A (Lote Fase) | B (Unificar en Lote) | C (N ProduccionLote por Lote) |
|----------|----------------|----------------------|--------------------------------|
| Fase guardada en lote | En lote_fase | Sí (campo en lotes) | Implícita |
| 1 o N lotes de producción por padre | Sí | Sí (lotes hijos) | Sí |
| Una sola tabla de “lote” | No | Sí | No |
| Complejidad de migración | Media | Alta | Baja |
| Reutilizar lógica actual ProduccionLote | Sí (con FK a fase) | No (pasar a Lote) | Sí al 100 % |

---

## Recomendación

- **Si priorizas simplificar a medio plazo y tener un solo concepto de “lote” con fase clara:**  
  **Opción B** (unificar en `lotes` con fase y lotes hijos para producción). Implica migrar `produccion_lotes` a registros en `lotes` (fase = Produccion, lote_padre_id = lote Levante) y que los seguimientos de producción referencien el `lote_id` del hijo (o una tabla de seguimiento por lote_id cuando fase = Produccion).

- **Si priorizas el mínimo cambio y ya poder tener varios lotes de producción por lote:**  
  **Opción C** (quitar unicidad de `lote_id` en `produccion_lotes`, agregar nombre, permitir 1:N). Luego, si quieres “fase” en el lote, se puede añadir un campo `fase` en `lotes` (Levante por defecto; actualizado o inferido cuando existe al menos un ProduccionLote).

- **Opción A** tiene sentido si quieres un modelo explícito de “fases” con historial por fase y no quieres (aún) tocar la estructura de `lotes` ni eliminar `produccion_lotes`.

---

## Siguiente paso

Definir:

1. ¿Quieres **un solo modelo de “lote”** (Opción B) o **mantener Lote + ProduccionLote** y solo permitir N producción por lote (Opción C)?
2. Si eliges B: plan de migración de `produccion_lotes` y de `produccion_seguimiento` (referenciar lote_id en lugar de produccion_lote_id).
3. Si eliges C: script para quitar unicidad de `lote_id` en `produccion_lotes`, agregar `nombre`, y (opcional) campo `fase` en `lotes`.

Cuando decidas A, B o C (y en B el detalle de campos de producción en `lotes`), se puede bajar a cambios concretos de BD, entidades y servicios.
