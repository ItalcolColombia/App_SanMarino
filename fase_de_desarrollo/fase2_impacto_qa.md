# Fase 2 — Análisis de IMPACTO / QA (habilitar descuento de inventario en Colombia)

> Análisis **solo-lectura** (especialista QA/impacto). Rama `refactor/optimizacion-multipais` (HEAD `041dbe4`).
> Base: `inventario_colombia_mapa.md`, `inventario_ecuador_mapa.md`, `inventario_unificacion_plan.md`, `tracker_estado.md`.
> Objetivo: mapear qué se rompería si la **Fase 2** habilita que Colombia descuente inventario desde seguimientos. NO se cambió código.
> BD local: Postgres nativo `:5433`, `sanmarinoapplocal` (95 tablas). Conteos verificados por `psql`.

---

## 0. Estado de la Fase 1 (punto de partida — CERRADO y verificado)

Los **4 caminos de consumo** ya están gateados por país del lote → **hoy Colombia NO descuenta del modelo B**. Gate puro:

`backend/src/ZooSanMarino.Application/Calculos/InventarioConsumoGate.cs:35-36`
```csharp
public static bool DebeDescontarModeloB(int? paisIdDelLote)
    => paisIdDelLote is PaisEcuador /*2*/ or PaisPanama /*3*/;   // Colombia(1)/null ⇒ false
```
El gate se resuelve aguas arriba con `ResolverPaisIdLoteAsync` (= `lote.PaisId ?? farm→departamento→pais`) y se aplica en Create/Update/Delete de los 4 servicios. NO se tocó el parser `ParseMetadataItemsToKg` (mantiene su test verde de fallback).

| Servicio | Archivo | Líneas del gate (Create / Update / Delete) |
|---|---|---|
| Levante | `SeguimientoLoteLevanteService.cs` | 438-451 / 501-527 / 561-570 |
| Engorde Ecuador | `SeguimientoAvesEngordeEcuadorService.cs` | 283-292 / 418-441 / 501-510 |
| Engorde Colombia | `SeguimientoAvesEngordeService.cs` | 744-753 / 873-894 / 950-959 |
| Reproductora (4º camino, S1b `67569d6`) | `SeguimientoDiarioLoteReproductoraService.cs` | 242-257 / 326-351 / 413-427 |

**Para habilitar el descuento Colombia en Fase 2**, el cambio mínimo es que `DebeDescontarModeloB` acepte Colombia (o gate distinto). Ese único punto abre los 4 caminos a la vez — **por eso el impacto es amplio y hay que dimensionarlo antes**.

---

## 1. MATRIZ — módulos/caminos donde se usa el consumo de inventario

