# Indicadores y Gráfica con varios registros por día (Santa Reyes) — 13-sep-2026

**Pedido del usuario:** «por el trabajo en Santa Reyes que permite varios registros en el mismo día
en levante y producción, el tab de Indicadores y Gráficas no funciona; el indicador y la guía son
semanales: organizar la agrupación».

## 1. Causas medidas (BD local `sanmarinoapplocal`, 13-sep-2026)

| # | Módulo | Qué pasa | Evidencia |
|---|---|---|---|
| C1 | Levante | **500** en `GET /api/SeguimientoLoteLevante/por-lote/{id}/indicadores` para Santa Reyes. `fn_indicadores_levante_postura` devuelve `consumo_tabla/peso_tabla/unif_tabla/mort_tabla` en **NULL** cuando la empresa tiene guía propia y la semana no tiene fila (la de Santa Reyes arranca en la semana 18 ⇒ todo el levante), pero `IndicadorSemanalLevanteDto` las declara `double` ⇒ EF no materializa NULL. La tabla muestra «No se pudieron cargar» y la gráfica queda vacía. | `fn_indicadores_levante_postura(155)` sem 6 ⇒ las 4 columnas NULL. Cambio de guía propia del 30-ago (`v_guia_propia_empresa`). |
| C1b | Levante (front) | Aunque el back respondiera, el front revienta con esos NULL: `formatPercentage(ind.unifTabla)` (`toFixed` sobre null) en la tabla y `x.consumoTabla.toFixed(0)` en `prepararSeriesGraficas` de la gráfica. | Lectura de código. |
| C2 | Levante | El **pesaje semanal** toma UN registro («último con peso>0» por id). Con 2 pesajes el mismo día no promedia (la grilla diaria sí), y si el último registro del día pesó solo hembras, el peso de machos del otro registro se pierde y se arrastra el de la semana anterior. | `fn_indicadores_levante_postura.sql` bloque «Pesaje». Sumas y días ya están bien (`COUNT(DISTINCT reg_date)`, `20260905035704`). |
| C3 | Producción | `fn_seguimiento_diario_produccion` (rama `seg_dias_agrupado`, solo flag ON) alimenta las 3 fns semanales y agrupa mal lo que no es aditivo: (a) `peso_huevo` se guarda en **0** cuando no se pesa ⇒ `AVG` con ceros (60 g + 0 ⇒ 30 g); (b) uniformidad/CV «último registro» aunque ese registro no la traiga (NULL tapa la medición); (c) empate en `c_ts` (los forms graban todos a mediodía) ⇒ «el último» no es determinista; (d) `metadata` «gana el último» ⇒ **`huevoItems` de los demás registros del día se pierden**: Primera/Pnc semanal < `huevo_tot`. | `seguimiento_diario_produccion.peso_huevo`: company 6 = 3 ceros / 0 nulos; `fn_clasificacion_huevo_items_produccion` lee `f.metadata` de la fila agrupada. |
| — | Producción | Sumas y días de `fn_indicadores_produccion_postura` ya son por día (lee la fn diaria). Sin cambio. Un lote de Santa Reyes con semana de vida < 18 (`semana_inicio_indicadores_produccion`) sale vacío **por diseño** (ej. LPP 20 local, semanas 2-3). | `fn_indicadores_produccion_postura(6,20,…)` = 0 filas. |

Espejos `.sql` de las 3 fns = cuerpo desplegado (comparado contra `pg_get_functiondef`, 13-sep). Sin
dependencias de vistas (`pg_depend` vacío).

## 2. Enfoque

**Regla transversal:** con **un registro por día** (todas las empresas sin el flag, y los días normales
de Santa Reyes) cada fórmula nueva da exactamente el valor de siempre. Lo nuevo solo se ejerce con
2+ registros el mismo día.

### Backend
- **B1** `IndicadorSemanalLevanteDto`: `ConsumoTabla`, `PesoTabla`, `UnifTabla`, `MortTabla` → `double?`
  (la fn ya los manda NULL). Test de contrato por reflexión.
- **B2** `fn_indicadores_levante_postura` — pesaje por **DÍA**: se toma el último día de la semana con
  pesaje (`ph>0 OR pm>0`); peso por sexo = promedio de los registros de ese día que pesaron ese sexo
  (`>0`); uniformidad por sexo = la del último registro (id) de ese día que la trae (`>0`). Sin
  pesaje en la semana: igual que hoy (último registro). Firma igual ⇒ `CREATE OR REPLACE`.
