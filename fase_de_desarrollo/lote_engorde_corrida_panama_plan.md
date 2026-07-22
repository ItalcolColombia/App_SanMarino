# Plan — Numeración de corrida por lote base + galpón (Panamá) en Lote Pollo Engorde

> Sesión paralela a **Gestión de Granjas: cascada + refresco** (no tocar su tracker/plan). Esta mejora
> vive en el módulo `lote-engorde` (front) + `LoteAveEngorde` (back) y es **independiente**.

## Contexto / pedido del usuario

En el módulo donde se crean los **lotes de pollo engorde**, hoy en **Panamá** el "Nombre del lote" sale de
elegir un **lote base** (selector `loteBaseEngordeId`, obligatorio en Panamá) y el nombre se setea = `base.nombre`.

Se necesita que, al elegir **lote base + galpón**, el sistema valide cuántas veces ese lote base ya se abrió
**en ese mismo galpón** y asigne el **siguiente número de corrida**:

- Primer lote de la base `96` en el galpón → nombre **`96 - 1`** (referencia = 1).
- Si se vuelve a abrir la **misma base** en el **mismo galpón** → **`96 - 2`** (otro lote de la misma corrida,
  llegó en otra fecha pero pertenece al mismo lote base). En otro galpón la numeración arranca en 1.

Reglas:
- El **lote base** queda amarrado (ya se persiste `loteBaseEngordeId`); se agrega la **referencia** (`numero_corrida`).
- **Solo Panamá**: el lote base y el auto-nombre. Para otros países el lote base sigue **opcional** y el nombre es libre
  (comportamiento actual **intacto**).

## Enfoque arquitectónico

- **Fuente de verdad = backend** (evita duplicados por concurrencia / lista stale / soft-deletes). El backend
  asigna `numero_corrida` y arma `LoteNombre` al crear. El front muestra un **preview** en vivo del nombre.
- **Gate explícito por flag, NO por país en el backend**: el front (que ya sabe `isPanama()`) manda
  `autoNombrePorCorrida = true` solo en Panamá y solo al **crear**. El backend NO detecta país → se mantiene la
  filosofía "obligatoriedad/decisión de Panamá 100% en el front" y **no afecta al `PuentePanamaService`**
  (importa lotes vía `CreateAsync` sin ese flag → no se renombra) ni a Ecuador (base opcional, sin flag → nombre libre).
- **Cálculo puro en `Application/Calculos/`** (siguiente número + armado de nombre) con tests xUnit; la parte con
  BD (MAX por base+galpón) queda en el service. Regla CLAUDE.md: la BD filtra/agrega, el backend orquesta.
- **Sin recomputo en edición**: la corrida se fija al crear; `UpdateAsync` preserva `numero_corrida` y el nombre.
  El front en modo edición no reescribe `loteNombre`.

## Archivos a crear / modificar

### Backend

| Archivo | Cambio |
|---|---|
| `Domain/Entities/LoteAveEngorde.cs` | + `public int? NumeroCorrida { get; set; }` (referencia de corrida; NULL para países sin la feature). |
| `Infrastructure/.../Configurations/LoteAveEngordeConfiguration.cs` | + map `numero_corrida`; índice compuesto `(company_id, lote_base_engorde_id, galpon_id)` para el MAX. |
| `Infrastructure/Migrations/*_AddNumeroCorridaLoteAveEngorde.cs` | **Nueva** migración **idempotente** (`ADD COLUMN IF NOT EXISTS numero_corrida integer`, `CREATE INDEX IF NOT EXISTS`). |
| `Application/DTOs/CreateLoteAveEngordeDto.cs` | + `public bool AutoNombrePorCorrida { get; set; }` (default false). |
| `Application/DTOs/LoteAveEngorde/LoteAveEngordeDetailDto.cs` | + `int? NumeroCorrida = null` (exponer la referencia). |
| `Application/Calculos/GestionLotesEngordeCalculos.cs` | **Nueva** `static class`: `SiguienteNumeroCorrida(int? maxActual)` y `ConstruirNombreCorrida(string baseNombre, int numero)` (formato `"{base} - {n}"`). |
| `Infrastructure/Services/LoteAveEngordeService.cs` | `CreateAsync`: si `dto.AutoNombrePorCorrida && loteBaseId != null && galponId != null` → traer `base.Nombre`, `MAX(numero_corrida)` por `(company, base, galpon)`, calcular `numero`, setear `ent.NumeroCorrida` y `ent.LoteNombre`. `ProjectToDetail`: mapear `NumeroCorrida`. `UpdateAsync`: **no** tocar `NumeroCorrida`. |

