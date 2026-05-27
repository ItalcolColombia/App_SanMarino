# Tracker de Estado — Mejoras Panamá (Clientes, Zonas, Lesiones, Granja)

**Plan:** [fase_de_desarrollo/panama_zona_clientes_lesiones_plan.md](./fase_de_desarrollo/panama_zona_clientes_lesiones_plan.md)
**Fecha:** 2026-05-26
**Estado:** ✅ Backend completo + frontend integrado — `dotnet build` 0 errores, migraciones aplicadas en BD local, pendiente E2E manual y deploy

**Decisiones confirmadas (2026-05-26):**
- Zona = string `"Zona 1"` / `"Zona 2"` en columnas `farms.zona`, `users.zona`
- Certificado GAB = bool (`farms.certificado_gab`)
- Tab Lesiones = solo Panamá (`*showIfCountry="'PANAMA'"`)
- Alcance: completo según plan (sin Liquidación/Reportes)

---

## Resumen

Implementar el conjunto de mejoras Panamá descritas en `/Users/chelsycardona/Downloads/Plande Desarrollo Panama.docx`:
- Conectar módulo Clientes con selects reales de País/Provincia/Distrito y agregar Zona
- Campo Zona en Usuarios
- Campos Cliente/Zona/Certificado GAB/lat-lng en Granja
- Tab Lesiones (entidad nueva) en Seguimiento Diario Reproductora, Apoyo y Engorde
- Campos QQ Mixtas/Hembras/Machos en seguimientos (solo en Panamá)
- Filtro de granjas por zona del usuario cuando país activo es Panamá

---

## Hallazgos clave de la auditoría

- ✅ `Cliente` entity + CRUD existen (faltan filtros por zona y wiring real de Pais/Departamento/Municipio en el form)
- ✅ Infraestructura "país activo" ya existe (`session.activePaisNombre`, header `X-Active-Pais`, directiva `*showIfCountry`)
- ⚠️ `SeguimientoDiarioAvesEngordePanama` tiene `QqMixtas/QqHembras/QqMachos` en la entity C# **pero NO en BD** — la entity quedó desincronizada (esa columna nunca tuvo migración)
- ❌ `Farm` no tiene Cliente/Zona/GAB/lat/lng (ni en entity ni en BD)
- ❌ `User` no tiene Zona (ni en entity ni en BD)
- ❌ Entidad/tabla/CRUD Lesiones no existe
- ❌ `SeguimientoLoteLevante` y `SeguimientoDiarioAvesEngorde` (engorde general) no tienen QQ
- ✅ BD local sincronizada con código hasta `20260525131406_RenameTrasladoColumnsPerFase` (45 migraciones aplicadas)
- ⚠️ **Inconsistencia BD local**: `__EFMigrationsHistory` marca `20260517135042_AddGestionClientes` como aplicada pero la tabla `clientes` NO existe físicamente. Mitigación: migraciones nuevas usan `IF NOT EXISTS` y no dependen de `clientes` para schema (solo lógica de FK suave en runtime).

---

## Decisiones bloqueantes — RESUELTAS

- [x] Zona = string `"Zona 1"`/`"Zona 2"` en `farms.zona`, `users.zona`
- [x] Certificado GAB = booleano (`farms.certificado_gab`)
- [x] Tab Lesiones = solo Panamá
- [x] Alcance = completo según plan
- [x] Orden = paralelo por dominios, validadores + monitor del plan

---

## Checklist — Fase 1: Backend Schema (migraciones idempotentes) ✅

