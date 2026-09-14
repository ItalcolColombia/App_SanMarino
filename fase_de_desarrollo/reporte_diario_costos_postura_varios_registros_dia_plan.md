# Reporte Diario Costos Postura con varios registros por día (Santa Reyes) — 13-sep-2026

**Pedido del usuario:** en `fn_reporte_diario_costos_postura`, la rama de LEVANTE hace
`DISTINCT ON (lote, día Bogotá)` sobre `seguimiento_diario_levante`: con el flag
`companies.permite_multiples_seguimientos_diarios` (hoy solo Santa Reyes) un día puede tener 2+
registros y el reporte se queda con el primero ⇒ mortalidad / selección / error de sexaje / venta /
consumo de los demás registros **desaparecen** del reporte de costos. Flag OFF: byte a byte igual.

## 1. Causas (lectura de código + BD local, 13-sep-2026)

| # | Rama | Qué pasa |
|---|---|---|
| C1 | Levante | `lev_dedup` = `DISTINCT ON` ⇒ 1 registro por día. Lote 155 (Santa Reyes) el 04-sep tiene **3 registros** (15 aves + 3.000 kg · 400 kg · 99 kg): el reporte muestra 3.000 kg, se pierden 499 kg. |
| C2 | Alimentos (ambas) | El json `alimentos` se arma explotando `metadata.itemsHembras/itemsMachos` de la FILA DEL DÍA. En levante esa fila es un solo registro (C1); en producción la fila sale de `fn_seguimiento_diario_produccion` v4, cuyo `metadata` es la del **último** registro (solo `huevoItems` se concatena) ⇒ los ítems de alimento de los demás registros no aparecen aunque `consumo_kg` sí sume. Además el fallback por `tipo_alimento` es por DÍA: un día con un registro con ítems + otro sin ítems pero con kg perdería el segundo. |
| C3 | Producción | `venta_aves_h/m` no la expone la fn canónica y el reporte la trae con `LEFT JOIN seguimiento_diario_produccion sp ON sp.id = fn.seg_id`; con el flag ON `seg_id` es `MIN(seg_id)` del día ⇒ solo la venta del primer registro. |
| — | Producción (resto) | Bajas, consumo y huevos ya salen agrupados de `fn_seguimiento_diario_produccion` v4 (SUMA). Sin cambio. |
| — | Flag OFF con días duplicados | Demo (company 4) tiene 3 días históricos con 2 registros en levante (lotes 120/123/127). Con flag OFF se conserva el `DISTINCT ON` de siempre — mismo criterio que `fn_seguimiento_diario_levante.seg_dias_dedup` — ⇒ la decisión **tiene que ser por flag**, no "agrupar siempre". |

Espejo `backend/sql/fn_reporte_diario_costos_postura.sql` = `prosrc` desplegado en local (comparado
13-sep; el rename `item_inventario_ecuador → item_inventario` lo aplicó `20260902140000` reescribiendo
desde `pg_get_functiondef`) ⇒ el espejo actual es el **Down verbatim**.

## 2. Enfoque

**Regla transversal:** el flag se lee UNA vez por llamada (`companies.id = p_company_id`, fail-closed
`false`): la fn es de una sola empresa. Flag OFF recorre **exactamente el mismo camino de siempre**.

### Backend — `fn_reporte_diario_costos_postura` v3 (misma firma ⇒ `CREATE OR REPLACE`)
- `cfg` gana `permite_multiples`.
- **Levante:** `lev_registros` (registros crudos del alcance) →
  - flag OFF: `lev_dedup` = el `DISTINCT ON` de siempre (gana `fecha, id` más temprano);
  - flag ON: `lev_agrupado` = `GROUP BY (lote, día)`: **SUMA** mortalidad, selección, error de sexaje,
    venta de aves y consumo; `tipo_alimento`/`metadata` del día = último registro (`fecha DESC, id DESC`)
    — informativo, el alimento se arma por registro (abajo).
- **Alimentos por REGISTRO:** CTE `alim_registros` = de dónde se explotan los ítems:
  - flag OFF: la fila del día (idéntico a hoy);
  - flag ON: levante ⇒ cada registro del día; producción ⇒ cada registro de `seguimiento_diario_levante`
    (`tipo_seguimiento='produccion'`) ∪ `seguimiento_diario_produccion` (no borrado) del lote y día —
    las mismas dos fuentes y el mismo corte de día que la rama legacy de `fn_seguimiento_diario_produccion`.
  - El fallback por `tipo_alimento` pasa a decidirse **por registro** (registro sin ítems de ese sexo pero
    con kg ⇒ entrada de fallback con SUS kg). Con un registro por día = regla de siempre.
  - Una entrada por ítem por registro (decisión D4, sin fusionar por nombre: los totales por
    (sexo, nombre) ya los agrupa `ReporteDiarioCostosPosturaCalculos.TotalesAlimento`).
  - Orden del json: `sexo, nombre` de siempre + desempate por registro (`fecha, id`).
- **Producción venta de aves:** flag OFF ⇒ join por `seg_id` de siempre; flag ON ⇒ SUMA de
  `venta_aves_*` de los registros de `seguimiento_diario_produccion` del lote y día.

