# Fase 3 de R2 — señalar el alimento que queda cuando se liquida un lote de engorde

**Origen:** [`marca_proximo_ciclo_rediseno_plan.md`](marca_proximo_ciclo_rediseno_plan.md) §R2 y
§«FASE 3 — SEÑALAMIENTO DE LA ANOMALÍA (R2)», y el pendiente abierto del tracker
(«Fase 3 — señalamiento de la anomalía R2. Sigue vivo y es independiente de la v16»).
**Fecha:** 2026-08-17 · **Empresas con engorde:** ItalcolEcuador y ItalcolPanama.

> **La regla que dio origen a R2 (dueño del producto):** al liquidar un lote el galpón tiene que
> quedar en **cero**; el procedimiento operativo es trasladar el sobrante fuera del galpón. «Lote
> liquidado con alimento pendiente» **no** se modela con guardas: es una **anomalía que el sistema
> debe SEÑALAR**. Esta fase construye ese señalamiento — no cambia ni un número del cálculo.

---

## 1. Diagnóstico revalidado contra la BD (17-ago-2026, dump tipo prod en local)

| hecho | dato del plan (jul-2026) | **medido hoy** |
|---|---|---|
| Liquidaciones congeladas vigentes | 84 | **90** (todas ItalcolEcuador) |
| …que congelaron con `saldo_alimento_kg > 0` | 24 (28,6 %) · 111.821 kg | **28** · **137.521 kg** |
| …copias de backfill con el saldo en NULL | — | **20** (no se les puede inventar un número) |
| `GET /api/CuadreAlimentoEngorde` con consumidores en el front | 0 | **0** (revalidado) |

**El falso positivo del aviso de liquidación también creció.** El modal cae a stock de **núcleo**
cuando el galpón no tiene filas de alimento (`modal-liquidacion-lote-engorde.component.ts:375-383`) y
ahí muestra los kilos de los galpones vecinos:

| empresa / estado | lotes con galpón | **con falso positivo** | kg ajenos que muestra |
|---|---|---|---|
| ItalcolEcuador · Abierto | 31 | **4** | 124.810 |
| ItalcolEcuador · Cerrado | 90 | **10** | 318.605 |
| ItalcolPanama · Abierto | 65 | **1** | 77.737 |

🔑 **Dato que decide el fix:** hoy **ninguna** granja guarda el alimento de engorde a nivel núcleo.
Ecuador y Panamá lo tienen **por galpón** (136 y 85 filas de stock); Sanmarino y Demo lo tienen a
**nivel granja** (núcleo y galpón vacíos, 23 y 6 filas). O sea: el fallback a núcleo **solo puede
traer kilos ajenos**, pero el stock sin galpón (nivel núcleo/granja) **sí es del lote** y no se puede
borrar sin romper a las empresas que manejan el alimento a nivel granja
(`maneja_alimento_por_galpon`, patrón `farm ?? company`).

---

## 2. Alcance — qué de la Fase 3 entra y qué no (y por qué)

| ítem del plan original | decisión |
|---|---|
| **F3.1** columna `marcado_no_diferible_kg` en `fn_cuadre_alimento_engorde` | ❌ **Sin objeto.** Dependía de `fn_alimento_marcado_atribucion`, borrada en la reversión de la ronda 4 (verificado: el archivo no existe). Además hay **0 movimientos marcados** en la BD y la puerta de entrada de la marca está cerrada en la UI. |
| **F3.1** columna `liquidado_con_saldo_kg` en esa misma fn | ❌ **No como columna de esa fn.** Cambiar su `RETURNS TABLE` obliga a `DROP FUNCTION` sobre una fn que leen **5 consumidores** y dispara el **gate multipaís**, y el número es de **otro grano** (por lote liquidado, no por galpón activo). ✅ Entra como **endpoint propio** que lee la foto congelada donde el número ya está escrito. |
| **F3.2** reporte «lotes liquidados con alimento sin trasladar» | ✅ **Entra** |
| **F3.3** falso positivo del aviso de liquidación (fallback a núcleo) | ✅ **Entra** — es el único detector de R2 que hoy ve un humano, y miente |
| **F3.4** exponer `GET /api/CuadreAlimentoEngorde` en el front | ✅ **Entra**, en la misma pantalla que F3.2 |

**Ninguna función SQL se toca** ⇒ no aplica el gate multipaís de cálculo compartido. La verificación
igual mide que `fn_cuadre_alimento_engorde` siga dando lo mismo (61 filas, 1 descuadrado preexistente).

---

