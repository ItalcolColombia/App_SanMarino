-- =====================================================
-- SCRIPT PARA CREAR TABLA produccion_avicola_raw
-- =====================================================
-- Ejecutar este script directamente en PostgreSQL
-- Asegúrate de estar conectado a la base de datos correcta

-- Crear la tabla principal
CREATE TABLE IF NOT EXISTS produccion_avicola_raw (
    -- Campos de auditoría (heredados de AuditableEntity)
    id SERIAL PRIMARY KEY,
    company_id INTEGER NOT NULL,
    created_by_user_id INTEGER NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_by_user_id INTEGER,
    updated_at TIMESTAMP WITH TIME ZONE,
    deleted_at TIMESTAMP WITH TIME ZONE,
    
    -- Campos de datos (todos como TEXT según tu esquema original)
    anio_guia TEXT,
    raza TEXT,
    edad TEXT,
    mort_sem_h TEXT,
    retiro_ac_h TEXT,
    mort_sem_m TEXT,
    retiro_ac_m TEXT,
    cons_ac_h TEXT,
    cons_ac_m TEXT,
    gr_ave_dia_h TEXT,
    gr_ave_dia_m TEXT,
    peso_h TEXT,
    peso_m TEXT,
    uniformidad TEXT,
    h_total_aa TEXT,
    prod_porcentaje TEXT,
    h_inc_aa TEXT,
    aprov_sem TEXT,
    peso_huevo TEXT,
    masa_huevo TEXT,
    grasa_porcentaje TEXT,
    nacim_porcentaje TEXT,
    pollito_aa TEXT,
    kcal_ave_dia_h TEXT,
    kcal_ave_dia_m TEXT,
    aprov_ac TEXT,
    gr_huevo_t TEXT,
    gr_huevo_inc TEXT,
    gr_pollito TEXT,
    valor_1000 TEXT,
    valor_150 TEXT,
    apareo TEXT,
    peso_mh TEXT
);

-- =====================================================
-- CREAR ÍNDICES PARA MEJORAR RENDIMIENTO
-- =====================================================

-- Índice en año de guía (para búsquedas por año)
CREATE INDEX IF NOT EXISTS ix_produccion_avicola_raw_anio_guia 
ON produccion_avicola_raw(anio_guia);

-- Índice en raza (para búsquedas por raza)
CREATE INDEX IF NOT EXISTS ix_produccion_avicola_raw_raza 
ON produccion_avicola_raw(raza);

-- Índice en company_id (para filtros por empresa)
CREATE INDEX IF NOT EXISTS ix_produccion_avicola_raw_company_id 
ON produccion_avicola_raw(company_id);

-- Índice en created_at (para ordenamiento temporal)
CREATE INDEX IF NOT EXISTS ix_produccion_avicola_raw_created_at 
ON produccion_avicola_raw(created_at);

-- =====================================================
-- AGREGAR COMENTARIOS A LA TABLA Y COLUMNAS
-- =====================================================

-- Comentario en la tabla
COMMENT ON TABLE produccion_avicola_raw IS 'Tabla para almacenar datos de producción avícola en formato raw (texto)';

-- Comentarios en columnas principales
COMMENT ON COLUMN produccion_avicola_raw.id IS 'Identificador único de la tabla';
COMMENT ON COLUMN produccion_avicola_raw.company_id IS 'ID de la empresa propietaria del registro';
COMMENT ON COLUMN produccion_avicola_raw.anio_guia IS 'Año de la guía de producción';
COMMENT ON COLUMN produccion_avicola_raw.raza IS 'Raza de las aves (ej: Cobb 500, Ross 308)';
COMMENT ON COLUMN produccion_avicola_raw.edad IS 'Edad de las aves en semanas';
COMMENT ON COLUMN produccion_avicola_raw.mort_sem_h IS 'Mortalidad semanal hembras';
COMMENT ON COLUMN produccion_avicola_raw.mort_sem_m IS 'Mortalidad semanal machos';
COMMENT ON COLUMN produccion_avicola_raw.peso_h IS 'Peso promedio hembras';
COMMENT ON COLUMN produccion_avicola_raw.peso_m IS 'Peso promedio machos';
COMMENT ON COLUMN produccion_avicola_raw.uniformidad IS 'Uniformidad del lote';
COMMENT ON COLUMN produccion_avicola_raw.prod_porcentaje IS 'Porcentaje de producción';
COMMENT ON COLUMN produccion_avicola_raw.peso_huevo IS 'Peso promedio del huevo';
COMMENT ON COLUMN produccion_avicola_raw.masa_huevo IS 'Masa total de huevos';
COMMENT ON COLUMN produccion_avicola_raw.grasa_porcentaje IS 'Porcentaje de grasa';
COMMENT ON COLUMN produccion_avicola_raw.nacim_porcentaje IS 'Porcentaje de nacimiento';
COMMENT ON COLUMN produccion_avicola_raw.pollito_aa IS 'Número de pollitos por ave alojada';
COMMENT ON COLUMN produccion_avicola_raw.valor_1000 IS 'Valor por 1000 pollitos';
COMMENT ON COLUMN produccion_avicola_raw.valor_150 IS 'Valor por 150 pollitos';
COMMENT ON COLUMN produccion_avicola_raw.apareo IS 'Información de apareo';
COMMENT ON COLUMN produccion_avicola_raw.peso_mh IS 'Peso macho/hembra';

-- =====================================================
-- VERIFICAR QUE LA TABLA SE CREÓ CORRECTAMENTE
-- =====================================================

-- Consulta para verificar la estructura de la tabla
SELECT 
    column_name,
    data_type,
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name = 'produccion_avicola_raw' 
ORDER BY ordinal_position;

-- Consulta para verificar los índices
SELECT 
    indexname,
    indexdef
FROM pg_indexes 
WHERE tablename = 'produccion_avicola_raw';

-- =====================================================
-- MENSAJE DE CONFIRMACIÓN
-- =====================================================
DO $$
BEGIN
    RAISE NOTICE 'Tabla produccion_avicola_raw creada exitosamente con todos los índices y comentarios.';
    RAISE NOTICE 'La tabla está lista para ser utilizada por la API.';
END $$;
