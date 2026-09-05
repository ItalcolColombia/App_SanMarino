# Plan — Múltiples seguimientos diarios de producción (Santa Reyes)

**Pedido (04-sep-2026):** Santa Reyes necesita poder cargar **más de un** registro de seguimiento
diario de producción/postura el mismo día para un mismo lote. Hoy el sistema permite exactamente
uno. El cambio debe ir **controlado por flag de empresa** (patrón §🏢 de `CLAUDE.md`) y hay que
validar cómo afecta a los reportes/indicadores que hoy asumen "1 fila = 1 día".

**Estado: decisiones tomadas (ver §0), investigación complementaria en curso (borrado seguro del
escritor huérfano + mapeo de LEVANTE). No se tocó código todavía.**

**Alcance ampliado (04-sep-2026, respuesta del usuario):** el mismo flag/lógica debe aplicarse
también a **seguimiento diario LEVANTE** (`seguimiento_diario_levante`, tipo `levante`), no solo a
producción/postura. Además, el escritor huérfano `SeguimientoProduccionService.cs` se **elimina**
(confirmado sin callers reales) en vez de solo dejarlo bloqueado.

---

## 0 · Decisiones (ya tomadas por el usuario, 04-sep-2026)

1. **Semántica: SUMA.** Varios registros el mismo lote+día se acumulan (mortalidad, huevos,
   consumo, ventas, traslados) para dar el total del día, como si fuera un solo registro.
2. **Campos no aditivos: confirmado.** Peso promedio → promedio ponderado por aves vivas de cada
   registro. Uniformidad, CV%, observaciones/metadata → gana el último registro del día (mismo
   criterio que ya usa hoy la función canónica). Ver tabla §3.
3. **Índice único de BD: opción (A).** Índice parcial con el `company_id` de Santa Reyes
   hardcodeado como literal en el predicado (`WHERE company_id <> <id>`) — mismo patrón que ya
   existe en el repo (`ux_sdlr_prod_lote_dia_utc ... WHERE tipo_seguimiento = 'produccion'`).
