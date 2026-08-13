# Plan — SANTA REYES: silos y bodegas como ubicación real del inventario (postura)

> Fecha: 2026-08-12 · Empresa objetivo: **Santa Reyes** (company_id 6, Colombia pais_id 1, postura comercial).
> Continuación de `santa_reyes_implementacion_plan.md` (Fases 1-5 ya en `main`). Aquí arranca la **Fase 6**.
> Levantamiento: entidades/servicios de inventario, seguimiento levante+producción, granjas/galpones/lotes,
> `Granja.xlsx` del cliente, BD local (`sanmarinoapplocal:5433`) y menús habilitados de la empresa.

---

## 1. El pedido, en una frase

Santa Reyes no mueve alimento «sobre el galpón»: lo mueve **sobre silos** (y sobre una **bodega** de granja).
El galpón deja de ser la ubicación del inventario y pasa a ser el **filtro** que dice qué silos mirar.
Y el lote declara **de qué silo(s) consume**, para que el seguimiento diario ofrezca solo esos.

Cadena completa que hay que habilitar:

```
Lista maestra de silos (1..100, por EMPRESA)
        │  se asignan a
        ▼
   GRANJA  ──►  farm_silos (los silos + la bodega que esa granja tiene)
        │             │
        │             ├──► GALPÓN  (galpon_silos: qué silos alimentan a este galpón)  ─ N:M
        │             │
        │             └──► LOTE    (lote_silos: de qué silos consume este lote)       ─ N:M
        ▼
  INVENTARIO: ingreso / traslado / consumo apuntan a un SILO o a la BODEGA
```

---

## 2. Hallazgo que fija la arquitectura (no es una opinión, está en el dato del cliente)

`Requerimiento Santa reyes\Granja.xlsx`, hoja **«Galpones y Silos»** (77 filas):

| Fila | Cantidad | `Movimiento` | Bodega ERP |
|---|---|---|---|
| `Silo 1..38` | 38 | **`Alimento`** | `B0601` (la de la **granja**) |
| `Insumos` (`BUG60100`) | 1 | **`Insumos`** | `B0601` |
| `Galpón 1..38` | 38 | **`Aves, Huevo, Insumos`** | `B0601` |

> **En el ERP del cliente el galpón NO mueve alimento.** El alimento se mueve en el silo; el galpón mueve
> aves, huevo e insumos. Y los 38 silos cuelgan de la bodega de la **granja**, no de un galpón.

De ahí sale la decisión estructural, ya confirmada por el usuario:

- **El stock vive en el SILO** (`farm_id` + `silo_id`, con `nucleo_id`/`galpon_id` en **NULL**).
- **El galpón es navegación**, no contenedor: sirve para desplegar «qué silos puedo elegir», nada más.
- **Un silo puede alimentar a varios galpones (N:M).** Por eso el stock *no puede* llevarse por
  `(galpón, silo)`: el mismo silo físico quedaría partido en dos saldos y ninguno sería el real.
- La **bodega** es una ubicación más de la granja (`tipo='Bodega'`), guarda **alimento e insumos**, y se
  admite el traslado interno **bodega → silo** y **silo → silo**.
- Con el flag encendido, **todo ítem** (alimento e insumos) exige ubicación silo/bodega. No hay movimiento
  «suelto a nivel granja» en Santa Reyes.

### Condición de arranque que abarata todo (verificada en BD)

```
company 6 (Santa Reyes):  inventario_gestion_movimiento = 0 filas
                          inventario_gestion_stock      = 0 filas
                          lotes (seguimiento)           = 0 filas
                          farm_silos                    = 39 filas (38 Silo + 1 Insumos, granja 109)
```

**No hay backfill.** No hay que migrar saldos históricos ni reatribuir movimientos: la empresa empieza a
operar directamente en el modelo nuevo. Es la ventana barata para hacer este cambio.

---

## 3. Flag por empresa

Siguiendo la sección «🏢 Features por EMPRESA» de CLAUDE.md — nombrado por **comportamiento**, jamás por
tenant:

```sql
ALTER TABLE companies
  ADD COLUMN IF NOT EXISTS maneja_inventario_por_silo boolean NOT NULL DEFAULT false;
```

- `true` solo para Santa Reyes (company 6).
- `false` (default) ⇒ **comportamiento byte a byte idéntico** para Sanmarino, Ecuador, Panamá y Demo.
- Viaja al front en `CompanyDto` — hay que agregarlo en **las 4 proyecciones** (gotcha ya cazado en Fase 1):
  `CompanyService.ToDto`, `CompanyService.Crud`, `CompanyResolver`, `CompanyPaisService`.
- Front lo lee por `ActiveCompanyConfigService` (caché 5 min, **fail-closed**: error/ausente ⇒ `false`).

---

## 4. Modelo de datos

### 4.1 `silo_catalogo` — NUEVA (la lista maestra 1..100, por empresa)

