# Plan de Desarrollo — Mejoras Panamá (Clientes, Zonas, Lesiones, Granja, Seguimiento Diario)

**Fecha:** 2026-05-26
**Autor:** Claude (Opus 4.7)
**Documento fuente:** `/Users/chelsycardona/Downloads/Plande Desarrollo Panama.docx`
**Branch sugerida:** `feature/panama-zonas-clientes-lesiones`

> **Pre-condición técnica clave (ya existe en el repo):** El frontend ya gestiona país activo (`session.activePaisId`, `session.activePaisNombre`), inyecta los headers HTTP `X-Active-Pais` y `X-Active-Pais-Nombre` desde `auth.interceptor.ts`, y expone la directiva estructural `*showIfCountry` en `core/directives/show-if-country.directive.ts`. Toda lógica "solo si Panamá" se monta encima de esto — **no se reinventa**.

---

## 1. Auditoría de estado actual (Schema-Audit-Rule aplicada)

Antes de proponer cambios, este es el estado real del código HOY contra lo que pide el documento de Panamá:

### 1.1 Backend — lo que YA existe

| Componente | Estado | Notas |
|---|---|---|
| `Domain/Entities/Cliente.cs` | ✅ Completa | Tiene `TipoDocumento, NumeroIdentificacion, Nombre, Correo, Telefono, TipoCliente, Pais, Provincia, Distrito, Planta, Zona, Status`. Todos `string?` salvo los tres primeros. |
| `Application/DTOs/Cliente/*` | ✅ Completos | `ClienteDto`, `CreateClienteRequest`, `UpdateClienteRequest`, `ClienteSearchRequest`. |
| `API/Controllers/ClienteController.cs` | ✅ CRUD + búsqueda paginada | Filtra por `search, tipoCliente, pais, tipoDocumento, soloActivos`. **Falta filtrar por `zona`.** |
| `Domain/Entities/Pais.cs`, `Departamento.cs`, `Municipio.cs` | ✅ Estructura jerárquica completa | Pais ─< Departamento ─< Municipio (PKs simples, FKs ya cableadas). |
| `API/Controllers/PaisController.cs`, `DepartamentoController.cs`, `MunicipioController.cs` | ✅ Existen | Servicios disponibles para alimentar selects en cascada. |
| `Domain/Entities/Zona.cs` | ⚠️ Existe pero diferente uso | PK compuesta `(ZonaCia, ZonaId)` ligada a Company. **NO sirve para zonas Panamá "Zona 1/Zona 2"** que son atributos del cliente y la granja. |
| `Domain/Entities/Farm.cs` | ⚠️ Le faltan campos | Tiene `CompanyId, Name, RegionalId, Status, DepartamentoId, MunicipioId`. **Falta: `ClienteId`, `ZonaId` (denormalizado), `CertificadoGab`, `Latitud`, `Longitud`.** |
| `Domain/Entities/User.cs` | ⚠️ Le falta campo | Sin `ZonaId`. **Falta agregar zona del usuario.** |
| `Domain/Entities/SeguimientoDiarioAvesEngordePanama.cs` | ✅ **YA tiene QqMixtas, QqHembras, QqMachos** | Línea 67-69. Por confirmar si está mapeado y expuesto en endpoints/DTOs/frontend. |
| `Domain/Entities/SeguimientoDiarioAvesEngorde.cs` (Apoyo/Engorde general) | ⚠️ **Sin campos QQ** | Falta agregar QQ Mixtas/Hembras/Machos. |
| `Domain/Entities/SeguimientoLoteLevante.cs` (Reproductora) | ⚠️ **Sin campos QQ** | Falta agregar QQ Mixtas/Hembras/Machos. |
| Lesiones (entidad/tabla/controller/DTO) | ❌ No existe | A crear desde cero. |
| `API/Controllers/FarmController.cs` | ⚠️ Filtra por usuario actual | Endpoint `GET /api/Farm?id_user_session=...` ya busca granjas asignadas al usuario. **Falta extender filtro por zona cuando país=PANAMÁ.** |