| # | Módulo/camino | Front | Back | Tabla que toca | ¿Descuenta HOY? | Si Colombia empieza a descontar |
|---|---|---|---|---|---|---|
| 1 | **Seguimiento LEVANTE** | `lote-levante` | `SeguimientoLoteLevanteService` | **modelo B**: `inventario_gestion_stock/_movimiento` (vía `RegistrarConsumoAsync`) | Solo EC/PA (gateado) | Escribiría `Consumo`/`Ingreso` en modelo B para lotes Colombia. Necesita ítems con `itemInventarioEcuadorId>0` reales, catálogo Colombia migrado a `item_inventario_ecuador`, y `inventario_gestion_stock` con existencias. Sin eso → o no descuenta, o vuelve el bug de id-colisión (§5). |
| 2 | **Seguimiento ENGORDE Ecuador** | `aves-engorde` (compartido EC/PA) | `SeguimientoAvesEngordeEcuadorService` | modelo B | Solo EC/PA (gateado) | Igual que #1. Es el controller que consume el front `aves-engorde`. |
| 3 | **Seguimiento ENGORDE Colombia** | (backfill/cuadrar-saldos) | `SeguimientoAvesEngordeService` | modelo B | Solo EC/PA (gateado) | Servicio Colombia que inyecta inventario Ecuador → el **más peligroso** para la colisión de ids. |
| 4 | **Seguimiento REPRODUCTORA** | `lote-produccion` (postura) | `SeguimientoDiarioLoteReproductoraService` | modelo B | Solo EC/PA (gateado) | Igual. 4º camino cerrado en S1b. |
| 5 | **Producción postura** | `lote-produccion` | `ProduccionService` | — | **NO** llama inventario (solo JSONB) | Sin cambio: no descuenta ni descontará salvo que se agregue explícitamente. |
| 6 | **UI `/inventario` (modelo A, Colombia)** | `features/inventario/` | `FarmInventory*Service` | **modelo A**: `farm_product_inventory`, `farm_inventory_movements`, `catalogo_items` | Solo alta MANUAL por UI (no desde seguimientos) | Si el auto-descuento va a **modelo B**, el modelo A (que la UI de Colombia muestra) **NO reflejará el consumo** → stock A y consumo real quedan aún más desconectados. Confusión de usuario. Si el auto-descuento fuera a modelo A, habría que reescribir el path (hoy `RegistrarConsumoAsync` es B-only). |
| 7 | **UI `/gestion-inventario` (modelo B, EC/PA)** | `features/gestion-inventario/` | `InventarioGestionService` | modelo B | Recibe el descuento de EC/PA | Si Colombia escribe en B, esta UI empezaría a mostrar stock/movimientos de granjas Colombia mezclados (aislados por company+pais, pero misma tabla). |
| 8 | **Gastos Ecuador (modelo C)** | `features/gastos-inventario/` | `InventarioGastoService` → delega en `RegistrarConsumoAsync` | modelo B (`inventario_gasto*` + muta B) | Solo Ecuador | Acoplado a B. Si Colombia usa B, C podría extenderse a Colombia; hoy es Ecuador-only por menú. |

**Conclusión #1:** el auto-descuento vive **100% en el modelo B** (`RegistrarConsumoAsync`). La UI de inventario de Colombia es **modelo A**. Habilitar el descuento en B **no alimenta** la UI que Colombia ve hoy (`/inventario`) → decisión de negocio ineludible: o se migra Colombia a la UI/modelo B (Fase 2 unificación), o el descuento no será visible donde el usuario Colombia lo espera.

---

## 2. INDICADORES de Colombia — dependencias de tablas (¿leen inventario?)

Auditadas las funciones SQL (FROM/JOIN) y los servicios. **NINGÚN indicador lee inventario** — evidencia:

| Indicador | Definición | Tablas que lee (FROM/JOIN) | ¿Lee inventario? |
|---|---|---|---|
| **Levante postura** | `backend/sql/fn_indicadores_levante_postura.sql` | `lotes`, `seguimiento_lote_levante`, `guia_genetica_sanmarino_colombia` | **NO** |
| **Producción postura** | `backend/sql/fn_indicadores_produccion_postura.sql` (servicio `IndicadoresProduccionService`, 0 refs a inventario) | `lotes`, `seguimiento_diario_levante_reproductoras`, `seguimiento_diario_produccion_reproductoras`, `lote_postura_produccion`, `lote_postura_levante`, `guia_genetica_sanmarino_colombia` | **NO** |
| **Engorde (pollo)** | `backend/sql/fn_indicadores_pollo_engorde.sql` (servicio `IndicadorEcuadorService`, 0 refs a inventario) | `lote_ave_engorde`, `lote_reproductora_ave_engorde`, `movimiento_pollo_engorde`, `seguimiento_diario_aves_engorde`, `seguimiento_diario_lote_reproductora_aves_engorde`, `farms`, `galpones` | **NO** |
| **Engorde diario (vista)** | `backend/sql/vw_indicadores_diarios_engorde.sql` | `seguimiento_diario_aves_engorde`, `guia_genetica_ecuador_header/_detalle`, `farms`, `galpones`, `nucleos`, `companies` | **NO** |

**Verificación adicional:** `grep` de las tablas de inventario (`farm_inventory_movements`, `farm_product_inventory`, `inventario_gestion_*`, `catalogo_items`, `item_inventario_ecuador`) sobre TODOS los scripts de indicadores/reportes/liquidación en `backend/sql/` → único hit = `vw_validacion_alimento_engorde.sql`, que es un **script de diagnóstico** (referenciado solo en SQL y en `CLASIFICACION_SCRIPTS.md`, **NO cableado a ningún endpoint ni desplegado como vista** — `information_schema.views` local: 0 filas `vw_validacion%`).

