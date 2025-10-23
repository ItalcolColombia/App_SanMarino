-- Script para crear la tabla produccion_avicola_raw manualmente
-- Ejecutar este script directamente en la base de datos PostgreSQL

CREATE TABLE IF NOT EXISTS produccion_avicola_raw (
    id SERIAL PRIMARY KEY,
    company_id INTEGER NOT NULL,
    created_by_user_id INTEGER NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_by_user_id INTEGER,
    updated_at TIMESTAMP WITH TIME ZONE,
    deleted_at TIMESTAMP WITH TIME ZONE,
    
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

-- Crear índices para mejorar el rendimiento
CREATE INDEX IF NOT EXISTS ix_produccion_avicola_raw_anio_guia ON produccion_avicola_raw(anio_guia);
CREATE INDEX IF NOT EXISTS ix_produccion_avicola_raw_raza ON produccion_avicola_raw(raza);
CREATE INDEX IF NOT EXISTS ix_produccion_avicola_raw_company_id ON produccion_avicola_raw(company_id);

-- Comentarios en la tabla
COMMENT ON TABLE produccion_avicola_raw IS 'Tabla para almacenar datos de producción avícola en formato raw';
COMMENT ON COLUMN produccion_avicola_raw.anio_guia IS 'Año de la guía de producción';
COMMENT ON COLUMN produccion_avicola_raw.raza IS 'Raza de las aves';
COMMENT ON COLUMN produccion_avicola_raw.edad IS 'Edad de las aves';
