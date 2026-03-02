|# Encabezados y ejemplos SQL – Granjas en producción, huevos y consumo de alimento

Documento de referencia para armar formularios o reportes (incl. módulo Mapas) con datos de **granjas en producción**, **cantidad de huevos** y **consumo de alimento** desde la base de datos.

---

## 1. Encabezados sugeridos para el formulario / reporte

Use estos títulos como secciones en pantalla o en el Excel exportado:

| Sección | Encabezado sugerido | Descripción |
|--------|---------------------|-------------|
| Granjas | **Granjas en producción** | Listado de granjas que tienen al menos un lote de postura en producción activa (estado Abierta). |
| Granjas | **Nombre granja** | Nombre de la granja (tabla `farms.name`). |
| Granjas | **Cantidad de lotes en producción** | Número de lotes activos por granja. |
| Huevos | **Cantidad de huevos** | Total de huevos (por lote, por granja o por período). |
| Huevos | **Huevos totales / incubables / limpios / etc.** | Desglose por tipo (total, incubable, limpio, piso, roto, etc.). |
| Huevos | **Período (fecha desde – fecha hasta)** | Rango de fechas del reporte. |
| Alimento | **Consumo de alimento (kg)** | Consumo total en kilogramos (hembras + machos). |
| Alimento | **Consumo hembras (kg)** / **Consumo machos (kg)** | Desglose por sexo. |
| Alimento | **Tipo de alimento** | Tipo registrado en el seguimiento diario. |

---

## 2. Tablas y columnas de referencia en base de datos

- **Granjas:** `farms` → `id`, `name`, `status`, `company_id`
- **Lotes en producción:** `lote_postura_produccion` → `lote_postura_produccion_id`, `granja_id`, `lote_nombre`, `estado_cierre` ('Abierta' = en producción, 'Cerrada' = finalizado)
- **Seguimiento diario (producción):** `seguimiento_diario` → `tipo_seguimiento` = 'produccion', `lote_postura_produccion_id`, `fecha`, `consumo_kg_hembras`, `consumo_kg_machos`, `huevo_tot`, `huevo_inc`, `huevo_limpio`, etc.

---

## 2.1 Resumen: SQL y tipo de paso para el reporte

En el mapa debe haber **al menos un paso tipo Export** con Script SQL para poder ejecutar y descargar. Orden sugerido:

| Orden | Tipo de paso | Descripción del reporte | Etiqueta sugerida |
|-------|--------------|--------------------------|-------------------|
| 1 | **Encabezado** (`head`) | Título, empresa, período y fecha de generación (1 fila) | `encabezado` |
| 2 | **Extracción** (`extraction`) | Opcional: datos que luego usará el export | `resumen_granjas` |
| 3 | **Exportación** (`export`) | Resultado que se vuelca al Excel/PDF (obligatorio) | `export_excel` |

**Reporte mínimo (solo Excel con resumen):**  
- Paso 1: Encabezado → SQL de la sección 3.0 (opcional si no quieres fila de título).  
- Paso 2: Exportación → SQL de la sección 3.5 (resumen por granja) y en Opciones: `{"formato":"excel"}`.

**Reporte con detalle por fecha:**  
- Paso 1: Encabezado → SQL 3.0.  
- Paso 2: Extracción → SQL 3.3 (huevos por período) o 3.4 (consumo por período).  
- Paso 3: Exportación → mismo SQL 3.3 o 3.4 (o 3.5 para resumen) y Opciones: `{"formato":"excel"}`.

A continuación se detalla cada SQL por tipo.

---

## 3. Ejemplos SQL para usar en Mapas (extracción / export)

### 3.0 Encabezado — Título y metadato del reporte

**Tipo de paso:** `head` (Encabezado)

Use este SQL en un paso de tipo **Encabezado** para obtener una sola fila con el título del reporte, la empresa, el período y la fecha de generación. Los placeholders se reemplazan por los parámetros de la ejecución.

```sql
SELECT
  'Granjas en producción, huevos y consumo de alimento' AS titulo_reporte,
  (SELECT name FROM companies WHERE id = {{companyId}} LIMIT 1) AS empresa,
  {{fechaDesde}} AS fecha_desde,
  {{fechaHasta}} AS fecha_hasta,
  CURRENT_DATE AS fecha_generacion;
```

Columnas resultantes (puede usarlas como primera fila o encabezado en el export):

| Columna             | Ejemplo / descripción                          |
|---------------------|-------------------------------------------------|
| titulo_reporte      | Granjas en producción, huevos y consumo de alimento |
| empresa             | Nombre de la compañía (según `company_id`)      |
| fecha_desde         | Fecha inicio del período                        |
| fecha_hasta         | Fecha fin del período                           |
| fecha_generacion    | Fecha en que se ejecutó el mapa                 |

