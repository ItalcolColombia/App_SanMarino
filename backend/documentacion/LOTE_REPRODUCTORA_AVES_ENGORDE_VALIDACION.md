# Lote Reproductora Aves de Engorde – Validación

## Funcionalidad del módulo

- **Listar** lotes reproductores asociados a cada **lote de pollo de engorde** (lote ave engorde).
- **Crear** lotes reproductores (uno o varios) por lote de pollo de engorde, usando las aves disponibles de ese lote.

---

## 1. Tabla en base de datos

| Aspecto | Detalle |
|--------|---------|
| **Tabla** | `public.lote_reproductora_ave_engorde` |
| **Script** | `backend/sql/create_lote_reproductora_ave_engorde.sql` |
| **Requisito** | Ejecutar antes `create_lote_ave_engorde.sql` (tabla referenciada por FK) |

**Columnas principales:** `id`, `lote_ave_engorde_id` (FK), `reproductora_id`, `nombre_lote`, `fecha_encasetamiento`, `m`, `h`, `aves_inicio_hembras`, `aves_inicio_machos`, `mixtas`, `mort_caja_h`, `mort_caja_m`, `unif_h`, `unif_m`, `peso_inicial_*`, `created_at`, `updated_at`.  
**Restricción:** UNIQUE (`lote_ave_engorde_id`, `reproductora_id`).

---

## 2. Backend (apunta a la nueva tabla)

| Capa | Archivo / elemento |
|------|--------------------|
| **Entidad** | `ZooSanMarino.Domain/Entities/LoteReproductoraAveEngorde.cs` |
| **EF Config** | `LoteReproductoraAveEngordeConfiguration.cs` → tabla `lote_reproductora_ave_engorde` |
| **DbContext** | `DbSet<LoteReproductoraAveEngorde> LoteReproductoraAveEngorde` |
| **DTOs** | `LoteReproductoraAveEngordeDto`, `CreateLoteReproductoraAveEngordeDto`, `UpdateLoteReproductoraAveEngordeDto`, `LoteReproductoraAveEngordeFilterDataDto` |
| **Servicio** | `ILoteReproductoraAveEngordeService` / `LoteReproductoraAveEngordeService` (CRUD, bulk, aves disponibles) |
| **Filter-data** | `ILoteReproductoraAveEngordeFilterDataService` / `LoteReproductoraAveEngordeFilterDataService` (granjas, núcleos, galpones, **lotes ave engorde**) |
| **Controller** | `LoteReproductoraAveEngordeController` – `api/LoteReproductoraAveEngorde` |

Todas las lecturas/escrituras del servicio usan **solo** `_ctx.LoteReproductoraAveEngorde` y filtro por compañía vía join con `LoteAveEngorde`.

---

## 3. API expuesta

| Método | Ruta | Uso |
|--------|------|-----|
| GET | `filter-data` | Filtros: granjas, núcleos, galpones, lotes ave engorde |
| GET | `?loteAveEngordeId=` | Listar registros (opcional por lote) |
| GET | `{id}` | Detalle por id |
| POST | `` | Crear uno |
| POST | `bulk` | Crear varios (mismo lote ave engorde) |
| PUT | `{id}` | Actualizar |
| DELETE | `{id}` | Eliminar |
| GET | `{loteAveEngordeId}/aves-disponibles` | Hembras/machos disponibles para asignar |

---

## 4. Frontend

- Ruta: `/config/lote-reproductora-ave-engorde`.
- Filtros en cascada: Granja → Núcleo → Galpón → **Lote Aves de Engorde** (pollo de engorde).
- Listado por lote seleccionado; botones Nuevo, Crear varios, Editar, Ver, Eliminar.
- Menú: script `add_lote_reproductora_ave_engorde_menu.sql` (ejecutar si el ítem no existe).

---

## 5. Checklist de puesta en marcha

1. [ ] Ejecutar en BD: `create_lote_ave_engorde.sql` (si no está).
2. [ ] Ejecutar en BD: `create_lote_reproductora_ave_engorde.sql`.
3. [ ] Ejecutar en BD: `add_lote_reproductora_ave_engorde_menu.sql` (para el ítem de menú).
4. [ ] Backend compilado y corriendo; probar `GET api/LoteReproductoraAveEngorde/filter-data` y `GET api/LoteReproductoraAveEngorde?loteAveEngordeId=1`.
5. [ ] Frontend: entrar a Config → Lote Reproductora Aves de Engorde; elegir lote de pollo de engorde; listar y crear al menos un lote reproductor.
