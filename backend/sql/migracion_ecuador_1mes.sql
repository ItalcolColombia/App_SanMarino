-- ============================================================
-- MIGRACIÓN COMPLETA PARA PROYECTO ECUADOR
-- Duración: 1 mes | Análisis: 3 días
-- ============================================================
-- Este script contiene todas las migraciones necesarias para
-- implementar los campos específicos de Ecuador y el módulo
-- de configuración parametrizable
-- ============================================================

BEGIN;

-- ============================================================
-- 1. MÓDULO DE CONFIGURACIÓN PARAMETRIZABLE
-- ============================================================

CREATE TABLE IF NOT EXISTS pais_modulo_funcionalidad (
    id SERIAL PRIMARY KEY,
    pais_id INTEGER NOT NULL REFERENCES paises(pais_id) ON DELETE CASCADE,
    modulo VARCHAR(50) NOT NULL,
    funcionalidad VARCHAR(100) NOT NULL,
    activo BOOLEAN DEFAULT true,
    requerido BOOLEAN DEFAULT false,
    orden INTEGER DEFAULT 0,
    etiqueta VARCHAR(255),
    descripcion TEXT,
    configuracion JSONB,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by_user_id INTEGER,
    updated_by_user_id INTEGER,
    CONSTRAINT unique_pais_modulo_funcionalidad UNIQUE (pais_id, modulo, funcionalidad)
);

CREATE INDEX IF NOT EXISTS idx_pais_modulo_funcionalidad_pais ON pais_modulo_funcionalidad(pais_id);
CREATE INDEX IF NOT EXISTS idx_pais_modulo_funcionalidad_modulo ON pais_modulo_funcionalidad(modulo);
CREATE INDEX IF NOT EXISTS idx_pais_modulo_funcionalidad_activo ON pais_modulo_funcionalidad(activo) WHERE activo = true;
CREATE INDEX IF NOT EXISTS idx_pais_modulo_funcionalidad_pais_modulo ON pais_modulo_funcionalidad(pais_id, modulo);

COMMENT ON TABLE pais_modulo_funcionalidad IS 'Configuración parametrizable de funcionalidades por país y módulo';
COMMENT ON COLUMN pais_modulo_funcionalidad.modulo IS 'Módulo del sistema: lote, seguimiento, despacho, inventario, etc.';
COMMENT ON COLUMN pais_modulo_funcionalidad.funcionalidad IS 'Nombre de la funcionalidad: fecha_recepcion, consumo_agua, gavetas, etc.';
COMMENT ON COLUMN pais_modulo_funcionalidad.configuracion IS 'Configuración adicional en formato JSON';

-- ============================================================
-- 2. CAMPOS MÓDULO LOTE
-- ============================================================

ALTER TABLE lotes 
ADD COLUMN IF NOT EXISTS fecha_recepcion TIMESTAMP,
ADD COLUMN IF NOT EXISTS incubadora_origen TEXT;

COMMENT ON COLUMN lotes.fecha_recepcion IS 'Fecha en que se recibieron los pollitos de 1 día (Ecuador)';
COMMENT ON COLUMN lotes.incubadora_origen IS 'Incubadora(s) de origen, puede ser múltiple separado por coma (Ecuador)';

-- ============================================================
-- 3. CAMPOS MÓDULO SEGUIMIENTO LEVANTE
-- ============================================================

ALTER TABLE seguimiento_lote_levante
ADD COLUMN IF NOT EXISTS consumo_agua_ph DOUBLE PRECISION,
ADD COLUMN IF NOT EXISTS consumo_agua_orp DOUBLE PRECISION,
ADD COLUMN IF NOT EXISTS consumo_agua_temperatura DOUBLE PRECISION,
ADD COLUMN IF NOT EXISTS medicamento_nombre VARCHAR(255),
ADD COLUMN IF NOT EXISTS medicamento_dosis VARCHAR(255),
ADD COLUMN IF NOT EXISTS medicamento_fecha TIMESTAMP;

