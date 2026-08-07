# Reporte Diario Área de Costos — POSTURA (levante + producción)

**Fecha:** 2026-08-07 · **Empresa objetivo:** Agroavícola San Marino (company_id 1, Colombia)
**Lote de validación:** lote base **S-369** (`lote_postura_base_id` 30) → sublotes **S-369A** (lote_id 144) y
**S-369B** (lote_id 145), granja *Pruebas Moises* (44), núcleo 883195, galpón G0443. Cargados por carga
masiva desde informes reales de la granja MANGOS (ver [`carga_masiva_s369ab_postura_plan.md`](carga_masiva_s369ab_postura_plan.md)).

> ⚠️ **Es POSTURA, no engorde.** El módulo `reporte-diario-costos-engorde` se usa **solo como molde de
> arquitectura** (fn SQL + service delgado + página con export). Las columnas, las fuentes de datos y
> las reglas de negocio son distintas y no se comparte una sola línea de aritmética con engorde.

---

## 1. Qué pide el área de costos

Reporte **diario**, sobre **lote base**, con tres pestañas:

| Pestaña | Columnas | Fase |
|---|---|---|
| **1. Aves** | fecha · lote:galpón · Mortalidad (H/M) · Selección (H/M) · Error de Sexaje (H/M) · Ventas (H/M) | Levante y Producción |
| **2. Alimento** | fecha · lote:galpón · Hembras (tipo alimento, cantidad) · Machos (tipo alimento, cantidad) | Levante y Producción |
| **3. Huevos** | fecha · lote:galpón · huevo fértil · huevo comercial · huevo inservible · ventas de huevo · traslado a planta · Huevo Total | **Solo Producción** |

**Filtros:** regional · granja · lote base · fase · fecha inicio · fecha fin.

---

## 2. Decisiones confirmadas por el usuario (07-ago-2026)

| # | Decisión | Detalle |
|---|---|---|
| **D1** | **Clasificación de huevo** | `fértil = huevo_inc` · `comercial = sucio+deforme+blanco+doble_yema+piso+pequeño` · `inservible = roto+desecho+otro`. Los tres **suman exacto** `huevo_tot`. |
| **D2** | **Alcance** | Lote base **opcional**. Sin lote base ⇒ todos los lotes de postura del alcance. Filas siempre abiertas por **lote : galpón**. |
| **D3** | **Fase** | Selector de 3 valores: **Levante · Producción · Ambas**. Con «Ambas» las filas traen columna *Fase* y se ve el ciclo completo. La pestaña Huevos solo trae filas de producción. |
| **D4** | **Alimento** | **Una fila por ítem** dentro del día (un día con 2 alimentos de hembras ocupa 2 filas), con nombre y kg reales del ítem. |

### 2.1 Por qué D1 es una decisión y no una lectura del código

El sistema **no tiene** los conceptos «comercial» ni «inservible» (grep en todo el repo: 0 usos). Lo más
parecido es el Reporte Contable, que hoy mapea:

```
HVTO FÉRTIL   = huevo_inc
HVO COMERCIAL = huevo_limpio + huevo_tratado
```

…y esos **son el mismo número**. Verificado en los datos reales de S-369B:

| | valor |
|---|---|
| `Σ huevo_inc` | 1.021.041 |
| `Σ (limpio + tratado)` | 992.662 + 28.379 = **1.021.041** |

Invariante confirmado fila a fila (15-may-26: 7.799 = 7.799; 15-jun-26: 7.157 = 7.157):

```
huevo_tot = limpio + tratado + sucio + deforme + blanco + doble_yema + piso + pequeño + roto + desecho + otro
huevo_inc = limpio + tratado                       (derivado, NO es una categoría aparte)
```

⇒ La clasificación D1 es una **partición exacta** de `huevo_tot`. No se toca el Reporte Contable en esta
entrega (queda anotado como deuda: sus dos columnas muestran el mismo dato).

---

## 3. Fuentes de datos — auditoría previa

