-- Tabla de movimientos de pollo engorde (lote ave engorde y lote reproductora ave engorde)
-- Ejecutar después de tener lote_ave_engorde, lote_reproductora_ave_engorde y companies.

CREATE TABLE IF NOT EXISTS movimiento_pollo_engorde (
    id SERIAL PRIMARY KEY,
    numero_movimiento VARCHAR(50) NOT NULL UNIQUE,
    fecha_movimiento TIMESTAMP WITH TIME ZONE NOT NULL,
    tipo_movimiento VARCHAR(50) NOT NULL,

    lote_ave_engorde_origen_id INTEGER NULL,
    lote_reproductora_ave_engorde_origen_id INTEGER NULL,
    granja_origen_id INTEGER NULL,
    nucleo_origen_id VARCHAR(64) NULL,
    galpon_origen_id VARCHAR(64) NULL,

    lote_ave_engorde_destino_id INTEGER NULL,
    lote_reproductora_ave_engorde_destino_id INTEGER NULL,
    granja_destino_id INTEGER NULL,
    nucleo_destino_id VARCHAR(64) NULL,
    galpon_destino_id VARCHAR(64) NULL,
    planta_destino VARCHAR(200) NULL,

    cantidad_hembras INTEGER NOT NULL DEFAULT 0,
    cantidad_machos INTEGER NOT NULL DEFAULT 0,
    cantidad_mixtas INTEGER NOT NULL DEFAULT 0,

    motivo_movimiento VARCHAR(500) NULL,
    descripcion VARCHAR(1000) NULL,
    observaciones VARCHAR(1000) NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'Pendiente',

    usuario_movimiento_id INTEGER NOT NULL,
    usuario_nombre VARCHAR(200) NULL,
    fecha_procesamiento TIMESTAMP WITH TIME ZONE NULL,
    fecha_cancelacion TIMESTAMP WITH TIME ZONE NULL,

    company_id INTEGER NOT NULL,
    created_by_user_id INTEGER NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_by_user_id INTEGER NULL,
    updated_at TIMESTAMP WITH TIME ZONE NULL,
    deleted_at TIMESTAMP WITH TIME ZONE NULL,

    CONSTRAINT fk_mpe_lote_ave_engorde_origen FOREIGN KEY (lote_ave_engorde_origen_id)
        REFERENCES lote_ave_engorde (lote_ave_engorde_id) ON DELETE RESTRICT,
    CONSTRAINT fk_mpe_lote_reproductora_origen FOREIGN KEY (lote_reproductora_ave_engorde_origen_id)
        REFERENCES lote_reproductora_ave_engorde (id) ON DELETE RESTRICT,
    CONSTRAINT fk_mpe_lote_ave_engorde_destino FOREIGN KEY (lote_ave_engorde_destino_id)
        REFERENCES lote_ave_engorde (lote_ave_engorde_id) ON DELETE RESTRICT,
    CONSTRAINT fk_mpe_lote_reproductora_destino FOREIGN KEY (lote_reproductora_ave_engorde_destino_id)
        REFERENCES lote_reproductora_ave_engorde (id) ON DELETE RESTRICT,
    CONSTRAINT fk_mpe_granja_origen FOREIGN KEY (granja_origen_id)
        REFERENCES farms (id) ON DELETE RESTRICT,
    CONSTRAINT fk_mpe_granja_destino FOREIGN KEY (granja_destino_id)
        REFERENCES farms (id) ON DELETE RESTRICT,
    CONSTRAINT fk_mpe_company FOREIGN KEY (company_id)
        REFERENCES companies (id) ON DELETE RESTRICT,
    CONSTRAINT ck_mpe_origen_uno CHECK (
        (lote_ave_engorde_origen_id IS NOT NULL AND lote_reproductora_ave_engorde_origen_id IS NULL)
        OR (lote_ave_engorde_origen_id IS NULL AND lote_reproductora_ave_engorde_origen_id IS NOT NULL)
    ),
    CONSTRAINT ck_mpe_cantidades CHECK (
        cantidad_hembras >= 0 AND cantidad_machos >= 0 AND cantidad_mixtas >= 0
        AND (cantidad_hembras + cantidad_machos + cantidad_mixtas) > 0
    ),
    CONSTRAINT ck_mpe_estado CHECK (estado IN ('Pendiente', 'Completado', 'Cancelado', 'Anulado'))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_movimiento_pollo_engorde_numero ON movimiento_pollo_engorde (numero_movimiento);
CREATE INDEX IF NOT EXISTS ix_movimiento_pollo_engorde_fecha ON movimiento_pollo_engorde (fecha_movimiento);
CREATE INDEX IF NOT EXISTS ix_movimiento_pollo_engorde_estado ON movimiento_pollo_engorde (estado);
CREATE INDEX IF NOT EXISTS ix_movimiento_pollo_engorde_company_id ON movimiento_pollo_engorde (company_id);
CREATE INDEX IF NOT EXISTS ix_movimiento_pollo_engorde_lae_origen ON movimiento_pollo_engorde (lote_ave_engorde_origen_id) WHERE lote_ave_engorde_origen_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_movimiento_pollo_engorde_lrae_origen ON movimiento_pollo_engorde (lote_reproductora_ave_engorde_origen_id) WHERE lote_reproductora_ave_engorde_origen_id IS NOT NULL;

COMMENT ON TABLE movimiento_pollo_engorde IS 'Movimientos/traslados de pollo engorde entre lotes (ave engorde o reproductora ave engorde).';
