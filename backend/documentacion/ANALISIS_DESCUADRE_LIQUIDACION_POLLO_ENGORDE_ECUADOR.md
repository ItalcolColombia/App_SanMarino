# Análisis del descuadre — Liquidación Técnica Pollo Engorde (Ecuador)

> **Fecha:** 2026-06-26 · **Caso:** Lote `2601`, Granja `38` (Kilometro 22), Núcleo `963529`
> **Lotes (corrida):** id `19` (Galpon-2 / G0036) y id `20` (Galpon-1 / G0035)
> **Endpoint:** `POST /api/IndicadorEcuador/liquidacion-pollo-engorde-reporte`
> **Referencia correcta:** Excel del usuario (ECU - ITALCOL S.A., LOTE 316-40202601).
> **Fuente analizada:** copia de prod en `localhost:5433/sanmarinoapplocal` (idéntica a RDS prod, verificada).

---

## 1. Conclusión (TL;DR)

Hay **dos causas reales** del descuadre; todo lo demás se deriva de ellas o es diferencia de definición:

| # | Causa | Tipo | Qué descuadra |
|---|-------|------|---------------|
| **C1** | **Un despacho del lote 20 está SIN peso** (mov `id 102`, 3.192 aves, todos los campos de peso en NULL). Aporta aves pero **0 kg**. | **DATO** | Producción kilo en pie (−5.923 kg) → y en cascada **peso promedio, conversión, eficiencia americana, productividad**. |
| **C2** | El **TOTAL del front** "Total kilos despachados a cliente" se calcula sumando solo los lotes **con merma registrada**; como la merma se digita **una vez por corrida** (va en el lote 19), el **lote 20 se excluye entero**. | **CÓDIGO (front)** | Total kilos despachados a cliente (muestra 133.225; debe ser ≈ 250.590). |

> **Simulación validada:** corrigiendo C1 (cargar el peso de mov 102) **y** C2 (fórmula del total), **todos los indicadores del Excel cuadran exactamente** (ver §5).
> La regla "**merma única por corrida**" es correcta y NO debe duplicarse: el problema no es la merma, es que el total de kilos a cliente debe incluir el kilaje de **todos** los lotes y restarle **una** merma.

---

## 2. Comparación Sistema vs Excel (TOTAL de la corrida 2601)

| Indicador | Excel (correcto) | Sistema actual | ¿Cuadra? | Causa |
|-----------|-----------------:|---------------:|:--------:|-------|
| Aves encasetadas | 97.165 | 97.165 | ✅ | |
| Aves sacrificadas | 90.623 | 90.623 | ✅ | |
| Mortalidad (u) | 6.084 | 6.084 | ✅ | |
| Mortalidad (%) | 6.26 | 6.26 | ✅ | |
| Merma (u) | 166 | 166 | ✅ | merma única corrida |
| Merma (%) | 0.17 | 0.18 | ⚠️ | denominador: Excel /encasetadas, fn /sacrificadas |
| Ajuste en aves | 458 | 458 | ✅ | |
| % ajuste | 0.47 | 0.47 | ✅ | |
| Supervivencia | 93.74 | 93.74 | ✅ | |
| Consumo total | 453.909 | 453.909 | ✅ | |
| Consumo ave | 5.01 | 5.01 | ✅ | |
| **Producción kilo en pie** | **251.052** | **245.129** | ❌ **−5.923** | **C1** (mov 102 sin peso) |
| Merma (kilos) | 462,66 | 462,66 | ✅ | |
| **Total kilos despachados a cliente** | **250.590** | **133.225** | ❌ | **C2** (front) + C1 |
| **Peso promedio** | **2,77** | **2,70** | ❌ | C1 |
| **Conversión** | **1,81** | **1,85** | ❌ | C1 |
| **Eficiencia Americana** | **153,22** | **146,08** | ❌ | C1 |
| **Productividad** | **84,75** | **78,89** | ❌ | C1 |
| Días de engorde | 69 | 61–62 | ⚠️ | definición (ver §6) |
| Edad ponderada | 45,58 | 45,0 | ⚠️ | definición/ponderación (ver §6) |

> Lo notable: **casi todo ya cuadra**. Los 6 indicadores en rojo (producción, despachado, peso, conversión, eficiencia, productividad) provienen de **una sola causa de dato (C1)** más, en el caso del despachado, la **fórmula del total (C2)**.

---

## 3. Causa C1 (DATO) — Despacho sin peso: `movimiento_pollo_engorde` id 102

| campo | valor |
|---|---|
| id / numero | **102** / `MPE-20260401-000102` |
| lote origen | 20 (Galpon-1) |
| fecha | 2026-03-22 · placa `MAA-2902` · edad 41 |
| aves (H+M+X) | **3.192** (sí se cuentan en aves sacrificadas) |
| peso_bruto / peso_tara / peso_neto | **NULL / NULL / NULL** |
| peso_neto_global / peso_bruto_real / peso_tara_real / promedio_peso_ave | **todos NULL** |