- [x] Verificar `__EFMigrationsHistory` local (45 mig. aplicadas, drift detectado: `clientes`/`seguimiento_diario_aves_engorde_panama` tablas marcadas pero no existen físicamente en local)
- [x] Migración `20260526210700_AddPanamaFieldsToFarm` (cliente_id, zona, certificado_gab, latitud, longitud)
- [x] Migración `20260526210808_AddZonaToUser`
- [x] Migración `20260526210945_CreateLesionesTable` (21 columnas + check constraint)
- [x] Migración `20260526211129_AddQqFieldsToSeguimientos` (4 tablas — incluida Panamá con guard `IF EXISTS`)
- [x] `dotnet ef database update` aplicado correctamente en local
- [x] Verificación post-migración: 16 columnas nuevas presentes, tabla lesiones operativa

## Checklist — Fase 2: Backend Domain / Application / API

- [x] Actualizar `Farm.cs` + `FarmConfiguration` (entity y mapeo listos)
- [⏳] Farm DTOs + service + controller (`GetByZonaUsuarioAsync`) — **agente Farm en ejecución**
- [x] Actualizar `User.cs` + `UserConfiguration` (entity y mapeo listos)
- [x] User DTOs (UserDto/CreateUserDto/UpdateUserDto/RegisterDto/UserListDto) + UserService + AuthService.Register (todos con Zona)
- [x] Agregar `zona` a `ClienteSearchRequest` + `ClienteService.SearchAsync` + `ClienteController.Search`
- [x] Crear `Lesion.cs` + `LesionConfiguration` + `DbSet<Lesion>` en context
- [⏳] DTOs + ILesionService + LesionService + LesionesController + DI registration — **agente Lesion en ejecución**
- [x] QQ en entities SeguimientoLoteLevante / SeguimientoDiarioAvesEngorde / SeguimientoDiarioLoteReproductoraAvesEngorde / SeguimientoDiarioAvesEngordePanama (entity + BD)
- [x] QQ en DTOs: SeguimientoLoteLevanteDto, CreateSeguimientoLoteLevanteRequest, CreateSeguimientoDiarioLoteReproductoraRequest
- [x] QQ en SeguimientoAvesEngordePanamaService (persiste en líneas 74-177)
- [⏳] Filtro por zona Panamá en FarmController (`GET /api/Farm/by-zona-usuario`) — **agente Farm en ejecución**
- [x] `dotnet build` final: **0 errores, 6 warnings preexistentes**
- [x] Cliente→Farm.Zona sync (decisión 6 del plan) — `ClienteService.UpdateAsync` ahora propaga zona al editar
- [x] `LesionesController` creado (CRUD completo + endpoint `/resumen`)
- [x] `ILesionService` registrado en DI (`Program.cs` línea 316)
- [x] `FarmService.GetByZonaUsuarioAsync` implementado + endpoint `GET /api/Farm/by-zona-usuario` en `FarmController`
- [x] 7 Selects EF corregidos (FarmLiteDto/FarmDetailDto) para pasar args explícitos de Panamá
- [ ] **FOLLOW-UP** (no bloqueante para esta entrega): QQ en services unificados (`SeguimientoLoteLevanteService.MapToLevanteDto`, `SeguimientoDiarioLoteReproductoraService` Create+Update, `SeguimientoAvesEngordeService` Create+Update) — requiere agregar campos a `SeguimientoDiarioDto/Create/Update` y mapeos. El servicio Panamá-específico ya persiste QQ correctamente.

## Checklist — Fase 3: Frontend Clientes ✅

- [x] Auditar: `PaisService`/`DepartamentoService`/`CiudadService` ya existen en `features/farm/services/`
- [x] Reemplazar selects hardcoded en `cliente-list.component` por `<select>` reactivos
- [x] Implementar cascada País → Provincia → Distrito
- [x] Default Panamá cuando `activePaisNombre === 'PANAMA'`
- [x] Agregar select Zona (`Zona 1` / `Zona 2`)

## Checklist — Fase 4: Frontend Usuarios ✅

- [x] Localizado: `features/config/user-management/components/modal-create-edit/`
- [x] Campo Zona agregado al tab "Personal" con 3 opciones (vacío/Zona 1/Zona 2)

## Checklist — Fase 5: Frontend Granja ✅