## 3. F3.3 — el aviso de liquidación deja de contar kilos ajenos

**Hoy:** `cargarStockAlimento()` pide el stock del galpón; si no hay filas con cantidad > 0 y el lote
tiene galpón, repite la consulta **sin galpón** y usa ese resultado como si fuera del galpón. El hint
`stockUsandoFallbackUbicacion` avisa en letra chica, pero el número grande («Alimento disponible
(inventario galpón)»), el aviso «Hay alimento en inventario. Puede realizar traslado…», el botón
**Realizar traslado** y la comparación `inventarioDifiereDelSeguimiento` siguen leyendo
`totalKgStockInventario`, que ya son kilos de otros galpones.

**Fix — partir el resultado del fallback por ubicación, no borrarlo:**

- filas **sin galpón** (`galponId` nulo/vacío) ⇒ stock de nivel núcleo/granja: **son de este lote**,
  cuentan igual que hoy (empresas con `maneja_alimento_por_galpon = false`);
- filas de **otro galpón** ⇒ **kilos ajenos**: se muestran aparte y rotulados, y **no** alimentan el
  número principal, ni el aviso, ni el botón, ni la comparación contra el saldo del seguimiento.

Cálculo **puro** nuevo (front): `funciones/separar-stock-por-ubicacion.funcion.ts` →
`{ propias, ajenas, kgPropias, kgAjenas }`, con su `.spec.ts`.

---

## 4. F3.2 + F3.4 — el reporte y la pantalla

### 4.1 Backend

| archivo | qué |
|---|---|
| `Application/Calculos/AnomaliaAlimentoLiquidadoCalculos.cs` | **nuevo** · puro: `KgSinTrasladar`, `KgSinRespaldo`, `Clasificar`, `Describir`. Tolerancia **1 kg**, la misma de `CuadreAlimentoEngordeCalculos` |
| `Application/DTOs/AnomaliaAlimentoLiquidadoDto.cs` | **nuevo** · fila + resumen |
| `Application/Interfaces/ICuadreAlimentoEngordeService.cs` | + `ObtenerLiquidadosConAlimentoAsync(bool soloAnomalias, CancellationToken)` |
| `Infrastructure/Services/CuadreAlimentoEngordeService.Anomalias.cs` | **nuevo** · `partial class`, LINQ que traduce a SQL (la BD filtra), empresa efectiva **fail-closed** con el mismo resolver del cuadre |
| `API/Controllers/CuadreAlimentoEngordeController.cs` | + `GET /api/CuadreAlimentoEngorde/liquidados-con-alimento` |

**Sin `SqlQueryRaw`**: se consulta con LINQ sobre entidades ya mapeadas
(`LiquidacionLoteEngordeCongelada`, `LoteAveEngorde`, `SeguimientoDiarioAvesEngorde`,
`LoteRegistroHistoricoUnificados`, `InventarioGestionStock`, `ItemInventario`). Evita de raíz las dos
trampas conocidas (nombre de columna con dígito; `DateOnly` en proyección) y lo deja verificado por el
compilador. **Igual hay que EJECUTAR el endpoint antes de mergear** — un test puro no ve esos errores.

### 4.2 Las reglas que deciden el número (una sola fórmula por número)

- `salidasPostKg` = `INV_TRASLADO_SALIDA` no anulados del galpón con
  `fecha_operacion > último día de seguimiento del lote` ⇒ **exactamente el criterio de `mov_post` de
  `fn_cuadre_alimento_engorde`**: lo que se movió después del último seguimiento no cabe en la foto.
- `kgSinTrasladar = max(0, saldoCongeladoKg − salidasPostKg)` — lo que la liquidación dejó y nunca
  salió del galpón por un traslado.
- `kgSinRespaldoKg = max(0, kgSinTrasladar − stockGalponHoyKg)` — kilos que la foto reclama y que ya
  **no existen** en el galpón: se los consumió otro ciclo. Es el «fantasma contable» que el gate de la
  v16 encontró en 43/G0055.
- **Estados** (de menor a mayor severidad):

  | estado | condición | lectura |
  |---|---|---|
  | `Trasladado` | `kgSinTrasladar <= 1 kg` | se siguió el procedimiento; informativo |
  | `PendienteEnGalpon` | queda saldo y el **stock lo respalda** | el sobrante sigue físicamente ahí: trasladarlo o dejar que lo tome el ciclo siguiente |
  | `SinRespaldoFisico` | `kgSinRespaldoKg > 1 kg` | la foto reclama kilos que ya no están: **anomalía** |

