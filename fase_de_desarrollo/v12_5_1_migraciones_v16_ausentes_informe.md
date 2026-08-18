# V12.5.1 — Las dos migraciones de la v16 de engorde no existen: informe de investigación

**Fecha:** 2026-08-18 · **HEAD:** `905d0a1` (rama `main`) · **Tipo:** investigación (no toca código, ni BD, ni tracker)
**Alcance:** verificar la discrepancia que señala el ítem `V12.5.1` de `tracker_estado.md` sobre las migraciones
`20260809120000_FnAlimentoMarcadoAtribucionEngorde` y `20260809120100_FnSeguimientoEngordeV16EntregaCicloSiguiente`.

---

## Resumen ejecutivo (el veredicto en 5 líneas)

1. **Las dos migraciones nunca se commitearon en ninguna rama, ni siquiera en un commit huérfano**: no aparecen en el historial de ningún ref, ni en los reflogs, ni en los tips de las 40 ramas del repo. El propio commit que escribió «F1.6 ✅ 2 migraciones EF idempotentes» en el tracker (`8424557`) **no tocó un solo archivo de backend**.
2. **No es un "trabajo perdido": fue una reversión deliberada y documentada.** La v16 se intentó **4 veces**, el gate la declaró **NO-GO** (dos verificadores independientes + un juez) y la sesión borró del working tree las migraciones, el `.sql` y el cálculo C#. El tracker registra esa reversión en el **mismo bloque** donde los checkboxes siguen en `- [x]`.
3. **La funcionalidad v16 (entrega/atribución) NO está en ninguna parte.** `backend/sql/fn_seguimiento_diario_engorde.sql` está en **v15** (1027 líneas), y la fn instalada en la BD local también es v15: cero rastros de `DIFERIDO` / `NEUTRO_` / `kg_diferido` / `cedente`. Sí está la marca `para_proximo_ciclo` **en su forma v15** (las 4 exclusiones), que es un modelo distinto y el que el gate declaró defectuoso.
4. **La BD local está perfectamente alineada con el repo:** 298 filas en `__EFMigrationsHistory` = 298 archivos de migración, **0 huérfanas y 0 pendientes**, y **ninguna fila `20260809*`**. No se reprodujo el modo de falla de `marcar_todas_migraciones_pendientes.sql`.
5. **Recomendación: corregir el tracker, NO crear las migraciones.** El riesgo real no es de despliegue (es cero por construcción): es de **lectura** — el bloque «FASE 1 IMPLEMENTADA» con 6 checkboxes en `- [x]` invita a una sesión futura a asumir que la v16 existe, o peor, a "recrear las migraciones que faltan" y reintroducir un cambio que ya fue rechazado por el gate con dos bloqueantes medidos.

---

## 1. ¿Existieron alguna vez? — No, nunca fueron commiteadas

### 1.1 No están en el árbol de trabajo (punto de partida, reverificado)

```
$ ls backend/src/ZooSanMarino.Infrastructure/Migrations/ | grep -i '202608'
...
20260808010000_FnSeguimientoEngordeV14CorteCicloSiguiente.cs
20260808120000_AlimentoPrevioEncasetMarcaCiclo.cs
20260808130000_FnSeguimientoEngordeV15AperturaVisibleYMarcaCiclo.cs
20260810002504_AddStockClaveNaturalUnica.cs          ← el salto: de 0808 a 0810
20260810031057_AddSyncTombstones.cs
...
```

Hay un **hueco limpio entre `20260808130000` y `20260810002504`**: no existe ninguna migración con timestamp `20260809*`.

### 1.2 No están en el historial de ningún ref (búsqueda por path exacto)