| Dato | Levante | Producción |
|---|---|---|
| Tabla canónica | `seguimiento_diario_levante` (`lote_id_int`) | `seguimiento_diario_produccion` (`lote_id`) |
| **fn diaria canónica** | ❌ **NO EXISTE** — `fn_indicadores_levante_postura` es **semanal** (grano `semana`) | ✅ `fn_seguimiento_diario_produccion(lpp_id, lote_id)` (77 columnas, grano día) |
| Mortalidad | `mortalidad_hembras` / `mortalidad_machos` | ídem |
| Selección | `sel_h` / `sel_m` | ídem |
| Error sexaje | `error_sexaje_hembras` / `error_sexaje_machos` | ídem |
| Ventas de aves | `venta_aves_hembras` / `venta_aves_machos` | ídem |
| Consumo total día | `consumo_kg_hembras` / `consumo_kg_machos` | `cons_kg_h` / `cons_kg_m` |
| Ítems de alimento | `metadata->'itemsHembras'` / `->'itemsMachos'` | ídem (la fn expone `metadata`) |
| Huevos | — (no aplica) | 11 columnas crudas |
| Ventas / traslado de huevo | — | `traslado_huevos` (`tipo_operacion`, `tipo_destino`, `estado`) |

**Consecuencia arquitectónica:**
- **Producción** ⇒ se reusa `fn_seguimiento_diario_produccion` vía `CROSS JOIN LATERAL` (mismo patrón con
  que engorde reusa su fn diaria). Los números cuadran 1:1 con la pantalla de seguimiento.
- **Levante** ⇒ **no hay** fn diaria canónica que reusar. Se lee `seguimiento_diario_levante` directo,
  **dentro de la fn nueva**, para que exista **un solo lugar** con ese criterio. No se reimplementa
  ninguna fórmula: todas las columnas del reporte son **valores registrados**, no derivados (el reporte
  no calcula saldo de aves ni % de postura, que son los números con dueño único).

**Ítems de alimento (`metadata`)** — forma real verificada:

```json
{"itemsHembras": [{"nombre": "PREPOSTURA REPRODUCTORA PESADA", "unidad": "kg",
                   "cantidad": 1113.2, "tipoItem": "alimento", "itemInventarioEcuadorId": 180}],
 "itemsMachos":  [{"nombre": "POLLA LEVANTE REPRODUCTORA PESADA", "unidad": "kg", "cantidad": 125.5, ...}]}
```

La columna `tipo_alimento` es la concatenación `"H: x + y / M: z"` ⇒ **no** se usa para el desglose (D4);
solo como **fallback** cuando `metadata` no trae ítems (filas viejas): 1 fila por sexo con el nombre
concatenado y el total de `consumo_kg_*`.

---

## 4. Enfoque arquitectónico

Backend `.NET 10` Clean Architecture + fn SQL; frontend Angular 22 standalone.
**La BD filtra y agrega; el backend orquesta** (regla multipaís de `CLAUDE.md`).

### 4.1 BD — `fn_reporte_diario_costos_postura`

