-- =====================================================
-- Script para crear las tablas del Sistema de Inventario de Aves
-- ZooSanMarino - PostgreSQL
-- =====================================================

-- Tabla: inventario_aves
-- Descripción: Almacena el inventario actual de aves por ubicación
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
    fecha_actualizacion TIMESTAMP WITH TIME ZONE NOT NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'Activo',
    observaciones VARCHAR(1000),
    
    -- Campos de auditoría
    company_id INTEGER NOT NULL,
    created_by_user_id INTEGER NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_by_user_id INTEGER,
    updated_at TIMESTAMP WITH TIME ZONE,
    deleted_at TIMESTAMP WITH TIME ZONE,
    
    -- Constraints
    CONSTRAINT ck_inventario_aves_cantidades_positivas 
        CHECK (cantidad_hembras >= 0 AND cantidad_machos >= 0 AND cantidad_mixtas >= 0),
    CONSTRAINT ck_inventario_aves_estado 
        CHECK (estado IN ('Activo', 'Trasladado', 'Liquidado', 'Eliminado'))
);

-- Tabla: movimiento_aves
-- Descripción: Registra todos los movimientos y traslados de aves
CREATE TABLE IF NOT EXISTS movimiento_aves (
    id SERIAL PRIMARY KEY,
    numero_movimiento VARCHAR(50) NOT NULL UNIQUE,
    fecha_movimiento TIMESTAMP WITH TIME ZONE NOT NULL,
    tipo_movimiento VARCHAR(50) NOT NULL,
    
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
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_by_user_id INTEGER,
    updated_at TIMESTAMP WITH TIME ZONE,
    deleted_at TIMESTAMP WITH TIME ZONE,
    
    -- Constraints
    CONSTRAINT ck_movimiento_aves_cantidades_positivas 
        CHECK (cantidad_hembras >= 0 AND cantidad_machos >= 0 AND cantidad_mixtas >= 0),
    CONSTRAINT ck_movimiento_aves_total_positivo 
        CHECK ((cantidad_hembras + cantidad_machos + cantidad_mixtas) > 0),
    CONSTRAINT ck_movimiento_aves_estado 
        CHECK (estado IN ('Pendiente', 'Completado', 'Cancelado')),
    CONSTRAINT ck_movimiento_aves_tipo 
        CHECK (tipo_movimiento IN ('Traslado', 'Ajuste', 'Liquidacion', 'Division', 'Unificacion')),
    CONSTRAINT ck_movimiento_aves_origen_destino 
        CHECK (
            (inventario_origen_id IS NOT NULL OR lote_origen_id IS NOT NULL) AND
            (inventario_destino_id IS NOT NULL OR lote_destino_id IS NOT NULL)
        )
);

-- Tabla: historial_inventario
-- Descripción: Mantiene el historial de todos los cambios en inventarios
CREATE TABLE IF NOT EXISTS historial_inventario (
    id SERIAL PRIMARY KEY,
    inventario_id INTEGER NOT NULL,
    lote_id VARCHAR(50) NOT NULL,
    
    -- Información del cambio
    fecha_cambio TIMESTAMP WITH TIME ZONE NOT NULL,
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
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_by_user_id INTEGER,
    updated_at TIMESTAMP WITH TIME ZONE,
    deleted_at TIMESTAMP WITH TIME ZONE,
    
    -- Constraints
    CONSTRAINT ck_historial_inventario_cantidades_positivas 
        CHECK (
            cantidad_hembras_anterior >= 0 AND cantidad_machos_anterior >= 0 AND cantidad_mixtas_anterior >= 0 AND
            cantidad_hembras_nueva >= 0 AND cantidad_machos_nueva >= 0 AND cantidad_mixtas_nueva >= 0
        ),
    CONSTRAINT ck_historial_inventario_tipo_cambio 
        CHECK (tipo_cambio IN ('Entrada', 'Salida', 'Ajuste', 'Traslado'))
);

-- =====================================================
-- ÍNDICES PARA OPTIMIZACIÓN
-- =====================================================