```
$ for p in \
   backend/src/ZooSanMarino.Infrastructure/Migrations/20260809120000_FnAlimentoMarcadoAtribucionEngorde.cs \
   backend/src/ZooSanMarino.Infrastructure/Migrations/20260809120100_FnSeguimientoEngordeV16EntregaCicloSiguiente.cs \
   backend/sql/fn_alimento_marcado_atribucion.sql \
   backend/sql/verificar_marca_proximo_ciclo.sql \
   backend/src/ZooSanMarino.Application/Calculos/AtribucionAlimentoMarcadoCalculos.cs ; do
     echo "--- $p"; git log --all --oneline -- "$p" | head -5; echo "  (fin)"; done

--- backend/src/ZooSanMarino.Infrastructure/Migrations/20260809120000_FnAlimentoMarcadoAtribucionEngorde.cs
  (fin)
--- backend/src/ZooSanMarino.Infrastructure/Migrations/20260809120100_FnSeguimientoEngordeV16EntregaCicloSiguiente.cs
  (fin)
--- backend/sql/fn_alimento_marcado_atribucion.sql
  (fin)
--- backend/sql/verificar_marca_proximo_ciclo.sql
  (fin)
--- backend/src/ZooSanMarino.Application/Calculos/AtribucionAlimentoMarcadoCalculos.cs
  (fin)
```

Los 5 artefactos que el tracker declara creados devuelven **cero commits** en `--all`.

### 1.3 Tampoco están en los reflogs (cubre commits no alcanzables por ninguna rama)

```
$ git log --all --reflog --oneline -- <los 4 paths de arriba>
[fin - vacio = ni siquiera en reflogs]
```

### 1.4 Tampoco en el tip de ninguna de las 40 ramas (busqueda por contenido)

```
$ git grep -l -E 'AtribucionAlimentoMarcado|FnSeguimientoEngordeV16|fn_alimento_marcado_atribucion|FnAlimentoMarcadoAtribucion' \
    $(git for-each-ref --format='%(refname)')

refs/heads/claude/affectionate-maxwell-287108:fase_de_desarrollo/marca_proximo_ciclo_rediseno_plan.md
refs/heads/claude/affectionate-maxwell-287108:tracker_estado.md
refs/heads/main:fase_de_desarrollo/marca_proximo_ciclo_rediseno_plan.md
refs/heads/main:fase_de_desarrollo/senalamiento_anomalia_r2_fase3_plan.md
refs/heads/main:tracker_estado.md
refs/heads/main-produccion:fase_de_desarrollo/marca_proximo_ciclo_rediseno_plan.md
refs/heads/main-produccion:tracker_estado.md
refs/remotes/origin/HEAD:...  refs/remotes/origin/main:...  refs/remotes/origin/main-produccion:...
```

**Los únicos archivos del repo que nombran la v16 son documentación** (el plan y el tracker). Ni un `.cs`, ni un `.sql`.

### 1.5 Tampoco quedaron en un stash

```
$ git stash list
stash@{0}: On postura-verenice-rev-6jul26: inventario draft superado (borrador WIP, version final ya en main) 2026-07-20
stash@{1}: On devpilot/d387ed60: devpilot local state
```

> **Exhaustividad, dicha explícitamente.** Las cuatro búsquedas (path exacto sobre `--all`, `--all --reflog`, `git grep` sobre los 40 refs, y `git stash list`) cubren: todas las ramas locales y remotas, todo su historial, los commits desreferenciados que aún viven en el reflog, y el stash. **Sí es un hecho que nunca se commitearon.** Lo único que estas búsquedas no pueden ver es un objeto dangling sin entrada de reflog o un clon de otra máquina — pero eso es hipotético y el §1.6 da la explicación positiva que lo hace innecesario.

### 1.6 La explicación positiva: fueron **revertidas a propósito**, y el propio tracker lo dice

No es una omisión ni un commit perdido. La secuencia está en git:

```
$ git show --no-patch --format='%h %ad %s' --date=short d6aeccb 8424557 801b14f
801b14f 2026-08-08 feat(inventario,engorde,postura): fecha real de llegada del alimento + ingreso inicial del ciclo visible
d6aeccb 2026-08-08 docs(tracker): la v16 de engorde se intento 3 veces y se revirtio
8424557 2026-08-09 fix(inventario): deshabilita marcar alimento "para el proximo ciclo" hasta su rediseno
```

**La prueba decisiva** es el `--stat` de `8424557`, el commit que cierra la ronda 4 y que escribió el bloque «FASE 1 IMPLEMENTADA» en el tracker:

