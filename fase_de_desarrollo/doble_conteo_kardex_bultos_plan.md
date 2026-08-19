# Plan — el kardex de bultos resta el consumo dos veces

> **Origen:** medición V40 (`a2ec07c`). **Cierra además** la decisión V19.2.1, que quedó abierta desde
> el 17-ago-2026 esperando elegir entre dos opciones que la medición mostró insuficientes.
> **Fecha:** 19-ago-2026.

---

## 1. El defecto, en una línea

El saldo de la sección **BULTO** del Reporte Contable resta el mismo alimento **dos veces**: una como
`retiros` (los movimientos `Consumo` de `inventario_gestion_movimiento`) y otra como
`consumoBultosHembras/Machos` (el consumo del seguimiento diario). Son el mismo alimento porque
**los movimientos `Consumo` los escribe el propio seguimiento**.

```
saldo = entradas − traslados − retiros − consumoH − consumoM
                               ^^^^^^^   ^^^^^^^^^^^^^^^^^^^
                               el mismo alimento, contado dos veces
```

`ReporteContableBultosCalculos.AcumularSaldos` recorta cada día con `Math.Max(0m, acumulado)`, así
que el error **no se ve como un número negativo**: se ve como un saldo en cero o más bajo de lo que
debe. Sin el recorte, el acumulado real de los lotes de MANGOS llega a **−2.599,6** y **−2.644,7**.

---

## 2. 🔑 El punto profundo: la invariante existe, está escrita, y se aplica en 2 de 3 lugares

Esto **no** es un descuido aislado. La regla está documentada en el repo, dos veces, con las mismas
palabras — y el tercer lugar la perdió en una traducción de tipos.

**(a) El módulo de inventario VIEJO la resolvió con el tipo de movimiento.**
`backend/src/ZooSanMarino.Domain/Enums/InventoryMovementType.cs:12-19`:

```csharp
// Fase 2 — consumo/devolución automáticos desde seguimientos (Colombia, modelo A).
// EXCLUIDOS de los 4 buckets del ReporteContable (filtra por Entry/TransferIn/
// TransferOut/Exit literales) → no distorsionan las cifras del contable.
ConsumoSeguimiento,
DevolucionSeguimiento
```

El consumo escrito por el seguimiento tiene **su propio tipo**, distinto de `Exit`, y el Contable lo
excluye a propósito. Con el flag apagado el reporte **no duplica**.

**(b) Engorde la conserva, y explica por qué.**
`backend/src/ZooSanMarino.Application/Calculos/TipoEventoInventarioCalculos.cs:51-52`:

```
<item><b>Consumo</b> (INV_CONSUMO): el saldo resta el consumo de
seguimiento_diario_aves_engorde, no el del inventario. Contarlo acá lo descontaría dos veces.</item>
```

`AfectaSaldoAlimentoEngorde` devuelve `true` sólo para `INV_INGRESO`, `INV_TRASLADO_ENTRADA` e
`INV_TRASLADO_SALIDA`. **`INV_CONSUMO` queda fuera.**

**(c) El Reporte Contable la perdió al traducir al módulo unificado.**
`backend/src/ZooSanMarino.Application/Calculos/ReporteAlimentoInventarioCalculos.cs:64`:

```csharp
"Consumo" => CategoriaMovimientoAlimento.Retiro,
```

El módulo unificado **colapsó** `ConsumoSeguimiento` y el consumo manual en un único tipo `Consumo`,
y la traducción lo mandó a `Retiro` — la cubeta que en el modelo viejo significaba «salió de la
granja por otra razón», nunca «el ave comió». `AjusteStock` y `EliminacionStock` sí se excluyeron
(van a `Ninguna`); `Consumo` no.

> **La causa raíz no es una línea mal escrita: es que la señal «esto lo escribió un seguimiento»
> dejó de ser un TIPO y pasó a ser una convención de texto en `reference`.** Mientras el módulo viejo
> podía enforcar la regla con el enum, el unificado sólo la puede enforcar por acuerdo entre lectores
> — y un lector se olvidó. Es exactamente el fallo que la guía del repo nombra como
> «una sola fórmula por número».

### 2.1 Por qué `Consumo` es ambiguo hoy

`movement_type = 'Consumo'` lo escriben **cinco** caminos distintos:

| Escritor | Archivo | ¿Es «el ave comió»? |
|---|---|---|
| Seguimiento levante (Colombia, modelo A) | `SeguimientoLoteLevanteService.Crud.cs:108,135,267,313` | **sí** |
| Seguimiento levante (Ecuador/Panamá, modelo B) | `SeguimientoLoteLevanteService.Crud.cs` (`RegistrarConsumoAsync`) | **sí** |
| Seguimiento producción | `ProduccionService.Seguimiento.cs:260` | **sí** |
| Migración masiva de alimento engorde | `MigracionService.AlimentoEngorde.cs:396` | **sí** |
| Consumo manual desde la UI de inventario | `InventarioGestionController.cs:282` | **no** |
| Gastos de inventario | `InventarioGastoService.cs:571` | **no** |

Los que sí ponen `reference` con formato `«Seguimiento lote levante #<id> <fecha>»` o
`«Consumo diario levante - Lote <n>»`.

### 2.2 Cuánto pesa cada cosa (medido, BD local, `tipo_item = 'alimento'`)

| Empresa | `Consumo` de seguimiento | `Consumo` que NO es de seguimiento |
|---|---|---|
| 1 Sanmarino | 929 movs · 420.016,0 kg | **1** mov · 3.280,0 kg |
| 3 ItalcolEcuador | 5.377 · 8.335.288,0 kg | 0 |
| 4 Demo | 5 · 2.200,0 kg | 0 |
| 5 ItalcolPanama | 1.147 · 2.965.701,4 kg | 0 |

**7.458 de 7.459 (99,99 %)** de los movimientos `Consumo` **de alimento** los escribe un seguimiento.
Los `Consumo` del módulo de gastos existen (640 en Ecuador) pero son de **otros tipos de ítem**
(dosis, unidades: vacunas, medicamentos) y el filtro `tipo_item='alimento'` del reporte ya los deja
afuera.

---

## 3. Candidatos, con el número de cada uno

Medido con `backend/sql/verificar_kardex_bultos_por_lote_padre.sql` extendido; **`sin clamp`** es la
aritmética pura y **`clamp`** es lo que el reporte mostraría (`Math.Max(0m, …)` por día).

| Granja | Lote | hoy (clamp) | hoy sin clamp | **C1** | C2 granja (clamp) | C2 granja sin clamp |
|---|---|---|---|---|---|---|
| LA ESMERALDA | A374A 114 | 509,7 | **−17,8** | **518,2** | 4.324,9 | 674,4 |
| LA ESMERALDA | A374A 116 | 494,9 | **−1.490,7** | **518,2** | 4.324,9 | 674,4 |
| LA ESMERALDA | A374B 115 | 505,9 | **−610,7** | **518,2** | 4.324,9 | 674,4 |
| LA ESMERALDA | A374B 117 | 518,2 | 518,2 | **518,2** | 4.324,9 | 674,4 |
| MANGOS | S369A 142 | 0,0 | **−2.599,6** | **376,4** | 380,3 | 376,4 |
| MANGOS | S369A 144 | 376,4 | 376,4 | **376,4** | 380,3 | 376,4 |
| MANGOS | S369B 143 | 0,0 | **−2.644,7** | **376,4** | 380,3 | 376,4 |
| MANGOS | S369B 145 | 376,4 | 376,4 | **376,4** | 380,3 | 376,4 |

- **C1 — no restar el consumo del seguimiento.** El saldo queda `entradas − traslados − retiros`, o
  sea el kardex de la granja con el consumo restado **una sola vez** (desde el inventario, que es
  quien lo tiene fechado y valorizado).
- **C2 — no restar `retiros`.** El saldo restaría el consumo del seguimiento. Es lo que hace engorde.
- **C3 — deduplicar por `reference`**: restar sólo los `Consumo` que NO vienen de un seguimiento, más
  el consumo del seguimiento.

### 3.1 🔑 La clave que decide todo: **`retiros` y `consumo` están en GRANOS distintos**

| Término | Grano | Qué cubre |
|---|---|---|
| `retiros` | **la GRANJA** | en el módulo unificado son los `Consumo`, que escribe el seguimiento **de todos los lotes** de esa granja |
| `consumoHembras/Machos` | **ESTE lote padre** | sólo lo que comió esta familia de lotes |

De ahí salen las tres consecuencias, y las tres están medidas:

1. **Restar los dos** descuenta el consumo de este padre dos veces ⇒ el saldo real cae a −2.599,6 y el
   piso en 0 lo publica como «galpón vacío».
2. **Restar sólo `consumoH/M`** (lo que parecía el arreglo natural, y lo que engorde hace) **pierde el
   consumo de los otros padres**, que era justamente lo que `retiros` aportaba. Medido: el saldo se
   dispara a **3.730,2 · 2.257,3 · 3.137,3 · 4.266,2** y **deja de converger** (4 valores distintos, y
   MANGOS pasa de 2 a 3). **Es peor que el defecto.**