```sql
CREATE TABLE IF NOT EXISTS public.silo_catalogo (
    id          serial       PRIMARY KEY,
    company_id  integer      NOT NULL,
    numero      integer      NOT NULL,          -- 1..100
    nombre      varchar(120) NOT NULL,          -- 'Silo 1' … 'Silo 100'
    descripcion varchar(200) NULL,
    activo      boolean      NOT NULL DEFAULT true,
    created_at  timestamptz  NOT NULL DEFAULT now(),
    updated_at  timestamptz  NULL,
    deleted_at  timestamptz  NULL,
    CONSTRAINT fk_silo_catalogo_company FOREIGN KEY (company_id) REFERENCES companies(id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_silo_catalogo_company_numero ON silo_catalogo (company_id, numero)
    WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_silo_catalogo_company_nombre ON silo_catalogo (company_id, nombre)
    WHERE deleted_at IS NULL;
```

**Solo silos numerados.** La *bodega* NO va acá: es una ubicación propia de cada granja (la «granja global»
del pedido), se crea directamente en `farm_silos` con `tipo='Bodega'`.

> **Por qué no `master_lists`.** El repo ya tiene listas maestras genéricas (patrón `region_option_key`) y
> es lo primero que uno mira. No sirve acá: `master_lists` es clave/valor de texto y el silo necesita
> `numero` tipado, `activo`, y ser **destino de FK** desde `farm_silos`. Una FK contra una lista de strings
> no se puede garantizar. Tabla propia.

### 4.2 `farm_silos` — EXISTE, se extiende

Ya está creada (migración `20260725175311_AddInfraErpAvicolaSantaReyes`) y **poblada con los 39 registros de
la granja 109**. Se le agrega:

```sql
ALTER TABLE public.farm_silos
  ADD COLUMN IF NOT EXISTS silo_catalogo_id integer NULL,     -- FK → silo_catalogo (NULL para la bodega)
  ADD COLUMN IF NOT EXISTS updated_at       timestamptz NULL,
  ADD COLUMN IF NOT EXISTS deleted_at       timestamptz NULL; -- baja lógica (hoy solo hay `activo`)
```

- `tipo` pasa a admitir **`'Silo' | 'Bodega'`**. La fila existente con `tipo='Insumos'` se normaliza a
  `'Bodega'` por migración data-only idempotente (`WHERE tipo='Insumos' AND company_id=6`).
- El índice único vigente `ux_farm_silos_granja_nombre (granja_id, nombre)` se conserva.
- Los códigos ERP (`codigo_erp_ubicacion`, `centro_operacion`, `codigo_bodega`) se quedan acá: son
  **por granja**, no del catálogo (el silo 1 de La Esperanza es `BS60101`; el silo 1 de otra granja no).

### 4.3 `galpon_silos` — NUEVA (N:M galpón ↔ silo)

```sql
CREATE TABLE IF NOT EXISTS public.galpon_silos (
    id           serial      PRIMARY KEY,
    company_id   integer     NOT NULL,
    granja_id    integer     NOT NULL,
    nucleo_id    varchar(20) NOT NULL,
    galpon_id    varchar(20) NOT NULL,
    farm_silo_id integer     NOT NULL,
    activo       boolean     NOT NULL DEFAULT true,
    created_at   timestamptz NOT NULL DEFAULT now(),
    created_by_user_id uuid  NULL,
    CONSTRAINT fk_galpon_silos_farm_silo FOREIGN KEY (farm_silo_id)
        REFERENCES farm_silos(id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_galpon_silos_galpon_silo
    ON galpon_silos (granja_id, nucleo_id, galpon_id, farm_silo_id);
CREATE INDEX IF NOT EXISTS ix_galpon_silos_silo ON galpon_silos (farm_silo_id);
```

**Invariante de servicio:** `farm_silos.granja_id == galpon_silos.granja_id`. Se valida en el service (no se
puede en FK compuesta sin desnormalizar) y se cubre con test.

> ⚠️ **Gotcha heredado — `fn_mover_galpon` / `fn_rekey_nucleo`** (`backend/sql/fn_mover_ubicacion.sql`).
> Estas funciones reescriben `nucleo_id` en todas las tablas que lo referencian cuando un galpón cambia de
> núcleo. Ya nos mordió antes con `nucleos.codigo_bodega` ([[crud-ubicacion-nucleo-galpon-lote]]).
> `galpon_silos` guarda el trío `(granja, nucleo, galpon)` ⇒ **hay que agregar su `UPDATE` a las DOS
> funciones**, o mover un galpón de núcleo dejaría sus silos huérfanos y el seguimiento se quedaría sin
> ubicaciones que ofrecer. Va en el mismo bloque que `inventario_gestion_movimiento`/`_stock`.

### 4.4 `lote_silos` — NUEVA (N:M lote ↔ silo: de dónde consume)

