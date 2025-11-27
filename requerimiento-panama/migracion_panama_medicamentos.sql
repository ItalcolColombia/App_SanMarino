-- ============================================================
-- MIGRACIÓN PARA MÓDULO DE MEDICAMENTOS POR GALPÓN - PANAMÁ
-- ============================================================
-- Este script crea la tabla y funcionalidades necesarias
-- para el registro de medicamentos por galpón en Panamá
-- ============================================================

BEGIN;

-- ============================================================
-- 1. TABLA PRINCIPAL: MEDICAMENTOS_GALPON
-- ============================================================

CREATE TABLE IF NOT EXISTS medicamentos_galpon (
    id SERIAL PRIMARY KEY,
    granja_id INTEGER NOT NULL REFERENCES farms(farm_id) ON DELETE CASCADE,
    galpon_id VARCHAR(100) NOT NULL,
    lote_id INTEGER REFERENCES lotes(lote_id) ON DELETE SET NULL,
    fecha_medicacion DATE NOT NULL,
    edad_medicacion INTEGER, -- Calculado automáticamente
    tipo_medicacion VARCHAR(100) NOT NULL,
    via_medicacion VARCHAR(50) NOT NULL,
    medicamento_suministrado VARCHAR(255) NOT NULL,
    dosis VARCHAR(50) NOT NULL,
    descripcion_dosis DECIMAL(10,2),
    tiempo_medicacion_dias INTEGER NOT NULL,
    respuesta_medicacion VARCHAR(50),
    observaciones TEXT,
    company_id INTEGER NOT NULL REFERENCES companies(company_id),
    created_by_user_id INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_by_user_id INTEGER,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMP
);

-- Índices para performance
CREATE INDEX IF NOT EXISTS idx_medicamentos_galpon_granja ON medicamentos_galpon(granja_id);
CREATE INDEX IF NOT EXISTS idx_medicamentos_galpon_galpon ON medicamentos_galpon(galpon_id);
CREATE INDEX IF NOT EXISTS idx_medicamentos_galpon_lote ON medicamentos_galpon(lote_id);
CREATE INDEX IF NOT EXISTS idx_medicamentos_galpon_fecha ON medicamentos_galpon(fecha_medicacion);
CREATE INDEX IF NOT EXISTS idx_medicamentos_galpon_company ON medicamentos_galpon(company_id);
CREATE INDEX IF NOT EXISTS idx_medicamentos_galpon_deleted ON medicamentos_galpon(deleted_at) WHERE deleted_at IS NULL;

-- Comentarios
COMMENT ON TABLE medicamentos_galpon IS 'Registro de medicamentos administrados por galpón (Panamá)';
COMMENT ON COLUMN medicamentos_galpon.edad_medicacion IS 'Edad calculada automáticamente: fecha_medicacion - fecha_encaset del lote';
COMMENT ON COLUMN medicamentos_galpon.tipo_medicacion IS 'Tipo: Tratamiento Antibiótico, Suplemento vitamínico, Anticoccidial, etc.';
COMMENT ON COLUMN medicamentos_galpon.via_medicacion IS 'Vía: Oral, Aspersión, Otro';
COMMENT ON COLUMN medicamentos_galpon.dosis IS 'Unidad de dosis: mg/kg, ml/L, ml/ave, g/ave';
COMMENT ON COLUMN medicamentos_galpon.descripcion_dosis IS 'Valor numérico de la dosis. Ej: 20 (si dosis es mg/kg)';
COMMENT ON COLUMN medicamentos_galpon.respuesta_medicacion IS 'Respuesta: Efectiva, Poco efectiva, No fue efectiva';

-- ============================================================
-- 2. FUNCIÓN PARA CALCULAR EDAD DE MEDICACIÓN
-- ============================================================

CREATE OR REPLACE FUNCTION calcular_edad_medicacion(
    p_fecha_medicacion DATE,
    p_lote_id INTEGER
)
RETURNS INTEGER AS $$
DECLARE
    v_fecha_encaset DATE;
