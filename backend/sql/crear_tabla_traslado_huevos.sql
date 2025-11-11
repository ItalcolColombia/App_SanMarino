-- Script para crear la tabla de traslados de huevos
-- Esta tabla registra todos los traslados y ventas de huevos desde lotes de producción

CREATE TABLE IF NOT EXISTS traslado_huevos (
    id SERIAL PRIMARY KEY,
    numero_traslado VARCHAR(50) NOT NULL UNIQUE,
    fecha_traslado TIMESTAMP NOT NULL,
    tipo_operacion VARCHAR(20) NOT NULL, -- 'Venta' o 'Traslado'
    
    -- Lote origen
    lote_id VARCHAR(50) NOT NULL,
    granja_origen_id INTEGER NOT NULL,
    
    -- Destino (si es traslado)
    granja_destino_id INTEGER,
    lote_destino_id VARCHAR(50),
    tipo_destino VARCHAR(20), -- 'Granja' o 'Planta'
    
    -- Motivo y descripción
    motivo VARCHAR(200),
    descripcion TEXT,
    
    -- Cantidades por tipo de huevo
    cantidad_limpio INTEGER NOT NULL DEFAULT 0,
    cantidad_tratado INTEGER NOT NULL DEFAULT 0,
    cantidad_sucio INTEGER NOT NULL DEFAULT 0,
    cantidad_deforme INTEGER NOT NULL DEFAULT 0,
    cantidad_blanco INTEGER NOT NULL DEFAULT 0,
    cantidad_doble_yema INTEGER NOT NULL DEFAULT 0,
    cantidad_piso INTEGER NOT NULL DEFAULT 0,
    cantidad_pequeno INTEGER NOT NULL DEFAULT 0,
    cantidad_roto INTEGER NOT NULL DEFAULT 0,
    cantidad_desecho INTEGER NOT NULL DEFAULT 0,
    cantidad_otro INTEGER NOT NULL DEFAULT 0,
    
    -- Estado
    estado VARCHAR(20) NOT NULL DEFAULT 'Pendiente', -- 'Pendiente', 'Completado', 'Cancelado'
    
    -- Usuario
    usuario_traslado_id INTEGER NOT NULL,
    usuario_nombre VARCHAR(200),
    
    -- Fechas de procesamiento
    fecha_procesamiento TIMESTAMP,
    fecha_cancelacion TIMESTAMP,
    
    -- Observaciones
    observaciones TEXT,
    
    -- Auditoría (compatible con AuditableEntity)
    company_id INTEGER NOT NULL,
    created_by_user_id INTEGER NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by_user_id INTEGER,
    updated_at TIMESTAMP WITH TIME ZONE,
    deleted_at TIMESTAMP WITH TIME ZONE,
    
    CONSTRAINT chk_tipo_operacion CHECK (tipo_operacion IN ('Venta', 'Traslado')),
    CONSTRAINT chk_estado CHECK (estado IN ('Pendiente', 'Completado', 'Cancelado')),
    CONSTRAINT chk_tipo_destino CHECK (tipo_destino IS NULL OR tipo_destino IN ('Granja', 'Planta'))
);

-- Índices para mejorar el rendimiento
CREATE INDEX IF NOT EXISTS idx_traslado_huevos_lote_id ON traslado_huevos(lote_id);
CREATE INDEX IF NOT EXISTS idx_traslado_huevos_fecha_traslado ON traslado_huevos(fecha_traslado);
CREATE INDEX IF NOT EXISTS idx_traslado_huevos_estado ON traslado_huevos(estado);
CREATE INDEX IF NOT EXISTS idx_traslado_huevos_granja_origen ON traslado_huevos(granja_origen_id);
CREATE INDEX IF NOT EXISTS idx_traslado_huevos_granja_destino ON traslado_huevos(granja_destino_id);
CREATE INDEX IF NOT EXISTS idx_traslado_huevos_company_id ON traslado_huevos(company_id);

-- Comentarios
COMMENT ON TABLE traslado_huevos IS 'Registro de traslados y ventas de huevos desde lotes de producción';
COMMENT ON COLUMN traslado_huevos.tipo_operacion IS 'Tipo de operación: Venta o Traslado';
COMMENT ON COLUMN traslado_huevos.estado IS 'Estado del traslado: Pendiente, Completado, Cancelado';
COMMENT ON COLUMN traslado_huevos.tipo_destino IS 'Tipo de destino: Granja o Planta (solo para traslados)';