```sql
CREATE TABLE IF NOT EXISTS public.lote_silos (
    id           serial      PRIMARY KEY,
    company_id   integer     NOT NULL,
    lote_id      integer     NOT NULL,          -- lotes.lote_id (el maestro, sobrevive levante→producción)
    farm_silo_id integer     NOT NULL,
    activo       boolean     NOT NULL DEFAULT true,
    created_at   timestamptz NOT NULL DEFAULT now(),
    created_by_user_id uuid  NULL,
    CONSTRAINT fk_lote_silos_lote  FOREIGN KEY (lote_id)      REFERENCES lotes(lote_id) ON DELETE CASCADE,
    CONSTRAINT fk_lote_silos_silo  FOREIGN KEY (farm_silo_id) REFERENCES farm_silos(id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_lote_silos_lote_silo ON lote_silos (lote_id, farm_silo_id);
```

**Se cuelga de `lotes.lote_id`, NO de `lote_postura_levante`/`_produccion`.** Razón: el pedido dice
explícitamente que el lote arrastra su silo en levante **y** en producción; `lotes` es la única fila que
sobrevive a las dos etapas (los espejos LPL/LPP se crean y cierran). Un solo registro sirve a los dos
seguimientos y no hay que copiarlo al cerrar el levante.

### 4.5 Columnas de ubicación en inventario

```sql
ALTER TABLE public.inventario_gestion_stock
  ADD COLUMN IF NOT EXISTS silo_id integer NULL;      -- FK → farm_silos(id)

ALTER TABLE public.inventario_gestion_movimiento
  ADD COLUMN IF NOT EXISTS silo_id      integer NULL, -- destino / ubicación del movimiento
  ADD COLUMN IF NOT EXISTS from_silo_id integer NULL; -- origen del traslado (espejo de from_galpon_id)

ALTER TABLE public.lote_registro_historico_unificado
  ADD COLUMN IF NOT EXISTS silo_id integer NULL;      -- trazabilidad en el espejo
```

### 4.6 ⚠️ El índice único de la clave natural — el punto más delicado del plan

Hoy:

```sql
ux_inventario_gestion_stock_clave_natural
  ON inventario_gestion_stock (farm_id, item_inventario_ecuador_id,
                               COALESCE(nucleo_id,''), COALESCE(galpon_id,''))
```

Nuevo:

```sql
DROP   INDEX IF EXISTS ux_inventario_gestion_stock_clave_natural;
CREATE UNIQUE INDEX ux_inventario_gestion_stock_clave_natural
  ON inventario_gestion_stock (farm_id, item_inventario_ecuador_id,
                               COALESCE(nucleo_id,''), COALESCE(galpon_id,''),
                               COALESCE(silo_id, 0));
```

Tres cosas que **no** se pueden pasar por alto:

1. **`SumarStockAtomicoAsync` tiene el `ON CONFLICT` cableado a la expresión del índice**
   (`InventarioGestionService.StockAtomico.cs`). Postgres exige que el inferidor coincida **exactamente**
   con el índice. Si se cambia el índice y no la sentencia, **todo ingreso de todas las empresas revienta**
   con `no unique or exclusion constraint matching the ON CONFLICT specification`. Los dos se tocan en el
   mismo commit, y el smoke de la Fase B empieza por un ingreso en Sanmarino (flag OFF).
2. Para empresas con flag OFF, `silo_id` es siempre `NULL` ⇒ `COALESCE(silo_id,0) = 0` constante ⇒ la clave
   es **equivalente a la anterior**. Ningún saldo se parte ni se fusiona. Verificable antes/después con un
   conteo por clave.
3. El `RETURNING *` del upsert materializa la entidad completa: agregar la columna a la tabla **y** a
   `InventarioGestionStock` va junto, o EF falla al mapear.

### 4.7 Espejo histórico

El trigger `trg_lote_hist_desde_inventario_gestion` atribuye el lote por
`fn_lote_ave_engorde_id_desde_ubicacion(farm, nucleo, galpon)` — **es de engorde**, y Santa Reyes es postura
sin engorde ⇒ devuelve `NULL` igual que hoy. **La atribución de lote no cambia.** Solo se agrega
`NEW.silo_id` al `INSERT` del trigger para que la fila del espejo diga en qué silo pasó. Cambio aditivo:
las empresas con flag OFF escriben `NULL`, exactamente lo que hay hoy.

Se respeta el invariante de CLAUDE.md: **el histórico se anula, nunca se abandona**. Los triggers
`_del` y `_cancel` siguen intactos.

---

## 5. La decisión, como lógica pura (obligatoria + tests)

`backend/src/ZooSanMarino.Application/Calculos/InventarioUbicacionSiloCalculos.cs` — `static class`, sin EF:

