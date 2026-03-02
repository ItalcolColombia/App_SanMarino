# Análisis: Módulo Mapas (Maps)

## 1. Objetivo del módulo

El módulo **Mapas** permite definir **documentos de mapeo** que extraen, transforman y exportan datos hacia el ERP (sistema CIESA): consumos, movimientos de huevos, etc. Todo se construye por **bloques configurables** desde la interfaz: el usuario escribe consultas SQL y define el flujo (encabezado → extracciones → transformaciones → ejecución → exportación). La ejecución puede parametrizarse (rango de fechas, granjas, compañía) y el resultado se exporta a **PDF o Excel**; cada ejecución queda registrada en historial con el payload usado.

---

## 2. Estructura del menú

| Nivel | Item | Descripción |
|-------|------|-------------|
| **Raíz** | **Mapas** | Item principal en el menú. Ruta base: `/mapas`. |
| **Submódulo 1** | **Configuraciones** | Lista de mapas creados; crear/editar mapas y sus pasos (encabezado, extracción, transformación, ejecución, exportación). |
| **Submódulo 2** | **Mapa** | Vista/ejecución del mapa: al elegir un mapa se muestra su detalle y desde aquí se **ejecuta** (botón Play) con parámetros (fechas, granjas, tipo de dato). |

Alternativa de rutas sugerida:

- `/mapas` → contenedor o redirección (p. ej. a lista de mapas).
- `/mapas/configuraciones` → lista de mapas (CRUD y entrada a configurar pasos).
- `/mapas/configuraciones/:id` → configuración de un mapa (pasos en orden).
- `/mapas/ejecutar/:id` o `/mapas/mapa/:id` → vista del mapa y modal de ejecución (Play).

---

## 3. Conceptos clave

### 3.1 Mapa (documento de mapeo)

- **Nombre**, **descripción**.
- **Compañía(s)** y **país** que pueden ver/usar el mapa (alcance multi-tenant).
- Secuencia ordenada de **pasos** (steps): cada paso tiene tipo (Head, Extraction, Transformation, Execute, Export) y un script SQL asociado (salvo Export que además tiene formato PDF/Excel).

### 3.2 Tipos de pasos

| Tipo | Descripción | Entrada | Salida |
|------|-------------|---------|--------|
| **Head (Encabezado)** | Primera definición; SQL que define “campos de título” o metadato del documento. | Ninguna (o parámetros de ejecución). | Se guarda como primer bloque (ej. `head` en el estado del pipeline). |
| **Extraction (Extracción)** | SQL que consulta tablas de la BD. | Tablas de la BD (y opcionalmente tablas temporales o JSONB de pasos anteriores). | **Tabla temporal** en BD **o** JSONB con etiqueta (ej. `extraction_1`, `extraction_2`) en una tabla de estado del mapa. Permite que el siguiente paso use esos datos. |
| **Transformation (Transformación)** | SQL que trabaja sobre datos ya extraídos. | Tabla temporal o JSONB de pasos anteriores. | Otra tabla temporal o **JSONB** con etiqueta (ej. `transformation_1`). No toca tablas base; solo datos ya cargados en el pipeline. |
| **Execute (Ejecución)** | SQL que “juega” o calcula sobre los datos ya transformados. | Solo JSONB/tablas temporales del pipeline (no tablas operativas). | JSONB con etiqueta (ej. `execution_1`). Sirve para clasificar, organizar o preparar el dataset final. |
| **Export (Exportación)** | Paso final: toma el resultado del pipeline y genera el archivo. | Último JSONB o resultado del paso anterior. | **Archivo plano**: **PDF** o **Excel**. El usuario elige formato al configurar el mapa o en la ejecución. |

Flujo de datos resumido:

```
[Parámetros] → Head → Extraction(es) → Transformation(es) → Execute → Export → PDF/Excel
                   ↓                      ↓                    ↓
              temp table /           temp table /         JSONB
              JSONB (extraction_N)   JSONB (transformation_N)  (execution_N)
```

- Las **tablas temporales** son de sesión o de ejecución (creadas al correr el mapa, destruidas al terminar).
- Los **JSONB** por paso permiten encadenar sin tocar tanto las tablas operativas: se guardan en una estructura de “estado del pipeline” por ejecución (o en una tabla de contexto por ejecución).

### 3.3 Ejecución del mapa

- El usuario entra al mapa (desde **Mapa** o desde **Configuraciones** con “Play”).
- Se abre un **modal** con:
  - **Rango de fechas** (desde/hasta).
  - **Alcance**: toda la compañía, o por **granjas** (selección múltiple).
  - **Tipo de dato / filtro**: consumos, movimientos de huevos, “todo”, etc. (según lo que el mapa use en sus SQL).
- Al dar **Play**:
  - Se inicia el proceso en backend (cola o proceso asíncrono recomendado).
  - En el modal se muestra **barra de progreso** y estado (“Extrayendo…”, “Transformando…”, “Exportando…”). El backend debe exponer el **estado del proceso** (por paso o porcentaje) para actualizar la barra.
  - Al finalizar: **descarga del archivo** (PDF o Excel) y opcionalmente mensaje de éxito con link a historial.