```sql
fn_reporte_diario_costos_postura(
    p_company_id            INT,
    p_granja_ids            INT[] DEFAULT NULL,   -- NULL = todas las granjas del alcance del usuario
    p_regional              TEXT  DEFAULT NULL,   -- NULL = todas
    p_lote_postura_base_id  INT   DEFAULT NULL,   -- NULL = todos los lotes (D2)
    p_fase                  TEXT  DEFAULT NULL,   -- 'Levante' | 'Produccion' | NULL = ambas (D3)
    p_fecha_desde           DATE  DEFAULT NULL,
    p_fecha_hasta           DATE  DEFAULT NULL
) RETURNS TABLE (
    fecha DATE, fase TEXT,
    lote_id INT, lote_nombre TEXT,
    galpon_id TEXT, galpon_nombre TEXT, nucleo_id TEXT,
    granja_id INT, granja_nombre TEXT, regional TEXT,
    lote_postura_base_id INT, lote_base_nombre TEXT,
    edad_dias INT, semana INT,
    -- Pestaña 1
    mortalidad_h INT, mortalidad_m INT,
    seleccion_h  INT, seleccion_m  INT,
    error_sexaje_h INT, error_sexaje_m INT,
    venta_aves_h INT, venta_aves_m INT,
    -- Pestaña 2
    consumo_kg_h FLOAT8, consumo_kg_m FLOAT8,
    alimentos TEXT,        -- json [{sexo:'H'|'M', nombre, cantidad_kg, origen:'metadata'|'tipo_alimento'}]
    -- Pestaña 3 — las 11 categorías CRUDAS (0 en filas de levante)
    huevo_tot INT, huevo_inc INT, huevo_limpio INT, huevo_tratado INT,
    huevo_sucio INT, huevo_deforme INT, huevo_blanco INT, huevo_doble_yema INT,
    huevo_piso INT, huevo_pequeno INT, huevo_roto INT, huevo_desecho INT, huevo_otro INT,
    huevo_venta INT, huevo_traslado_planta INT
) LANGUAGE sql STABLE
```

> **Corrección de diseño (durante la implementación).** El plan original hacía la clasificación D1 en
> SQL. Se movió a C#: la fn devuelve el huevo **crudo** y el único dueño de la fórmula es
> `ReporteDiarioCostosPosturaCalculos.ClasificarHuevo`, que es lo que los tests ejercitan. Calcularla en
> los dos lados habría creado la segunda implementación del mismo número — precisamente lo que prohíbe
> «una sola fórmula por número».

Estructura interna (CTEs):

1. `lotes_scope` — `lotes` ⋈ `farms` ⋈ `master_list_options` (regional) ⋈ `lote_postura_base`, filtrando
   `company_id`, `deleted_at IS NULL`, `p_granja_ids`, `p_regional`, `p_lote_postura_base_id`.
   **Fail-closed:** `p_granja_ids` lo arma el service con las granjas asignadas al usuario; array vacío ⇒ 0 filas.
2. `dias_levante` — `seguimiento_diario_levante` ⋈ `lotes_scope` por `lote_id_int`, día calendario Bogotá,
   recortado por `p_fecha_desde/hasta`. Se omite si `p_fase = 'Produccion'`.
3. `dias_produccion` — `lotes_scope` `CROSS JOIN LATERAL fn_seguimiento_diario_produccion(NULL, lote_id)`,
   más `LEFT JOIN seguimiento_diario_produccion` por `seg_id` para las **dos columnas que la fn no
   expone**: `venta_aves_hembras/machos`. La fn sí trae `mov_venta_h/m`, pero salen de `movimiento_aves`
   y valen **0** en los lotes cargados masivamente (S-369B tiene 224 H / 67 M en las columnas de la
   tabla y 0 en `movimiento_aves`). Se omite si `p_fase = 'Levante'`.
4. `movimientos_huevo` — `traslado_huevos` agregada por (`lote_id`, día), `estado = 'Completado'`,
   `deleted_at IS NULL`: `huevo_venta` = `tipo_operacion='Venta'`; `huevo_traslado_planta` =
   `tipo_operacion='Traslado' AND tipo_destino='Planta'` (criterio idéntico al Reporte Contable).
5. `alimentos_json` — explode de `metadata->'itemsHembras'|'itemsMachos'` a `[{sexo,nombre,cantidad_kg}]`
   con fallback a `tipo_alimento` + `consumo_kg_*` cuando no hay ítems.

   ⚠️ **El metadata tiene DOS formas** según el camino de captura del consumo, y el nombre del alimento
   hay que resolverlo contra el catálogo correspondiente:

   | Camino | Forma del ítem | Lotes | Resolución del nombre |
   |---|---|---|---|
   | 2 (inventario) | `{"nombre": "...", "itemInventarioEcuadorId": 180}` | S-369 | viene en el jsonb |
   | 1 (catálogo) | `{"catalogItemId": 70}` — **sin `nombre`** | K345 (viejos) | `catalogo_items.nombre` |

   Sin esa resolución la columna «tipo alimento» salía **«Sin especificar»** en todos los lotes viejos
   y el reporte quedaba inútil para costear. `COALESCE(nombre, item_inventario_ecuador, catalogo_items)`.

   El fallback de `tipo_alimento` también tiene dos formatos vivos: `"H: x + y / M: z"` (captura nueva)
   y `"x / y"` **sin prefijo de sexo** (lotes viejos). Se contemplan los dos.
