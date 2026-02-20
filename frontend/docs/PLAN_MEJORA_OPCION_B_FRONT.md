# Plan de validación y mejora – Frontend (Opción B: Lote unificado con Fase)

Después de ejecutar el script de base de datos de la Opción B, este documento lista los módulos del frontend afectados y las tareas para alinear completamente cada uno con el nuevo modelo (lote unificado con `fase`, lotes hijos en producción, `LoteId` numérico en seguimiento).

---

## 1. Resumen del cambio en backend

| Antes | Después (Opción B) |
|-------|---------------------|
| Tabla `produccion_lotes` (registro inicial por lote padre) | Lote hijo en tabla `lotes` con `fase = 'Produccion'` y `lote_padre_id` |
| Seguimiento producción con `produccion_lote_id` o `lote_id` texto | Seguimiento con `lote_id` INTEGER FK a `lotes` (lote hijo en producción) |
| API ProduccionLote (CRUD separado) | API Produccion crea/obtiene “lote en producción” = lote hijo en `lotes` |
| ProduccionDiaria / SeguimientoProduccion con `LoteId` string en algunos DTOs | `LoteId` numérico en backend; DTOs pueden seguir enviando/recibiendo string en APIs que parsean |

---

## 2. Módulos afectados y estado

### 2.1 Lote Producción (módulo principal)

**Ruta:** `features/lote-produccion/`

**Archivos clave:**
- `services/produccion.service.ts` – API `/Produccion` (existe lote, crear lote, crear/actualizar/listar seguimiento)
- `services/produccion-lote.service.ts` – API `/ProduccionLote` (legacy)
- `services/lote-produccion.service.ts` – API `/ProduccionDiaria`
- `pages/lote-produccion-list/` – listado, filtro, registro inicial, seguimiento diario
- `pages/filtro-select/` – filtro por granja/núcleo/galpón/lote
- `pages/modal-registro-inicial/` – crear “registro inicial” = lote hijo Producción
- `pages/modal-seguimiento-diario/` – crear/editar seguimiento
- `pages/modal-detalle-seguimiento/`
- `components/tabs-principal/`, `tabla-lista-registro/`, `liquidacion-tecnica/`, etc.

**Impacto:**  
El backend ya devuelve “lote en producción” como lote hijo (su `lote_id` es el que se usa como `produccionLoteId` en seguimientos). Las interfaces del front ya usan `loteId` y `produccionLoteId` numéricos, alineados con el backend.

**Validar:**
- [ ] Tras elegir un lote padre y “Registro inicial”, se crea el lote hijo y el ID devuelto se usa correctamente en “Seguimiento diario”.
- [ ] Filtro `SeguimientoProduccion/filter-data`: que los lotes listados incluyan solo lotes con fase Producción o lotes padre que tengan hijo en producción, según lo que exponga el backend.
- [ ] Listado de seguimientos por `loteId` (padre o hijo según contrato del endpoint): comprobar que coincide con el nuevo modelo.

**Mejoras opcionales:**
- [ ] En UI, aclarar que “Lote en producción” es un lote hijo (ej. etiqueta “Lote producción” o “Lote hijo”).
- [x] ProduccionFlowManager sin ProduccionLoteService; UI con textos "Registro inicial" / "lote en fase Producción". Opcional: deprecar `produccion-lote.service.ts` si el backend retira `/ProduccionLote`.

---

### 2.2 Producción diaria (ProduccionDiaria API)

**Ruta:** `features/lote-produccion/services/lote-produccion.service.ts`

**API:** `GET/POST/PUT/DELETE /ProduccionDiaria`, `GET .../filter`, `GET .../check-config/:loteId`

**Impacto:**  
Backend usa `LoteId` numérico en SeguimientoProduccion. El DTO del backend para crear/actualizar puede seguir aceptando `LoteId` como string en el body (parseado a int). El front envía `loteId: number` en create/update y usa `number` en getByLote/filter.

**Validar:**
- [ ] Crear/editar registro de producción diaria con `loteId` numérico: comprobar que no hay error 400 y que el registro se guarda con el lote correcto.

**Mejoras realizadas:**
- [x] `checkProduccionLoteConfig(loteId: string | number)`: acepta número o string y convierte a string para la URL.

---

### 2.3 Traslados de huevos

**Ruta:** `features/traslados-huevos/`

**Archivos:** `services/traslados-huevos.service.ts`, `pages/traslado-huevos-form/`, `components/modal-traslado-huevos/`

**Impacto:**  
Interfaces usan `loteId: string` en DTOs (CrearTrasladoHuevosDto, TrasladoHuevosDto). El backend parsea a int para lógica interna. No es estrictamente necesario cambiar el front para que funcione.

**Validar:**
- [ ] Crear/editar traslado eligiendo un lote de producción: el valor enviado (string del lote) debe corresponder al `lote_id` del lote hijo (ej. `"123"`).
- [ ] Listado y detalle de traslados: que `loteId` y `loteNombre` se muestren bien.

