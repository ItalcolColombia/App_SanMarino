# Plan — Alinear nombres de Lote Pollo Engorde (Panamá) al lote base asignado

## Contexto / problema
La feature **Corrida por lote base + galpón (Panamá)** ya está en el código: al **crear** un lote de
pollo engorde en Panamá el backend arma el nombre como `"{lote base} - {corrida}"` (ej. `94 - 1`,
`94 - 2`), con `numero_corrida = MAX por (company, base, galpón) + 1`
(ver `Application/Calculos/GestionLotesEngordeCalculos.cs` y `LoteAveEngordeService.CreateAsync`).

Pero en **producción ya existen lotes de Panamá creados ANTES** de la feature: tienen un
`lote_base_engorde_id` asignado pero su `lote_nombre` es libre (no lleva el prefijo del lote base) y
`numero_corrida` está en NULL. Hay que **alinear esos nombres** al lote base que ya tienen asignado,
sin tocar los que ya cumplen ni los de otros países.

## Enfoque arquitectónico
**Migración EF idempotente de solo-datos (backfill DML)**, no cambia schema. Se aplica sola en el
deploy (`Database__RunMigrations=true`). Reusa **la misma regla ya testeada** del cálculo puro
(`ConstruirNombreCorrida` = `trim(base) + " - " + n`; numeración = MAX del grupo + 1), expresada como
UPDATE con window function para hacer el backfill en lote.

Como es **model-neutral** (solo `migrationBuilder.Sql`), **NO se toca `ZooSanMarinoContextModelSnapshot.cs`**
(el `BuildTargetModel` del Designer se deriva idéntico al snapshot actual). Esto evita el lock de
`API/bin` del backend del usuario corriendo y NO colisiona con las sesiones paralelas (mismo criterio
que la migración-seed de reproductora).

## Archivos a crear
- `backend/src/ZooSanMarino.Infrastructure/Migrations/20260722210000_FixNombresLoteEngordePanamaPorLoteBase.cs`
- `backend/src/ZooSanMarino.Infrastructure/Migrations/20260722210000_FixNombresLoteEngordePanamaPorLoteBase.Designer.cs`
  (derivado del Designer de `20260722190000`, cuyo `BuildTargetModel` se verificó **idéntico** al snapshot actual)
- `fase_de_desarrollo/fix_nombres_lote_engorde_panama_por_lote_base_plan.md` (este plan)
- `tracker_estado.md` — **append** de la sección (no se borra lo de las otras sesiones)

## Reglas de negocio del backfill (idénticas al runtime)
Alcance (lo que SÍ se toca), todo a la vez:
- País = **Panamá** — resuelto por nombre: `pais_id IN (SELECT pais_id FROM paises WHERE lower(pais_nombre) LIKE 'panam%')`
  (robusto a tilde/ID: en la BD el nombre es `Panama` sin tilde, `pais_id=3`).
- `lote_base_engorde_id IS NOT NULL` (hay base del cual derivar el nombre; el usuario pidió "según el lote base que tiene asignado").
- `galpon_id IS NOT NULL` (la corrida se numera por base **+ galpón**, igual que el runtime).
- `numero_corrida IS NULL` (aún sin numerar ⇒ nombre libre; hace el backfill **idempotente** y no pisa los ya correctos).

Asignación:
- `numero_corrida = (MAX(numero_corrida) existente del grupo (company, base, galpón), COALESCE 0)
  + ROW_NUMBER() OVER (PARTITION BY grupo ORDER BY lote_ave_engorde_id)`.
  → continúa **después** del máximo ya existente (no reusa números ya en uso por lotes creados por la feature).
- `lote_nombre = trim(COALESCE(base.nombre,'')) || ' - ' || numero_corrida` (idéntico a `ConstruirNombreCorrida`).
- MAX del grupo se calcula sobre **todas** las filas del grupo (incluye ya-numeradas y soft-deleted), igual que el `MaxAsync` del runtime (que no filtra `deleted_at`).

Lo que NO se toca:
- Lotes **sin** lote base (nombre libre, no hay de qué derivar) → intactos (coincide con el gate del runtime).
- **Ecuador / Colombia** (nombre libre; en el dump Ecuador tiene 0 lotes con base) → excluidos por el filtro de país.
- Lotes que ya cumplen (`numero_corrida` no NULL) → intactos.
- Columnas de auditoría (`updated_at/updated_by`) → intactas (mínima superficie; la traza queda en `__EFMigrationsHistory` + git).

`Down()`: **no-op documentado** (backfill de una vía; no se guardaron los nombres libres previos).

## Casos de prueba / validación
- **Simulación read-only contra la BD local (dump de prod)** — resultado esperado y obtenido:
  - Grupo (company 5, base 1 `94`, galpón `GALPON`): lote 139 ya `94 - 1` (corrida 1) ⇒ lote 138 `94`
    (corrida NULL) → **`94 - 2`** (continúa desde el max=1). ✔
  - 140 `92 - 1`, 141 `92 - 2` → intactos. ✔
  - 27 lotes sin base (109-137) → intactos. ✔
  - Ecuador (pais_id=2) con base = 0 filas → nada afectado. ✔
- `dotnet build` (0 errores; lock de `Infrastructure.dll` por el backend vivo del usuario = no es error de código).
- `dotnet test` (suite Application verde; sin regresión — la migración no cambia código C#).
- Los 9 tests de `GestionLotesEngordeCalculosTests` cubren la regla de nombre/incremento que el backfill reusa.

## Aplicación
- **Prod:** la aplica el deploy sola (idempotente). Verificación post-deploy: `SELECT` de lotes Panamá con base.
- **BD local :5433:** NO se aplica desde aquí (compartida con las sesiones paralelas + lock de bin). Se ofrece bajo pedido.
