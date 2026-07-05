# Plan — Unificar inventario Colombia en el módulo nuevo + migración de datos

> **Fase:** análisis hecho (BD prod cargada en local :5433). Faltan decisiones (ver §6) antes de escribir/correr migración.
> Módulo VIEJO = `inventario` ("Inventario de Productos") · Módulo NUEVO = `gestion-inventario` ("Gestión de Inventario / por granja y ubicación").

## 1. Objetivo
Dejar UN solo inventario (el nuevo, multipaís, que era el de Ecuador — más completo, con traslados/tránsito). Migrar los datos de **Colombia** del módulo viejo al nuevo, quitar el viejo del menú (código se elimina después, tras validar).

## 2. Mapa de tablas (verificado en código + BD)
| | VIEJO (`inventario`) | NUEVO (`gestion-inventario`) |
|---|---|---|
| Stock | `farm_product_inventory` (farm+catalog_item) | `inventario_gestion_stock` (company/pais/farm/nucleo/galpon + item_inventario_ecuador_id) |
| Movimientos | `farm_inventory_movements` | `inventario_gestion_movimiento` |
| Catálogo ítems | `catalogo_items` | `item_inventario_ecuador` |
| Endpoints | `/farms/{id}/inventory/*` | `/inventario-gestion/*` |

## 3. Realidad de los datos (local :5433, prod cargada)
- `farm_product_inventory`: **20 filas Colombia (co1)**, item_type=alimento, farms 1/3/4/5/20 (todas co1), unit kg. ← saldos a migrar.
- `inventario_gestion_stock`: 373 filas (**solo 1 Colombia**, 372 Ecuador). ← Colombia casi no está.
- `catalogo_items`: 61 ítems Colombia (co1). `item_inventario_ecuador`: **146 ítems, TODOS company 3 (Ecuador), 0 de Colombia.**
- ⇒ **La "migración de ítems" NO está aplicada:** los ítems Colombia siguen solo en el catálogo viejo; 0 coinciden por código en el nuevo. Sin ítems en `item_inventario_ecuador`, el stock nuevo no tiene a qué apuntar.
- **El esquema del destino YA está alineado** (`inventario_gestion_stock` tiene company_id/pais_id/farm_id/nucleo_id/galpon_id) → **no hace falta DDL en esa tabla**, solo DML (migrar datos).

## 4. Migración de datos propuesta (idempotente, por pasos)
Colombia = company_id **1**, pais_id **1**. Modelo B (nivel granja) ⇒ nucleo_id/galpon_id **NULL**.
1. **Ítems** `catalogo_items`(co1) → `item_inventario_ecuador`(co1, pais1): `INSERT ... WHERE NOT EXISTS` por (company_id, codigo). Mapeo: codigo→codigo, nombre→nombre, item_type→tipo_item, activo→activo, unidad='kg' (default), concepto/grupo/etc → derivar o NULL.
2. **Stock** `farm_product_inventory`(co1) → `inventario_gestion_stock`(co1, pais1, farm_id, nucleo/galpon NULL, item_inventario_ecuador_id vía join por codigo del paso 1, quantity, unit). `WHERE NOT EXISTS` por (farm_id, item_inventario_ecuador_id, nucleo, galpon).
3. **(Opcional / a confirmar) Movimientos/kardex** `farm_inventory_movements`(co1, 323) → `inventario_gestion_movimiento` con mapeo de movementType (Entry→Ingreso, Exit→…, Transfer→…). Más complejo; puede quedar fuera del "por ahora".

## 5. Menú (duplicado)
El sidebar muestra **"Gestión de Inventario" 3 veces** + el viejo "Gestion de Inventario". Es data-driven (`menus`/`company_menus`/`role_menus`). Hay que: quitar el ítem del inventario VIEJO del menú de Colombia y **deduplicar** las entradas del nuevo (dejar 1). Sin borrar código del viejo todavía.

## 6. DECISIONES / BLOQUEOS abiertos (antes de ejecutar)
- ⛔ **¿Qué BD usa realmente la app?** appsettings dice **:5432**, pero psql solo alcanza **:5433 (docker)**, que es donde está la data prod. Necesito saber dónde probar/aplicar la migración (dónde lee la app).
- ❓ **Alcance "por ahora":** ¿solo ítems + stock (saldos), o también movimientos/histórico? ¿y el re-cableo del *consumo* de seguimiento diario al módulo nuevo (cambio de código, riesgoso — "Slice 2b")?
- ❓ Confirmar Colombia = nivel granja (nucleo/galpon NULL) en el nuevo módulo.

## 7. Reglas
- Migración **idempotente** (INSERT ... WHERE NOT EXISTS); probar en local primero; **confirmar antes de tocar prod** (DML masivo).
- El código manda: el destino ya tiene el esquema; no forzar DDL innecesario.
- Backfill masivo Colombia → SQL crudo en `/backend/sql/` (mezcla DML), no migración EF de columnas.
