# Fase 2 — Definición de Negocio: Colombia descuenta stock desde seguimientos

> Análisis solo-lectura (especialista de negocio). Base: `inventario_ecuador_mapa.md`, `inventario_colombia_mapa.md`, `inventario_unificacion_plan.md` + verificación contra el código real y la BD local (`:5433`, `sanmarinoapplocal`).
> **Fase 1 ya cerró** el bug cross-país (gate `InventarioConsumoGate.DebeDescontarModeloB` → Colombia NO descuenta de modelo B). Fase 2 = habilitar que Colombia SÍ descuente desde sus seguimientos diarios, autorizado por el usuario.

---

## 0. Hallazgos de código que anclan la definición (fuente de verdad)

| Hecho verificado | Evidencia (archivo:línea) |
|---|---|
| Los **4 caminos de consumo** usan el MISMO gate y descuentan del **modelo B** (`_inventarioGestionService.RegistrarConsumoAsync/RegistrarIngresoAsync`) | levante `SeguimientoLoteLevanteService.cs:438-451,501-527,561-570`; engorde EC `SeguimientoAvesEngordeEcuadorService.cs`; engorde CO `SeguimientoAvesEngordeService.cs:744-753,873-894,950-959`; reproductora `SeguimientoDiarioLoteReproductoraService.cs:242-257,326-351,413-427` |
| El gate es un único punto puro | `Application/Calculos/InventarioConsumoGate.cs:35-36` → `paisIdDelLote is PaisEcuador(2) or PaisPanama(3)` |
| **Producción postura Colombia (`ProduccionService`) NO toca inventario** en absoluto | `ProduccionService.cs:24-25,752` (comentarios explícitos) |
| `RegistrarConsumoAsync` (modelo B) **exige stock existente y suficiente** o lanza excepción | `InventarioGestionService.cs:1116-1119` (`if (stock == null || stock.Quantity < req.Quantity) throw`) |
| Indicador **levante postura Colombia** NO lee inventario: consumo sale de `seguimiento_lote_levante.consumo_kg_*` | `sql/fn_indicadores_levante_postura.sql:126,143` |
| Indicador **producción postura Colombia** NO lee inventario: consumo sale de `seguimiento_diario_*_reproductoras.consumo_kg_*` | `sql/fn_indicadores_produccion_postura.sql:243-244,340-341` |
| **`ReporteContableService` SÍ lee modelo A** (`catalogo_items` + `farm_inventory_movements`) — ÚNICO consumidor del modelo A fuera del UI manual | `ReporteContableService.cs:715` (`_ctx.CatalogItems`), `:737` (`_inventoryMovementService.GetPagedAsync` → `farm_inventory_movements`) |
| En el contable, de los movimientos A solo derivan **Entradas/Traslados/Retiros** de bultos; el **Consumo de bultos** viene de `ConsumoAlimento…/40` (seguimientos), NO de movimientos | `ReporteContableService.cs:756-776` (entradas/traslados/retiros) vs `:594-595,623-624,850-855` (consumo = kg de seguimiento ÷ 40) |
| Ítems de lote Colombia llevan **solo `catalogItemId`** (el `itemInventarioEcuadorId` es del par EC/PA) | `DTOs/…ItemSeguimientoDto` `CreateSeguimientoLoteLevanteRequest.cs:10-21,261-289` (bloque legacy sin `itemInventarioEcuadorId`) |
| Catálogos con **id NO intercambiable** y tipos de movimiento distintos: A usa `Entry/Exit/TransferIn/TransferOut` (inglés); B usa `Ingreso/Consumo/Traslado…` (español) | BD local: `farm_inventory_movements.movement_type` vs `InventarioGestionService.cs:1138-1139` |

