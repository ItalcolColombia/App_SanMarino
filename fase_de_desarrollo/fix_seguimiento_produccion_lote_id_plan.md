# Fix: Seguimiento diario de producción falla con "El lote postura producción no tiene LoteId asociado" (400)

**Fecha:** 2026-07-26 · **Empresa afectada hoy:** Demo (company 4) · **Alcance:** todas las empresas con postura (Demo, Santa Reyes, futuras)

## 1. Diagnóstico (verificado contra BD local = copia de prod)

- `POST /api/Produccion/seguimiento` con `lotePosturaProduccionId: 9` → 400.
- `ProduccionService.CrearSeguimientoAsync` (y `ActualizarSeguimientoAsync`) exige `lote_postura_produccion.lote_id > 0` porque `seguimiento_diario_produccion.lote_id` es **NOT NULL** en BD (verificado por information_schema). Ese requisito NO se puede relajar: indicadores, espejo huevo y reportes cuelgan de `lote_id`.
- Estado real en BD (copia prod):
  - LPP **#9** "P-LOTE 235A" (Demo, granja 90): `lote_id = NULL`, `lote_postura_levante_id = 16`. El levante 16 **SÍ** tiene `lote_id = 124` (lote vivo, `lotes.lote_id=124`).
  - LPP **#8** "P-k456C" (Demo, granja 87): `lote_id = NULL`, levante 11 → `lote_id = 119` (lote soft-deleted, fila existe → FK válida).
  - **0** levantes activos sin `lote_id` → el flujo de levante está sano.
- **Causa raíz:** los LPP se crean SOLO al cerrar un levante (`LotePosturaLevanteService.CrearLoteProduccion`). Ese método antes NO copiaba `LoteId`/`LotePadreId`; ya fue corregido en código (hoy hereda, línea ~128), pero las filas creadas ANTES del fix quedaron rotas. El backfill `backend/sql/backfill_lote_postura_produccion_lote_id.sql` fue escrito pero **nunca se aplicó a prod** (no es migración). Al copiar prod→local, el bug viajó.
- Nota terminológica: `lotes.lote_postura_base_id` (catálogo `lote_postura_base`) es **opcional por diseño** y NO es la causa del error; el faltante es `lote_postura_produccion.lote_id` → `lotes.lote_id`.

## 2. Enfoque (3 capas de defensa)

1. **Migración EF data-only idempotente** `BackfillLoteIdLotePosturaProduccion`: mismo UPDATE del script canónico (hereda `lote_id` y `lote_padre_id` desde el levante, solo filas con `lote_id NULL/<=0` y levante con `lote_id` válido). Prod se repara sola al deployar (`Database__RunMigrations=true`). Repara TODAS las empresas, no solo Demo.
2. **Self-heal en runtime** en `ProduccionService` (Crear + Actualizar): si el LPP no tiene `LoteId`, resolverlo desde su levante (sin filtrar soft-deleted: la referencia sigue siendo válida) y **persistir** la reparación en la fila LPP (`ExecuteUpdate`: `lote_id` + `lote_padre_id` si null). Continuar el guardado normal. Así el 400 no vuelve a ocurrir en ninguna empresa aunque aparezcan filas rotas por caminos futuros.
3. **Error claro solo si es irreparable** (sin levante o levante sin lote): mensaje en español que nombre el lote y la causa real.

La decisión "qué LoteId usar" es lógica pura → `Application/Calculos` + tests xUnit (patrón obligatorio del repo; gate CI).

## 3. Archivos a crear/modificar

| Acción | Archivo |
|---|---|
| Crear | `backend/src/ZooSanMarino.Application/Calculos/SeguimientoProduccionLoteIdCalculos.cs` — `static int? ResolverLoteIdEfectivo(int? loteIdProduccion, int? loteIdLevante)`: primer valor > 0, si no `null`. |
| Modificar | `backend/src/ZooSanMarino.Infrastructure/Services/ProduccionService.cs` — en `CrearSeguimientoAsync` y `ActualizarSeguimientoAsync`: resolver vía Calculos + query al levante; persistir heal; throw claro solo si no resoluble. |
| Crear | `backend/src/ZooSanMarino.Infrastructure/Migrations/20260726120000_BackfillLoteIdLotePosturaProduccion.cs` + `.Designer.cs` (Designer clonado de la última migración, clase/atributo renombrados; **ModelSnapshot NO se toca**). `Up()` = SQL idempotente; `Down()` no-op. |
| Crear | `backend/tests/ZooSanMarino.Application.Tests/SeguimientoProduccionLoteIdCalculosTests.cs` — xUnit `[Theory]` con los casos de la sección 5. |

Sin cambios de front (el modal ya muestra el mensaje del backend). Sin cambios de esquema (solo datos).

## 4. SQL de la migración (idéntico en efecto al script canónico)

```sql
UPDATE public.lote_postura_produccion p
SET lote_id       = lev.lote_id,
    lote_padre_id = COALESCE(p.lote_padre_id, lev.lote_padre_id)
FROM public.lote_postura_levante lev
WHERE p.lote_postura_levante_id = lev.lote_postura_levante_id
  AND (p.lote_id IS NULL OR p.lote_id <= 0)
  AND lev.lote_id IS NOT NULL AND lev.lote_id > 0;
```
Idempotente: re-ejecutar no toca filas ya sanas. No filtra `deleted_at` (reparar borrados es inofensivo y deja el dato consistente si se restauran).

## 5. Reglas de negocio y casos de prueba (Calculos puros)

| loteIdProduccion | loteIdLevante | Resultado |
|---|---|---|
| 124 | (cualquiera) | 124 (el propio manda) |
| null | 124 | 124 (hereda del levante) |
| 0 | 124 | 124 (0 = inválido) |
| -1 | 124 | 124 (negativo = inválido) |
| null | null | null → error claro |
| null | 0 | null → error claro |
| 5 | 9 | 5 (no pisa un valor válido) |

Runtime: si el resultado ≠ `lpp.LoteId` original → `ExecuteUpdate` sobre la fila LPP (heal persistente). El resto del flujo (duplicado por fecha, fecha futura, ítems, consumo B, espejo) queda **byte a byte idéntico**.

## 6. Validación

1. `cd backend && dotnet build` → 0 errores, sin advertencias nuevas.
2. `dotnet test` → tests nuevos + existentes verdes.
3. Migración local: `dotnet ef database update` (dotnet-ef 10 de `~/.dotnet/tools-ef10/`, desde Infrastructure con `--startup-project .`). Verificar por SQL: LPP #8 → 119, #9 → 124; 0 filas activas sin lote_id.
4. Smoke real: repetir el POST del usuario (lote #9, Demo) → 201/200 y fila en `seguimiento_diario_produccion` con `lote_id=124`.
5. Smoke empresa sana (flag OFF equivalente): el camino con `LoteId` ya presente no cambia (mismo código de entrada, resolución retorna el propio).

## 7. Despliegue

Merge a main → push a `main-produccion` → el deploy aplica la migración solo (EF al arrancar). Verificación post-deploy estándar (TaskDef/imagen) + reintentar el seguimiento en Demo prod.
