# Requerimiento — Cuadre de alimento y aves en pollo engorde (ItalcolEcuador)

**Fecha:** 2026-07-29 · **Empresa:** ItalcolEcuador (`companies.id = 3`) · **Estado:** NO iniciado
**Antecedente:** el mismo trabajo ya se hizo y desplegó para **ItalcolPanama**.
Plan de referencia: [`cuadre_engorde_panama_aves_alimento_plan.md`](cuadre_engorde_panama_aves_alimento_plan.md)

> Este documento es **autocontenido**: incluye el diagnóstico ya medido, el patrón que funcionó en
> Panamá y las diferencias de Ecuador. No hace falta re-diagnosticar desde cero, pero **sí volver a
> medir sobre un dump fresco** antes de tocar nada: los números de acá son del 2026-07-29.

---

## 1. Qué se pide

Que en cada lote de pollo engorde de Ecuador:

1. El **saldo de alimento** del seguimiento diario coincida con el stock de **Gestión de inventario**.
2. El **saldo de aves** de la tabla de seguimiento coincida con el widget **«Aves disponibles»**.
3. El **Reporte Diario de Costos** muestre el stock real por alimento (hoy Ecuador sigue leyendo el
   snapshot jsonb, que está incompleto).

---

## 2. Estado medido (2026-07-29, sobre dump de producción)

### 2.1 Alimento — descuadre por granja

`saldo_lógico = ingresos − consumo del seguimiento` vs `stock de inventario`:

| Granja | Ingresos | Consumo | Saldo lógico | Stock inventario | Descuadre |
|---|---:|---:|---:|---:|---:|
| SAN GUILLERMO | 1.554.529,3 | 1.263.120,0 | 291.409,3 | 85.091,2 | **+206.318,1** |
| Kilometro 86 | 1.375.518,4 | 1.101.302,0 | 274.216,4 | 101.232,0 | **+172.984,4** |
| Kilometro 22 | 1.332.413,7 | 1.187.199,0 | 145.214,7 | 33.937,8 | **+111.276,9** |
| BODEGA PRINCIAL KM 86 | 555.093,1 | 0,0 | 555.093,1 | 639.514,1 | **−84.421,0** |
| CAROLINA | 1.260.521,8 | 1.037.000,0 | 223.521,8 | 152.895,0 | **+70.626,8** |
| Sacachun 3b | 1.212.920,2 | 1.149.140,0 | 63.780,2 | 50.161,5 | +13.618,7 |
| Sacachun 2 | 1.098.527,8 | 1.086.130,0 | 12.397,8 | 11.223,0 | +1.174,8 |
| Kilometro 61 | 761.248,4 | 673.830,0 | 87.418,4 | 86.819,4 | +599,0 |
| Sacachun 3A | 265.240,8 | 257.670,0 | 7.570,8 | 7.406,8 | +164,0 |
| Bodega Principal | 17.915,5 | 0,0 | 17.915,5 | 17.915,5 | 0,0 |

Otros datos:
- Consumo del inventario **7.639.771,9 kg** vs consumo del seguimiento **7.755.391,0 kg** → desfase **115.619,1 kg**
- **196 movimientos `AjusteStock`** por 1.284.742,8 kg: la operación ya viene compensando a mano
- **1.447.399,0 kg de ingresos SIN galpón** (bodega de granja) en 10 granjas, contra 7.986.530,0 kg con galpón

### 2.2 Aves

- **0 lotes** con bajas de los 7 días del cruce sin aplicar ⇒ **Ecuador NO tiene el bug del cruce**
  que sí tenía Panamá. Esa parte no hay que replicarla.
- **158.109 bajas sin aplicar** al maestro en los 103 lotes con seguimiento. Ojo: en su mayoría es la
  cohorte **anterior** al descuento automático, que por diseño nunca movió el maestro y conserva la
  fórmula previa (ver §4.1). Hay que separar lo legítimo de lo que falta aplicar.
- **11 lotes con la conservación rota** (`aves_encasetadas − bajas − ventas ≠ maestro`). En Panamá
  eran 0. **Estos 11 son el punto de partida del análisis de aves.**

---

## 3. Diferencias con Panamá (lo que NO se puede copiar tal cual)

| # | Diferencia | Consecuencia |
|---|---|---|
| D1 | **1.447.399 kg de ingresos sin galpón** (bodega de granja) | El saldo por galpón no los ve. Hay que decidir si la bodega de granja entra al cuadre o se trata aparte. En Panamá TODOS los ingresos tienen galpón, por eso no apareció. |
| D2 | **`tipo_alimento` viene con prefijo de sexo** (`"H: AV. SUPER POLLO ENGORDE"`) en los 4.638 registros | No cruza con `item_inventario_ecuador.nombre`. Por eso el flag del Reporte de Costos quedó OFF en Ecuador. Hay que normalizar el nombre (quitar `H: `/`M: `) antes de activarlo. |
| D3 | **295 días con `tipo_alimento` compuesto** (`"A / B"`) | Solo el jsonb tiene el reparto real, y no siempre. Mismo problema que en Panamá pero 8× más grande. |
| D4 | **Galpones con ciclos SUCESIVOS** (3-4 lotes por galpón, uno detrás de otro) | ⚠️ Ver §5, gotcha crítico. |
| D5 | **196 AjusteStock ya cargados** (1.284.742,8 kg) | Igual que en Panamá, la operación compensó a mano. **No asumir que el stock manual es la verdad** sin verificar (en Panamá esa premisa resultó falsa). |
| D6 | Ecuador **no tiene** el bug del cruce en aves | La migración `20260729100000` no aplica acá. |

