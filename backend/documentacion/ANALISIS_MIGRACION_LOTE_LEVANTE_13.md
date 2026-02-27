# Análisis: Migración Lote 13 (Levante) a LPL y seguimiento_diario

## 1. Objetivo

Migrar el **lote_id = 13** (tabla `lotes`) al nuevo flujo:

1. **lote_postura_levante (LPL)**: mismo dato del lote + estado + aves con que se abrió + aves actuales al cierre.
2. **seguimiento_diario**: los registros diarios que hoy están en `seguimiento_lote_levante` pasan a la tabla unificada `seguimiento_diario` (tipo = `levante`).

Antes de migrar, se deben **calcular las aves actuales** al cierre del levante (semana 25/26) usando mortalidad, sexaje y selección del seguimiento de levante.

---

## 2. Tablas involucradas

### 2.1 Origen (legacy)

| Tabla | Uso |
|-------|-----|
| **lotes** | Un registro por lote; para levante tiene `hembras_l`, `machos_l` (aves con que se abrió). |
| **seguimiento_lote_levante** | Registros diarios de levante: mortalidad, selección, error sexaje, consumo, etc. FK `lote_id` → `lotes.lote_id`. |

### 2.2 Destino (nuevo flujo)

| Tabla | Uso |
|-------|-----|
| **lote_postura_levante** | Un registro por lote en levante; mismos datos que `lotes` + `aves_h_inicial`, `aves_m_inicial`, `aves_h_actual`, `aves_m_actual`, `estado_cierre`. |
| **seguimiento_diario** | Registros diarios unificados (levante/producción/reproductora). Para levante: `tipo_seguimiento = 'levante'`, `lote_postura_levante_id`. |

---

## 3. Cálculo de aves actuales (antes de cerrar levante)

Las **aves actuales** al cierre del levante (p. ej. semana 25) se obtienen restando del inicial todos los descuentos registrados en el seguimiento:

- **Aves disponibles hembras** = aves con que se abrió (hembras) − mortalidad − selección − error sexaje (hembras).
- **Aves disponibles machos** = aves con que se abrió (machos) − mortalidad − selección − error sexaje (machos).

Fórmulas:

```
aves_h_actual = hembras_l - SUM(mortalidad_hembras) - SUM(sel_h) - SUM(error_sexaje_hembras)
aves_m_actual = machos_l   - SUM(mortalidad_machos)  - SUM(sel_m)  - SUM(error_sexaje_machos)
```

- **Inicial (aves con que se abrió el lote)**: `lotes.hembras_l`, `lotes.machos_l` (o desde el primer registro de seguimiento si se prefiere).
- **Descuentos**: sumas sobre **todos** los registros de `seguimiento_lote_levante` para ese `lote_id`:
  - `mortalidad_hembras`, `mortalidad_machos`
  - `sel_h`, `sel_m` (selección)
  - `error_sexaje_hembras`, `error_sexaje_machos`

Se aplica `GREATEST(0, valor)` para no dejar negativos.

---

## 4. Flujo de migración recomendado (lote 13)

### Paso 0: Verificación y cálculo (solo SELECT)

- Consultar `lotes` para `lote_id = 13` (hembras_l, machos_l, nombre, granja, etc.).
- Consultar `seguimiento_lote_levante` para `lote_id = 13` y las sumas de mortalidad, sel_h, sel_m, error_sexaje_hembras, error_sexaje_machos.
- Calcular y mostrar `aves_h_actual` y `aves_m_actual` con las fórmulas anteriores.
- Opcional: detectar semana máxima registrada (p. ej. por `fecha_encaset` + `fecha_registro`) para confirmar “cierre en semana 25”.

### Paso 1: Crear LPL desde lote 13

- Si no existe registro en `lote_postura_levante` con `lote_id = 13`, insertar uno desde `lotes` (igual que en `migracion_01_lotes_to_lpl.sql`), con:
  - `aves_h_inicial` = `hembras_l`, `aves_m_inicial` = `machos_l`.
  - `aves_h_actual` y `aves_m_actual` = mismo valor inicial por ahora (se actualizarán en el paso 3).
  - `estado_cierre` = `'Abierto'` (o `'Cerrado'` si ya se va a cerrar en el mismo script).

