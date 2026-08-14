# Unidad de medida en el stock de inventario — TK-2026-000019

**Ticket:** TK-2026-000019 · SOPORTE · ALTA · ItalcolEcuador (company 3, país 2)
**Reporte:** «en el stock de inventario nos salen la unidad de medida en kg cuando al momento de
crear el ítem se lo crea con la unidad de medida que corresponde».
**Evidencia del usuario:** AV0374 (AMINAPOT 1LT) y AV0373 (GLIFOSATO 1LT) salen `kg` en Stock y `l`
en el catálogo de ítems.

---

## 1. Causa raíz

`inventario_gestion_stock` tiene su **propia** columna `unit` (`varchar(20) NOT NULL DEFAULT 'kg'`) y
la pantalla de Stock la muestra tal cual (`{{ s.unit }}`). Esa columna **nunca** se sincroniza con
`item_inventario_ecuador.unidad`, que es lo que el usuario elige al crear el ítem:

| Camino | Qué unidad graba hoy |
|---|---|
| `RegistrarIngresoAsync` | `req.Unit ?? "kg"` y, si la fila de stock ya existía, **hereda la vieja** (`mov.Unit = existing.Unit`; el `ON CONFLICT` no pisa `unit`) |
| `RegistrarTrasladoAsync` | `req.Unit ?? "kg"` |
| `RegistrarConsumoAsync` / nivel granja | `req.Unit ?? "kg"` |
| `ColombiaInventarioConsumoService` | `"kg"` fijo |
| `ActualizarStockAsync` (botón Editar) | **texto libre** que tipea el usuario (`maxlength=20`) |

Consecuencia medida sobre el dump de producción del 14ago26:

- **145 de 569** filas de stock tienen una unidad distinta a la del catálogo.
- Operación venía **parchando a mano** fila por fila: por eso conviven `LT`, `UND`, `GALONES`, `Gr`,
  `Ml`, `DOSIS` (escritos a mano en el modal Editar) con el vocabulario cerrado del catálogo
  (`kg, und, l, ml, g, lb, saco`). 49 movimientos `AjusteStock` con motivo
  «Ajuste manual. Anterior: 0.000 kg. Nuevo: 0 LT.» son exactamente eso.
- **0 filas de ALIMENTO divergen** ⇒ ningún saldo/cálculo de alimento se toca (ver §5).

Nada calcula con esta columna: solo se muestra y se usa como filtro del historial. La aritmética de
alimento vive en `fn_seguimiento_diario_engorde` / `SaldoAlimentoEngordeAplicador` y siempre es kg.

## 2. Decisión de diseño — una sola unidad por ítem, la del catálogo

`item_inventario_ecuador.unidad` es la **fuente de verdad** (vocabulario cerrado, editable desde
*Ítems de inventario*). El stock y los movimientos la **copian**, nunca la inventan. Es el mismo
criterio que ya usa Gastos de inventario (`InventarioGastoService` pasa `item.Unidad`).

## 3. Cambios

### 3.1 Cálculo puro (nuevo)
`backend/src/ZooSanMarino.Application/Calculos/UnidadInventarioCalculos.cs`
- `Resolver(unidadCatalogo, unidadSolicitada?)` → catálogo si tiene valor; si no, la pedida; si no, `kg`.
- `Normalizar(unidad)` → mapea las variantes legacy tipeadas a mano (`LT→l`, `Ml→ml`, `Gr→g`,
  `UND→und`, `GALONES→gal`, `DOSIS→dosis`) al vocabulario del catálogo. Solo lo usa el backfill.
- Tests xUnit en `backend/tests/ZooSanMarino.Application.Tests/UnidadInventarioCalculosTests.cs`.

### 3.2 Backend — `InventarioGestionService`
- **Lectura:** `GetStockAsync` proyecta `Resolver(x.ItemInventario.Unidad, x.Unit)` ⇒ la pantalla
  queda correcta aunque quede alguna fila legacy sin backfillear.
- **Escritura:** ingreso, traslado (misma granja / inter-granja / recepción), consumo, consumo y
  devolución nivel granja, ajuste y eliminación graban `Resolver(item.Unidad, …)`.
