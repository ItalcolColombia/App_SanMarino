# Plan — CRUD de ubicación seguro: mover/editar/eliminar Núcleo · Galpón · Lote (transversal multipaís)

> **Objetivo:** que editar un núcleo/galpón, moverlos de ubicación o eliminarlos **nunca duplique registros ni deje lotes huérfanos**, y que el flujo sea coherente en Colombia, Ecuador y Panamá (mismas tablas/FKs). Incidente raíz en prod: editar el núcleo de un galpón creó otro galpón y hubo que migrar los lotes del galpón viejo por BD.
>
> **Regla rectora (CLAUDE.md):** el código actual es la fuente de verdad; refactor ≠ cambio de comportamiento; sin DDL en prod sin OK explícito; filtrado/cascada pesada se resuelve en la BD, no en memoria.

---

## 1. Diagnóstico de causa raíz (auditado en el código actual)

| # | Defecto | Evidencia | Efecto para el usuario |
|---|---|---|---|
| RC1 | La granja es **parte de la PK** del núcleo: `Nucleo` PK = `{ NucleoId, GranjaId }`. `UpdateAsync` solo cambia `NucleoNombre`; el controller exige `dto.GranjaId == ruta`. | `NucleoConfiguration.cs` `HasKey(x => new { x.NucleoId, x.GranjaId })`; `NucleoService.UpdateAsync`; `NucleoController` PUT `{nucleoId}/{granjaId}`. | "Cambio la granja del núcleo y no pasa nada / da error"; los galpones no se mueven. |
| RC2 | Mover un galpón **no arrastra sus lotes**. La ubicación del lote está denormalizada (`granja_id`, `nucleo_id`, `galpon_id`) con FK `Restrict`. | `GalponService.UpdateAsync` cambia `NucleoId/GranjaId` del galpón pero nada más; `Lote` cols + `LoteConfiguration` FKs `Restrict`. | El lote queda apuntando al núcleo/granja viejos → inconsistente / huérfano. |
| RC3 | `Galpon.CreateAsync` **auto-regenera el ID e inserta** si el `GalponId` ya existe (en vez de fallar). | `GalponService.CreateAsync` (bloques "ya existe, generando uno nuevo" + retry en `23505`). | Cualquier alta/edición mal enrutada → **galpón duplicado** silencioso (el incidente de prod). |
| RC4 | El **frontend de núcleo** deja editar `granjaId` en modo edición (no bloqueado) y arma el `PUT` con la granja nueva. | `nucleo-list.component.ts` `applyFormInModal` (sin `disable` de granja). | Edición de granja que "falla silenciosa" (404) o desincroniza. |
| RC5 | **Borrado sin validar hijos ni cascada** en núcleo y galpón (solo `DeletedAt`). | `NucleoService.DeleteAsync`, `GalponService.DeleteAsync`. | Núcleo/galpón borrado deja galpones/lotes activos colgando. |

**Modelo de datos (verdad de hoy):**
- `Nucleo` PK `{ NucleoId:string, GranjaId:int }` + `CompanyId`, `NucleoNombre`. FK→Farm(GranjaId) `Restrict`.
- `Galpon` PK `GalponId:string` (índice único `{CompanyId, GalponId}`). FK→Nucleo`{NucleoId,GranjaId}` `Restrict`, FK→Farm(GranjaId) `Restrict`.
- `Lote` PK `LoteId:int` (surrogate). Ubicación denormalizada `{GranjaId, NucleoId?, GalponId?}`. FK→Nucleo`{NucleoId,GranjaId}` `Restrict`, FK→Galpon(GalponId) `Restrict`, FK→Farm(GranjaId) `Restrict`.
- **Todas las FKs son `Restrict`** → ningún borrado/movimiento cascadea a nivel BD; hay que orquestarlo transaccionalmente.

---

## 2. Enfoque arquitectónico

**No re-diseñar la PK** (cambiar a surrogate en Núcleo tocaría FKs de galpones+lotes, historial EF y prod → riesgo tipo SIGSEGV; fuera de alcance). En su lugar:

1. **Operaciones "mover" explícitas y transaccionales** (endpoints dedicados), que arrastran a los hijos dentro de una transacción y validan destino/empresa/acceso. Un `move` no es un `create`.
2. **Blindar el alta** para que un `GalponId` duplicado **falle con 409**, nunca auto-genere+inserte.
3. **Blindar la edición** para que cambiar de ubicación pase por el `move` (que cascadea), no por un update parcial que desincroniza.
4. **Borrado con flujo completo**: contar hijos activos y **bloquear con mensaje claro** o cascada con confirmación (según nivel).
5. **Lógica pura → `Application/Calculos/UbicacionCalculos.cs`** (validaciones de destino, detección de colisión de re-key, conteos), con xUnit. Cascada/consultas pesadas **en la BD** (LINQ traducible o SQL), no en memoria (regla multipaís).
6. **País-agnóstico**: los `move` **no** tocan `lote_nombre` ni `numero_corrida` (naming de Ecuador/Panamá se preserva); solo columnas de ubicación.

**Semántica de "mover" por nivel:**