### 1.2 Frontend — lo que YA existe

| Componente | Estado | Notas |
|---|---|---|
| `features/clientes/` | ✅ Módulo existente | `models/cliente.models.ts`, `services/cliente.service.ts`, `components/cliente-list/` (HTML 600 LoC, TS 311 LoC). **Pero los selects de País/Provincia/Distrito están hardcoded a `PAISES` (string[])** en línea 68-72 de `cliente.models.ts` — no consume los endpoints reales. |
| `features/aves-engorde-panama/` | ✅ Módulo existente | Tiene `pages/`, `services/`, `utils/`. Aquí va el seguimiento engorde Panamá. |
| `features/seguimiento-diario-lote-reproductora/` | ✅ Módulo existente | Aquí va Reproductora (R1/R2/R3). |
| `features/lote-reproductora-ave-engorde/` | ✅ Módulo existente | Apoyo (lote reproductora ave engorde). |
| `core/auth/auth.service.ts` | ✅ Almacena país | `activePaisId`, `activePaisNombre` en session. |
| `core/auth/auth.interceptor.ts` | ✅ Inyecta headers | `X-Active-Pais` y `X-Active-Pais-Nombre`. |
| `core/directives/show-if-country.directive.ts` | ✅ Disponible | Directiva estructural `*showIfCountry="'PANAMA'"`. **Esta es la herramienta para mostrar/ocultar campos QQ, tab Lesiones, selectores Cliente en Granja, etc.** |
| `core/services/country/country-filter.service.ts` | ✅ Disponible | Servicio para chequear país desde código TS. |
| `features/farm/` | ✅ Módulo existente | Aquí van Cliente, Zona, GAB, lat/lng. |

### 1.3 Conclusiones de la auditoría

1. **No es "módulo Clientes desde cero".** Es **extender el módulo existente** para: (a) conectar selects con servicios Pais/Departamento/Municipio reales, (b) agregar el select Zona, (c) default Panamá cuando aplique.
2. La entidad `Cliente` tiene los campos del doc como `string?`. Mantener strings y dejar que el frontend popule desde los selects evita migraciones disruptivas y conserva backward-compat. **La zona del cliente se guarda como string `"Zona 1"`/`"Zona 2"`** — o numérico `1`/`2`. **Decisión a confirmar con usuario** (ver Sección 7).
3. `SeguimientoDiarioAvesEngordePanama` ya tiene los campos QQ en la entidad — **falta validar si están en BD prod, en DTO, en service y en frontend.** El plan asume que **probablemente sí están en BD local** (entidad consolidada) pero **hay que confirmar contra `__EFMigrationsHistory`** y endpoints.
4. La entidad `Zona` existente no sirve para este uso — es por compañía, no por país. **Para Panamá usamos un campo simple `int? ZonaId` (1 o 2) o `string ZonaCodigo`** en Farm/User/Cliente. **Decisión a confirmar.**
5. **Lesiones es una entidad nueva**. Una sola tabla `lesiones` con FKs a cliente/granja/galpón/lote sirve a los tres módulos (Reproductora, Apoyo, Engorde) — la UI le muestra el tab cuando aplique.

---

## 2. Alcance del desarrollo

Lo que sí hace este plan (en orden de fases):

