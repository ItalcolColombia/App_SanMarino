# Plan — Reconciliar el espejo `.sql` de `fn_indicadores_produccion_postura` + `uniformidad_guia` NULL

**Fecha:** 2026-08-07 · Continúa el handoff de la sesión de postura (§2.1 y §2.2)
**Bloque propio — no tocar desde otras sesiones** (hay una sesión de Tickets con trabajo abierto)

---

## 1 · Problema

### 1.1 · El espejo está desincronizado de lo desplegado (§2.1 del handoff)

`backend/sql/fn_indicadores_produccion_postura.sql` **no coincide** con la función que corre en la BD.
Le falta todo lo que agregó la migración `20260806093256_SaldoProduccionDescuentaVentasYTraslados`,
empezando por la columna de salida **`seleccion_machos`**.

Consecuencia medida en la sesión anterior: desplegar el archivo del disco deja la fn en **68 columnas
en vez de 69** ⇒ `IndicadorProduccionSemanalBdRow.SeleccionMachos` revienta en runtime. Es una bomba
de tiempo: el día que alguien haga `psql -f` de ese archivo (que es exactamente para lo que está),
tumba la columna.

Complicación: el cuerpo **desplegado** viene inflado en líneas en blanco (1.965 líneas para 457
significativas: ~3 blancos antes y después de cada línea real). **No se puede reconciliar con un
copy-paste**: hay que portar el delta semántico al archivo limpio.

> Corrección al handoff: el `\r\r\n` que reportaba era **artefacto del volcado** — psql.exe en
> Windows traduce `\n`→`\r\n` al escribir por pipe, duplicando los CR. Medido dentro de la BD
> (`length(prosrc) - length(replace(prosrc, chr(13), ''))`) el cuerpo tiene **1.964 CR y 1.964 LF**:
> CRLF perfectamente balanceado. Lo inflado son las líneas en blanco, no los retornos de carro.

### 1.2 · `uniformidad_guia` sale 0 cuando debería ser NULL (§2.2 del handoff)

`g_unif := COALESCE(g_unif, 0);` replica un `ParseDouble ⇒ 0` viejo del C#. Pero la guía genética
**no define uniformidad para edades de producción** (solo 25 de sus 98 filas la traen, todas de
levante) ⇒ la columna «Uniformidad Guía» muestra **0 en todas las semanas**, que se lee como «la guía
exige 0 %» en vez de «sin dato».

Ya está mitigado en el front (`hayGuiaUniformidad()` pinta «—»), pero el arreglo de fondo es que la fn
mande NULL.

---

## 2 · Auditoría previa (hecha antes de escribir este plan)

| Verificación | Resultado |
|---|---|
| Migraciones que tocan la fn | 11; la última y vigente es `20260806093256` |
| Constante `FnConSaldoCorregido` de esa migración vs definición **viva** (`pg_get_functiondef`, normalizada) | **0 diferencias** ⇒ lo vivo es exactamente lo que despliega la migración |
| Espejo vs viva (normalizado: sin `\r`, sin líneas vacías, espacios colapsados) | 220 líneas de diff = **exactamente los 9 deltas de `20260806093256`** + el formato de `pg_get_functiondef` (prefijo `public.`, `$function$`, `RETURNS TABLE` en una línea). **Ninguna otra divergencia oculta** |
| ¿Otro `.sql` redefine la fn? | **No.** Los otros 6 archivos de `backend/sql/` que la nombran son comentarios o scripts de verificación |
| Consumidores de `uniformidad_guia` | `IndicadorProduccionSemanalBdRow.UniformidadGuia` = `double?` · `Dec(double?)` → null-safe · `IndicadorProduccionSemanalDto.UniformidadGuia` = `decimal?` · front `uniformidadGuia?: number \| null`, `hayGuiaUniformidad()` ya trata null/undefined/0 como ausencia, `redondearFila()` deja pasar null · Excel `UnifGuia` pasa a celda vacía (hoy exporta el mismo 0 mentiroso) |
| ¿`g_unif` se usa en otro lado de la fn? | Solo en `uniformidad_guia :=` y `diferencia_uniformidad := fn_dif_pct(r_unif, g_unif)` |

