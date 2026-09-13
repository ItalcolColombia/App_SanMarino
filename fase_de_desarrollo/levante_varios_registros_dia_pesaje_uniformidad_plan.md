# Levante con varios registros por día: pesaje y uniformidad (Santa Reyes) — 13-sep-2026

**Origen:** pendiente anotado en `indicadores_semanales_varios_registros_dia_plan.md` §5 (commit `f3a4a13`):
la fn diaria de LEVANTE y los dos reportes semanales de levante tienen el mismo «último registro» con
NULL/empate que ya se corrigió en producción (`fn_seguimiento_diario_produccion` v4) y en el pesaje de
`fn_indicadores_levante_postura`.

## 1. Diagnóstico (BD local `sanmarinoapplocal`, 13-sep-2026)

| # | Objeto | Qué pasa | Evidencia |
|---|---|---|---|
| L1 | `fn_seguimiento_diario_levante`, CTE `seg_dias_agrupado` (solo flag ON) | uniformidad/CV por sexo = `(array_agg(x ORDER BY c_ts DESC))[1]`: (a) un registro posterior **sin** la medición (NULL) tapa la del día; (b) los forms graban todo el día a mediodía ⇒ `c_ts` empata y «el último» no es determinista. | Lote 155 (Santa Reyes): 3 registros el 04-sep, los 3 a las `12:00-05`. |
| L2 | idem | peso por sexo, kcal y proteína = `AVG(x)`: un 0 guardado como «no medido» baja el promedio a la mitad (el caso de `peso_huevo` en producción). | En local: 0 ceros / 0 negativos en las 8 columnas (1.179 filas). Se blinda igual (con un registro por día no cambia nada). |
| L3 | `fn_reporte_semanal_levante_extras` (Reporte Técnico Semanal) | Pesaje de la semana = **UN** registro (`ph>0 OR pm>0 ORDER BY reg_date DESC, id DESC LIMIT 1`): con 2 pesajes el mismo día no promedia, y si el último pesó solo un sexo el otro se pierde y se arrastra el de la semana anterior. Uniformidad/CV del mismo registro aunque no la traiga. | Mismo bloque que tenía `fn_indicadores_levante_postura` antes de `20260913120000`. |
| L4 | `fn_resumen_semanal_ra_pesadas_levante` (Informe RA Pesadas) | Igual que L3 (`LATERAL … ORDER BY (pesó primero), reg_date DESC, id DESC LIMIT 1`). | idem. |
| — | `sp_recalcular_seguimiento_levante` (modal «Cálculos») | Consumidor puro de `fn_seguimiento_diario_levante`: hereda L1/L2 sin cambio de código. | `base` lee la fn. |
| — | Sumas y días | Correctos desde `20260905035704` (`COUNT(DISTINCT reg_date)`, SUM asociativa). Sin cambio. | |

Espejos `.sql` de las 4 fns = cuerpo desplegado (diff normalizado contra `pg_get_functiondef`: 0 líneas).
La cabecera de `fn_seguimiento_diario_levante.sql` dice que los 3 semanales leen «vía TEMP TABLE sobre esta
fn»: es falso, leen la tabla cruda (se corrige en el changelog).

**Fuera de alcance (anotado):** `fn_reporte_diario_costos_postura`, rama levante (`lev_dedup`), hace
`DISTINCT ON (lote, día)` ⇒ con 2+ registros el día **descarta** el 2.º (mortalidad/consumo). No es «último
registro», es pérdida de filas: tarea aparte.

## 2. Enfoque (mismo criterio que producción v4 / pesaje de indicadores)

**Regla transversal:** con UN registro por día cada expresión devuelve el valor de esa fila ⇒ flag OFF
intacto por construcción (y, en los reportes, que no ramifican por flag, idéntico con un pesaje por día).