**Mejoras:**
- [ ] Opcional: usar `loteId: number` en DTOs y convertir a string solo en el payload si el API lo requiere, para consistencia con el resto de la app.

---

### 2.4 Movimientos de aves

**Ruta:** `features/movimientos-aves/`

**Archivos:** `services/movimientos-aves.service.ts`, `components/modal-movimiento-aves/`, `pages/movimientos-aves-list/`

**Impacto:**  
El backend usa `LoteId` numérico en seguimiento producción. Los movimientos ya trabajan con `LoteOrigenId`/`LoteDestinoId` numéricos en backend. El front suele enviar/recibir IDs numéricos.

**Validar:**
- [ ] Alta de movimiento con lote origen/destino en producción: que el ID del lote (hijo) sea el correcto.
- [ ] Cálculo de descuentos en seguimiento producción (semana ≥ 26): que sigan aplicándose bien con el nuevo `lote_id` numérico.

---

### 2.5 Reporte contable

**Ruta:** `features/reporte-contable/`

**Archivos:** `services/reporte-contable.service.ts`, `pages/reporte-contable-main/`, `components/tabla-movimientos-huevos/`

**Impacto:**  
El reporte consume APIs que filtran por `lotePadreId` o listas de `loteIds` (int). El backend ya está alineado con int.

**Validar:**
- [ ] Generar reporte por lote padre: que incluya datos de lotes hijos en producción y de seguimiento_diario/produccion_diaria con `lote_id_int`/lote_id numérico.
- [ ] Tabla de movimientos de huevos: que `LoteId`/`loteNombre` se muestren correctamente (el backend puede devolver `LoteId` como int o string según DTO).

---

### 2.6 Reporte técnico producción

**Ruta:** `features/reporte-tecnico-produccion/`

**Archivos:** `services/reporte-tecnico-produccion.service.ts`, `pages/reporte-tecnico-produccion-main/`, tablas de reporte diario/cuadro/clasificación

**Impacto:**  
Las peticiones usan `loteId` (número) para obtener datos de seguimiento y ventas/traslados. Backend ya trabaja con int.

**Validar:**
- [ ] Selección de lote (padre o hijo según lo que pida el reporte): que los datos diarios y semanales de producción coincidan con el nuevo modelo.
- [ ] Clasificación huevo comercio y cuadros: que no haya errores por tipo de `loteId`.

---

### 2.7 Reportes técnicos (módulo unificado)

**Ruta:** `features/reportes-tecnicos/`

**Archivos:** `services/reporte-tecnico.service.ts`, `services/reporte-tecnico-produccion.service.ts`, componentes de tablas de datos diarios/semanales producción

**Impacto:**  
Mismo que 2.6: uso de `loteId` numérico en APIs.

**Validar:**
- [ ] Reporte técnico general con pestaña/tipo “Producción”: que los datos carguen bien para el lote seleccionado.

---

### 2.8 Indicador Ecuador

**Ruta:** `features/indicador-ecuador/`

**Archivos:** `services/indicador-ecuador.service.ts`, `pages/indicador-ecuador-list/`

**Impacto:**  
Consume APIs que filtran por `LoteId` (int en backend). Front suele pasar número desde el filtro.

**Validar:**
- [ ] Cálculo de indicadores por lote: que los datos de producción (seguimiento con `lote_id` int) se incluyan correctamente.

---

### 2.9 Dashboard y navegación de traslados

**Ruta:** `core/services/traslado-navigation/`, `features/dashboard/`

**Archivos:** `traslado-navigation.service.ts`, `dashboard.component.ts`

**Impacto:**  
Traslado-navigation usa `loteId` (number) para determinar si un lote es “Produccion” o “Levante”. El backend ya usa int en SeguimientoProduccion y Lotes.

**Validar:**
- [ ] En flujos de traslado (aves/huevos), que el tipo de lote (Producción vs Levante) se resuelva bien para lotes con `fase = 'Produccion'` y para lotes padre con hijo en producción.
- [ ] Dashboard: si muestra lotes o resúmenes por producción, que sigan siendo coherentes con el nuevo modelo.

---

### 2.10 Traslados aves / inventario

**Ruta:** `features/traslados-aves/`

**Archivos:** `services/traslados-aves.service.ts`, `pages/inventario-dashboard/`, `pages/traslado-form/`, etc.

**Impacto:**  
Disponibilidad y listados de lotes pueden usar `loteId` numérico. Backend ya unificado.

**Validar:**
- [ ] Inventario de aves por lote: que los lotes en producción (hijos) aparezcan donde corresponda y con IDs correctos.
- [ ] Formulario de traslado de aves: que la selección de lote origen/destino sea consistente con `lotes` (incluyendo fase Producción).

---

### 2.11 Filtros compartidos y selección de lote

**Ruta:** `shared/components/lote-filter/`, `shared/components/hierarchical-filter/`, `features/lote/`