```csharp
public enum ModoUbicacionInventario { Clasico, PorSilo }

static ModoUbicacionInventario ResolverModo(bool companyManejaInventarioPorSilo);

/// PorSilo: siloId obligatorio (>0) y nucleo/galpon SIEMPRE se normalizan a NULL en stock y movimiento.
/// Clasico: siloId debe venir null; si viene, se rechaza (evita datos mezclados).
static (string? NucleoId, string? GalponId, int? SiloId) NormalizarUbicacion(
    ModoUbicacionInventario modo, string? nucleoId, string? galponId, int? siloId);

static string? ValidarUbicacion(ModoUbicacionInventario modo, int? siloId, string? galponId, bool esAlimento);
```

Tests xUnit en `tests/ZooSanMarino.Application.Tests/InventarioUbicacionSiloCalculosTests.cs`:
flag OFF ⇒ salida **idéntica** a la entrada (mensajes de error incluidos, byte a byte); flag ON ⇒ silo
obligatorio, núcleo/galpón anulados, mensaje explícito cuando falta el silo.

**Gate de CI:** sin estos tests verdes no se mergea (regla de la sección 🚀 de CLAUDE.md).

`ItemConsumoKey` gana el silo sin romper llamadores:

```csharp
// antes: public readonly record struct ItemConsumoKey(int Id, bool EsItemInventario);
public readonly record struct ItemConsumoKey(int Id, bool EsItemInventario, int? SiloId = null);
```

Con `SiloId = null` la igualdad y el hash son los de hoy ⇒ Colombia/Ecuador/Panamá sin cambios.

---

## 6. Backend — archivos y servicios

### 6.1 Dominio + Configurations (nuevos)

| Archivo | Qué |
|---|---|
| `Domain/Entities/SiloCatalogo.cs` | entidad de la lista maestra |
| `Domain/Entities/GalponSilo.cs` | N:M galpón↔silo |
| `Domain/Entities/LoteSilo.cs` | N:M lote↔silo |
| `Domain/Entities/FarmSilo.cs` | **+** `SiloCatalogoId`, `UpdatedAt`, `DeletedAt`; doc-comment actualizado (deja de ser «solo catálogo, fase futura») |
| `Domain/Entities/InventarioGestionStock.cs` | **+** `SiloId` |
| `Domain/Entities/InventarioGestionMovimiento.cs` | **+** `SiloId`, `FromSiloId` |
| `Domain/Entities/Company.cs` | **+** `ManejaInventarioPorSilo` |
| `Persistence/Configurations/*` | 3 configuraciones nuevas + 4 tocadas |
| `Persistence/ZooSanMarinoContext.cs` | 3 `DbSet` nuevos |

### 6.2 Servicios nuevos (`Application/Interfaces` + `Infrastructure/Services/Silos/`)

Carpeta propia `Infrastructure/Services/Silos/` con el patrón partial de CLAUDE.md
(namespace **plano** `ZooSanMarino.Infrastructure.Services`):

| Servicio | Responsabilidad | Endpoints |
|---|---|---|
| `SiloCatalogoService` | CRUD de la lista maestra 1..100 (scope empresa activa) | `GET/POST/PUT/DELETE /api/SiloCatalogo`, `POST /api/SiloCatalogo/generar-rango` (crea 1..N de una) |
| `FarmSiloService` | Silos+bodega **de una granja**: asignar desde el catálogo, editar códigos ERP, baja lógica | `GET /api/FarmSilo?granjaId=`, `POST`, `PUT/{id}`, `DELETE/{id}`, `POST /api/FarmSilo/asignar-desde-catalogo` |
| `GalponSiloService` | Qué silos alimentan a un galpón | `GET /api/GalponSilo?granjaId=&nucleoId=&galponId=`, `PUT` (reemplaza el set) |
| `LoteSiloService` | De qué silos consume un lote + los elegibles según su galpón | `GET /api/LoteSilo/{loteId}`, `GET /api/LoteSilo/{loteId}/disponibles`, `PUT /api/LoteSilo/{loteId}` |

**Scoping fail-closed en los 4** (patrón `InventarioCatalogoScopeCalculos`): si no se resuelve empresa activa
o la granja no es de la empresa ⇒ lista vacía / error, **nunca** datos de otra empresa.

### 6.3 `InventarioGestionService` — el service grande (2.739 líneas)

Ya tiene la carpeta `Funciones/` iniciada (`InventarioGestionService.StockAtomico.cs`). Se agrega **un
partial nuevo** en vez de engordar el ancla:

`Infrastructure/Services/InventarioGestion/Funciones/InventarioGestionService.Silos.cs`
- `ResolverModoUbicacionAsync(farmId, ct)` → lee el flag de la empresa **dueña de la granja** (no la activa:
  fail-closed por dato, patrón `ResolverCompanyIdDeGranjaAsync`).
- `ValidarSiloDeGranjaAsync(farmId, siloId, ct)` → el silo existe, está activo y es de esa granja.
- `GetSilosElegiblesAsync(farmId, nucleoId, galponId, ct)` → si viene galpón, los de `galpon_silos`;
  si no, todos los de la granja. **Nuevo endpoint** `GET /api/InventarioGestion/silos`.

