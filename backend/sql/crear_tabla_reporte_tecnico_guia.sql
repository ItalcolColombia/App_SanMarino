-- =====================================================
-- SCRIPT PARA CREAR TABLA reporte_tecnico_guia
-- =====================================================
-- NOTA: Esta tabla es OPCIONAL. El sistema ahora usa principalmente
-- la tabla produccion_avicola_raw para obtener los valores de guía genética
-- basándose en la Raza y AnoTablaGenetica del lote.
-- 
-- Esta tabla puede usarse para almacenar valores manuales adicionales
-- que no estén en la guía genética estándar (como ErrSexAcH, ErrSexAcM, etc.)
-- =====================================================
-- Ejecutar este script directamente en PostgreSQL
-- Asegúrate de estar conectado a la base de datos correcta

BEGIN;

-- =====================================================
-- CREAR TABLA reporte_tecnico_guia
-- =====================================================

CREATE TABLE IF NOT EXISTS reporte_tecnico_guia (
    -- Campos de auditoría (heredados de AuditableEntity)
    id SERIAL PRIMARY KEY,
    company_id INTEGER NOT NULL,
    created_by_user_id INTEGER NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_by_user_id INTEGER,
    updated_at TIMESTAMP WITH TIME ZONE,
    deleted_at TIMESTAMP WITH TIME ZONE,
    
    -- Identificación del lote y semana
    lote_id INTEGER NOT NULL,
    semana INTEGER NOT NULL CHECK (semana >= 1 AND semana <= 25),
    
    -- ========== VALORES GUÍA HEMBRAS ==========
    porc_mort_h_guia NUMERIC(8, 3),
    retiro_h_guia NUMERIC(8, 3),
    cons_ac_gr_h_guia NUMERIC(10, 2),
    gr_ave_dia_guia_h NUMERIC(8, 2),
    incr_cons_h_guia NUMERIC(8, 2),
    peso_h_guia NUMERIC(8, 2),
    unif_h_guia NUMERIC(5, 2),
    
    -- ========== VALORES GUÍA MACHOS ==========
    porc_mort_m_guia NUMERIC(8, 3),
    retiro_m_guia NUMERIC(8, 3),
    cons_ac_gr_m_guia NUMERIC(10, 2),
    gr_ave_dia_guia_m NUMERIC(8, 2),
    incr_cons_m_guia NUMERIC(8, 2),
    peso_m_guia NUMERIC(8, 2),
    unif_m_guia NUMERIC(5, 2),
    
    -- ========== VALORES GUÍA NUTRICIONALES HEMBRAS ==========
    alim_h_guia VARCHAR(100),
    kcal_sem_h_guia NUMERIC(12, 3),
    prot_sem_h_guia NUMERIC(8, 3),
    
    -- ========== VALORES GUÍA NUTRICIONALES MACHOS ==========
    alim_m_guia VARCHAR(100),
    kcal_sem_m_guia NUMERIC(12, 3),
    prot_sem_m_guia NUMERIC(8, 3),
    
    -- ========== ERROR SEXAJE ACUMULADO ==========
    err_sex_ac_h INTEGER,
    err_sex_ac_m INTEGER,
    
    -- ========== DATOS MANUALES ADICIONALES ==========
    cod_guia VARCHAR(50),
    id_lote_rap VARCHAR(50),
    traslado INTEGER,
    nucleo_l VARCHAR(50),
    anon INTEGER,
    
    -- Constraint: Un solo registro por lote y semana
    CONSTRAINT unique_reporte_tecnico_guia_lote_semana UNIQUE (lote_id, semana),
    
    -- Foreign Key a tabla lotes
    CONSTRAINT fk_reporte_tecnico_guia_lote 
        FOREIGN KEY (lote_id) 
        REFERENCES lotes(lote_id) 
        ON DELETE CASCADE
);

-- =====================================================
-- CREAR ÍNDICES PARA MEJORAR RENDIMIENTO
-- =====================================================

-- Índice único para lote + semana (ya existe como constraint, pero agregamos índice para búsquedas)
CREATE INDEX IF NOT EXISTS ix_reporte_tecnico_guia_lote_semana 
    ON reporte_tecnico_guia(lote_id, semana);

-- Índice en lote_id para búsquedas por lote
CREATE INDEX IF NOT EXISTS ix_reporte_tecnico_guia_lote_id 
    ON reporte_tecnico_guia(lote_id);

-- Índice en semana para búsquedas por semana
CREATE INDEX IF NOT EXISTS ix_reporte_tecnico_guia_semana 
    ON reporte_tecnico_guia(semana);

-- Índice en company_id para filtros por empresa
CREATE INDEX IF NOT EXISTS ix_reporte_tecnico_guia_company_id 
    ON reporte_tecnico_guia(company_id);

-- Índice para soft delete
CREATE INDEX IF NOT EXISTS ix_reporte_tecnico_guia_deleted_at 
    ON reporte_tecnico_guia(deleted_at) 
    WHERE deleted_at IS NULL;

-- =====================================================
-- COMENTARIOS EN LA TABLA Y COLUMNAS
-- =====================================================