- Solo entran copias **vigentes** (`anulada_at IS NULL`): reabrir un lote anula la copia y la fila sale
  del reporte, que es lo correcto.
- Copias de **backfill** con `saldo_alimento_kg` NULL (20 hoy): **no se les inventa un saldo**; se
  cuentan aparte en el resumen (`sinDatoCongelado`) para que el 28/90 no se lea como 28/70.
- Contexto accionable por fila: **ciclo siguiente** del galpón (lote, nombre, encaset) si existe.

### 4.3 Front

| archivo | qué |
|---|---|
| `features/gestion-inventario/services/cuadre-alimento-engorde.service.ts` | **nuevo** · los 2 GET |
| `features/gestion-inventario/components/cuadre-alimento-engorde/` | **nuevo** · componente con los 2 paneles (galpones que no cuadran · liquidados con alimento sin trasladar), `changeDetection: Eager` |
| `pages/gestion-inventario-page/…` | + tab `cuadre` (host delgado, `?tab=cuadre`), sin lógica propia |

Sin menú nuevo ⇒ **no hace falta tocar `menus` / `role_menus` / `company_menus`** ni un paso manual
post-deploy: quien ya entra a Gestión de Inventario ve el tab. Empresas sin engorde ven el estado
vacío explicado (el endpoint devuelve 0 filas), sin `if (empresa == X)`.

---

## 5. Casos de prueba (xUnit, cálculo puro)

| # | caso | espera |
|---|---|---|
| T1 | saldo 3.000, salidas 3.000, stock 0 | `Trasladado`, sinTrasladar 0, sinRespaldo 0 |
| T2 | saldo 3.000, salidas 0, stock 5.000 | `PendienteEnGalpon`, sinTrasladar 3.000, sinRespaldo 0 |
| T3 | saldo 15.540, salidas 0, stock 9.980 (43/G0055 real) | `SinRespaldoFisico`, sinRespaldo 5.560 |
| T4 | saldo 3.000, salidas 0, stock 0 | `SinRespaldoFisico`, sinRespaldo 3.000 |
| T5 | saldo 3.000, salidas 2.999,5 | `Trasladado` (dentro de la tolerancia de 1 kg) |
| T6 | saldo 3.000, salidas 4.000 (salió más de lo que decía la foto) | `Trasladado`, sinTrasladar 0 (nunca negativo) |
| T7 | `Describir` nombra los kilos y qué hacer, en el idioma de la operación | texto por estado |
| T8 | orden de severidad `Trasladado < PendienteEnGalpon < SinRespaldoFisico` | el resumen ordena por severidad |

Front: `separar-stock-por-ubicacion.funcion.spec.ts` — galpón propio / otro galpón / sin galpón /
mezcla / lista vacía.

---

## 6. Verificación

1. `dotnet build` (0 errores) + `dotnet test` (verde, con los T1-T8).
2. `yarn build` (0 errores; único warning aceptado, el de bundle budget).
3. **Smoke ejecutando los 2 endpoints** con sesión de ItalcolEcuador: el reporte tiene que devolver
   las **28** filas medidas arriba. Los dos endpoints son de **solo lectura** ⇒ no hace falta clonar
   la BD. ⚠️ El cuadre se scopea a la **empresa activa**, así que devuelve los galpones de Ecuador
   (36 hoy), no las 61/66 filas que da `fn_cuadre_alimento_engorde(NULL)` sobre todas las empresas.
4. **Ninguna fn SQL tocada** ⇒ `git diff backend/sql` vacío. La línea base del cuadre se **re-mide**
   en el momento (la BD local se mueve entre sesiones); lo que se verifica es que este cambio no la
   toca, no que siga dando el número escrito en julio.
5. Smoke del modal de liquidación en un lote con el falso positivo (Ecuador, galpón sin alimento y
   núcleo con alimento): el número grande queda en **0 kg**, el botón «Realizar traslado» **no**
   aparece y los kilos ajenos se ven rotulados como de otros galpones.
6. Clon dropeado · puertos libres al terminar.

---

## 7. Fuera de alcance (dicho)

- No se toca `fn_cuadre_alimento_engorde` ni ninguna otra función SQL.
- No se toca la marca `para_proximo_ciclo` ni su rediseño (sigue congelado por el NO-GO de la ronda 4).
- No se **bloquea** la liquidación con alimento pendiente: la regla del dueño del producto es
  **señalar**, no impedir. `puedeLiquidarPorAves` sigue como está.
- No se corrige ningún dato histórico: los 28 lotes ya congelados con saldo quedan como están; el
  reporte los pone a la vista para que operación decida.
