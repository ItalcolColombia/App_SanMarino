# Plan — Inventario Gestión: scoping multi-empresa / multi-país consistente + ítems de Panamá

## Contexto y diagnóstico (verificado en código + BD local :5433 tipo-prod)

El módulo **Gestión de Inventario** (`gestion-inventario`, back `InventarioGestionService` + `ItemInventarioService`)
debe funcionar multi-empresa/multi-país (Colombia, Ecuador, Panamá). Reporte del usuario (sesión activa en
**ItalcolPanama / Panamá**): las granjas traen las de "pollo engorde" y el catálogo de alimento trae solo Ecuador.

### Hechos confirmados
- **Una sola tabla `farms`** + `user_farms`. El engorde/postura NO se distingue en la granja, sino en el **lote**
  (`lote_ave_engorde` vs `lote_postura_base`/`lotes`). No existe "otra tabla" de granjas.
- **Panamá (company 5, país 3 = ItalcolPanama)** es **engorde puro**: 9 granjas, 128 lotes de engorde, **0 lotes de
  postura**, **0 ítems** de catálogo, 1 usuario miembro (`131eb3a2…`) asignado a las 9 granjas.
  → Decisión del usuario: en Panamá el inventario sirve a **ENGORDE**; las 9 granjas son correctas; faltan ítems.
- Datos catálogo por empresa/país: Colombia (1/1)=62, Ecuador (3/2)=146, Demo (4/1)=1, **Panamá (5/3)=0**.

### Causa raíz del síntoma "alimento trae Ecuador"
`ItemInventarioService.GetAllAsync` (`backend/src/ZooSanMarino.Infrastructure/Services/ItemInventarioService.cs:21`)
resuelve la empresa por **`_current.CompanyId` directo**. Si el middleware no dejó `EffectiveCompanyId` seteado en
ese request (p.ej. en Panamá), `_current.CompanyId` no queda en 5 y el `if (_current.CompanyId > 0)` **no filtra →
devuelve TODO el catálogo** (los 146 de Ecuador dominan). En cambio las **granjas** resuelven bien porque
`InventarioGestionService.GetEffectiveCompanyIdAsync` resuelve la empresa por **nombre** del header
`X-Active-Company` (`ICompanyResolver.GetCompanyIdByNameAsync`) antes de caer a `_current.CompanyId`.

→ **Los dos endpoints resuelven la empresa por caminos distintos** → granjas=Panamá, ítems=Ecuador.

Las **granjas NO son bug** (las 9 de Panamá son las correctas para engorde). El bug real y visible está en el
**catálogo de ítems**.

## Enfoque arquitectónico
Unificar `ItemInventarioService` al **mismo criterio de empresa efectiva** que ya usan `InventarioGestionService`
y el módulo de engorde (`MovimientoPolloEngordeFilterDataService.GetEffectiveCompanyIdAsync`): resolver por
**nombre del header** (`ActiveCompanyName` → `ICompanyResolver`) y caer a `_current.CompanyId`. Además **fallar
cerrado**: con sesión presente pero empresa no resoluble, devolver **vacío** (no todo el catálogo) para evitar la
fuga cross-tenant. País desde `_current.PaisId` (comportamiento actual, se conserva).

Refactor ≠ cambio de comportamiento para Colombia/Ecuador: ahí `ActiveCompanyName` resuelve a la misma empresa que
`_current.CompanyId`, así que el resultado no cambia (CO=62, EC=146). Solo cambia el caso roto (Panamá / empresa no
resuelta): pasa de "todo el catálogo (Ecuador)" a "los de la empresa activa" (Panamá → vacío hasta cargar datos).

## Archivos a modificar (backend)
- `backend/src/ZooSanMarino.Infrastructure/Services/ItemInventarioService.cs`
  - Inyectar `ICompanyResolver? companyResolver` (ya registrado en DI, scoped).
  - Nuevo helper `GetEffectiveCompanyIdAsync()` (nombre → resolver → `_current.CompanyId`), idéntico al patrón
    canónico de engorde/gestión.
  - `GetAllAsync`: usar empresa efectiva; **fail-closed** (sesión presente + empresa no resoluble ⇒ lista vacía);
    filtrar company + país (país solo si `PaisId>0`).
  - `GetByIdAsync`, `UpdateAsync`, `DeleteAsync`: filtrar por empresa efectiva (coherencia lectura/escritura).
  - `CreateAsync`, `CargaMasivaAsync`: escribir con empresa efectiva + país efectivo (para que la **carga de ítems
    de Panamá** aterrice en company 5 / país 3, no en la empresa del token). País: `_current.PaisId`, con fallback
    a `company_pais` (company→país) si el header de país no llegara.
- `InventarioGestionService.cs`: **sin cambios** (las granjas ya resuelven bien). Se documenta el porqué.

## Cambios de BD / SQL
- Ninguno de esquema.
- **Datos (Track B, aparte):** cargar los ítems de catálogo de Panamá (company 5, país 3), hoy 0. Vía "Carga
  masiva" (Excel) del propio módulo una vez arreglado el scoping, o seed SQL idempotente. **No** se toca prod sin OK
  explícito del usuario (solo local :5433 para validar).

## Reglas de negocio
1. Lecturas y escrituras del catálogo se acotan a la **empresa activa validada** (misma para granjas e ítems).
2. Nunca devolver el catálogo completo cuando hay sesión pero no se resuelve empresa (fail-closed).
3. Colombia/Ecuador: salida idéntica a hoy (no regресión).
4. Panamá: catálogo = ítems de company 5/país 3 (vacío hasta Track B); granjas = las 9 asignadas (sin cambio).

## Casos de prueba
- **Unit (xUnit, Application.Tests o Infrastructure test según harness):**
  - Empresa activa = Panamá (nombre resuelve 5) ⇒ `GetAllAsync` filtra company 5 (no Ecuador).
  - Empresa no resoluble + sesión presente ⇒ **lista vacía** (no todo el catálogo).
  - Empresa activa = Colombia/Ecuador ⇒ misma salida que hoy.
- **Build + tests:** `cd backend && dotnet build` (0 err, sin nuevos warnings) + `dotnet test` (verde).
- **Validación datos local:** tras Track B, en Panamá el catálogo trae ítems de Panamá; en Ecuador sigue trayendo 146.

## Fuera de alcance (documentado, requiere OK aparte)
- Endurecer la validación de pertenencia empresa (hoy el header por nombre no valida membresía en granjas/engorde
  tampoco) → hardening de tenant-isolation, tocar con cuidado por el superadmin. Se deja como follow-up.
- Deploy a producción (flujo controlado + verificación post-deploy).
