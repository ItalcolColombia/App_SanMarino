# 19 — Reconciliación stock de inventario ↔ saldo de alimento del seguimiento (engorde)

**Fecha:** 2026-05-31
**Disparador:** El usuario pide validar que (a) los ingresos de alimento tengan la fecha correcta
respecto a la apertura del lote, (b) consumos e ingresos cuadren, y (c) **el saldo de alimento del
seguimiento cuadre con el stock real del inventario, POR TIPO DE ALIMENTO**, en TODOS los lotes.
**Entorno:** solo local.
**Pre-requisito ya hecho:** fn v6 (M1) — el saldo de seguimiento ya es internamente consistente
(persistido = fn = lógica C#). Ver `fase_de_desarrollo/18_*`.

---

## 1. Hallazgo central: existen DOS sistemas de alimento paralelos que NO concuerdan

### Sistema A — Inventario real (`inventario_gestion_stock` / `inventario_gestion_movimiento`)
* `inventario_gestion_stock.quantity` = **stock físico actual** por (farm, núcleo, galpón, item),
  por **tipo de alimento** (`item_inventario_ecuador`: SM0178 Super Pollo Engorde, SM0176 Iniciación, etc.).
* Lo mantiene el `InventarioGestionService` transaccionalmente: `Ingreso (+)`, `Consumo (−)`,
  `TrasladoSalida/Entrada`, `TrasladoInterGranja*`, `AjusteStock` (SETEA absoluto), `EliminacionStock` (borra).
* ⚠️ El log de movimientos **no es un ledger sumable** (AjusteStock guarda el delta, no el absoluto;
  Eliminacion borra). **El `quantity` de la tabla es la fuente de verdad del stock.**

### Sistema B — Saldo del seguimiento (`fn_seguimiento_diario_engorde` / `saldo_alimento_kg`)
* Corre sobre `lote_registro_historico_unificado` (ingresos) menos el **consumo MANUAL del
  seguimiento** (`consumo_kg_hembras/machos`).
* El histórico que usa mezcla **3 orígenes**: `inventario_gestion_movimiento` (real) +
  `cuadrar_saldos_engorde` (ajustes Excel) + `manual_backfill_ingreso_lote_2601`.

### Por qué divergen (cuantificado en los 34 lotes "2602")
1. **Ingresos fantasma**: los ajustes `cuadrar_saldos_engorde` + `manual_backfill_*` suman kg al saldo
   del seguimiento que **nunca entraron al stock real** (ej. G0040: 127 940 kg de ajuste; G0048: 61 565).
2. **Consumo de fuentes distintas**: el `INV_CONSUMO` del inventario es ~2-2.5× el consumo reportado
   en el seguimiento, **incluso acotando por la ventana del lote sin solape de ciclos**
   (lote 10/G0048, ventana 03-06→05-09: INV_CONSUMO 174 765 vs seguimiento 78 750). Además el
   `INV_CONSUMO` no está fechado en los mismos días que el seguimiento (en marzo del lote 10 no hay
   ningún INV_CONSUMO). → los dos consumos son **flujos de captura independientes que no coinciden**.
3. **Stock a nivel galpón y acumulado entre ciclos** vs saldo per-lote: cada galpón tiene ciclos
   SECUENCIALES (2601 → 2602 → 2603, ej. G0048 = lotes 31/10/79). El stock actual es del ocupante
   vigente; el saldo de seguimiento es de un ciclo.
4. **Timing**: ingresos posteriores al último seguimiento inflan el stock vs el último saldo registrado.

### Ejemplo lote 75 / G0042
* Stock real: SM0178 = 38 740 + SM0176 = 695 → **39 435 kg**.
* Saldo seguimiento (30-may): **16 680 kg**. Diferencia **−22 755 kg**.

---

## 2. Decisiones de negocio necesarias ANTES de "cuadrar" (no se puede resolver solo con datos)

1. **¿Cuál es la fuente de verdad del alimento físico?** ¿El inventario (`inventario_gestion_stock`)
   o el seguimiento? La corrección apunta en direcciones opuestas según la respuesta.
2. **¿Por qué `INV_CONSUMO` ≈ 2× el consumo del seguimiento?** Hay que decidir si:
   * el `INV_CONSUMO` está duplicado / mal generado (bug a corregir), o
   * el seguimiento sub-reporta el consumo (dato a completar), o
   * son conceptos distintos (ej. consumo físico vs consumo asignado a aves).
3. **Ajustes `cuadrar_saldos_engorde` / `manual_backfill`**: ¿se mantienen (y se replican en el stock
   real) o se eliminan del histórico del seguimiento?
4. **Mapeo por tipo**: el `tipo_alimento` del seguimiento es texto libre (ej. "H: AV. SUPER POLLO
   ENGORDE", a veces dos por día) y no descompone el `consumo_kg` por tipo. Para validar "por tipo"
   hay que definir el mapeo texto→`item_inventario_ecuador` y cómo repartir el consumo diario por tipo.

---

## 3. Plan de validación propuesto (read-only primero, sin tocar datos)

### Fase A — Reporte de validación por lote y por tipo (solo lectura)
Construir una vista/consulta que, por cada lote de engorde y cada `item_inventario_ecuador` (tipo):
* **Fechas**: ingresos antes de `fecha_encaset` atribuidos al lote; ingresos fuera de la ventana del ciclo.
* **Ingresos**: histórico (seguimiento) vs `inventario_gestion_movimiento` real → marcar fantasmas.
* **Consumo**: seguimiento vs `INV_CONSUMO`, acotado a la ventana del ciclo.
* **Stock**: `inventario_gestion_stock.quantity` vs saldo de seguimiento esperado al cierre del ciclo.
* Clasificar cada lote: ✅ cuadra · ⚠️ desfase de timing · ❌ desfase estructural (consumo/ingreso/tipo).

### Fase B — Diagnóstico agregado
Tabla resumen: cuántos lotes cuadran, magnitud total del desfase, descomposición por causa
(fantasma / consumo 2× / cross-ciclo / timing). Esto dimensiona el trabajo de cuadre.

### Fase C — Cuadre (SOLO tras decisiones de §2 y aprobación explícita)
Según la fuente de verdad elegida: alinear el sistema secundario al primario, por lote y por tipo,
con respaldo y reversible. Idempotente. Solo local primero; prod con confirmación.

---

## 4. Estado
* Diagnóstico §1 COMPLETO (validado en datos locales).
* **BLOQUEADO en las decisiones de §2** — requeridas del usuario antes de Fase C.
* Fase A (reporte read-only) se puede construir ya, sin riesgo, para ver el detalle por lote/tipo.
