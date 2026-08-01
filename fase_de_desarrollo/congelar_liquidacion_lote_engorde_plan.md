# Plan — Congelar la liquidación de un lote de pollo engorde

**Fecha:** 2026-07-30 · **Módulo:** pollo engorde (`lote_ave_engorde`) · **Empresas afectadas:** ItalcolEcuador (liquida hoy) e ItalcolPanama (preventivo) · **Transversal, sin flag por empresa** (ver §2.4)

---

## 1. Qué se pide y por qué

> «Que un lote liquidado no se pueda tocar y guarde una copia de lo liquidado, así se puede mantener estable;
> y lo liquidado valida sobre la tabla que congela la liquidación; y si se abre se borra esa copia liquidada y
> pasa a liquidar otra vez, actualiza la tabla congelada donde me guarda la liquidación, y esa sería la que
> mostraría sobre el tiempo cada vez que consulte ese módulo.»

**El problema es real y ya pegó.** La tabla diaria NO es una foto: `fn_seguimiento_diario_engorde` la recalcula
en cada request. El 28-jul, al corregir la fórmula (v9 → v12), **corridas cerradas hacía meses cambiaron solas**
sin que nadie tocara un dato. Costos ya las había dado por cuadradas.

**Hoy liquidar no persiste ningún número.** `LoteAveEngordeService.CerrarLoteAsync` (`:519-563`) escribe
únicamente `estado_operativo_lote='Cerrado'`, `liquidado_at`, `liquidado_por_user_id` y la merma. Todo lo demás
—tabla diaria, resumen, indicadores, 3 reportes— se recalcula en vivo. «Liquidado» es un flag operativo, no un
cierre contable.

Peor: el flag **cambia la aritmética**. En `fn_seguimiento_diario_engorde.sql:472-481`, con el lote `'cerrado'`
la fn fuerza `aves_iniciales = bajas + ventas` para que el saldo cierre en 0. Es decir, **el acto de liquidar ya
re-pinta el histórico**. La copia debe tomarse DESPUÉS de aplicar el estado, o la foto no coincidirá con lo que
el usuario vio al liquidar.

---

## 2. Decisiones de arquitectura

### 2.1 Dónde se corta: en la FUNCIÓN SQL, no en C#

El endpoint `GET /api/SeguimientoAvesEngordeEcuador/por-lote/{id}/tabla-diaria` es un pasamanos:
`SqlQueryRaw<SeguimientoDiarioTablaFilaDto>("SELECT * FROM fn_seguimiento_diario_engorde({0}::int)")`
(`SeguimientoAvesEngordeEcuadorService.Consultas.cs:170`). El backend no calcula nada.

Y hay **cuatro consumidores más** que entran por `CROSS JOIN LATERAL` a la misma fn:

| Consumidor | Punto de entrada |
|---|---|
| Reporte Diario de Costos Engorde | `backend/sql/fn_reporte_diario_costos_engorde.sql:117` |
| Informe Semanal Pollo Engorde (Panamá) | `backend/sql/fn_informe_semanal_pollo_engorde.sql:138` |
| Cuadre de alimento engorde | `backend/sql/fn_cuadre_alimento_engorde.sql:84` |
| `SaldoAlimentoEngordeAplicador` (recálculo del saldo persistido) | `SaldoAlimentoEngordeAplicador.cs:45-56` |

**Decisión: el `if` "si está liquidado, leé la copia" vive dentro de `fn_seguimiento_diario_engorde`.**
Con un solo cambio, los cinco caminos quedan congelados y el front no se toca.

