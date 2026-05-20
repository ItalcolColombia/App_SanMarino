-- =============================================================================
-- Marcar migraciones aplicadas via SQL directo en __EFMigrationsHistory
--
-- Propósito: las estructuras ya existen en la BD (aplicadas vía SQL directo).
--            EF Core las ve como "Pending" porque no tienen registro en la tabla
--            de historial. Este script las registra sin ejecutar el código C#.
--
-- IMPORTANTE: NO incluye AddFnSeguimientoDiarioEngorde — esa se aplica via
--             dotnet ef database update (o apply_fn_seguimiento_diario_engorde.sql).
--
-- Seguridad: ON CONFLICT DO NOTHING → idempotente, seguro de re-ejecutar.
-- Ejecutar en: DBeaver / psql contra la base de datos objetivo (misma conexión
--              que usa el backend).
-- =============================================================================

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES
    -- ── 2025 ─────────────────────────────────────────────────────────────────
    ('20250115000000_AddUserFarmTable',                                              '9.0.6'),
    ('20251002005503_AddProduccionAvicolaRawOnly',                                  '9.0.6'),
    ('20251002052305_AddInventarioAvesTables',                                      '9.0.6'),
    ('20251007193923_FixMovimientoAvesConfiguration',                               '9.0.6'),
    ('20251014224901_AddSeguimientoProduccionTable',                                '9.0.6'),
    ('20251014231114_AddProduccionLoteYSeguimiento',                                '9.0.6'),
    ('20251028224534_AddFieldsToProduccionLote',                                    '9.0.6'),
    -- ── 2026 ─────────────────────────────────────────────────────────────────
    ('20260216092010_AddCompanyMenus',                                              '9.0.6'),
    ('20260221110809_AddEstadoCierreLotePosturaLevante',                            '9.0.6'),
    ('20260221121120_AddEstadoCierreLotePosturaProduccion',                         '9.0.6'),
    ('20260222091321_AddLotePosturaProduccionIdToSeguimientoDiario',                '9.0.6'),
    ('20260304193720_AddCodigoPlantillaToMapa',                                     '9.0.6'),
    ('20260325145412_AddInventarioGastoTables',                                     '9.0.6'),
    ('20260325224353_AddInventarioGastoAndFixCkMpeEstado',                          '9.0.6'),
    ('20260330071231_AddSaldoAlimentoKgSeguimientoDiarioAvesEngorde',               '9.0.6'),
    ('20260407120401_AddLotePosturaBase',                                           '9.0.6'),
    ('20260414233234_AlignLotesFkAndBackfillTrasladoHuevosLoteId',                  '9.0.6'),
    ('20260415002117_BackfillEspejoHuevoProduccionData',                            '9.0.6'),
    ('20260415003627_RealignEspejoHuevoProduccionTrasladosPorLoteId',               '9.0.6'),
    ('20260416161717_AddMenuHistorialInventario',                                   '9.0.6'),
    ('20260427061140_AddHistoricoConsumoAlimentoSeguimientoDiarioAvesEngorde',      '9.0.6'),
    ('20260427100412_AddPesoGlobalToMovimientoPolloEngorde',                        '9.0.6'),
    ('20260429011842_AddLiquidacionCierreLoteLevante',                              '9.0.6'),
    ('20260429014200_AddLiquidacionCierreClosedByUserAndGrAveDia',                  '9.0.6'),
    ('20260429020000_AddMovimientosAvesToSeguimientoDiario',                        '9.0.6'),
    ('20260430041919_DropFkLoteHistLote',                                           '9.0.6'),
    ('20260507174154_RenameTable_ProduccionAvicolaRaw_to_GuiaGenetica',             '9.0.6'),
    ('20260507181055_RenameTable_ProduccionDiaria_to_SeguimientoDiarioProduccionReproductoras', '9.0.6'),
    ('20260508030155_RenameTable_SeguimientoDiario_to_SeguimientoDiarioLevanteReproductoras',   '9.0.6'),
    ('20260508053425_UpdateMenu_ReporteTecnico_GenericLabel',                       '9.0.6'),
    ('20260517104629_SplitSeguimientoDiarioAvesEngordeByCountry',                  '9.0.6'),
    ('20260517131727_AddMenu_SeguimientoAvesEngordePanama',                        '9.0.6'),
    ('20260517135042_AddGestionClientes',                                           '9.0.6')
    -- AddFnSeguimientoDiarioEngorde NO se incluye aquí: se aplica via EF o apply_fn_seguimiento_diario_engorde.sql
ON CONFLICT (migration_id) DO NOTHING;

-- =============================================================================
-- Verificación: deben aparecer las 33 migraciones (más las que ya estaban)
-- =============================================================================

SELECT COUNT(*) AS total_registradas FROM "__EFMigrationsHistory";

-- Ver cuáles de las 34 quedaron registradas:
SELECT migration_id
FROM "__EFMigrationsHistory"
WHERE migration_id IN (
    '20250115000000_AddUserFarmTable',
    '20251002005503_AddProduccionAvicolaRawOnly',
    '20251002052305_AddInventarioAvesTables',
    '20251007193923_FixMovimientoAvesConfiguration',
    '20251014224901_AddSeguimientoProduccionTable',
    '20251014231114_AddProduccionLoteYSeguimiento',
    '20251028224534_AddFieldsToProduccionLote',
    '20260216092010_AddCompanyMenus',
    '20260221110809_AddEstadoCierreLotePosturaLevante',
    '20260221121120_AddEstadoCierreLotePosturaProduccion',
    '20260222091321_AddLotePosturaProduccionIdToSeguimientoDiario',
    '20260304193720_AddCodigoPlantillaToMapa',
    '20260325145412_AddInventarioGastoTables',
    '20260325224353_AddInventarioGastoAndFixCkMpeEstado',
    '20260330071231_AddSaldoAlimentoKgSeguimientoDiarioAvesEngorde',
    '20260407120401_AddLotePosturaBase',
    '20260414233234_AlignLotesFkAndBackfillTrasladoHuevosLoteId',
    '20260415002117_BackfillEspejoHuevoProduccionData',
    '20260415003627_RealignEspejoHuevoProduccionTrasladosPorLoteId',
    '20260416161717_AddMenuHistorialInventario',
    '20260427061140_AddHistoricoConsumoAlimentoSeguimientoDiarioAvesEngorde',
    '20260427100412_AddPesoGlobalToMovimientoPolloEngorde',
    '20260429011842_AddLiquidacionCierreLoteLevante',
    '20260429014200_AddLiquidacionCierreClosedByUserAndGrAveDia',
    '20260429020000_AddMovimientosAvesToSeguimientoDiario',
    '20260430041919_DropFkLoteHistLote',
    '20260507174154_RenameTable_ProduccionAvicolaRaw_to_GuiaGenetica',
    '20260507181055_RenameTable_ProduccionDiaria_to_SeguimientoDiarioProduccionReproductoras',
    '20260508030155_RenameTable_SeguimientoDiario_to_SeguimientoDiarioLevanteReproductoras',
    '20260508053425_UpdateMenu_ReporteTecnico_GenericLabel',
    '20260517104629_SplitSeguimientoDiarioAvesEngordeByCountry',
    '20260517131727_AddMenu_SeguimientoAvesEngordePanama',
    '20260517135042_AddGestionClientes'
)
ORDER BY migration_id;
-- Resultado esperado: 33 filas
