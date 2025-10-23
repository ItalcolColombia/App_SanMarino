-- =====================================================
-- SOLUCIÓN FINAL: Marcar migración como aplicada
-- =====================================================
-- Ejecutar este script en tu base de datos PostgreSQL

-- 1. Insertar el registro de migración en la tabla de historial
INSERT INTO "__EFMigrationsHistory" (
    "MigrationId", 
    "ProductVersion"
) VALUES (
    '20251002005503_AddProduccionAvicolaRawOnly',
    '9.0.6'
) ON CONFLICT ("MigrationId") DO NOTHING;

-- 2. Verificar que se insertó correctamente
SELECT * FROM "__EFMigrationsHistory" 
WHERE "MigrationId" = '20251002005503_AddProduccionAvicolaRawOnly';

-- 3. Verificar que la tabla produccion_avicola_raw existe
SELECT 
    table_name,
    column_name,
    data_type
FROM information_schema.columns 
WHERE table_name = 'produccion_avicola_raw' 
ORDER BY ordinal_position;

-- 4. Mensaje de confirmación
DO $$
BEGIN
    RAISE NOTICE 'Migración marcada como aplicada exitosamente.';
    RAISE NOTICE 'La API está lista para usar la tabla produccion_avicola_raw.';
END $$;