---

## 4. Qué se hizo en Panamá (patrón a evaluar, no a copiar ciegamente)

Todo desplegado en producción el 2026-07-29 (PR #56 y #57).

### 4.1 Aves — el descuadre era de CÓDIGO, no de datos

`GetAvesDisponiblesAsync` partía del maestro `hembras_l/machos_l` —que `RetiroAvesEngordeAplicador`
**ya descuenta**— y le volvía a restar la mortalidad acumulada.

**Fix:** `Application/Calculos/AvesDisponiblesEngordeCalculos.cs` resta solo las bajas **pendientes**,
medidas por las filas `BAJA_SEGUIMIENTO` del histórico. Los lotes viejos no tienen esas filas ⇒ su
pendiente es el total ⇒ conservan la fórmula previa. **Ese fix ya está en producción y aplica a
Ecuador también** — verificar si con él los 11 lotes de conservación rota se explican solos.

> **Regla aprendida:** antes de tocar datos para «cuadrar» aves, verificar la conservación
> `aves_encasetadas − bajas_aplicadas − ventas = maestro`. Si se cumple, el bug está en la fórmula,
> no en los datos, y forzar el maestro destruiría datos sanos.

### 4.2 Alimento — el saldo mezclaba scopes

Los ingresos se leían con scope **galpón** y el consumo con scope **lote**. Con dos lotes conviviendo,
cada uno veía todos los ingresos y solo su consumo. Corregido en `fn_seguimiento_diario_engorde` **v10**
(CTE `consumo_galpon_por_fecha`) y en los dos services de C#. **Ya está en producción y aplica a
Ecuador**, pero es no-op allá porque no tiene lotes solapados.

### 4.3 Alimento — la causa raíz de datos

**El inventario nunca descontó el consumo de los 7 días del cruce.** Verificado al decimal en 19 de 25
galpones. Consecuencia: **el stock estaba inflado, no el seguimiento**, y el valor correcto es el
lógico (`ingresos − consumo`).

⚠️ **En Ecuador esta causa NO aplica** (no tiene días de cruce sin aplicar). Su desfase de
115.619,1 kg entre consumo de inventario y de seguimiento **tiene otro origen y hay que encontrarlo
antes de proponer nada**. Empezar comparando día a día un galpón descuadrado, como se hizo en
Panamá — ahí el desfase estaba concentrado en los primeros días del lote.

### 4.4 Reporte de Costos

`fn_reporte_diario_costos_engorde` **v3**: consumo del seguimiento + stock de ingresos − consumo.
Gobernado por el flag `companies.reporte_costos_alimento_desde_fuentes_reales`, **ON solo en
ItalcolPanama**. Para activarlo en Ecuador hay que resolver antes D1, D2 y D3.

---

## 5. Gotchas críticos (costaron tiempo en Panamá)

1. 🔴 **Ciclos sucesivos vs lotes que CONVIVEN.** Al pasar el consumo a scope galpón, hay que
   restringirlo a lotes con **rangos de seguimiento solapados**. Sin ese filtro se rompen 22 lotes de
   Ecuador (1.037 filas): sus galpones encadenan 3-4 ciclos que no comparten bodega, y como los lotes
   viejos quedan en `Abierto` (`fecha_max` NULL) la fn les sigue mostrando fechas posteriores.
   Ya está resuelto en la v10 — **no romperlo**.

2. 🔴 **Los `AjusteStock` son invisibles para el seguimiento.** Entran al histórico como
   `tipo_evento = 'INV_OTRO'` y ningún cálculo del saldo mira ese tipo. Además
   `Quantity = Math.Abs(delta)` **pierde el signo**, recuperable del `reason`
   (`"Anterior: X. Nuevo: Y."`).
   ⚠️ **Propagarlos al seguimiento doble-descuenta** (en Panamá la simulación cayó a 2/25 galpones).

3. 🔴 **Identidad verificada del stock** (error 0,0 en los 24 galpones de Panamá):
   `stock = Σ Ingreso − Σ Consumo_inventario + Σ AjusteStock(con signo)`.
   Sirve para validar cualquier hipótesis.

4. 🔴 **Dos filtros, no uno.** Al calcular un saldo que deba coincidir con la pantalla hay que replicar
   **ambos** filtros de `fn_seguimiento_diario_engorde`: `'Seguimiento aves engorde #%'` **y**
   `'%devolución por eliminación%'`. Olvidar el segundo dejó un galpón con 590 kg de diferencia.

5. 🔴 **Detección de carga masiva duplicada:** la marca de la corrida está en
   `lote_registro_historico_unificado.created_at`, **NO** en `inventario_gestion_movimiento.created_at`
   (ese guarda la fecha de operación). Buscar «duplicados exactos» por (fecha, ítem, cantidad) es
   **peor que inútil**: en Panamá no cubría el caso real y marcaba como duplicados dos ingresos
   legítimamente iguales.

6. 🔴 **Fechas del cruce a medianoche UTC** (19:00-05). Las filas `BAJA_SEGUIMIENTO` usan
   `(fecha AT TIME ZONE 'UTC')::date`; `fecha::date` a secas depende de la zona de la sesión y corre
   el día uno atrás.

7. ⚠️ **El jsonb `historico_consumo_alimento` está incompleto**: suma 1.554.181,4 kg contra
   1.706.089,8 kg de consumo real. No usarlo como fuente de totales.

---

## 6. Enfoque sugerido

**Fase 0 — Diagnóstico (obligatoria, no saltear).**
1. Refrescar la BD local desde un dump de producción.
2. Re-medir todo lo de §2: los números de este documento envejecen.
3. **Encontrar el origen del desfase de 115.619,1 kg** entre consumo de inventario y de seguimiento.
   Método que funcionó: elegir un galpón muy descuadrado y comparar **día a día**
   `consumo del seguimiento` vs `INV_CONSUMO` del inventario; el desfase suele concentrarse en un
   tramo identificable.
4. Decidir qué hacer con la **bodega de granja** (1.447.399 kg sin galpón): ¿entra al cuadre o va aparte?
5. Analizar los **11 lotes con conservación rota** en aves.

**Fase 1 — Validar hipótesis sin tocar datos.** Simular cada estrategia y medir cuántas granjas
cuadran, como se hizo en Panamá (`scratchpad/sim_final.sql`). En Panamá esto descartó 3 de 4
estrategias, incluida una que **empeoraba** el resultado.

**Fase 2 — Corrección**, con el usuario decidiendo qué manda (saldo lógico vs stock manual). En Panamá
la respuesta fue **el saldo lógico**, y hubo que ajustar el inventario.

**Fase 3 — Reporte de Costos**: normalizar el nombre (quitar `H: `/`M: `), resolver los compuestos y
recién ahí activar el flag `reporte_costos_alimento_desde_fuentes_reales` en Ecuador.

---

## 7. Criterios de aceptación

- [ ] Saldo de alimento del seguimiento == stock de Gestión de inventario en todas las granjas (tol. 1 kg)
- [ ] Saldo de aves de la tabla == «Aves disponibles» en todos los lotes con los 7 días completos
- [ ] Conservación `aves_encasetadas − bajas − ventas = maestro` intacta en los 103 lotes
- [ ] **Panamá sin cambios**: comparación fila a fila antes/después con 0 diferencias
- [ ] Migraciones **idempotentes** y con `Down` probado (una 2ª corrida no debe mover nada)
- [ ] `dotnet build` 0/0 · `dotnet test` verde (hoy son 1341 tests)
- [ ] Verificación post-deploy contra ECS (TaskDef, imagen == SHA, `HEALTHY`, steady state)

---

## 8. Archivos de referencia

| Qué | Dónde |
|---|---|
| Plan y diagnóstico de Panamá | `fase_de_desarrollo/cuadre_engorde_panama_aves_alimento_plan.md` |
| Bitácora detallada | `tracker_estado.md` (bloque «Cuadre de aves y alimento en pollo engorde (Panamá)») |
| Fn del seguimiento (v10) | `backend/sql/fn_seguimiento_diario_engorde.sql` |
| Fn del reporte (v3) | `backend/sql/fn_reporte_diario_costos_engorde.sql` |
| Cálculo puro de aves | `backend/src/ZooSanMarino.Application/Calculos/AvesDisponiblesEngordeCalculos.cs` |
| Migración de bajas del cruce | `…/Migrations/20260729100000_AplicarBajasCruceReproductoraAlMaestroEngorde.cs` |
| Migración de cuadre (Panamá) | `…/Migrations/20260729120000_CuadreAlimentoEngordePanama.cs` |
| Migración del flag del reporte | `…/Migrations/20260729224401_ReporteCostosAlimentoDesdeFuentesReales.cs` |

**Commits:** `21e53ab`, `2cc4855`, `2f58e22`, `a050ec7`, `9a753ea` · **PRs:** #56 y #57

> ⚠️ Al escribir en `tracker_estado.md`: hay varias sesiones trabajando el mismo repo. Agregá tu
> bloque **AL FINAL**, nunca borres lo de otra sesión.