**Corolario clave de §2.2:** `fn_dif_pct` ya devuelve NULL cuando `p_guia = 0`. Con `g_unif` en NULL
sigue devolviendo NULL ⇒ **`diferencia_uniformidad` no cambia**. La **única** columna que se mueve es
`uniformidad_guia`: `0 → NULL`, y solo en las filas donde hay guía pero su `uniformidad` viene vacía.

---

## 3 · Enfoque

Dos cambios, un solo archivo de SQL + una migración.

### 3.1 · Portar el delta al espejo (9 ediciones, sin cambio de comportamiento)

1. `RETURNS TABLE`: `seleccion_machos integer` tras `porcentaje_seleccion_hembras`
2. `DECLARE`: `v_cum_sel_m bigint := 0;`
3. `DECLARE`: `r_sel_m`, `r_venta_h/m`, `r_retiro_h/m`, `r_tras_out_h/m`, `r_tras_in_h/m`
4. CTE `_seg` **rama LPP**: `sel_m` + las 8 columnas `mov_*`
5. CTE `_seg` **rama lote**: ídem
6. Agregación semanal: 9 `SUM(...)` + sus 9 destinos en el `INTO`
7. `v_cum_sel_m := v_cum_sel_m + r_sel_m;`
8. `r_retiro_sem_m` / `r_retiro_ac_m` / `r_aves_m_inicio` incluyen la selección de machos
9. Decremento del saldo: descuenta ventas/retiros/salidas y suma ingresos (H y M)
10. `seleccion_machos := r_sel_m;` en la emisión de la fila

Se conservan los comentarios del espejo (la definición viva los perdió: Postgres no guarda los del
`RETURNS TABLE`) y se **agregan** los que traía la migración.

### 3.2 · `uniformidad_guia` NULL (único cambio de comportamiento)

Eliminar `g_unif := COALESCE(g_unif, 0);` dejando el comentario que explica **por qué** esta columna
se aparta del criterio `ParseDouble ⇒ 0` de sus vecinas.

**Se dejan quietos** `g_cons_*`, `g_mort_*`, `g_peso_*` y `g_retiro_ac_*`: la guía **sí** los trae en
toda la curva; cambiarlos movería números sin necesidad.

### 3.3 · Migración

`CREATE OR REPLACE` (la firma **no** cambia: `seleccion_machos` ya está desplegada) con el cuerpo del
espejo ya reconciliado. `Down()` restituye la versión previa verbatim. Designer clonado, ModelSnapshot
intacto (hay otra sesión con entidades en vuelo).

---

## 4 · Casos de prueba (gate de fn compartida, §5 del handoff)

1. **Gate multipaís fila a fila**: desplegar la fn nueva como `..._V1` en paralelo, `EXCEPT` en los dos
   sentidos sobre **todas** las empresas × 53 semanas × ambos flujos (LPP y lote).
2. **Aislar la columna culpable**: `count(*) FILTER (WHERE n.<col> IS DISTINCT FROM v.<col>)` por cada
   una de las 69 columnas. **Esperado: `uniformidad_guia` la única distinta**, y siempre `0 → NULL`.
3. **`diferencia_uniformidad` = 0 diferencias** (la predicción de `fn_dif_pct`; si difiere, el análisis
   estaba mal).
4. **Sanidad de columnas**: la fn reconciliada devuelve **69** columnas, con `seleccion_machos` en la
   posición 15.
5. `dotnet build` 0 errores · `dotnet test` verde · `yarn build` (el front toca solo el comentario).
6. Verificación por pantalla del tab Indicadores de producción: «Unif Guía» sigue en «—».

---

## 5 · Riesgos

| Riesgo | Mitigación |
|---|---|
| El espejo tenía divergencias además del delta conocido | Ya descartado por el diff normalizado del §2 (220 líneas = los 9 deltas y nada más) |
| Romper la firma al re-crear | La firma no cambia ⇒ `CREATE OR REPLACE` sin `DROP`; el gate cuenta 69 columnas |
| Que el NULL rompa un consumidor | Cadena auditada punta a punta: toda nullable. El front ya pinta «—» |
| Pisar trabajo de la sesión de Tickets | Bloque propio al final del tracker; `git add` archivo por archivo; ModelSnapshot sin tocar |