### 3.1 Extracción — Granjas en producción (listado)

**Tipo de paso:** `extraction` (Extracción)

Granjas que tienen al menos un lote de postura producción con `estado_cierre = 'Abierta'`. En Mapas use **{{companyId}}** para filtrar por compañía.

```sql
SELECT
  f.id          AS granja_id,
  f.name        AS nombre_granja,
  COUNT(DISTINCT lpp.lote_postura_produccion_id) AS cantidad_lotes_produccion
FROM farms f
INNER JOIN lote_postura_produccion lpp ON lpp.granja_id = f.id
WHERE (lpp.estado_cierre IS NULL OR lpp.estado_cierre = 'Abierta')
  AND f.company_id = {{companyId}}
  AND (f.deleted_at IS NULL)
GROUP BY f.id, f.name
ORDER BY f.name;
```

### 3.2 Extracción — Cantidad de huevos por granja (resumen por lote)

**Tipo de paso:** `extraction` (Extracción)

Huevos totales y desglose por lote en producción (datos del lote; para totales por período use 3.3 con `seguimiento_diario`).

```sql
SELECT
  f.id                    AS granja_id,
  f.name                  AS nombre_granja,
  lpp.lote_postura_produccion_id,
  lpp.lote_nombre,
  lpp.aves_h_actual       AS aves_hembras_actual,
  lpp.aves_m_actual       AS aves_machos_actual,
  COALESCE(lpp.huevo_tot, 0)    AS huevos_totales,
  COALESCE(lpp.huevo_inc, 0)    AS huevos_incubables,
  COALESCE(lpp.huevo_limpio, 0) AS huevos_limpios,
  COALESCE(lpp.huevo_piso, 0)   AS huevos_piso,
  COALESCE(lpp.huevo_roto, 0)   AS huevos_rotos,
  lpp.peso_huevo          AS peso_promedio_huevo
FROM farms f
INNER JOIN lote_postura_produccion lpp ON lpp.granja_id = f.id
WHERE (lpp.estado_cierre IS NULL OR lpp.estado_cierre = 'Abierta')
  AND f.company_id = {{companyId}}
  AND (f.deleted_at IS NULL)
ORDER BY f.name, lpp.lote_nombre;
```

### 3.3 Extracción — Cantidad de huevos por período (desde seguimiento diario)

**Tipo de paso:** `extraction` (Extracción)

Totales de huevos en el rango de fechas que el usuario elige al ejecutar el mapa. En el **Script SQL** del paso use los placeholders **{{fechaDesde}}** y **{{fechaHasta}}**; el motor los reemplaza por los parámetros de la ejecución.

```sql
SELECT
  f.id                    AS granja_id,
  f.name                  AS nombre_granja,
  lpp.lote_nombre,
  sd.fecha                AS fecha_seguimiento,
  COALESCE(sd.huevo_tot, 0)    AS huevos_totales,
  COALESCE(sd.huevo_inc, 0)    AS huevos_incubables,
  COALESCE(sd.huevo_limpio, 0) AS huevos_limpios,
  COALESCE(sd.huevo_piso, 0)   AS huevos_piso,
  COALESCE(sd.peso_huevo, 0)   AS peso_huevo
FROM seguimiento_diario sd
INNER JOIN lote_postura_produccion lpp ON lpp.lote_postura_produccion_id = sd.lote_postura_produccion_id
INNER JOIN farms f ON f.id = lpp.granja_id
WHERE sd.tipo_seguimiento = 'produccion'
  AND sd.fecha >= {{fechaDesde}}
  AND sd.fecha <= {{fechaHasta}}
  AND f.company_id = {{companyId}}
  AND (f.deleted_at IS NULL)
ORDER BY f.name, lpp.lote_nombre, sd.fecha;
```

### 3.4 Extracción — Consumo de alimento por granja y período

**Tipo de paso:** `extraction` (Extracción)

Consumo total (hembras + machos) y desglose por lote y fecha:

```sql
SELECT
  f.id                          AS granja_id,
  f.name                        AS nombre_granja,
  lpp.lote_nombre,
  sd.fecha                      AS fecha_seguimiento,
  COALESCE(sd.consumo_kg_hembras, 0) AS consumo_kg_hembras,
  COALESCE(sd.consumo_kg_machos, 0)  AS consumo_kg_machos,
  (COALESCE(sd.consumo_kg_hembras, 0) + COALESCE(sd.consumo_kg_machos, 0)) AS consumo_kg_total,
  sd.tipo_alimento              AS tipo_alimento
FROM seguimiento_diario sd
INNER JOIN lote_postura_produccion lpp ON lpp.lote_postura_produccion_id = sd.lote_postura_produccion_id
INNER JOIN farms f ON f.id = lpp.granja_id
WHERE sd.tipo_seguimiento = 'produccion'
  AND sd.fecha >= {{fechaDesde}}
  AND sd.fecha <= {{fechaHasta}}
  AND f.company_id = {{companyId}}
  AND (f.deleted_at IS NULL)
ORDER BY f.name, lpp.lote_nombre, sd.fecha;
```