```
$ git show --stat --format='' 8424557
 .../marca_proximo_ciclo_rediseno_plan.md           | 513 +++++++++++++++++++++
 .../gestion-inventario-page.component.ts           |  18 +-
 .../inventario-historial-page.component.ts         |  11 +-
 tracker_estado.md                                  | 338 ++++++++++++++
 4 files changed, 874 insertions(+), 6 deletions(-)
```

**Cuatro archivos: un plan, dos componentes Angular y el tracker. Cero backend, cero migraciones, cero SQL.** El mismo commit que marcó `- [x] F1.6 2 migraciones EF idempotentes` es el que confirma que no se commitearon.

Y el tracker (`tracker_estado.md:1033-1084`) explica por qué, con su propio veredicto:

> `## VEREDICTO DE LA RONDA 4: **NO-GO — REVERTIDA** (y la marca queda DESHABILITADA en la UI)`
> `### Reversión (verificada)`
> `- [x] Working tree: git checkout -- backend + borrados los untracked del intento`
> `      (fn_alimento_marcado_atribucion.sql, verificar_marca_proximo_ciclo.sql,`
> `      AtribucionAlimentoMarcadoCalculos.cs + test, migraciones 20260809120000_* y 20260809120100_*).`

Los dos bloqueantes que motivaron el NO-GO están medidos en el tracker (líneas 1046-1057): liquidar el **cedente** esconde 3.000 kg reales de toda tabla diaria viva; liquidar el **destino** los **duplica** (+3.000 kg creados) con `descuadre_kg = 0,00` en ambos estados, o sea con el detector ciego.

**Conclusión de la pregunta 1:** las migraciones **nunca existieron en git**. Sí existieron en el working tree de aquella sesión, se probaron, el gate las rechazó y se borraron. El tracker documentó la implementación como hecho consumado (`- [x]`) y agregó la reversión más abajo **sin desmarcar los checkboxes de arriba**. Esa es la única falla real: un problema de **redacción del tracker**, no de código perdido.

---

## 2. ¿La funcionalidad está o no está? — La v16 no; la marca v15 sí, y es otra cosa

### 2.1 El `.sql` del repo está en v15

```
$ wc -l backend/sql/fn_seguimiento_diario_engorde.sql
1027 backend/sql/fn_seguimiento_diario_engorde.sql

$ grep -n -i -E 'v1[0-9]' backend/sql/fn_seguimiento_diario_engorde.sql | head -8
6:-- v15 (2026-08-08) — El «Ingreso inicial del ciclo» deja de ser invisible + marca de atribución.
54:-- v14 (2026-08-07) — Fix: un lote que terminó SIN cerrarse absorbía el ciclo SIGUIENTE del galpón.
81:-- v13 (2026-07-31) — Liquidación CONGELADA ...
99:-- v12 (2026-07-30) — Fix: la apertura tampoco puede retroceder más allá del FIN del ciclo anterior.
118:-- v11 (2026-07-29) — Fix: la apertura dejaba de ser propia y heredaba el CICLO ANTERIOR del galpón.
142:-- v10 (2026-07-29) — Fix: el consumo pasa a scope GALPÓN (inventario compartido entre lotes).
```

La versión más alta del encabezado es **v15**. No hay bloque `v16`. Confirma literalmente el texto truncado del ítem V12.5.1 («`backend/sql/fn_seguimiento_diario_engorde.sql` sigue en **v15**»).

### 2.2 La marca `para_proximo_ciclo` SÍ está, pero en el modelo **v15 (exclusión)**, no en el modelo **v16 (entrega aditiva)**

```
$ grep -n 'para_proximo_ciclo' backend/sql/fn_seguimiento_diario_engorde.sql
24:--   (B) OVERRIDE POR MARCA `para_proximo_ciclo` (columna nueva del histórico unificado, migración
25:--       20260808120000, espejo de `inventario_gestion_movimiento.para_proximo_ciclo`):
525:          (COALESCE(h.para_proximo_ciclo, FALSE)
541:          (NOT COALESCE(h.para_proximo_ciclo, FALSE)
615:      AND (rs.fecha_min IS NULL OR NOT COALESCE(h.para_proximo_ciclo, FALSE))
761:      AND (rs.fecha_min IS NULL OR NOT COALESCE(h.para_proximo_ciclo, FALSE))
790:           AND (rs.fecha_min IS NULL OR NOT COALESCE(h.para_proximo_ciclo, FALSE)))
826:           AND (rs.fecha_min IS NULL OR NOT COALESCE(h.para_proximo_ciclo, FALSE)))
```