-- Índices para inventario_aves
CREATE INDEX IF NOT EXISTS ix_inventario_aves_lote_id ON inventario_aves(lote_id);
CREATE INDEX IF NOT EXISTS ix_inventario_aves_ubicacion ON inventario_aves(granja_id, nucleo_id, galpon_id);
CREATE INDEX IF NOT EXISTS ix_inventario_aves_company_id ON inventario_aves(company_id);
CREATE INDEX IF NOT EXISTS ix_inventario_aves_estado ON inventario_aves(estado);
CREATE INDEX IF NOT EXISTS ix_inventario_aves_fecha_actualizacion ON inventario_aves(fecha_actualizacion);

-- Índice único para evitar duplicados por ubicación (solo registros activos)
CREATE UNIQUE INDEX IF NOT EXISTS uq_inventario_aves_lote_ubicacion_company 
    ON inventario_aves(lote_id, granja_id, COALESCE(nucleo_id, ''), COALESCE(galpon_id, ''), company_id) 
    WHERE deleted_at IS NULL;

-- Índices para movimiento_aves
CREATE UNIQUE INDEX IF NOT EXISTS uq_movimiento_aves_numero_movimiento ON movimiento_aves(numero_movimiento);
CREATE INDEX IF NOT EXISTS ix_movimiento_aves_fecha_movimiento ON movimiento_aves(fecha_movimiento);
CREATE INDEX IF NOT EXISTS ix_movimiento_aves_tipo_movimiento ON movimiento_aves(tipo_movimiento);
CREATE INDEX IF NOT EXISTS ix_movimiento_aves_estado ON movimiento_aves(estado);
CREATE INDEX IF NOT EXISTS ix_movimiento_aves_lote_origen_id ON movimiento_aves(lote_origen_id);
CREATE INDEX IF NOT EXISTS ix_movimiento_aves_lote_destino_id ON movimiento_aves(lote_destino_id);
CREATE INDEX IF NOT EXISTS ix_movimiento_aves_usuario_movimiento_id ON movimiento_aves(usuario_movimiento_id);
CREATE INDEX IF NOT EXISTS ix_movimiento_aves_company_id ON movimiento_aves(company_id);
CREATE INDEX IF NOT EXISTS ix_movimiento_aves_granjas ON movimiento_aves(granja_origen_id, granja_destino_id);

-- Índices para historial_inventario
CREATE INDEX IF NOT EXISTS ix_historial_inventario_inventario_id ON historial_inventario(inventario_id);
CREATE INDEX IF NOT EXISTS ix_historial_inventario_lote_id ON historial_inventario(lote_id);
CREATE INDEX IF NOT EXISTS ix_historial_inventario_fecha_cambio ON historial_inventario(fecha_cambio);
CREATE INDEX IF NOT EXISTS ix_historial_inventario_tipo_cambio ON historial_inventario(tipo_cambio);
CREATE INDEX IF NOT EXISTS ix_historial_inventario_movimiento_id ON historial_inventario(movimiento_id);
CREATE INDEX IF NOT EXISTS ix_historial_inventario_usuario_cambio_id ON historial_inventario(usuario_cambio_id);
CREATE INDEX IF NOT EXISTS ix_historial_inventario_company_id ON historial_inventario(company_id);
CREATE INDEX IF NOT EXISTS ix_historial_inventario_ubicacion ON historial_inventario(granja_id, nucleo_id, galpon_id);

-- =====================================================
-- CLAVES FORÁNEAS
-- =====================================================

-- Claves foráneas para inventario_aves
ALTER TABLE inventario_aves 
    ADD CONSTRAINT IF NOT EXISTS fk_inventario_aves_lotes_lote_id 
    FOREIGN KEY (lote_id) REFERENCES lotes(lote_id) ON DELETE RESTRICT;

ALTER TABLE inventario_aves 
    ADD CONSTRAINT IF NOT EXISTS fk_inventario_aves_farms_granja_id 
    FOREIGN KEY (granja_id) REFERENCES farms(id) ON DELETE RESTRICT;

