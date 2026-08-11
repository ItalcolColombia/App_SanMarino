# Programación de lotes de engorde para Ecuador (lote base) + gasto contra lote programado

**Pedido (Ecuador):** «un módulo donde podamos tener todos los lotes que se van a encasetar en el año
para poder dar de baja los productos que se utilizan en la desinsectación para el lote que aún no se
encuentra activo, ya que hasta el momento solo tenemos los lotes creados por los técnicos de cada granja».

**Traducción al repo:** Ecuador necesita (1) el módulo **Lote base** que hoy solo ve Panamá —la
programación anual, asignada por granja, que alimenta el selector al crear el lote de engorde—, y
(2) poder cargar un **gasto de inventario contra un lote programado** que todavía no existe como
`lote_ave_engorde`.

---

## 0. Medición previa (BD local = dump de prod, 11-ago-2026)

| Empresa | Lotes base vivos | Lotes engorde | …con lote base | Gastos inventario |
|---|---|---|---|---|
| `ItalcolEcuador` (id 3, pais 2) | **0** | 121 | **0** | 369 (todos con lote real) |
| `ItalcolPanama` (id 5, pais 3) | 8 | 76 | 50 | 0 |

**Consecuencia operativa (riesgo #1):** con el flag encendido el lote base es **obligatorio**; si
Ecuador no tiene programación cargada, **nadie puede crear un lote**. Por eso el encendido de Ecuador
va en **migración propia y separada**, aplicable/revertible sin tocar la de Panamá.

## 1. Estado real del código (auditoría)

- El backend de lote base **ya es multi-empresa y country-agnostic**: `LoteBaseEngordeService`,
  `ILoteBaseEngordeService`, `LoteBaseEngordeController` (`/api/LoteBaseEngorde`), entidades
  `LoteBaseEngorde` + puente `LoteBaseEngordeGranja`. **No hay nada que portar en el back.**
- Lo único que gatea a Panamá es el **front**, por `CountryFilterService.isPanama()`:
  - `lote-engorde-list.component.html:26,41,64,301` → pestañas «Lotes» / «Lotes base».
  - `lote-engorde-list.component.html:430,602` → selector de lote base vs. nombre libre.
  - `lote-engorde-list.component.ts:189,194-201,224,364,898` → base obligatorio, recomputo de nombre
    por corrida y `autoNombrePorCorrida`.
  - ⚠️ `html:371,424-429` y `ts:346` son del **código ERP por granja** (otra feature) → **no se tocan**.
- El filtro por granja del selector (`granjaIds.includes(granjaSeleccionada)`) ya es genérico.
- `inventario_gasto` cuelga de `lote_ave_engorde_id` (nullable) — **no hay forma de apuntar a un lote
  programado**. La lista sale de la fn SQL `fn_inventario_gastos_search`.

## 2. Decisiones tomadas con el usuario

1. **Ecuador reutiliza el lote base igual que Panamá** (no se consume 1 a 1) ⇒ hereda la numeración de
   corrida existente: nombre `"{base} - {n}"` por galpón (`GestionLotesEngordeCalculos`, ya testeada).
2. **El nombre del lote sale obligatoriamente del base** en las empresas con la feature encendida.
3. **El gasto contra lote programado entra en esta entrega**, con **re-atribución automática** al
   crear el lote real.
4. **Gating por flag tipado en `companies`**, no por país (regla de CLAUDE.md). Se retira de paso el
   `isPanama()` de las rutas de lote base (el ERP por granja sigue con su propio gate).

## 3. Enfoque arquitectónico

### 3.1 Flag de empresa
- `companies.programacion_lotes_engorde` `boolean NOT NULL DEFAULT false`.
- Entidad `Company.ProgramacionLotesEngorde` + `CompanyDto` + **las 4 proyecciones**: `CompanyService.ToDto`,
  `CompanyService.Crud`, `CompanyResolver`, `CompanyPaisService` (+ `CreateCompanyDto`/`UpdateCompanyDto`).
- Front: `CompanyFlags.programacionLotesEngorde` en `ActiveCompanyConfigService` (fail-closed).

### 3.2 Front — Lote Pollo Engorde
- `esPanama` deja de gatear lote base; entra `programacionLotes` (del flag, resuelto en `ngOnInit`).
- La pestaña «Lotes base» (programación + «Asignar granjas») queda visible con el flag ON.
- Con flag ON: `loteBaseEngordeId` requerido, `loteNombre` se calcula (`recomputeNombrePanama` se
  renombra a `recomputeNombrePorCorrida`) y `autoNombrePorCorrida = flag && !editing`.
- Con flag OFF: **byte a byte lo de hoy** (nombre libre, base opcional, sin pestañas).

### 3.3 Backend — gasto contra lote programado
- `inventario_gasto.lote_base_engorde_id` `integer NULL` + FK a `lote_base_engorde` + índice parcial
  `WHERE lote_base_engorde_id IS NOT NULL AND lote_ave_engorde_id IS NULL` (los «pendientes»).
- Regla de integridad: **nunca ambos**. Validación en el service (400) + `CHECK` en BD:
  `NOT (lote_ave_engorde_id IS NOT NULL AND lote_base_engorde_id IS NOT NULL)`.
- `fn_inventario_gastos_search`: agrega `lote_base_engorde_id`, `lote_base_nombre` y el filtro
  `p_lote_base_id`. ⚠️ Cambia `RETURNS TABLE` ⇒ **`DROP FUNCTION` antes del `CREATE`** y espejo
  `backend/sql/fn_inventario_gastos_search.sql` actualizado **en el mismo commit**.
- **Re-atribución** al crear `LoteAveEngorde` con base B, granja F, galpón G, encaset E:
  ```
  UPDATE inventario_gasto
     SET lote_ave_engorde_id = <nuevo>, lote_base_engorde_id = NULL
   WHERE company_id = <cia> AND farm_id = F AND lote_base_engorde_id = B
     AND lote_ave_engorde_id IS NULL AND estado = 'Activo'
     AND (galpon_id IS NULL OR galpon_id = G)
     AND fecha <= E
  ```
  Determinista y monótona: el 2º lote del mismo base+galpón solo puede tomar gastos que el 1º no tomó
  (los anteriores a **su** encaset ya fueron reclamados). **No toca stock** — el descuento ya ocurrió al
  registrar el gasto; esto solo cambia la atribución.
- Lógica pura en `Application/Calculos/GastoLoteProgramadoCalculos.cs`
  (`ValidarDestinoGasto`, `DebeReatribuir`) + xUnit.

### 3.4 Front — Gastos de inventario
- Con flag ON, el selector de lote ofrece **«Lote programado (aún sin encasetar)»** además de los lotes
  reales; se envía `loteBaseEngordeId`. Con flag OFF el formulario queda idéntico.
- La lista muestra el nombre del base con distintivo «programado».

## 4. Archivos

**Backend**
- `Domain/Entities/Company.cs`, `InventarioGasto.cs`
- `Application/DTOs/CompanyDto.cs`, `CreateCompanyDto.cs`, `UpdateCompanyDto.cs`
- `Application/DTOs/InventarioGastoDtos.cs` (create/update/list/filtro)
- `Application/Calculos/GastoLoteProgramadoCalculos.cs` **(nuevo)**
- `Infrastructure/Persistence/Configurations/CompanyConfiguration.cs`, `InventarioGastoConfiguration.cs`
- `Infrastructure/Services/CompanyService*.cs`, `CompanyResolver.cs`, `CompanyPaisService.cs`
- `Infrastructure/Services/InventarioGastoService.cs`, `LoteAveEngordeService.cs`
- `backend/sql/fn_inventario_gastos_search.sql`
- Migraciones (4): `AddProgramacionLotesEngordeFlag`, `AddLoteBaseEngordeIdInventarioGasto`,
  `FnInventarioGastosSearchConLoteBase`, `SeedProgramacionLotesEngordePanama`,
  `SeedProgramacionLotesEngordeEcuador` *(separada, se aplica cuando la programación esté cargada)*
- `tests/ZooSanMarino.Application.Tests/GastoLoteProgramadoCalculosTests.cs` **(nuevo)**

**Frontend**
- `core/services/company-config/active-company-config.service.ts`
- `features/lote-engorde/components/lote-engorde-list/*` (ts + html)
- `features/gastos-inventario/**` (modelo, service, página)

## 5. Reglas de negocio

1. Flag OFF ⇒ comportamiento actual **idéntico** (nombre libre, base opcional, sin pestaña, gasto solo
   contra lote real).
2. Flag ON ⇒ base obligatorio al **crear**; en **edición** no se recalcula el nombre (la corrida se fija
   al crear) y se conserva el base guardado aunque ya no esté asignado a la granja.
3. Un lote base solo aparece en la granja a la que está asignado y si está `activo`.
4. Un gasto apunta a **lote real XOR lote programado**, nunca a ambos, nunca a un base de otra empresa.
5. La re-atribución solo mueve gastos `Activo`, sin lote real, de la misma granja y con
   `fecha <= fecha_encaset`.
6. Anular/eliminar el gasto sigue igual; el base **no se puede borrar** si tiene gastos pendientes
   (mismo criterio que «tiene lotes vivos amarrados»).

## 6. Casos de prueba

**xUnit (`GastoLoteProgramadoCalculosTests`)**
- Destino válido: solo lote real ✓ · solo base ✓ · ambos ✗ · ninguno ✓ (gasto de granja).
- `DebeReatribuir`: misma granja+base+galpón, fecha ≤ encaset ⇒ true.
- Galpón NULL en el gasto ⇒ true (gasto de granja programado).
- Galpón distinto ⇒ false. Otra granja ⇒ false. Fecha > encaset ⇒ false. Estado `Eliminado` ⇒ false.
- Ya atribuido a otro lote ⇒ false (no re-mueve).
- Segundo lote del mismo base+galpón ⇒ solo toma lo no reclamado.

**Integración / smoke**
- Empresa con flag **OFF** (Sanmarino/Demo): crear lote con nombre libre y sin base ⇒ sin cambios;
  crear gasto contra lote real ⇒ sin cambios. Ninguna pestaña nueva.
- Empresa **ON** (Panamá): comportamiento actual intacto (nombre `96 - 3`, corrida por galpón).
- Empresa **ON** (Ecuador): crear base «E-01» → asignar granja → gasto de desinsectación contra el
  programado → crear el lote → el gasto queda atribuido al lote real y desaparece de pendientes.
- Regresión: `GET /api/CuadreAlimentoEngorde` sigue en **1 descuadrado / 61 filas** (no debe moverse).

## 7. Validación

- `cd backend && dotnet build` (0 errores, sin warnings nuevos) + `dotnet test` (2.197 verdes + nuevos).
- `cd frontend && yarn build` (solo el warning de bundle budget preexistente).
- Migraciones aplicadas en BD local `:5433` levantando el backend en Development.
- `psql`: verificar que ninguna fila existente de `inventario_gasto` cambió
  (`count(lote_ave_engorde_id) = 369` antes y después).