- **B1 `fn_seguimiento_diario_levante` v2** — solo `seg_dias_agrupado`:
  - uniformidad/CV por sexo → `(array_agg(x ORDER BY c_ts DESC, c_id DESC) FILTER (WHERE x IS NOT NULL))[1]`.
  - peso por sexo, kcal, proteína → `COALESCE(AVG(x) FILTER (WHERE x > 0), AVG(x))`.
  - `seg_dias_dedup`, firma y resto sin cambio ⇒ `CREATE OR REPLACE`.
- **B2 `fn_reporte_semanal_levante_extras`** — pesaje por DÍA: último día de la semana con pesaje; peso por
  sexo = promedio de los registros de ese día que pesaron ese sexo (`> 0`); uniformidad/CV por sexo = la del
  último registro (id) de ese día que la trae (`> 0`). Semana sin pesaje: el último registro, como siempre.
  Kcal/prot ya promedian `> 0`.
- **B3 `fn_resumen_semanal_ra_pesadas_levante`** — misma regla en SQL (`dia_pesaje` + `pesaje_del_dia`; el
  `LATERAL` de siempre queda solo para la semana sin pesaje).
- **B4 Espejos C#** (dueña = fn, contrato = tests):
  - `SeguimientoDiarioLevanteCalculos.AgruparPorDia` v2 (+ `CvH/CvM/KcalH/ProtH` opcionales, desempate por `RegId`).
  - `PesajeSemanalLevanteCalculos` + `CvH/CvM` opcionales (misma regla que uniformidad) — pasa a especificar
    también el pesaje de los dos reportes.
- **B5 Migración** `20260913150000_LevanteVariosRegistrosDiaPesajeUniformidad`: Up = las 3 fns nuevas; Down =
  versiones previas verbatim (= espejos actuales = desplegado). `.Fn.cs` generado desde los espejos, Designer
  clonado de `20260913120000`, sin tocar el ModelSnapshot. Espejos `.sql` con changelog.
- **B6 Gate** `backend/sql/verificar_paridad_levante_varios_registros_dia.sql` (1.ª corrida congela, 2.ª compara
  por empresa): `fn_seguimiento_diario_levante` (todos los lotes), `sp_recalcular_seguimiento_levante`
  (salida en `produccion_resultado_levante`, dentro de un subbloque revertido: no deja escrituras),
  `fn_reporte_semanal_levante_extras` (todos los lotes) y `fn_resumen_semanal_ra_pesadas_levante` (todas las
  empresas × años). Empresas ≠ Santa Reyes ⇒ 0 diferencias.

## 3. Cambios de BD
Solo funciones (`CREATE OR REPLACE`, firmas intactas). Sin DDL de tablas ni datos.

## 4. Casos de prueba
- xUnit `AgruparPorDia`: 1 registro idéntico (con los campos nuevos); uniformidad/CV NULL del último no tapa;
  empate de ts ⇒ gana mayor id (independiente del orden de entrada); peso/kcal/prot con 0 ⇒ promedio de los que
  midieron; todo 0 ⇒ 0; todo NULL ⇒ NULL.
- xUnit `PesajeDeLaSemana`: CV = último del día que la trae; semana sin pesaje devuelve el CV del último registro.
- Clon `CREATE DATABASE … TEMPLATE sanmarinoapplocal`, datos de prueba solo en el clon (lote 155, 04-sep: 3
  registros a mediodía con pesos, un 0, uniformidad/CV solo en el primero):
  - gate B6 con las fns actuales (congela) → fns nuevas por psql → compara: solo Santa Reyes cambia, justificado.
  - Down (versiones previas) → gate = 0 diferencias en todas las empresas.
  - Migración aplicada por EF contra el clon (Up y Down) → mismo resultado.
- `dotnet build` API 0 err/0 warn nuevos · `dotnet test` Application.Tests · `verificar-sql-llega-por-migracion.js`.

## 5. Deploy
No. Requiere OK explícito del usuario.