Las líneas 615/761/790/826 son **los 4 guards de exclusión** que el tracker identifica como la causa raíz del defecto (`hist_full`, `hist_alimento`, `docs_por_fecha`, `fechas_universo`). La v16 iba precisamente a **revertirlos a v14** y sustituirlos por dos términos aditivos (`+kg_diferido` en la apertura del destino, `−kg_diferido` como salida del cedente). **Nada de eso está.**

### 2.3 No hay otra migración con otro nombre que haga lo mismo

Las únicas migraciones vivas del feature son las **v15**, ambas commiteadas en `801b14f` el 08-ago y presentes en el repo y en la BD:

| Migración | Qué hace | ¿En repo? | ¿En BD local? |
|---|---|---|---|
| `20260808120000_AlimentoPrevioEncasetMarcaCiclo` | `ADD COLUMN IF NOT EXISTS para_proximo_ciclo` en `inventario_gestion_movimiento` y en `lote_registro_historico_unificado` + el trigger espejo | ✅ | ✅ |
| `20260808130000_FnSeguimientoEngordeV15AperturaVisibleYMarcaCiclo` | instala la fn diaria **v15** (`.Fn.cs`, 1945 líneas) | ✅ | ✅ |
| `20260809120000_FnAlimentoMarcadoAtribucionEngorde` | (v16 — atribución) | ❌ | ❌ |
| `20260809120100_FnSeguimientoEngordeV16EntregaCicloSiguiente` | (v16 — entrega) | ❌ | ❌ |

Ninguna migración posterior (`20260810*` … `20260818*`) toca `fn_seguimiento_diario_engorde` ni la atribución de la marca. La única de agosto que roza el tema es `20260818010000_RecalcularSaldoAlimentoEngordePersistido`, que **recalcula saldos**, no cambia la fn.

### 2.4 La puerta de entrada está cerrada en el front, y esa mitigación sí se commiteó

```ts
// frontend/src/app/features/gestion-inventario/pages/gestion-inventario-page/gestion-inventario-page.component.ts:1083
get mostrarParaProximoCicloIngreso(): boolean {
  return false;
}
```

El comentario de ese getter (líneas 1070-1082) documenta el estado real mejor que el bloque del tracker: *«Cuatro intentos de arreglarlo terminaron revertidos porque cada guarda mudaba el defecto de lugar»*. La mitigación está en `main` **y en `main-produccion`** (`git merge-base --is-ancestor 8424557 main-produccion` → `SI`).

**Conclusión de la pregunta 2:** la funcionalidad v16 **no está** — ni en el `.sql`, ni en una migración con otro nombre, ni en el C#. Lo que está es la marca v15, con su defecto conocido, neutralizada porque no se pueden crear marcas nuevas.

---

## 3. ¿Qué dice la BD local? — Alineada al 100 %, sin rastro de la v16

Conexión leída de `backend/src/ZooSanMarino.API/appsettings.Development.json` (única fuente):
`Host=127.0.0.1;Port=5433;Database=sanmarinoapplocal`. **Todas las consultas fueron de solo lectura** (`SELECT`); no se escribió nada.

### 3.1 Cruce completo `__EFMigrationsHistory` ↔ archivos del repo

```
$ psql ... -At -c 'SELECT migration_id FROM "__EFMigrationsHistory";' | sort > db.txt
$ ls .../Migrations/*.cs | grep -v Designer | grep -v ModelSnapshot | sed -E 's#.*/##; s#\.(Fn|Seed)\.cs$##; s#\.cs$##' | sort -u > repo.txt

BD=298  REPO=298
### EN BD SIN ARCHIVO (huerfanas en el historial):
[fin]
### ARCHIVO SIN FILA EN BD (pendientes):
[fin]
### filas 20260809*:
[fin]
```