3. **Restar sólo `retiros`** es la única resta coherente al grano de la granja: **518,2**
   (LA ESMERALDA) y **376,4** (MANGOS), **un solo saldo por granja**.

### 3.2 Decisión: **C1, aplicado sólo a la rama unificada**

> En el módulo unificado, `retiros` **ya es** el consumo diario de la granja ⇒ el consumo del
> seguimiento no entra al saldo. En el módulo viejo, `retiros = Exit` **no** trae el consumo (va a
> `ConsumoSeguimiento`, excluido a propósito) ⇒ ahí restar `consumoH/M` es correcto y único, y **no se
> toca nada**.

Por eso el arreglo **no puede ser un cambio plano en `AcumularSaldos`**: es una decisión **por rama**,
y vive en una función pura (`DeltaDelSaldo`) que el service alimenta con el flag de la empresa.

**Lo que NO resuelve, y hay que decirlo:** el saldo pasa a ser un número **de granja** mostrado en un
reporte **por lote**. Sigue haciendo falta el aviso de V19.1 («este kardex es de la granja, no sumes
los reportes entre sí»), que ya existe. Lo que sí desaparece es la **contradicción**: los 4 padres de
una granja dejan de mostrar 4 saldos distintos del mismo kardex.

### 3.3 El camino equivocado, y por qué queda escrito

La primera implementación fue **C3** —excluir de `retiros` el `Consumo` escrito por un seguimiento,
detectándolo por el prefijo de `reference`— porque es lo que hace engorde y lo que el módulo viejo
resolvía con un tipo propio. **Compilaba, pasaba 2.922 tests, y estaba mal**: al calcular la
expectativa del smoke antes de correrlo, el saldo se disparaba y dejaba de converger. Lo que lo
delató no fue un test sino **calcular el número esperado antes de mirar el resultado**.

La lección concreta: *engorde puede restar el consumo del seguimiento porque su kardex es **por
galpón**, al mismo grano que el consumo. El Contable no puede, porque el suyo es **por granja** y el
consumo es por lote.* Copiar el patrón sin comparar los granos era el error.

### 3.4 Un hallazgo lateral, medido y NO corregido

Los dos escritores del mismo consumo **derivaron**: para los mismos días de LA ESMERALDA el inventario
dice **149.918,5 kg** y el seguimiento **146.952,5 kg** (74,2 bultos). En MANGOS la deriva es **0,0**
(239.886,2 de los dos lados). Son dos espejos del mismo hecho que ya no coinciden ⇒ por la regla del
repo uno tiene que ser el dueño y el otro el test. **Tiene su propio alcance.**

---

## 4. Alcance del cambio

### 4.1 Lo implementado (backend)

| Archivo | Cambio |
|---|---|
| `Application/Calculos/ReporteContableBultosCalculos.cs` | **`EsConsumoYaContabilizadoPorSeguimiento(reason, destination)`** — rama LEGACY, portado de `b853e95`. · **`DeltaDelSaldo(fila, retirosYaTraenElConsumo)`** — rama UNIFICADA. |
| `Infrastructure/Services/ReporteContableService.cs` | La query legacy descarta los `Exit` con la firma del seguimiento. · Resuelve el flag una vez por reporte y lo pasa a `CalcularSaldosAcumulativos`. |

### 4.2 Una decisión distinta por rama, y por qué

Las dos ramas duplican, con **firmas distintas**, y por eso el arreglo no puede ser el mismo:

| | rama LEGACY (`farm_inventory_movements`) | rama UNIFICADA (`inventario_gestion_movimiento`) |
|---|---|---|
| quién duplicaba | el **front**, con `postExit` (`reason='Consumo diario'` + `destination='Consumo'`) | el **backend**, con `movement_type='Consumo'` |
| cuánto | 252 movs / 131.278,3 kg (empresa 1) + 1 en Demo | 930 movs / 420.016,0 kg (empresa 1) |
| grano de `retiros` | mezcla: espejos del consumo + salidas reales | **la GRANJA entera** (todos los lotes) |
| arreglo | **excluir los espejos** de `retiros`; el consumo lo sigue aportando el seguimiento | **no restar el consumo del seguimiento**; `retiros` ya lo trae, y mejor (grano de granja) |

En legacy no se puede usar «`retiros` es el consumo» porque después del 10-jul el front dejó de
escribir: no habría nada que restar. En unificada no se puede usar «excluir los espejos» porque se
perdería el consumo de los otros padres. **Cada rama se arregla con la mejor fuente que tiene.**