COMMENT ON TABLE reporte_tecnico_guia IS 
    'Almacena valores de guía (GUIA) manuales para el reporte técnico de Levante. Estos valores se usan para comparación con los valores reales calculados.';

COMMENT ON COLUMN reporte_tecnico_guia.lote_id IS 
    'ID del lote al que pertenece esta guía';

COMMENT ON COLUMN reporte_tecnico_guia.semana IS 
    'Semana de levante (1-25) para la cual se define esta guía';

COMMENT ON COLUMN reporte_tecnico_guia.porc_mort_h_guia IS 
    'Porcentaje de mortalidad hembras guía (para comparación)';

COMMENT ON COLUMN reporte_tecnico_guia.retiro_h_guia IS 
    'Porcentaje de retiro hembras guía (para comparación)';

COMMENT ON COLUMN reporte_tecnico_guia.cons_ac_gr_h_guia IS 
    'Consumo acumulado en gramos hembras guía (para comparación)';

COMMENT ON COLUMN reporte_tecnico_guia.gr_ave_dia_guia_h IS 
    'Gramos por ave por día hembras guía (para comparación)';

COMMENT ON COLUMN reporte_tecnico_guia.peso_h_guia IS 
    'Peso hembras guía (para comparación)';

COMMENT ON COLUMN reporte_tecnico_guia.unif_h_guia IS 
    'Uniformidad hembras guía (para comparación)';

COMMENT ON COLUMN reporte_tecnico_guia.porc_mort_m_guia IS 
    'Porcentaje de mortalidad machos guía (para comparación)';

COMMENT ON COLUMN reporte_tecnico_guia.retiro_m_guia IS 
    'Porcentaje de retiro machos guía (para comparación)';

COMMENT ON COLUMN reporte_tecnico_guia.cons_ac_gr_m_guia IS 
    'Consumo acumulado en gramos machos guía (para comparación)';

COMMENT ON COLUMN reporte_tecnico_guia.gr_ave_dia_guia_m IS 
    'Gramos por ave por día machos guía (para comparación)';

COMMENT ON COLUMN reporte_tecnico_guia.peso_m_guia IS 
    'Peso machos guía (para comparación)';

COMMENT ON COLUMN reporte_tecnico_guia.unif_m_guia IS 
    'Uniformidad machos guía (para comparación)';

COMMENT ON COLUMN reporte_tecnico_guia.alim_h_guia IS 
    'Tipo de alimento hembras guía';

COMMENT ON COLUMN reporte_tecnico_guia.kcal_sem_h_guia IS 
    'Kilocalorías semanales hembras guía';

COMMENT ON COLUMN reporte_tecnico_guia.prot_sem_h_guia IS 
    'Proteína semanal hembras guía';

COMMENT ON COLUMN reporte_tecnico_guia.alim_m_guia IS 
    'Tipo de alimento machos guía';

COMMENT ON COLUMN reporte_tecnico_guia.kcal_sem_m_guia IS 
    'Kilocalorías semanales machos guía';

COMMENT ON COLUMN reporte_tecnico_guia.prot_sem_m_guia IS 
    'Proteína semanal machos guía';

COMMENT ON COLUMN reporte_tecnico_guia.err_sex_ac_h IS 
    'Error de sexaje acumulado hembras (manual)';

COMMENT ON COLUMN reporte_tecnico_guia.err_sex_ac_m IS 
    'Error de sexaje acumulado machos (manual)';

COMMENT ON COLUMN reporte_tecnico_guia.cod_guia IS 
    'Código de guía genética (manual)';

COMMENT ON COLUMN reporte_tecnico_guia.id_lote_rap IS 
    'ID del lote RAP (manual)';

COMMENT ON COLUMN reporte_tecnico_guia.traslado IS 
    'Número de traslado (manual)';

COMMENT ON COLUMN reporte_tecnico_guia.nucleo_l IS 
    'Núcleo del lote (manual)';

COMMENT ON COLUMN reporte_tecnico_guia.anon IS 
    'Año del núcleo (manual)';

-- =====================================================
-- VERIFICACIÓN DE LA TABLA
-- =====================================================

-- Verificar que la tabla se creó correctamente
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM information_schema.tables 
        WHERE table_schema = 'public' 
        AND table_name = 'reporte_tecnico_guia'
    ) THEN
        RAISE NOTICE '✅ Tabla reporte_tecnico_guia creada exitosamente';
    ELSE
        RAISE EXCEPTION '❌ Error: La tabla reporte_tecnico_guia no se creó';
    END IF;
END $$;

COMMIT;

-- =====================================================
-- INSTRUCCIONES DE USO
-- =====================================================
-- 
-- Para ejecutar este script:
-- 
-- Opción 1: Desde psql
--   psql -U usuario -d nombre_base_datos -f crear_tabla_reporte_tecnico_guia.sql
-- 
-- Opción 2: Desde pgAdmin
--   1. Abrir pgAdmin
--   2. Conectarse a la base de datos
--   3. Abrir Query Tool
--   4. Copiar y pegar este script
--   5. Ejecutar (F5)
-- 
-- Opción 3: Desde línea de comandos
--   psql -h localhost -U postgres -d sanmarino -f crear_tabla_reporte_tecnico_guia.sql
-- 
-- =====================================================

