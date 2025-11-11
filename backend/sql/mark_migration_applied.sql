-- ============================================================
-- Marcar la migración como aplicada manualmente
-- ============================================================
-- Ejecuta este SQL después de aplicar los cambios manualmente
-- ============================================================

-- Verificar si existe la tabla de migraciones
SELECT * FROM "__EFMigrationsHistory" 
WHERE "MigrationId" LIKE '%AddFieldsToProduccionLote%';

-- Marcar la migración como aplicada
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251028224534_AddFieldsToProduccionLote', '7.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

-- Verificar que se registró correctamente
SELECT * FROM "__EFMigrationsHistory" 
ORDER BY "MigrationId" DESC 
LIMIT 5;