BEGIN
    -- Obtener fecha de encasetamiento del lote
    SELECT fecha_encaset INTO v_fecha_encaset
    FROM lotes
    WHERE lote_id = p_lote_id
      AND deleted_at IS NULL;
    
    -- Si no hay fecha de encasetamiento, retornar NULL
    IF v_fecha_encaset IS NULL THEN
        RETURN NULL;
    END IF;
    
    -- Calcular diferencia en días
    RETURN p_fecha_medicacion - v_fecha_encaset;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION calcular_edad_medicacion IS 'Calcula la edad de medicación: fecha_medicacion - fecha_encaset del lote';

-- ============================================================
-- 3. TRIGGER PARA CALCULAR EDAD AUTOMÁTICAMENTE
-- ============================================================

CREATE OR REPLACE FUNCTION trigger_calcular_edad_medicacion()
RETURNS TRIGGER AS $$
BEGIN
    -- Calcular edad automáticamente si hay lote_id
    IF NEW.lote_id IS NOT NULL THEN
        NEW.edad_medicacion := calcular_edad_medicacion(NEW.fecha_medicacion, NEW.lote_id);
    ELSE
        NEW.edad_medicacion := NULL;
    END IF;
    
    -- Actualizar updated_at
    NEW.updated_at := CURRENT_TIMESTAMP;
    
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Crear trigger
DROP TRIGGER IF EXISTS calcular_edad_medicacion_trigger ON medicamentos_galpon;
CREATE TRIGGER calcular_edad_medicacion_trigger
    BEFORE INSERT OR UPDATE ON medicamentos_galpon
    FOR EACH ROW
    EXECUTE FUNCTION trigger_calcular_edad_medicacion();

-- ============================================================
-- 4. FUNCIÓN PARA OBTENER RESUMEN POR GRANJA Y LOTE
-- ============================================================

