# Plan — La apertura de alimento deja de heredar el ciclo anterior del galpón

**Fecha:** 2026-07-29 · **Empresas afectadas:** ItalcolEcuador (activo) · ItalcolPanama (preventivo)
**Diagnóstico:** [`cuadre_engorde_ecuador_diagnostico_saldo_alimento.md`](cuadre_engorde_ecuador_diagnostico_saldo_alimento.md)

## 1. Problema

La ventana de alimento previo al encaset (v9, `36a8bab`, 28-jul) hace que la **apertura** de un lote
cuente movimientos del **ciclo anterior del mismo galpón**. Como el filtro de devoluciones descarta las
entradas (`devolución por eliminación`) pero conserva las salidas, la apertura queda **negativa** y corre
todas las filas de la grilla por igual.

Testigo: Kilometro 22 / G0036 / lote 2603 (`id 98`) → apertura **−7.960 kg**, grilla 3.420 contra
11.380 de stock real.

**Es estructural y recurrente:** la ventana solo alcanza la limpieza del ciclo anterior si ese ciclo
existe ⇒ **aparece desde el tercer ciclo de cada galpón en adelante**. Ecuador ya va por la corrida 4.
Panamá hoy tiene 0 casos, pero en cuanto encadene un tercer ciclo por galpón le pasa lo mismo.

## 2. Enfoque

**El criterio ya existe en el código.** v10 restringió el *consumo* a los lotes que **CONVIVEN** en el
galpón (rangos de seguimiento solapados). La **apertura y los movimientos** deben usar exactamente el
mismo criterio: un lote cuyo ciclo terminó antes de que empezara el mío **no comparte bodega conmigo**,
así que ni su alimento ni sus traslados son míos.

```
lote ajeno  :=  otro lote del mismo (granja, núcleo, galpón)
                cuyo rango de seguimiento NO se solapa con el mío
                → NOT (min_otro <= max_mio AND max_otro >= min_mio)

movimiento contable para mí  :=  lote_ave_engorde_id IS NULL   (no atribuido, se conserva)
                              OR lote_ave_engorde_id NOT IN (lotes ajenos)
```

La atribución es fiable para esto: en Ecuador, de los movimientos de alimento **con galpón** hay
**0 ingresos y 0 salidas sin lote** y **0 movimientos apuntando a un lote de otro galpón**. Los 8
`INV_TRASLADO_ENTRADA` sin lote (35.770 kg) siguen contándose, igual que hoy.

### Simulación previa (dump de producción, ya corrida)

| Empresa | Lotes tocados | Aperturas negativas hoy → con fix | Kg fantasma hoy → con fix |
|---|---:|---|---|
| **ItalcolEcuador** | 30 | 26 → **6** | −98.692 → **−13.800** |
| **ItalcolPanama** | **0** | 0 → 0 | 0 → 0 |

Panamá es **no-op exacto**. El lote testigo pasa de −7.960 a **0** → saldo día 1 = 11.520 ✓.
Los 6 negativos que quedan son déficit real (consumo registrado antes que su llegada), que por decisión
de v9 **se muestra tal cual** y no se recorta.

## 3. Archivos a tocar

### 3.1 SQL — `backend/sql/fn_seguimiento_diario_engorde.sql` → **v11**

Nuevo CTE `lotes_ajenos` (complemento del predicado de `consumo_galpon_por_fecha`) y filtro
`(h.lote_ave_engorde_id IS NULL OR h.lote_ave_engorde_id NOT IN (SELECT id FROM lotes_ajenos))` en los
**cuatro** CTE que leen movimientos con scope galpón:

| CTE | Para qué | Por qué también |
|---|---|---|
| `apert_mov` | apertura | **la causa raíz** |
| `hist_full` | `saldo_close` → `fecha_max` | que el cierre no lo mueva otro ciclo |
| `hist_alimento` | columnas Ingreso/Traslado + `pt_calc` | que no muestre como propio lo ajeno |
| `fechas_universo` + `docs_por_fecha` | filas y documento | coherencia (si no, quedan filas vacías) |

La rama `VENTA_AVES` ya está acotada a `h.lote_ave_engorde_id = p_lote_id` → **no se toca**.

### 3.2 Cálculo puro — `Application/Calculos/SeguimientoAvesEngordeCalculos.cs`

- `ComputeSaldoAperturaGalponAntesPrimerSeguimiento(..., IReadOnlySet<int>? lotesAjenos = null)`
- `CalcularSaldoAlimentoPorSeguimiento(..., IReadOnlySet<int>? lotesAjenos = null)`
- Helper compartido `EsDeLoteAjeno(h, lotesAjenos)`.
- Parámetro **opcional con default null** ⇒ sin el set, comportamiento byte a byte idéntico al actual.