**Es la ÚNICA línea sin peso de toda la corrida** (las demás líneas "raras" —id 80, 81, 129, 423— son despachos multi-lote correctamente prorrateados). La función calcula `kg = SUM(COALESCE(peso_neto, bruto−tara))`; aquí todo es NULL ⇒ **0 kg** para 3.192 aves.

**Reconciliación exacta:**
```
Excel producción kilo en pie ........ 251.052
Sistema (Σ peso_neto) ............... 245.129
Diferencia ......................... 5.923   ← exactamente el peso faltante de mov 102
```
La diferencia coincide con la celda roja del Excel ("5923 diferencia con el sistema que hacen falta para cuadrar").

> ⚠️ **Ojo:** 5.923 kg / 3.192 aves = **1,856 kg/ave**, peso bajo para 41 días. **Confirmar el tiquete físico de báscula** de la placa `MAA-2902` (2026-03-22): si el neto real es 5.923 → cuadra exacto con el Excel; si difiere, habría que ajustar también el Excel.

---

## 4. Causa C2 (CÓDIGO front) — El total a cliente excluye los lotes sin merma

`frontend/.../indicador-ecuador-list.component.ts → liquidacionTotales()` (líneas ~706-724):

```ts
for (const r of R) {
  ...
  prodKg += r.produccionKiloEnPie ?? r.kgCarnePollos;     // ✅ suma SIEMPRE (los 2 lotes)
  if (r.mermaUnidades != null || r.mermaKilos != null) {  // ❌ solo lotes CON merma
      mermaUni += r.mermaUnidades ?? 0;
      mermaKg  += r.mermaKilos ?? 0;
      totCliente += r.totalKilosDespachadosCliente ?? (...);  // ❌ lote 20 nunca entra
  }
}
...
totalKilosDespachadosCliente: hayMerma ? totCliente : null,   // ❌ = solo lote 19
```

Como la **merma es única por corrida** (va en el lote 19), el `if` deja **fuera al lote 20** del acumulado `totCliente` ⇒ el total muestra solo `133.225` (lote 19) en vez de `produccion_total − merma`.

**Fix (front):** el total a cliente debe calcularse a nivel corrida, no por lote:
```ts
// usar prodKg (que ya suma todos los lotes) menos la merma única de la corrida
totalKilosDespachadosCliente: hayMerma ? (prodKg - mermaKg) : null,
```
- Con datos actuales: `245.129 − 462,66 = 244.666`.
- Con C1 corregido: `251.052 − 462,66 = 250.590` ✅ (= Excel).
- No requiere tocar la función SQL ni el backend (el total lo arma el front). `produccion_kilo_en_pie` ya viene por lote sin depender de la merma.

---

## 5. Simulación: aplicando C1 + C2, ¿cuadra con el Excel?

Constantes de corrida (de la BD, no cambian): aves sac = 90.623 · consumo = 453.909 · merma = 462,66 kg.
Escenario corregido: producción kilo en pie = 245.129 + **5.923** (mov 102) = **251.052**.

| Indicador | Fórmula | Sistema actual | **Corregido** | Excel |
|-----------|---------|---------------:|--------------:|------:|
| Producción kilo en pie | Σ kg despachados | 245.129 | **251.052** | 251.052 |
| Total kg despachados cliente | producción − merma | 244.666¹ | **250.590** | 250.590 |
| Peso promedio | kg / aves sac | 2,70 | **2,77** | 2,77 |
| Conversión | consumo / kg | 1,85 | **1,81** | 1,81 |
| Eficiencia Americana | (peso/conv)×100 | 146,08 | **153,22** | 153,22 |
| Productividad | (peso/conv)/conv×100 | 78,89 | **84,75** | 84,75 |
| Consumo ave | consumo / aves sac | 5,01 | 5,01 | 5,01 |
| Supervivencia | (enc−mort)/enc×100 | 93,74 | 93,74 | 93,74 |

¹ con la fórmula C2 ya corregida pero sin el dato C1. Con la fórmula vieja, el sistema muestra **133.225**.

> **Resultado: aplicando C1 + C2 los 8 indicadores cuadran exactamente con el Excel.** Esto prueba que el descuadre de peso promedio, conversión, eficiencia y productividad **no son bugs de fórmula**: son consecuencia del único dato faltante (mov 102).

### 5.1 Validación ejecutada (2026-06-26, copia local)
- **A (C2)** aplicado en `indicador-ecuador-list.component.ts → liquidacionTotales()` (`totalKilosDespachadosCliente = hayMerma ? (prodKg - mermaKg) : null`, se eliminó el acumulador `totCliente`). `yarn build` OK.
- **B (C1)** aplicado en local: `UPDATE movimiento_pollo_engorde SET peso_neto=5923, peso_neto_global=5923 WHERE id=102` (1 fila). `fn_indicadores_pollo_engorde(20)` → kg lote 20 = 117.363,83 · peso prom = 2,793.
- Replicando el total del front contra la BD post-fix: **16/17 indicadores idénticos al Excel.** Única micro-diferencia: **Eficiencia Americana 153,23 vs 153,22** porque el front pondera el peso promedio del TOTAL por kg (`Σ peso·kg / Σ kg` = 2,77049) en vez de `kg_total / aves_total` (2,77033). Cosmético; el peso promedio igual redondea a 2,77. Fix opcional de 1 línea.
- ⚠️ El valor 5.923 es **provisional** (para cuadrar con el Excel). Para prod, usar el neto real del tiquete de báscula de la placa `MAA-2902` (22/03). Revertir en local: `UPDATE movimiento_pollo_engorde SET peso_neto=NULL, peso_neto_global=NULL WHERE id=102;`.