### Espejo C# (la fn es la dueña; esto es su contrato)
`Application/Calculos/ReporteDiarioCostosPosturaVariosRegistrosCalculos.cs` (puro): fila de levante del
día (OFF primero / ON suma), registros que aportan alimento, alimentos por registro con fallback
(incluye el partido de `tipo_alimento`), venta de aves de producción del día.
Tests `tests/ZooSanMarino.Application.Tests/ReporteDiarioCostosPosturaVariosRegistrosCalculosTests.cs`.

### Migración
`20260913160000_ReporteCostosPosturaVariosRegistrosDia` — Up: fn v3 (espejo); Down: v2 verbatim
(espejo actual). `.Fn.cs` generado desde los espejos; Designer clonado de `20260913120000` (sin tocar
el ModelSnapshot). Timestamp posterior a `20260913150000_LevanteVariosRegistrosDiaPesajeUniformidad`
(otra sesión, ya en `main`; toca otras funciones) para que el orden quede lineal.

### Gate
`backend/sql/verificar_paridad_reporte_costos_postura.sql` (modelo `verificar_paridad_indicadores_semanales.sql`):
1ª corrida congela `fn_reporte_diario_costos_postura(company)` de TODAS las empresas; 2ª compara fila a
fila (con y sin orden del json de alimentos). Toda empresa sin el flag ⇒ **0 diferencias**.

## 3. Cambios de BD
Solo la función (`CREATE OR REPLACE`, firma intacta). Sin DDL de tablas, sin datos.

## 4. Casos de prueba
1. Flag OFF (Sanmarino, Demo, Ecuador, Panamá): gate = 0 filas distintas (Demo incluye sus 3 días con 2 registros ⇒ sigue ganando el primero).
2. Santa Reyes lote 155, 04-sep (3 registros): mortalidad 15, consumo H 3.499 kg, 3 entradas de alimento (3.000 + 400 + 99).
3. Registro sin ítems pero con kg el mismo día que uno con ítems ⇒ sale su entrada de fallback.
4. Selección / error de sexaje / venta repartidos en 2 registros ⇒ suman.
5. Producción Santa Reyes (LPP 20) con 2 registros el mismo día: venta de aves suma y los ítems de ambos registros aparecen.
6. Santa Reyes días con 1 registro ⇒ sin diferencias.
7. Down (v2) ⇒ gate 0 diferencias en todas las empresas.
8. `dotnet build` API · `dotnet test` Application.Tests · `node backend/scripts/verificar-sql-llega-por-migracion.js`.

## 5. Resultado medido (clon `sanmarino_costos_0913`, 13-sep-2026)

Clon de `sanmarinoapplocal` + `fn_seguimiento_diario_produccion` v4 (estado de `main`) + datos de prueba de
Santa Reyes: levante lote 155 04-sep 4.º registro (sin ítems, 50 kg M, sel 2/1, error 1/1, venta 5, mort M 2;
`company_id` = 6 — los registros 1678/1609 lo tienen NULL y el trigger `unico_por_dia` no ve el flag si se
copian tal cual); levante lote 152 21-ago 2.º registro (mort 3, 20 kg con ítem); producción lote 152 04-sep
2.º registro (venta 7/2, 100 kg con ítem).

| Día | v2 (ANTES) | v3 (DESPUÉS) |
|---|---|---|
| Levante 155 · 04-sep | mort 15/0 · sel 0/0 · error 0/0 · venta 0 · 3.000 / 0 kg · 1 ítem | mort 15/2 · sel 2/1 · error 1/1 · venta 5 · **3.499 / 50 kg** · 3 ítems H + fallback M «CAMPESINO SR M» 50 |
| Levante 152 · 21-ago | mort 0 · 999,991 kg · 1 ítem | mort 3 · **1.019,991 kg** · 2 ítems |
| Producción 152 · 04-sep | venta 0/0 · 499 kg con 1 ítem (100) | venta **7/2** · 499 kg con 2 ítems (399 + 100) |

- Gate (base = v2 congelada, 1.764 filas): **Sanmarino 1.118 lev + 602 prod y Demo 35 + 2 = 0 filas distintas**
  (Demo incluye sus 3 días con 2 registros ⇒ sigue ganando el primero). Santa Reyes: 3 filas distintas, las
  3 de arriba, con y sin orden del json. Ecuador/Panamá no tienen postura en la BD local (0 filas).
- Down (v2 del espejo previo) ⇒ gate **0 en todas las empresas**. Mismo resultado aplicando el SQL que genera
  `dotnet ef migrations script` para Up y Down (sin las líneas de `__EFMigrationsHistory`).
- `dotnet build` API 0 warn / 0 err · `dotnet test` Application.Tests **4.225/4.225** (+22) ·
  `verificar-sql-llega-por-migracion.js` OK · `ef migrations list` reconoce la migración.
- Los 4 grupos de ítems empatados (mismo sexo y nombre el mismo día) de empresas sin el flag son del MISMO
  registro ⇒ el desempate nuevo por registro no los reordena (gate `filas_distintas` = 0).