COMMENT ON COLUMN seguimiento_lote_levante.consumo_agua_ph IS 'Nivel de PH del agua (Ecuador)';
COMMENT ON COLUMN seguimiento_lote_levante.consumo_agua_orp IS 'Nivel de ORP del agua (Ecuador)';
COMMENT ON COLUMN seguimiento_lote_levante.consumo_agua_temperatura IS 'Temperatura del agua en °C (Ecuador)';
COMMENT ON COLUMN seguimiento_lote_levante.medicamento_nombre IS 'Nombre del medicamento aplicado (Ecuador)';
COMMENT ON COLUMN seguimiento_lote_levante.medicamento_dosis IS 'Dosis del medicamento aplicado (Ecuador)';
COMMENT ON COLUMN seguimiento_lote_levante.medicamento_fecha IS 'Fecha de aplicación del medicamento (Ecuador)';

-- ============================================================
-- 4. CAMPOS MÓDULO DESPACHO (MOVIMIENTO_AVES)
-- ============================================================

ALTER TABLE movimiento_aves
ADD COLUMN IF NOT EXISTS numero_despacho VARCHAR(50),
ADD COLUMN IF NOT EXISTS cliente_id INTEGER,
ADD COLUMN IF NOT EXISTS cliente_nombre VARCHAR(255),
ADD COLUMN IF NOT EXISTS numero_gavetas INTEGER,
ADD COLUMN IF NOT EXISTS pollos_por_gaveta INTEGER,
ADD COLUMN IF NOT EXISTS rango_peso VARCHAR(50),
ADD COLUMN IF NOT EXISTS hora_inicio TIME,
ADD COLUMN IF NOT EXISTS hora_salida TIME,
ADD COLUMN IF NOT EXISTS guia_remision VARCHAR(100),
ADD COLUMN IF NOT EXISTS guia_agrocalidad VARCHAR(100),
ADD COLUMN IF NOT EXISTS placa_vehiculo VARCHAR(20),
ADD COLUMN IF NOT EXISTS sellos VARCHAR(255),
ADD COLUMN IF NOT EXISTS ayuno TIME,
ADD COLUMN IF NOT EXISTS conductor VARCHAR(255),
ADD COLUMN IF NOT EXISTS peso_bruto_total DECIMAL(10,2),
ADD COLUMN IF NOT EXISTS peso_tara_total DECIMAL(10,2),
ADD COLUMN IF NOT EXISTS peso_neto_total DECIMAL(10,2),
ADD COLUMN IF NOT EXISTS promedio_peso_ave DECIMAL(10,3);

COMMENT ON COLUMN movimiento_aves.numero_despacho IS 'Número único del despacho (Ecuador)';
COMMENT ON COLUMN movimiento_aves.cliente_id IS 'ID del cliente (FK a tabla clientes)';
COMMENT ON COLUMN movimiento_aves.cliente_nombre IS 'Nombre del cliente (Ecuador)';
COMMENT ON COLUMN movimiento_aves.numero_gavetas IS 'Número de gavetas en el despacho (Ecuador)';
COMMENT ON COLUMN movimiento_aves.pollos_por_gaveta IS 'Cantidad de pollos por gaveta (Ecuador)';
COMMENT ON COLUMN movimiento_aves.guia_agrocalidad IS 'Número de guía de Agrocalidad (Ecuador)';
COMMENT ON COLUMN movimiento_aves.placa_vehiculo IS 'Placa del vehículo de transporte (Ecuador)';
COMMENT ON COLUMN movimiento_aves.conductor IS 'Nombre del conductor (Ecuador)';
COMMENT ON COLUMN movimiento_aves.peso_bruto_total IS 'Peso bruto total calculado (Ecuador)';
COMMENT ON COLUMN movimiento_aves.peso_neto_total IS 'Peso neto total calculado (Ecuador)';
COMMENT ON COLUMN movimiento_aves.promedio_peso_ave IS 'Promedio de peso por ave calculado (Ecuador)';

-- ============================================================
-- 5. TABLA DESPACHO_GAVETAS
-- ============================================================

