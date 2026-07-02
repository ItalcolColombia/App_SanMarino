# Informe — columnas en BD no mapeadas por EF (Fase 4 del refactor)

> Generado 2026-07-01 en la rama `refactor/optimizacion-multipais`, cruzando `information_schema.columns`
> (BD local, alineada con migraciones) contra el `ZooSanMarinoContextModelSnapshot`.
> 89 tablas analizadas · 11 con columnas fuera del modelo EF.
> **Ninguna se elimina sin OK explícito**; varias están VIVAS vía SQL crudo (vistas/funciones/triggers).

## Con uso en SQL crudo → NO tocar (vivas fuera de EF)

| Tabla | Columnas | Scripts SQL que las usan |
|---|---|---|
| `lote_registro_historico_unificado` | `peso_neto`, `peso_tara_real`, `promedio_peso_ave` | 9 / 4 / 2 (liquidación Ecuador, fn indicadores) |
| `menus` | `key`, `sort_order`, `is_group`, `created_at`, `updated_at` | 8 / 5 (seeds de menú) |
| `lotes` | `fecha_recepcion`, `incubadora_origen` | 1 |
| `seguimiento_diario_levante_reproductoras` | `lote_id_int` | 1 (migración de datos) |
| `seguimiento_diario_produccion_reproductoras` | `lote_produccion_id` | 1 |
| `seguimiento_lote_levante` | `medicamento_nombre`, `medicamento_dosis`, `medicamento_fecha` | 1 |

Acción recomendada: si estas columnas se usan en runtime (vistas/fn), considerar **mapearlas en EF** para que
el modelo cuente la historia completa (evita que un futuro `DropColumn` autogenerado las destruya).

## Sin uso detectado en código NI SQL → candidatas a eliminación (verificar en prod primero)

| Tabla | Columnas | Nota |
|---|---|---|
| `movimiento_aves` | `nucleo_destino_granja_id`, `nucleo_destino_nucleo_id`, `nucleo_origen_granja_id`, `nucleo_origen_nucleo_id` | probable diseño abandonado de destinos por núcleo |
| `roles` | `allow_multiple_countries`, `allow_multiple_companies` | flags nunca leídos por el back |
| `produccion_lotes` | `observaciones` | sin uso en servicios ni SQL |
| `produccion_resultado_levante` | `id` | posible falso positivo del parser (clave con otro nombre en snapshot) — verificar manualmente |

Antes de cualquier `ALTER TABLE ... DROP COLUMN` en prod: (1) verificar datos no nulos (`SELECT count(*) WHERE col IS NOT NULL`),
(2) confirmar que ningún reporte Power BI las consume, (3) backup vigente, (4) OK explícito.