6. `UNION ALL` de levante + producción, ordenado por `fecha, lote_nombre, galpon_id`.

#### 4.1.1 Filas de traslado sin LPP
`fn_seguimiento_diario_produccion` marca con `fila_sin_lpp` las filas creadas por traslados desde la
pantalla de seguimiento. **Se INCLUYEN** en este reporte: sus mortalidad/selección/error son 0 y su
aporte real es el movimiento, que costos necesita ver. (Las fns *semanales* las excluyen porque
duplicarían indicadores acumulados; acá no hay acumulados.)

### 4.2 Application

| Archivo | Contenido |
|---|---|
| `DTOs/ReporteDiarioCostosPosturaDtos.cs` | `ReporteDiarioCostosPosturaRequest`, `...FilaDto`, `...AlimentoItemDto`, `...ReporteDto`, `...TotalesDto`, `...Row` (crudo de `SqlQueryRaw`) |
| `Interfaces/IReporteDiarioCostosPosturaService.cs` | `GenerarAsync(request, ct)` |
| **`Calculos/ReporteDiarioCostosPosturaCalculos.cs`** | **PURO** (sin EF): `ClasificarHuevo(...)` (D1), `TotalesAves`, `TotalesAlimento`, `TotalesHuevo`, `EtiquetaLoteGalpon`, `NormalizarFase` |

### 4.3 Infrastructure

`Services/ReporteDiarioCostosPostura/ReporteDiarioCostosPosturaService.cs` (ancla: ctor + interfaz +
helpers) y, si supera ~250 líneas, `Funciones/...Service.<Concern>.cs` (`partial class`, namespace plano
`ZooSanMarino.Infrastructure.Services`).

Responsabilidades del service (delgado):
1. Empresa efectiva por `ICompanyResolver` + `ICurrentUser` (patrón del service de engorde).
2. Granjas visibles vía `IFarmService.GetAllAsync(userGuid, companyId)` → `p_granja_ids` (**fail-closed**).
3. Alcance granular por granja (`ILocationScopeResolver`) para recortar galpones/núcleos no visibles.
4. Llamada a la fn con `SqlQueryRaw<...Row>`; parseo del json de alimentos con `JsonNamingPolicy.SnakeCaseLower`.
5. Totales delegados en `ReporteDiarioCostosPosturaCalculos`.

### 4.4 API

`Controllers/ReporteDiarioCostosPosturaController.cs` → `POST /api/ReporteDiarioCostosPostura/generar`
(`[Authorize]`). Sin la palabra `admin` en la ruta (AWS WAF).

### 4.5 Migraciones EF (idempotentes)

| Migración | Contenido |
|---|---|
| `AddFnReporteDiarioCostosPostura` | `CREATE OR REPLACE FUNCTION` + espejo en `backend/sql/fn_reporte_diario_costos_postura.sql` |
| `AddMenuReporteDiarioCostosPostura` | Ítem `/reporte-diario-costos-postura` bajo «Reportes», `role_menus` y `company_menus` heredados de `/reporte-contable` (postura, empresa Sanmarino). `INSERT ... WHERE NOT EXISTS` |

Designer clonado, **sin tocar el ModelSnapshot** (no hay cambios de modelo).

### 4.6 Frontend — `features/reporte-diario-costos-postura/`