CREATE OR REPLACE FUNCTION obtener_resumen_medicamentos_granja_lote(
    p_granja_id INTEGER,
    p_lote_id INTEGER
)
RETURNS TABLE (
    galpon_id VARCHAR,
    galpon_nombre VARCHAR,
    tiene_medicaciones BOOLEAN,
    total_medicaciones INTEGER,
    ultima_medicacion DATE,
    tipos_medicacion TEXT
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        g.galpon_id::VARCHAR,
        COALESCE(g.galpon_nombre, g.galpon_id)::VARCHAR as galpon_nombre,
        CASE WHEN COUNT(mg.id) > 0 THEN true ELSE false END as tiene_medicaciones,
        COUNT(mg.id)::INTEGER as total_medicaciones,
        MAX(mg.fecha_medicacion) as ultima_medicacion,
        STRING_AGG(DISTINCT mg.tipo_medicacion, ', ' ORDER BY mg.tipo_medicacion) as tipos_medicacion
    FROM galpones g
    LEFT JOIN medicamentos_galpon mg ON 
        mg.galpon_id = g.galpon_id 
        AND mg.granja_id = p_granja_id
        AND mg.lote_id = p_lote_id
        AND mg.deleted_at IS NULL
    WHERE g.granja_id = p_granja_id
      AND g.deleted_at IS NULL
    GROUP BY g.galpon_id, g.galpon_nombre
    ORDER BY g.galpon_id;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION obtener_resumen_medicamentos_granja_lote IS 'Obtiene resumen de medicamentos por granja y lote, incluyendo galpones sin medicaciones';

-- ============================================================
-- 5. CONFIGURACIÓN PARA PANAMÁ EN SISTEMA PARAMETRIZABLE
-- ============================================================
-- Asumiendo que Panamá tiene PaisId = 3
-- Ajustar según la estructura real de la base de datos

INSERT INTO pais_modulo_funcionalidad (pais_id, modulo, funcionalidad, activo, requerido, orden, etiqueta, descripcion, configuracion)
VALUES
  (3, 'medicamentos', 'registro_medicamentos_galpon', true, true, 1, 'Registro de Medicamentos por Galpón', 
   'Módulo completo de registro de medicamentos administrados por galpón con trazabilidad al lote', 
   '{"tipo_input": "module", "ruta": "/medicamentos-galpon", "icono": "pills"}'),
  
  (3, 'medicamentos', 'consulta_individual_galpon', true, false, 2, 'Consulta Individual por Galpón', 
   'Permite consultar todos los medicamentos de un galpón específico', 
   '{"tipo_input": "feature", "ruta": "/medicamentos-galpon/consulta"}'),
  
  (3, 'medicamentos', 'resumen_granja_lote', true, false, 3, 'Resumen por Granja y Lote', 
   'Muestra resumen de medicamentos por granja y lote, incluyendo galpones sin medicaciones', 
   '{"tipo_input": "feature", "ruta": "/medicamentos-galpon/resumen"}')
ON CONFLICT (pais_id, modulo, funcionalidad) DO NOTHING;

-- ============================================================
-- 6. VALIDACIONES Y CONSTRAINTS
-- ============================================================

-- Validar que fecha_medicacion no sea futura
ALTER TABLE medicamentos_galpon
ADD CONSTRAINT check_fecha_medicacion_no_futura 
CHECK (fecha_medicacion <= CURRENT_DATE);

-- Validar que tiempo_medicacion_dias sea positivo
ALTER TABLE medicamentos_galpon
ADD CONSTRAINT check_tiempo_medicacion_positivo 
CHECK (tiempo_medicacion_dias > 0);

-- Validar que descripcion_dosis sea positiva si está presente
ALTER TABLE medicamentos_galpon
ADD CONSTRAINT check_descripcion_dosis_positiva 
CHECK (descripcion_dosis IS NULL OR descripcion_dosis > 0);

-- Validar valores de tipo_medicacion
ALTER TABLE medicamentos_galpon
ADD CONSTRAINT check_tipo_medicacion_valido 
CHECK (tipo_medicacion IN (
    'Tratamiento Antibiótico',
    'Suplemento vitamínico',
    'Tratamiento Anticoccidial',
    'Tratamiento vías respiratorios',
    'Tratamiento gastrointestinal',
    'Otro'
));

-- Validar valores de via_medicacion
ALTER TABLE medicamentos_galpon
ADD CONSTRAINT check_via_medicacion_valida 
CHECK (via_medicacion IN ('Oral', 'Aspersión', 'Otro'));

-- Validar valores de dosis
ALTER TABLE medicamentos_galpon
ADD CONSTRAINT check_dosis_valida 
CHECK (dosis IN ('mg/kg', 'ml/L', 'ml/ave', 'g/ave'));

-- Validar valores de respuesta_medicacion
ALTER TABLE medicamentos_galpon
ADD CONSTRAINT check_respuesta_medicacion_valida 
CHECK (respuesta_medicacion IS NULL OR respuesta_medicacion IN (
    'Efectiva',
    'Poco efectiva',
    'No fue efectiva'
));

-- ============================================================
-- 7. VERIFICACIONES
-- ============================================================

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'medicamentos_galpon') THEN
        RAISE EXCEPTION 'Error: La tabla medicamentos_galpon no se creó correctamente';
    END IF;
    
    IF NOT EXISTS (SELECT 1 FROM information_schema.routines WHERE routine_name = 'calcular_edad_medicacion') THEN
        RAISE EXCEPTION 'Error: La función calcular_edad_medicacion no se creó correctamente';
    END IF;
    
    RAISE NOTICE 'Migración de medicamentos Panamá completada exitosamente';
END $$;

COMMIT;

-- ============================================================
-- ROLLBACK (en caso de necesitar revertir)
-- ============================================================
/*
BEGIN;

-- Eliminar configuración
DELETE FROM pais_modulo_funcionalidad WHERE pais_id = 3 AND modulo = 'medicamentos';

-- Eliminar triggers
DROP TRIGGER IF EXISTS calcular_edad_medicacion_trigger ON medicamentos_galpon;

-- Eliminar funciones
DROP FUNCTION IF EXISTS trigger_calcular_edad_medicacion();
DROP FUNCTION IF EXISTS calcular_edad_medicacion(DATE, INTEGER);
DROP FUNCTION IF EXISTS obtener_resumen_medicamentos_granja_lote(INTEGER, INTEGER);

-- Eliminar tabla
DROP TABLE IF EXISTS medicamentos_galpon CASCADE;

COMMIT;
*/