Métodos del ancla que reciben el silo (todos pasan por `NormalizarUbicacion` antes de tocar la BD):

| Método | Cambio |
|---|---|
| `RegistrarIngresoAsync` | `req.SiloId` → normaliza → `SumarStockAtomicoAsync(..., siloId)` |
| `RegistrarTrasladoMismaGranjaAsync` | `FromSiloId` → `ToSiloId`; habilita **bodega→silo** y **silo→silo** |
| `RegistrarTrasladoInterGranjaTransitoAsync` | silo de origen; el destino se elige al recibir |
| `RegistrarRecepcionTransitoAsync` | `Distribucion` reparte por **silo** (hoy reparte por galpón) |
| `RegistrarConsumoAsync` / `RegistrarConsumoNivelGranjaAsync` | `siloId` en la clave de descuento |
| `RegistrarIngresoNivelGranjaAsync` | ídem (devoluciones) |
| `GetStockAsync` / `GetMovimientosAsync` / `GetIngresosAsync` / `GetTrasladosAsync` | filtro `siloId` + join a `farm_silos` para `SiloNombre` |
| `ActualizarStockAsync` / `EliminarStockAsync` / `AnularMovimientoHistoricoAsync` | arrastran el silo de la fila |
| `GetFilterDataAsync` | devuelve los silos de las granjas del usuario + el flag |

Las primitivas atómicas (`SumarStockAtomicoAsync`, `DescontarStockAtomicoAsync`,
`BuscarStockSinRastreoAsync`) suman el parámetro `siloId` y el `ON CONFLICT` se alinea al índice nuevo (§4.6).

### 6.4 DTOs (`Application/DTOs/InventarioGestionDtos.cs`)

Todos los campos nuevos van **al final y con default**, para no romper llamadores posicionales:

- `InventarioGestionIngresoRequest` → `int? SiloId = null`
- `InventarioGestionTrasladoRequest` → `int? FromSiloId = null, int? ToSiloId = null`
- `InventarioGestionConsumoRequest` → `int? SiloId = null`
- `InventarioGestionRecepcionDestinoDto` → `int? SiloId = null`
- `InventarioGestionStockDto` / `...MovimientoDto` / `...IngresoListDto` / `...TrasladoListDto` →
  `int? SiloId`, `string? SiloNombre` (+ `FromSiloId`/`FromSiloNombre` en traslados)
- `InventarioGestionFilterDataDto` → `IEnumerable<FarmSiloDto> Silos`, `bool CompanyManejaInventarioPorSilo = false`

DTOs nuevos: `SiloCatalogoDto` (+Create/Update), `FarmSiloDto` (+Create/Update), `GalponSiloDto`,
`LoteSiloDto`, `AsignarSilosRequest(int[] FarmSiloIds)`.

### 6.5 Consumo desde el seguimiento diario

**Contrato del metadata** — cada ítem de alimento gana `siloId`:

```jsonc
{ "itemsHembras": [ { "itemInventarioEcuadorId": 150, "siloId": 4, "cantidad": 320, "unidad": "kg" } ] }
```

- `MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen` lee `siloId` y lo mete en `ItemConsumoKey`.
  Ítem sin `siloId` ⇒ `null` ⇒ comportamiento actual. **Dos filas del mismo ítem en silos distintos son
  claves distintas y se descuentan por separado** — que es justo lo que pide el negocio.
- `ParseMetadataItemsToKg` (variante plana, Ecuador/Panamá) **no se toca**.
- `ColombiaInventarioConsumoService`: `ValidarStockConsumoAsync` / `AplicarConsumoAsync` /
  `AplicarDevolucionAsync` / `AplicarDiffAsync` propagan `key.SiloId`. El `WHERE` de disponibilidad pasa a
  `x.NucleoId == null && x.GalponId == null && x.SiloId == key.SiloId`.
  Con flag OFF, `SiloId == null` ⇒ el `WHERE` es el de hoy.
- **Validación nueva (flag ON):** el `siloId` de cada fila debe estar en `lote_silos` del lote. Si no,
  se rechaza con mensaje explícito antes de persistir (misma transacción atómica que ya existe).
- Services tocados: `SeguimientoLoteLevanteService.Crud.cs` (líneas ~109 y ~243) y
  `SeguimientoProduccionService` / `ProduccionService` (la rama Colombia modelo B).

Los caminos de **Ecuador/Panamá** (`ModeloB` con núcleo+galpón) y los de **engorde** no se tocan: quedan
fuera del `if` por gate de modelo, igual que hoy.

---

## 7. Frontend

Flag nuevo en `core/services/company-config/active-company-config.service.ts`:
`manejaInventarioPorSilo: boolean` (+ en `FLAGS_APAGADOS`).