ALTER TABLE inventario_aves 
    ADD CONSTRAINT IF NOT EXISTS fk_inventario_aves_nucleos_nucleo_id_granja_id 
    FOREIGN KEY (nucleo_id, granja_id) REFERENCES nucleos(nucleo_id, granja_id) ON DELETE RESTRICT;

ALTER TABLE inventario_aves 
    ADD CONSTRAINT IF NOT EXISTS fk_inventario_aves_galpones_galpon_id 
    FOREIGN KEY (galpon_id) REFERENCES galpones(galpon_id) ON DELETE RESTRICT;

-- Claves foráneas para movimiento_aves
ALTER TABLE movimiento_aves 
    ADD CONSTRAINT IF NOT EXISTS fk_movimiento_aves_inventario_origen_id 
    FOREIGN KEY (inventario_origen_id) REFERENCES inventario_aves(id) ON DELETE RESTRICT;

ALTER TABLE movimiento_aves 
    ADD CONSTRAINT IF NOT EXISTS fk_movimiento_aves_inventario_destino_id 
    FOREIGN KEY (inventario_destino_id) REFERENCES inventario_aves(id) ON DELETE RESTRICT;

ALTER TABLE movimiento_aves 
    ADD CONSTRAINT IF NOT EXISTS fk_movimiento_aves_lote_origen_id 
    FOREIGN KEY (lote_origen_id) REFERENCES lotes(lote_id) ON DELETE RESTRICT;

ALTER TABLE movimiento_aves 
    ADD CONSTRAINT IF NOT EXISTS fk_movimiento_aves_lote_destino_id 
    FOREIGN KEY (lote_destino_id) REFERENCES lotes(lote_id) ON DELETE RESTRICT;

ALTER TABLE movimiento_aves 
    ADD CONSTRAINT IF NOT EXISTS fk_movimiento_aves_granja_origen_id 
    FOREIGN KEY (granja_origen_id) REFERENCES farms(id) ON DELETE RESTRICT;

ALTER TABLE movimiento_aves 
    ADD CONSTRAINT IF NOT EXISTS fk_movimiento_aves_granja_destino_id 
    FOREIGN KEY (granja_destino_id) REFERENCES farms(id) ON DELETE RESTRICT;

-- Claves foráneas para historial_inventario
ALTER TABLE historial_inventario 
    ADD CONSTRAINT IF NOT EXISTS fk_historial_inventario_inventario_id 
    FOREIGN KEY (inventario_id) REFERENCES inventario_aves(id) ON DELETE RESTRICT;

ALTER TABLE historial_inventario 
    ADD CONSTRAINT IF NOT EXISTS fk_historial_inventario_lote_id 
    FOREIGN KEY (lote_id) REFERENCES lotes(lote_id) ON DELETE RESTRICT;

ALTER TABLE historial_inventario 
    ADD CONSTRAINT IF NOT EXISTS fk_historial_inventario_movimiento_id 
    FOREIGN KEY (movimiento_id) REFERENCES movimiento_aves(id) ON DELETE RESTRICT;

ALTER TABLE historial_inventario 
    ADD CONSTRAINT IF NOT EXISTS fk_historial_inventario_granja_id 
    FOREIGN KEY (granja_id) REFERENCES farms(id) ON DELETE RESTRICT;

ALTER TABLE historial_inventario 
    ADD CONSTRAINT IF NOT EXISTS fk_historial_inventario_nucleo_id_granja_id 
    FOREIGN KEY (nucleo_id, granja_id) REFERENCES nucleos(nucleo_id, granja_id) ON DELETE RESTRICT;

ALTER TABLE historial_inventario 
    ADD CONSTRAINT IF NOT EXISTS fk_historial_inventario_galpon_id 
    FOREIGN KEY (galpon_id) REFERENCES galpones(galpon_id) ON DELETE RESTRICT;

-- =====================================================
-- COMENTARIOS EN LAS TABLAS
-- =====================================================