- **Lote** (surrogate PK, lo más simple y de mayor valor): update de `{GranjaId, NucleoId, GalponId}` del lote. Sin re-key. Validar que el galpón/núcleo destino existan y sean de la misma empresa/granja.
- **Galpón** (PK propia): update de `{NucleoId, GranjaId}` del galpón **+ cascada a sus lotes** (mismo `{NucleoId, GranjaId}`), en una transacción. `GalponId` no cambia → FK lote→galpón se mantiene.
- **Núcleo** (re-key, el más delicado): `{NucleoId, GranjaId}` → `{NucleoId, GranjaNuevaId}`. Como la granja es PK y las FKs son `Restrict`, se hace **insert-repoint-delete** en transacción: (1) insertar núcleo destino, (2) repointar galpones, (3) repointar lotes, (4) borrar núcleo origen. Colisión (`NucleoId` ya existe en granja destino) → **bloquear** con mensaje (merge fuera de alcance).

---

## 3. Fases (incrementales; recomiendo 0→1→2→4 como núcleo, 3 opcional/gated)

### Fase 0 — Blindaje inmediato anti-duplicado / anti-huérfano (mínima superficie)
Corta el sangrado sin endpoints nuevos.
- **Backend**
  - `GalponService.CreateAsync`: eliminar el auto-regenerar-ID. Si `GalponId` viene y ya existe (activo o borrado) → `throw InvalidOperationException` → **409** en el controller. Alta con ID vacío = generar **una** vez (helper existente) y si colisiona por carrera → error, no bucle.
  - `NucleoController.Update` / `GalponController.Update`: dejar explícito que **no** cambian de ubicación (documentar) — la ubicación va por `mover`.
- **Frontend**
  - `nucleo-list`: en **modo edición** bloquear el `<select>` de granja (como galpón bloquea `galponId`). Cambiar de granja se hace con "Mover".
  - `galpon-list`: garantizar que `editing` siempre llame `update` (ya lo hace) y que el `galponId` no se recalcule en edición (ya está `disable`d; agregar assert/guard).
- **Tests:** xUnit `CreateAsync` duplicado → excepción; front build.

### Fase 1 — Mover **Lote** de ubicación
- **DTO** `MoverLoteDto { LoteId, GranjaDestinoId, NucleoDestinoId?, GalponDestinoId? }`.
- **Servicio** `LoteService.MoverAsync`: validar destino (galpón∈núcleo∈granja, misma empresa, acceso del usuario); update de columnas dentro de transacción; **no** tocar `lote_nombre`/`numero_corrida`/`fase`.
- **Auditoría previa (bloqueante):** listar tablas que **denormalizan** la ubicación del lote además de `lotes` (p. ej. `seguimiento_diario_*`, movimientos, reportes materializados). Si alguna guarda `granja/nucleo/galpon`, cascada ahí también; si solo referencian `lote_id`, no se tocan. Documentar el hallazgo en el tracker.
- **Endpoint** `POST /api/Lote/{loteId}/mover`. **Front:** acción "Mover" en la lista de lotes (modal granja→núcleo→galpón dependientes).
- **Tests:** puro (validación destino) + integración (mover dentro de granja / a otra granja; rechazo destino inexistente / de otra empresa).

### Fase 2 — Mover **Galpón** (con cascada a lotes)
- **DTO** `MoverGalponDto { GalponId, GranjaDestinoId, NucleoDestinoId }`.
- **Servicio** `GalponService.MoverAsync`: validar destino; en transacción: update lotes del galpón a `{NucleoDestino, GranjaDestino}` → update galpón. Si el galpón tiene lotes con seguimiento, respetar auditoría de Fase 1.
- **Endpoint** `POST /api/Galpon/{galponId}/mover`. **Front:** acción "Mover" en `galpon-list` (separada de "Editar", que queda para nombre/medidas/tipo).
- **Regla:** editar galpón (nombre/ancho/largo/tipo) **no** cambia ubicación; cambiar ubicación **solo** por "Mover".
- **Tests:** puro + integración (galpón con N lotes → todos repuntados; conteos coherentes; sin duplicado).

### Fase 3 — Mover **Núcleo** a otra granja (re-key, OPCIONAL, gated por confirmación)
- **DTO** `MoverNucleoDto { NucleoId, GranjaOrigenId, GranjaDestinoId }`.
- **Servicio** `NucleoService.MoverAsync` (insert-repoint-delete en transacción):
  1. Validar destino (misma empresa, acceso); **colisión**: si `{NucleoId, GranjaDestino}` ya existe → bloquear (`InvalidOperationException` → 409) con mensaje claro.
  2. Insertar núcleo destino (copia nombre/company/audit).
  3. `UPDATE galpones SET granja_id=destino WHERE nucleo_id=@id AND granja_id=origen`.
  4. `UPDATE lotes SET granja_id=destino WHERE nucleo_id=@id AND granja_id=origen`.
  5. Borrar núcleo origen.
- **Endpoint** `POST /api/Nucleo/{nucleoId}/{granjaId}/mover`. **Front:** acción "Mover a otra granja" con `ConfirmDialogService` (impacto: N galpones, M lotes).
- **Tests:** integración (re-key completo, conteos, colisión bloqueada, rollback ante fallo).

