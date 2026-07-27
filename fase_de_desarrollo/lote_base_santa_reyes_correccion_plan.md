# Plan — Corrección migración Santa Reyes: lotes del Excel → LOTE BASE (no lotes seguimiento)

**Fecha:** 2026-07-25 · **Estado BD:** local aplicada hasta `20260726000000` · **Prod:** cadena SR sin push (nada desplegado)

## Diagnóstico

El seed `20260725190000_SeedEmpresaSantaReyes` (bloque 13) creó los 10 lotes del Excel del cliente
(`Requerimiento Santa reyes/Lotes.xlsx`) directamente en `public.lotes` (tab **Lotes Seguimientos**) con
espejos `lote_etapa_levante` + `lote_postura_levante` (33-42) + `lote_postura_produccion` (20-28, 9 en fase
Producción) e históricos por trigger. **Eso es incorrecto**: el Excel NO trae aves de encasetamiento
(hembras/machos NULL, "Aves encaset. = 0" en la UI) — son definiciones de lote, es decir **LOTE BASE**
(`lote_postura_base`), el nodo desde el que luego se crean los lotes seguimiento reales.

Columnas del Excel `Lotes.xlsx` vs campos actuales de `lote_postura_base`:

| Excel | lote_postura_base hoy | Falta |
|---|---|---|
| Lote | `lote_nombre` | — |
| Ccosto y Extensión (G3002216) | `codigo_erp` | — |
| Desc. Ccostos | ✗ | **`descripcion_erp`** |
| Raza | ✗ | **`raza`** |
| Tipo Ave (ROJA/BLANCA) | ✗ | **`tipo_linea`** |
| Granja | `farm_id` | — |
| Fecha Encasetamiento | ✗ | **`fecha_encaset`** (date; `erp_create` es otra cosa: fecha de creación en el ERP) |

**Estado real BD local (verificado):**
- Santa Reyes (company 7, granja 110 "La Esperanza"): lotes 141-150 del seed (created_by 1, 25-jul 13:31),
  sin seguimientos/traslados/cohortes/movimientos/liquidaciones. Limpieza segura.
- Demo (company 4, granja 88 "LA ESPERANZA"): lotes de PRUEBA manual con nombres del cliente
  `LOTE 217` (125) y `LOTE 234` (126), creados 2026-07-17 (previos a la migración), 126 con 10 filas en
  `seguimiento_diario_levante` (por `lote_id` varchar; `lote_id_int` sin filas). El usuario pide limpiarlos.
- FKs: `historico_lote_postura` → lpl/lpp es **CASCADE**; `lpp → lpl` es RESTRICT (borrar lpp antes que lpl).
  Sin datos en `reporte_tecnico_guia`, `lote_aves_cohortes`, `traslado_huevos`, `movimiento_aves`,
  `historial_inventario`, `inventario_aves`, `lote_galpones/reproductoras/seguimientos`, `produccion_*`.

## Enfoque arquitectónico

**Fix-forward** con UNA migración nueva `20260726030933_AddCamposLoteBaseYMoverLotesSantaReyes`
(no se reescribe el seed ya aplicado; regla "no romper historial EF"). El id generado (UTC) ya ordena
DESPUÉS de `20260726000000`, sin renombre manual. En prod la cadena completa corre
en el mismo deploy: el seed crea los lotes y esta migración los reubica de inmediato — neto correcto.

1. **Schema (EF + idempotente):** 4 columnas nuevas nullable en `lote_postura_base`:
   `descripcion_erp varchar(200)`, `raza varchar(100)`, `tipo_linea varchar(50)`, `fecha_encaset date`.
   Entidad + Configuration actualizadas → `dotnet ef migrations add` (tools EF 10 de `~/.dotnet/tools-ef10`,
   desde Infrastructure) → editar `Up()` a `ADD COLUMN IF NOT EXISTS`.
2. **Data (DO block idempotente en la misma migración):**
   - **A) SR:** borrar lotes seed + espejos (orden: lpp → lpl → etapa → lotes; histórico cascadea).
     Guardas: company/granja por nombre, los 10 nombres, `created_by_user_id = 1`, `hembras_l IS NULL`,
     y SIN seguimientos (levante por texto e int, producción por int) — si alguien registró datos, ese lote NO se toca.
   - **B) Demo:** borrar lotes de prueba con nombres del cliente en granja `LA ESPERANZA` de Demo
     **solo si `created_at < 2026-07-20`** (protege lo que el cliente cree evaluando en prod post-25jul),
     incluyendo sus `seguimiento_diario_levante/_produccion`, espejos y etapa.
   - **C) SR:** insertar los 10 **lote base** (datos VERBATIM del Excel, cantidades 0, `erp_create` NULL,
     granja La Esperanza, país Colombia, created_by 1) con `WHERE NOT EXISTS` + UPDATE de alineación con
     guarda `IS DISTINCT FROM`.
   - `Down()`: borra los 10 lote base de SR (best-effort) + `DROP COLUMN IF EXISTS` de las 4 columnas.
3. **Sin tocar Demo con seeds nuevos** (Demo ya tiene lotes base propios para evaluar el flujo).

## Archivos a modificar

**Backend**
- `Domain/Entities/LotePosturaBase.cs` — 4 props nuevas.
- `Infrastructure/Persistence/Configurations/LotePosturaBaseConfiguration.cs` — mapeo de columnas.
- `Application/DTOs/LotePosturaBaseDto.cs` — `LotePosturaBaseDto`, `CreateLotePosturaBaseDto`, `UpdateLotePosturaBaseDto`.
- `Infrastructure/Services/LotePosturaBaseService.cs` — Create/Update/Map (único sitio que construye el DTO).
- `Infrastructure/Migrations/20260726030933_AddCamposLoteBaseYMoverLotesSantaReyes.cs` (+Designer, snapshot vía EF).

**Frontend**
- `features/lote/services/lote-postura-base.service.ts` — 4 campos en las 3 interfaces.
- `features/lote/components/lote-list/lote-list.component.ts` — `initBaseForm`, `openBaseModal`, `saveBase`.
- `features/lote/components/lote-list/lote-list.component.html` — thead/fila tab Lote Base (+Raza, Tipo línea,
  F. encaset), colspan del empty-state, modal form (4 inputs) y modal detalle (4 campos).

## Reglas de negocio

- Lote base = definición sin aves encasetadas; los campos nuevos son **opcionales y neutrales** (sin gating por
  flag: `codigo_erp`/`erp_create` ya existen sin gate para todas las empresas).
- Refactor de datos ≠ cambio de comportamiento para el resto: empresas sin estos campos ven `—`.
- Cantidades del lote base SR = 0 (el Excel no trae aves).

## Casos de prueba / validación

1. `dotnet build` 0 errores; `dotnet ef database update` local OK.
2. BD local post-migración: `lotes` de SR = 0; espejos/etapa/histórico de 141-150 = 0; Demo 125/126 y sus 10
   seguimientos eliminados; `lote_postura_base` SR = 10 filas con codigo/desc/raza/línea/fecha correctos.
3. Re-ejecución de la migración (idempotencia): 0 cambios.
4. `dotnet test` verde (ningún test construye `LotePosturaBaseDto`).
5. `cd frontend && yarn build` 0 errores (solo warning de bundle budget preexistente).
6. Smoke UI (manual usuario): tab Lote Base muestra los 10 lotes con los datos del Excel; tab Lotes
   Seguimientos de SR vacío; modal crear/editar lote base captura los 4 campos nuevos.