**Conclusión #2:** los indicadores leen de `seguimiento_diario*`, `lotes`, `lote_*`, `movimiento_pollo_engorde`, `guia_genetica_*` — **jamás de inventario**. **Habilitar el descuento Colombia NO mueve ningún indicador.** El consumo (kg de alimento) que muestran los indicadores sale del propio seguimiento (`ConsumoKgHembras/Machos`), no del inventario.

---

## 3. REPORTE CONTABLE de Colombia (`ReporteContableService`) — RIESGO #1

`backend/src/ZooSanMarino.Infrastructure/Services/ReporteContableService.cs`. Tablas/fuentes que lee:

| Bloque del reporte | Fuente (código) | Tabla real |
|---|---|---|
| Aves (mortalidad, sel, consumo kg) | `_ctx.SeguimientoDiario` (levante/producción) + `_ctx.SeguimientoProduccion` | `seguimiento_diario`, `produccion_diaria` |
| Ventas/traslados de aves | `_movimientoAvesService.GetMovimientosByLoteAsync` | `movimiento_aves`/traslados |
| **Bultos: catálogo alimento** (`ObtenerDatosBultosAsync` 715-724) | `_ctx.CatalogItems` filtrado `type_item=='alimento'` | **`catalogo_items` (modelo A)** |
| **Bultos: entradas/traslados/retiros** (737-777) | `_inventoryMovementService.GetPagedAsync(granjaId,...)` | **`farm_inventory_movements` (modelo A)** |
| **Bultos: consumo H/M** (592-595) | `seguimiento_diario.ConsumoKg / 40 kg` | `seguimiento_diario` (NO inventario) |

**Análisis del riesgo de duplicación:**

- El reporte contable lee **exclusivamente el modelo A** para el bloque de bultos (entradas/traslados/retiros vía `farm_inventory_movements`; catálogo vía `catalogo_items`).
- El auto-descuento de Colombia (si se habilita) escribiría en el **modelo B** (`inventario_gestion_movimiento`), que el reporte contable **NO lee**.
- **Por tanto NO hay doble conteo por la misma tabla, ni cambian los saldos de bultos** del contable — SIEMPRE Y CUANDO el descuento vaya a B (el diseño actual de `RegistrarConsumoAsync`).
- El **consumo de bultos** del contable ya se calcula desde `seguimiento_diario` (kg/40), NO desde inventario → es independiente del inventario en ambos modelos.

**Evidencia de conteos actuales (psql, company 1 = Colombia):**

`farm_inventory_movements` (modelo A, lo que lee el contable):
```
company_id | movement_type | count |  sum_qty
-----------+---------------+-------+----------
     1     | Entry         |   31  | 730679.0
     1     | Exit          |  153  |  63257.8
     1     | TransferIn    |    1  |    323.0
     1     | TransferOut   |    1  |    323.0
     4     | Entry         |    1  |  10000.0
     4     | Exit          |    1  |    500.0
```
El par `TransferIn/Out` de 323 kg (ids 7/8, farm 3→1, `catalog_item_id=61`, 2025-09-17) es un traslado inter-granja legítimo de modelo A — NO relacionado con la fila espuria de modelo B (§4).

**⚠️ Riesgo REAL si la Fase 2 unifica A→B (no solo "habilitar descuento"):** si además de habilitar el descuento se **migran los movimientos de modelo A a modelo B** y/o se **repunta `ReporteContableService` a leer modelo B**, entonces el consumo auto-descontado y las entradas manuales convivirían en la misma tabla → habría que auditar que `ObtenerDatosBultosAsync` no sume dos veces (una por el movimiento `Consumo` auto y otra por `seguimiento_diario.ConsumoKg/40`). **Mientras el contable siga leyendo modelo A y el descuento vaya a B, no hay duplicación.** El punto de control es no cambiar la fuente del contable en el mismo slice en que se habilita el descuento.

---

## 4. Estado actual Colombia (validación con psql)

**Conteos por tabla / company / país:**

