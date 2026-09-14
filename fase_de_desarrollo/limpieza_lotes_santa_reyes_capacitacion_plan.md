# Limpieza de lotes de Santa Reyes tras la capacitación — plan

**Fecha:** 14-sep-2026 · **Tipo:** operativo de una sola vez (SQL), sin cambio de código.
**Entregable:** `backend/sql/migracion_limpieza_lotes_santa_reyes_capacitacion.sql`

## Objetivo

Después de la capacitación, dejar Santa Reyes sin los lotes que se crearon en la aplicación
(con todo lo que cuelga de ellos) para que el equipo los vuelva a registrar desde cero.

## Decisiones del usuario (14-sep-2026)

1. **Inventario de alimento → a cero.** Se borran ingresos, consumos, traslados y stock de
   Santa Reyes (igual que la limpieza de Demo). Luego se registran otra vez los ingresos reales
   **antes** de cargar seguimientos (un consumo sin stock se pierde en silencio).
2. **Se conservan los 10 «Lote base»** (`lote_postura_base`, LOTE 216…234, códigos ERP G300xxxx):
   los sembró la migración `20260726030933` desde el Excel del cliente, no la app.

## Enfoque

- **No va por migración EF.** Una migración que borra datos operativos se re-ejecutaría en
  cualquier entorno nuevo y no tiene `Down()`. Prefijo `migracion_*` = exento del gate
  `verificar-sql-llega-por-migracion.js` (operativo de una sola vez).
- **Molde:** `backend/sql/migracion_limpieza_demo_practica_costos.sql` (28-ago-2026), ampliado con
  las tablas propias de Santa Reyes (silos por lote, ítems de huevo, cohortes, traslado de huevos,
  reservas del seguimiento) y **conservando** `lote_postura_base`.
- **Fail-closed por empresa:** se resuelve UNA vez por `identifier = '901000001-1'` **y**
  `name = 'Santa Reyes'`; si no hay exactamente 1, aborta sin tocar nada.
- **Ensayo por defecto:** el archivo termina en `ROLLBACK;`. Para aplicarlo se cambia a `COMMIT;`.

## Qué borra (orden respetando FKs RESTRICT medidas con `pg_constraint`)

| Paso | Tablas |
|---|---|
| Hojas RESTRICT | `vacunacion_cronograma_item` (+cascade `vacunacion_registro_aplicacion`), `traslado_huevos` |
| Seguimientos | `seguimiento_diario_levante`, `seguimiento_diario_produccion`, `seguimiento_reserva_alimento`, `seguimiento_reserva_aves` |
| Históricos/espejos | `lote_registro_historico_unificado`, `historico_lote_postura`, `espejo_huevo_produccion`, `liquidacion_cierre_lote_levante` |
| Inventario | `inventario_gestion_movimiento`, `inventario_gestion_stock`, `farm_inventory_movements`, `farm_product_inventory`, `inventario_aves`, `inventario_gasto`, `historial_inventario` |
| Movimientos/otros | `movimiento_aves`, `historial_traslado_lote`, `lesiones`, `lote_seguimientos`, `produccion_lotes`, `migracion_masiva` |
| Lotes | `lote_postura_produccion` → `lote_postura_levante` → `lote_aves_cohortes` → `lotes` (cascade: `lote_etapa_levante`, `lote_huevo_items`, `lote_silos`, `reporte_tecnico_guia`, `user_farm_scopes`) |

## Qué NO toca

Empresa y configuración (`companies`, flags, `company_*`), estructura (`farms`, `nucleos`, `galpones`),
silos (`farm_silos`, `galpon_silos`, `silo_catalogo`), catálogos (`catalogo_items`, `item_inventario`,
`master_lists`), guía genética propia, **lotes base**, usuarios/roles, tickets/ItalJira.

## Efectos colaterales conocidos

- `trg_sync_tombstone` deja lápidas en `sync_tombstones` por cada seguimiento/movimiento borrado:
  es lo que hace que la app móvil los quite de su copia local. Deseado.
- `trg_inventario_gestion_movimiento_lote_hist_del` intenta anular el histórico: como el histórico
  de la empresa se borra antes, no encuentra filas (no-op).
- La migración `20260905200000_SeedLotePruebaPoblacionSantaReyes` ya está aplicada: el lote
  `SR-2025-01` que sembró se borra y **no** vuelve a aparecer en un deploy.

## Casos de prueba (BD local = copia de prod, con ROLLBACK)

1. Empresa inexistente / identifier distinto → aborta con excepción, 0 filas tocadas.
2. Conteos DESPUÉS de Santa Reyes: todo lo operativo en 0; `lote_postura_base`=10, `farms`,
   `nucleos`, `galpones`, `farm_silos` sin cambio.
3. Control multiempresa: las otras 4 empresas con los mismos conteos antes y después.
4. Ninguna violación de FK (con `ON_ERROR_STOP` el script cortaría).
5. Corrida doble (idempotencia): la segunda pasada borra 0 filas.
