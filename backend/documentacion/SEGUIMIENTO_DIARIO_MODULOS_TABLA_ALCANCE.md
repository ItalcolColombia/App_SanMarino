# Seguimiento Diario – Módulos, tabla unificada y alcance

Documentación de lo realizado en cada módulo de seguimiento diario, la tabla que utiliza y el alcance actual.

---

## 1. Tabla unificada: `seguimiento_diario`

**Ubicación:** `public.seguimiento_diario` (PostgreSQL).  
**Script:** `backend/sql/create_seguimiento_diario_unificado.sql`

Contiene los tres tipos de seguimiento en una sola tabla, diferenciados por `tipo_seguimiento`:

| Valor            | Módulo        | Descripción                          |
|------------------|---------------|--------------------------------------|
| `'levante'`      | Lote Levante  | Seguimiento diario lote levante      |
| `'produccion'`   | Producción    | Seguimiento diario producción       |
| `'reproductora'` | Reproductora  | Seguimiento diario lote reproductora |

**Clave natural (unicidad):** `(tipo_seguimiento, lote_id, COALESCE(reproductora_id,''), fecha)`.

**Campos principales:**

- **Comunes (todos los tipos):** mortalidad_hembras/machos, sel_h/sel_m, error_sexaje_hembras/machos, consumo_kg_hembras/machos, tipo_alimento, observaciones, ciclo, peso_prom_hembras/machos, uniformidad_hembras/machos, cv_hembras/machos, consumo_agua_*, metadata, items_adicionales.
- **Solo reproductora:** peso_inicial, peso_final.
- **Solo levante:** kcal_al_h, prot_al_h, kcal_ave_h, prot_ave_h.
- **Solo producción:** huevo_*, peso_huevo, etapa, peso_h, peso_m, uniformidad, coeficiente_variacion, observaciones_pesaje.
- **Auditoría:** created_by_user_id, created_at, updated_at.

---

## 2. Módulo: Seguimiento Diario – Lote Levante

| Aspecto    | Detalle |
|-----------|---------|
| **Tabla** | `seguimiento_diario` con `tipo_seguimiento = 'levante'` |
| **API**   | `SeguimientoLoteLevanteController` (api/SeguimientoLoteLevante). POST, PUT, GET por id, GET filtrado por lote/fechas, DELETE. |
| **Servicio backend** | `SeguimientoLoteLevanteService` → usa `ISeguimientoDiarioService`. Convierte DTOs de levante a `CreateSeguimientoDiarioDto` / `UpdateSeguimientoDiarioDto` y persiste en la tabla unificada. |
| **Frontend** | Módulo `lote-levante` (seguimiento lote levante). |

**Lo realizado:**

- El módulo ya estaba migrado a la tabla unificada: create/update/list/get/delete pasan por `ISeguimientoDiarioService` y envían `tipo_seguimiento = 'levante'`.
- Los reportes y lógica que lean de `seguimiento_diario` con filtro por tipo levante consumen estos datos.

**Alcance:** Crear, editar, listar y eliminar seguimientos diarios de levante; todos los registros nuevos y actuales se almacenan en `seguimiento_diario`.

---

## 3. Módulo: Seguimiento Diario – Producción

| Aspecto    | Detalle |
|-----------|---------|
| **Tabla** | `seguimiento_diario` con `tipo_seguimiento = 'produccion'` |
| **API**   | `ProduccionController` (api/Produccion). POST `seguimiento`, PUT `seguimiento/{id}`, GET listado por lote, GET por id, DELETE por id. |
| **Servicio backend** | `ProduccionService`: `CrearSeguimientoAsync` y `ActualizarSeguimientoAsync` construyen `CreateSeguimientoDiarioDto` / `UpdateSeguimientoDiarioDto` y llaman a `ISeguimientoDiarioService`. Listado y eliminación también sobre la tabla unificada (filtro tipo producción). |
| **Frontend** | Módulo `lote-produccion`: modal de seguimiento diario (General, Hembras, Machos, Huevos, Pesaje, Agua). |