1. **Fase 1 — Backend Schema:** migraciones idempotentes para Farm (cliente/zona/GAB/lat/lng), User (zona), Lesion (nueva tabla), y QQ en seguimientos de Reproductora + Engorde general (si no existen ya en Panamá puro).
2. **Fase 2 — Backend Domain/Application/API:** entidades, DTOs, services, controllers para todo lo anterior + nuevo filtro de granjas por zona en Panamá.
3. **Fase 3 — Frontend Clientes:** cablear Pais/Departamento/Municipio, default Panamá cuando aplique, select Zona.
4. **Fase 4 — Frontend Usuarios:** agregar select Zona.
5. **Fase 5 — Frontend Granja:** select Cliente con auto-llenado de zona deshabilitada, GAB, captura lat/lng.
6. **Fase 6 — Frontend Seguimiento Diario:** tab "Lesiones" + campos QQ (los tres módulos) — **solo visible si país activo = PANAMA**.
7. **Fase 7 — Filtros Panamá:** endpoint y wiring para que en Panamá las granjas se filtren por zona del usuario logueado.
8. **Fase 8 — Testing & cleanup:** validación local con BD docker, contratos API, screenshots, `make down`.

### Fuera de alcance (mencionado en doc, NO en esta iteración)

Estos puntos del documento NO se incluyen aquí (a confirmar con usuario si quiere incluirlos):

- **Liquidación de lote engorde** con todas las fórmulas (Días Engorde, Aves Encasetadas, %Mortalidad, Conversión, Eficiencia Americana, E.E.F, etc.). Es un módulo grande de su propia escala — ya hay `LiquidacionTecnicaController` y `LiquidacionTecnicaEcuadorController`; lo natural sería crear `LiquidacionTecnicaPanamaController` con las fórmulas específicas. **No incluido por scope.**
- **Restricción Día 1–Día 7 para Registro Diario Reproductora.** Es una validación de negocio existente que el doc confirma. Si requiere ajuste, va aparte.
- **Módulo de Reportes Panamá** (gráficos diarios/semanales/liquidaciones). Es un módulo de reportería que ya tiene contrapartes (`reportes-tecnicos`, `reporte-tecnico-administrativo`). **No incluido por scope.**

---

## 3. Diseño detallado por fase

### Fase 1 — Backend Schema (migraciones EF idempotentes)

> **Regla:** Cada `Up()` usa `migrationBuilder.Sql("ALTER TABLE ... ADD COLUMN IF NOT EXISTS ...")` para que sea seguro re-ejecutar y para que no rompa si alguien ya parchó la BD manualmente. Los `Down()` revierten con `DROP COLUMN IF EXISTS`.

#### 1.1 Migración `AddPanamaFieldsToFarm`

Tabla afectada: `farms`

```sql
ALTER TABLE farms ADD COLUMN IF NOT EXISTS cliente_id integer NULL;
ALTER TABLE farms ADD COLUMN IF NOT EXISTS zona varchar(20) NULL;  -- 'Zona 1' o 'Zona 2'
ALTER TABLE farms ADD COLUMN IF NOT EXISTS certificado_gab boolean NOT NULL DEFAULT false;
ALTER TABLE farms ADD COLUMN IF NOT EXISTS latitud numeric(10,7) NULL;
ALTER TABLE farms ADD COLUMN IF NOT EXISTS longitud numeric(10,7) NULL;
CREATE INDEX IF NOT EXISTS ix_farms_cliente_id ON farms(cliente_id);
CREATE INDEX IF NOT EXISTS ix_farms_zona ON farms(zona);
-- FK suave: no agregamos constraint para evitar romper data legacy sin cliente
```

#### 1.2 Migración `AddZonaToUser`

Tabla afectada: `users`

```sql
ALTER TABLE users ADD COLUMN IF NOT EXISTS zona varchar(20) NULL;  -- 'Zona 1' o 'Zona 2'; NULL = sin restricción / ambas
CREATE INDEX IF NOT EXISTS ix_users_zona ON users(zona);
```

#### 1.3 Migración `CreateLesionesTable`

Tabla nueva: `lesiones`

