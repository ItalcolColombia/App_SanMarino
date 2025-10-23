-- =====================================================
-- Script SIMPLE para crear las tablas del Sistema de Inventario de Aves
-- ZooSanMarino - PostgreSQL (Sin claves foráneas inicialmente)
-- =====================================================

-- Tabla: inventario_aves
CREATE TABLE IF NOT EXISTS inventario_aves (
    id SERIAL PRIMARY KEY,
    lote_id VARCHAR(50) NOT NULL,
    granja_id INTEGER NOT NULL,
    nucleo_id VARCHAR(50),
    galpon_id VARCHAR(50),
    
    -- Cantidades de aves
    cantidad_hembras INTEGER NOT NULL DEFAULT 0,
    cantidad_machos INTEGER NOT NULL DEFAULT 0,
    cantidad_mixtas INTEGER NOT NULL DEFAULT 0,
    
    -- Información del inventario
    fecha_actualizacion TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    estado VARCHAR(20) NOT NULL DEFAULT 'Activo',
    observaciones VARCHAR(1000),
    
    -- Campos de auditoría
    company_id INTEGER NOT NULL,
    created_by_user_id INTEGER NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_by_user_id INTEGER,
    updated_at TIMESTAMP WITH TIME ZONE,
    deleted_at TIMESTAMP WITH TIME ZONE
);

-- Tabla: movimiento_aves
CREATE TABLE IF NOT EXISTS movimiento_aves (
    id SERIAL PRIMARY KEY,
    numero_movimiento VARCHAR(50) NOT NULL,
    fecha_movimiento TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    tipo_movimiento VARCHAR(50) NOT NULL DEFAULT 'Traslado',
    
    -- Origen del movimiento
    inventario_origen_id INTEGER,
    lote_origen_id VARCHAR(50),
    granja_origen_id INTEGER,
    nucleo_origen_id VARCHAR(50),
    galpon_origen_id VARCHAR(50),
    
    -- Destino del movimiento
    inventario_destino_id INTEGER,
    lote_destino_id VARCHAR(50),
    granja_destino_id INTEGER,
    nucleo_destino_id VARCHAR(50),
    galpon_destino_id VARCHAR(50),
    
    -- Cantidades movidas
    cantidad_hembras INTEGER NOT NULL DEFAULT 0,
    cantidad_machos INTEGER NOT NULL DEFAULT 0,
    cantidad_mixtas INTEGER NOT NULL DEFAULT 0,
    
    -- Información del movimiento
    motivo_movimiento VARCHAR(500),
    observaciones VARCHAR(1000),
    estado VARCHAR(20) NOT NULL DEFAULT 'Pendiente',
    
    -- Usuario que realiza el movimiento
    usuario_movimiento_id INTEGER NOT NULL,
    usuario_nombre VARCHAR(200),
    
    -- Fechas de procesamiento
    fecha_procesamiento TIMESTAMP WITH TIME ZONE,
    fecha_cancelacion TIMESTAMP WITH TIME ZONE,
    
    -- Campos de auditoría
    company_id INTEGER NOT NULL,
    created_by_user_id INTEGER NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_by_user_id INTEGER,
    updated_at TIMESTAMP WITH TIME ZONE,
    deleted_at TIMESTAMP WITH TIME ZONE
);

-- Tabla: historial_inventario
CREATE TABLE IF NOT EXISTS historial_inventario (
    id SERIAL PRIMARY KEY,
    inventario_id INTEGER NOT NULL,
    lote_id VARCHAR(50) NOT NULL,
    
    -- Información del cambio
    fecha_cambio TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    tipo_cambio VARCHAR(50) NOT NULL,
    movimiento_id INTEGER,
    
    -- Cantidades antes del cambio
    cantidad_hembras_anterior INTEGER NOT NULL DEFAULT 0,
    cantidad_machos_anterior INTEGER NOT NULL DEFAULT 0,
    cantidad_mixtas_anterior INTEGER NOT NULL DEFAULT 0,
    
    -- Cantidades después del cambio
    cantidad_hembras_nueva INTEGER NOT NULL DEFAULT 0,
    cantidad_machos_nueva INTEGER NOT NULL DEFAULT 0,
    cantidad_mixtas_nueva INTEGER NOT NULL DEFAULT 0,
    
    -- Ubicación en el momento del cambio
    granja_id INTEGER NOT NULL,
    nucleo_id VARCHAR(50),
    galpon_id VARCHAR(50),
    
    -- Usuario que realizó el cambio
    usuario_cambio_id INTEGER NOT NULL,
    usuario_nombre VARCHAR(200),
    
    -- Información adicional
    motivo VARCHAR(500),
    observaciones VARCHAR(1000),
    
    -- Campos de auditoría
    company_id INTEGER NOT NULL,
    created_by_user_id INTEGER NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_by_user_id INTEGER,
    updated_at TIMESTAMP WITH TIME ZONE,
    deleted_at TIMESTAMP WITH TIME ZONE
);

-- Índices básicos
CREATE INDEX IF NOT EXISTS ix_inventario_aves_lote_id ON inventario_aves(lote_id);
CREATE INDEX IF NOT EXISTS ix_inventario_aves_company_id ON inventario_aves(company_id);
CREATE INDEX IF NOT EXISTS ix_inventario_aves_estado ON inventario_aves(estado);

CREATE INDEX IF NOT EXISTS ix_movimiento_aves_numero_movimiento ON movimiento_aves(numero_movimiento);
CREATE INDEX IF NOT EXISTS ix_movimiento_aves_company_id ON movimiento_aves(company_id);
CREATE INDEX IF NOT EXISTS ix_movimiento_aves_estado ON movimiento_aves(estado);

CREATE INDEX IF NOT EXISTS ix_historial_inventario_inventario_id ON historial_inventario(inventario_id);
CREATE INDEX IF NOT EXISTS ix_historial_inventario_lote_id ON historial_inventario(lote_id);
CREATE INDEX IF NOT EXISTS ix_historial_inventario_company_id ON historial_inventario(company_id);

-- Hacer único el número de movimiento
ALTER TABLE movimiento_aves ADD CONSTRAINT uq_numero_movimiento UNIQUE (numero_movimiento);

-- Insertar en historial de migraciones
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20241002_AddInventarioAvesTables', '9.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

-- Mensaje de confirmación
SELECT 'Tablas de Inventario de Aves creadas exitosamente!' as resultado;