4. **Escritor huérfano (`SeguimientoProduccionService.cs`): SE ELIMINA por completo**, no se
   gatea. Confirmado sin ningún caller real (ni front, ni app móvil `zootecnicoapp/`, ni otro
   service en C#) — la única acción de su controller que el front usa (`GET .../filter-data`) NO
   depende de `ISeguimientoProduccionService`, así que sobrevive intacta al borrado. Verificación
   final de "nada queda colgando" en curso (§0.6).
5. **Rollout: todo en un solo commit**, incluyendo el fix del Reporte Técnico Producción para que
   no duplique renglones.
6. **Alcance ampliado: aplica también a seguimiento diario LEVANTE**, no solo a
   producción/postura — mismo flag, misma lógica de agregación. Mapeo cerrado, ver §5 —
   **LEVANTE es un proyecto de tamaño comparable o mayor al de producción**, no una extensión
   trivial: no existe hoy ninguna función canónica que centralice su grilla, así que hay que tocar
   6 consumidores independientes en vez de una sola función.

## 0.6 · Verificación de borrado seguro — CERRADA, sin bloqueantes

Confirmado por investigación de código (grep del repo completo: backend, frontend, `zootecnicoapp/`
móvil): ningún caller real fuera del propio trío a borrar. Detalle:
- `SeguimientoProduccionService.cs` + `ISeguimientoProduccionService` + su registro DI
  (`Program.cs:279`) + `SeguimientoProduccionScopeCalculos.cs` (+ su test) — borrado limpio.
- Los 4 DTOs (`CreateSeguimientoProduccionDto`, `UpdateSeguimientoProduccionDto`,
  `SeguimientoProduccionDto`, `FilterSeguimientoProduccionDto`) viven en
  `Application/DTOs/ProduccionLoteDto.cs:45-105` **junto con** otros DTOs que SÍ están vivos
  (`ProduccionLoteDto`, `Create/Update/FilterProduccionLoteDto`, usados por `ProduccionLoteController`
  — módulo distinto). ⚠️ Borrar solo los 4 records "Seguimiento*", no el archivo completo.
- `SeguimientoProduccionController.cs`: borrar las acciones `Create`/`GetAll`/`GetByLoteId`/`Update`/
  `Delete`/`Filter` (usan `_svc`). **Dejar intacta** `GetFilterData` (ruta `GET /filter-data`, la usa
  el front) — no depende de `_svc`, depende de `IServiceScopeFactory` + `ILoteProduccionFilterDataService`
  + `ILotePosturaProduccionService`. Al borrar el campo `_svc`, el constructor del controller queda
  solo con `IServiceScopeFactory`.
- `'SeguimientoProduccion'` como string literal en el front (`migraciones-masivas/models/migracion.model.ts:11`,
  `selector-tipo-migracion.component.ts:101`) es un **tipo de migración masiva**, no relacionado —
  ese módulo escribe directo al `DbSet` vía `MigracionService.AlimentoPostura.cs`/`.MovimientosAves.cs`,
  nunca pasa por `ISeguimientoProduccionService`. No se toca.

## 0.7 · Hallazgo fuera de alcance — NO se arregla acá

`ProduccionService.Seguimiento.cs` (el servicio VIVO) tiene un hueco real de scoping multi-empresa
en el camino legacy `ProduccionLoteId` (Create ~L59-64, Update ~L431-436): no filtra por
`CompanyId` al resolver el lote, a diferencia del camino nuevo `LotePosturaProduccionId` que sí lo
hace. En Update es explotable: un usuario puede mover su propio registro para que cuelgue de un
lote de OTRA empresa. **No relacionado con este feature** — spawneado como tarea aparte
(`task_0c2fcd76`, "Fix cross-tenant lote assignment in ProduccionService.Seguimiento").

---

## 1 · Qué hay hoy (confirmado, con file:line)

### 1.1 Los dos escritores y su validación

| Escritor | Endpoint | Usado por el front |
|---|---|---|
| `ProduccionService.Seguimiento.cs` (Create ~L51-69, Update ~L508-516) | `POST/PUT /api/Produccion/seguimiento{/{id}}` | **Sí** — único camino real |
| `SeguimientoProduccionService.cs` (Create ~L176-198, Update ~L296-301) | `POST/PUT /api/SeguimientoProduccion{/{id}}` | No (solo `GET /filter-data` sigue en uso) |

Ambos hacen `FechasPuras.RangoDiaUtc(fecha)` + `AnyAsync`/`FirstOrDefaultAsync` y lanzan
`InvalidOperationException("Ya existe un seguimiento para esta fecha y lote.")` (vivo) o
`"...manual para ese lote en esa fecha."` (huérfano) si ya hay fila ese día.

### 1.2 El índice único en BD

`Migrations/20260801070000_IndiceUnicoSeguimientoProduccionDia.cs`:
- `ix_seguimiento_diario_produccion_lote_id_fecha_registro` — `(lote_id, fecha_registro)` exacto
  (alinea BD con lo que el modelo EF ya declaraba).
- `ux_seguimiento_diario_produccion_lote_dia_utc` — **el que importa**: expresión
  `(lote_id, (fecha_registro AT TIME ZONE 'UTC')::date)`, único por día calendario UTC.

Ambos `IF NOT EXISTS` + defensivos (no tumban el arranque si ya hay duplicados).

### 1.3 La función canónica — hoy "gana el más temprano", no suma ni duplica

`backend/sql/fn_seguimiento_diario_produccion.sql`, CTE `seg_dias`:
```sql
SELECT DISTINCT ON ((c.c_ts AT TIME ZONE 'America/Bogota')::date)
       c.*, (c.c_ts AT TIME ZONE 'America/Bogota')::date AS reg_date
  FROM crudos c
 ORDER BY (c.c_ts AT TIME ZONE 'America/Bogota')::date, c.c_ts
```
Con 2+ filas del mismo lote+día: **no duplica, no suma — descarta todas menos la de timestamp más
temprano**. Las 3 fns semanales derivadas (`fn_indicadores_produccion_postura`,
`fn_clasificacion_huevo_items_produccion`, `fn_resumen_semanal_ra_pesadas_produccion`) heredan este
comportamiento tal cual (no reimplementan el dedup). El espejo C#
`SeguimientoDiarioProduccionCalculos.DedupPorDia` replica exactamente la misma regla y tiene un test
que la fija (`DedupPorDia_GanaElTimestampMasTemprano`).

**Esto es el corazón del problema:** si solo se quita la validación de alta, Santa Reyes podría
cargar 2 registros el mismo día pero **la grilla, los indicadores semanales, RA Pesadas y la
clasificación de huevo seguirían mostrando solo el primero** — el segundo registro se perdería en
silencio en todos los reportes que sí pasan por la función canónica.

### 1.4 Consumidores que NO pasan por la función canónica (y se comportarían distinto entre sí)

| Consumidor | Archivo | Qué pasaría con 2+ filas/lote+día, sin cambios |
|---|---|---|
| Dashboard postura | `DashboardService.Postura.cs:44-51` | `GROUP BY` solo por día (no por lote+día) → **suma** ambas filas en el total de la empresa — coincide con la semántica de "agrupar" que pide el usuario, pero de casualidad, no por diseño |
| Header del lote | `ProduccionService.Consultas.cs:124-141` (`ObtenerInformacionLoteAsync`) | `GroupBy(_ => 1)` sobre TODAS las filas del LPP → suma total de vida, pero `Registros` (conteo de filas) dejaría de representar "días" |
| Reporte Técnico Producción (Diario/Consolidado/Tabs) | `ReporteTecnicoProduccionService.Diario.cs` / `.Tabs.cs` | Sin agrupar: **una fila de grilla por cada registro** (2 filas el mismo día) — el saldo de aves se decrementa en cascada fila por fila. Aritméticamente el acumulado final da igual que sumarlas antes, pero **visualmente duplica el día** en la grilla, que es justo lo que el usuario quiere evitar |
| Clasificación huevo comercio semanal | `ReporteTecnicoProduccionService.ClasificacionHuevo.cs:44-56` | `.Sum()` sobre la lista semanal cruda → correcto si los registros son eventos reales distintos |
| Grilla `GET /api/Produccion/seguimiento`, saldo de aves del header, Reporte Técnico Semanal | vía función canónica | **Heredan el problema de §1.3** — hay que arreglar la fn para que estos queden bien automáticamente |

### 1.5 Front

Sin chequeo de duplicado cliente-side (confía 100% en el error del backend). La grilla
(`tabla-lista-registro.component.ts`) es un array plano sin agrupar por fecha — si el backend
permite 2 filas, las muestra tal cual, ambas. Un solo cálculo asume implícitamente 1 registro = 1
día: `consumoRealGrAveDiaH/M` en `tabla-lista-indicadores.component.ts:426-439` usa
`ind.totalRegistros` (conteo de FILAS) como si fuera "cantidad de días" — con 2 filas el mismo día
diluiría el consumo g/ave/día silenciosamente en el Excel exportado. Patrón de flag ya usado en
`active-company-config.service.ts` (`CompanyFlags`, 17 flags existentes, todos fail-closed) — el
nuevo flag se agrega igual.

### 1.6 Precedente de flag por empresa a copiar

Migración columna: `20260820220012_AddFlagLimitaTiposInventarioAlimentoYAves.cs` (ADD COLUMN IF NOT
EXISTS + UPDATE WHERE name='Santa Reyes' en la misma migración). 4 proyecciones de `CompanyDto` a
tocar: `CompanyService.ToDto`, `CompanyService.Crud` (Create+Update), `CompanyResolver` (2 lugares),
`CompanyPaisService` (solo si lo necesita el móvil — no aplica acá). Empresa efectiva por sesión ya
resuelta en `SeguimientoProduccionScopeCalculos.EmpresaEfectiva` (Patrón B) — replicar en el
service vivo (`ProduccionService`), que hoy no tiene ese helper.

---

## 2 · Diseño propuesto (pendiente de §0)

- Flag nuevo en `companies`: `permite_multiples_seguimientos_diarios_produccion` (bool, default
  `false`), siguiendo el patrón de §1.6. Nombrado por comportamiento, no por tenant.
- **Alta (C#):** en los dos escritores (o solo el vivo, según decisión §0.4), si el flag está ON no
  se lanza la excepción de duplicado — se permite el INSERT. La rama de MERGE-si-solo-traslado
  existente no se toca (sigue aplicando antes de esta validación).
- **BD:** según decisión §0.3.
- **Función canónica v3** (`fn_seguimiento_diario_produccion`): el CTE `seg_dias` se bifurca por el
  flag de la empresa del lote (join a `companies` vía `lotes.company_id`, ya presente en la fn) —
  flag OFF: el `DISTINCT ON` actual, **byte a byte idéntico** (gate obligatorio); flag ON: `GROUP BY`
  día con la regla de suma/promedio de §0.1-§0.2 por campo.
- **Espejo C#:** `SeguimientoDiarioProduccionCalculos` gana una función `AgruparPorDia` paralela a
  `DedupPorDia`, con su propio test — mismo criterio "una fórmula, dos implementaciones, una es el
  test de la otra".
- **Consumidores fuera de la fn** (§1.4): Header y Reporte Técnico Producción pasan a agrupar por
  día ANTES de iterar/sumar (reusando la misma función de agregación), para que sea consistente con
  la grilla y no dupliquen renglones.
- **Front:** agregar el flag a `CompanyFlags`/`active-company-config.service.ts`; arreglar
  `consumoRealGrAveDiaH/M` para dividir por días únicos, no por `totalRegistros`. La grilla puede
  seguir mostrando las filas crudas tal cual (es información real, el usuario ve lo que cargó) — el
  "agrupado" es para reportes/indicadores, no necesariamente para el listado de auditoría.

## 4b · Estado de implementación — PRODUCCIÓN cerrada y verificada (05-sep-2026)

Implementado en worktree aislado `App_SanMarino_seg_multiples` (para no pisar el trabajo sin
commitear de otra sesión sobre `Migracion*`). Todo en verde: build 0/0, `dotnet test` 3887 verdes.

- Flag `Company.PermiteMultiplesSeguimientosDiarios` + 4 proyecciones de `CompanyDto` +
  migración `20260905015025_AddFlagPermiteMultiplesSeguimientosDiarios` (seed Santa Reyes=true).
- `SeguimientoProduccionService.cs`/`ISeguimientoProduccionService`/`SeguimientoProduccionScopeCalculos`
  + su controller CRUD y sus DTOs `Seguimiento*` — **eliminados** (S2, sin bloqueantes).
- Índices únicos parciales recreados dinámicamente por flag (migración
  `20260905015934_IndicesUnicosDiaExcluyenFlagMultiplesRegistros`, `DO $mig$` sin hardcodear ids) —
  verificado en Postgres real: `ux_seguimiento_diario_produccion_lote_dia_utc` quedó
  `WHERE (company_id IS NULL) OR (company_id <> 6)`.
- `fn_seguimiento_diario_produccion` v3 (migración `20260905021548_...`): `seg_dias` bifurca en
  `seg_dias_dedup` (intacto) / `seg_dias_agrupado` (nuevo), elegidos por el flag de la empresa del
  lote. Espejo C# `SeguimientoDiarioProduccionCalculos.AgruparPorDia` + 5 tests nuevos.
- **Smoke real contra Postgres local** (transacción revertida, lote 152/LPP 20 de Santa Reyes, id 6
  real en la copia local): 2 registros el mismo día → la fn devuelve una sola fila con mortalidad
  1+2=3, huevos 100+150=250, consumo 10+12=22, peso_h avg(1.50,1.60)=1.55, uniformidad=82.00
  (último registro), tipo_alimento="Alimento B" (último) — exactamente la regla de §0.1/§0.2.
  Flag OFF confirmado sin cambios contra un lote real de 301 filas (empresa 1, LPP 7).
- Los 3 consumidores no-canónicos (header `ObtenerInformacionLoteAsync`, Reporte Técnico
  Producción `ObtenerSeguimientosDesdePDAsync`/`ObtenerSegsProdTabsAsync`/`ConItemsAsync`) ahora
  leen de la fn en vez de la tabla cruda. `DashboardService.Postura.cs` no necesitaba cambios
  (ya sumaba por día a nivel empresa, sin distinguir lote — coincidencia de diseño, no bug).

**Pendiente de producción**: frontend (flag en `CompanyFlags` + fix de
`consumoRealGrAveDiaH/M`), y el gate multipaís formal contra la copia completa de prod (el smoke
manual ya da fuerte evidencia, pero no reemplaza correr
`verificar_paridad_seguimiento_produccion.sql`).

**Hallazgo aparte, fuera de esta feature**: `ProduccionLoteDto.cs` comparte archivo con DTOs de OTRO
módulo (`ProduccionLoteController`/`Service`) — se dejaron intactos, solo se borraron los 4 records
`Seguimiento*`.

## 5 · LEVANTE — mapeo cerrado, arquitectura muy distinta a producción

**No existe una función canónica** equivalente a `fn_seguimiento_diario_produccion` para levante
(búsqueda exhaustiva en `backend/sql/` y migraciones: 0 resultados). Hoy, **nada dedupea por día**:
los 6 consumidores leen la tabla cruda (`seguimiento_diario_levante`, `tipo_seguimiento='levante'`)
y `SUM`/`COUNT(*)` fila por fila. Es la asimetría opuesta a producción: producción **descarta**
silenciosamente la fila extra (dedup agresivo); levante **sobre-cuenta** todo (sin dedup alguno).
Como hoy el índice único + el bloqueo en C# impiden que existan 2 filas el mismo día, esto nunca se
manifestó — pero es exactamente lo que rompería si se habilita el alta múltiple sin tocar estos 6
puntos:

| Consumidor | Archivo | Qué haría con 2+ filas/lote+día sin cambios |
|---|---|---|
| Validación de alta (`SeguimientoDiarioService.CreateAsync`, rama `levante`) | `SeguimientoDiarioService.cs:252-286` | Ya tiene lógica de MERGE-sobre-traslado (Feature 13) además del bloqueo — más compleja que producción, hay que preservarla |
| Validación de edición | `SeguimientoDiarioService.cs:537-546` | Bloqueo duro simple (`AnyAsync`+throw), sin la lógica de merge del alta |
| `fn_indicadores_levante_postura` (indicadores semanales VPI/IP) | `Migrations/20260710012849_Fase3RenameSeguimientoTables.cs:169-267` | `SUM` duplica; `dias_con_registro` viene de `COUNT(*)`, no de días distintos |
| `fn_reporte_semanal_levante_extras` (Reporte Técnico Semanal) | `backend/sql/fn_reporte_semanal_levante_extras.sql:150-262` | Mismo patrón — suma y sobre-cuenta días |
| `fn_resumen_semanal_ra_pesadas_levante` | `backend/sql/fn_resumen_semanal_ra_pesadas_levante.sql:120-152,232-251` | `COUNT(*) AS dias` sobre-cuenta; sumas duplican |
| `sp_recalcular_seguimiento_levante` → `produccion_resultado_levante` | `Migrations/20260710012849_Fase3RenameSeguimientoTables.cs:736-918` | Sin dedup: acumulados `SUM(...) OVER (ORDER BY fecha)` y `gr_ave_dia_h/m` (delta de peso entre filas consecutivas) quedan contaminados con 2 filas el mismo día |
| `LiquidacionCierreLoteLevanteService.Calcular` (cierre de levante) | `LiquidacionCierreLoteLevanteService.cs:94-111,223-235,285` | `segs.Sum(...)` duplica; `TotalRegistrosSeguimiento: segs.Count` sobre-cuenta; `ultimo = segs.LastOrDefault()` toma la última fila cronológica (no necesariamente el resumen correcto del último día) |
| Grilla + Excel de levante (frontend) | `frontend/src/app/features/lote-levante/pages/tabs-principal/tabs-principal.component.ts:250-393,576-750` | Sin pérdida de datos, pero 2 renglones con el mismo `edadDia`/semana — visualmente duplicado, igual síntoma que Reporte Técnico Producción |

No hay espejo C# de cálculo puro para levante (`Application/Calculos/` no tiene un
`SeguimientoDiarioLevanteCalculos` ni un `DedupPorDia` equivalente) — a diferencia de producción,
acá no hay un punto único de verdad que arreglar: **hay que decidir la regla de agregación en cada
uno de los 6 puntos**, o construir una función canónica nueva para levante (mismo patrón que
producción) y migrar los 6 consumidores a leerla — la segunda opción es más trabajo inicial pero dejaría
a levante con la misma garantía de "una sola fórmula por número" que ya tiene producción.

## 3 · Tabla de campos — aditivo vs. no aditivo (propuesta, a confirmar)

| Campo | Regla propuesta |
|---|---|
| Mortalidad H/M, Selección H/M, Consumo alimento H/M (kg), Huevos (todas las columnas), Ventas, Traslados | **SUMA** |
| Peso promedio H/M | **Promedio ponderado** por aves vivas de cada registro (no promedio simple) |
| Uniformidad, CV% | Requiere decisión — no hay una forma matemáticamente correcta de combinar 2 mediciones de uniformidad del mismo día sin la muestra cruda. Propongo: **último registro del día gana** (igual que hoy en la fn) |
| Observaciones / metadata jsonb | Concatenar o listar por registro (a definir) |
| `Validado`/`ValidadoAt`/`ValidadoPor` | Si CUALQUIER registro del día está sin validar, el día completo cuenta como pendiente (fail-closed, mismo criterio que doble-validación) |

## 4 · Tests y validación (gate obligatorio)

- xUnit nuevo/actualizado: `SeguimientoDiarioProduccionCalculosTests` (flag OFF = comportamiento
  actual byte a byte; flag ON = casos de suma/promedio de §3).
- Gate multipaís de `CLAUDE.md` §🛡️: correr `verificar_paridad_seguimiento_produccion.sql` antes y
  después en TODAS las empresas — todas menos Santa Reyes deben dar 0 diffs.
- Smoke funcional con el flag ON en Santa Reyes: 2 registros mismo lote+día → grilla, header,
  dashboard, Reporte Técnico Producción y los 3 indicadores semanales todos consistentes entre sí.
- `dotnet build` + `dotnet test` + `yarn build`.

---

*Nota: no se tocó `tracker_estado.md` en las secciones de otra sesión — hay trabajo en curso ahí
sobre `carga_masiva_santa_reyes_plan.md` (Fase F5, silos). Este plan y su bloque de tracker son
independientes y van al final del archivo.*
