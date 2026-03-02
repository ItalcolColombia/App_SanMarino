# Tipos de pasos en un Mapa (Encabezado, Extracción, Transformación, Ejecución, Exportación)

Este documento describe **qué es cada tipo de paso** en el módulo Mapas y cuándo usarlo al configurar un mapa.

---

## Orden recomendado del flujo

```
Encabezado → Extracción(es) → Transformación(es) → Ejecución → Exportación
```

Cada paso tiene un **Script SQL** opcional (excepto que en **Exportación** es obligatorio si quieres generar archivo). El motor ejecuta los pasos en orden; el resultado del último paso con SQL antes del Export se usa para generar el archivo (Excel/PDF).

---

## 1. Encabezado (Head)

| Campo   | Valor en el sistema |
|--------|----------------------|
| **Tipo** | `head` |
| **Etiqueta** | Ej. `encabezado`, `header` |

### ¿Qué es?

Es el **primer paso** del mapa. Sirve para definir una sola fila (o pocas) de **metadato o título** del reporte: nombre del reporte, empresa, período (fecha desde/hasta), fecha de generación, etc.

### ¿Para qué sirve?

- Mostrar en la parte superior del Excel (o del PDF) un título y datos del contexto (empresa, rango de fechas).
- No se usa para datos de negocio; solo para “cabecera” del documento.

### Script SQL

- **Opcional.** Si lo dejas vacío, el paso se salta.
- Si lo usas, conviene que devuelva **una sola fila** con columnas como: `titulo_reporte`, `empresa`, `fecha_desde`, `fecha_hasta`, `fecha_generacion`.
- Puedes usar los placeholders: `{{companyId}}`, `{{fechaDesde}}`, `{{fechaHasta}}`.

### Ejemplo de uso

Un SELECT que devuelve una fila con el título del reporte y las fechas del período (ver en el mismo documento de referencia el **SQL para el paso Encabezado**).

---

## 2. Extracción (Extraction)

| Campo   | Valor en el sistema |
|--------|----------------------|
| **Tipo** | `extraction` |
| **Etiqueta** | Ej. `extraction_1`, `granjas_produccion` |

### ¿Qué es?

Un paso que **lee datos de la base de datos** (tablas reales: granjas, lotes, seguimiento, etc.) y los deja listos para los siguientes pasos.

### ¿Para qué sirve?

- Traer de la BD lo que necesitas: granjas en producción, huevos por período, consumo de alimento, etc.
- Cada paso de extracción puede ser una consulta distinta (por ejemplo: una para encabezado/resumen y otra para detalle).
- **No modifica** tablas; solo **consulta**.

### Script SQL

- **Opcional** a nivel de motor (puedes tener un paso sin SQL y se salta).
- En la práctica sueles poner aquí el SELECT que arma el dataset: filtrado por `{{companyId}}`, `{{fechaDesde}}`, `{{fechaHasta}}`, `{{granjaIds}}` si aplica.
- El resultado de este paso (y de los que siguen) se va “acumulando” hasta llegar al paso de **Exportación**, que es el que usa el resultado final para generar el archivo.

### Ejemplo de uso

- “Listado de granjas en producción”.
- “Huevos por granja y período”.
- “Consumo de alimento por granja y fecha”.

---

## 3. Transformación (Transformation)

| Campo   | Valor en el sistema |
|--------|----------------------|
| **Tipo** | `transformation` |
| **Etiqueta** | Ej. `transformation_1`, `resumen_por_granja` |

### ¿Qué es?

Un paso que **trabaja sobre los datos ya obtenidos** en pasos anteriores (por ejemplo, el resultado de una o varias extracciones). No lee directamente de todas las tablas operativas; opera sobre el resultado que dejó el paso previo en el pipeline.

### ¿Para qué sirve?

- **Resumir**, **agrupar**, **calcular totales**, **filtrar** o **renombrar columnas** sobre lo ya extraído.
- Unificar o combinar lógica sin tocar las tablas base otra vez.
- En la implementación actual del motor, cada paso con SQL **reemplaza** el resultado anterior; por tanto, si quieres “transformar” lo extraído, en este paso pondrías un SQL que tome como entrada implícita el concepto de “lo que devolvió el paso anterior” (según cómo esté implementado el pipeline) o una nueva consulta que agrupe/filtre datos de las mismas tablas con otra forma.