### 3.3 Services (los TRES caminos quedan con la misma fórmula)

| Archivo | Cambio |
|---|---|
| `Services/SeguimientoAvesEngorde/Funciones/…Service.SaldoAlimento.cs` (carga masiva) | resolver `lotesAjenos` y pasarlo |
| `Services/SeguimientoAvesEngordeEcuador/Funciones/…EcuadorService.SaldoAlimento.cs` (form diario) | resolver `lotesAjenos` + **adoptar la ventana** (hoy usa `fecha_encaset` a secas) |

> ⚠️ Unificar la ventana en el service de Ecuador **cambia valores persistidos**. Es intencional: hoy las
> dos fuentes discrepan y esa discrepancia es justamente el síntoma. Con la ventana acotada por
> `lotesAjenos` deja de ser peligrosa. Se mide el impacto antes de mergear.

### 3.4 Migraciones EF (idempotentes)

1. `…_FnSeguimientoEngordeV11AperturaSinCicloAnterior` — `CREATE OR REPLACE FUNCTION` (idempotente por
   naturaleza). `Down` restaura la v10 completa.
2. `…_RecalcularSaldoAlimentoEngordeAperturaCicloAnterior` — recálculo de datos: reescribe
   `seguimiento_diario_aves_engorde.saldo_alimento_kg` de **todos** los lotes de engorde con la fórmula
   corregida, en SQL puro (mismo cálculo que la fn). Idempotente: una 2ª corrida no mueve nada
   (`IS DISTINCT FROM` en el `UPDATE`). Backup previo en tabla `_backup_*` como en el cuadre de Panamá.

### 3.5 Tests xUnit — `tests/ZooSanMarino.Application.Tests/`

`AperturaAlimentoCicloAnteriorCalculosTests.cs` (NUEVO):

1. Sin `lotesAjenos` → resultado **idéntico** al actual (retrocompatibilidad, el gate de Panamá)
2. Caso testigo lote 98: 4 movimientos de un lote ajeno → apertura 0 (hoy −7.960)
3. Lote **conviviente** (rangos solapados) → sus movimientos **SÍ** cuentan (no romper v10/Panamá)
4. `lote_ave_engorde_id NULL` → cuenta (comportamiento conservado)
5. Movimiento del **propio** lote → cuenta
6. Ajeno con movimiento **dentro** de mi rango (limpieza registrada tarde) → no cuenta
7. Ventana: mismo lote con y sin `diasAlimentoPrevio` → el filtro es ortogonal a la ventana
8. Set vacío ≠ null → mismo resultado

## 4. Reglas de negocio

- **La bodega es del galpón, no del lote.** Solo comparten saldo los lotes que conviven.
- Un movimiento **sin atribución** se considera del ciclo vigente (no se pierde alimento).
- El saldo **puede ser negativo**: significa consumo registrado antes que su llegada (decisión v9, se mantiene).
- **Refactor ≠ cambio de comportamiento** donde no es el objetivo: Panamá debe salir con **0 diferencias** fila a fila.

## 5. Validación

- [ ] `dotnet build` 0 errores / 0 advertencias nuevas
- [ ] `dotnet test` verde (hoy 1.341)
- [ ] fn v11 en BD local: **Panamá fila a fila con 0 diferencias** contra v10
- [ ] fn v11: el lote 98 muestra 11.520 el día 1 y 11.380 al cierre (= stock)
- [ ] Los 7 galpones de §4.1 del diagnóstico pasan a cuadrar contra el stock
- [ ] Recuento del veredicto: de 25 OK a ≥32 de 35
- [ ] Migración de datos idempotente: 2ª corrida mueve 0 filas
- [ ] `Down` probado (v11 → v10 devuelve los valores originales)

## 6. Fuera de alcance (documentado, no se hace acá)

- Los **3 descuadres persistentes de datos** (Kilometro 61 G0037 −10.000, Kilometro 86 G0040 −2.400,
  CAROLINA G0058 +480). El recálculo de §3.4.2 los corrige si su causa es la fórmula; lo que quede
  después es descuadre real y se decide aparte.
- El **saldo persistido que se queda viejo** cuando entra alimento después del último seguimiento
  (`RecalcularSaldoAlimentoPorLoteAsync` solo corre al crear/editar seguimiento). Se mide después del fix;
  con la grilla recalculando en vivo, deja de ser visible para la operación.
- Los **6 galpones con descuadre histórico** (§2 del diagnóstico): no afectan lo que ve la operación hoy.