### Fase 4 — Flujo completo de **eliminar** (guards + cascada con confirmación)
- **Núcleo `DeleteAsync`:** si tiene galpones o lotes **activos** → o (a) bloquear con mensaje ("tiene N galpones y M lotes; muévalos o elimínelos primero"), o (b) cascada soft-delete núcleo→galpones→(guard lotes). Recomendado: **bloquear si hay lotes con datos; cascada de galpones vacíos con confirmación.**
- **Galpón `DeleteAsync`:** si tiene lotes activos → bloquear (mensaje) o exigir mover/eliminar lotes antes.
- **Front:** `ConfirmDialogService` con el detalle del impacto; toasts de error del backend.
- **Tests:** puro (conteo de hijos → decisión) + integración (bloqueo con lotes; cascada de vacíos).

---

## 4. Archivos a crear / modificar

**Backend**
- `Application/Calculos/UbicacionCalculos.cs` *(nuevo, puro)* — validación de destino, colisión de re-key, decisión de borrado por conteos.
- `Application/DTOs/...` — `MoverLoteDto`, `MoverGalponDto`, `MoverNucleoDto`.
- `Application/Interfaces/` — firmas `MoverAsync` en `ILoteService`/`IGalponService`/`INucleoService`.
- `Infrastructure/Services/NucleoService.cs` — `MoverAsync`, guards en `DeleteAsync`, quitar cambio de ubicación de `UpdateAsync`.
- `Infrastructure/Services/GalponService.cs` — `MoverAsync`, guards en `DeleteAsync`, **quitar auto-regenerar-ID** de `CreateAsync` (409 en duplicado).
- `Infrastructure/Services/Lote*Service.cs` — `MoverAsync` + auditoría de tablas con ubicación denormalizada.
- `API/Controllers/{Nucleo,Galpon,Lote}Controller.cs` — endpoints `mover` + mapear 409/400.
- `tests/ZooSanMarino.Application.Tests/UbicacionCalculosTests.cs` *(nuevo)*.

**Frontend**
- `features/nucleo/...` — bloquear granja en edición; acción "Mover a otra granja"; `nucleo.service` `mover()`.
- `features/galpon/...` — separar "Editar" (datos) de "Mover"; `galpon.service` `mover()`.
- `features/lote*/...` — acción "Mover ubicación"; service `mover()`.
- Reusar primitivas: `ToastService`, `ConfirmDialogService` (métodos que confirman pasan a `async`), `GestionGranjasRefreshService.notify(...)`.

**BD/SQL** — Sin cambios de schema (no migración): las columnas ya existen. Los `move` son DML transaccional. Solo si la auditoría (Fase 1) encuentra tablas con ubicación denormalizada se decide si se cascadea por servicio o por función SQL en `/backend/sql/`.

---

## 5. Reglas de negocio
- Un `move` **jamás** crea un registro nuevo del mismo objeto (núcleo/galpón/lote) salvo el insert-repoint-delete interno del re-key de núcleo (transaccional, sin duplicado neto).
- Destino siempre validado: existe, misma empresa (`GetEffectiveCompanyIdAsync`), granja accesible por el usuario, y coherencia galpón∈núcleo∈granja.
- Los `move` no alteran nombres ni numeración de lote (naming multipaís preservado).
- Borrado: nunca dejar hijos activos colgando de un padre borrado.
- Todo `move`/`delete` con cascada corre en **una transacción** (rollback ante cualquier fallo).

## 6. Casos de prueba (equivalencia + nuevos)
1. Editar solo el **nombre** del núcleo → OK, sin tocar galpones/lotes.
2. Mover **núcleo** a otra granja → galpones y lotes repuntados; conteos coherentes; colisión bloqueada.
3. Mover **galpón** a otro núcleo → sus lotes repuntados; **cero duplicados**.
4. Mover **lote** dentro de la granja y a otra granja → ubicación actualizada; seguimientos intactos.
5. Alta de galpón con **ID duplicado** → 409 (no inserta duplicado).
6. Eliminar núcleo/galpón **con lotes** → bloqueado con mensaje; **sin** lotes → cascada/soft-delete OK.
7. Regresión multipaís: los flujos anteriores se comportan igual en CO/EC/PA (misma tabla, sin naming country-specific alterado).

## 7. Validación
- `cd backend && dotnet build` (0 errores/nuevos warnings) + `dotnet test`.
- `cd frontend && yarn build` (solo warning preexistente de bundle budget).
- Smoke local con stack vivo **bajo pedido** (BD `:5433` compartida con la sesión en paralelo del usuario → coordinar antes de migrar/mover datos).

## 8. Riesgos
- Re-key de núcleo (Fase 3) es el más delicado (insert-repoint-delete); por eso va gated por confirmación y con rollback probado. Recomiendo priorizar Fases 0–2 y 4.
- Tablas con ubicación denormalizada del lote aún no auditadas (bloqueante de Fase 1) — podría ampliar la cascada.
- Módulo transversal: cualquier cambio se prueba pensando en CO/EC/PA.