**298 = 298 · 0 huérfanas · 0 pendientes · 0 filas `20260809*`.** No se reprodujo aquí el modo de falla de `backend/sql/marcar_todas_migraciones_pendientes.sql` (marcar como aplicada una migración que nunca corrió → SIGSEGV en el arranque): **nadie tocó el historial a mano**, exactamente como afirma el tracker (`- [x] __EFMigrationsHistory NO se tocó`).

Última fila: `20260818042406_SuperAdminPorDato` — la migración más reciente del repo.

### 3.2 Los objetos de la v16 no existen en la BD

```
$ SELECT proname FROM pg_proc WHERE proname IN ('fn_alimento_marcado_atribucion','fn_alimento_base_cedente_engorde');
[fin]                                  ← ninguna de las 2 fns auxiliares de la v16 existe

$ SELECT indexname FROM pg_indexes WHERE indexname='ix_lote_hist_para_proximo_ciclo';
[fin]                                  ← el índice parcial de la v16 tampoco existe

$ SELECT indexname FROM pg_indexes WHERE tablename='lote_registro_historico_unificado';
ix_lote_hist_company_fecha
ix_lote_hist_farm_fecha
ix_lote_hist_lote_fecha
ix_lote_hist_tipo
lote_registro_historico_unificado_pkey
uq_lote_hist_origen
```

> ⚠️ **Corrección menor al tracker (dato nuevo):** la línea 1081 dice que el índice `ix_lote_hist_para_proximo_ciclo` «**NO se tocó** (otra sesión lo estaba creando)». Hoy **ese índice no existe en la BD local ni en ningún archivo del repo** (`grep -rn 'ix_lote_hist_para_proximo_ciclo' backend` → sin resultados). Es inocuo (era solo una optimización de lectura para un feature que no existe), pero la afirmación del tracker quedó falsa.

### 3.3 La fn instalada es v15, verificada por su cuerpo (no por el nombre)

```
$ SELECT ... FROM pg_get_functiondef(oid) WHERE proname='fn_seguimiento_diario_engorde';
para_proximo_ciclo=true | apertura_alimento_kg=true | v15_marca_D2=true |
lotes_ajenos=true | corte_apertura=true | lineas=744

$ -- rastros de la v16 en el cuerpo instalado: DIFERIDO | NEUTRO_ | kg_diferido | cedente
false|false|false|false
```

La fn instalada contiene el comentario `⭐ v15 (D2)` y los CTEs de v11/v12, y **ningún** token de la v16. Coincide byte a byte en concepto con `backend/sql/fn_seguimiento_diario_engorde.sql`.

### 3.4 No hay marcas vivas que puedan disparar el defecto

```
$ SELECT count(*) ... WHERE para_proximo_ciclo;
inventario_gestion_movimiento=0 | lote_registro_historico_unificado=0
```

Cero marcas. Con la puerta de entrada cerrada en el front (§2.4), el defecto §2.3b del tracker está **mitigado, no resuelto** — que es exactamente como lo describe la línea 810.

---

## 4. ¿Hay riesgo en producción?

### Límite del informe, dicho antes que nada

**No se pudo consultar producción desde esta máquina y no se intentó.** RDS está en VPC privada (`10.4.6.6`, psql da timeout), ECS Exec está deshabilitado en el servicio, y el usuario IAM no tiene `rds:DescribeDBInstances` (esto es lo que ya documentan los ítems `P.1`–`P.3` del tracker, no una medición nueva de esta sesión). **Todo lo que sigue sobre prod es inferencia desde el código y desde git, no medición.**

### 4.1 Riesgo de despliegue: **cero, por construcción**

`Database__RunMigrations=true` hace que EF aplique lo pendiente al arrancar. «Pendiente» = migración **presente en el assembly** y ausente de `__EFMigrationsHistory`. Como las dos migraciones `20260809*` **nunca estuvieron en ningún commit**, nunca estuvieron en ninguna imagen de ECR, y por lo tanto:

- EF **nunca pudo aplicarlas** en prod (no están en el `Migrations/` compilado).
- EF **nunca pudo registrarlas** en `__EFMigrationsHistory` de prod (solo escribe la fila cuando aplica).
- El próximo deploy **no las va a encontrar ni a intentar**: para EF esas migraciones no existen.

No hay aquí el escenario SIGSEGV de CLAUDE.md, que requiere lo inverso (una fila en el historial sin que su DDL haya corrido, o una migración que falla a mitad).

### 4.2 Escenario hipotético descartable

La única forma de que prod tuviera una fila `20260809*` sería un `INSERT` manual en `__EFMigrationsHistory`, cosa que nadie tuvo motivo para hacer (no había migración que "saltear"). Y aunque existiera, sería **inerte**: EF ignora las filas del historial que no corresponden a ninguna migración del assembly — no las revierte ni falla por ellas. El escenario peligroso es el contrario y aquí no se da.

### 4.3 El estado de la fn en prod es coherente

`801b14f` (v15) **ya está en `main-produccion`** (`git merge-base --is-ancestor 801b14f main-produccion` → `SI`), y la mitigación del front (`8424557`) también. O sea: prod corre —o correrá en el próximo deploy— la **v15 con la puerta de entrada cerrada**, que es el estado deseado tras el NO-GO.

Lo que sí queda pendiente de verificación al próximo deploy son las **20 migraciones que `main` tiene y `main-produccion` no** (de `20260814220000_SeedTicketDobleValidacionSeguimientos` a `20260818042406_SuperAdminPorDato`). **Ninguna de ellas toca la marca ni la fn diaria de engorde**, así que no interactúan con este hallazgo.

### 4.4 El riesgo real es documental, no operativo

El bloque del tracker titulado «**FASE 1 IMPLEMENTADA**» con **6 checkboxes en `- [x]`** —incluido `F1.6 2 migraciones EF idempotentes`— es una afirmación falsa sobre el estado del código, en el archivo que CLAUDE.md declara **«única fuente de verdad del estado del desarrollo»**. Los dos modos de falla concretos:

1. Una sesión futura lee «F1.6 ✅», nota que los archivos faltan y **los recrea** para "arreglar el repo": reintroduce un cambio que el gate rechazó con dos bloqueantes medidos (kilos escondidos y kilos duplicados con el detector ciego), y como toca `fn_seguimiento_diario_engorde` dispara el **gate multipaís** de CLAUDE.md.
2. Una sesión futura planifica encima de la v16 asumiendo que la atribución existe, y arma un feature sobre una base que no está.

El antídoto ya está escrito **dentro del mismo bloque** (la sección «Reversión (verificada)», el «VEREDICTO … NO-GO — REVERTIDA» y la nota de la línea 1011). Sólo que está **300 líneas más abajo** de los `- [x]` que lo contradicen, y quien haga una lectura parcial se queda con la afirmación equivocada.

---

## 5. Recomendación

### Qué hacer: **corregir el tracker. No crear las migraciones.**

**5.1 — NO crear las migraciones `20260809*`.** Es la recomendación principal y la más importante. Recrearlas sería revertir una decisión de gate tomada con evidencia:

- El veredicto fue **NO-GO** con **C1 = NO-GO, C2 = GO-CON-RESERVAS, juez = NO-GO**, y quien escribió el código no declaró el GO (regla G4 del propio bloque).
- Los dos bloqueantes son de **conservación de kilos**, la clase de defecto que CLAUDE.md §🛡️ eleva a invariante: liquidar el cedente esconde 3.000 kg de toda tabla diaria viva; liquidar el destino **crea** 3.000 kg con `descuadre_kg = 0,00` en ambos estados (el detector no lo ve).
- El propio bloque concluye que **el diseño era el equivocado**, no la implementación: *«la atribución es un veredicto recalculado en lectura sobre estado mutable […] el rediseño correcto es persistir la atribución como hecho»*. Recrear las migraciones sería recrear el diseño descartado.
- El alcance medido era un **no-op**: 0 movimientos en estado `DIFERIDO` sobre 1.680 marcados reales.
- Y `V15.0.2` (línea 2345) ya **descartó formalmente** todo el bloque de la marca, con checkbox `- [x]`.