| # | Pantalla (ruta habilitada en SR) | Qué se agrega |
|---|---|---|
| 1 | **`/config/silos`** — NUEVA, bajo Configuración | ABM de la lista maestra 1..100 + botón «generar rango». Menú solo para empresas con el flag (`company_menus` + `role_menus`, localizando por `route`) |
| 2 | `/config/farm-management` → `farm-list` / `farm-form` | Sección **«Silos de la granja»**: multiselect desde el catálogo + la bodega (auto-creada al guardar), con sus códigos ERP. Gated por flag |
| 3 | `/config/farm-management` → `galpon-form` (Gestión Granjas) | **«Silos que alimentan este galpón»**: multiselect de los silos de la granja. Gated |
| 4 | `/config/lote-management` → **`lote-list`** (el form **VIVO**) | **«Silos de consumo del lote»**: multiselect de los silos del galpón del lote (`GET /LoteSilo/{id}/disponibles`), editable después de crear. Gated |
| 5 | `/gestion-inventario` (page + historial) | Ingreso y Traslado: tras elegir galpón, selector **Silo / Bodega** (obligatorio con flag). Columna «Silo» en stock, histórico, ingresos y traslados + en los export a Excel. Recepción de tránsito reparte por silo |
| 6 | `/daily-log/seguimiento` → `lote-levante/modal-create-edit` | Selector de **silo por fila de alimento**; el dropdown de ítems se filtra al stock **de ese silo**; el disponible que muestra es el del silo |
| 7 | `/daily-log/produccion` → `lote-produccion/modal-seguimiento-diario` | Idéntico al 6 |

**Reglas de front que este plan hereda y no negocia** (CLAUDE.md):
- Todo componente/modal nuevo lleva **`changeDetection: ChangeDetectionStrategy.Eager` explícito**
  (Angular 22: omitirlo = OnPush = el modal se cuelga en «Cargando…»). Los 4 modales nuevos entran acá.
- `ToastService` / `ConfirmDialogService` — **prohibido** `alert()`/`confirm()` nativos.
- Export a Excel por `shared/utils/excel/exportar-tabla-excel.funcion.ts`, nunca `XLSX` inline.
- Lógica pura de cada pantalla a `funciones/<accion>.funcion.ts` + tipos a `models/`
  (referencia canónica: `movimientos-pollo-engorde`).
- Sin getters de template que alojen arrays nuevos por ciclo (rompe CD — [[ng0103-getters-arrays-nuevos]]).

---

## 8. Migraciones EF (idempotentes, en este orden)

| # | Nombre | Tipo | Contenido |
|---|---|---|---|
| 1 | `AddInventarioPorSilo` | schema | flag en `companies`; `silo_catalogo`; columnas nuevas de `farm_silos`; `galpon_silos`; `lote_silos`; `silo_id`/`from_silo_id` en stock/movimiento/histórico; **swap del índice único** (§4.6); FKs e índices — todo con `IF NOT EXISTS` |
| 2 | `SeedSilosSantaReyes` | data-only (Designer clonado, ModelSnapshot intacto) | `maneja_inventario_por_silo=true` para company 6; 100 filas en `silo_catalogo`; vincular los 38 `farm_silos` existentes a su `silo_catalogo_id` por nombre; `tipo 'Insumos'→'Bodega'` en la fila de bodega; **todo con `WHERE NOT EXISTS` / `IS DISTINCT FROM`** |
| 3 | `AddSilosAFnMoverUbicacion` | SQL crudo | `CREATE OR REPLACE` de `fn_mover_galpon` y `fn_rekey_nucleo` agregando `UPDATE galpon_silos SET nucleo_id = ...` (§4.3) + el `INSERT` del trigger `trg_lote_hist_desde_inventario_gestion` con `NEW.silo_id` |

⚠️ **Orden por timestamp:** la #2 hace `UPDATE companies ... WHERE name='Santa Reyes'`; el timestamp debe ser
posterior al seed que creó la empresa (`20260725190000`). Si EF genera un id menor, se renombra a mano
(ya pasó en Fase 2 con `AddClasificacionHuevoPorItems`).

⚠️ `backend/sql/fn_mover_ubicacion.sql` es un **espejo**, no lo desplegado ([[espejo-sql-desincronizado-y-gate]]):
se actualiza el `.sql` **y** se aplica por migración. Cambiar solo el `.sql` deja el fix muerto.

---

## 9. Reglas de negocio (el contrato exacto)

1. **Flag OFF ⇒ nada cambia.** `silo_id` siempre `NULL`; si un request lo trae, se rechaza.
2. **Flag ON ⇒ `silo_id` obligatorio** en ingreso, traslado (origen y destino), consumo y recepción de
   tránsito — para **todo tipo de ítem**, alimento e insumos.
3. **Flag ON ⇒ `nucleo_id` y `galpon_id` se persisten `NULL`** en `inventario_gestion_stock` y
   `inventario_gestion_movimiento`. El galpón viaja en el request solo para filtrar la lista de silos.
