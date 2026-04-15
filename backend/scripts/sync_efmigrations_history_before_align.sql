-- Marca como aplicadas las migraciones cuyo esquema YA EXISTE en la base (SQL manual, otro entorno, etc.)
-- y cuyo registro falta en __EFMigrationsHistory.
--
-- ¿Cuándo usarlo en PRODUCCIÓN?
--   Sí, una sola vez, si al arrancar la API o al ejecutar `dotnet ef database update` falla porque intenta
--   crear tablas/objetos que ya existen (mismo problema que en dev).
--   No hace falta si __EFMigrationsHistory ya lista todas las migraciones hasta la última desplegada.
--
-- Orden recomendado:
--   1) Backup de la base.
--   2) Ejecutar este script contra la BD de prod (psql, DBeaver, etc.).
--   3) Desplegar la API con RunMigrations=true o ejecutar `dotnet ef database update` apuntando a prod.
--      EF aplicará solo las migraciones que falten (p. ej. 20260414233234_AlignLotesFkAndBackfillTrasladoHuevosLoteId).
--
-- ProductVersion: alinear con Microsoft.EntityFrameworkCore del proyecto (p. ej. 9.0.6).

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version) VALUES
('20250115000000_AddUserFarmTable', '9.0.6'),
('20250901200628_Initial_Aligned', '9.0.6'),
('20250905153450_RenameCiudadMunicipio_DepartamentoFarm', '9.0.6'),
('20250908191526_add_extras_to_seguimiento_levante', '9.0.6'),
('20251002005503_AddProduccionAvicolaRawOnly', '9.0.6'),
('20251002052305_AddInventarioAvesTables', '9.0.6'),
('20251007193923_FixMovimientoAvesConfiguration', '9.0.6'),
('20251014224901_AddSeguimientoProduccionTable', '9.0.6'),
('20251014231114_AddProduccionLoteYSeguimiento', '9.0.6'),
('20251028224534_AddFieldsToProduccionLote', '9.0.6'),
('20251029001730_FixProduccionLoteSnapshot', '9.0.6'),
('20260216092010_AddCompanyMenus', '9.0.6'),
('20260221110809_AddEstadoCierreLotePosturaLevante', '9.0.6'),
('20260221121120_AddEstadoCierreLotePosturaProduccion', '9.0.6'),
('20260222091321_AddLotePosturaProduccionIdToSeguimientoDiario', '9.0.6'),
('20260304193720_AddCodigoPlantillaToMapa', '9.0.6'),
('20260325145412_AddInventarioGastoTables', '9.0.6'),
('20260325224353_AddInventarioGastoAndFixCkMpeEstado', '9.0.6'),
('20260330071231_AddSaldoAlimentoKgSeguimientoDiarioAvesEngorde', '9.0.6'),
('20260407120401_AddLotePosturaBase', '9.0.6')
ON CONFLICT (migration_id) DO NOTHING;