### 4.3 Frontend

`AdvertenciaBultos` ya existe (V19.1). Se extiende para decir qué significa el saldo. Cero cambios de
componente más allá del texto.

### 4.4 Lo que NO entró, medido y por decisión

- **El recorte a 0 de `AcumularSaldos`.** Su doc dice que «el acumulador interno conserva el negativo»
  y **no lo conserva**: el carry entre días contiguos relee el valor ya recortado. Es un contrato
  incumplido. **No entra** porque con este arreglo el recorte deja de activarse en los lotes medidos
  (`sin clamp == con clamp` en los 9) ⇒ arreglarlo no cambia nada acá y sí movería la rama vieja sin
  medición que lo respalde.
- **`ObtenerSaldoAnteriorSemana` (V40.11).** El resumen semanal no arrastra el saldo entre semanas
  vacías (lote 114: 259,9 vs 509,7). **No entra**, y no es criterio propio: `b853e95` ya lo midió el
  8-ago y lo dejó afuera con número — arreglarlo hace que **72 encabezados** cambien y que 50 de las
  80 semanas del lote 13 dejen de mostrar 0. **Es una decisión de producto, no de este arreglo.**
- **El escritor del front** (`modal-seguimiento-engorde.component.ts:1833,1867`). Sigue posteando al
  kardex legacy. Sacarlo sin medir puede dejar a Colombia sin descuento.
- **La deriva entre los dos escritores** (§3.4): 74,2 bultos en LA ESMERALDA, 0,0 en MANGOS.

## 5. Gate obligatorio: esto toca cálculo compartido

Por CLAUDE.md §🛡️, todo cambio que mueva el saldo de alimento exige **paridad multipaís antes y
después**, en TODAS las empresas, no sólo en la que motivó el fix:

```bash
psql ... -f backend/sql/verificar_paridad_saldo_engorde.sql          # congela (antes)
psql ... -f backend/sql/verificar_kardex_bultos_por_lote_padre.sql   # congela (antes)
# ... cambio ...
psql ... -f backend/sql/verificar_paridad_saldo_engorde.sql          # compara (despues)
```

Toda empresa que no sea el objetivo tiene que salir con **0 en todas las columnas**.

**Y el A/B del flag** (el que exige `reportes-leen-inventario-viejo`): apagado → encendido → apagado,
y el tercero tiene que volver EXACTO al primero.

---

## 6. Casos de prueba (xUnit, `tests/ZooSanMarino.Application.Tests/`)

`ReporteAlimentoInventarioCalculosTests` (extender los 26 que ya hay):

| # | Caso | Esperado |
|---|---|---|
| T1 | `Consumo` con `reference` de seguimiento levante | `Ninguna` |
| T2 | `Consumo` con `reference` de seguimiento producción | `Ninguna` |
| T3 | `Consumo` con `reference` nula | `Retiro` (fail-open: no se pierde un retiro real) |
| T4 | `Consumo` con `reference` de gasto de inventario | `Retiro` |
| T5 | `Ingreso`, `TrasladoEntrada`, `TrasladoSalida` | sin cambio respecto de hoy |
| T6 | `AjusteStock`, `EliminacionStock` | `Ninguna`, sin cambio |

`ReporteContableBultosCalculosTests`:

| # | Caso | Esperado |
|---|---|---|
| T7 | Serie donde el consumo llega antes que las entradas | el saldo final NO depende del recorte |
| T8 | Serie sin días negativos | byte a byte idéntico al comportamiento de hoy |
| T9 | Semanas vacías en medio | `saldoBultosAnterior` arrastra el último saldo con datos |

**Gate de no-regresión:** los tests que hoy fijan el comportamiento con doble conteo tienen que
actualizarse **explícitamente**, uno por uno, con el número viejo y el nuevo escritos en el test.

---

## 7. Orden de trabajo

1. Congelar el baseline: los dos scripts de verificación, en las 5 empresas.
2. `ReporteAlimentoInventarioCalculos`: la decisión pura + sus tests. Verde antes de tocar nada más.
3. `AcumularSaldos`: el clamp (§4.4) + sus tests.
4. `ObtenerSaldoAnteriorSemana` (§4.5), commit aparte.
5. Advertencia del front (§4.3).
6. `dotnet build` + `dotnet test` + `yarn build`.
7. Smoke contra el endpoint real en los 8 lotes padres (el mismo de V40.14) + gate de paridad.
8. A/B del flag por empresa.
