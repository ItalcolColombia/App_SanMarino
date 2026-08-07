# Handoff — hallazgos de la sesión de postura (06-07 ago 2026)

Todo lo de acá ya está **commiteado en `main`** y **aplicado en la BD local**. En producción se
aplica solo en el próximo deploy (EF corre las migraciones al arrancar).

Origen: cargar el lote histórico **S-369** (levante + producción + alimento) desde tres Excel y
hacer que los reportes de la app coincidan con ellos. Al hacerlo salieron a la luz una docena de
defectos que **no eran de este lote** sino del código, y que estaban vivos en producción para todas
las empresas.

---

## 1 · Commits de esta sesión (postura)

| Commit | Qué arregla |
|---|---|
| `d50cd9c` | `lote_id` de inventario a integer — el traslado `MOV-*` decía «Completado» sin mover aves |
| `2ac57a8` | El reporte de levante contaba 614 aves de más y editar el lote borraba las bajas |
| `2d26fae` | El saldo de producción no veía ventas, ni traslados, ni la selección de machos |
| `91533a0` | La curva de levante devolvía 0 puntos · el traslado entre sublotes se aplicaba de un solo lado |
| `5054da3` | Un lote poblado por traslado reportaba saldo negativo o al doble |
| `b315612` | El saldo de levante no descontaba las ventas de aves |
| `219f05f` | Tab Indicadores unificado (levante + producción) y se quita el «Reporte semana» |

**Migraciones que se aplicarán en el deploy** (todas idempotentes, data-only con Designer clonado):

```
20260806050306_AlinearLoteIdInventarioAvesAInteger
20260806074742_ArreglarTriggerSyncLotePosturaLevanteNoPisarAvesVivas
20260806092854_VentaAvesEnFilaDiariaProduccion
20260806093256_SaldoProduccionDescuentaVentasYTraslados
20260806171700_AlinearPesoHuevoProduccionANullable
20260806194500_CurvaLevanteAceptaSemanaNula
20260806211500_BaseAvesPorTrasladoEnLevante
20260806235000_VentaAvesEnFilaDiariaLevante
```

---

## 2 · 🔴 LO QUE QUEDA ABIERTO (empezá por acá)

### 2.1 · El espejo `.sql` de producción está desincronizado de lo desplegado

**El más urgente, porque es una bomba de tiempo.**

`backend/sql/fn_indicadores_produccion_postura.sql` **NO coincide con la función que corre en la
BD**: le falta la columna de salida `seleccion_machos`, que agregó la migración
`20260806093256_SaldoProduccionDescuentaVentasYTraslados` y que el espejo nunca recibió.

Lo desplegué en local para probar y **dejó la fn en 68 columnas en vez de 69** ⇒ habría reventado
`IndicadorProduccionSemanalBdRow.SeleccionMachos` en runtime. Lo detectó el gate y restauré desde la
definición viva. **El día que alguien redespliegue ese archivo sin darse cuenta, tumba la columna.**

Complicación extra: el cuerpo **desplegado** viene con CRLF inflado (7 líneas en blanco entre cada
sentencia) porque la migración lo embebió así, mientras el archivo del disco está limpio. O sea que
no se puede reconciliar con un copy-paste de `pg_get_functiondef`: hay que **portar el cambio de
`seleccion_machos` al archivo limpio** y validar con el gate de §5.

Aprovechá y metele el arreglo que quedó pendiente (§2.2).

### 2.2 · `uniformidad_guia` sale 0 cuando debería ser NULL

En `fn_indicadores_produccion_postura`:

```sql
g_unif   := COALESCE(g_unif, 0);   -- y lo mismo con g_peso_h / g_peso_m
```

