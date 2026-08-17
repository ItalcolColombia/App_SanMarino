# Vacunación W4 — Scoping por núcleo/galpón/lote en las dos funciones SQL del módulo

**Fecha:** 2026-08-17 · **Continúa:** W1 (`bd935cb`), W2 (`f2794c6`), W3 (`59496a8`)
**Plan madre:** [`vacunacion_cronograma_vivo_plantillas_plan.md`](vacunacion_cronograma_vivo_plantillas_plan.md) §W4
**Alcance:** backend (2 fns SQL + 3 services) · sin cambios de front · sin tablas ni columnas nuevas

---

## 1. El problema, en una línea

El módulo Vacunación respeta el alcance granular de ubicación (`user_farms.restrict_locations` +
`user_farm_scopes`) en **todo lo que pasa por C#**, y **no lo respeta en las dos lecturas que hace
la BD**: los combos (`fn_vacunacion_filter_data`) y la bandeja de pendientes
(`fn_vacunacion_pendientes`) filtran sólo por `user_farms` (granja completa).

Efecto hoy: un usuario restringido a un galpón **ve en el desplegable todos los lotes de la granja**
y **recibe en la bandeja de inicio pendientes de lotes que no puede abrir** (si hace clic, el
cronograma le responde vacío — el guard de C# sí lo frena). O sea: fuga de nombres de lote y ruido
operativo, no fuga de datos sanitarios.

## 2. Estado auditado (lo que YA está — no rehacerlo)

| Camino | Alcance de granja | Alcance granular | Dónde |
|---|---|---|---|
| Cronograma de un lote (`POST /cronograma`) | ✅ | ✅ `PermiteLoteDeLineaAsync` | `VacunacionCronogramaService.cs:36-61` |
| Materializador (botón y masivo) | ✅ | ✅ `PermiteAsync` | `VacunacionMaterializadorService.cs:100-106` |
| Reportes de cumplimiento (2) | ✅ | ✅ `ResolverLotesVisiblesPorGranjaRestringidaAsync` + `FilaVisible` | `VacunacionReportesService.cs:39-79` |
| **Combos (`filter-data`)** | ✅ | ❌ **falta** | `fn_vacunacion_filter_data.sql:30-40` |
| **Bandeja de pendientes** | ✅ | ❌ **falta** | `fn_vacunacion_pendientes.sql:69-80` |

El encabezado de `fn_vacunacion_pendientes.sql` ya lo dejó anotado en W3.1.5: *«hay que cambiar LAS
DOS funciones, o la bandeja mostrará lotes que el resto del módulo ya no deja ver»*.

**Hallazgo del pasar:** la misma regla («con lote de la tabla `lotes` manda el nivel LOTE; sin él,
galpón y después núcleo») está **copiada a mano en 3 services**. Sumarle una 4ª y 5ª copia en SQL es
justo lo que prohíbe *una sola fórmula por número*.

## 3. Decisión de arquitectura

**El cierre de visibilidad se calcula UNA vez en C# (`UserLocationScopeCalculos.ComputeScope`, ya
existente con 20 tests) y viaja a la fn como 4 arrays. La BD sigue filtrando.**

```
fn_vacunacion_filter_data(p_user_guid, p_company_id, p_pais_id,
                          p_scope_farm_ids INT[],   -- granjas RESTRINGIDAS del usuario
                          p_scope_nucleos  TEXT[],  -- claves compuestas 'granjaId|nucleoId'
                          p_scope_galpones TEXT[],  -- PK global
                          p_scope_lotes    INT[])   -- lotes.lote_id permitidos
```

**Por qué así y no replicando el cierre en SQL:**

- El cierre tiene reglas que no son un `WHERE` (grants muertos que no otorgan nada, ancestros
  visibles para navegación, unión núcleo⇒galpones⇒lotes). Escribirlo en SQL lo **duplica**, y las
  dos copias divergen — es exactamente el incidente del saldo de alimento (3 implementaciones, 3
  números distintos).
- El resolver (`LocationScopeResolver`, Scoped con caché por request) **ya cuesta cero** para el
  usuario sin restricciones: una query liviana a `user_farms` y termina. Sólo para granjas
  restringidas carga grants + catálogo.
- El filtrado pesado (lotes de 3 líneas × empresa) **sigue en la BD**: los arrays son chicos
  (el cierre de un usuario restringido), no se trae nada a memoria para descartarlo.
- **Fail-closed por construcción:** una granja restringida sin grants entra en `p_scope_farm_ids` y
  no aparece en ningún otro array ⇒ cero filas de esa granja. No hay «lista vacía = sin filtro».