- `SumarStockAtomicoAsync`: el `ON CONFLICT … DO UPDATE` agrega `unit = EXCLUDED.unit` ⇒ una fila
  vieja se realinea sola en el siguiente ingreso. No toca la clave del índice único (regla dura:
  índice y `ON CONFLICT` son una sola cosa).
- `ActualizarStockAsync`: **ignora** `req.Unit` (queda por compatibilidad de wire) y usa la del
  catálogo; si la fila estaba desalineada, el ajuste la realinea y lo deja escrito en el motivo.

### 3.3 Frontend
- `gestion-inventario-page`: el input libre **Unidad** del modal de ajuste pasa a **solo lectura**
  (muestra la del catálogo). Es el mecanismo que generó `LT`/`GALONES`/`DOSIS`.
- `item-inventario-list`: el selector de unidad suma `dosis` y `gal` (hoy `kg, und, l, ml, g, lb,
  saco`) para poder representar la vacuna por dosis y el diésel por galones.

### 3.4 Migración data-only idempotente
`20260814050000_AlinearUnidadInventarioConCatalogo`
1. **Promoción al catálogo (solo ItalcolEcuador, company 3)** de los 10 ítems donde el catálogo
   quedó en el default y la corrección real la escribió operación en el stock:
   `AV0357, AV0376, SM0009, SM0047, SM0082, SM0128 → l` · `AV0444 → ml` · `SM0142 → und` ·
   `AV0512 → dosis` · `CS953502 (DIESEL) → gal`. Guardado con `WHERE unidad = '<default actual>'`
   para no pisar una corrección posterior del usuario.
2. **Alineación** de `inventario_gestion_stock.unit`, `inventario_gestion_movimiento.unit` y
   `lote_registro_historico_unificado.unidad` a `item_inventario_ecuador.unidad`
   (`IS DISTINCT FROM`, todas las empresas — Sanmarino también tiene 1 fila torcida).
   Es relabeling: ninguna cantidad cambia.
3. `Down`: no revierte datos (no hay estado anterior recuperable ni deseable); documentado.

`20260814060000_SolucionarTicketUnidadStockTK19`
- `tickets` código `TK-2026-000019` → `estado = 'SOLUCIONADO'`, `fecha_solucion`,
  `solucion_descripcion` con el texto para el usuario. `notificado_correo` intacto en `false`
  (no se dispara ningún correo). Idempotente (`WHERE estado <> 'SOLUCIONADO'`).

## 4. Reglas de negocio
- La unidad del stock **no la elige el usuario en el stock**: la elige en el catálogo del ítem.
- Cambiar la unidad de un ítem en el catálogo **no convierte cantidades** — es una etiqueta.
- Con `unidad` vacía en el catálogo (imposible por NOT NULL, pero defensivo) se cae a `kg`.

## 5. Casos de prueba
- xUnit `UnidadInventarioCalculos`: catálogo manda · catálogo vacío cae a la pedida · las dos vacías
  caen a `kg` · normalización de las 6 variantes legacy · `Normalizar` idempotente.
- Gate de datos (antes/después, `backend/sql/verificar_unidad_stock_catalogo.sql`):
  - `divergentes` pasa de **145 → 0**.
  - **0 filas de alimento** cambian de unidad en NINGUNA empresa (Ecuador, Panamá, Sanmarino, Demo,
    Santa Reyes) — ninguna suma de kilos se toca.
  - Ninguna `quantity` cambia (checksum de `sum(quantity)` por empresa idéntico).
- Smoke: Stock de ItalcolEcuador muestra `l` en AV0373/AV0374; ingreso nuevo sobre una fila legacy
  la deja alineada; el modal Editar ya no deja tipear la unidad.
- `dotnet build` + `dotnet test` + `yarn build`.

## 6. Fuera de alcance (queda reportado)
- **ItalcolPanamá** tiene los mismos 10 ítems clonados con la misma unidad por defecto, pero **0
  divergencias** en su stock: no hay evidencia de qué unidad quiere esa operación, así que no se
  promueve su catálogo. Se avisa para que decidan.