- **B3** `fn_seguimiento_diario_produccion` **v4**, SOLO `seg_dias_agrupado` (flag OFF intacto por
  construcción):
  - `peso_h`, `peso_m`, `peso_huevo` → promedio de los valores `>0`; si ninguno pesó, el `AVG` de siempre.
  - uniformidad / CV (mixta y por sexo) → último registro **que la trae** (no NULL).
  - todo «gana el último» → desempate `c_ts DESC, c_seg_id DESC` (determinista = el cargado último).
  - `metadata` → la del último registro, con `huevoItems` = **concatenación** de los `huevoItems` de
    todos los registros del día (orden de carga). Sin ningún `huevoItems` en el día: la metadata de siempre.
  - Heredan sin cambio de código: `fn_indicadores_produccion_postura`, `fn_clasificacion_huevo_items_produccion`,
    `fn_resumen_semanal_ra_pesadas_produccion`.
- **B4** Espejos C# (dueña = fn, contrato = tests):
  - `SeguimientoDiarioProduccionCalculos.AgruparPorDia` v4 (+ `PesoHuevo`, `HuevoItems` opcionales) y tests nuevos.
  - `PesajeSemanalLevanteCalculos.PesajeDeLaSemana` (nuevo) + tests.
- **B5** Migración `20260913120000_IndicadoresSemanalesVariosRegistrosDia` (Up: las 2 fns nuevas; Down:
  las previas verbatim) + Designer clonado del último + espejos `.sql` actualizados con changelog.
- **B6** Gate `backend/sql/verificar_paridad_indicadores_semanales.sql` (1ª corrida congela, 2ª compara
  por empresa): `fn_indicadores_levante_postura` (todos los lotes de levante),
  `fn_indicadores_produccion_postura` y `fn_clasificacion_huevo_items_produccion` (todos los LPP).
  Toda empresa ≠ Santa Reyes ⇒ 0 diferencias. Más el gate existente `verificar_paridad_seguimiento_produccion.sql`.

### Frontend (`lote-levante`)
- **F1** `IndicadorSemanalLevanteDto` (TS): las 4 columnas guía `number | null`.
- **F2** `tabla-lista-indicadores`: tipos `number | null`; «Uniformidad Guía» con `formatOpcionalPct`
  (guion para null; con número, salida idéntica a `formatPercentage`).
- **F3** `graficas-principal`: `prepararSeriesGraficas` y `difConsumoPorc` null-safe (mismo resultado con número).
- **F4** Observaciones de la semana: desempate por `id` en el mismo día (gana el último cargado).

## 3. Cambios de BD
Solo funciones (`CREATE OR REPLACE`, firmas intactas). Sin DDL de tablas, sin datos.

## 4. Casos de prueba
- xUnit: DTO nullable; `PesajeDeLaSemana` (1 registro = «último con peso>0»; 2 pesajes el día ⇒ promedio;
  último registro sin machos conserva los machos del día; uniformidad último `>0`; semana sin pesaje ⇒
  último registro); `AgruparPorDia` v4 (peso huevo con 0 ⇒ promedio de los que pesaron; todo 0 ⇒ 0;
  uniformidad NULL no tapa; empate de ts ⇒ gana mayor id; `huevoItems` concatenados; 1 registro idéntico).
- Clon `CREATE DATABASE … TEMPLATE sanmarinoapplocal`:
  - **Antes** del fix (binario actual): `GET …/por-lote/155/indicadores` como Santa Reyes ⇒ 500.
  - Datos de prueba en el clon: 2 pesajes el mismo día en lote 155; LPP 20 con referencia corrida para
    semanas ≥ 18 y un 2.º registro del día con `huevoItems` y `peso_huevo`.
  - Gate B6 + paridad diaria: congelar, aplicar migración, comparar ⇒ solo Santa Reyes cambia, justificado.
  - **Después**: 200 en levante con NULL en guía; producción: Primera+Pnc = `huevo_tot` de la semana,
    peso huevo = promedio de los que pesaron; contraprueba en un lote Sanmarino (idéntico).
- `dotnet build` 0 err + `dotnet test`; `yarn build`; `ng test`; gate `verificar-sql-llega-por-migracion.js`.

## 5. Fuera de alcance (anotado)
- `fn_seguimiento_diario_levante` (modal «Cálculos») y los reportes semanales de levante
  (`fn_reporte_semanal_levante_extras`, `fn_resumen_semanal_ra_pesadas_levante`) tienen el mismo patrón
  «último registro» con NULL/empate; no alimentan Indicadores/Gráfica. Queda para otra tarea.
- Deploy: requiere OK explícito del usuario.