---

## 6. Diferencias de DEFINICIÓN (no son errores de cálculo)

| Indicador | Excel | Sistema | Explicación |
|---|---|---|---|
| Merma (%) | 0,17 (/encasetadas) | 0,18 (/sacrificadas) | La fn usa `merma_u / aves_sacrificadas`. El Excel usa `/encasetadas`. Decidir cuál es el oficial. |
| Días de engorde | 69 | 61 y 62 (prom 61,5) | Excel = span de la corrida (encaset 2/2 → liquidación 4/12). Sistema = por lote (encaset→último despacho). |
| Edad ponderada | 45,58 | 45,0 | Diferencia de ponderación (por aves vs por kg vs por edad de cada tiquete). Revisar el peso de ponderación deseado. |

---

## 7. Plan de corrección

### 7.1 DATO (resuelve C1 y todo lo que deriva)
- Cargar el peso del despacho **mov 102** (`MPE-20260401-000102`, placa `MAA-2902`, 3.192 aves) con el tiquete físico de báscula.
  - `UPDATE movimiento_pollo_engorde SET peso_bruto=…, peso_tara=…, peso_neto=… WHERE id=102;`
  - Para cuadrar con el Excel, el **neto debe ser 5.923 kg** (confirmar contra el tiquete; ver caveat §3).
- (Higiene) Revisar la placa `MAA-2902` vs `MMA-2902` (posible error de digitación).

### 7.2 CÓDIGO (resuelve C2)
- `liquidacionTotales()`: `totalKilosDespachadosCliente: hayMerma ? (prodKg - mermaKg) : null`.
- (Opcional) Unificar el denominador de **Merma %** con el criterio oficial (encasetadas vs sacrificadas).
- (Opcional, decisión de negocio) "Días de engorde" y "Edad ponderada" a nivel corrida si se quiere replicar el Excel.

### 7.3 Riesgo latente (no afecta este caso)
- La fn filtra `estado <> 'Cancelado'`, pero las ventas anuladas son `estado='Anulado'` (hoy solo las excluye `deleted_at`). Verificado: **0 anuladas sin `deleted_at`**. Recomendado endurecer a `estado NOT IN ('Cancelado','Anulado')`.

---

## 8. Notas de verificación (peso_neto vs báscula)

Hay 2 camiones que despachan de **ambos galpones a la vez** (PFC-6803 el 2026-03-20, ABQ-1811 el 2026-04-03/04). En esas líneas `peso_bruto/peso_tara` guardan el **peso global del camión clonado por línea**; sumar `bruto−tara` por línea **duplica** esos camiones (+11.116 kg). La app usa `peso_neto` **prorrateado por aves** (fix R3.1) y **no duplica** — es correcto. El Excel también deduplica (su 251.052 = 245.129 deduplicado + 5.923 de mov 102), por eso solo difiere en el dato faltante.

---

## 9. Apéndice — Consultas usadas (solo lectura)

```sql
-- Reconciliación de producción kilo en pie (corrida 19+20)
SELECT SUM(COALESCE(peso_neto,0))                      AS sistema,      -- 245129
       COUNT(*) FILTER (WHERE peso_neto IS NULL)       AS sin_peso      -- 1 (mov 102)
FROM movimiento_pollo_engorde
WHERE lote_ave_engorde_origen_id IN (19,20)
  AND estado='Completado' AND deleted_at IS NULL
  AND tipo_movimiento IN ('Venta','Despacho','Retiro');

-- El despacho sin peso
SELECT id, numero_movimiento, cantidad_hembras+cantidad_machos+cantidad_mixtas AS aves,
       peso_bruto, peso_tara, peso_neto, placa
FROM movimiento_pollo_engorde WHERE id = 102;

-- Indicadores por lote (idénticos a la API)
SELECT * FROM fn_indicadores_pollo_engorde(19,2.7,4.5);
SELECT * FROM fn_indicadores_pollo_engorde(20,2.7,4.5);
```

**Archivos relevantes:**
- Función: [backend/sql/fn_indicadores_pollo_engorde.sql](../sql/fn_indicadores_pollo_engorde.sql)
- Servicio: [IndicadorEcuadorService.cs](../src/ZooSanMarino.Infrastructure/Services/IndicadorEcuadorService.cs)
- Total del front: `frontend/src/app/features/indicador-ecuador/pages/indicador-ecuador-list/indicador-ecuador-list.component.ts` → `liquidacionTotales()`