**5.2 — Corregir el tracker, y hacerlo de la forma que no borra historia.** Esa edición le corresponde a la sesión dueña del bloque o al orquestador; este informe no toca `tracker_estado.md`. La corrección mínima:

- **Retitular el bloque**: `# v16 de engorde — FASE 1 IMPLEMENTADA: …` → algo como `# v16 de engorde — INTENTO REVERTIDO (NO-GO ronda 4): …`. El título es lo único que lee una sesión que escanea el archivo.
- **Desmarcar los 6 checkboxes de «Qué quedó implementado» (`F1.1`–`F1.6` + el del `.sql` del gate)**: pasarlos de `- [x]` a `- [i]`, que es el marcador que CLAUDE.md reserva para hallazgos, y prefijar cada uno con «(revertido, nunca commiteado)». Lo que describen sí ocurrió — en un working tree que ya no existe. Borrarlos perdería el registro de qué se probó y por qué falló, que es lo valioso del bloque.
- **Mover el aviso de reversión arriba**, inmediatamente bajo el título, en vez de a 200 líneas de distancia.
- **Corregir la línea 1081**: el índice `ix_lote_hist_para_proximo_ciclo` no existe hoy ni en la BD local ni en el repo (§3.2). Ninguna otra sesión lo creó.
- **Cerrar el ítem `V12.5.1`** (`- [ ]` → `- [x]`) citando este informe: la discrepancia queda explicada y no requiere trabajo de código.

**5.3 — Lo que queda realmente abierto** es el rediseño ya identificado en las líneas 1086-1093 («persistir la atribución como hecho», «arreglar los 4 guards para que respeten R1»), que necesita **su propio plan** y el **gate multipaís** de CLAUDE.md por tocar `fn_seguimiento_diario_engorde`. No es esta tarea.

### Riesgos de cada opción

| Opción | Riesgo |
|---|---|
| **Corregir solo el tracker** (recomendada) | Ninguno técnico. Queda abierto el rediseño, que ya estaba abierto y listado en la tabla de pendientes del encabezado («4 · v16 de engorde — marca `para_proximo_ciclo` · rediseño (persistir la atribución)»). |
| **Crear las migraciones** | 🔴 Alto. Reintroduce el defecto de conservación de kilos; dispara el gate multipaís; contradice `V15.0.2`; y el rediseño correcto (v14 exacta + atribución persistida) **tiraría ese trabajo**. |
| **No hacer nada** | 🟠 Medio y creciente. La afirmación falsa sigue en la fuente de verdad y la probabilidad de que una sesión futura actúe sobre ella crece con cada relectura. |

---

## Anexo — Qué NO se verificó

- **Producción**: `__EFMigrationsHistory` de RDS, la versión de la fn instalada allá y el conteo de `para_proximo_ciclo` en prod. Inaccesibles desde esta máquina (§4). Todo lo dicho sobre prod es **inferencia desde git y el código**, no medición. Si se quiere certeza, la consulta de un renglón ya está escrita en el ítem `P.1` del tracker y se puede correr por DB Studio: `SELECT migration_id FROM "__EFMigrationsHistory" WHERE migration_id LIKE '202608%' ORDER BY 1 DESC LIMIT 10;`
- **`dotnet build` / `dotnet test` / `dotnet ef`**: no se corrieron a propósito. Otra sesión de Claude Code está editando el backend en este mismo repo y pelear por el `bin/` produce MSB3027 (CLAUDE.md §🔌 Ciclo de vida del backend local). Este informe no necesitó compilar nada.
- **Clones en otras máquinas**: si aquellas migraciones sobreviven en el disco de otra máquina, no hay forma de verlo desde acá. Es irrelevante para la recomendación: aunque existiera una copia, el gate ya las rechazó.
- **Escrituras**: no se modificó ningún archivo de código, ni el tracker, ni la BD. No se hizo `git add` ni `git commit`. Único archivo creado: este informe.