COMMENT ON TABLE inventario_aves IS 'Inventario actual de aves por ubicación (granja, núcleo, galpón)';
COMMENT ON COLUMN inventario_aves.lote_id IS 'ID del lote al que pertenecen las aves';
COMMENT ON COLUMN inventario_aves.cantidad_hembras IS 'Cantidad actual de aves hembras';
COMMENT ON COLUMN inventario_aves.cantidad_machos IS 'Cantidad actual de aves machos';
COMMENT ON COLUMN inventario_aves.cantidad_mixtas IS 'Cantidad actual de aves mixtas';
COMMENT ON COLUMN inventario_aves.estado IS 'Estado del inventario: Activo, Trasladado, Liquidado, Eliminado';

COMMENT ON TABLE movimiento_aves IS 'Registro de movimientos y traslados de aves entre ubicaciones';
COMMENT ON COLUMN movimiento_aves.numero_movimiento IS 'Número único del movimiento generado automáticamente';
COMMENT ON COLUMN movimiento_aves.tipo_movimiento IS 'Tipo: Traslado, Ajuste, Liquidacion, Division, Unificacion';
COMMENT ON COLUMN movimiento_aves.estado IS 'Estado: Pendiente, Completado, Cancelado';
COMMENT ON COLUMN movimiento_aves.usuario_movimiento_id IS 'ID del usuario que realiza el movimiento';

COMMENT ON TABLE historial_inventario IS 'Historial completo de cambios en inventarios para trazabilidad';
COMMENT ON COLUMN historial_inventario.tipo_cambio IS 'Tipo de cambio: Entrada, Salida, Ajuste, Traslado';
COMMENT ON COLUMN historial_inventario.cantidad_hembras_anterior IS 'Cantidad de hembras antes del cambio';
COMMENT ON COLUMN historial_inventario.cantidad_hembras_nueva IS 'Cantidad de hembras después del cambio';

-- =====================================================
-- FUNCIONES AUXILIARES
-- =====================================================

-- Función para generar número de movimiento automático
CREATE OR REPLACE FUNCTION generar_numero_movimiento()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.numero_movimiento IS NULL OR NEW.numero_movimiento = '' THEN
        NEW.numero_movimiento := 'MOV-' || TO_CHAR(NOW(), 'YYYYMMDD') || '-' || LPAD(NEW.id::TEXT, 6, '0');
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Trigger para generar número de movimiento automáticamente
DROP TRIGGER IF EXISTS trigger_generar_numero_movimiento ON movimiento_aves;
CREATE TRIGGER trigger_generar_numero_movimiento
    BEFORE INSERT ON movimiento_aves
    FOR EACH ROW
    WHEN (NEW.numero_movimiento IS NULL OR NEW.numero_movimiento = '')
    EXECUTE FUNCTION generar_numero_movimiento();

-- =====================================================
-- INSERTAR REGISTRO EN HISTORIAL DE MIGRACIONES
-- =====================================================

-- Insertar en la tabla de historial de migraciones de Entity Framework
-- para que EF Core reconozca que estas tablas ya fueron creadas
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20241002_AddInventarioAvesTables', '9.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

-- =====================================================
-- VERIFICACIÓN FINAL
-- =====================================================

-- Verificar que las tablas fueron creadas correctamente
DO $$
BEGIN
    -- Verificar inventario_aves
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'inventario_aves') THEN
        RAISE NOTICE '✅ Tabla inventario_aves creada correctamente';
    ELSE
        RAISE EXCEPTION '❌ Error: Tabla inventario_aves no fue creada';
    END IF;
    
    -- Verificar movimiento_aves
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'movimiento_aves') THEN
        RAISE NOTICE '✅ Tabla movimiento_aves creada correctamente';
    ELSE
        RAISE EXCEPTION '❌ Error: Tabla movimiento_aves no fue creada';
    END IF;
    
    -- Verificar historial_inventario
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'historial_inventario') THEN
        RAISE NOTICE '✅ Tabla historial_inventario creada correctamente';
    ELSE
        RAISE EXCEPTION '❌ Error: Tabla historial_inventario no fue creada';
    END IF;
    
    RAISE NOTICE '🎉 ¡Todas las tablas del Sistema de Inventario de Aves fueron creadas exitosamente!';
END $$;

-- =====================================================
-- FIN DEL SCRIPT
-- =====================================================
