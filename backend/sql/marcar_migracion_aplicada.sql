-- Script para marcar la migración como aplicada manualmente
-- Ejecutar este script en tu base de datos PostgreSQL

-- Insertar el registro en la tabla __EFMigrationsHistory
INSERT INTO "__EFMigrationsHistory" (
    "MigrationId", 
    "ProductVersion"
) VALUES (
    '20251002004759_AddProduccionAvicolaRawTable',
    '9.0.6'
) ON CONFLICT ("MigrationId") DO NOTHING;

-- Verificar que se insertó correctamente
SELECT * FROM "__EFMigrationsHistory" 
WHERE "MigrationId" = '20251002004759_AddProduccionAvicolaRawTable';