### Frontend (`features/lote-engorde/`)

| Archivo | Cambio |
|---|---|
| `services/lote-engorde.service.ts` | `LoteAveEngordeDto`: + `numeroCorrida?: number \| null`. `CreateLoteAveEngordeDto`: + `autoNombrePorCorrida?: boolean`. |
| `components/lote-engorde-list/lote-engorde-list.component.ts` | Nuevo `recomputeNombrePanama()` (create-only): `n = max(numeroCorrida de this.lotes con misma base+galpón) + 1`; `loteNombre = "{base} - {n}"` + `nombreCorridaPreview`. Disparadores: valueChanges de `loteBaseEngordeId` **y** `galponId`, y tras las cascadas núcleo/galpón. En `save()`: `autoNombrePorCorrida = esPanama && !editing`. En edición NO reescribir nombre. |
| `components/lote-engorde-list/lote-engorde-list.component.html` | Panamá: bajo el selector de lote base, **preview** de solo lectura `Nombre del lote → 96 - 1`. Mostrar la referencia/`numero_corrida` en detalle (opcional). |

### Tests

| Archivo | Cambio |
|---|---|
| `tests/ZooSanMarino.Application.Tests/GestionLotesEngordeCalculosTests.cs` | **Nuevo** xUnit: siguiente número (NULL→1, N→N+1) y armado de nombre (`"96 - 1"`, trim). |

## Cambios de BD / SQL

- Columna `lote_ave_engorde.numero_corrida integer NULL` + índice `(company_id, lote_base_engorde_id, galpon_id)`.
- Migración **idempotente**; **sin backfill** (lotes previos quedan `numero_corrida = NULL`; una base ya usada
  antes de la feature reinicia en 1 → se documenta, no se pidió backfill).
- Aplicar en **BD local :5433**; en prod el deploy la aplica sola. Gotcha: si el backend del usuario está corriendo,
  generar la migración en **worktree aislado** / build a carpeta alterna (locks de `API/bin`), EF tools 10 con `.dotnet` user-local.

## Reglas de negocio

1. **Panamá + crear** con lote base y galpón elegidos → `numero_corrida = MAX(por company+base+galpon) + 1`;
   `loteNombre = "{base} - {numero}"`.
2. Numeración **por (lote base, galpón)**: otro galpón reinicia en 1.
3. **Otros países**: lote base opcional, nombre libre, `numero_corrida = NULL`. Sin cambios.
4. **Edición**: no recomputa corrida ni renombra.
5. Backend es autoritativo; el preview del front es informativo (la lista refresca con el valor real tras guardar).

## Casos de prueba

- `SiguienteNumeroCorrida(null) == 1`; `SiguienteNumeroCorrida(2) == 3`.
- `ConstruirNombreCorrida("96", 1) == "96 - 1"`; trim de espacios.
- Live (smoke): base `96` galpón A → `96 - 1`; repetir → `96 - 2`; base `96` galpón B → `96 - 1`.
- Ecuador: crear lote con nombre libre y sin base → OK, `numero_corrida` NULL (sin regresión).
- Puente Panamá (si aplica): importa sin `autoNombrePorCorrida` → nombre externo preservado.

## Validación

- `cd backend && dotnet build` (0 err, 0 warn nuevos) + `dotnet test` (todo verde).
- `cd frontend && yarn build` (0 err; solo warning de bundle budget preexistente).
- Smoke live en Panamá si el usuario reinicia su backend.