| Tabla | Modelo | company 1/pais 1 (Colombia) | company 4/pais 1 (Demo) | company 3/pais 2 (Ecuador) | Nota |
|---|---|---|---|---|---|
| `catalogo_items` | A | 61 | 61 | **3** ⚠️ | 3 filas Ecuador espurias (ids 303/304/305) contaminan el catálogo A |
| `farm_product_inventory` | A | 17 | 1 | 0 | stock Colombia |
| `farm_inventory_movements` | A | 186 | 2 | 0 | kardex Colombia (188 total) |
| `item_inventario_ecuador` | B | 0 | 0 | 136 | catálogo B limpio de Colombia |
| `inventario_gestion_stock` | B | **1** ⚠️ | 0 | 132 | **fila espuria Colombia** (id 352) |
| `inventario_gestion_movimiento` | B | **1** ⚠️ | 0 | 5817 | **fila espuria Colombia** (id 5705) |

**Filas espurias pendientes de limpieza (el bug de Fase 1 pre-gate, con evidencia de la colisión de ids):**
```
inventario_gestion_movimiento id=5705: company 1, pais 1, farm 20, item_inventario_ecuador_id=89, Ingreso 323 kg, 2026-06-12
inventario_gestion_stock      id=352 : company 1, pais 1, farm 20, item_inventario_ecuador_id=89, nucleo 819014 / galpon G0326, 323 kg
```
**La colisión de ids en carne y hueso:**
- `item_inventario_ecuador.id=89` = Ecuador `AV0374 "AV. AMINAPOT 720 1LT 0%"` (medicamento, company 3/pais 2).
- `catalogo_items.id=89` = Colombia `000691 "POLLITA INICIACION REPRODUCTORA"` (company 1/pais 1).
- Un seguimiento de un lote Colombia con `catalogItemId=89` → el fallback pre-Fase-1 (`id<=0 ⇒ catalogItemId`) lo trató como `item_inventario_ecuador_id=89` y escribió stock/movimiento **en el modelo B bajo el scope de Colombia**. La documentación decía "1 fila espuria"; en realidad son **2** (movimiento + stock). Ambas requieren OK antes de borrar (tracker §"Diferido").

**Qué FUNCIONA hoy en Colombia:**
- Catálogo (`catalogo_items`), stock (`farm_product_inventory`), movimientos MANUALES por UI `/inventario` (ingresos/salidas/traslados/ajustes) → modelo A, operativo.
- Kardex Colombia migrado a SQL (`fn_kardex_farm_inventory`, S3) — golden 18/18 pares, sin regresión.
- Reporte contable (bultos desde modelo A + consumo desde seguimiento).

**Qué NO funciona / no existe hoy en Colombia:**
- **Descuento automático desde seguimientos** (gateado a EC/PA). El consumo de un seguimiento Colombia NO reduce el stock de `/inventario`.
- Flujo TRÁNSITO estructurado, ubicación núcleo/galpón (modelo A usa `location` string).

---

## 5. RIESGOS DE REGRESIÓN al habilitar el descuento Colombia (priorizados)