```sql
CREATE TABLE IF NOT EXISTS lesiones (
  id              bigserial PRIMARY KEY,
  cliente_id      integer NULL,           -- FK suave a clientes
  farm_id         integer NOT NULL,       -- FK a farms
  galpon_id       text    NULL,           -- galpon usa text PK
  lote_id         text    NULL,           -- lote usa text PK
  lote_reproductora_id text NULL,
  edad_dias       integer NULL,
  aves_macho      integer NULL,
  aves_hembra     integer NULL,
  aves_mixtas     integer NULL,
  tipo_lesion     varchar(120) NOT NULL,
  observaciones   text NULL,
  fecha_registro  timestamptz NOT NULL DEFAULT now(),
  modulo_origen   varchar(20) NOT NULL,    -- 'REPRODUCTORA' | 'APOYO' | 'ENGORDE'
  company_id      integer NOT NULL,
  created_by_user_id integer NULL,
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_by_user_id integer NULL,
  updated_at      timestamptz NULL,
  deleted_at      timestamptz NULL,
  status          varchar(1) NOT NULL DEFAULT 'A'
);

CREATE INDEX IF NOT EXISTS ix_lesiones_farm_id ON lesiones(farm_id);
CREATE INDEX IF NOT EXISTS ix_lesiones_cliente_id ON lesiones(cliente_id);
CREATE INDEX IF NOT EXISTS ix_lesiones_company_id ON lesiones(company_id);
CREATE INDEX IF NOT EXISTS ix_lesiones_modulo_origen ON lesiones(modulo_origen);
CREATE INDEX IF NOT EXISTS ix_lesiones_lote_id ON lesiones(lote_id);
```

Decisión: **una sola tabla con discriminador `modulo_origen`** (no tres tablas separadas). Esto cumple "Resumen por categoría" del doc trivialmente con un `GROUP BY tipo_lesion, modulo_origen`.

#### 1.4 Migración `AddQqFieldsToSeguimientoLevanteAndEngorde`

> **Auditoría previa requerida:** Antes de redactar la migración, ejecutar contra BD local y prod:
> ```sql
> SELECT column_name FROM information_schema.columns
> WHERE table_name IN ('seguimiento_lote_levante','seguimiento_diario_aves_engorde','seguimiento_diario_aves_engorde_panama')
>   AND column_name IN ('qq_mixtas','qq_hembras','qq_machos');
> ```
> Si `seguimiento_diario_aves_engorde_panama` ya tiene los QQ, **NO los duplicamos**.

Tabla `seguimiento_lote_levante` (Reproductora):
```sql
ALTER TABLE seguimiento_lote_levante ADD COLUMN IF NOT EXISTS qq_mixtas numeric(10,2) NULL;
ALTER TABLE seguimiento_lote_levante ADD COLUMN IF NOT EXISTS qq_hembras numeric(10,2) NULL;
ALTER TABLE seguimiento_lote_levante ADD COLUMN IF NOT EXISTS qq_machos numeric(10,2) NULL;
```

Tabla `seguimiento_diario_aves_engorde` (engorde general, también cubre Apoyo si comparten tabla — confirmar):
```sql
ALTER TABLE seguimiento_diario_aves_engorde ADD COLUMN IF NOT EXISTS qq_mixtas numeric(10,2) NULL;
ALTER TABLE seguimiento_diario_aves_engorde ADD COLUMN IF NOT EXISTS qq_hembras numeric(10,2) NULL;
ALTER TABLE seguimiento_diario_aves_engorde ADD COLUMN IF NOT EXISTS qq_machos numeric(10,2) NULL;
```

> Si "Apoyo" usa una tabla distinta (a confirmar — la entidad `SeguimientoDiarioLoteReproductoraAvesEngorde` sugiere que sí), aplicar la misma migración a esa tabla también.

### Fase 2 — Backend Domain / Application / API

#### 2.1 Farm

