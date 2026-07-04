# Fase 2 — Plan: Colombia descuenta stock desde seguimientos (acople de inventarios)

> Síntesis de `fase2_negocio_definicion.md` (negocio) + `fase2_impacto_qa.md` (QA/impacto).
> Objetivo: habilitar que Colombia descuente inventario desde sus seguimientos, acoplando lo existente,
> SIN afectar indicadores ni reporte contable de Colombia. Convivencia con datos actuales; sin fusión de esquemas (eso sería Fase 3).

## Decisiones ya resueltas por el análisis (no requieren OK)
- **Modelo destino = A (Colombia)** — `farm_inventory_movements` / `catalogo_items` vía `catalogItemId`. Descartado B porque: (1) los ids A y B no son intercambiables (colisión real: `id=89` es medicamento Ecuador en B y "POLLITA INICIACIÓN" en A); (2) Colombia no tiene stock en B → `RegistrarConsumoAsync(B)` lanza; (3) reintroduciría el bug que Fase 1 cerró.
- **Indicadores: NO se afectan** (evidencia de ambos especialistas): ninguna fn de indicadores (levante/producción/engorde) lee inventario; el consumo de los indicadores sale de `consumo_kg_*` de las tablas de seguimiento, no del inventario.
- **Contable protegido con `movement_type` nuevo**: el auto-descuento Colombia se registra en `farm_inventory_movements` con un tipo **`ConsumoSeguimiento`** (nuevo), **excluido de los 4 buckets del contable** (Entradas/Traslados/Retiros/Consumo-bultos). El `ReporteContableService` lee `Exit`→Retiros y el consumo de bultos desde seguimientos (kg/40) — al no ser ninguno de esos, las cifras del contable quedan **idénticas** y el stock A sí baja.
- **Acople = convivencia, NO fusión**: se mantienen ambas rutas y menús. Sin catálogo único, sin mapeo de ids, sin migración A↔B (Fase 3).

## Diseño técnico (menor riesgo)
1. **Despacho por país en el punto de consumo**: hoy `InventarioConsumoGate.DebeDescontarModeloB(pais)` es booleano (EC/PA→B). Se extiende a un despacho: EC/PA→modelo B (sin cambios), CO→**modelo A** (nuevo). Nunca usa el fallback `catalogItemId→item_inventario_ecuador_id`.
2. **Nuevo camino de descuento en A**: un método/servicio `FarmInventoryConsumoService` (o extensión de `FarmInventoryMovementService`) que registre consumo/devolución en `farm_inventory_movements` con `movement_type=ConsumoSeguimiento`, ubicación `farm_id + location/galpón`, kg (conversión gramos/1000 existente). Idempotencia por diff old/new (patrón ya usado en Ecuador).
3. **Enum `FarmInventoryMovementType`**: agregar `ConsumoSeguimiento` (persistido como string vía la conversión existente). El signo del kardex (`fn_kardex_signo`) debe mapear `ConsumoSeguimiento`→ -1 (resta stock) — **actualizar la fn + el switch C# equivalente** y re-verificar golden.
4. **Contable**: verificar que `ReporteContableService` NO incluya `ConsumoSeguimiento` en ningún bucket (por diseño no lo incluye; agregar test/afirmación).
5. **Pre-limpieza (con OK)**: eliminar las 2 filas espurias de Colombia en modelo B (mov id 5705, stock id 352) + 3 filas Ecuador en `catalogo_items` — script idempotente, ejecutar solo con OK.

## Decisiones del usuario (RESUELTAS — 2026-07-03)
- **F1 = Levante + Producción postura** descuentan. **Producción postura hoy NO toca inventario** (`ProduccionService`) → es lógica NUEVA (Create/Update/Delete + parseo de ítems del metadata JSONB, patrón levante).
- **F2 = TODOS los ítems** (alimento + medicamentos/vacunas/insumos), no solo alimento. Ubicación: alimento → granja+galpón; otros → granja (regla `IsAlimento` como en Ecuador). Requiere que los ítems existan en `catalogo_items` con stock.
- **F3 = BLOQUEAR el guardado** si el stock es insuficiente. ⚠️ **RIESGO OPERATIVO ALTO**: Colombia hoy casi no tiene stock precargado en modelo A → los seguimientos con ítems fallarán hasta que se carguen ingresos. **Prerrequisito de rollout: seed/carga de inventario inicial Colombia.** Implementar con **validación previa transaccional** (chequear stock de TODOS los ítems ANTES de persistir el seguimiento; si falta alguno → rechazar con mensaje claro por ítem, sin dejar el seguimiento a medias). El bloqueo debe ser atómico con el guardado del seguimiento.
- **F4 = Sí, limpiar datos espurios en local ahora** (2 filas Colombia en modelo B: mov 5705, stock 352 + 3 filas Ecuador en `catalogo_items`). Prod → OK aparte.

## Implicancias de las decisiones para el diseño
- **Bloqueo atómico**: el descuento Colombia debe validarse ANTES de guardar el seguimiento y ser transaccional — si algún ítem no tiene stock, se rechaza TODO el guardado (no puede quedar el seguimiento sin el descuento ni el descuento a medias). Esto cambia el patrón actual (Ecuador descuenta DESPUÉS de guardar, en try/catch tolerante). Para Colombia se requiere validación previa + transacción.
- **Producción postura**: agregar consumo desde `ProduccionService` (hoy solo guarda JSONB) resolviendo ubicación del lote y despachando a modelo A.
- **Todos los ítems**: `catalogo_items` debe tener los medicamentos/insumos; si un ítem del seguimiento no existe en el catálogo Colombia → el bloqueo lo rechaza (mensaje claro).