```
models/reporte-diario-costos-postura.model.ts
funciones/
  README.md
  clasificar-filas-por-pestana.funcion.ts     # pura: parte las filas en Aves / Alimento / Huevos
  construir-aoa-costos-postura.funcion.ts     # pura: 3 hojas para el Excel
services/reporte-diario-costos-postura.service.ts
pages/reporte-diario-costos-postura-main/     # orquestador delgado
```

- `changeDetection: ChangeDetectionStrategy.Eager` **explícito** (Angular 22 ⇒ omitirlo es OnPush y cuelga el spinner).
- Filtros en cascada: regional → granja → lote base; fase (Levante/Producción/Ambas); fechas.
- 3 pestañas; **Huevos oculta** cuando la fase seleccionada es Levante.
- Export con `exportarMultiHojaExcel` de `shared/utils/excel/` (**prohibido** `XLSX` inline).
- Mensajes con `ToastService`; nada de `alert()`/`confirm()`.
- Vista precalculada con **referencias estables** (nada de getters que alocan por ciclo → NG0103).
- Ruta lazy en `app.config.ts`: `/reporte-diario-costos-postura`.

---

## 5. Reglas de negocio

1. **RN-1 · Clasificación de huevo (D1)** — partición exacta de `huevo_tot`; si la suma de los tres grupos
   ≠ `huevo_total`, es un defecto de datos y el reporte lo muestra tal cual (no se «cuadra» a la fuerza).
2. **RN-2 · Fase** — la fase de una fila la determina **la tabla de origen**, no `lotes.fase` (el cierre de
   levante nunca actualiza esa columna: hueco ya documentado en la carga masiva de producción).
3. **RN-3 · Un día, una fila por lote** — el día calendario se resuelve en **America/Bogota**
   (`date_trunc` sobre `timestamptz` usa la zona de la SESIÓN ⇒ se fija explícitamente).
4. **RN-4 · Alimento (D4)** — una fila por ítem; el `Σ cantidad_kg` por sexo del día debe igualar
   `consumo_kg_h/m`. Diferencia ⇒ el ítem faltante se emite como fallback, nunca se descarta.
5. **RN-5 · Movimientos de huevo** — solo `estado='Completado'`; los `Cancelado` se ignoran (criterio del
   Reporte Contable).
6. **RN-6 · Fail-closed multi-empresa** — empresa efectiva por datos; granjas por alcance del usuario;
   sin granjas visibles ⇒ reporte vacío, jamás datos de otra empresa.
7. **RN-7 · Sin fechas** — `p_fecha_desde` NULL ⇒ primer día con registro del alcance; `p_fecha_hasta`
   NULL ⇒ hoy. (No aplica la «regla del segundo lote» de engorde: en postura el ciclo es largo y costos
   pide el histórico completo.)

---

## 6. Casos de prueba

### 6.1 xUnit — `ReporteDiarioCostosPosturaCalculosTests` (gate CI)

| # | Caso | Esperado |
|---|---|---|
| T1 | `ClasificarHuevo` con la fila real del 15-may-26 (S-369B) | fértil 7.506 · comercial 181 · inservible 112 · **suma = 7.799 = huevo_tot** |
| T2 | `ClasificarHuevo` con la fila real del 15-jun-26 | fértil 6.884 · comercial 173 · inservible 100 · suma = 7.157 |
| T3 | `ClasificarHuevo` con todo en 0 | los tres en 0, sin división por cero |
| T4 | `fértil` == `limpio + tratado` para las filas de prueba | verifica el invariante que sostiene D1 |
| T5 | Partición exacta sobre el acumulado de S-369B | fértil+comercial+inservible = 1.115.079 = `Σ huevo_tot` |
| T6 | `TotalesAves` suma H/M de las 4 categorías | totales por sexo y gran total |
| T7 | `TotalesAlimento` agrupa por (sexo, nombre) | un día con 2 alimentos H produce 2 grupos (D4) |
| T8 | `NormalizarFase` sinónimos (`levante`, `Levante`, `produccion`, `Producción`, null) | mapea a `'Levante'`/`'Produccion'`/null |
| T9 | `EtiquetaLoteGalpon` sin galpón | `"S-369B : (sin galpón)"`, nunca excepción |
| T10 | Fila de levante en el clasificador de huevo | los 4 campos en 0 (no aplica) |