4. **El silo pertenece a la granja del movimiento.** Silo de otra granja ⇒ error, nunca descuento silencioso.
5. **Un silo puede estar en varios galpones.** El saldo es uno solo, del silo.
6. **La bodega es una ubicación con stock propio** (`tipo='Bodega'`), acepta alimento e insumos, y admite
   traslado interno bodega→silo y silo→silo dentro de la misma granja.
7. **El lote consume solo de sus silos.** Un `siloId` fuera de `lote_silos` se rechaza antes de persistir.
8. **`lote_silos` es editable en cualquier momento** (silo vacío ⇒ el usuario reasigna, o suma un segundo
   silo). No hay recálculo retroactivo: los consumos ya registrados conservan su silo.
9. **Empresa efectiva siempre por dato** (`farms.company_id` de la granja), nunca la empresa activa del
   token, para no fugar datos entre empresas.

---

## 10. Casos de prueba

### 10.1 xUnit — `InventarioUbicacionSiloCalculosTests`

| # | Caso | Esperado |
|---|---|---|
| 1 | Flag OFF, sin silo | ubicación intacta (núcleo/galpón tal cual), `SiloId` null |
| 2 | Flag OFF, **con** silo | error explícito (no se mezclan modelos) |
| 3 | Flag ON, sin silo | error «debe indicar el silo o bodega» |
| 4 | Flag ON, con silo + galpón | `SiloId` seteado, **núcleo y galpón a NULL** |
| 5 | Flag ON, ítem no-alimento | mismo trato que alimento (decisión del usuario: todo exige ubicación) |

### 10.2 xUnit — `ItemConsumoKey` / `MetadataEngordeCalculos`

| # | Caso | Esperado |
|---|---|---|
| 6 | Metadata sin `siloId` | claves con `SiloId=null` ⇒ **hash y agrupación idénticos a hoy** |
| 7 | Mismo ítem, 2 silos | **2 claves distintas**, cada una con su kg |
| 8 | Mismo ítem, mismo silo, hembras+machos | 1 clave, kg sumados |

### 10.3 Integración / smoke local (BD `sanmarinoapplocal:5433`)

| # | Caso | Verificación |
|---|---|---|
| 9 | **Regresión flag OFF**: ingreso + traslado + consumo en Sanmarino y Ecuador | mismos saldos y mismas filas que antes del cambio; `silo_id` NULL en el 100 % |
| 10 | Conteo de claves naturales antes/después del swap de índice | idéntico (ninguna fila se parte ni se fusiona) |
| 11 | SR: ingreso 1.000 kg al Silo 4 | `stock(farm 109, item, nucleo NULL, galpon NULL, silo 4) = 1000` |
| 12 | SR: ingreso al mismo silo/ítem otra vez | **1 sola fila** (upsert), `quantity=2000` |
| 13 | SR: traslado Bodega → Silo 4 | bodega baja, silo sube, 1 `TransferGroupId`, 2 movimientos |
| 14 | SR: traslado Silo 4 → Silo 20 | ídem entre silos |
| 15 | SR: ingreso sin silo | **rechazado** con mensaje claro (no crea stock a nivel granja) |
| 16 | SR: silo de otra granja | rechazado |
| 17 | Silo 4 asignado a galpón 1 **y** 2 | el saldo es **uno**; ambos galpones lo ven al filtrar |
| 18 | Seguimiento levante SR, alimento desde Silo 4 | descuenta del silo 4; el histórico muestra el silo |
| 19 | Seguimiento con silo **no** asignado al lote | rechazado antes de persistir, **sin fila de seguimiento** |
| 20 | Seguimiento producción, 2 alimentos de 2 silos | 2 consumos, un descuento por silo |
| 21 | Stock insuficiente en el silo | rollback total (ni seguimiento ni descuento) |
| 22 | Editar seguimiento cambiando de silo | `AplicarDiffAsync` devuelve al silo viejo y descuenta del nuevo |
| 23 | Mover galpón de núcleo (`fn_mover_galpon`) | `galpon_silos` sigue al galpón, 0 huérfanos |
| 24 | Reasignar `lote_silos` con consumos ya hechos | los movimientos viejos conservan su silo (sin recálculo) |

### 10.4 Smoke doble por UI (obligatorio, CLAUDE.md §🏢)

- **Empresa flag OFF** (Sanmarino o Demo): las pantallas 5/6/7 se ven **exactamente igual que hoy**;
  ningún selector de silo aparece.
- **Santa Reyes**: el ciclo completo — crear silos 1..100 → asignar 38 + bodega a La Esperanza → asignar
  silos al galpón 1 → crear lote con silos → ingreso → traslado → seguimiento levante → seguimiento
  producción → histórico con columna Silo → export a Excel.

---

## 11. Riesgos y cómo se contienen

