# Plan — Seguimiento diario: catálogo de alimento desde inventario NUEVO + alimento distinto para machos

**Módulos:** `lote-levante` (modal-create-edit) y `lote-produccion` (modal-seguimiento-diario).
**País afectado:** Colombia (company_id 1 / pais_id 1, nivel granja). Ecuador/Panamá ya usan el inventario nuevo — no se tocan salvo refactor compartido neutro.
**Tipo de cambio:** 100 % frontend Angular. **Sin migración de BD ni cambios de backend** (verificado, ver §Hallazgos).

---

## Hallazgos que fundamentan el plan (verificados)

1. **El dropdown de alimento de Colombia lee del inventario VIEJO.** `modal-create-edit.component.ts:1307` (levante) y `modal-seguimiento-diario.component.ts:1088` (producción) llaman `inventarioSvc.getInventory()` → `/api/farms/{id}/inventory` (`catalogo_items`) para `!isEcuadorOrPanama`. Evidencia: cantidades del dropdown (MACHOS REPRODUCTORES 9477.40, POLLA LEVANTE 317.60) ≠ inventario nuevo (0.400, 0.600), y el ítem de pruebas **"moises" (6900 kg)** no aparece.

2. **"moises" existe SOLO en `item_inventario_ecuador` (id 208), NO en `catalogo_items`.** Por eso un fix "solo cantidades sobre el catálogo viejo" (intento 2026-07-09, nunca mergeado) no puede mostrarlo. Hay que cambiar la **fuente del catálogo** de Colombia al inventario nuevo.

3. **El backend YA soporta todo lo pedido — no requiere cambios:**
   - `ParseMetadataItemsToKg` (`MetadataEngordeCalculos.cs:26`) **acumula por id** sobre `itemsHembras + itemsMachos + itemsGenerales`: mismo alimento en H y M ⇒ **suma kg** (un solo descuento); alimentos distintos ⇒ descuentos separados. = requisito "sumar si es el mismo".
   - `ColombiaInventarioConsumoService` resuelve dos caminos: **camino-1** `catalogItemId` (catalogo_items) → código → `item_inventario_ecuador`; **camino-2** ids que no están en `catalogo_items` → `item_inventario_ecuador.id` directo (pass-through validado a Colombia). Cubre "moises".
   - El modelo ya persiste `itemsHembras[]`/`itemsMachos[]` independientes en jsonb; `consumoKgHembras/Machos` se computan de esos arrays (reportes/liquidaciones/indicadores intactos).

4. **Contrato de ids seguro (medido en BD local):** único ítem "nuevo" de Colombia = "moises" (208); **0 colisiones** entre ids de `item_inventario_ecuador` CO e ids de `catalogo_items`. Regla de envío por ítem:
   - Ítem **migrado** (su código existe en `catalogo_items`) → enviar `catalogItemId` = id de `catalogo_items` (camino-1, idéntico al comportamiento histórico).
   - Ítem **nuevo** (código no está en `catalogo_items`, p.ej. "moises") → enviar `itemInventarioEcuadorId` = id de `item_inventario_ecuador` (camino-2).
   - Disambiguación en el front por **código** (mapa `codigo→catalogo_items.id`). No depender de que los ids no colisionen.

5. **Doble descuento muerto a eliminar:** el `onSave` de Colombia (levante `:2132-2149`) hace `colombiaApplyInventoryDelta()` = POST manual de movimientos al inventario VIEJO en cada guardado. El backend ya descuenta el nuevo vía `ColombiaInventarioConsumoService`. Quitar `colombiaApplyInventoryDelta` (y helpers muertos asociados) para dejar un solo descuento (el nuevo, visible).

---

## Enfoque arquitectónico

### Parte 1 — Fuente del catálogo de alimento de Colombia = inventario nuevo

- Colombia pasa a usar la **misma maquinaria de catálogo/stock que EC/PA** dentro del modal: `gestionInventarioSvc.getItemsByType(...)` (catálogo `item_inventario_ecuador`, activos) + `gestionInventarioSvc.getStock({ farmId })` a **nivel granja** (núcleo/galpón NULL). `inventarioPorItem` se llena por id de `item_inventario_ecuador`.
- Se **carga además** el mapa `codigo(normalizado)→catalogo_items.id` (vía `CatalogoAlimentosService`/`InventarioService.getCatalogo`) para el contrato de ids del §Hallazgo 4.
- El path viejo `inventarioSvc.getInventory` queda **sin uso** para Colombia (se puede borrar el ramal muerto).
- **"Tipo de ítem" en Colombia:** ocultar el selector (decisión previa del usuario / memoria `alimentos-multiples-genero-levante`). El bloque de alimento filtra internamente por concepto `alimento`. EC/PA conservan el selector de concepto.
- **"los que están completos"** = ítems `activo=true` del inventario nuevo con código y stock consultable. Sin cambio de definición.