- `Farm.cs`: agregar `int? ClienteId`, `int? ZonaId`, `string? CertificadoGab`, `decimal? Latitud`, `decimal? Longitud`.
- DTOs `FarmDto`, `FarmDetailDto`, `CreateFarmDto`, `UpdateFarmDto`: agregar los mismos campos.
- `FarmService` / Repository: persistir y leer los nuevos campos.
- `FarmConfiguration.cs` (EF mapping): agregar `.HasColumnName("cliente_id")`, `.HasColumnName("zona_id")`, etc.
- **Nuevo endpoint** `GET /api/Farm/by-zona?paisId={id}&zonaId={id}` o extender `GET /api/Farm` con `?zonaId=`. Cuando se llama con país=PANAMÁ y el usuario tiene zona asignada, filtrar `WHERE farms.zona_id = users.zona_id`.

#### 2.2 User

- `User.cs`: agregar `int? ZonaId`.
- DTOs `UserDto`, `RegisterDto`, `UpdateUserDto`: agregar `ZonaId`.
- `UsersController` y servicio: persistir y exponer.

#### 2.3 Cliente

- `ClienteSearchRequest`: agregar `string? zona`.
- `ClienteController.Search`: agregar `[FromQuery] string? zona = null` y pasarlo al service.
- `ClienteService.SearchAsync`: filtrar por zona.

#### 2.4 Lesion (nuevo)

- `Domain/Entities/Lesion.cs` (heredando `AuditableEntity` para `Status`, `CompanyId`, `CreatedAt`, etc.).
- `Application/DTOs/Lesiones/`:
  - `LesionDto.cs`
  - `CreateLesionRequest.cs`
  - `UpdateLesionRequest.cs`
  - `LesionSearchRequest.cs`
- `Application/Interfaces/ILesionService.cs`
- `Infrastructure/Services/LesionService.cs`
- `Infrastructure/Persistence/Configurations/LesionConfiguration.cs`
- `API/Controllers/LesionesController.cs`:
  - `GET    /api/lesiones/search`
  - `GET    /api/lesiones/{id}`
  - `POST   /api/lesiones`
  - `PUT    /api/lesiones/{id}`
  - `DELETE /api/lesiones/{id}`
  - `GET    /api/lesiones/resumen?moduloOrigen=...&clienteId=...&farmId=...`  ← Resumen por categoría

#### 2.5 Seguimientos — QQ fields

- `SeguimientoLoteLevante.cs`: agregar `decimal? QqMixtas`, `decimal? QqHembras`, `decimal? QqMachos`.
- `SeguimientoDiarioAvesEngorde.cs`: agregar los tres campos.
- DTOs respectivos: agregar campos.
- Mapeos EF y services: persistir/leer.
- **No tocar `SeguimientoDiarioAvesEngordePanama`** — ya los tiene.

### Fase 3 — Frontend Clientes

Editar `features/clientes/components/cliente-list/cliente-list.component.ts` y `.html`:

1. Importar `PaisService`, `DepartamentoService`, `MunicipioService` (validar nombres reales en `core/services/` o `features/farm/services/` — probable que ya existan; si no, crearlos en `core/services/`).
2. En el form de cliente:
   - País → select que carga `GET /api/paises`. Si `session.activePaisNombre === 'PANAMA'`, pre-seleccionar Panamá.
   - Provincia → select que carga `GET /api/departamentos?paisId={paisIdSelected}` al cambiar país.
   - Distrito → select que carga `GET /api/municipios?departamentoId={...}` al cambiar provincia.
   - Zona → select con opciones `[{value: 1, label: 'Zona 1'}, {value: 2, label: 'Zona 2'}]` — visible siempre, requerido si país es Panamá.
3. Reemplazar el array `PAISES` hardcoded por la lista del backend (mantener compat: si backend devuelve vacío, seguir mostrando la lista de fallback).
4. Mantener `Planta` (es texto/select propio, no parte de la jerarquía geográfica).

### Fase 4 — Frontend Usuarios

Buscar el componente actual de usuarios (probable: `features/config/` o similar — auditar antes de editar):

- Agregar campo `zonaId` al form de creación/edición.
- Select con opciones: `[{value: null, label: 'Todas las zonas'}, {value: 1, label: 'Zona 1'}, {value: 2, label: 'Zona 2'}]`.
- Mostrar el campo **siempre** (no condicionar a país), pero documentar que sólo aplica para flujos de Panamá.