CREATE TABLE IF NOT EXISTS despacho_gavetas (
    id SERIAL PRIMARY KEY,
    movimiento_aves_id INTEGER NOT NULL REFERENCES movimiento_aves(id) ON DELETE CASCADE,
    numero_gaveta INTEGER NOT NULL,
    peso_bruto DECIMAL(10,2) NOT NULL,
    peso_tara DECIMAL(10,2) NOT NULL,
    peso_neto DECIMAL(10,2) GENERATED ALWAYS AS (peso_bruto - peso_tara) STORED,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_gaveta_movimiento UNIQUE (movimiento_aves_id, numero_gaveta)
);

CREATE INDEX IF NOT EXISTS idx_despacho_gavetas_movimiento ON despacho_gavetas(movimiento_aves_id);

COMMENT ON TABLE despacho_gavetas IS 'Detalle de pesos por gaveta en despachos (Ecuador)';
COMMENT ON COLUMN despacho_gavetas.peso_neto IS 'Calculado automáticamente: peso_bruto - peso_tara';

-- ============================================================
-- 6. TABLA CLIENTES (si no existe)
-- ============================================================

CREATE TABLE IF NOT EXISTS clientes (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(255) NOT NULL,
    codigo VARCHAR(50) UNIQUE,
    nit_cedula VARCHAR(50),
    direccion TEXT,
    telefono VARCHAR(50),
    email VARCHAR(255),
    activo BOOLEAN DEFAULT true,
    company_id INTEGER NOT NULL REFERENCES companies(company_id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_clientes_company ON clientes(company_id);
CREATE INDEX IF NOT EXISTS idx_clientes_activo ON clientes(activo) WHERE activo = true;
CREATE INDEX IF NOT EXISTS idx_clientes_codigo ON clientes(codigo) WHERE codigo IS NOT NULL;

COMMENT ON TABLE clientes IS 'Catálogo de clientes para despachos (Ecuador)';

-- Agregar FK de cliente_id en movimiento_aves si la tabla clientes existe
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'clientes') THEN
        IF NOT EXISTS (
            SELECT 1 FROM information_schema.table_constraints 
            WHERE constraint_name = 'fk_movimiento_aves_cliente'
        ) THEN
            ALTER TABLE movimiento_aves
            ADD CONSTRAINT fk_movimiento_aves_cliente 
            FOREIGN KEY (cliente_id) REFERENCES clientes(id);
        END IF;
    END IF;
END $$;

-- ============================================================
-- 7. DATOS INICIALES - CONFIGURACIÓN PARA ECUADOR
-- ============================================================
-- Asumiendo que Ecuador tiene PaisId = 2
-- Ajustar según la estructura real de la base de datos

INSERT INTO pais_modulo_funcionalidad (pais_id, modulo, funcionalidad, activo, requerido, orden, etiqueta, descripcion, configuracion)
VALUES
  -- Módulo Lote
  (2, 'lote', 'fecha_recepcion', true, false, 1, 'Fecha de Recepción', 'Fecha en que llegaron los pollitos de 1 día', 
   '{"tipo_input": "date", "validacion_max": "fecha_actual", "placeholder": "Seleccione la fecha"}'),
  
  (2, 'lote', 'incubadora_origen', true, false, 2, 'Incubadora(s) de Origen', 'Incubadora(s) de donde provienen los pollitos', 
   '{"tipo_input": "select_multiple", "opciones": ["IBARRA", "CHONGON", "OTRA"], "placeholder": "Seleccione incubadora(s)"}'),
  
  -- Módulo Seguimiento
  (2, 'seguimiento', 'consumo_agua_ph', true, false, 1, 'PH del Agua', 'Nivel de PH del agua', 
   '{"tipo_input": "number", "validacion_min": 6.0, "validacion_max": 8.5, "decimales": 2, "unidad": "pH"}'),
  
  (2, 'seguimiento', 'consumo_agua_orp', true, false, 2, 'ORP del Agua', 'Nivel de ORP del agua', 
   '{"tipo_input": "number", "validacion_min": 0, "validacion_max": 1000, "decimales": 0}'),
  
  (2, 'seguimiento', 'consumo_agua_temperatura', true, false, 3, 'Temperatura del Agua', 'Temperatura del agua en °C', 
   '{"tipo_input": "number", "validacion_min": 0, "validacion_max": 50, "decimales": 1, "unidad": "°C"}'),
  
  (2, 'seguimiento', 'medicamento_nombre', true, false, 4, 'Nombre del Medicamento', 'Nombre del medicamento aplicado', 
   '{"tipo_input": "text", "placeholder": "Ingrese el nombre del medicamento"}'),
  
  (2, 'seguimiento', 'medicamento_dosis', true, false, 5, 'Dosis del Medicamento', 'Dosis del medicamento aplicado', 
   '{"tipo_input": "text", "placeholder": "Ingrese la dosis"}'),
  
  (2, 'seguimiento', 'medicamento_fecha', true, false, 6, 'Fecha de Aplicación', 'Fecha en que se aplicó el medicamento', 
   '{"tipo_input": "date", "validacion_max": "fecha_actual"}'),
  
  -- Módulo Despacho
  (2, 'despacho', 'numero_despacho', true, true, 1, 'Número de Despacho', 'Número único del despacho', 
   '{"tipo_input": "text", "placeholder": "Ej: DESP-2025-001"}'),
  
  (2, 'despacho', 'cliente_nombre', true, true, 2, 'Cliente', 'Nombre del cliente', 
   '{"tipo_input": "select", "placeholder": "Seleccione el cliente"}'),
  
  (2, 'despacho', 'numero_gavetas', true, true, 3, 'Número de Gavetas', 'Cantidad de gavetas', 
   '{"tipo_input": "number", "validacion_min": 1, "placeholder": "Ingrese el número de gavetas"}'),
  
  (2, 'despacho', 'pollos_por_gaveta', true, false, 4, 'Pollos por Gaveta', 'Cantidad de pollos por gaveta', 
   '{"tipo_input": "number", "validacion_min": 1}'),
  
  (2, 'despacho', 'rango_peso', true, false, 5, 'Rango de Peso', 'Rango de peso de los pollos', 
   '{"tipo_input": "text", "placeholder": "Ej: 2-3.2kg"}'),
  
  (2, 'despacho', 'hora_inicio', true, false, 6, 'Hora de Inicio', 'Hora de inicio del despacho', 
   '{"tipo_input": "time"}'),
  
  (2, 'despacho', 'hora_salida', true, false, 7, 'Hora de Salida', 'Hora de salida del despacho', 
   '{"tipo_input": "time"}'),
  
  (2, 'despacho', 'guia_remision', true, false, 8, 'Guía de Remisión', 'Número de guía de remisión', 
   '{"tipo_input": "text", "placeholder": "Ingrese el número de guía"}'),
  
  (2, 'despacho', 'guia_agrocalidad', true, false, 9, 'Guía Agrocalidad', 'Número de guía de Agrocalidad', 
   '{"tipo_input": "text", "placeholder": "Ingrese el número de guía"}'),
  
  (2, 'despacho', 'placa_vehiculo', true, false, 10, 'Placa del Vehículo', 'Placa del vehículo de transporte', 
   '{"tipo_input": "text", "placeholder": "Ej: ABC-1234"}'),
  
  (2, 'despacho', 'sellos', true, false, 11, 'Sellos', 'Números de sellos', 
   '{"tipo_input": "text", "placeholder": "Ingrese los números de sellos"}'),
  
  (2, 'despacho', 'ayuno', true, false, 12, 'Tiempo de Ayuno', 'Tiempo de ayuno antes del despacho', 
   '{"tipo_input": "time"}'),
  
  (2, 'despacho', 'conductor', true, false, 13, 'Conductor', 'Nombre del conductor', 
   '{"tipo_input": "text", "placeholder": "Ingrese el nombre del conductor"}'),
  
  (2, 'despacho', 'peso_bruto_total', true, false, 14, 'Peso Bruto Total', 'Peso bruto total calculado', 
   '{"tipo_input": "number", "decimales": 2, "unidad": "kg", "readonly": true}'),
  
  (2, 'despacho', 'peso_tara_total', true, false, 15, 'Peso Tara Total', 'Peso tara total calculado', 
   '{"tipo_input": "number", "decimales": 2, "unidad": "kg", "readonly": true}'),
  
  (2, 'despacho', 'peso_neto_total', true, false, 16, 'Peso Neto Total', 'Peso neto total calculado', 
   '{"tipo_input": "number", "decimales": 2, "unidad": "kg", "readonly": true}'),
  
  (2, 'despacho', 'promedio_peso_ave', true, false, 17, 'Promedio Peso Ave', 'Promedio de peso por ave calculado', 
   '{"tipo_input": "number", "decimales": 3, "unidad": "kg", "readonly": true}')
ON CONFLICT (pais_id, modulo, funcionalidad) DO NOTHING;

-- ============================================================
-- 8. FUNCIONES Y TRIGGERS
-- ============================================================

-- Función para actualizar updated_at automáticamente
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Triggers para updated_at
DROP TRIGGER IF EXISTS update_pais_modulo_funcionalidad_updated_at ON pais_modulo_funcionalidad;
CREATE TRIGGER update_pais_modulo_funcionalidad_updated_at
    BEFORE UPDATE ON pais_modulo_funcionalidad
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_despacho_gavetas_updated_at ON despacho_gavetas;
CREATE TRIGGER update_despacho_gavetas_updated_at
    BEFORE UPDATE ON despacho_gavetas
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- ============================================================
-- 9. VERIFICACIONES
-- ============================================================

-- Verificar que las tablas se crearon correctamente
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'pais_modulo_funcionalidad') THEN
        RAISE EXCEPTION 'Error: La tabla pais_modulo_funcionalidad no se creó correctamente';
    END IF;
    
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'despacho_gavetas') THEN
        RAISE EXCEPTION 'Error: La tabla despacho_gavetas no se creó correctamente';
    END IF;
    
    RAISE NOTICE 'Migración completada exitosamente';