La guía **no define uniformidad para edades de producción** (solo 25 de sus 98 filas la traen, todas
de levante). El `COALESCE` es **deliberado** (replica un `ParseDouble ⇒ 0` viejo del C#), pero hace
que la columna «Uniformidad Guía» muestre 0 en todas las semanas, que se lee como «la guía exige
0 %» en vez de «sin dato», y que la diferencia se calcule contra ese 0.

**Ya está mitigado en el front** (`hayGuiaUniformidad()` trata el 0 como ausencia y pinta «—»), pero
el arreglo de fondo es que la fn mande NULL. Dejar quietos `g_cons_*`, `g_mort_*` y `g_retiro_ac_*`:
la guía **sí** los trae en toda la curva y cambiarlos movería números sin necesidad.

### 2.3 · El seguimiento diario acepta cargar más bajas que aves disponibles

**Requiere decisión del usuario: convertirlo en bloqueo rechaza escrituras que hoy pasan, en todas
las empresas.**

Caso probado (lote 123, Demo): base 5.303, una salida de 5.100 el 06-jul, ~85 aves vivas, y el
**03-ago alguien cargó 500 muertes**. El reporte muestra −460 —es el honesto— y el maestro lo tapa
con su clamp mostrando 0.

Lo que hay hoy, verificado leyendo el código:

| Punto | Estado |
|---|---|
| `SeguimientoLoteLevanteService.Crud.cs:357` (REQ-011b) | Su propio doc-comment dice «soft-check, **NO bloqueo duro**»: solo `LogWarning`, envuelto en `try/catch` que se traga todo, y compara `saldo == 0` **exacto** ⇒ con saldo negativo, o con 5 aves y 100 de mortalidad, **no dispara** |
| `CreateLoteDto` / `UpdateLoteDto` | `HembrasL` es `int?` y **no existe ningún validator** para esos DTO |
| `LoteService.cs:613` | `ent.HembrasL = dto.HembrasL;` **sin condición** ⇒ un PUT que omita el campo **borra la base** |
| `lote-list.component.ts:465` | `hembrasL: [null]` y `machosL: [null]`, **sin `Validators.required`** (mientras `granjaId` y `fechaEncaset` sí lo tienen) |
| Maestro (`aves_h_actual`) | Se escribe con `Math.Max(0, …)` / `GREATEST(0, …)` ⇒ **esconde el sobregiro** |

Sugerencia de fases: primero los guards de creación/edición del lote (riesgo bajo), después el
bloqueo del seguimiento diario **con un barrido previo** de cuántas filas históricas violarían la
regla.

### 2.4 · Dos lotes con saldo de hembras negativo por dato, no por código

`A374A` (lote 116, **Agroavicola Sanmarino**, LA ESMERALDA) ya quedó arreglado por `5054da3`.
`LOTE 235A` (lote 123, **Demo**) sigue en −460 y **está bien que siga**: el dato está genuinamente
sobregirado (§2.3). No lo "arregles" en el reporte.

### 2.5 · Nombres duplicados

- El guard de nombre de lote duplicado (`LoteService.cs:797`) es **por granja** y nació el
  **2026-07-17**. `A374A` hoy sí se rechazaría (sus dos copias están en granja 20), pero
  **`LOTE 235A` seguiría pasando** porque sus copias están en granjas distintas (90 y 95).
- En **Demo** hay **lotes base duplicados**: «LOTE 235» ×2 (ids 9, 16) y «LOTE 237» ×3 (11, 17, 18).

### 2.6 · Menor

- `fn_resumen_semanal_ra_pesadas_levante` deja `part` en NULL para semanas cuyo único lote tiene
  saldo negativo. Es a propósito (un 0 lo haría desaparecer de los ponderados), pero desaparece
  cuando se arregle §2.3.
- El `%` de producción usa **aves vivas corrientes** como denominador, no el promedio inicio/fin de
  la semana. Difiere <0,2 % y es internamente consistente; queda como nota, no como bug.

---

## 3 · Reglas de negocio confirmadas con el usuario (no las revisites)

1. **Un lote SIN aves encasetadas es legítimo**: hay lotes que se pueblan **solo por traslado** desde
   otros lotes. Forzar `hembras_l > 0` sería incorrecto.
2. **«Todo lote debe colgar de un lote base» no alcanza**: los lotes que fallaron ya tenían lote
   base, y el base incluso declaraba bien las aves (30.833 = suma exacta de sus sublotes). El
   reporte no lee el base para el saldo, lee `hembras_l` **del sublote**.
3. La regla que falta es más fina: (a) el sublote debe recibir su parte de las aves del base, (b) no
   se escribe seguimiento contra un lote sin aves, (c) nombre único dentro del lote base.

---

## 4 · Trampas que ya me costaron tiempo (para que no las repitas)

### Base de datos / esquema

- `seguimiento_diario_levante.lote_id` es **VARCHAR** mientras `lotes.lote_id` es **INTEGER** ⇒ hay
  que castear `::text`. Esa tabla **no tiene `deleted_at`**.
- `companies` **no tiene `deleted_at` ni `company_id`**: su PK es `id`.
- `lote_postura_levante` usa `fecha_encaset`, no `fecha_encasetamiento`.
- `__EFMigrationsHistory` tiene la columna en snake_case: `migration_id`.
- La guía genética tiene **DOS filas para la edad 25**: `25` (levante) y `25P` (producción). La
  columna `edad` es **texto** ⇒ `edad::int` revienta con `25P`. Cualquier verificación que lea la
  fila `25` para producción va a dar un falso positivo.
- La guía guarda **peso en gramos** y los indicadores trabajan en **kg**: la fn divide /1000.

### Consultar la BD local

Puerto **5433**, base `sanmarinoapplocal`. Helper del scratchpad:

```
python -c "import io,sys; sys.stdout=io.TextIOWrapper(sys.stdout.buffer,encoding='utf-8',errors='replace'); import db; print(db.q('<SQL>'))"
```

⚠️ `db.q()` con SQL largo y acentos por `-c` rompe con `invalid byte sequence for encoding UTF8`:
escribí el SQL a archivo y usá `psql -f`.

### Levantar el entorno

- Backend: `dotnet run` muere cuando termina la llamada de la herramienta. Funciona lanzándolo con
  `Invoke-CimMethod Win32_Process Create` sobre un `.bat` (reparenta el proceso).
- El backend **ignora `PORT`**: usá `ASPNETCORE_URLS=http://localhost:5002`.
- El front espera la API en **5002** (`environment.ts`).
- **CORS sale de configuración**: para servir el front en otro puerto,
  `AllowedOrigins__0=http://localhost:4200` + `AllowedOrigins__1=http://localhost:4300` por variable
  de entorno — **no hay que tocar `Program.cs`**.
- El 4200 suele estar tomado por otra sesión; en `.claude/launch.json` ya existe
  `frontend-node22-4300`.
- Smoke UI sin credenciales: mintear el JWT y meter `auth_session` (JSON **plano**) en
  **`localStorage`**, nunca en `sessionStorage` (algo lo limpia al arrancar y rebota a `/login`).
- Build de .NET: si MSBuild tira `MSB4166 child node exited prematurely`, matá los `dotnet`/`MSBuild`
  colgados y recompilá con `-m:1`.

### Git / migraciones

- **`dotnet ef migrations add` arrastra los cambios de entidad EN VUELO de otras sesiones** al
  `ModelSnapshot`. Con varias ventanas trabajando el mismo repo, cloná el Designer y dejá el
  snapshot quieto; el DDL idempotente absorbe el desfase.
- **Siempre stagear archivo por archivo.** El árbol tuvo hasta 56 archivos de otra sesión (Tickets)
  al mismo tiempo.
- Al correr `dotnet ef database update` se aplican **todas** las migraciones pendientes, incluidas
  las de otras sesiones sobre la BD local compartida.

---

## 5 · El gate que hay que correr al tocar una fn compartida

Es la receta que cazó todos los bugs de esta sesión. **Comparar fila a fila contra la versión previa
desplegada en paralelo con otro nombre**, en TODAS las empresas:

```sql
-- 1) traer la version previa y renombrarla
--    git show <sha>:backend/sql/<fn>.sql  →  reemplazar el nombre por <fn>_V0  →  psql -f
--    (si el .sql esta desincronizado, sacar la base de pg_get_functiondef de la fn VIVA)

-- 2) except en los dos sentidos
with e as (select id from companies),
 nue as (select c.id cid, s.s, r.* from e c cross join generate_series(1,53) s(s)
         cross join lateral public.<fn>(c.id, 2026, s.s, NULL, NULL, false) r),
 vie as (... <fn>_V0 ...)
select (select count(*) from (select * from nue except select * from vie) z) solo_nuevo,
       (select count(*) from (select * from vie except select * from nue) z) solo_viejo;

-- 3) si difiere, AISLAR la columna culpable (el except no dice cual):
--    count(*) filter (where n.<col> is distinct from v.<col>)  por cada columna de salida
```

El paso 3 es el que evitó desplegar un bug: el `except` decía «29 filas distintas» sin decir por
qué; el conteo por columna mostró que 28 de las 29 columnas eran idénticas y la culpable era `part`.

### El árbitro independiente

`lote_postura_levante.aves_h_actual / aves_m_actual` lo escriben los services, **no las fns**: es
independiente del reporte. Cuando reporte y maestro discrepan, uno de los dos tiene un bug. Tras los
arreglos, **7 de 8 lotes de todas las empresas cuadran exacto**.

⚠️ Pero el maestro **clampea**, así que esconde los sobregiros reales: donde el reporte dice −460 y
el maestro 0, **el honesto es el reporte**.

---

## 6 · Estado de validación al cerrar

- **665 días** del lote S-369 comparados campo a campo contra el Excel: **0 diferencias**.
- Consolidado: **480 celdas**, 0 diferencias. Levante 24/24 semanas contra la hoja «general»;
  producción 22/23 — la única celda distinta son **5 huevos del galpón 9 del 24-jun**, que es un
  descuadre **del propio informe** (recolección 2.549 vs clasificación 2.554), no de la app.
- **19/19 endpoints** de reportes de las dos fases responden 200 con datos.
- `dotnet build` 0 errores · `dotnet test` **1.834 verdes** · `ng build` correcto (único warning: el
  bundle budget preexistente que el repo ya acepta).
- Indicadores vs guía genética: levante **24/24 exactas**; producción 24/24 en todas las columnas
  salvo la uniformidad de §2.2.

**Banco de pruebas reproducible:** el lote S-369 vive en la granja 44 («Pruebas Moises»), guía AP
2026, con 4 archivos de carga masiva en `C:\Users\SAN MARINO\Documents\lote carga masiva pruebas`.
El ciclo completo se rehace desde cero con `ciclo_completo.py` + `cargar_produccion.py` del
scratchpad (borra la granja 44 y la reconstruye).

---

## 7 · Documentos relacionados

- `tracker_estado.md` — el detalle checkbox por checkbox de todo lo anterior.
- `fase_de_desarrollo/19_indicadores_levante_produccion_ux_plan.md` — el plan del tab Indicadores.
- `C:\Users\SAN MARINO\Documents\lote carga masiva pruebas\REPORTES_POR_FASE.md` — inventario de
  reportes endpoint por endpoint, por fase.
- `…\VALIDACION_S-369.md` — la comparación día a día contra el Excel.