### Fase 5 — Frontend Granja

Editar form de creación/edición de granja:

1. **Select Cliente** — con búsqueda (autocomplete) consumiendo `GET /api/clientes/search`. Filtrar por país activo cuando aplique.
2. Al seleccionar cliente, llamar `GET /api/clientes/{id}` y popular un input **deshabilitado** "Zona del cliente" con la zona del cliente; al guardar, persistir esa zona como `zona_id` en la granja (denormalización para evitar joins en runtime, según pidió el usuario).
3. Reactivo: si el cliente cambia, actualizar la zona automáticamente.
4. **Certificado GAB** — input texto (o file upload si requiere archivo — confirmar; el doc sólo dice "Certificado GAB" sin más detalle).
5. **Latitud / Longitud** — dos inputs `number` + botón "Capturar ubicación actual" que llama `navigator.geolocation.getCurrentPosition()` y los popula.
6. Aplicar `*showIfCountry="'PANAMA'"` a los campos Cliente/GAB/lat/lng para que solo aparezcan cuando el país activo es Panamá. Si el usuario quiere que sean visibles en todos los países, simplemente no se condiciona (a confirmar — el doc lo describe en contexto Panamá).

### Fase 6 — Frontend Seguimiento Diario (Reproductora, Apoyo, Engorde)

Para los tres módulos:

1. **Tab "Lesiones"** dentro del componente de seguimiento del lote. Usa `*showIfCountry="'PANAMA'"` para que el tab solo aparezca en Panamá.
2. Contenido del tab:
   - Listado paginado de lesiones del lote (consumiendo `GET /api/lesiones/search?loteId=...&moduloOrigen=REPRODUCTORA`).
   - Botón "Agregar lesión" → modal con form (cliente, granja, galpón, lote, lote reproductora, edad días, # aves macho/hembra/mixtas, tipo lesión, observaciones).
   - Sección "Resumen por categoría" consumiendo `GET /api/lesiones/resumen` y mostrando tabla agrupada.
3. **Campos QQ Mixtas/Hembras/Machos** en el form del seguimiento diario:
   - Visibles sólo con `*showIfCountry="'PANAMA'"`.
   - Se persisten al guardar el seguimiento.

### Fase 7 — Filtros de granja por zona (Panamá)

- Extender `FarmController.GetAll` o crear `GET /api/Farm/by-user-and-zona`:
  - Si el usuario tiene `ZonaId` asignada (no nulo) **y** el contexto activo es Panamá (header `X-Active-Pais-Nombre`=`PANAMA`), agregar `WHERE farms.zona_id = users.zona_id`.
  - Caso contrario, comportamiento actual (filtro por `UserFarm`).
- Frontend: en los selectores de granja del módulo Seguimiento Diario (los tres tabs), consumir este endpoint cuando país=PANAMÁ.
- Mantener el filtro por `UserFarm` activo en paralelo para no romper Colombia/Ecuador.

### Fase 8 — Testing local y validación

Por cada fase de backend:
1. `make up` (Docker DB local).
2. `dotnet ef database update` para aplicar migraciones.
3. Llamar endpoints con `curl` o Swagger contra `localhost:5002`.
4. Validar status codes, payloads, FK constraints.

Por cada fase de frontend:
1. `yarn start` en `localhost:4200`.
2. Login + cambio de país a Panamá.
3. Verificar que los campos/tabs aparecen sólo en Panamá.
4. Crear/editar/listar registros end-to-end.

**Al terminar cada sesión:** `make down` (no dejar contenedores vivos según CLAUDE.md).

---

## 4. Archivos a crear o modificar (resumen)

### Backend — nuevos archivos
- `backend/src/ZooSanMarino.Domain/Entities/Lesion.cs`
- `backend/src/ZooSanMarino.Application/DTOs/Lesiones/LesionDto.cs`
- `backend/src/ZooSanMarino.Application/DTOs/Lesiones/CreateLesionRequest.cs`
- `backend/src/ZooSanMarino.Application/DTOs/Lesiones/UpdateLesionRequest.cs`
- `backend/src/ZooSanMarino.Application/DTOs/Lesiones/LesionSearchRequest.cs`
- `backend/src/ZooSanMarino.Application/Interfaces/ILesionService.cs`
- `backend/src/ZooSanMarino.Infrastructure/Services/LesionService.cs`
- `backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/LesionConfiguration.cs`
- `backend/src/ZooSanMarino.API/Controllers/LesionesController.cs`
- `backend/src/ZooSanMarino.Infrastructure/Migrations/<ts>_AddPanamaFieldsToFarm.cs`
- `backend/src/ZooSanMarino.Infrastructure/Migrations/<ts>_AddZonaToUser.cs`
- `backend/src/ZooSanMarino.Infrastructure/Migrations/<ts>_CreateLesionesTable.cs`
- `backend/src/ZooSanMarino.Infrastructure/Migrations/<ts>_AddQqFieldsToSeguimientos.cs`

### Backend — modificados
- `Domain/Entities/Farm.cs`, `User.cs`, `SeguimientoLoteLevante.cs`, `SeguimientoDiarioAvesEngorde.cs`
- `Infrastructure/Persistence/Configurations/FarmConfiguration.cs`, `UserConfiguration.cs`, mapeos de seguimientos
- DTOs: `FarmDto`, `FarmDetailDto`, `CreateFarmDto`, `UpdateFarmDto`, `UserDto`, `RegisterDto`, `UpdateUserDto`, `ClienteSearchRequest`, DTOs de seguimientos
- Services correspondientes
- `FarmController.cs`, `UsersController.cs`, `ClienteController.cs`, `SeguimientoLoteLevanteController.cs` (o equivalente), `SeguimientoAvesEngordeController.cs`

### Frontend — nuevos archivos
- `features/lesiones/` (o dentro de cada módulo de seguimiento — decisión a confirmar)
  - `models/lesion.models.ts`
  - `services/lesion.service.ts`
  - `components/lesion-tab/` (componente compartido reusable entre los tres módulos de seguimiento)

### Frontend — modificados
- `features/clientes/models/cliente.models.ts` (eliminar arrays hardcoded o convertirlos en fallback)
- `features/clientes/services/cliente.service.ts` (agregar filtros zona)
- `features/clientes/components/cliente-list/*` (form con selects en cascada)
- `features/farm/components/` (form con cliente, zona, GAB, lat/lng)
- `features/farm/services/` (filtro por zona)
- `features/config/` o equivalente (form usuarios con campo zona)
- `features/seguimiento-diario-lote-reproductora/` (tab Lesiones + campos QQ)
- `features/lote-reproductora-ave-engorde/` (tab Lesiones + campos QQ)  ← Apoyo
- `features/aves-engorde-panama/` (tab Lesiones + campos QQ — si no usa los del módulo Panamá específico)

---

## 5. Test cases mínimos por fase

### Fase 1 (Schema)
- ✅ `dotnet ef database update` aplica sin error en BD local limpia.
- ✅ Re-ejecutar las migraciones no falla (idempotencia `IF NOT EXISTS`).
- ✅ Roll back via `Down()` revierte limpio.

### Fase 2 (Backend)
- ✅ POST `/api/Farm` con `clienteId`, `zonaId`, `certificadoGab`, `latitud`, `longitud` persiste y los devuelve en el GET.
- ✅ POST `/api/users` con `zonaId` persiste.
- ✅ POST `/api/lesiones` válido devuelve 201 + DTO; missing required → 400.
- ✅ GET `/api/lesiones/resumen?moduloOrigen=ENGORDE&farmId=X` devuelve agregado.
- ✅ GET `/api/Farm?zonaId=1` filtra granjas correctamente.

### Fase 3–7 (Frontend)
- ✅ Login con compañía Panamá → activePaisNombre = 'PANAMA' en session storage.
- ✅ Form Clientes: cambiar país recarga provincia/distrito.
- ✅ Granja: seleccionar cliente popula zona automáticamente; al guardar, payload incluye zona.
- ✅ Usuario zona=1 logueado en Panamá: `GET /api/Farm` solo devuelve granjas con `zonaId=1`.
- ✅ Usuario en Colombia: comportamiento idéntico al actual (sin regresión).
- ✅ Seguimiento Diario Reproductora en Panamá: tab "Lesiones" visible; en Colombia: oculto.
- ✅ Campos QQ visibles solo en Panamá.

---

## 6. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Migraciones rompen prod (incidente histórico SIGSEGV) | Idempotencia `IF NOT EXISTS`. Probar localmente antes de mergear. Verificar `__EFMigrationsHistory` post-deploy. |
| Cliente como string vs FK | Decisión documentada en sección 7. Por defecto: string (no rompe nada existente). |
| Tab "Lesiones" infla bundle de frontend | Componente standalone lazy-loaded por feature. |
| Filtro por zona rompe Colombia/Ecuador | Filtro condicional al header `X-Active-Pais-Nombre`. Tests en ambos países. |
| Geolocation falla en HTTP no-HTTPS | Mostrar mensaje claro al usuario; los campos lat/lng siguen siendo editables a mano. |

---

## 7. Decisiones confirmadas (2026-05-26 con el usuario)

1. **Zona** → **string `"Zona 1"` / `"Zona 2"`**. Columna `zona` text NULL en `farms` y `users`. El select del frontend tiene exactamente esos dos valores. NULL no aplica (o si el usuario tiene ambas zonas, se modela aparte si surge la necesidad).
2. **Certificado GAB** → **booleano** (`bool certificado_gab` en `farms`). Frontend muestra select "Sí / No". NULL/false = sin certificado.
3. **Tab Lesiones** → **solo Panamá** (`*showIfCountry="'PANAMA'"` en los tres módulos de seguimiento).
4. **Alcance** → **implementar todo** lo descrito en este plan, con agentes paralelos para acelerar (sin Liquidación/Reportes — siguen fuera de scope salvo nueva indicación).
5. **Filtros Lesiones** → idénticos a los de Seguimiento Diario Reproductora/Engorde (cliente, granja, galpón, lote, módulo origen, fecha).
6. **Zona se persiste denormalizada en `farms` y `users`** — al editar un cliente se actualiza la zona en sus granjas asociadas (trigger en service de Cliente).

---

## 8. Orden recomendado de ejecución

Para entregar valor incremental y reducir blast radius:

1. **Sprint A (Schema + Cliente UI):** Fase 1 + 3 — los selects de país/dept/dist en Clientes ya son útiles para Colombia/Ecuador también.
2. **Sprint B (Granja):** Fase 5 + parte de Fase 2 (Farm) — habilita el flujo Cliente → Granja.
3. **Sprint C (User Zona + Filtro):** Fase 4 + 7 — filtro de granjas por zona operativo.
4. **Sprint D (Lesiones):** Fase 2 (Lesion) + 6 (tab) — feature completa de Lesiones.
5. **Sprint E (QQ Seguimientos):** Restante Fase 2 + 6 — campos QQ visibles en formularios.
6. **Sprint F (Cierre):** Fase 8 — testing E2E + deploy.

---

## 9. Referencias internas

- `CLAUDE.md` — workflow de desarrollo, migraciones EF, deploy.
- `fase_de_desarrollo/ANALISIS_MODULO_GRANJA.md` — análisis previo del módulo granja.
- `fase_de_desarrollo/11_reporte_liquidacion_tecnica.md` — base para futura Liquidación Panamá.
- `tracker_estado.md` — checklist activo de implementación.