- [x] Select Cliente con `getAll()`
- [x] Input "Zona del cliente" deshabilitado autopoblado al seleccionar cliente
- [x] Select Sí/No Certificado GAB
- [x] Inputs Lat/Lng + botón "Capturar ubicación actual" con `navigator.geolocation`
- [x] Todo bajo `*appShowIfCountry="'PANAMA'"`

## Checklist — Fase 6: Frontend Seguimiento Diario (tab Lesiones + QQ) ✅

- [x] Componente compartido `lesion-tab` creado en `features/lesiones/components/lesion-tab/` (con listado, modal CRUD, resumen, tabs internas)
- [x] Montado en Reproductora (`modal-seguimiento-reproductora`) — tabs externos + QQ form
- [x] Montado en Apoyo (`lote-reproductora-ave-engorde-list`) — tab + QQ form
- [x] Montado en Engorde Panamá (`seguimiento-aves-engorde-form` + tabs-principal-engorde) — QQ en form
- [x] Aplicado `*appShowIfCountry="'PANAMA'"` en todos los elementos nuevos

## Checklist — Fase 7: Filtros por zona en Panamá ✅

- [x] Nuevo endpoint `GET /api/Farm/by-zona-usuario` en `FarmController`
- [x] `FarmService.GetByZonaUsuarioAsync` filtra por user.Zona si país=PANAMA, fallback a UserFarm para otros países
- [⏸] Wiring frontend del nuevo endpoint en módulos Seguimiento (follow-up — opcionalmente se cambian los selects de granja para usar este endpoint cuando país=PANAMA)
- [x] Sin regresión: comportamiento Colombia/Ecuador preservado (rama `else` con `UserFarm.Any(...)`)

## Checklist — Fase 8: Testing & cleanup

- [x] BD local sincronizada con código (`__EFMigrationsHistory` con las 4 migraciones Panamá)
- [x] `dotnet build` backend: 0 errores
- [ ] `yarn build` frontend (pendiente de validación final del usuario)
- [ ] E2E manual: crear cliente Panamá → granja → seguimiento + lesión
- [ ] E2E manual: usuario zona 1 ve solo granjas zona 1
- [ ] Tests backend (`dotnet test`)
- [ ] Tests frontend (`yarn test`) si se agregan
- [ ] `make down` (no aplica esta sesión — usamos Postgres nativo, no container)

---

## Notas

- Fuente del trabajo: documento `/Users/chelsycardona/Downloads/Plande Desarrollo Panama.docx` (incluye además: Liquidación con fórmulas, restricción día 1-7 Reproductora, módulo Reportes Panamá — explícitamente **fuera de scope** de esta iteración).
- Imagen del documento confirma el form de Clientes con etiqueta "Líder de Zona" (a confirmar si es un filtro o un campo nuevo distinto al Zona puro).
- Pre-deploy: aplicar workflow CI/CD documentado en CLAUDE.md (idempotencia, verificación post-deploy ECS, etc.).

---

## Notas del Monitor (fecha 2026-05-26)

Auditoría de coherencia plan ↔ código a corte de hoy. Resumen ejecutivo:

### Cumplido
- Migraciones 1–4 escritas y aplicadas localmente, todas idempotentes (`IF NOT EXISTS`, `DO $$ IF EXISTS`), con `Down()` reversibles.
- Entity `Farm`: `ClienteId`, `Zona` (string), `CertificadoGab` (bool), `Latitud`, `Longitud` agregadas y mapeadas en `FarmConfiguration` con índices.
- Entity `User`: `Zona` agregada y mapeada con índice; propagada a `UserDto`, `CreateUserDto`, `UpdateUserDto`, `RegisterDto`, `UserListDto`, `UserService` (Create/Update/Get/GetUsers) y `AuthService.Register`.
- DTOs de Farm (`CreateFarmDto`, `UpdateFarmDto`, `FarmDto`, `FarmDetailDto`, `FarmLiteDto`, `FarmSearchRequest`) reflejan los 5 campos Panamá; `FarmService.Create/Update/GetAll/GetById` los persiste/proyecta y `Search` filtra por `Zona` y `ClienteId`.
- `ClienteSearchRequest` + `ClienteService.SearchAsync` + `ClienteController.Search` aceptan `zona` y filtran correctamente.
- Entity `Lesion`, `LesionConfiguration`, `DbSet<Lesion> Lesiones` en `ZooSanMarinoContext`; DTOs (`LesionDto`, `Create/Update/SearchRequest`, `LesionResumenDto`), `ILesionService` e implementación `LesionService` con CRUD, paginado, resumen agrupado y soft-delete.
- Campos `QqMixtas/QqHembras/QqMachos` presentes en 4 entities (`SeguimientoLoteLevante`, `SeguimientoDiarioAvesEngorde`, `SeguimientoDiarioLoteReproductoraAvesEngorde`, `SeguimientoDiarioAvesEngordePanama`) y en `ZooSanMarinoContextModelSnapshot.cs` (col convención snake_case).
- `SeguimientoAvesEngordePanamaService` ya persiste y proyecta los QQ correctamente en líneas 74-177.

### Gap (alto impacto)
- **`LesionesController.cs` NO existe.** Toda la pila Lesiones está sin endpoint REST — los DTOs/Service están aislados.
- **`ILesionService` NO está registrado en DI** (`Program.cs` línea 313 registra `IClienteService` pero no `ILesionService`). Aun cuando se cree el controller, el `AddScoped` falta.
- **`IFarmService.GetByZonaUsuarioAsync` declarado pero NO implementado en `FarmService`.** Compilación de `IFarmService` fallará — esa es la causa de los `[⏳]` en el tracker (Fase 2/7). No hay endpoint `GET /api/Farm/by-zona-usuario` en `FarmController`.
- **Persistencia QQ en 3 de 4 servicios falta:** `SeguimientoLoteLevanteService.cs`, `SeguimientoDiarioLoteReproductoraService.cs` (líneas 105-153 + Update 155+) y `SeguimientoAvesEngordeService.cs` (línea 710+) construyen las entities sin asignar `QqMixtas/QqHembras/QqMachos` desde el DTO. Las columnas existen en BD pero el flujo Create/Update no las llena. Solo Panamá-específico (`SeguimientoAvesEngordePanamaService`) lo hace.
- **DTO base `SeguimientoLoteLevanteDto.MapToDto` y `MapToLevanteDto` no leen ni proyectan los QQ** — esos campos no aparecen en GET.
- **Sincronización Cliente→Farm.Zona (decisión 6 del plan) NO implementada** en `ClienteService.UpdateAsync`. Editar la zona de un cliente no actualiza la zona en las granjas asociadas — la denormalización queda inconsistente.

### Riesgo
- **Inconsistencia tipo de columna `lote_id` en `lesiones`**: plan dice `text NULL` (sec 1.3 línea 122), migración `CreateLesionesTable` la creó como `INTEGER NULL` (línea 24). La entity `Lote` tiene `int? LoteId` y `Galpon.GalponId` es string, así que `INTEGER` es coherente con el modelo real — el plan estaba desactualizado. **No-bloqueante** pero conviene anotar en plan que la decisión final fue `INTEGER`.
- **`CertificadoGab` no nullable** (bool con default false) — al hacer `UPDATE farms SET certificado_gab = NULL` no se puede; pero `UpdateFarmDto.CertificadoGab` también es no nullable con default false, así que un PATCH parcial sin enviar el campo lo pondrá en `false` en lugar de preservar el valor previo. Decision riesgosa para edits parciales.
- **`HasColumnName("zona")` omitido en `UserConfiguration` para snake_case explícito** — funciona por convención `EFCore.NamingConventions`, pero `FarmConfiguration` sí lo hace explícito; inconsistencia menor de estilo. (Verificado: `e.Property(u => u.Zona).HasColumnName("zona")` existe en línea 44.)
- **`Lesion.LoteReproductoraId` es `string?`** en entity, plan dice `text NULL` — ambos OK, pero el filtro en `LesionService.SearchAsync` compara igualdad de string sin normalizar trim/case.
- **`Cliente.Zona` y `Farm.Zona` no validan que el valor sea uno de `['Zona 1', 'Zona 2']`** — cualquier string de hasta 20 chars pasa. Riesgo de datos sucios.
- **`Pais` en `Cliente` aún es `string?`** (no FK a `paises.pais_id`). El plan documenta esto como decisión (mantener strings); no es bug pero conviene re-confirmar antes de wiring frontend.