### Paso 2: Copiar seguimiento levante → seguimiento_diario

- Para cada fila de `seguimiento_lote_levante` con `lote_id = 13`:
  - INSERT en `seguimiento_diario` con:
    - `tipo_seguimiento = 'levante'`
    - `lote_id` = `'13'` (string)
    - `lote_postura_levante_id` = id del LPL del lote 13 (obtenido en paso 1)
    - `fecha` = `fecha_registro`
    - Mapeo de columnas: mortalidad_hembras, mortalidad_machos, sel_h, sel_m, error_sexaje_hembras, error_sexaje_machos, consumo, alimento, observaciones, ciclo, pesos, uniformidad, cv, agua, metadata, items_adicionales, etc.
- Evitar duplicados (p. ej. por `lote_postura_levante_id` + `fecha` o por existencia previa).

### Paso 3: Actualizar LPL con aves actuales y cerrar (si aplica)

- Recalcular `aves_h_actual` y `aves_m_actual` desde **seguimiento_diario** (tipo levante, `lote_postura_levante_id` = LPL del 13) con las mismas fórmulas.
- UPDATE `lote_postura_levante` SET `aves_h_actual`, `aves_m_actual` (y opcionalmente `estado_cierre = 'Cerrado'` si edad ≥ 26).

### Paso 4 (opcional): Cierre y creación de LPP

- Si el lote ya está en semana 26 y se cierra el levante, ejecutar la lógica de `migracion_03_cerrar_lpl_crear_lpp.sql` para ese LPL (crear LPP-H y LPP-M con las aves actuales calculadas).

---

## 5. Consultas de referencia

### Lote actual (origen)

```sql
SELECT * FROM lotes WHERE lote_id = 13;
```

### Aves iniciales desde lote (y opcionalmente desde primer seguimiento)

```sql
SELECT l.hembras_l AS cantidad_hembras_inicial, l.machos_l AS cantidad_machos_inicial
FROM seguimiento_lote_levante sl
INNER JOIN lotes l ON l.lote_id = sl.lote_id
WHERE l.lote_id = 13
LIMIT 1;
```

### Nueva tabla de seguimientos (destino)

```sql
SELECT * FROM seguimiento_diario
WHERE tipo_seguimiento = 'levante' AND (lote_id = '13' OR lote_postura_levante_id = <lpl_id>);
```

---

## 6. Resumen de fórmulas

| Dato | Origen | Fórmula / Nota |
|------|--------|-----------------|
| Aves con que se abrió (H/M) | `lotes.hembras_l`, `lotes.machos_l` | Copiar a LPL como `aves_h_inicial`, `aves_m_inicial`. |
| Aves actuales al cierre (H) | Seguimiento | `hembras_l - SUM(mortalidad_hembras) - SUM(sel_h) - SUM(error_sexaje_hembras)` |
| Aves actuales al cierre (M) | Seguimiento | `machos_l - SUM(mortalidad_machos) - SUM(sel_m) - SUM(error_sexaje_machos)` |
| Cierre levante | Semana 25/26 | Se actualiza LPL.`aves_h_actual`, `aves_m_actual` y `estado_cierre = 'Cerrado'`. |

Los scripts asociados son:

- **`sql/migracion_00_calculo_aves_lote_13.sql`**: solo consultas y cálculo de aves (paso 0). Ejecutar primero para validar números.
- **`sql/migracion_01_lotes_to_lpl.sql`**: crea LPL para todos los lotes sin LPL (paso 1 global).
- **`sql/migracion_seguimiento_levante_to_diario_lote_13.sql`**: para lote 13: crea LPL si no existe, copia seguimiento_lote_levante → seguimiento_diario y actualiza aves_h_actual/aves_m_actual en LPL (pasos 1, 2 y 3).

**Nota**: Si en tu base de datos la tabla `seguimiento_lote_levante` usa nombres de columna en PascalCase (p. ej. `FechaRegistro`, `MortalidadHembras`), ajusta los scripts SQL o crea vistas con alias en snake_case.
