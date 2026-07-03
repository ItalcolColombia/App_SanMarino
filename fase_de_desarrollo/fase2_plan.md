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

## Forks que requieren TU decisión (afectan alcance/comportamiento)
- **F1 — ¿Qué seguimientos de Colombia descuentan?** Levante postura (mínimo) · +Producción postura (hoy NO toca inventario → lógica nueva) · +Engorde CO · +Reproductora.
- **F2 — ¿Qué ítems?** Solo `alimento` (recomendado) vs también medicamentos/vacunas/insumos.
- **F3 — ¿Stock insuficiente?** Permitir saldo negativo / registrar igual (recomendado: Colombia no precarga stock, bloquear haría fallar todo) vs bloquear el guardado.
- **F4 — Consumo tras cierre de lote**: seguir patrón actual (permitir con reapertura) vs bloquear.

## Validación (por slice + QA final)
- `dotnet build` 0/0 + `dotnet test`; `yarn build`; golden kardex re-verificado con `ConsumoSeguimiento`.
- **Contable: golden de no-afectación** — snapshot del reporte contable Colombia ANTES y DESPUÉS de activar el descuento para el mismo período: deben ser idénticos.
- **Indicadores: golden de no-afectación** — indicadores levante/producción Colombia idénticos antes/después.
- **Visual con pantallazos por perfil** (Colombia foco): crear seguimiento con ítem alimento → stock baja en `/inventario` (Stock/Kardex/Movimientos con el nuevo tipo); editar → ajuste; eliminar → devolución; contable idéntico; indicadores idénticos.

## Orden de slices (autónomo tras OK de forks)
S1 pre-limpieza filas espurias (con OK) → S2 enum + fn_kardex_signo + golden → S3 camino de descuento en A + despacho por país en el gate → S4 activar en los seguimientos elegidos (F1) → S5 tests de no-afectación (contable + indicadores) → QA + visual con pantallazos.