**Clave de núcleo compuesta `granjaId|nucleoId`**: `nucleo_id` se repite entre granjas (PK compuesta
con la granja). Un array plano de `nucleo_id` haría visible el núcleo homónimo de otra granja.

### Regla única (la que hoy está copiada 3 veces)

```
granja NO restringida                        ⇒ visible
lote de la tabla `lotes` presente            ⇒ visible sii lote ∈ LotesPermitidos   (manda el nivel LOTE)
sin lote, con galpón                         ⇒ visible sii galpón ∈ GalponesVisibles
sin lote, sin galpón, con núcleo             ⇒ visible sii núcleo ∈ NucleosVisibles
sin ninguno de los tres                      ⇒ NO visible (fail-closed)
```

Se extrae **tal cual** a `UserLocationScopeCalculos.PermiteUbicacion(...)` y los 3 services pasan a
delegar. **Refactor sin cambio de comportamiento**: misma salida, un solo dueño.

> Engorde no tiene fila en `lotes` ⇒ se gobierna por galpón/núcleo (limitación conocida y documentada
> del feature original, no se toca acá).

### Decisión W4.2 — de dónde sale la ubicación en los reportes

Los reportes filtran por la ubicación **guardada en el ítem** (`vacunacion_cronograma_item.granja_id/
nucleo_id/galpon_id`, sellada al crearlo); la bandeja y el cronograma usan la ubicación **del lote
hoy**. Un lote que cambia de galpón deja al ítem con la ubicación vieja ⇒ el mismo lote podría verse
en la bandeja y esconderse en el reporte.

**Se alinea al LOTE** (la autoridad es dónde está el lote hoy, que es lo que el usuario puede abrir),
igual que las otras dos rutas. Es el único caso en que W4 cambia una salida ya existente y queda
declarado. El dato del ítem **no se toca** (es historia sanitaria).

## 4. Archivos

### Crear
- `backend/src/ZooSanMarino.Infrastructure/Migrations/<ts>_ScopingUbicacionVacunacionFns.cs` (+ Designer clonado)
  — data-only: `DROP FUNCTION IF EXISTS` (firma vieja **y** nueva) + `CREATE OR REPLACE` de las 2 fns.
- `backend/tests/ZooSanMarino.Application.Tests/UserLocationScopeSqlParamsTests.cs` — tests del
  aplanado + de la regla única.

### Modificar
- `backend/sql/fn_vacunacion_filter_data.sql` — 4 parámetros + filtro en el CTE `lotes`.
- `backend/sql/fn_vacunacion_pendientes.sql` — ídem (y se le retira la nota «leer antes de W4»).
- `backend/src/ZooSanMarino.Application/Calculos/UserLocationScopeCalculos.cs` — `PermiteUbicacion`
  (regla única) + `AplanarParaSql` (cierre → 4 arrays).
- `backend/src/ZooSanMarino.Infrastructure/Services/Vacunacion/Funciones/VacunacionCronogramaService.Filtros.cs`
  — resolver el cierre y pasar los 4 arrays.
- `.../Funciones/VacunacionRegistroService.Pendientes.cs` — ídem.
- `.../Vacunacion/VacunacionRegistroService.cs` — inyectar `ILocationScopeResolver` (hoy no lo tiene).
- `.../Vacunacion/VacunacionCronogramaService.cs` · `VacunacionMaterializadorService.cs` ·
  `VacunacionReportesService.cs` — delegar en `PermiteUbicacion` (sin cambio de comportamiento);
  en reportes, además, la ubicación sale del lote.