**Lo realizado:**

- Migración de creación y actualización a `seguimiento_diario` con tipo `'produccion'`.
- Endpoint PUT `seguimiento/{id}` para actualizar (antes solo POST).
- Request ampliado: error sexaje hembras/machos, ciclo, uniformidad/CV por sexo (alineado al esquema unificado).
- Modal: campos por sexo en tabs Hembras/Machos (error sexaje, uniformidad, CV); ciclo en General; create vs update bien diferenciados.

**Alcance:** Crear y actualizar seguimientos diarios de producción; listado y eliminación sobre la tabla unificada. Los nuevos registros y las ediciones se guardan solo en `seguimiento_diario`.

---

## 4. Módulo: Seguimiento Diario – Lote Reproductora

| Aspecto    | Detalle |
|-----------|---------|
| **Tabla** | `seguimiento_diario` con `tipo_seguimiento = 'reproductora'` |
| **API**   | `LoteSeguimientoController` (api/LoteSeguimiento). GET (opcional: loteId, reproductoraId, desde, hasta), GET `{id}`, POST, PUT `{id}`, DELETE `{id}`. |
| **Servicio backend** | `LoteSeguimientoService` → usa `ISeguimientoDiarioService`. Convierte `CreateLoteSeguimientoDto` / `UpdateLoteSeguimientoDto` a DTOs unificados con `TipoSeguimiento = "reproductora"` y persiste en `seguimiento_diario`. Mantiene validaciones de lote y reproductora en compañía. |
| **Frontend** | Módulo `seguimiento-diario-lote-reproductora`: listado por Granja → Núcleo → Galpón → Lote → Reproductora; modal con tabs General, Hembras, Machos, Pesaje, Agua. |

**Lo realizado:**

- Migración completa del servicio a la tabla unificada: create, update, get by id, get by lote+reproductora (con filtro de fechas) y delete usan `ISeguimientoDiarioService` y tipo `'reproductora'`.
- GET con query opcionales `loteId`, `reproductoraId`, `desde`, `hasta` para que el listado cargue solo los registros del lote y reproductora seleccionados (y rango de fechas si se envía).
- La API y los DTOs (`LoteSeguimientoDto`, Create/Update) se mantienen; el frontend no requiere cambios.

**Alcance:** Crear, editar, listar (filtrado por lote/reproductora/fechas) y eliminar seguimientos diarios de reproductora. Todos los registros nuevos y las operaciones de lectura/escritura del módulo se hacen contra `seguimiento_diario`. La tabla legacy `lote_seguimientos` ya no se escribe desde este módulo; otros servicios que sigan consultando solo `lote_seguimientos` no verán los registros nuevos (por ahora no se ha cambiado ese alcance).

---

## 5. Resumen por tabla

| Tabla                  | Uso actual |
|------------------------|------------|
| **seguimiento_diario** | Única tabla de escritura y lectura para los tres módulos (levante, producción, reproductora). Cada registro tiene `tipo_seguimiento` y los campos específicos del tipo; el resto en NULL. |
| **lote_seguimientos**  | Legacy. Ya no se escribe desde el módulo reproductora; puede seguir siendo leída por reportes u otros servicios hasta que se migren. |
| **Otras tablas**       | Levante y producción ya no usan tablas propias de seguimiento para create/update; todo va a `seguimiento_diario`. |

---

## 6. Alcance global

- **Unificación:** Los tres módulos de seguimiento diario (Levante, Producción, Reproductora) utilizan la misma tabla `seguimiento_diario` y el mismo servicio `ISeguimientoDiarioService`, diferenciando por `tipo_seguimiento`.
- **API y frontend:** Las APIs y pantallas actuales se mantienen; la migración es a nivel de persistencia en backend.
- **Reportes y otros consumos:** Por ahora no se ha modificado la lectura en servicios que usan `lote_seguimientos` o otras tablas legacy; si se desea incluir datos nuevos de reproductora en esos reportes, habría que añadir consultas a `seguimiento_diario` con `tipo_seguimiento = 'reproductora'`.
