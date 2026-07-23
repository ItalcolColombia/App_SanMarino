# Tracker — Código ERP de engorde por granja + avance automático al cerrar ciclo (Panamá)

**Plan:** [fase_de_desarrollo/codigo_erp_granja_engorde_panama_plan.md](fase_de_desarrollo/codigo_erp_granja_engorde_panama_plan.md)

## Backend
- [x] `Farm.cs`: propiedad `CodigoErpEngorde`
- [x] `FarmConfiguration.cs`: mapeo `codigo_erp_engorde` (varchar 20)
- [x] DTOs granja (`FarmDto`, `CreateFarmDto`, `UpdateFarmDto`, `FarmDetailDto`): campo al final
- [x] `FarmService`: Create/Update normaliza + valida (solo dígitos); todas las proyecciones incluyen el campo (ToFarmDtoListAsync ×2, GetByIdAsync ×2, Create, Update, GetByZonaUsuario, ProjectToDetail)
- [x] `GestionLotesEngordeCalculos`: `SiguienteCodigoErpGranja` + `EsCodigoErpGranjaValido` (puros)
- [x] `LoteAveEngordeService.CreateAsync`: estampa `LoteErp` desde la granja si tiene código
- [x] `LoteAveEngordeService.UpdateAsync`: conserva `LoteErp` almacenado si la granja tiene código
- [x] `LoteAveEngordeService.CerrarLoteAsync`: avance automático del código de la granja (guardas: lote base, código granja, ERP coincide, ningún otro abierto) en el mismo SaveChanges
- [x] Migración EF idempotente `20260723130810_AddCodigoErpEngordeToFarm` + snapshot regenerado
- [x] Tests xUnit del cálculo puro (incluye 4001099→4001100)
- [x] Build verde: Infrastructure 0 err/0 warn, API 0 err/0 warn (OutDir temporal, bin bloqueado por API corriendo) · `dotnet test` 619/619 ✔
- [x] Migración aplicada en BD local (:5433) — columna verificada con psql

## Frontend
- [x] `farm.service.ts`: `codigoErpEngorde` en `FarmDto`/`CreateFarmDto`
- [x] Form granja (sección Panamá): input `codigoErpEngorde` con validación numérica (pattern `^\d*$`, max 18) + help text; payload normalizado (trim, ''→null)
- [x] Form lote engorde: `loteErp` se autollena desde la granja (Panamá, `farmById` del form-data) y queda readonly; edición bloquea si ya capturó; granja sin código/otros países = editable como hoy
- [x] `yarn build` verde (solo warning preexistente de bundle budget)

## Ajuste post-revisión
- [x] Estampado en `CreateAsync` gateado por `AutoNombrePorCorrida` (flujo interactivo Panamá): protege la idempotencia del Puente Panamá (`LoteErp="PA-{id}"`) y el ERP explícito de la migración masiva Excel, que no mandan el flag

## Verificación
- [x] Compilación: Infrastructure 0 err/0 warn · API 0 err/0 warn · tests 619/619 · front OK
- [x] Migración `20260723130810_AddCodigoErpEngordeToFarm` aplicada en BD local (:5433); columna `farms.codigo_erp_engorde varchar(20)` verificada con psql (aplicó también las 2 pendientes de la sesión paralela: FixNombres… y SeedGuiaGenetica…)
- [ ] Smoke manual (requiere REINICIAR el backend local — el proceso que corre tiene binarios previos al cambio): granja Panamá con `4001017` → crear lote captura el código (readonly); cerrar última corrida del lote base avanza a `4001018`; re-cerrar lote reabierto no doble-avanza; granja sin código = comportamiento actual