### 6.2 Validación SQL contra S-369 (datos reales)

| # | Verificación | Testigo |
|---|---|---|
| V1 | Filas de levante | 144 → 168 días · 145 → 168 días |
| V2 | Filas de producción | 144 → 168 días · 145 → 161 días |
| V3 | `Σ mortalidad_h` levante 145 | **307** · machos **125** |
| V4 | `Σ selección` levante 145 | H **71** · M **308** |
| V5 | `Σ error sexaje` levante 145 | H **379** · M **3** |
| V6 | `Σ ventas` levante 145 | H 0 · M **290** · producción 145 → H **224** / M **67** |
| V7 | `Σ consumo` levante 145 | H **104.073,6 kg** · M **16.772,4 kg** |
| V8 | `Σ consumo` producción 145 | H **237.626,8 kg** · M **18.703 kg** |
| V9 | `Σ huevo_total` producción 145 | **1.115.079** · fértil **1.021.041** |
| V10 | Partición D1 en la fn | `Σ(fértil+comercial+inservible) == Σ huevo_total` por lote |
| V11 | Alimento: `Σ cantidad_kg` de los ítems == `consumo_kg_*` | por día y por sexo, ambas fases |
| V12 | Día con 2 alimentos H (23-feb-26, lote 145) | 2 filas de hembras: *PREPICO…* y *PREPOSTURA…* |
| V13 | Movimientos de huevo | **0 filas** para 144/145 (⚠️ ver §7) |
| V14 | Multi-empresa | con sesión de otra empresa el reporte del lote base 30 devuelve **0 filas** |
| V15 | Fase = Ambas | filas de levante y producción concatenadas, sin solapamiento de días por lote |

### 6.3 Smoke HTTP + UI
- Backend propio en `:5499` con JWT + `X-Secret-Up` minteados; **no** tocar el `:5002` de otras sesiones.
- UI: sesión inyectada en `localStorage.auth_session`; abrir y cerrar la página **dos veces** (checklist de
  change detection); verificar que las 3 pestañas pintan y que el Excel baja con 3 hojas.

---

## 7. Limitaciones conocidas de la validación

1. **`traslado_huevos` no tiene ninguna fila para S-369A/B** (solo lotes 13 y 14 tienen movimientos). Las
   columnas *ventas de huevo* y *traslado a planta* saldrán en **0** para el lote de prueba: la columna se
   valida con lote 13 (217 traslados a planta Completados, 1.484.804 huevos) y contra el Reporte Contable.
2. **`farms.regional_id = 27` de la granja *Pruebas Moises* no resuelve** a ninguna opción de
   `master_list_options` ⇒ su regional sale **vacía**. El filtro por regional no encuentra esa granja; hay
   que probar el filtro con una granja real (p. ej. MANGOS → *Centro*, LA ESMERALDA → *Oriente*).
3. **Deuda anotada, fuera de alcance:** el Reporte Contable muestra «HVTO FÉRTIL» y «HVO COMERCIAL» con el
   mismo número (§2.1). No se toca en esta entrega.
4. **Levante no tiene fn diaria canónica.** Si en el futuro se crea `fn_seguimiento_diario_levante`
   (hermana de la de producción), este reporte debe re-sourcearse sobre ella y verificarse byte a byte.

---

## 8. Validación obligatoria antes de mergear

- `cd backend && dotnet build` → 0 errores, 0 advertencias nuevas.
- `cd backend && dotnet test` → suite completa verde (incluye los tests nuevos).
- `cd frontend && yarn build` → OK (único warning aceptado: *bundle budget* preexistente).
- SQL: los 15 testigos de §6.2 en verde.
- Smoke HTTP + UI de §6.3.
- **Sin procesos huérfanos**: backend/front de smoke detenidos al terminar.
