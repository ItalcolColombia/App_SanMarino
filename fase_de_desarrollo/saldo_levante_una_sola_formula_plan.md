# Saldo de aves de levante — cuatro consumidores, dos fórmulas

**Fecha:** 2026-08-17 · Origen: hallazgo abierto del bloque ItalJira («Tres fórmulas distintas para el
saldo de levante»), re-verificado hoy contra el código y **medido en datos**.
**Invariante que se está violando:** CLAUDE.md § «Una sola fórmula por número».

---

## 1. El estado real (peor que lo anotado: son cuatro, no tres)

| Consumidor | ¿Descuenta ventas? | Dónde |
|---|---|---|
| `fn_reporte_semanal_levante_extras` | **SÍ** | `aves_*_fin` |
| `fn_resumen_semanal_ra_pesadas_levante` | **SÍ** — comentario propio: *«el saldo tiene que descontarla o el reporte sobrestima el lote»* | `saldo_hembras/machos` |
| `fn_indicadores_levante_postura` | **NO** | `r_aves_fin := v_aves_acum − mort − sel − err − tras_sal + tras_ing` (línea 396) |
| `ReporteTecnicoService` | **NO** | 4 call sites construyen `MovimientoDia(mort, sel, err, trasSal, trasIng)` — `Venta` queda en su default `0` |

`SaldoAvesLevanteCalculos` —la especificación ejecutable— **sí** contempla la venta
(`BajasNetas = mort + sel + err + salidas + ventas + retiros − ingresos`), pero **su único
consumidor nunca se la pasa**. O sea: la spec está bien y nadie la usa completa.

## 2. La divergencia es visible HOY, no es un riesgo futuro

Dos pantallas, el mismo lote, la misma semana, dos conteos de aves (Sanmarino, empresa 1):

| Lote | Semana | Indicadores (sin venta) | Reporte semanal (con venta) | Diferencia |
|---|---|---|---|---|
| 143 | 23 | 10.626 | 10.476 | **150** |
| 143 | 24 | 10.619 | 10.329 | **290** |
| 142 | 24 | 10.646 | 10.450 | **196** |

La diferencia es **exactamente** la venta acumulada. Sólo 2 lotes tienen ventas registradas hoy
(143: 290 aves en 2 filas; 142: 196 en 1), y en ambos `venta_aves_cantidad` coincide con
`venta_aves_hembras + venta_aves_machos`. Por eso «no se notaba»: no porque no pase, sino porque
casi nadie registró ventas de levante todavía. En cuanto se registren de verdad, cada lote vendido
muestra dos saldos distintos según la pantalla.

## 3. Quién manda

`fn_reporte_semanal_levante_extras` y la spec en C#. El sentido no está en discusión: **una ave
vendida sale del lote**; no contarla infla el saldo y, en cascada, **subestima el consumo por ave**
(mismo mecanismo que ya corrigió el error de sexaje en su momento, documentado en
`SaldoAvesLevanteCalculos`).

## 4. Alcance de esta entrega

**Entra:** `fn_indicadores_levante_postura` descuenta la venta, en el mixto y por sexo, con la misma
convención que el resto de la fn (el total mixto se arma como `h + m`, igual que `mort`, `sel`,
`err`, `tras_sal` y `tras_ing`).

**No entra (se deja anotado con su evidencia):** `ReporteTecnicoService`. Alimenta el Reporte Técnico
Semanal, que es una salida que operación y costos ya leen; moverle el saldo pide su propia
verificación contra el informe impreso. Se documenta el hallazgo, no se toca.

### Archivos

| Archivo | Cambio |
|---|---|
| `backend/sql/fn_indicadores_levante_postura.sql` | `venta_h`/`venta_m` en el `base` CTE, `venta` en el proyectado, agregación por semana y resta en las 3 líneas de saldo |
| `backend/src/ZooSanMarino.Infrastructure/Migrations/<ts>_FnIndicadoresLevanteDescuentaVenta.cs` (+ `.Fn.cs`) | `CREATE OR REPLACE` (la firma no cambia) con el cuerpo nuevo; `Down()` = cuerpo actual VERBATIM |
| `backend/tests/.../SaldoAvesLevanteCalculosTests.cs` | test que fija que la venta es una baja como cualquier otra |

## 5. Casos de prueba

| # | Caso | Esperado |
|---|---|---|
| T1 | `BajasNetas` con venta > 0 | la venta suma a las bajas |
| T2 | Venta y traslado de salida por la misma cantidad | mismo efecto sobre el saldo (los dos sacan aves del lote) |
| P1 | **Paridad**: `fn_indicadores_levante_postura` vs `fn_reporte_semanal_levante_extras`, TODOS los lotes, antes y después | antes: 3 filas con diferencia (142 y 143) · después: **0** |
| P2 | Lotes **sin** ventas | **0 filas cambiadas** — el arreglo no puede mover un solo número donde no hubo venta |
| P3 | El resto de las columnas de la fn (peso, uniformidad, consumo, % mortalidad) | intactas en todos los lotes |