### Script SQL

- **Opcional.**
- Suele ser un SELECT que agrupa, suma o filtra. Si el motor en tu versión pasa el resultado del paso anterior como tabla temporal o nombre conocido, aquí harías el SELECT sobre esa tabla; si no, puedes repetir consultas sobre las tablas base pero con otra lógica (resumen, totales, etc.).

### Ejemplo de uso

- “Totales por granja a partir de los datos ya extraídos”.
- “Solo filas con consumo &gt; 0”.
- “Columnas calculadas (porcentajes, promedios)”.

---

## 4. Ejecución (Execute)

| Campo   | Valor en el sistema |
|--------|----------------------|
| **Tipo** | `execute` |
| **Etiqueta** | Ej. `execute_1`, `preparar_export` |

### ¿Qué es?

Un paso de **“ejecución” o preparación final** sobre el pipeline: último ajuste o cálculo antes de exportar. En muchos flujos se usa para dejar el dataset en la forma exacta en que debe ir al archivo (columnas finales, orden, filtros).

### ¿Para qué sirve?

- Última preparación del resultado: ordenar, seleccionar columnas, aplicar reglas de negocio finales.
- En la práctica puede solaparse con **Transformación**; la diferencia es de orden en el flujo: Transformación(es) suelen ir antes y **Ejecución** suele ser el último paso con SQL antes de **Exportación**.

### Script SQL

- **Opcional.**
- Un SELECT que devuelve el conjunto de filas y columnas que quieres que llegue al paso de Exportación.

### Ejemplo de uso

- “Dejar solo las columnas que deben ir al Excel y en el orden deseado”.
- “Aplicar una última condición (ej. solo activos)”.

---

## 5. Exportación (Export)

| Campo   | Valor en el sistema |
|--------|----------------------|
| **Tipo** | `export` |
| **Etiqueta** | Ej. `export_excel` |

### ¿Qué es?

El **paso final** del mapa. Toma el resultado del último paso con SQL ejecutado (extracción, transformación o ejecución) y **genera el archivo** que se descarga: Excel o PDF.

### ¿Para qué sirve?

- Producir el **Excel** (o PDF) que el usuario descarga.
- Sin al menos un paso de tipo **Export** con **Script SQL** configurado, el mapa no se puede ejecutar para generar archivo (el sistema lo valida al dar “Ejecutar”).

### Script SQL

- **Obligatorio** si quieres que el mapa genere archivo. Debe ser una consulta que devuelva las filas y columnas que quieres en la hoja (o en el PDF).
- En muchos casos este SQL es el **mismo** que el del último paso de extracción o transformación (el dataset final). O puedes poner aquí directamente el SELECT final y usar los pasos anteriores solo para encabezados o lógica intermedia.
- **Opciones (JSON):** aquí sí se usan las opciones del paso, por ejemplo formato de export: `{"formato":"excel"}` o `{"formato":"pdf"}`. El valor debe ser JSON válido.

### Ejemplo de uso

- SELECT que devuelve: granja, lote, período, huevos, consumo_kg, etc. → ese resultado se vuelca al Excel (o PDF).
- Opciones: `{"formato":"excel"}`.

---

## Resumen rápido

| Tipo            | Descripción breve |
|-----------------|-------------------|
| **Encabezado**  | Primera fila o metadato del reporte (título, empresa, fechas). |
| **Extracción**  | Lee datos de la BD (granjas, huevos, consumo, etc.). |
| **Transformación** | Trabaja sobre lo ya extraído: resumir, agrupar, calcular. |
| **Ejecución**   | Última preparación del dataset antes de exportar. |
| **Exportación** | Genera el archivo (Excel/PDF) con el resultado del pipeline. |

Placeholders que puedes usar en cualquier SQL: `{{companyId}}`, `{{fechaDesde}}`, `{{fechaHasta}}`, `{{granjaIds}}`.