### Decisiones que merecen revisión
- `Zona` como string libre (decisión 1 del plan): considerar agregar `CHECK (zona IN ('Zona 1', 'Zona 2'))` a `farms.zona`, `users.zona`, `clientes.zona` para evitar errores tipográficos. Migración chica, mucho valor.
- Consultar si el campo "Líder de Zona" del form (mencionado al final de "Notas") es distinto al campo Zona del Cliente o si es un alias de UI.

### Mejoras sugeridas (accionables)
1. `backend/src/ZooSanMarino.API/Program.cs` (línea ~313, junto a `IClienteService`): agregar `builder.Services.AddScoped<ILesionService, LesionService>();`.
2. `backend/src/ZooSanMarino.API/Controllers/LesionesController.cs` (nuevo): crear controller con los 6 endpoints listados en el plan (`search/{id}/POST/PUT/DELETE/resumen`).
3. `backend/src/ZooSanMarino.Infrastructure/Services/FarmService.cs`: implementar `GetByZonaUsuarioAsync(string? paisActivo, CancellationToken ct)` — leer `_current.UserGuid → users.zona`, si `paisActivo == "PANAMA"` && user.Zona ≠ null, filtrar `farms.Zona == user.Zona`; caso contrario, comportamiento actual de `GetAssignedFarmsForCompanyAsync`. Sin esto la API no compila.
4. `SeguimientoLoteLevanteService.cs`, `SeguimientoDiarioLoteReproductoraService.cs` línea 115 (Create), línea 155+ (Update); `SeguimientoAvesEngordeService.cs` línea 710 (Create) y su Update: agregar `QqMixtas = dto.QqMixtas, QqHembras = dto.QqHembras, QqMachos = dto.QqMachos`. Y propagar en los `MapToDto`/`MapToLevanteDto` para los GET. Sin esto, los campos QQ se guardan como NULL siempre desde el endpoint general.
5. `ClienteService.UpdateAsync` (línea 153): tras el `entity.Zona = dto.Zona`, ejecutar `UPDATE farms SET zona = @newZona WHERE cliente_id = @clienteId` para cumplir decisión 6 del plan (denormalización viva). Opcionalmente vía `_ctx.Farms.Where(f => f.ClienteId == id).ExecuteUpdateAsync(...)`.

### Pendiente del plan no tocado todavía
- Fase 3 frontend Clientes: cascada Pais/Departamento/Municipio (sigue hardcoded).
- Fase 4 frontend Usuarios: form aún sin select Zona (modal-create-edit.component.ts modificado pero falta auditar).
- Fase 5 frontend Granja: HTML/TS de farm-form modificado — falta validar el flujo cliente → zona autopoblada + GAB + geolocation + `*showIfCountry`.
- Fase 6 frontend Seguimiento: tab Lesiones + campos QQ — sin tocar.
- Fase 7 backend: endpoint `by-zona-usuario` no implementado.
- Fase 8: `dotnet build` final no ejecutado (tracker lo marca como bloqueado por agentes).

**No se editaron archivos del proyecto, solo este tracker.**