| Riesgo | Severidad | Contención |
|---|---|---|
| `ON CONFLICT` desalineado del índice nuevo ⇒ **todo ingreso de todas las empresas falla** | 🔴 crítica | Índice y sentencia en el mismo commit; smoke de regresión flag OFF **antes** de tocar SR (caso 9) |
| `fn_mover_galpon`/`fn_rekey_nucleo` sin `galpon_silos` ⇒ silos huérfanos al mover un galpón | 🟠 alta | Migración #3 + caso 23. Es el mismo error que ya costó una migración de fix en Fase 1 |
| ~~`fn_inventario_gastos_existencias` hace `LEFT JOIN` asumiendo **una** fila de stock por (granja, ítem); con N silos multiplica filas~~ | 🟠 alta | ✅ **Cerrado en la Fase D** (2026-08-13, migración `FnGastosExistenciasSaldoPorSilo`): CTE `saldos` con `SUM` + `GROUP BY`. Reproducido (3 filas parciales → 1 de 380) y regresión OFF con las 1.179 filas de Ecuador idénticas. **Ojo**: la fn sola no habilita Gastos en SR — faltan `siloId` en el alta y `GROUP BY` en `GetItemsWithStockAsync` (ver tracker) |
| Front que arma el payload sin `siloId` ⇒ el backend rechaza y el usuario ve error sin saber por qué | 🟡 media | Selector obligatorio con validación en el form + mensaje explícito del backend nombrando el silo faltante |
| `silo_id` no llega al espejo `lote_registro_historico_unificado` | 🟡 media | Migración #3 agrega la columna al `INSERT` del trigger; caso 18 lo verifica |
| Carga masiva (hoja Alimento) sin columna Silo | 🟡 media | La Carga Masiva **no tiene rutas hijas habilitadas en SR** ⇒ fuera de alcance. Fase D |
| Divergencia `backend/sql/*.sql` vs lo desplegado | 🟡 media | Espejo y migración se actualizan juntos ([[espejo-sql-desincronizado-y-gate]]) |

### Invariantes de CLAUDE.md que este cambio **no** puede romper

- **Gate multipaís de cálculo compartido**: este plan **no toca** `fn_seguimiento_diario_engorde`,
  `fn_cuadre_alimento_engorde` ni ningún `*SaldoAlimento*` — son de engorde y Santa Reyes es postura sin
  engorde. Si alguna sub-tarea terminara tocándolos, **se dispara el gate**
  (`verificar_paridad_saldo_engorde.sql` antes y después, 0 en toda empresa que no sea el objetivo).
- **El histórico se anula, nunca se abandona**: los triggers `_del`/`_cancel` quedan intactos.
- **Una sola fórmula por número**: el saldo por silo se calcula en **un** lugar
  (`inventario_gestion_stock` vía las primitivas atómicas). No se agrega una segunda vía.
- **El cuadre se mira, no se espera**: `GET /api/CuadreAlimentoEngorde` debe seguir en **0 descuadrados**
  después de la Fase B (es de engorde, pero comparte las tablas de stock).

---

## 12. Fases (cada una desplegable y verificable por separado)

| Fase | Alcance | Riesgo para otras empresas |
|---|---|---|
| **A — Catálogo y asignación** | Flag; `silo_catalogo`; extensión de `farm_silos`; `galpon_silos`; `lote_silos`; 4 servicios + endpoints; pantallas 1-4 del front. **No toca inventario.** | **Ninguno**: tablas y pantallas nuevas, gateadas por flag |
| **B — Inventario por silo** | `silo_id` en stock/movimiento/histórico; swap del índice único; `InventarioGestionService.Silos.cs`; pantalla 5. | **Acá está el riesgo** (índice + `ON CONFLICT`). Se abre con el smoke de regresión flag OFF |
| **C — Consumo por silo** | `ItemConsumoKey`/metadata; `ColombiaInventarioConsumoService`; seguimiento levante y producción; pantallas 6-7. | Bajo: la rama Colombia modelo B con `SiloId=null` es la de hoy |
| **D — Cierre** | `fn_inventario_gastos_existencias` con `GROUP BY`; columna Silo en carga masiva; reportes (Contable, Técnico) con silo. | Bajo, todo aditivo |

Validación de **cada** fase: `cd backend && dotnet build` (0 errores, sin advertencias nuevas) +
`dotnet test` · `cd frontend && yarn build` (único warning aceptado: bundle budget) · smoke doble
(empresa OFF + Santa Reyes) · `make down` al terminar (sin procesos huérfanos).

---

## 13. Lo que este plan NO hace (explícito, para que no se asuma)

- **No migra datos históricos**: Santa Reyes tiene 0 movimientos y 0 stock. Si entre la aprobación y la
  ejecución la empresa empieza a cargar inventario, **hay que replanificar** con backfill.
- **No cambia el modelo de Ecuador/Panamá** (núcleo+galpón) ni el de engorde.
- **No toca la clasificación de huevos ni las cohortes** (Fases 2 y 3, ya cerradas).
- **No habilita Gastos de inventario ni Carga Masiva en Santa Reyes** (no están en sus menús).
- **No hace push ni deploy**: eso se pide explícitamente aparte.