END $$;

COMMIT;

-- ============================================================
-- ROLLBACK (en caso de necesitar revertir)
-- ============================================================
/*
BEGIN;

-- Eliminar datos de configuración
DELETE FROM pais_modulo_funcionalidad WHERE pais_id = 2;

-- Eliminar tablas
DROP TABLE IF EXISTS despacho_gavetas CASCADE;
DROP TABLE IF EXISTS pais_modulo_funcionalidad CASCADE;

-- Eliminar columnas de movimiento_aves
ALTER TABLE movimiento_aves 
DROP COLUMN IF EXISTS numero_despacho,
DROP COLUMN IF EXISTS cliente_id,
DROP COLUMN IF EXISTS cliente_nombre,
DROP COLUMN IF EXISTS numero_gavetas,
DROP COLUMN IF EXISTS pollos_por_gaveta,
DROP COLUMN IF EXISTS rango_peso,
DROP COLUMN IF EXISTS hora_inicio,
DROP COLUMN IF EXISTS hora_salida,
DROP COLUMN IF EXISTS guia_remision,
DROP COLUMN IF EXISTS guia_agrocalidad,
DROP COLUMN IF EXISTS placa_vehiculo,
DROP COLUMN IF EXISTS sellos,
DROP COLUMN IF EXISTS ayuno,
DROP COLUMN IF EXISTS conductor,
DROP COLUMN IF EXISTS peso_bruto_total,
DROP COLUMN IF EXISTS peso_tara_total,
DROP COLUMN IF EXISTS peso_neto_total,
DROP COLUMN IF EXISTS promedio_peso_ave;

-- Eliminar columnas de seguimiento_lote_levante
ALTER TABLE seguimiento_lote_levante
DROP COLUMN IF EXISTS consumo_agua_ph,
DROP COLUMN IF EXISTS consumo_agua_orp,
DROP COLUMN IF EXISTS consumo_agua_temperatura,
DROP COLUMN IF EXISTS medicamento_nombre,
DROP COLUMN IF EXISTS medicamento_dosis,
DROP COLUMN IF EXISTS medicamento_fecha;

-- Eliminar columnas de lotes
ALTER TABLE lotes
DROP COLUMN IF EXISTS fecha_recepcion,
DROP COLUMN IF EXISTS incubadora_origen;

COMMIT;
*/