### NO se tocan
Front (el combo se acota solo), tablas, columnas, índices, permisos, menús, ni las 2 fns de
cumplimiento (el filtro sigue en C#, donde ya estaba).

## 5. Reglas de negocio

1. Usuario **sin restricciones** ⇒ los 4 arrays van vacíos ⇒ las fns devuelven **byte a byte** lo de
   hoy (gate de no-regresión del smoke).
2. Granja restringida **sin grants** ⇒ cero filas de esa granja (fail-closed).
3. Grant muerto (lugar borrado / re-keyeado) ⇒ no otorga nada — lo garantiza `ComputeScope` contra el
   catálogo vigente.
4. Sin sesión (`UserGuid` nulo): `filter-data` sigue lanzando `UnauthorizedAccessException`;
   `pendientes` sigue devolviendo lista vacía. Sin cambios.
5. La lista de **granjas** del combo no se recorta: son las asignadas al usuario, igual que hoy y que
   los reportes. Lo que se recorta son los **lotes**.
6. `p_hoy`, franja, clasificación y orden de la bandeja: **intactos** (W4 sólo filtra filas).

## 6. Casos de prueba

**xUnit (Application):**
- `PermiteUbicacion`: los 5 casos de la tabla (global, lote sí/no, galpón sí/no, núcleo sí/no, los
  tres nulos ⇒ false) + que un lote presente **ignora** galpón/núcleo aunque estén permitidos.
- `AplanarParaSql`: cierre vacío ⇒ 4 arrays vacíos; granja restringida sin grants ⇒ su id en
  `FarmIds` y nada más; clave de núcleo compuesta; determinismo del orden.
- Equivalencia: para un cierre dado, `PermiteUbicacion` y la pertenencia a los arrays aplanados dan
  el **mismo** resultado (es el contrato que la SQL implementa).

**Smoke SQL en transacción revertida** (la BD local no tiene ni un usuario restringido: se siembra y
se revierte), sobre lotes reales de la empresa 3:
- Usuario **sin** restricción ⇒ filas idénticas a la versión previa de la fn (desplegada en paralelo
  con otro nombre y comparada fila a fila, 0 diferencias) — en las **dos** fns.
- Grant de núcleo / de galpón / de lote ⇒ ve exactamente lo suyo; los hermanos desaparecen.
- `restrict_locations = true` **sin** grants ⇒ 0 filas.
- Grant apuntando a un galpón de **otra** granja ⇒ no otorga nada.
- Bandeja y combos **coherentes**: ningún lote pendiente que el combo no muestre (era el riesgo de
  subir una sola fn).

**Smoke del servicio real** (EF + `SqlQueryRaw`, no sólo psql): que el mapeo siga trayendo todos los
campos y que el cierre resuelto por `LocationScopeResolver` coincida fila a fila con el filtro SQL.

**Smoke con usuario restringido de punta a punta** (W4.2): mismo usuario contra cronograma, bandeja y
los 2 reportes ⇒ el conjunto de lotes visibles es **el mismo** en los cuatro.

## 7. Riesgos

| Riesgo | Mitigación |
|---|---|
| Cambiar la firma de una fn rompe llamadores | Sólo hay 1 llamador por fn (verificado por grep). `DROP … IF EXISTS` de **ambas** firmas en la migración. |
| Un usuario legítimo deja de ver sus lotes | Regla 1 + gate de paridad: sin restricciones, salida idéntica. Hoy **0 usuarios restringidos** en la BD local ⇒ el cambio es no-op para todos hasta que un admin restrinja. |
| Divergencia SQL↔C# | La SQL sólo hace pertenencia a conjuntos; el cierre es C#. El smoke los compara fila a fila. |
| BD local desalineada (le faltan migraciones de otras sesiones) | Se crean las fns por DDL directo para el smoke y se revierte; `__EFMigrationsHistory` no se toca. |

---

## 8. Lo que apareció al ejecutar (no estaba en el plan)

### 8.1 El orden de los lotes del combo nunca fue determinístico
`fn_vacunacion_filter_data` ordenaba `ORDER BY l.fecha_encaset DESC NULLS LAST` — un orden **parcial**:
dos lotes encasetados el mismo día salían en el orden que quisiera el plan de consulta. El gate de
paridad lo destapó (mismo contenido, 121 lotes, **0 diferencias fila a fila**, pero distinto orden
entre empates). Se agregó el desempate `, l.linea_productiva, l.lote_id`: la lista ahora es estable
llamada tras llamada, y el gate compara contenido (que es el invariante) más determinismo.

### 8.2 🔴 `GET /cumplimiento` (reporte por lote) estaba ROTO en runtime — bug preexistente
El smoke del servicio real reventó con
`The required column 'total_tardio1semana' was not present in the results of a 'FromSql' operation`.

- La función devuelve `total_tardio_1_semana` / `total_tardio_2_mas_semanas`.
- La convención snake_case de EF traduce `TotalTardio1Semana` → **`total_tardio1semana`** (no mete
  guión bajo después de un dígito), y `SqlQueryRaw` exige esa columna exacta.
- ⇒ el endpoint tiraba excepción **para todos los usuarios y todas las empresas**, desde que existe.
  No lo veía nadie: compila, pasa los tests y sólo aparece ejecutando (la misma clase de error que
  W2.5.2). El reporte de **detalle** no se ve afectado: ninguna de sus columnas tiene dígitos.

**Arreglo mínimo** (en `VacunacionReportesService.Consultas.cs`): la consulta deja de ser `SELECT *`
y agrega los dos alias que EF espera. **No** se renombra la función (la comparten reportes ya
desplegados) **ni** el DTO (viaja al front). Queda cubierto por el smoke del servicio real.