### Parte 2 — Alimento distinto para machos (selector de ítem por sexo)

- Reestructurar el bloque "Alimento e Ítems del Día" en **dos sub-bloques independientes**: **Hembras** (ítem + cantidad, +N filas) y **Machos** (ítem + cantidad, +N filas). Cada uno mapea a `itemsHembras[]` / `itemsMachos[]` de forma independiente.
- Caso común (mismo alimento ambos sexos): se elige el mismo ítem en ambos lados; el backend **suma** el consumo y hace **un** descuento (Hallazgo 3). Caso distinto: dos ítems, dos descuentos.
- **Visual "división":** cada sexo muestra su(s) alimento(s) y kg por separado en el modal y en la **tabla de seguimiento diario** (desglose H vs M). Excel opcional.
- Levante hoy usa fila emparejada (ítem único compartido + cantidadH/cantidadM) → se decopla. Producción ya tiene arrays `itemsHembras`/`itemsMachos` separados → verificar template y alinear.

---

## Archivos a modificar

**Frontend — Levante:**
- `features/lote-levante/pages/modal-create-edit/modal-create-edit.component.ts` — carga catálogo Colombia (nuevo), mapa código→id, `onSave` (ids por ítem + quitar `colombiaApplyInventoryDelta`), decoplar machos.
- `.../modal-create-edit.component.html` — dos sub-bloques H/M; ocultar "Tipo de ítem" en Colombia.
- `features/lote-levante/pages/tabs-principal/*` (o donde se pinte la tabla diaria) — desglose H vs M.
- Posible `models/` + `funciones/` (clean code): función pura de resolución de id por ítem (`resolver-id-inventario.funcion.ts`) + tipo de ítem de alimento.

**Frontend — Producción:**
- `features/lote-produccion/pages/modal-seguimiento-diario/modal-seguimiento-diario.component.ts` + `.html` — mismos cambios (catálogo nuevo Colombia, ids, decoplar machos, ocultar tipo de ítem).
- Tabla/listado de producción — desglose H vs M.

**Servicios:** reutilizar `GestionInventarioService` (getItemsByType/getStock) e `InventarioService.getCatalogo`. Sin endpoints nuevos.

**Backend / BD:** **ninguno.**

---

## Reglas de negocio

- Colombia descuenta **una sola vez** contra el inventario nuevo (backend). Nunca más al viejo.
- Mismo alimento en H y M ⇒ un descuento por la suma. Alimentos distintos ⇒ descuentos independientes.
- Al **editar** un registro, el backend aplica el **diff** (`AplicarDiffAsync`) por ítem; el front solo manda los arrays actuales. No re-postear movimientos manuales.
- Refactor ≠ cambio de comportamiento: EC/PA y aritmética de consumo/redondeos idénticos.
- Registros legacy (sin arrays) siguen abriéndose sin romper (compatibilidad hacia atrás ya existente).

## Casos de prueba

1. Colombia levante: dropdown lista ítems del inventario nuevo, **incluye "moises" (6900 kg)** y cantidades correctas (POLLA LEVANTE 0.600, no 317.60).
2. Guardar con "moises" (ítem nuevo) ⇒ descuenta `item_inventario_ecuador` 208 vía camino-2; stock nuevo baja; el viejo no se toca.
3. Guardar con ítem migrado (p.ej. POLLA LEVANTE) ⇒ camino-1 por código; descuenta el nuevo correctamente.
4. Mismo alimento H y M ⇒ un solo movimiento de consumo = suma de kg.
5. Alimento distinto para machos ⇒ dos movimientos; la tabla diaria muestra la división H/M.
6. Editar registro (subir/bajar kg, cambiar ítem) ⇒ diff correcto, sin doble contabilidad.
7. Producción: repetir 1–6.
8. EC/PA: sin regresión (catálogo, stock, guardado y descuento idénticos).
9. `yarn build` AOT 0 errores (solo warning bundle budget preexistente). Sin `NG0103` (referencias estables en getters de template).

## Fuera de alcance

- Borrar filas de menú viejo `/inventario` (Fase 2 aparte, requiere OK).
- Excel de desglose (opcional, si sobra tiempo).
- Reproductora/engorde (otro módulo/DTO).
