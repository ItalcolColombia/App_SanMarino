-- Marcar la migración FixProduccionLoteSnapshot como aplicada
-- Esto es necesario porque la tabla ya existe con la estructura correcta

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251029001730_FixProduccionLoteSnapshot', '7.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

-- Verificar que se marcó correctamente
SELECT * FROM "__EFMigrationsHistory" 
WHERE "MigrationId" = '20251029001730_FixProduccionLoteSnapshot';