### 3.4 Historial de ejecuciones

- Cada ejecución se guarda en una tabla **historial de mapas** (ej. `historial_mapa` o `map_execution_history`).
- Campos sugeridos:
  - **Mapa** (id del mapa).
  - **Usuario** que ejecutó.
  - **Fecha/hora** de ejecución.
  - **Rango de fechas** y **filtros** aplicados (granjas, tipo de dato, etc.) — idealmente JSONB para flexibilidad.
  - **Tipo de archivo** exportado (PDF / Excel).
  - **Payload/resultado**: **JSONB** con la información con la que se generó el archivo (o referencia al blob del archivo si se guarda en disco/storage). Objetivo: auditoría y trazabilidad (“qué pidió el usuario” y “qué devolvió la aplicación”).

En **Configuraciones**, en la lista de mapas, se puede mostrar **cuántas ejecuciones** tiene cada mapa (y opcionalmente última ejecución).

---

## 4. Modelo de datos propuesto

### 4.1 Tablas principales

| Tabla | Propósito |
|-------|-----------|
| **mapa** | Definición del mapa: nombre, descripción, company_id, pais_id (o JSONB de países/compañías permitidos), activo, orden de pasos (o se deriva de `mapa_paso.orden`). |
| **mapa_paso** | Pasos del mapa: mapa_id, orden, tipo (head, extraction, transformation, execute, export), nombre_etiqueta (ej. extraction_1), script_sql, opciones (JSONB: formato export PDF/Excel, etc.). Para Export: formato por defecto. |
| **mapa_ejecucion** (o **historial_mapa**) | Una fila por ejecución: mapa_id, usuario_id, fecha_ejecucion, parametros (JSONB: rango fechas, granjas, tipo dato), tipo_archivo (pdf/excel), resultado_json (JSONB) o ruta_archivo, estado (en_proceso, completado, error), company_id. |
| **mapa_ejecucion_log** (opcional) | Log por paso dentro de una ejecución: mapa_ejecucion_id, paso_orden, estado, mensaje, tiempo_ms. Útil para progreso y depuración. |

### 4.2 Alcance por compañía y país

- **mapa**: `company_id` (dueño o “todas” si es global), y/o `pais_id`, o un JSONB `alcance` con lista de company_ids y pais_ids que pueden ver el mapa.
- En backend, al listar mapas y al ejecutar: filtrar por compañía/país del usuario actual.

### 4.3 Tablas temporales y JSONB durante la ejecución

- **Temp tables**: creadas en la misma transacción/sesión de la ejecución del mapa; nombres únicos por ejecución (ej. `tmp_map_<ejecucion_id>_extraction_1`) para no colisionar entre ejecuciones paralelas.
- **JSONB**: se puede guardar en memoria durante la ejecución y solo persistir el **resultado final** (o por paso) en `mapa_ejecucion.resultado_json` para historial. No es obligatorio tener una tabla “estado del pipeline” persistida; puede ser un DTO en backend que se rellena paso a paso y al final se serializa a JSONB en `mapa_ejecucion`.

---

## 5. Flujos de pantalla

### 5.1 Configuraciones (lista de mapas)

- Listado: nombre, descripción, fecha de creación, **número de ejecuciones**, última ejecución (opcional).
- Acciones: **Crear mapa**, **Editar** (entra a configurar pasos), **Play** (abre modal de ejecución con parámetros).
- Al hacer clic en un mapa (o en “Configurar”): se abre la vista de **configuración del mapa** (pasos).

### 5.2 Configuración de un mapa (pasos)

- Datos generales del mapa: nombre, descripción, compañía, país (o alcance).
- Sección **Encabezado (Head)**: textarea/código para SQL del head; guardar.
- Botones **Añadir paso** con tipo:
  - **Extracción**: SQL; salida → tabla temporal o JSONB con etiqueta (ej. `extraction_2`).
  - **Transformación**: SQL sobre datos previos; salida → tabla temporal o JSONB.
  - **Ejecución**: SQL sobre JSONB/datos ya transformados; salida → JSONB.
- **Exportación**: SQL (o “usar resultado del paso anterior”) + selección de formato por defecto (PDF / Excel).
- Orden de pasos: arrastrar y soltar o campos “orden” editables.
- Guardar mapa / Guardar pasos.

### 5.3 Ejecución (modal Play)

- Parámetros: fecha desde, fecha hasta, alcance (todas las granjas / por granjas), tipo (consumos, movimientos de huevos, etc.).
- Botón **Play** → envía petición al backend con mapa_id y parámetros.
- Modal muestra:
  - Barra de progreso.
  - Texto de estado (“Extrayendo…”, “Transformando…”, “Generando archivo…”).
- Backend notifica fin (WebSocket, polling o respuesta larga): al completar se ofrece **Descargar archivo** y se guarda la fila en historial.

---

## 6. Backend: motor de ejecución de mapas

### 6.1 Responsabilidades