| Prio | Riesgo | Causa | Evidencia | Mitigación / qué validar |
|---|---|---|---|---|
| **P0** | **Colisión de ids de catálogo → descuento del ítem/país equivocado** | `catalogo_items.id` (A) ≠ `item_inventario_ecuador.id` (B); el parser hace fallback `catalogItemId→item_inventario_ecuador_id`. Si Colombia descuenta en B sin migrar/mapear catálogo, un `catalogItemId` que colisiona con un `item_inventario_ecuador.id` real descuenta stock equivocado. | id=89 colisiona (medicamento EC vs pollita CO) — ya produjo 2 filas espurias | **NO** habilitar el descuento contra el parser con fallback. Requisito previo: catálogo Colombia migrado/mapeado a `item_inventario_ecuador` con ids propios; el descuento debe usar SOLO `itemInventarioEcuadorId>0` real, nunca el fallback. |
| **P1** | **Descuento invisible para el usuario Colombia** | Descuento va a modelo B; UI Colombia (`/inventario`) es modelo A | §1 #6 | Decidir en Fase 2: migrar Colombia a UI/modelo B, o no habilitar hasta unificar. Validar visualmente que el stock que descuenta se vea donde el usuario lo espera. |
| **P1** | **Contable inflado / doble conteo** | Solo si en el mismo slice se repunta `ReporteContableService` a modelo B o se migran movimientos A→B | §3 | Mantener contable leyendo modelo A mientras el descuento va a B. Si se unifica, auditar que bultos no se sumen dos veces (movimiento `Consumo` auto vs `seguimiento.ConsumoKg/40`). Golden del reporte contable pre/post. |
| **P2** | **Saldos negativos / validación de stock** | `RegistrarConsumoAsync` valida stock y lanza si insuficiente; para Colombia sin stock inicial en B, todo consumo fallaría (o quedaría en catch mudo→log) | `InventarioGestionService.RegistrarConsumoAsync` (línea ~1102) | Sembrar stock inicial Colombia en `inventario_gestion_stock` antes de habilitar; o política de permitir negativo/omitir. Validar comportamiento con stock 0. |
| **P2** | **Doble descuento** | Si se habilita B **y** el modelo A también empezara a descontar, o si Update recalcula diff mal | §1 (paths Update hacen diff old/new) | Un solo path de descuento (B). Test de que Update no descuenta dos veces sobre el mismo seguimiento. |
| **P3** | **Ubicación núcleo/galpón obligatoria para alimento** | En B, ítem alimento exige núcleo+galpón; lotes Colombia con `location` string no los tienen estructurados | `IsAlimento()` mira `concepto`; recepción exige núcleo/galpón si alimento | Rellenar núcleo/galpón de los lotes Colombia, o el descuento de alimento fallará. |
| **P3** | **Indicadores/producción sin impacto** (riesgo descartado) | — | §2: 0 refs a inventario en todas las fn de indicadores | Ninguna acción; confirmado que no se mueven. |
| **P4** | **Datos espurios preexistentes** | 3 filas Ecuador en `catalogo_items` + 2 filas Colombia en modelo B | §4 | Limpiar (con OK) antes de habilitar, para no arrastrar ruido a la unificación. |

---

## 6. Qué validar VISUALMENTE después de habilitar (checklist E2E por perfil)

1. **Colombia** `/inventario`: tras cargar un seguimiento levante/producción con ítems, ¿el stock refleja el descuento? (si el descuento va a B y la UI es A → NO se verá: es el bug de UX P1; confirmar decisión). Kardex Colombia sigue cuadrando (golden `fn_kardex_farm_inventory`).
2. **Colombia** reporte contable: bultos (saldo anterior/entradas/traslados/retiros/consumo H-M) **idénticos** antes/después de habilitar el descuento (deben serlo si el contable sigue leyendo modelo A). Comparar semana a semana un lote padre real.
3. **Colombia** indicadores levante/producción/engorde: valores **idénticos** antes/después (no dependen de inventario; sirve de control negativo).
4. **Ecuador/Panamá** `/gestion-inventario`: stock/movimientos/tránsito **sin regresión** (no deben aparecer filas Colombia mezcladas si el aislamiento company+pais funciona). Gastos Ecuador OK.
5. **Colisión de ids:** cargar un seguimiento de un lote Colombia cuyo `catalogItemId` colisione con un `item_inventario_ecuador.id` con stock → verificar que NO descuenta el ítem equivocado (P0). Este es el test crítico que debe pasar antes de ir a prod.

---

## Archivos clave (referencia)
- Gate: `backend/src/ZooSanMarino.Application/Calculos/InventarioConsumoGate.cs`
- Descuento (único punto): `InventarioGestionService.RegistrarConsumoAsync` (~1102)
- 4 servicios de consumo: `SeguimientoLoteLevanteService.cs`, `SeguimientoAvesEngordeEcuadorService.cs`, `SeguimientoAvesEngordeService.cs`, `SeguimientoDiarioLoteReproductoraService.cs`
- Reporte contable (riesgo #1): `ReporteContableService.cs` (`ObtenerDatosBultosAsync` 699-780, lee modelo A)
- Indicadores (NO leen inventario): `backend/sql/fn_indicadores_levante_postura.sql`, `fn_indicadores_produccion_postura.sql`, `fn_indicadores_pollo_engorde.sql`, `vw_indicadores_diarios_engorde.sql`
- Kardex Colombia (SQL): `backend/sql/fn_kardex_farm_inventory` (migración `20260703130000`)