### 3.5 Extracción / Exportación — Resumen por granja: huevos + consumo en el período

**Tipo de paso:** `extraction` o `export` (recomendado como **Export** para generar el Excel)

Un solo resultado por granja con totales de huevos y consumo en el rango de fechas. Ideal como paso previo al **Export** a Excel.

```sql
SELECT
  f.id   AS granja_id,
  f.name AS nombre_granja,
  COUNT(DISTINCT lpp.lote_postura_produccion_id) AS lotes_en_produccion,
  SUM(COALESCE(sd.huevo_tot, 0))     AS total_huevos_periodo,
  SUM(COALESCE(sd.consumo_kg_hembras, 0) + COALESCE(sd.consumo_kg_machos, 0)) AS consumo_kg_total_periodo
FROM farms f
INNER JOIN lote_postura_produccion lpp ON lpp.granja_id = f.id
LEFT JOIN seguimiento_diario sd
  ON sd.lote_postura_produccion_id = lpp.lote_postura_produccion_id
  AND sd.tipo_seguimiento = 'produccion'
  AND sd.fecha >= {{fechaDesde}}
  AND sd.fecha <= {{fechaHasta}}
WHERE (lpp.estado_cierre IS NULL OR lpp.estado_cierre = 'Abierta')
  AND f.company_id = {{companyId}}
  AND (f.deleted_at IS NULL)
GROUP BY f.id, f.name
ORDER BY f.name;
```

**Placeholders del módulo Mapas:** en el Script SQL del paso escriba tal cual `{{fechaDesde}}`, `{{fechaHasta}}`, `{{companyId}}` y opcionalmente `{{granjaIds}}`. El motor los sustituye por parámetros; las fechas y company_id se rellenan con los valores de la ejecución.

---

## 4. Uso en el módulo Mapas

1. **Encabezado:** En un paso tipo **encabezado** puede definir el título del reporte, por ejemplo:  
   `"Reporte: Granjas en producción, huevos y consumo de alimento"`.

2. **Extracción:** Cree pasos tipo **extracción** y pegue en *Script SQL* uno de los ejemplos anteriores. Use los placeholders **{{fechaDesde}}**, **{{fechaHasta}}**, **{{companyId}}**; el backend los reemplaza por los parámetros de la ejecución.

3. **Export:** En el paso de **export** (Excel) se generará una hoja con las columnas resultantes de la consulta. Los encabezados de la tabla (sección 1) pueden usarse como títulos de columna en el Excel o como etiquetas en el formulario si los datos se muestran en pantalla.

---

## 5. Ejemplo de encabezados para Excel (columnas)

Para el resultado de **3.5**, puede nombrar las columnas en el export así:

| Columna resultado | Encabezado en Excel / formulario |
|-------------------|-----------------------------------|
| granja_id         | ID Granja                         |
| nombre_granja     | Granja en producción              |
| lotes_en_produccion | Cantidad de lotes en producción |
| total_huevos_periodo | Cantidad de huevos (período)   |
| consumo_kg_total_periodo | Consumo de alimento (kg)     |

Si necesita más columnas (p. ej. consumo hembras/machos por separado), use la consulta **3.4** y exponga `consumo_kg_hembras` y `consumo_kg_machos` con encabezados **Consumo hembras (kg)** y **Consumo machos (kg)**.

---

## 6. Tabla de referencia: SQL y tipo del reporte

| Sección | Tipo de paso | Descripción del reporte |
|---------|--------------|--------------------------|
| **3.0** | `head` (Encabezado) | Título del reporte, empresa, fecha_desde, fecha_hasta, fecha_generacion (1 fila) |
| **3.1** | `extraction` (Extracción) | Listado de granjas en producción y cantidad de lotes por granja |
| **3.2** | `extraction` (Extracción) | Huevos por granja y lote (resumen desde lote_postura_produccion) |
| **3.3** | `extraction` (Extracción) | Huevos por granja, lote y fecha (desde seguimiento_diario en el período) |
| **3.4** | `extraction` (Extracción) | Consumo de alimento (kg) por granja, lote y fecha en el período |
| **3.5** | `extraction` o `export` | Resumen por granja: lotes en producción, total huevos período, consumo kg total período |

**Opciones en paso Export:** use JSON válido, por ejemplo `{"formato":"excel"}` o `{"formato":"pdf"}`.