## Validación (por slice + QA final)
- `dotnet build` 0/0 + `dotnet test`; `yarn build`; golden kardex re-verificado con `ConsumoSeguimiento`.
- **Contable: golden de no-afectación** — snapshot del reporte contable Colombia ANTES y DESPUÉS de activar el descuento para el mismo período: deben ser idénticos.
- **Indicadores: golden de no-afectación** — indicadores levante/producción Colombia idénticos antes/después.
- **Visual con pantallazos por perfil** (Colombia foco): crear seguimiento con ítem alimento → stock baja en `/inventario` (Stock/Kardex/Movimientos con el nuevo tipo); editar → ajuste; eliminar → devolución; contable idéntico; indicadores idénticos.

## Orden de slices (autónomo tras OK de forks)
S1 pre-limpieza filas espurias (con OK) → S2 enum + fn_kardex_signo + golden → S3 camino de descuento en A + despacho por país en el gate → S4 activar en los seguimientos elegidos (F1) → S5 tests de no-afectación (contable + indicadores) → QA + visual con pantallazos.

---

## ✅ Correcciones del arquitecto — INCORPORADAS (VEREDICTO: CAMBIOS REQUERIDOS → resueltos en el diseño)
1. **Transacción anidada (crítico):** `FarmInventoryMovementService.PostExitAsync` YA abre su propia `BeginTransactionAsync` → NO reutilizarlo dentro de una tx externa (Npgsql no soporta tx anidadas → excepción). Diseño correcto: (a) **validación previa de stock de TODOS los ítems ANTES** de `CreateAsync`, fuera del try/catch tolerante, con `throw` por ítem si falta; (b) **una sola `IDbContextTransaction`** abierta en el servicio de seguimiento (mismo `_ctx` scoped — todos los servicios comparten el DbContext del request) que envuelva `CreateAsync` + descuento; (c) crear **`FarmInventoryConsumoService`** (nuevo) que decremente `farm_product_inventory.Quantity` + inserte el movimiento **SIN abrir tx propia** (participa de la externa). El ajuste de aves centralizado queda dentro de la misma tx (semántica todo-o-nada — **cambia el comportamiento actual**, es intencional, documentar).
2. **`itemsGenerales` (crítico para "todos los ítems"):** `MetadataEngordeCalculos.ParseMetadataItemsToKg` hoy solo lee `itemsHembras`+`itemsMachos`, NO `itemsGenerales` (que el request SÍ acepta y persiste). Extender el parser a las 3 arrays (aditivo; Ecuador no usa generales → seguro; verificar el test verde). Leer SOLO de `Metadata`, nunca también de `ItemsAdicionales` (evita doble descuento).
3. **Tipo de la devolución (crítico):** Update con `diff<0` y Delete NO pueden usar `ConsumoSeguimiento` (mapea a −1 → restaría otra vez). Introducir **`DevolucionSeguimiento` → +1**, también **excluido del contable**, con su mapeo en `fn_kardex_signo` (+1) + `FarmInventoryKardexCalculos.Signo` (+1m) + golden.
4. **Enum real = `InventoryMovementType`** (`Domain/Enums/InventoryMovementType.cs`; valores `Entry/Exit/TransferOut/TransferIn/Adjust`), NO `FarmInventoryMovementType`. Persistencia `HasConversion(ToString/Parse)`, `HasMaxLength(20)` → `ConsumoSeguimiento`(18)/`DevolucionSeguimiento`(20) caben. Sin CHECK en BD → agregar valores es seguro. Golden C#: agregar casos `ConsumoSeguimiento→-1`, `DevolucionSeguimiento→+1`.
5. **Contable confirmado NO afectado:** `ReporteContableService.ObtenerDatosBultosAsync` filtra por string literal (`Entry`/`TransferIn`=entradas, `TransferOut`=traslados, `Exit`=retiros); los tipos nuevos no matchean → excluidos. El consumo de bultos sale de `seguimiento_diario.ConsumoKg/40`, independiente del inventario.
6. **Ubicación modelo A:** `farm_product_inventory` es por `farm_id`+`catalog_item_id` (NO por galpón). El descuento va contra `farm_id = lote.GranjaId`; galpón/`location` es informativo. Más simple que el plan original.
7. **Producción postura:** `ProduccionService` usa `_context` directo (tx trivial); descuenta desde los DTOs `ItemsHembras/Machos/Generales` del request (NO re-parsea JSON); requiere lookup de `GranjaId` del lote (vía `Lotes`/`LotePosturaProduccion.LoteId`), hoy no resuelto. Comparte el `FarmInventoryConsumoService`.
8. **Seed de stock para TODOS los tipos** (no solo alimento), porque el bloqueo aplica a todos los ítems y la mayoría de medicamentos/insumos no tienen stock precargado (18 filas Colombia). Prerrequisito operativo del rollout.
9. **P5 (consumo post-cierre):** default = seguir la regla de cierre existente (edición post-cierre exige reapertura, que ya existe); sin manejo especial adicional. Documentado.

**Slices corregidos:** S2 = enum (`ConsumoSeguimiento`, `DevolucionSeguimiento`) + `fn_kardex_signo` (+ golden C#) en el mismo slice. Nuevo **S2b = extender parser a `itemsGenerales`** (con su test). S3 = `FarmInventoryConsumoService` (sin tx propia) + validación previa + tx única en el servicio de seguimiento. S4 = activar en levante + producción (esta última = lógica nueva). S5 = tests de no-afectación (contable + indicadores golden antes/después).