- Recibir mapa_id + parámetros (fechas, granjas, tipo).
- Cargar definición del mapa y sus pasos ordenados.
- Por cada paso:
  - **Head**: ejecutar SQL (con parámetros inyectados de forma segura); guardar resultado en contexto (DTO/JSONB).
  - **Extraction**: ejecutar SQL; si la salida es “tabla temporal”, crear la tabla y rellenarla; si es JSONB, leer resultado y guardar en contexto con la etiqueta del paso.
  - **Transformation / Execute**: ejecutar SQL pudiendo leer del contexto (tablas temp o JSONB de pasos anteriores); escribir salida en nueva temp o JSONB etiquetado.
  - **Export**: tomar el resultado del paso anterior (o SQL final), generar PDF o Excel (flat file) y devolver archivo para descarga; guardar en historial el JSONB y metadatos.
- Gestionar **transacciones** y **limpieza** de tablas temporales al terminar (éxito o error).
- Actualizar **estado de progreso** (tabla o señal para el front) para la barra de carga.

### 6.2 Seguridad y buenas prácticas

- **SQL dinámico**: los scripts son definidos por el usuario; hay riesgo de inyección si se concatenan parámetros. Usar **parámetros vinculados** para fechas, company_id, lista de granjas, etc. Evitar ejecutar DDL arbitrario (CREATE/DROP) salvo que se restrinja a nombres controlados (ej. prefijo `tmp_map_`).
- **Permisos de BD**: el usuario de la aplicación que ejecuta los mapas debería tener solo permisos de **SELECT** y de crear/drop tablas temporales en un esquema acotado, nunca DELETE/UPDATE sobre tablas operativas críticas sin control.
- **Timeouts**: límite de tiempo por paso y por ejecución total (ej. 30 s por paso, 5 min total).
- **Tamaño de resultado**: límite de filas o tamaño de JSONB para no saturar memoria; para exportaciones muy grandes, considerar streaming o trabajos en background con notificación.

### 6.3 Exportación a PDF/Excel

- **Excel**: librería (ej. NPOI, ClosedXML, EPPlus) para generar .xlsx con los datos del último paso (tabla en memoria o DataTable).
- **PDF**: librería (ej. QuestPDF, iText, DinkToPdf) para generar informe plano (tabla o lista) a partir del mismo dataset.
- El formato (PDF/Excel) puede ser configuración del paso Export del mapa y/o seleccionable en el modal de ejecución.

---

## 7. Frontend: integración con el resto de la aplicación

- El módulo **Mapas** vive junto al resto de módulos: mismo menú principal, mismo esquema de rutas (rutas bajo `/mapas/...`), mismos guards de autenticación y compañía.
- Listado de mapas y ejecución deben respetar **company_id** (y país si aplica): solo se ven y ejecutan mapas permitidos para la compañía del usuario.
- Reutilizar componentes existentes (filtros de granja, selectores de fecha, botones de descarga) para mantener coherencia visual y de UX.

---

## 8. Resumen de entregables

| Área | Entregable |
|------|------------|
| **Menú** | Item “Mapas” con subitems “Configuraciones” y “Mapa” (o rutas equivalentes). |
| **BD** | Tablas: mapa, mapa_paso, mapa_ejecucion (historial), opcional mapa_ejecucion_log. Script SQL para menú (insert en `menus`). |
| **Backend** | CRUD de mapas y pasos; endpoint de ejecución (mapa_id + parámetros); motor que ejecuta pasos en orden (Head → Extraction → Transformation → Execute → Export); generación PDF/Excel; guardado de historial con JSONB y filtros. |
| **Frontend** | Lista de mapas (Configuraciones); pantalla de configuración de mapa (pasos con tipo y SQL); modal de ejecución (parámetros + barra de progreso + descarga); posible vista de historial por mapa. |
| **Seguridad** | Filtro por company_id/pais; parámetros SQL vinculados; timeouts y límites de tamaño. |

---

## 9. Orden sugerido de implementación

1. **Modelo de datos**: tablas `mapa`, `mapa_paso`, `mapa_ejecucion` (+ script de menú).
2. **Backend**: entidades EF, repositorios/servicios CRUD para mapas y pasos.
3. **Backend**: motor de ejecución (un paso a la vez: Head, Extraction, Transformation, Execute) con tablas temporales y/o JSONB en memoria; sin Export aún.
4. **Backend**: paso Export (generación Excel y PDF) y guardado en historial.
5. **Backend**: endpoint de estado de progreso (polling o WebSocket) para la barra de carga.
6. **Frontend**: módulo Mapas, rutas, lista de mapas y formulario de creación/edición de mapa (datos generales + pasos).
7. **Frontend**: modal de ejecución (parámetros + Play + progreso + descarga).
8. **Frontend**: opcional vista de historial de ejecuciones por mapa.
9. **Ajustes**: permisos por rol (quién puede crear/editar/ejecutar mapas), timeouts y documentación de uso.

Este documento sirve como especificación para el desarrollo del módulo Mapas y puede refinarse con nombres definitivos de tablas/campos y detalles de API en una siguiente iteración.
