# Clasificación de scripts SQL (`/backend/sql/`)

> Generado en la rama `refactor/optimizacion-multipais` (Fase 1 del plan de refactor).
> Objetivo: distinguir **objetos vivos** (deben mantenerse sincronizados con la BD) de **one-shots históricos** (ya aplicados; solo valor de auditoría). **Ningún script se borra**: esto es documentación.

## 1. Objetos VIVOS — fuente de verdad de funciones/vistas/triggers

Estos definen objetos que existen en BD y el backend/Power BI consumen. Cualquier cambio va aquí + migración EF idempotente que los re-aplique.

| Script | Objeto | Consumidor |
|---|---|---|
| `fn_seguimiento_diario_engorde.sql` | función | seguimiento engorde (back) |
| `fn_indicadores_pollo_engorde.sql` | función | indicadores engorde (back) |
| `fn_informe_semanal_pollo_engorde.sql` | función | informe semanal Panamá |
| `fn_auditoria_liquidacion_engorde.sql` | función | auditoría liquidación Ecuador |
| `fn_aplicar_correccion_despachos_sin_peso.sql` | función | corrección despachos |
| `fn_cruce_reproductora_a_engorde.sql` | función | cruce reproductora→engorde |
| `vw_liquidacion_ecuador_pollo_engorde.sql` | vista | **Power BI Ecuador — NO renombrar** |
| `vw_seguimiento_pollo_engorde.sql` (+`_add_company_id`) | vista | **Power BI Ecuador — NO renombrar** |
| `vw_indicadores_diarios_engorde.sql` | vista | **Power BI Ecuador — NO renombrar** |
| `vw_guia_genetica_por_lote_postura.sql` | vista | postura Colombia |
| `vw_validacion_alimento_engorde.sql` | vista | validación alimento |
| `liquidacion_indicador_ecuador_pollo_engorde_vista.sql` | vista | indicador Ecuador |
| `indicadores_diarios_engorde_tabla_unificada*.sql` | vista/tabla | engorde unificado |
| `seguimiento_pollo_engorde_tabla_unificada*.sql` | vista/tabla | engorde unificado |
| `trigger_espejo_huevo_produccion_seguimiento_diario.sql` | trigger | espejo huevos |
| `trigger_lotes_to_lote_postura_levante.sql` | trigger | postura |
| `trigger_lote_postura_levante_cerrar_produccion.sql` | trigger | postura (ver `disable_trg_...` que lo desactivó) |
| `create_user_dwh_readonly.sql` | rol BD | Power BI / DWH |

## 2. DDL fundacional (histórico, ya absorbido por migraciones EF)

`create_*.sql` (tablas), `crear_tabla_*.sql`, `script_crear_*.sql`, `inventario_gestion_tables.sql`, `item_inventario_ecuador_table.sql`, `ensure_lote_reproductoras_structure.sql`. Ya aplicados; el schema actual lo gobiernan las migraciones EF. Solo referencia histórica.

## 3. Parches de schema históricos (ya aplicados)

Todos los `add_*.sql`, `alter_*.sql`, `agregar_*.sql`, `allow_null_*.sql`, `ALTER_TABLE_campos_faltantes.sql`, `apply_*.sql`, `patch_add_clientes.sql`, `SCRIPT_COMPLETO_CAMPOS_PRODUCCION.sql`. Nota: los `add_*_menu.sql` son seeds de menú por módulo (idempotentes con WHERE NOT EXISTS en su mayoría).

## 4. One-shots de datos (backfills / correcciones / migraciones de datos)

`backfill_*.sql`, `migracion_*.sql`, `migrar_*.sql`, `migrate_*.sql`, `fix_*.sql`, `correccion_*.sql`, `corregir_*.sql`, `cuadre_*.sql`, `actualizar_*.sql`, `alinear_*.sql`, `revertir_*.sql`, `tmp_repair_*.sql`, `carga_prueba_*.sql`, `reorganize_menu_order_and_groups.sql`, `inventario_gestion_limpiar_*.sql`, `inventario_gestion_movimiento_borrar_todo.sql` (⚠️ destructivo, solo local), `opcion_b_unificar_lote_fase.sql`, `disable_trg_lpl_cerrar_produccion.sql`, `solucion_final_migraciones.sql`, `lote_ave_engorde_estado_operativo_liquida.sql`, `seguimiento_diario_aves_engorde_saldo_alimento_kg.sql`.

⚠️ **Nunca re-ejecutar en prod** sin auditoría previa: son fotos de un momento.

## 5. Diagnóstico / verificación (solo lectura, reutilizables)

`verify_*.sql`, `verificar_*.sql`, `diagnose_*.sql`, `diagnostico_*.sql`, `consulta_*.sql`, `validacion_*.sql`, `audit_*.sql`.

## 6. ⛔ Cuarentena — NO ejecutar jamás

| Script | Motivo |
|---|---|
| `marcar_todas_migraciones_pendientes.sql` | **Causa raíz del incidente SIGSEGV en ECS**: marcó como aplicadas migraciones nunca ejecutadas. Se conserva solo como evidencia. |
| `marcar_migracion_aplicada.sql`, `mark_migration_applied.sql`, `mark_fix_produccion_lote_applied.sql` | Insertan a mano en `__EFMigrationsHistory`; prohibido por CLAUDE.md salvo auditoría completa. |

## 7. Documentación

`README.md`, `README_ESPEJO_HUEVOS.md`, `CROQUIS_LOTE_ETAPAS_HISTORIAL.md`, `INSTRUCCIONES_*.md`, `RESUMEN_CAMBIOS_LOTE_REPRODUCTORA.md`, `ejecutar_menu_reportes_tecnicos.ps1`.
