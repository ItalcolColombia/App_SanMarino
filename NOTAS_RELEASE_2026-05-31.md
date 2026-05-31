# 📝 Notas de Release — 2026-05-31

Liquidación de Pollo Engorde Ecuador (venta por factura, mermas, ajuste y sobrante) +
alineación del backend con la base de datos para despliegue a producción.

---

## 🗄️ BASE DE DATOS / MIGRACIONES

| Migración | Descripción |
|-----------|-------------|
| `20260530020432_AddVentaFacturaMermaSobranteEngorde` | Columnas de **merma** (`merma_unidades/kilos/registrada_at/por`) y **sobrante** (`aves_sobrante`) en `lote_ave_engorde`; `factura_id` + `aves_sobrante` en `movimiento_pollo_engorde`; índice parcial `ix_mpe_factura_id`. |
| `20260530203013_AddPesoIndividualSeguimientoEngorde` | Soporte de **peso individual** real en seguimiento/movimiento de engorde; recrea `fn_seguimiento_diario_engorde` y trigger de historial. |
| `20260530211846_AddFnIndicadoresPolloEngorde` | Función `fn_indicadores_pollo_engorde(p_lote_id, p_peso_ajuste, p_divisor_ajuste)`. |
| `20260531034622_FixFnSeguimientoEngordeCortePorCierreAlimento` | Fix de `fn_seguimiento_diario_engorde`: corte por cierre de alimento (versión final). |
| `20260531180558_AddMissingDbFunctionsTriggersAndViews` | **Alineación:** registra en migraciones EF todas las funciones (13), triggers (6) y vistas (3) que solo existían por scripts SQL manuales. Idempotente (`CREATE OR REPLACE` / `DROP+CREATE`). |
| `20260531184044_RecalcularSaldoAlimentoEngorde20260531` | Re-recálculo masivo de `saldo_alimento_kg` con la **función final**; respaldo en `_migracion_saldo_alimento_2026_05_31`. Idempotente y reversible. |

**Scripts SQL** (`backend/sql/`):
- `fn_seguimiento_diario_engorde.sql` *(actualizado)* · `fn_indicadores_pollo_engorde.sql` *(nuevo)*
- `backfill_factura_id_engorde.sql` *(nuevo)* · `create_lote_registro_historico_unificado.sql` *(actualizado)*

> ⚠️ **Impacto en datos al desplegar:** se recalcula `saldo_alimento_kg` de engorde
> (~1.935/3.495 filas cambian). Respaldado en `_migracion_saldo_alimento_2026_05_28` y
> `_migracion_saldo_alimento_2026_05_31` (reversible).

---

## ⚙️ BACKEND (.NET) — por módulo

### Módulo: Aves Engorde / Liquidación
- **Entidades:** `LoteAveEngorde` (merma, sobrante), `MovimientoPolloEngorde` (factura, sobrante, pesos reales).
- **Configuraciones EF:** `LoteAveEngordeConfiguration`, `MovimientoPolloEngordeConfiguration` (mapeo `peso_*_real`, índice `ix_mpe_factura_id`).
- **Servicios:** `LoteAveEngordeService` (cerrar lote con merma, `ActualizarMermaAsync`), `MovimientoPolloEngordeService` (factura por despacho, sobrante).
- **Controller:** `LoteAveEngordeController` (`PUT /api/LoteAveEngorde/{id}/merma`).
- **Interface:** `ILoteAveEngordeService`.

### Módulo: Indicador Ecuador
- **Servicio:** `IndicadorEcuadorService` (uso de `fn_indicadores_pollo_engorde`; fix peso individual = `SUM(COALESCE(peso_neto, bruto-tara))`).
- **DTOs:** `IndicadorEcuadorDto`, `IndicadorEcuadorRow` *(nuevo)*, `LiquidacionLoteEngordeDto`, `LoteAveEngordeDetailDto`, `MovimientoPolloEngordeDto`, `SeguimientoDiarioTablaFilaDto`.
- **Cálculos:** carpeta `Application/Calculos/` *(nueva)*.

### Configuración
- `appsettings.Development.json`: puerto local 5433 → 5432 (solo dev).

---

## 🖥️ FRONTEND (Angular) — por módulo

### Módulo: Aves Engorde
- `pages/modal-liquidacion-lote-engorde` (html + ts) — captura de merma/sobrante/factura en liquidación.
- `pages/tabs-principal-engorde` (html + ts).
- `services/seguimiento-aves-engorde.service.ts`.

### Módulo: Indicador Ecuador
- `components/liquidacion-reporte` (html + ts) — reporte de liquidación con nuevos indicadores.
- `pages/indicador-ecuador-list` (ts).
- `services/indicador-ecuador.service.ts`.

### Módulo: Lote Engorde
- `components/lote-engorde-list` (html).
- `services/lote-engorde.service.ts`.

### Módulo: Movimientos Pollo Engorde
- `components/modal-movimiento-pollo-engorde` (html + ts) — venta por factura, sobrante, peso individual.
- `pages/movimientos-pollo-engorde-list` (html + ts).
- `services/movimiento-pollo-engorde.service.ts`.

---

## 📚 DOCUMENTACIÓN / TRACKING
- `fase_de_desarrollo/16_mapeo_indicador_ecuador_y_plan_fn_sql.md` *(actualizado)*.
- `fase_de_desarrollo/17_validacion_entidades_vs_bd_*` *(nuevos)* — validación entidades↔BD, auditoría de funciones/triggers/vistas, mapeo completo.
- `tracker_estado.md` — estado de la validación y alineación para producción.

---

## ✅ Validación previa al despliegue
- Migraciones probadas sobre **copia cruda de producción** (estaba en `20260525131406`): las 12 pendientes aplican sin error.
- Objetos de BD alineados: 16 funciones, 7 triggers, 3 vistas; 0 discrepancias entre saldo persistido y función final.
- `dotnet build` ✅ 0 errores.