*Contra la alternativa (cachear el DTO en C#):* congelaría la pantalla y dejaría el Reporte de Costos y el
Informe Semanal mostrando OTROS números para el mismo lote liquidado — exactamente la incoherencia que motivó
el pedido, movida de lugar.

### 2.2 La fn pasa de `LANGUAGE sql` a `LANGUAGE plpgsql` (v13)

Hoy es `LANGUAGE sql STABLE` (`fn_seguimiento_diario_engorde.sql:186`): un único `WITH … SELECT`, sin lugar
donde poner un `IF`. Convertirla a plpgsql permite envolver **el cuerpo actual sin tocar una sola línea**:

```sql
CREATE OR REPLACE FUNCTION fn_seguimiento_diario_engorde(p_lote_id INT)
RETURNS TABLE ( … las mismas 47 columnas, mismo orden, mismos tipos … )
LANGUAGE plpgsql STABLE AS $$
DECLARE v_liq BIGINT;
BEGIN
    SELECT c.id INTO v_liq
      FROM liquidacion_lote_engorde_congelada c
     WHERE c.lote_ave_engorde_id = p_lote_id AND c.anulada_at IS NULL;

    IF v_liq IS NOT NULL THEN
        RETURN QUERY
        SELECT f.seg_id, f.fecha, … , f.created_by_user_id
          FROM liquidacion_lote_engorde_congelada_fila f
         WHERE f.liquidacion_id = v_liq
         ORDER BY f.orden;
        RETURN;
    END IF;

    RETURN QUERY
    <CUERPO ACTUAL v12 VERBATIM>;
END $$;
```

*Costo:* plpgsql pierde la posibilidad de inlining. En la práctica es ~0: el cuerpo tiene CTEs y window
functions, así que Postgres **hoy tampoco la inlinea**. Igual se valida con `EXPLAIN ANALYZE` del Reporte de
Costos (§10), que es el que la llama por LATERAL sobre muchos lotes.

*Contra la alternativa (`UNION ALL` + `WHERE NOT EXISTS` manteniendo `LANGUAGE sql`):* obliga a reestructurar el
`SELECT` final de 340 líneas y a confiar en que el planner pode el subárbol por One-Time Filter — más frágil y
mucho más difícil de revisar que un `IF … RETURN`.

### 2.3 La señal de gate sigue siendo `estado_operativo_lote='Cerrado'`; la copia es un derivado garantizado

El borrador anterior proponía «la copia ES la señal». **Se descarta.** Motivos:

1. Nueve gates ya comparan `"Cerrado"` con `OrdinalIgnoreCase` (los 6 CRUD de seguimiento ×2 services + los 3
   flujos de `MigracionService`). Cambiarles el criterio es riesgo de comportamiento a cambio de nada.
2. La propia fn ramifica por `estado_operativo_lote = 'cerrado'` para la aritmética. Dos nociones distintas de
   «liquidado» dentro del mismo módulo es deuda garantizada.
3. Un `EXISTS` contra otra tabla en cada gate de escritura es una query extra por operación.

**El invariante `Cerrado ⟺ existe copia vigente` lo garantiza la BD**, no la disciplina:
- `UNIQUE (lote_ave_engorde_id) WHERE anulada_at IS NULL` → imposible dos copias vigentes.
- Trigger `trg_lote_ave_engorde_anula_congelada` `AFTER UPDATE OF estado_operativo_lote`: si el estado deja de
  ser `'Cerrado'` y hay copia vigente, la anula (idempotente, `WHERE anulada_at IS NULL`). Es la red contra
  cualquier `UPDATE` crudo. Mismo patrón que los triggers `_del`/`_cancel` de la Fase 9 («la BD garantiza el
  invariante»).
- El sentido inverso (`Cerrado` sin copia) no se puede forzar por trigger porque la copia se escribe un
  instante después, en la misma transacción. Se cubre con la transacción explícita de §5.1 y con una consulta
  de salud en `verificar_congelado_engorde.sql` que debe dar **0 filas**.

⚠️ **`liquidado_at` NO es señal de nada.** `AbrirLoteAsync` (`:634-664`) no la limpia, así que todo lote
reabierto queda `Abierto` con `liquidado_at` no nulo. No usarla nunca como sinónimo de «liquidado» (hoy la vista
`vw_liquidacion_ecuador_pollo_engorde.sql:261` la expone como `fecha_liquidacion` — falso positivo conocido).

### 2.4 Sin flag por empresa

CLAUDE.md exige flag tipado en `companies` cuando **una** empresa se comporta distinto. Acá no: es una
corrección de integridad para todo el módulo de engorde. Un flag crearía dos semánticas simultáneas de
«liquidado» y volvería inconsistentes los reportes multi-empresa. **El rollback es el `Down()` de la migración
+ re-deploy de la fn v12**, no un switch en caliente.

---

## 3. Alcance: qué se congela y qué NO

### 3.1 SE CONGELA

| # | Qué | Cómo |
|---|---|---|
| 1 | **La tabla diaria completa** (47 columnas × N días) | Copia relacional + rama congelada de la fn |
| 2 | **Reporte Diario de Costos Engorde** | Gratis: LATERAL a la fn |
| 3 | **Informe Semanal Pollo Engorde (Panamá)** | Gratis: LATERAL a la fn |
| 4 | **Cuadre de alimento engorde** | Gratis: LATERAL a la fn |
| 5 | **`saldo_alimento_kg` persistido** | Gratis y *auto-reparable*: `SaldoAlimentoEngordeAplicador` lee de la fn, así que tras el freeze **reescribe el valor congelado**. Si algo desviara la columna, el siguiente movimiento del galpón la devuelve a la copia. No requiere cambio de código |
| 6 | **El resumen de liquidación** (13 campos del `LiquidacionLoteEngordeResumenDto`) | Columnas tipadas en la cabecera; leen la copia los **DOS** services (`…EcuadorService.Consultas.cs:77` y `…EngordeService.Consultas.cs:57`) |

### 3.2 NO se congela (y por lo tanto SIGUE CAMBIANDO — dicho explícitamente)

| Qué | Por qué queda fuera | Qué lo estabiliza igual (parcial) |
|---|---|---|
| **Pestañas Indicadores y Gráficas de Ecuador** — se calculan **en el navegador** (`indicadores-diarios-engorde-compute.service.ts:63-187`) desde los seguimientos crudos + la guía genética viva | Congelarlas exige materializar otra forma de datos y mover el cómputo al backend: es un módulo aparte, del tamaño de esta feature | Los seguimientos crudos y el maestro del lote quedan inmutables por los gates de §6. **Lo único que las mueve es que alguien corrija la guía genética** (ya pasó: K345 guía 2023). Mitigación: la cabecera guarda `raza`, `ano_tabla_genetica` y `guia_header_id` en `metadata` para poder auditarlo |
| **Liquidación Técnica Ecuador** — segundo motor (`fn_indicadores_pollo_engorde`, vista `vw_liquidacion_ecuador_pollo_engorde`, comentada literalmente como «Tiempo real») y tercer camino en C# (`LiquidacionTecnicaEcuadorService.cs:21-27`) | Aritmética propia, parámetros de ajuste del caller y dependencia de la guía viva | Nada. Sigue vivo. Documentado |
| **Vista Power BI `vw_seguimiento_pollo_engorde`** | Es una **reimplementación set-based de la v7** (así lo dice su cabecera), ya divergente de la v12 desde antes de esta feature, con nombres y derivaciones propios. Meterle la copia es reescribirla | Nada. La divergencia es preexistente, no la introduce el freeze. Queda como tarea aparte |
| **Pestaña R. Reproductora** | Lee `lote_reproductora_ave_engorde` + su seguimiento, tablas de otro agregado | Los gates B6 y B7 de §6 las vuelven inmutables con el lote cerrado |
| **Stock de inventario mostrado en el modal** | Es stock ACTUAL de bodega, por definición vivo | Se rotula en el front (§7): «Stock actual» vs «Saldo de alimento liquidado» |
| **`fn_reporte_indicadores_panama`** | NO VERIFICADO: no encontré su definición en `backend/sql/` ni en una migración con ese nombre; no sé si pasa por la fn. Ver §12 | Sus 6 insumos quedan bloqueados (B9) y el seguimiento inmutable |

---

## 4. Estructura de datos — MIXTA: cabecera relacional + detalle relacional + `metadata jsonb`

### 4.1 `liquidacion_lote_engorde_congelada` (cabecera, 1 fila vigente por lote)

```
id                        BIGSERIAL PK
lote_ave_engorde_id       INT       NOT NULL  FK -> lote_ave_engorde  ON DELETE CASCADE
company_id                INT       NOT NULL
granja_id                 INT       NOT NULL          -- filtrar/auditar sin join
liquidado_at              TIMESTAMPTZ NOT NULL        -- copiado del lote (el que eligió el usuario)
liquidado_por_user_id     TEXT      NOT NULL
congelada_at              TIMESTAMPTZ NOT NULL DEFAULT now()
origen                    TEXT      NOT NULL          -- 'cierre' | 'backfill' | 'recongelado' | 'correccion'
fn_version                TEXT      NOT NULL          -- 'v13' (la que produjo la foto)
filas                     INT       NOT NULL          -- cantidad de días copiados
checksum                  TEXT      NOT NULL          -- md5(string_agg(f::text,'|' ORDER BY orden))
-- resumen de liquidación (los 13 campos del DTO, tipados)
lote_nombre               TEXT      NOT NULL
estado_operativo_lote     TEXT      NOT NULL
hembras_inicio            INT       NULL
machos_inicio             INT       NULL
mixtas_inicio             INT       NULL
total_aves_inicio         INT       NULL      -- NULL en copias de backfill (ver §8)
ventas_total_hembras      INT       NULL
ventas_total_machos       INT       NULL
ventas_total_mixtas       INT       NULL
aves_vivas_actuales       INT       NULL
movimientos_venta_count   INT       NULL
saldo_alimento_kg         NUMERIC(18,3) NULL
merma_unidades            INT       NULL
merma_kilos               NUMERIC(18,3) NULL
-- contexto variable
metadata                  JSONB     NULL              -- raza, ano_tabla_genetica, guia_header_id,
                                                      -- dias_alimento_previo_encaset, insumos Panamá, etc.
-- anulación (reapertura)
anulada_at                TIMESTAMPTZ NULL
anulada_por_user_id       TEXT      NULL
anulada_motivo            TEXT      NULL
created_at                TIMESTAMPTZ NOT NULL
created_by_user_id        TEXT      NULL
```

**Índices**
- `ux_liquidacion_lote_engorde_congelada_vigente` **UNIQUE** `(lote_ave_engorde_id) WHERE anulada_at IS NULL`
  — el invariante «una sola copia vigente», a nivel de base. (El precedente Panamá ya usa un único por lote:
  `ux_liquidacion_lote_engorde_panama_lote`; `liquidacion_cierre_lote_levante` lo OMITIÓ y por eso su upsert
  puede duplicar — ese defecto no se copia.)
- `ix_liquidacion_lote_engorde_congelada_lote` `(lote_ave_engorde_id, congelada_at DESC)` — historial.
- `ix_liquidacion_lote_engorde_congelada_company` `(company_id, congelada_at DESC)`.

### 4.2 `liquidacion_lote_engorde_congelada_fila` (detalle, N filas por copia)

```
id               BIGSERIAL PK
liquidacion_id   BIGINT NOT NULL FK -> liquidacion_lote_engorde_congelada(id) ON DELETE CASCADE
orden            INT    NOT NULL          -- row_number() al congelar: reproduce el ORDER BY exacto de la fn
<las 47 columnas del RETURNS TABLE, MISMOS nombres y MISMOS tipos>
```

Las 47 se copian **literales** de `fn_seguimiento_diario_engorde.sql:129-186`: `seg_id BIGINT`, `fecha DATE`,
`edad_dia INT`, `semana SMALLINT`, …, `metadata JSONB`, `items_adicionales JSONB`,
`historico_consumo_alimento JSONB`, `created_by_user_id TEXT`.

**Regla dura: `DOUBLE PRECISION` se guarda como `DOUBLE PRECISION`.** Nada de convertirlo a `numeric` "para que
quede prolijo": cambiaría el redondeo y la copia dejaría de ser la foto (refactor ≠ cambio de comportamiento).

**Índice:** `ix_liquidacion_lote_engorde_congelada_fila_liq (liquidacion_id, orden)`.

**No se mapea en EF.** La tabla de detalle se crea por SQL en la migración y **no** tiene entidad ni `DbSet`: la
única lectura es dentro de la fn, y C# sigue consumiendo la tabla diaria por `SqlQueryRaw` sobre la fn. Mapearla
significaría mantener el mismo esquema de 47 campos en un **tercer** lugar (ya está en la fn y en
`SeguimientoDiarioTablaFilaDto`). EF ignora las tablas que no conoce, así que un `migrations add` posterior no
la toca. Entidad EF **solo para la cabecera** (`LiquidacionLoteEngordeCongelada`), que es lo que C# necesita
para el resumen y la auditoría.

### 4.3 Por qué relacional y no `jsonb` para el detalle

El borrador anterior proponía `tabla_diaria jsonb`. Se descarta por la razón de §2.1: **el switch de lectura
tiene que estar dentro de la fn**, y desde SQL:

- Relacional → `SELECT f.seg_id, f.fecha, … FROM …_fila f WHERE f.liquidacion_id = v_liq ORDER BY f.orden`.
  Tipado por el motor; si alguien cambia un tipo, la migración falla en el deploy, no en producción.
- `jsonb` → `jsonb_to_recordset(c.tabla_diaria) AS x(seg_id bigint, fecha date, … 47 columnas …)`, es decir
  **re-declarar la lista de 47 columnas dentro de la fn**: un cuarto lugar donde mantener el esquema, y ante
  cualquier deriva de nombre o tipo devuelve `NULL` en silencio en vez de fallar.

El argumento a favor de jsonb era «inmunidad al cambio de esquema». No aplica: si la fn gana una columna, la
tabla de detalle la gana con `ADD COLUMN IF NOT EXISTS` nullable y las copias viejas quedan en `NULL` —
exactamente lo que jsonb daría con una clave ausente, pero explícito y verificable.

Además, relacional permite el diff fila a fila copia-vs-vivo en SQL plano (§10), que es la herramienta de
auditoría de toda la feature.

**El `jsonb` se conserva donde sí corresponde:** `metadata` en la cabecera, para el contexto variable
(raza/guía/insumos). Y se respeta el anti-patrón de jul-2026
(`20260729224401_ReporteCostosAlimentoDesdeFuentesReales`): **ningún `jsonb` decide un total.** Los totales
salen de la copia relacional, escrita con un `INSERT … SELECT` de la fn **completa** —no de un subconjunto, que
fue justamente el defecto de `historico_consumo_alimento.saldo_final`.

---

## 5. Ciclo de vida: cuándo se escribe, cuándo se borra

### 5.1 Escritura — dentro de la transacción del cierre

`CerrarLoteAsync` hoy no abre transacción explícita. Pasa a:

```
using var tx = await _ctx.Database.BeginTransactionAsync();
   … validaciones y gates actuales (sin cambios) …
   ent.EstadoOperativoLote = "Cerrado"; ent.LiquidadoAt = …; ent.LiquidadoPorUserId = …; merma …
   await AvanzarCodigoErpGranjaSiCicloCerradoAsync(ent);   // Panamá: +1 al código ERP
   await _ctx.SaveChangesAsync();                          // el estado 'Cerrado' ya es visible en la tx
   await _ctx.Database.ExecuteSqlInterpolatedAsync(
       $"SELECT fn_congelar_liquidacion_engorde({loteId}::int, {userId}::text, 'cierre'::text)");
   … upsert de los 13 campos del resumen en la cabecera …
await tx.CommitAsync();
```

**El orden importa doble:**
1. El `SaveChanges` va primero porque la fn ramifica por `estado_operativo_lote='cerrado'` para forzar el
   cierre en 0. Congelar antes guardaría una foto distinta a la que el usuario aprobó.
2. Dentro de `fn_congelar_liquidacion_engorde`, el detalle debe leerse con la fn **todavía en vivo**: apenas
   exista la cabecera vigente, la fn empieza a devolver la copia y se auto-referenciaría vacía. Se resuelve con
   CTEs modificantes en un único statement (todos ven el mismo snapshot):

```sql
CREATE FUNCTION fn_congelar_liquidacion_engorde(p_lote_id INT, p_user TEXT, p_origen TEXT)
RETURNS BIGINT LANGUAGE plpgsql AS $$ …
  WITH filas AS (
      SELECT row_number() OVER () AS orden, f.*
        FROM fn_seguimiento_diario_engorde(p_lote_id) f   -- aún no hay cabecera: calcula en vivo
  ), cab AS (
      INSERT INTO liquidacion_lote_engorde_congelada (…, filas, checksum, …)
      SELECT …, (SELECT count(*) FROM filas),
             (SELECT md5(string_agg(x::text,'|' ORDER BY x.orden)) FROM filas x), …
      RETURNING id
  )
  INSERT INTO liquidacion_lote_engorde_congelada_fila (liquidacion_id, orden, …47…)
  SELECT (SELECT id FROM cab), fl.orden, fl.* FROM filas fl;
…$$;
```

**Si el congelado falla, la liquidación falla entera** (rollback). Sin copia no hay liquidación. Esto corrige el
defecto del precedente `LiquidacionCierreLoteLevante`, cuyo snapshot lo dispara el FRONT en modo best-effort
(`seguimiento-lote-levante-list.component.ts:1002-1003`, «// Guardar liquidación de cierre (best-effort)»),
fuera de la transacción del backend.

### 5.2 Borrado — anulación en la transacción de la reapertura

`AbrirLoteAsync` pasa a:

```
using var tx = …;
   … validaciones actuales …
   await _ctx.Database.ExecuteSqlInterpolatedAsync($@"
       UPDATE liquidacion_lote_engorde_congelada
          SET anulada_at = now(), anulada_por_user_id = {userId}, anulada_motivo = {motivo}
        WHERE lote_ave_engorde_id = {loteId} AND anulada_at IS NULL");
   ent.EstadoOperativoLote = "Abierto"; ent.ReabiertoAt = …; ent.MotivoReapertura = …;
   await _ctx.SaveChangesAsync();
await tx.CommitAsync();
```

**Se anula, no se hace `DELETE`.** «Borrar» en el sentido del pedido = deja de existir para el módulo (la fn
filtra `anulada_at IS NULL`, así que el lote vuelve a calcularse en vivo desde el instante de la reapertura).
Pero la fila queda: es el rastro de qué se había liquidado y con qué números, y el precedente del repo es
exactamente ese (`LotePosturaLevanteService.cs:516-521`, «Soft delete en vez de DELETE … queda el rastro de que
este existió»; `LoteRegistroHistoricoUnificado.Anulado`). Costo: ~50 filas de detalle por liquidación; con 23
lotes, ~1.400 filas.

Al re-liquidar se genera una copia **nueva** (`origen='cierre'`), y el `UNIQUE` parcial garantiza que solo una
esté vigente. Queda el historial completo de versiones por lote.

### 5.3 Cómo se garantiza que no queden copias huérfanas

| Escenario | Defensa |
|---|---|
| Dos cierres concurrentes / retry del front | `UNIQUE (lote_ave_engorde_id) WHERE anulada_at IS NULL` → el segundo falla, la tx revierte |
| Reapertura por API | Anulación en la misma transacción (§5.2) |
| Reapertura por `UPDATE` crudo en BD | Trigger `trg_lote_ave_engorde_anula_congelada` (§2.3) |
| Borrado del lote | FK `ON DELETE CASCADE` (y el hard delete queda bloqueado, B3) |
| Fila de detalle sin cabecera | FK del detalle `ON DELETE CASCADE` |
| `Cerrado` sin copia | Consulta de salud en `verificar_congelado_engorde.sql`, debe dar 0 |
| Copia vigente con lote `Abierto` | Misma consulta de salud, debe dar 0 |

---

## 6. Caminos de escritura: LISTA CERRADA de lo que se bloquea y lo que no

Gate único, con el mensaje canónico ya existente para no inventar una tercera convención:

```csharp
if (string.Equals(lote.EstadoOperativoLote, "Cerrado", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("El lote está liquidado. Reabra el lote para modificarlo.");
```

La decisión de si una operación puede correr sale de un cálculo **puro y testeable**:
`Application/Calculos/LiquidacionCongeladaGateCalculos.cs` →
`ValidarEscritura(string? estadoOperativo, OperacionLoteEngorde op)`, con la lista cerrada de operaciones y su
mensaje. El service resuelve el estado y delega.

### A. Ya bloqueados hoy — se mantienen tal cual (9 caminos, cero cambios)

1-6. `SeguimientoAvesEngordeService.Crud.cs:79 / :255 / :455` y
`SeguimientoAvesEngordeEcuadorService.Crud.cs:27 / :190 / :392` (Create/Update/Delete ×2 services).
7-9. `MigracionService`: seguimiento engorde, venta engorde y seguimiento reproductora
(`MigracionService.SeguimientoEngorde.cs:27, :43, :67`).

### B. Gates NUEVOS (lista cerrada, 10 puntos)

| # | Dónde | Por qué |
|---|---|---|
| **B1** | `LoteAveEngordeService.UpdateAsync` (`:352`) | El más destructivo: cambiar `AvesEncasetadas` o `FechaEncaset` invalida toda la liquidación |
| **B2** | `LoteAveEngordeService.DeleteAsync` (`:483`) | Soft delete de un lote liquidado |
| **B3** | `LoteAveEngordeService.HardDeleteAsync` (`:502`) | Arrastra por FK todo el histórico |
| **B4** | `SeguimientoAvesEngordeService.AplicarCuadrarSaldosAsync` (`CuadrarSaldos.cs:307`) | Uno de los 2 huecos que escriben `seguimiento_diario_aves_engorde` esquivando el CRUD; además anula/inserta movimientos del histórico. Ya recibe `loteId`: basta agregar `EstadoOperativoLote` a la proyección de `:318`. **El preview NO se bloquea, solo el "aplicar"** |
| **B5** | `SeguimientoAvesEngordeService.BackfillMetadataAsync` (`Metadata.cs:110`) | Segundo hueco directo; con `onlyIfMissing=false` pisa la metadata completa. La query de `:118` ya trae el lote |
| **B6** | `SeguimientoDiarioLoteReproductoraService`: `CreateAsync:165`, `UpdateAsync:278`, `ConfirmarAsync:410`, `DeleteAsync:474` | **Corta el camino más invisible**: el trigger `trg_cruce_reproductora_engorde` (`fn_cruce_reproductora_a_engorde.sql:219`) hace `DELETE`+`INSERT` de los días 1-7 del seguimiento del lote de engorde **desde la BD**, donde ningún gate de C# lo alcanza. El join al lote padre ya está armado en cada método |
| **B7** | `LoteReproductoraAveEngordeService.EnsureLoteAveEngordeExistsAsync` (`:629`) | Un solo helper compartido por los 5 métodos (Create/CreateBulk/Update/Reabrir/Delete). Cambian las aves asignadas, que alimentan el máximo vendible del lote |
| **B8** | `MovimientoPolloEngordeService`: `CreateAsync:17`, `UpdateAsync:392`, `CancelAsync:508`, `EliminarAsync:525`, `CompleteAsync:706`, `CompletarBatchAsync:769`, `VentaGranja.cs:18`, y `MovimientoPolloEngordePanamaService.cs:29` | Helper `ValidarLoteNoLiquidadoAsync(loteIds)` en el archivo ancla del partial. `CompleteAsync` y `EliminarAsync` **mutan `lote_ave_engorde`** (`:726-731`, `:692-695`) y `ValidarDisponibilidadParaCrearAsync:129-131` incrementa `AvesSobrante` en silencio. **Con bypass explícito** (parámetro `bool omitirGateLiquidado`) para que `CorreccionAvesDisponiblesEngordeService:435` siga pudiendo llamar `CompleteAsync` |
| **B9** | `ReporteIndicadorPanamaService.GuardarLiquidacionAsync` (`:32`) | Los 6 insumos se digitan **antes** de cerrar (el modal llama `/liquidar` y después `/cerrar`), así que bloquear con `Cerrado` no rompe el flujo y evita que se editen post-liquidación. **Además, en el mismo cambio, se le agrega el gate de empresa/alcance fail-closed que hoy NO tiene** (§9) |
| **B10** | `PuentePanamaService.Sincronizar.cs` — rama «YaExiste» (`:509-514`) | Hoy no corta: sigue a `:572`/`:581` y entra por `_loteReproService`/`_seguimientoReproService`, que no tienen gate. Si el lote destino está `Cerrado`, marcar `prev.Estado = "Liquidado"`, contar como omitido y **retornar antes de `:572`** |

### C. NO se bloquean (lista cerrada, con la razón)

| Camino | Por qué NO |
|---|---|
| **Los 14 métodos de `InventarioGestionService`** | La bodega es **por galpón** y conviven 1-4 lotes: bloquear un ingreso porque un lote viejo del galpón está liquidado congelaría la operación del lote vivo. **Y no hace falta:** el lote liquidado lee de la copia, así que ningún movimiento posterior lo mueve. La columna `saldo_alimento_kg` tampoco se desvía, porque `SaldoAlimentoEngordeAplicador` la recalcula **desde la fn**, que ahora devuelve la copia → el recálculo se vuelve auto-reparador |
| **`SaldoAlimentoEngordeAplicador` y `RetiroAvesEngordeAplicador`** | Son `internal static` sin DI, llamados tanto por caminos bloqueados como abiertos, y `RecalcularPorUbicacionAsync` recalcula el galpón entero: un gate ahí abortaría el refresco de los lotes vecinos vivos. Además `SincronizarAsync` se auto-invoca para **revertir** filas huérfanas; bloquearlo impediría devolver aves |
| **`CorreccionAvesDisponiblesEngordeService`** | Existe justamente para reparar liquidados (su tipo de descuadre se llama `FantasmaCerrado`, `:273`). **Pero**: si aplica correcciones (no `dryRun`) sobre un lote con copia vigente, **re-congela** en la misma transacción (`origen='correccion'`, la copia anterior queda anulada). Así la copia nunca queda mintiendo. Ojo: este service muta `HembrasL/MachosL` por su cuenta (`:456-458`), sin pasar por el aplicador |
| **`LoteAveEngordeService.ActualizarMermaAsync`** (`:605`) | Costos digita la merma después de liquidar por diseño (`LoteAveEngorde.cs:63`: «NO afectan el registro diario»). Se permite, y **actualiza los 2 campos de merma de la cabecera congelada** en la misma tx — no toca el detalle |
| **`LoteAveEngordeService.AbrirLoteAsync`** | Es el mecanismo de desbloqueo |
| **`RegistrarPesoFacturaAsync` / `OrganizarPesoAsync`** (peso báscula diferido) | El peso llega días después de la venta, a veces después del cierre. Bloquearlo rompería el flujo de Panamá/Ecuador. **Consecuencia aceptada y documentada:** el peso que llega después NO entra en la copia. Regla operativa: liquidar cuando el peso ya está; si llega después, reabrir y re-liquidar |
| **`AuditarVentasEngordeAsync` / `CorregirVentasCompletadasAsync`** | Herramientas de reparación, mismo criterio que la corrección de aves |
| **Todas las lecturas** | Obvio |

---

## 7. Conmutación de la lectura

| Capa | Dónde va el `if` | Efecto |
|---|---|---|
| **BD** | `fn_seguimiento_diario_engorde` v13 (§2.2) | Tabla diaria + Reporte de Costos + Informe Semanal + Cuadre + saldo persistido |
| **C#** | `GetLiquidacionResumenAsync` en **los dos** services (`…EcuadorService.Consultas.cs:77` y `…EngordeService.Consultas.cs:57`): si hay cabecera vigente **con resumen no nulo**, proyectar el DTO desde ella y salir | Modal de liquidación y cualquier consumidor del resumen |
| **C#** | `SeguimientoAvesEngordeService.GetByLoteAsync` (`:27`) llama `RecalcularSaldoAlimentoPorLoteAsync` **dentro de un GET** | Se deja como está: al leer de la fn ya congelada, el recálculo escribe el mismo valor y el `IS DISTINCT FROM` lo convierte en 0 filas. Documentado, no se toca |
| **Front** | **Cero cambios funcionales**: el DTO es el mismo, la interfaz TS es espejo y el componente no construye nada (`tabs-principal-engorde.component.ts:103`) | — |

Cambios de front, solo cosméticos y de honestidad (§3.2):
1. Badge en la cabecera del listado y en el modal: **«Liquidado · datos congelados el dd/mm/aaaa (v13)»**.
2. En el modal, rotular **«Stock actual de bodega»** (vivo) vs **«Saldo de alimento liquidado»** (copia) — hoy
   el template muestra el stock vivo como cifra principal (`:300`) y el resumen como referencia (`:309`).
3. Nota al pie en Indicadores/Gráficas cuando el lote está liquidado: **«Indicadores calculados en vivo contra
   la guía genética vigente»**.
4. Cualquier componente nuevo lleva `changeDetection: ChangeDetectionStrategy.Eager` explícito.

---

## 8. Lotes ya liquidados: backfill

**Se congelan por migración**, no se espera a que alguien los reliquide: si no, siguen recalculando y pueden
volver a moverse en el próximo fix de la fn (que es el escenario que motivó todo).

- Migración EF **idempotente**, en este orden dentro del mismo archivo:
  1. `CREATE TABLE IF NOT EXISTS` de las dos tablas + índices + trigger.
  2. `CREATE OR REPLACE` de la fn v13 (con la rama congelada) + `fn_congelar_…`, `fn_anular_…`,
     `fn_recongelar_…`.
  3. **Backfill**: `SELECT fn_congelar_liquidacion_engorde(l.lote_ave_engorde_id, 'backfill', 'backfill')`
     para todo lote con `estado_operativo_lote ILIKE 'cerrado'`, `deleted_at IS NULL` y
     `NOT EXISTS (copia vigente)`. Al no haber copia, la fn calcula en vivo y la foto queda con los valores
     **actuales post-v12**, que son los mejores disponibles hoy.
- **Los 13 campos del resumen quedan en `NULL` en las copias de backfill** (`origen='backfill'`) y
  `GetLiquidacionResumenAsync` cae a vivo cuando `total_aves_inicio IS NULL`. Motivo: replicar en SQL la
  aritmética de `LiquidacionEngordeCalculos` sería una segunda implementación del mismo cálculo, que es
  exactamente la deuda que el repo ya paga cara. El valor del backfill está en congelar la **tabla diaria**,
  que es lo que se movió el 28-jul. La primera reliquidación llena el resumen.
- **Los lotes reabiertos NO se congelan.** El filtro es `estado_operativo_lote='Cerrado'`, nunca
  `liquidado_at IS NOT NULL` (§2.3).
- Recuento esperado (**NO VERIFICADO en esta sesión**, viene del borrador previo): Ecuador 23 con `liquidado_at`,
  de los cuales 20 `Cerrado` → 20 copias; 3 reabiertos → 0 copias; Panamá 0. **Verificar contra la BD antes de
  correr la migración** y dejar el conteo real en el tracker.
- Escape hatch: `fn_recongelar_liquidacion_engorde(lote, user)` + endpoint admin
  `POST /api/LoteAveEngorde/{id}/recongelar` (rol admin, auditado). Regenera la copia sin reabrir el lote, para
  el caso «se descubrió un bug en la fórmula después de congelar». Anula la copia previa y crea una nueva con
  `origen='recongelado'`.

---

## 9. Riesgos

| Riesgo | Impacto | Mitigación |
|---|---|---|
| **La copia está y los datos cambian igual** por un camino no bloqueado (peso diferido, corrección de aves, guía genética) | La copia deja de reflejar la realidad | Peso diferido: regla operativa + reabrir/reliquidar. Corrección de aves: re-congela automáticamente. Guía: fuera de alcance, pero la cabecera guarda `raza`/`ano_tabla_genetica`/`guia_header_id` en `metadata` para auditarlo. Y `verificar_congelado_engorde.sql` diffea copia vs vivo cuando se quiera |
| **Reportes que agregan varios lotes** | Reporte de Costos, Informe Semanal y Cuadre **quedan congelados** para los lotes liquidados y vivos para el resto → una misma corrida puede mezclar filas congeladas y vivas | Es el comportamiento correcto y deseado. Se documenta en el instructivo de operación. La Liquidación Técnica Ecuador y la vista Power BI **NO** se congelan (§3.2) |
| **Congelar un número equivocado** (bug en v12 descubierto después) | 20 lotes quedan mal para siempre | `fn_recongelar_liquidacion_engorde` + endpoint admin (§8) |
| **Regresión de performance** por plpgsql (pérdida de inlining) | Reporte de Costos e Informe Semanal llaman la fn por LATERAL sobre N lotes | `EXPLAIN ANALYZE` antes/después del Reporte de Costos de un mes completo, en Ecuador y Panamá. Umbral: no más de +15% |
| **El gate traba a un usuario legítimo** | Soporte | Smoke doble (§10.4): con lote abierto todo funciona igual; con lote liquidado el mensaje dice qué hacer («Reabra el lote para modificarlo») |
| **La transacción del cierre falla a mitad** | Panamá: el `/liquidar` de los 6 insumos ya quedó persistido (son dos endpoints HTTP distintos, sin transacción entre ellos) | El cierre revierte entero; los insumos quedan y se sobrescriben en el siguiente intento (upsert por lote). La atomicidad entre los dos endpoints NO se arregla en esta feature: queda anotada |
| **Fuga multi-empresa preexistente** en `ReporteIndicadorPanamaService`: `GuardarLiquidacionAsync` y `GetReporteAsync` no filtran por empresa ni alcance — cualquier autenticado escribe/lee la liquidación de cualquier `loteAveEngordeId` | Contradice la regla de empresa efectiva fail-closed | Se cierra en B9, replicando el patrón que el mismo archivo ya usa en `GetReportePorCorridaAsync:127-146` |

**Auditoría:** cada copia registra quién, cuándo, con qué versión de fórmula, cuántas filas, checksum y origen;
las copias anuladas guardan quién reabrió y con qué motivo. El historial de liquidaciones de un lote es
`SELECT * FROM liquidacion_lote_engorde_congelada WHERE lote_ave_engorde_id = X ORDER BY congelada_at`.

---

## 10. Tests y validación

### 10.1 xUnit (`backend/tests/ZooSanMarino.Application.Tests/`) — gate de merge del CI
- **`LiquidacionCongeladaGateCalculosTests.cs`** (nuevo): la lista cerrada de operaciones × estado
  (`"Abierto"` / `"Cerrado"` / `"cerrado"` / `""` / `null`), incluido el bypass de corrección. ~22 casos.
  Con estado `"Abierto"` el resultado debe ser **idéntico al comportamiento previo, mensaje incluido**.
- `LiquidacionEngordeCalculosTests.cs`: verde sin cambios (el resumen no cambia de aritmética).
- `ReporteDiarioCostosEngordeCalculosTests.cs` y `PrevencionDescuadresAlimentoTests.cs`: verdes sin tocar.

### 10.2 SQL — `backend/sql/verificar_congelado_engorde.sql` (nuevo)
1. Lotes `Cerrado` sin copia vigente → **0**.
2. Copias vigentes con lote `Abierto` → **0**.
3. Dos copias vigentes del mismo lote → **0** (lo impide el UNIQUE; se verifica igual).
4. Filas de detalle sin cabecera → **0**.
5. `filas` de la cabecera vs `count(*)` del detalle → iguales.
6. Diff copia vs vivo por lote congelado, columna a columna (con `checksum` como atajo).

### 10.3 SQL — gate vinculante ya existente
`backend/sql/verificar_paridad_saldo_engorde.sql` **antes y después**, en TODAS las empresas: tocamos
`fn_seguimiento_diario_engorde`, así que la regla de CLAUDE.md («Invariantes que NO se pueden romper») obliga a
la comparación fila a fila. Para los lotes **no congelados** el diff debe ser **0 en todas las columnas**.

### 10.4 Smoke funcional (local, BD refrescada desde el dump de prod)
1. Liquidar un lote → existe copia, `filas > 0`, la tabla diaria devuelve la copia.
2. **Cambiar la fórmula a mano** (tocar un factor de la fn en la BD de prueba) → el lote liquidado **no se
   mueve**; un lote abierto del mismo galpón sí. *Este es el test que define la feature.*
3. Reabrir → copia anulada, vuelve a calcular en vivo, la tabla diaria cambia.
4. Re-liquidar → copia nueva; la anterior queda anulada en el historial.
5. Editar el lote / borrarlo / crear seguimiento / crear venta / tocar reproductora → error claro en los 5.
6. **Ingreso de inventario en un galpón que comparte un lote vivo y uno liquidado** → el ingreso pasa, el lote
   vivo se recalcula, el liquidado no se mueve.
7. Corrección de aves disponibles sobre un lote liquidado → aplica y **re-congela**; el checksum cambia.
8. Panamá: `/liquidar` (6 insumos) → `/cerrar` en ese orden funciona; `/liquidar` después de cerrado → 400.
9. Empresa con lotes liquidados y otra sin ellos: el Reporte de Costos y el Informe Semanal siguen dando lo
   mismo para los lotes NO liquidados.
10. Backfill: copias creadas = lotes `Cerrado`, los reabiertos sin copia, y correr la migración dos veces no
    duplica nada.

### 10.5 Build
`cd backend && dotnet build` (0 errores, sin advertencias nuevas) + `dotnet test` (los 1.417 actuales verdes
+ los nuevos) · `cd frontend && yarn build` (el único warning aceptado es el de bundle budget preexistente).
`make down` al terminar: sin procesos huérfanos.

---

## 11. Orden de implementación

1. Migración EF idempotente `AddLiquidacionLoteEngordeCongelada`: tablas + índices + `fn_congelar_…` +
   `fn_anular_…` + `fn_recongelar_…` + trigger + **fn v13** + backfill (en ese orden, un solo archivo, con
   `Down()`).
2. `backend/sql/fn_seguimiento_diario_engorde.sql` actualizado a v13 (el repo mantiene el `.sql` de referencia
   además de la migración).
3. Entidad + Configuration + `DbSet` de la cabecera (solo la cabecera).
4. `LiquidacionCongeladaGateCalculos` + sus tests.
5. Cierre/reapertura transaccionales en `LoteAveEngordeService`.
6. Lectura del resumen desde la copia en los DOS services.
7. Los 10 gates de §6.B (empezando por B1-B3, que son el mismo archivo).
8. Endpoint admin de re-congelado.
9. Front: badge + rótulos.
10. Validación §10 completa y actualización del instructivo de operación.

---

## 12. Marcado explícito: lo que NO verifiqué en esta sesión

- **Los conteos de la BD** (23 liquidados Ecuador / 20 `Cerrado` / 3 reabiertos / 0 Panamá) vienen del borrador
  previo. No los corrí contra la base. **Verificar antes del backfill.**
- **`fn_reporte_indicadores_panama`**: no encontré su definición en `backend/sql/` ni en una migración con ese
  nombre. No sé si pasa por `fn_seguimiento_diario_engorde` (si pasara, quedaría congelada gratis) ni qué
  agrega. Hay que ubicarla antes de cerrar el alcance de Panamá.
- **Paridad de columnas de `vw_seguimiento_pollo_engorde`** con la tabla de detalle: la vista es un espejo **v7**
  con nombres y derivaciones propias (`saldo_alimento_kg_calculado`, `tipo_fila`, `documento_hist`, columnas de
  granja/empresa). La divergencia con la v12 es **preexistente**; no la medí.
- **Costo real del cambio a plpgsql**: no corrí `EXPLAIN`. Es el riesgo de performance a medir en §10.
- **Harness de tests del front**: por memoria del proyecto está roto de antes (`yarn test` compila 0 specs). No
  lo verifiqué; el plan no depende de tests de front.
- **`LiquidacionTecnicaEcuadorService` y `fn_indicadores_pollo_engorde`**: los dejé fuera de alcance por
  arquitectura (motores independientes), sin medir cuánto se mueven en la práctica.
- **El comportamiento auto-reparador del saldo de alimento** (§3.1 punto 5) es una deducción del código de
  `SaldoAlimentoEngordeAplicador.RecalcularPorLoteAsync`, no una prueba: hay que confirmarlo en el smoke 6.
