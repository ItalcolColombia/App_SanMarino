# Tracker — Migraciones Masivas: línea ENGORDE (Lotes / Seguimiento / Venta)

Plan: [migraciones_masivas_engorde_plan.md](fase_de_desarrollo/migraciones_masivas_engorde_plan.md)
Contexto del módulo (Postura, Fases 0–2 ✅, Fase 3 documentada): [migraciones_masivas_plan.md](fase_de_desarrollo/migraciones_masivas_plan.md).

**Tarea activa:** agregar 3 tipos de migración masiva para pollo engorde, reusando patrones existentes.
Decisiones: Seguimiento = replicar efectos (vía `CreateAsync`); Venta = `Completado` + descuento único (trigger escribe `VENTA_AVES`); los 3 juntos.

---

## Fase A — Wiring base (catálogo + DI + frontend)
- [x] `TipoMigracion.cs`: +3 enum + 3 entradas de catálogo (Fase 4, Disponible, RequiereLote)
- [x] `MigracionService.cs`: inyectar `ILoteAveEngordeService` + `ISeguimientoAvesEngordeService`
- [x] `MigracionService.Operaciones.cs`: extender switch `GetElegibles`/`GenerarPlantilla`/`Procesar`
- [x] Front `models/migracion.model.ts`: +3 al union `TipoMigracionCodigo`
- [x] Front `selector-tipo-migracion.component.ts`: +3 íconos

## Fase B — Lotes Pollo Engorde (Estructura, reusa CreateAsync)
- [x] `MigracionService.EstructuraEngorde.cs`: `ProcesarLotesPolloEngordeAsync` (parse+valida+`CreateLoteAveEngordeDto`→`_loteAveEngordeService.CreateAsync` en TX)
- [x] Precarga: granjas/núcleos/galpones + combos Raza+Año de guía + granjas asignadas al usuario
- [x] Plantilla `GenerarPlantillaLotesPolloEngordeAsync` (Datos + Referencias + Instrucciones + dropdowns)
- [x] Validaciones: granja inexistente/no asignada, raza+año inexistente en guía, galpón/núcleo incoherente

## Fase C — Seguimiento Diario Engorde (reusa CreateAsync, replica efectos)
- [x] `MigracionService.SeguimientoEngorde.cs`: `ElegiblesEngordeAsync` (LoteAveEngorde no Cerrado, filtro jerárquico)
- [x] `ProcesarSeguimientoEngordeAsync`: precarga fechas existentes (idempotencia), arma `CreateSeguimientoLoteLevanteRequest`→`ToDto()`→`CreateAsync` fila por fila (sin TX externa)
- [x] Plantilla por lote `GenerarPlantillaSeguimientoEngordeAsync`
- [x] Guarda lote Cerrado + colisión días 1-7 `origen_cruce` (vía filtro fechas)

## Fase D — Venta Pollo Engorde (fn plpgsql, Completado + descuento único)
- [x] `backend/sql/fn_migracion_venta_engorde.sql` (loop idempotente: insert Completado + numero MPE + descuento contador; trigger hace VENTA_AVES)
- [x] Migración EF que embebe la fn (`CREATE OR REPLACE` idempotente + `Down` DROP) — `20260712190000_AddFnMigracionVentaEngorde`
- [x] `MigracionService.VentaEngorde.cs`: `ProcesarVentaEngordeAsync` (parse+valida+`SqlQueryRaw`)
- [x] Plantilla por lote `GenerarPlantillaVentaEngordeAsync`

## Fase E — Cálculo puro + tests
- [x] Helpers puros: sin funciones nuevas (peso neto/promedio viven en la fn SQL; coerción reusa `MigracionCalculos` ya testeado)
- [x] Smoke psql de `fn_migracion_venta_engorde` ✅ (insert 1 + numero MPE + neto/prom + descuento 446→436/327→322 + VENTA_AVES por trigger con ref al numero final + idempotente 0 + empresa ajena 0 + ROLLBACK)

## Fase F — Verificación
- [x] `dotnet build` Infrastructure 0/0 (API bloqueado por backend en marcha — no es error de código; API compila, solo falla el copy al bin en uso)
- [x] `dotnet test` 267/267 verdes
- [x] `yarn build` OK (solo warning de bundle budget preexistente)
- [x] Riesgo empresa-activa VERIFICADO: `HttpCurrentUser.CompanyId` toma el CompanyId efectivo del middleware (header X-Active-Company) → reusar `SeguimientoAvesEngordeService.CreateAsync` respeta la empresa activa
- [~] E2E: Venta verificada end-to-end por psql (insert/idempotencia/descuento/trigger VENTA_AVES/tenant). Lotes y Seguimiento reusan servicios ya testeados. **Falta E2E por UI** (requiere reiniciar el backend en marcha del usuario — no forzado)
- [ ] `make down` / procesos detenidos (el backend en marcha es del usuario; no se toca)

## Notas de avance
- Todo el código nuevo compila (Infrastructure 0/0). El único bloqueo de build es el DLL del API tomado por el backend del usuario en ejecución (PID viejo) — no es error de código.
- `_current.UserId` y `AuditableEntity.CreatedByUserId` son **int** → la fn de venta usa `p_usuario integer` (no text como levante).
- La fn `fn_migracion_venta_engorde` ya está aplicada en la BD local (CREATE OR REPLACE); el smoke-test corrió en TX con ROLLBACK (sin datos persistidos).
- Pendiente para cerrar: E2E por UI tras reinicio del backend (mostrará los 3 tiles nuevos vía /tipos).

---

## Notas de avance
- (se completa a medida que avanza)
