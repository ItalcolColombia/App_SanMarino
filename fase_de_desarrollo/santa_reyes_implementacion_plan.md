# Plan — Implementación empresa SANTA REYES (Colombia, postura comercial)

> Fecha: 2026-07-25 · Fuente del requerimiento: 3 Excel en `C:\Users\SAN MARINO\Desktop\Requerimiento Santa reyes\` (Granja.xlsx, Items.xlsx, Lotes.xlsx) + indicaciones del usuario.
> Levantamiento de código: 6 exploraciones (empresa/roles/usuarios, granjas, lotes+guía genética, clasificación huevos, traslados/edades, patrón multi-empresa).

---

## 1. Contexto y objetivo

**Santa Reyes** es una empresa NUEVA que entra a Colombia (pais_id=1). Es **postura comercial** (huevo de mesa: rojo/blanco/criollo), a diferencia de Agroavícola Sanmarino (reproductoras / huevo incubable). Usará los módulos existentes de **levante + producción (postura) + inventario + granjas + lotes + traslados + reportes**, con estas particularidades:

1. **Campos ERP adicionales** al crear granja/núcleo/galpón/lote (bodega, centro de operación, instalación, ubicación, centro de costo) — visibles SOLO para Santa Reyes.
2. **Sin guía genética al inicio**: crear lote debe permitir raza/año vacíos mientras la empresa no tenga guía cargada; al cargar su guía, vuelve a ser obligatorio.
3. **Clasificación de huevos propia**: por ÍTEM de huevo del inventario (HUEVO ROJO, BLANCO, CRIOLLO, MANCHADO, PICADO…) con categoría **Primera / Pnc**, en lugar de las 11 columnas fijas actuales.
4. **Paso levante→producción manual** (ellos lo hacen ~semana 16, no 26) liquidando un lote y pasando las aves a otro lote **conservando la edad de las aves trasladadas** (un lote puede tener aves propias de 14 sem y recibidas de 20 sem, cada grupo cuenta su propia edad).
5. Roles **Administrador** e **Implementador** con los mismos permisos para la empresa. Usuarios con contraseña `123456789`.

**Principio rector multi-empresa** (patrón ya establecido en el repo — `maneja_alimento_por_galpon`): las señales viven en la BD como **columnas tipadas por comportamiento en `companies`** (jamás `if pais == X` ni `if empresa == 'Santa Reyes'` en código), el backend las lee siempre, y el front las recibe vía DTO. Módulos on/off por empresa = `company_menus` + `role_menus` (mismo mecanismo que aísla Puente Panamá).

---

## 2. Levantamiento de datos (los 3 Excel)

### Granja.xlsx
- **Granjas** (4 filas): granja `La Esperanza` (regional Occidente, Buga, Valle del Cauca) + núcleos `Núcleo 1/2/3`. Campos ERP: Código Bodega (`B0601` granja, `B3001/2/3` núcleos), Desc. Bodega, C.O. (`830`), Desc. C.O., Instalación (`B06` granja, `B30` núcleos), Desc. Instalación.
- **Galpones y Silos** (77 filas): 38 silos de alimento (`BS60101..BS60138`), 1 bodega de insumos (`BUG60100`), 38 galpones tipo `Jaula` (`BG60201..BG60238`). Todos con C.O. 830 y bodega B0601.

### Items.xlsx
- **Items Alimento** (45): código, referencia, descripción, tipo inventario `IN300511S`.
- **Items Insumos** (246) en 9 categorías: VACUNAS 61, INSUMOS 60, DESINFECTANTES 49, MEDICAMENTOS 27, EMPAQUES 21, COMBUSTIBLES 15, MATERIAS PRIMAS 7, MANTENIMIENTO 4, PRODUCTO TERMINADO 2.
- **Items Huevo** (21): código, descripción, U.M. (`UND`, 1 en `KIL`), **Tipo Huevo = `Primera` (10) / `Pnc` (11)**.

### Lotes.xlsx (10 lotes, granja La Esperanza)
Lote (`LOTE 216`…`LOTE 234`), **Ccosto y Extensión** (`G3002216`…), Desc., Raza (`BABCOK BROWN`, `LOHMANN LSL`, `LOHMANN BROWN`, `HY LINE`), Tipo Ave (`ROJA`/`BLANCA`), Fecha Encaset (2024-11-22 → 2026-02-24). **Sin cantidades de aves ni galpón** (se cargarán después por edición/carga masiva).

---

## 3. Dónde va cada cosa (mapa requerimiento → solución)

| Requerimiento | Solución | Dónde |
|---|---|---|
| Empresa nueva + país | INSERT `companies` + `company_pais (pais_id=1)` vía migración idempotente | Migración `SeedSantaReyes` |
| Roles admin + implementador (mismos permisos) | 2 roles `is_company_admin=true` + `role_companies` + `role_permissions` (todos los permisos) + `role_menus` | Migración `SeedSantaReyes` |
| Usuarios (pass `123456789`) | INSERT `logins` (hash PBKDF2 V3 generado con `PasswordHasher<Login>`, literal en SQL) + `users` + `user_logins` + `user_companies` + `user_roles` + `user_farms` | Migración `SeedSantaReyes` |
| Módulos levante/postura/inventario | Copiar `company_menus` de company 1 (Sanmarino Colombia) **excluyendo** rutas/labels de engorde, Puente/Panamá, Ecuador (`%engorde%`, `%puente%`, `%panam%`, `/indicador-ecuador`, `/inventario-gastos`) + `role_menus` = mismo set para los 2 roles | Migración `SeedSantaReyes` |
| Regional "Occidente" | Master list `region_option_key` scopeada (company Santa Reyes, país Colombia) con opción `Occidente` | Migración `SeedSantaReyes` |
| Campos ERP de granja (6) | Columnas nullable en `farms`: `codigo_bodega`, `descripcion_bodega`, `centro_operacion`, `descripcion_centro_operacion`, `codigo_instalacion`, `descripcion_instalacion` | Migración `InfraErpAvicola` + DTOs + form granja |
| Bodega por núcleo | Columnas nullable en `nucleos`: `codigo_bodega`, `descripcion_bodega` | Ídem |
| Ubicación ERP por galpón | Columnas nullable en `galpones`: `codigo_erp_ubicacion`, `descripcion_erp_ubicacion` | Ídem |
| Silos / bodega insumos | Tabla nueva `farm_silos` (catálogo: company_id, granja_id, nombre, tipo `Silo|Insumos`, codigo_erp_ubicacion, descripcion, centro_operacion, codigo_bodega, activo) — solo catálogo por ahora; el consumo por silo es fase futura | Migración `InfraErpAvicola` |
| Centro de costo del lote | Columnas nullable en `lotes`: `codigo_centro_costo`, `descripcion_centro_costo`. Tipo Ave (ROJA/BLANCA) se guarda en `lotes.tipo_linea` | Migración `InfraErpAvicola` + DTOs + form lote |
| Visibilidad de campos ERP solo Santa Reyes | Flag tipado `companies.maneja_codigos_erp_avicola boolean NOT NULL DEFAULT false` (true solo Santa Reyes). Expuesto en `CompanyDto`; front lo lee vía servicio de config de empresa activa y condiciona los campos en los formularios | Migración + `CompanyDto` + front core |
| Guía genética opcional-hasta-cargar | En `LoteService.CreateAsync/UpdateAsync` (líneas ~166-183 y ~413-430): si la empresa **no tiene ninguna fila** en `ProduccionAvicolaRaw` → raza/año opcionales (raza texto libre); si tiene → validación actual (requeridos + existir). Front espejo: si `GET /guia-genetica/razas` viene vacío → input de texto libre opcional; si hay razas → selects required actuales. Cálculo puro en `Application/Calculos` + tests | `LoteService` + `modal-create-edit-lote` |
| Guía de Santa Reyes cuando llegue | Ya existe: pantalla `config/guia-genetica-admin` + import Excel scopeado por empresa (`guia_genetica_sanmarino_colombia.company_id`) — sin cambios | — |
| Ítems de inventario (312) | INSERT en `catalogo_items` (modelo Colombia) con `company_id` Santa Reyes, `pais_id=1`, `metadata` jsonb (referencia, tipo_inventario, categoría, um, tipo_huevo) | Migración `SeedSantaReyes` |
| Clasificación huevos Primera/Pnc | **Fase 2** (diseño §6): flag `companies.clasificacion_huevo_por_items` + desglose por ítem en `seguimiento_diario_produccion.metadata` jsonb (columna ya existe), total a `huevo_tot` para no romper consumidores | Fase 2 |
| Edad de aves trasladadas | **Fase 3** (diseño §7): tabla `lote_aves_cohortes` + captura en traslado + vista de edades del lote | Fase 3 |
| Granja/núcleos/galpones/silos/lotes de los Excel | Seeds en migración `SeedSantaReyes` (idempotentes, `WHERE NOT EXISTS`) | Migración |

---

## 4. Decisiones de arquitectura

1. **Flags por empresa, tipados y nombrados por comportamiento** (`maneja_codigos_erp_avicola`, luego `clasificacion_huevo_por_items`): columna en `companies`, default `false`, seed `true` solo para Santa Reyes. Nada de detectar país/nombre de empresa en código. Resolución/lógica pura en `Application/Calculos` cuando aplique.
2. **Front**: nuevo `ActiveCompanyConfigService` en `app/core` (caché TTL ~5 min, invalida al cambiar empresa activa vía `session$`) que consume `GET /api/Company/{activeCompanyId}` y expone los flags. Los formularios (granja, lote) muestran los campos ERP solo si el flag está activo — patrón espejo de `companyManejaAlimentoPorGalpon`.
3. **Menús**: NO se crean menús nuevos; se enlazan los existentes de Colombia a Santa Reyes (`company_menus`) y a sus roles (`role_menus`), localizando SIEMPRE por `route`, nunca por id fijo. Antes de tocar `menus`, columnas defensivas `ADD COLUMN IF NOT EXISTS key/sort_order/is_group/created_at/updated_at` (existen fuera de banda en prod, no en el modelo EF).
4. **Usuarios en migración**: hash PBKDF2 V3 (ASP.NET Identity `PasswordHasher<Login>`) generado offline e incrustado como literal — no es reproducible en SQL puro. `user_companies` SIN `pais_id` (columna ignorada por EF; el código manda).
5. **Seeds de lotes por SQL** (bypass deliberado de la validación de guía — es justamente el caso "sin guía"): `lotes` + espejo `lote_postura_levante` (todos los lotes) y, para los que superan la semana 26 a hoy, `EstadoCierre='Cerrado'` + `lote_postura_produccion` `('Abierta')` con la misma `fecha_encaset`. `fase` calculada igual que `LoteService` (>=26 semanas → `Produccion`). Cantidades de aves NULL (no vienen en el Excel). `ano_tabla_genetica` NULL, `raza` texto del Excel.
6. **Galpones→núcleos (dato ficticio documentado)**: el Excel no asigna galpones a núcleos. Distribución: Galpón 1-13 → Núcleo 1, 14-26 → Núcleo 2, 27-38 → Núcleo 3 (ajustable luego en la UI de Gestión Granjas). Silos e Insumos van a `farm_silos` (no son galpones: no deben aparecer al crear lotes).
7. **Idempotencia total**: todo `INSERT ... WHERE NOT EXISTS` / `ON CONFLICT DO NOTHING`, `ALTER TABLE ... IF NOT EXISTS`, `Down()` simétrico donde sea razonable. Las 2 migraciones deben poder correr en prod al deployar sin intervención (Database__RunMigrations=true).
8. **Datos ficticios explícitos** (empresa nueva): NIT `901000001-1`, emails `admin@santareyes.com` / `implementador@santareyes.com`, cédulas `1000000001/2`, dirección/teléfono genéricos de Buga. Marcados como editables luego por la UI.

---

## 5. FASE 1 (esta entrega) — Empresa + estructura + seeds + guía condicional

### 5.1 Migración EF #1 — `AddInfraErpAvicolaSantaReyes` (schema, idempotente)
- `companies`: `ADD COLUMN IF NOT EXISTS maneja_codigos_erp_avicola boolean NOT NULL DEFAULT false`.
- `farms`: 6 columnas ERP nullable (§3).
- `nucleos`: `codigo_bodega varchar(20)`, `descripcion_bodega varchar(200)`.
- `galpones`: `codigo_erp_ubicacion varchar(20)`, `descripcion_erp_ubicacion varchar(200)`.
- `lotes`: `codigo_centro_costo varchar(20)`, `descripcion_centro_costo varchar(200)`.
- `CREATE TABLE IF NOT EXISTS farm_silos` (+ índices + FK a farms).
- `menus`: columnas defensivas (§4.3).
- Entidades/Configurations actualizadas (Company, Farm, Nucleo, Galpon, Lote, + entidad `FarmSilo` nueva) — **el código manda**: entidad↔columna alineadas.

### 5.2 Migración EF #2 — `SeedEmpresaSantaReyes` (datos, idempotente, todo por SQL con lookups por nombre/route)
Orden interno: empresa → país → flag → master list regional → roles → role_companies → role_permissions → company_menus → role_menus → usuarios (logins/users/user_logins/user_companies/user_roles) → granja → núcleos → galpones → silos → user_farms → catálogo ítems → lotes → espejos.
- Departamento/municipio por lookup: `Valle del Cauca` + municipio `Guadalajara de Buga` (o `Buga` exacto; cuidado con `Bugalagrande` — matchear exacto primero).
- Catálogo: 312 ítems a `catalogo_items` (leer entidad `CatalogItem` para columnas reales; `metadata` jsonb con `{referencia, tipoInventario, categoria, um, tipoHuevo}` según hoja).
- Lotes: 10 (§4.5), `codigo_centro_costo` = "Ccosto y Extensión", `tipo_linea` = Tipo Ave.

### 5.3 Backend (código)
- `Company.cs` + `CompanyConfiguration` + `CompanyDto/Create/Update` + `CompanyService` mapeos: `ManejaCodigosErpAvicola`.
- `Farm/Nucleo/Galpon/Lote` entidades + Configurations + DTOs (Create/Update/Read) + services: nuevos campos ERP pass-through (sin lógica).
- `FarmSilo` entidad + configuration (sin endpoints aún; catálogo para fases futuras).
- **Guía condicional** en `LoteService`: extraer decisión a `Application/Calculos/GuiaGeneticaRequisitoCalculos.cs` (`static bool ExigirGuia(bool companyTieneGuia)` + validación de combinación) y usarla en Create/Update: `companyTieneGuia = await _ctx.ProduccionAvicolaRaw.AnyAsync(p => p.CompanyId == companyId && p.DeletedAt == null)`. Sin guía → permitir raza libre/null y año null. Con guía → comportamiento actual intacto.
- Tests xUnit: `tests/ZooSanMarino.Application.Tests/GuiaGeneticaRequisitoCalculosTests.cs` (casos §8).

### 5.4 Frontend
- `core/services/company-config/active-company-config.service.ts`: flags de empresa activa (GET /api/Company/{id}, caché, reaccciona a `session$`).
- **Form granja** (modal de `FarmListComponent`, el vivo): sección "Códigos ERP" con los 6 campos, visible solo si `manejaCodigosErpAvicola`. Modelos TS `FarmDto/CreateFarmDto/UpdateFarmDto` actualizados.
- **Gestión Granjas** (núcleos/galpones): campos bodega núcleo y ubicación ERP galpón condicionales al mismo flag (en sus modales de crear/editar).
- **Form lote** (`modal-create-edit-lote`): (a) campos `codigoCentroCosto/descripcionCentroCosto` condicionales al flag; (b) **guía condicional**: si `razasDisponibles.length === 0` → raza input texto libre OPCIONAL + año opcional (sin `Validators.required`); si hay razas → selects required como hoy. Payload respeta contrato.
- Sin `alert/confirm` nativos (ToastService/ConfirmDialogService), tokens de color centralizados, sin getters que alocan por ciclo.

### 5.5 Validación Fase 1
- `cd backend && dotnet build` + `dotnet test` (0 errores).
- `cd frontend && yarn build` (0 errores; único warning aceptado: bundle budget).
- `dotnet ef database update` contra BD local (`sanmarinoapplocal` :5433) → verificar por SQL: empresa, 2 roles, 2 usuarios, menús (>0), granja+3 núcleos+38 galpones+39 silos, 312 ítems, 10 lotes (+espejos correctos por fase).
- Smoke opcional UI: login `admin@santareyes.com` / `123456789` → seleccionar Santa Reyes → ver menú, crear granja de prueba con campos ERP visibles, crear lote sin guía.

---

## 6. FASE 2 (diseño) — Clasificación de huevos por ítems (Primera/Pnc)

**Problema**: hoy son 11 columnas fijas (`huevo_limpio`…`huevo_otro`) duplicadas en ~10 consumidores (espejo, trigger SQL, traslado huevos, fn indicadores, reporte técnico, front). Santa Reyes clasifica por ítem de inventario con categoría Primera/Pnc.

**Diseño (mínimo invasivo, sin tocar el esquema de columnas)**:
- Flag `companies.clasificacion_huevo_por_items boolean NOT NULL DEFAULT false` (true Santa Reyes).
- El modal de seguimiento diario de producción, con flag activo, reemplaza los 11 inputs por filas dinámicas: ítem de huevo (catálogo `catalogo_items` categoría HUEVO de la empresa) + cantidad. El desglose viaja en el payload y se guarda en `seguimiento_diario_produccion.metadata` jsonb bajo `huevoItems: [{catalogItemId, codigo, nombre, tipoHuevo, cantidad, um}]` (la columna YA existe; mismo patrón que `itemsHembras/Machos`).
- `huevo_tot` = suma de cantidades (mantiene vivos: espejo, trigger, saldos, indicadores que usan el total). Las 11 columnas quedan en 0 para Santa Reyes; `huevo_inc` = 0 (comercial, no incuba).
- Lectura/edición: el modal reconstruye las filas desde metadata. Reportes por tipo Primera/Pnc = fase posterior (nueva fn SQL que expanda el jsonb; no bloquea).
- Todo gated por flag: cero impacto para Sanmarino/Ecuador/Panamá.

## 7. FASE 3 — Edades por cohorte + traslado cross-etapa levante→producción

**Problema**: 1 lote = 1 `fecha_encaset` = 1 edad; el traslado no registra edad; Santa Reyes pasa aves ~semana 16 a lotes que pueden tener otra edad, y necesita ver "edad de las aves que pasaron" contando días desde el encaset del lote ORIGEN.

### 7.1 Backend (esta entrega)

**Flag de empresa** — `companies.permite_traslado_aves_cross_etapa boolean NOT NULL DEFAULT false` (patrón idéntico a `maneja_codigos_erp_avicola` / `clasificacion_huevo_por_items`): entidad `Company` + `CompanyConfiguration` + `CompanyDto`/`Create`/`Update` + TODAS las proyecciones (`CompanyService.ToDto`, `CompanyService.Crud`, `CompanyResolver` ×2, `CompanyPaisService`). `true` solo para Santa Reyes.

**Tabla nueva `lote_aves_cohortes`** (entidad `LoteAvesCohorte : AuditableEntity` + `LoteAvesCohorteConfiguration` + DbSet):
`id` (identity PK), `company_id`, `lote_id` (RECEPTOR, FK `lotes.lote_id` Restrict), `lote_origen_id?`, `movimiento_aves_id?`, `fecha_ingreso date`, `fecha_encaset_cohorte date` (la del lote ORIGEN → la edad de la cohorte se calcula SIEMPRE desde esta fecha), `cantidad_hembras`, `cantidad_machos`, `observaciones varchar(300)?`, auditoría (`created_by_user_id`, `created_at`, `updated_*`, `deleted_at`). Índices por `lote_id`, `company_id`, `lote_origen_id`. Fechas puras con `DateOnly` ↔ `date` (sin trampa de zona horaria). La cohorte "propia" del lote es implícita (su `lotes.fecha_encaset`).

**Cálculo puro** `Application/Calculos/LoteCohortesCalculos.cs` (+ xUnit):
- `EdadDias(DateOnly encaset, DateOnly fecha)` → días transcurridos, **clamp a 0** si la fecha es anterior al encaset.
- `EdadSemanas(...)` → delega en `MovimientoAvesCalculos.SemanaDesdeEncaset` (única fórmula: `días/7 + 1`; día 0 = semana 1).
- `EsMismaEtapa(origen, destino)` / `PuedeTrasladarCrossEtapa(companyPermite, origen, destino)`: misma etapa siempre `true`; etapas distintas solo con flag **y** en el sentido `Levante → Produccion` (producción→levante NUNCA).
- `MensajeCrossEtapaBloqueado(origen, destino)`: conserva EXACTO el texto actual del bloqueo.

**Traslado** (`TrasladoAvesDesdeSegService`, partido en `Funciones/` como partial class):
- La validación de etapa pasa a delegar en `PuedeTrasladarCrossEtapa`. La empresa se resuelve por `farms.company_id` de la granja del lote ORIGEN (fail-closed: si no resuelve → se comporta como flag `false`), y en cross-etapa se exige que el lote destino sea de la MISMA empresa. **Misma etapa = camino actual byte a byte** (ni una query extra en el camino de decisión).
- Cross-etapa `Levante→Produccion`: la pata ORIGEN sigue el camino de levante (SALIDA en `seguimiento_diario` + acumulados `lote_postura_levante` + `aves_*_actual`) y la pata DESTINO el de producción (INGRESO en `seguimiento_diario_produccion` + acumulados `lote_postura_produccion` + `aves_*_actual`); ambas patas se extrajeron a helpers privados sin cambiar aritmética.
- **Todo traslado** (misma etapa o cross) inserta cohorte en el lote DESTINO con `fecha_encaset_cohorte` = encaset del lote ORIGEN (`lotes.fecha_encaset`, si es null la del espejo; si ambas null NO se crea cohorte y el traslado NO falla), dentro de la MISMA transacción y ligada al `movimiento_aves_id` de auditoría.
- Anulación: `MovimientoAvesService.EliminarMovimientoAsync` (único camino de reversión existente) soft-deletea las cohortes ligadas por `movimiento_aves_id`.

**Lectura**: `GET api/traslados/cohortes/{loteId}` → `{ loteId, loteNombre, fechaEncasetPropia, edadPropiaDias, edadPropiaSemanas, cohortes: [{ id, loteOrigenId, loteOrigenNombre, fechaIngreso, fechaEncasetCohorte, edadDias, edadSemanas, cantidadHembras, cantidadMachos, observaciones }] }` (edades a HOY, scope por empresa efectiva de la granja del lote, cohortes vivas, orden `fecha_ingreso` desc).

**Migración** `20260725210000_AddCohortesTrasladoCrossEtapa` (idempotente): `CREATE TABLE IF NOT EXISTS lote_aves_cohortes` + índices `IF NOT EXISTS` + `ALTER TABLE companies ADD COLUMN IF NOT EXISTS permite_traslado_aves_cross_etapa` + `UPDATE companies SET ... = true WHERE name = 'Santa Reyes'` (corre DESPUÉS del seed `20260725190000`).

### 7.2 Front (entrega paralela)
- Selector de etapa destino en el modal de traslado (solo con flag on y origen levante).
- Bloque "Edades en el lote" en seguimiento levante y producción: cohorte propia + cohortes recibidas (cantidad H/M, edad actual en semanas/días, lote origen, fecha ingreso).
- El cierre del lote levante sigue siendo la acción manual existente (`/cerrar`).

### 7.3 Consideración reportes
Indicadores semanales siguen usando la edad del lote (guía). Las edades por cohorte son informativas/operativas en v1 (alimento por edad, vacunación). Integración a indicadores = fase posterior si se pide.

---

## 8. Casos de prueba

**Guía condicional (xUnit `GuiaGeneticaRequisitoCalculosTests`)**
1. Empresa sin filas de guía + raza null + año null → válido (no exige).
2. Empresa sin filas + raza texto libre + año null → válido.
3. Empresa con guía + raza null → inválido ("requeridos").
4. Empresa con guía + raza/año no existentes en guía → inválido (mensaje actual).
5. Empresa con guía + combinación válida → válido (comportamiento actual intacto).

**Migraciones (SQL post-update local)**
6. Re-ejecutar `Up()` (doble `database update` simulado) → sin duplicados (idempotencia).
7. Conteos: 1 empresa, 1 company_pais, 2 roles (is_company_admin=true), 2 usuarios logueables, company_menus>0 sin rutas engorde/panamá, granja=1, núcleos=3, galpones=38, farm_silos=39, catalogo_items=312 (45 alimento/246 insumo/21 huevo), lotes=10 con espejos coherentes (fase por semanas a hoy).
8. Lote LOTE 234 (encaset 2026-02-24, ~21 sem) → `fase='Levante'`, LPL Abierto; LOTE 216 (2024-11-22) → `fase='Produccion'`, LPL Cerrado + LPP Abierta.

**Front (manual/build)**
9. Empresa activa Sanmarino → forms de granja/lote SIN campos ERP (regresión cero).
10. Empresa activa Santa Reyes → campos ERP visibles; crear lote sin guía permite raza libre/vacía; al existir guía (cargar 1 fila) el form vuelve a selects required.

---

## 7bis. FASE 3 — Alcance de implementación (en curso 25-jul PM)

- Flag `companies.permite_traslado_aves_cross_etapa` (default false; true Santa Reyes) — migración `20260725210000`.
- Tabla `lote_aves_cohortes` (receptor, origen, fecha_encaset_cohorte = encaset del ORIGEN, cantidades, movimiento de auditoría, soft-delete).
- `TrasladoAvesDesdeSegService`: decisión cross-etapa pura (`LoteCohortesCalculos.PuedeTrasladarCrossEtapa` — solo levante→producción, misma empresa por `farms.company_id`); TODO traslado registra cohorte en el destino, en la misma transacción. Flag off = byte a byte actual.
- `GET api/traslados/cohortes/{loteId}`: edad propia + cohortes con edad actual (días/semanas) calculada en backend.
- Front: selector "Etapa destino" en el modal de traslado (solo flag on + origen levante) y bloque "Edades en el lote" (componente `edades-lote`) en seguimiento de levante y producción — visible para todas las empresas (sin cohortes = línea informativa).
- Liquidación manual SR (~sem 16): traslado cross-etapa de las aves al lote de producción destino (queda cohorte con edad) + cierre manual existente del levante. Sin cierres automáticos.

## 10. FASE 4 — Empresa Demo lista para evaluación (flag off, flujo clásico)

Objetivo: los evaluadores prueban el flujo estándar en **Demo** sin ver NADA de Santa Reyes; al final se les muestra Santa Reyes parametrizada.
1. Auditoría Demo (BD local): flags en false, menús, usuarios/roles, granjas/núcleos/galpones, lotes/espejos, guía genética propia (CRÍTICO: sin guía, el form de lote mostraría raza libre — comportamiento SR; Demo debe tener guía cargada para exhibir el flujo clásico con selects obligatorios), catálogo + item_inventario (dropdown alimento), master list regional.
2. Completar faltantes mínimos vía migración idempotente `AlistarDemoParaPruebas` (solo lo que impida probar; sin inventar data masiva).
3. Smoke doble: Demo (sin campos ERP, sin clasificación por ítems, guía obligatoria) y Santa Reyes (todo lo nuevo).

## 11. FASE 5 — Reportes levante/producción adaptados a Santa Reyes

Con clasificación por ítems, las 11 columnas fijas quedan en 0 para SR → los consumidores que muestran ese desglose (reporte técnico producción "Clasificación Huevo Comercio", grillas de indicadores) mostrarían ceros. Adaptación mínima sobre lo existente:
1. Auditoría (en curso): qué usa `huevo_tot` (sigue OK) vs. las 11 columnas; impacto de aves iniciales null y guía null (esperado: vacío con mensaje, sin romper).
2. Desglose Primera/Pnc desde `metadata->'huevoItems'`: función SQL nueva en `/backend/sql` (expande jsonb por día, agrupa por tipo/ítem) + endpoint delgado + sección front en el host natural (indicadores producción / reporte técnico), visible solo con `clasificacion_huevo_por_items`.
3. Con flag off: reportes intactos (regresión cero).
4. Lo que quede fuera de "lo que tenemos ahora" (reportes nuevos a medida SR) se levanta como requerimiento aparte con el cliente.

## 9. Despliegue

1. Mergear con build+tests verdes (gate CI obligatorio).
2. Push a `main-produccion` → pipeline aplica las migraciones al arrancar (Database__RunMigrations=true). **Sin DDL manual en prod.**
3. Verificación post-deploy obligatoria (TaskDef/imagen/rollout) + smoke login Santa Reyes.
4. Rollback: migraciones nuevas son aditivas (columnas nullable + tabla nueva + seeds); `Down()` disponible. Sin impacto sobre empresas existentes (flags en false).