**Impacto:**  
Si estos componentes devuelven o reciben `loteId` como string en algún flujo de producción, conviene alinear a número donde el backend es int.

**Validar:**
- [ ] Donde se use “lote” en contexto de producción (registro inicial, seguimiento, reportes): que el valor usado en la API sea el esperado (padre vs hijo según endpoint).
- [ ] Filtros que llaman a `filter-data` o similares: que la lista de lotes incluya o distinga correctamente lotes en fase Producción.

---

## 3. Plan de tareas por prioridad

### Fase 1 – Validación (sin cambios de código)

1. Recorrer **Lote Producción**: registro inicial → seguimiento diario → listado y edición. Comprobar que no hay 404/400 y que los IDs son correctos.
2. Recorrer **Traslados huevos**: crear traslado con lote de producción; ver que se guarde y se refleje en reportes.
3. Recorrer **Movimientos aves**: movimiento con lote en producción; ver descuentos en seguimiento si aplica.
4. Generar **Reporte contable** y **Reporte técnico producción** para un lote con producción y revisar que los números sean coherentes.
5. Revisar **Indicador Ecuador** y **Dashboard** con un lote en producción.

### Fase 2 – Ajustes menores (si algo falla)

1. Si algún endpoint espera `loteId` string y el front envía number: convertir a string en el cliente solo para esa llamada, o documentar y ajustar el backend.
2. Si `checkProduccionLoteConfig` o similares exigen string en la URL: mantener `loteId` como string en ese flujo o enviar `String(loteId)`.
3. Revisar DTOs de traslados huevos: si en algún momento el backend deja de aceptar string para `loteId`, actualizar interfaces y envío a número (o string según API).

### Fase 3 – Mejoras de alineación (opcional)

1. **Unificar tipo de lote en producción:** donde hoy se use `string` para `loteId` en contexto de producción, pasar a `number` y convertir a string solo donde la API lo requiera.
2. **Deprecar ProduccionLote en front:** si el backend retira `/ProduccionLote`, eliminar `produccion-lote.service.ts` y cualquier referencia; usar solo `/Produccion` (crear/obtener lote en producción = lote hijo).
3. **Textos y ayudas:** en pantallas de “Registro inicial” y “Seguimiento diario”, aclarar que el “lote en producción” es un lote hijo del lote padre (Levante), si mejora la comprensión del usuario.
4. **Filter-data:** si el backend expone un endpoint que devuelve “solo lotes que tienen fase Producción o tienen hijo en producción”, usar ese endpoint en el filtro del módulo Lote Producción para no mostrar lotes que no aplican.

---

## 4. Checklist final por módulo

| Módulo                 | Validación manual | Ajuste tipo loteId | Deprecar legacy | Notas |
|------------------------|-------------------|--------------------|-----------------|--------|
| Lote Producción        | [ ]               | [x]                | [ ] ProduccionLoteService opcional | Flow manager y mensajes alineados |
| ProduccionDiaria (LoteProduccionService) | [ ] | [x] check-config | -               |  |
| Traslados huevos      | [ ]               | [ ] opcional       | -               |  |
| Movimientos aves      | [ ]               | -                  | -               |  |
| Reporte contable      | [ ]               | -                  | -               |  |
| Reporte técnico prod. | [ ]               | -                  | -               |  |
| Reportes técnicos     | [ ]               | -                  | -               |  |
| Indicador Ecuador     | [ ]               | -                  | -               |  |
| Dashboard / Traslado nav. | [ ]            | -                  | -               |  |
| Traslados aves        | [ ]               | -                  | -               |  |
| Filtros / Lote        | [ ]               | [ ] si aplica      | -               |  |

---

## 5. Referencia rápida de APIs afectadas

- **`GET/POST /Produccion/lotes`** – Existe / Crear “lote en producción” (lote hijo). Respuesta: ID numérico (lote_id del hijo).
- **`GET /Produccion/lotes/:loteId`** – Detalle del lote en producción (por lote padre); backend devuelve el hijo.
- **`POST/PUT /Produccion/seguimiento`** – Body con `produccionLoteId` (number) = ID del lote hijo.
- **`GET /Produccion/seguimiento`** – Query `loteId` (número) = lote padre o hijo según implementación backend.
- **`GET /ProduccionDiaria/:loteId`** – `loteId` numérico (lote hijo en producción).
- **`POST /ProduccionDiaria`** – Body con `loteId`: backend puede aceptar string y parsear; recomendable enviar number.
- **`GET /ProduccionDiaria/check-config/:loteId`** – `loteId` en URL (string aceptado).
- **Traslados huevos / Movimientos aves** – DTOs con `loteId` string o number según contrato actual; backend ya trabaja con int internamente.

Con la base de datos ya migrada, la prioridad es **Fase 1 (validación)** en cada módulo; luego aplicar solo los ajustes que hagan falta y, si se desea, las mejoras de Fase 3.