**Conteo BD local (verificado):** `catalogo_items` = 122 (pais 1) + 3 (pais 2, contaminación); `farm_product_inventory` = 18 (todo CO); `farm_inventory_movements` = 188 (todo CO); `item_inventario_ecuador` = 136 (pais 2); `inventario_gestion_stock` = 132 (pais 2) **+ 1 fila espuria CO (id 352, item 89, 323 kg)**; `inventario_gestion_movimiento` = 5817 (pais 2) **+ 1 fila espuria CO (id 5705, "Ingreso — devolución por eliminación", seguimiento levante #892)**.

> La fila espuria de Colombia en modelo B es **residuo directo del bug pre-Fase 1**: un consumo cross-país que, al eliminarse el seguimiento, generó una "devolución" (Ingreso) creando stock Colombia dentro del inventario Ecuador. **Debe limpiarse antes de activar Fase 2** (script documentado, ejecutar solo con OK).

---

## 1. Regla de descuento para Colombia

### 1.1 A qué inventario debe descontar Colombia → **Modelo A (`farm_inventory_movements` / `catalogo_items`), NO modelo B**

**Decisión recomendada (menor riesgo):** Colombia descuenta de **SU** inventario, el modelo A, escribiendo un movimiento `Exit` (tipo "Consumo") en `farm_inventory_movements` contra el `catalogItemId`, con ubicación `farm_id` + `location`. **No** se migra Colombia a modelo B en Fase 2.

Justificación:
- El catálogo que hoy usan los ítems de seguimiento Colombia es `catalogo_items` (`catalogItemId`); los ids **no** son intercambiables con `item_inventario_ecuador`. Forzar modelo B reintroduce exactamente el bug que Fase 1 cerró.
- El único stock real de Colombia vive en `farm_product_inventory` (18 filas). En modelo B, Colombia tiene 0 stock legítimo (solo la fila espuria). `RegistrarConsumoAsync` **lanza si no hay stock** → descontar de B haría fallar todos los consumos de Colombia.
- El **reporte contable de Colombia ya lee modelo A** para los bultos: mantener el descuento en A mantiene una sola fuente de verdad de stock por país.

### 1.2 Qué seguimientos descuentan

| Seguimiento | ¿Descuenta hoy? | ¿Debe descontar en Fase 2 (Colombia)? | Nota |
|---|---|---|---|
| **Levante postura** (`SeguimientoLoteLevanteService`) | No (gate lo bloquea para CO) | **Sí** — es el flujo con ítems en metadata | Puebla `itemsHembras/Machos/Generales` con `catalogItemId` |
| **Producción postura** (`ProduccionService`) | No (nunca tocó inventario) | **DECISIÓN DE USUARIO** (fork P1) | Hoy solo escribe JSONB; activarlo es lógica nueva, no solo levantar un gate |
| **Engorde Colombia** (`SeguimientoAvesEngordeService`) | No (gate lo bloquea) | **Sí, si Colombia opera engorde con inventario** (fork P2) | Servicio CO que inyecta inventario; hoy gateado |
| **Reproductora** (`SeguimientoDiarioLoteReproductoraService`) | No (gate lo bloquea) | **Sí** si aplica a lotes CO | Mismo patrón de gate |

**Regla base:** descontar **solo ítems `tipoItem == "alimento"`** (los demás — vacuna/medicamento — no tienen stock consolidado por bultos y hoy no participan del contable). Confirmar con usuario si medicamentos/vacunas también deben descontar (fork P3).

### 1.3 Catálogo y mapeo de ubicación

- **Catálogo:** `catalogo_items` (el `catalogItemId` que ya viaja en el metadata). **No** se requiere catálogo único ni mapeo de ids para Fase 2 si se descuenta en A.
- **Ubicación:** modelo A usa `farm_id` + `location` (string). El lote Colombia tiene `GranjaId` + `GalponId` (string). Regla: `farm_id = lote.GranjaId`; `location` = `lote.GalponId` (o el `location` que use hoy el UI manual de `/inventario` para ese producto). **No** se rellena núcleo/galpón estructurado (eso es solo de B).
- **Cantidad:** `cantidad → kg` con la misma conversión que hoy (`ParseMetadataItemsToKg`: gramos/1000). El contable convierte kg→bultos con `FACTOR_CONVERSION_BULTO_KG = 40`. Mantener la aritmética idéntica.

### 1.4 Mecanismo técnico recomendado

Extender el gate para que decida **a qué modelo** descontar según el país, en vez de un booleano "descuenta B / no descuenta":

```
Ecuador/Panamá  → modelo B (RegistrarConsumoAsync de InventarioGestionService)   [sin cambios]
Colombia        → modelo A (nuevo: registrar Exit en FarmInventoryMovementService) [Fase 2]
```

El gate `InventarioConsumoGate` sigue siendo el único punto de decisión; se añade un segundo camino (modelo A) que consume `catalogItemId` — nunca el fallback a `item_inventario_ecuador_id`. Así el bug cerrado en Fase 1 **no** puede reaparecer.

---

## 2. Acople / convivencia A y B (menor riesgo)

**Recomendación: NO fusionar A→B en Fase 2. Convivencia con despacho por país.**

- **Rutas/menús:** mantener `/inventario` (modelo A, Colombia) y `/gestion-inventario` (modelo B, EC/PA) como están. El gating es por menú en BD; no se toca. Consolidar a una sola ruta es Fase 3 (requiere migrar 188 mov + 18 stock CO a estructura núcleo/galpón + mapear catálogos + actualizar menús prod).
- **Catálogo único:** **no** en Fase 2. Los ids no son intercambiables y el volumen/estructura difieren. Un catálogo único obliga a mapear `codigo` A↔B y rellenar ubicación estructurada → alto riesgo, se difiere.
- **Reducción de código:** el punto de acople de MENOR riesgo es el **descuento desde seguimientos**: hoy los 4 servicios ya comparten `InventarioConsumoGate` + el parser `ParseMetadataItemsToKg`. Fase 2 añade un solo helper "descontar en modelo A por país" reutilizado por los servicios que apliquen a Colombia. No se duplica la lógica de UI ni de rutas.
- **Datos existentes:** intactos. Modelo A (188/18/122) y modelo B (5817/132/136) no se tocan salvo la limpieza de las 2 filas espurias CO.

**Enfoque elegido = "despachar por país dentro del gate existente" (menor superficie de cambio, sin migración de datos, sin tocar menús prod, sin catálogo único).**

---

## 3. Impacto en INDICADORES y REPORTE CONTABLE (CRÍTICO — no deben afectarse)

### 3.1 Indicadores Colombia (levante / producción / engorde) — **NO se afectan**

Verificado: `fn_indicadores_levante_postura` y `fn_indicadores_produccion_postura` calculan el consumo desde `…consumo_kg_*` de las tablas de seguimiento — **nunca** leen `farm_inventory_movements`, `catalogo_items` ni `inventario_gestion_*`. Que Colombia empiece a escribir movimientos de inventario **no cambia ningún indicador**, porque los indicadores no leen inventario.

### 3.2 Reporte contable Colombia — **riesgo REAL de duplicación; regla obligatoria**

El `ReporteContableService` sí lee modelo A (`ObtenerDatosBultosAsync`). El desglose:

| Concepto del contable | Fuente hoy | ¿El descuento nuevo lo afecta? |
|---|---|---|
| **Consumo de bultos** (H/M) | `ConsumoAlimento…/40`, o sea kg de **seguimientos** (NO de inventario) | **NO** — ya sale de seguimientos; independiente de si hay o no movimiento de inventario |
| **Entradas / Traslados / Retiros de bultos** | `farm_inventory_movements` filtrado a `movement_type ∈ {Entry, TransferIn, TransferOut, Exit}` de productos alimento | **SÍ, si el nuevo consumo se registra con un tipo que caiga en esos buckets** |

**El peligro concreto:** hoy el contable mapea `Exit` → **Retiros de bultos**. Si el descuento automático de Colombia escribe el consumo como `movement_type = "Exit"`, esos consumos se sumarían a **Retiros**, distorsionando el saldo de bultos del contable (doble contabilización: el consumo ya baja el saldo vía la columna Consumo, y volvería a bajarlo vía Retiros).

**Regla obligatoria para NO afectar el contable:**
1. El movimiento de consumo automático desde seguimientos debe usar un **`movement_type` NUEVO y distinto** (p.ej. `"ConsumoSeguimiento"` o `"ConsumoAutomatico"`), que el contable **NO** incluya en ninguno de sus buckets (`Entry/TransferIn` → Entradas, `TransferOut` → Traslados, `Exit` → Retiros).
2. Alternativa equivalente: dejar `ObtenerDatosBultosAsync` filtrando explícitamente **solo** los tipos actuales (`Entry, TransferIn, TransferOut, Exit` del UI manual) y excluir el tipo nuevo. Preferible el tipo nuevo (auto-excluyente y auditable).
3. **NO** tocar el cálculo de Consumo de bultos (sigue = kg de seguimiento ÷ 40). El consumo del contable y el descuento de stock son **dos vistas del mismo hecho**: el contable lo muestra (desde seguimiento), el inventario lo materializa (baja stock). No deben sumarse.

**Resultado:** con un `movement_type` propio, el saldo de bultos del contable queda idéntico al de hoy (los nuevos movimientos son invisibles para sus 4 buckets), y el stock de `farm_product_inventory` sí baja. Cero cambio en cifras del contable.

---

## 4. Casos de negocio a validar

Sobre modelo A (Colombia), con `movement_type` propio para el consumo automático:

| Caso | Comportamiento esperado |
|---|---|
| **Crear seguimiento** con ítems alimento | 1 `Exit`/`ConsumoSeguimiento` por ítem (kg), baja `farm_product_inventory`. Reference al seguimiento. Si no hay stock suficiente → **política a definir (fork P4)**: (a) permitir stock negativo, (b) registrar el movimiento sin bloquear y avisar, (c) bloquear (como B). Recomendado (a)/(b) para no romper la captura diaria; B bloquea, pero en Colombia el stock puede no estar precargado. |
| **Editar seguimiento** (cambia ítems/cantidades) | Diff old/new por `catalogItemId` (mismo patrón que levante EC): `diff>0` → consumo adicional; `diff<0` → devolución (Entry/ingreso con tipo propio). Reference "(ajuste)"/"(devolución)". |
| **Eliminar seguimiento** | Devolución total: reponer stock por cada ítem (movimiento inverso con tipo propio). Reference "(devolución por eliminación)". Igual que hace levante EC hoy en modelo B. |
| **Devoluciones / ajustes manuales** | Siguen por el UI manual `/inventario` (modelo A) sin cambio. El automático y el manual conviven en `farm_inventory_movements` distinguidos por `movement_type`/reference. |
| **Cierre de lote** | **DECISIÓN (fork P5):** ¿el cierre congela el inventario (no más descuentos) o permite ajustes post-cierre? Hoy el descuento va atado a crear/editar seguimiento; si tras cierre no se editan seguimientos, no hay descuento nuevo. Confirmar si hay flujo de consumo post-cierre. |
| **Idempotencia** | Re-guardar el mismo seguimiento no debe duplicar consumo (usar diff old/new en update, no re-insertar el total). Ya es el patrón de EC. |

---

## 5. DECISIONES QUE REQUIEREN CONFIRMACIÓN DEL USUARIO (forks)

| # | Fork | Opciones | Recomendación |
|---|---|---|---|
| **P0** | Modelo destino del descuento Colombia | (A) modelo A `farm_inventory_movements` / (B) migrar a modelo B | **A** (menor riesgo; no migra datos; no reintroduce bug cross-país) |
| **P1** | ¿Producción postura Colombia descuenta? | Sí / No (hoy NO toca inventario) | Definir con usuario. Es lógica nueva, no solo un gate. Probable **No** en Fase 2 salvo requerimiento explícito |
| **P2** | ¿Engorde Colombia descuenta? | Sí / No | Depende de si Colombia opera engorde con inventario propio |
| **P3** | ¿Qué tipos de ítem descuentan? | Solo `alimento` / también vacuna+medicamento | **Solo alimento** (lo que consume el contable de bultos) |
| **P4** | Stock insuficiente / no precargado | Bloquear (como B) / permitir negativo / registrar + avisar | **Permitir/registrar sin bloquear** (Colombia no precarga stock; bloquear rompería captura diaria) |
| **P5** | Consumo tras cierre de lote | Congelar / permitir ajuste | Confirmar si existe edición de seguimiento post-cierre |
| **P6** | Limpieza filas espurias CO en modelo B | Ejecutar script (borra stock id 352 + mov id 5705) | **Sí, antes de Fase 2** — con OK explícito (DDL/DML de datos) |
| **P7** | `movement_type` del consumo automático | Tipo nuevo propio (`ConsumoSeguimiento`) / reusar `Exit` | **Tipo nuevo** (obligatorio para no distorsionar Retiros del contable) |

---

## 6. Resumen ejecutivo de la definición

- **Regla:** Colombia descuenta de **su** inventario (modelo A, `farm_inventory_movements` sobre `catalogItemId`), no del modelo B. Se activa desde **levante postura** (y opcionalmente engorde/reproductora), solo ítems **alimento**, ubicación `farm_id + location/galpón`, kg vía la conversión actual.
- **Acople:** **convivencia**, no fusión. Se extiende el punto único `InventarioConsumoGate` para despachar por país (A vs B). Sin migración de datos, sin tocar menús prod, sin catálogo único. La fusión real A→B es Fase 3.
- **Indicadores:** **no se afectan** (no leen inventario; consumo viene de `consumo_kg_*`).
- **Contable:** **no debe afectarse** si el consumo automático usa un **`movement_type` propio** excluido de los buckets Entradas/Traslados/Retiros. El Consumo de bultos ya sale de seguimientos y no se toca. Sin esto → **duplicación en Retiros**.
- **Pre-requisito:** limpiar las 2 filas espurias Colombia en modelo B (residuo del bug pre-Fase 1).
