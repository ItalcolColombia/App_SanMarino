-- Tablas para el módulo Gestión de Inventario (Panama/Ecuador).
-- Los ítems usan item_inventario_ecuador (Configuración > Ítems inventario Ecuador).
-- Orden: 1) item_inventario_ecuador (si no existe), 2) inventario_gestion_stock, 3) inventario_gestion_movimiento.

-- 1) Catálogo de ítems de inventario (Ecuador/Panama) — crear primero si no existe.
CREATE TABLE IF NOT EXISTS public.item_inventario_ecuador (
    id SERIAL PRIMARY KEY,
    codigo VARCHAR(50) NOT NULL,
    nombre VARCHAR(200) NOT NULL,
    tipo_item VARCHAR(50) NOT NULL DEFAULT 'alimento',
    unidad VARCHAR(20) NOT NULL DEFAULT 'kg',
    descripcion VARCHAR(500) NULL,
    activo BOOLEAN NOT NULL DEFAULT true,
    company_id INT NOT NULL,
    pais_id INT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT fk_item_inv_ecuador_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE RESTRICT,
    CONSTRAINT fk_item_inv_ecuador_pais FOREIGN KEY (pais_id) REFERENCES public.paises(pais_id) ON DELETE RESTRICT,
    CONSTRAINT uq_item_inv_ecuador_company_pais_codigo UNIQUE (company_id, pais_id, codigo)
);
CREATE INDEX IF NOT EXISTS ix_item_inventario_ecuador_tipo_item ON public.item_inventario_ecuador (tipo_item);
CREATE INDEX IF NOT EXISTS ix_item_inventario_ecuador_company_id ON public.item_inventario_ecuador (company_id);
CREATE INDEX IF NOT EXISTS ix_item_inventario_ecuador_pais_id ON public.item_inventario_ecuador (pais_id);
COMMENT ON TABLE public.item_inventario_ecuador IS 'Catálogo de ítems de inventario para Gestión de Inventario (Ecuador/Panama).';

-- Columnas adicionales para planilla (GRUPO, TIPO DE INVENTARIO, Desc. tipo inventario, Referencia, Desc. item, Concepto)
ALTER TABLE public.item_inventario_ecuador ADD COLUMN IF NOT EXISTS grupo VARCHAR(100) NULL;
ALTER TABLE public.item_inventario_ecuador ADD COLUMN IF NOT EXISTS tipo_inventario_codigo VARCHAR(50) NULL;
ALTER TABLE public.item_inventario_ecuador ADD COLUMN IF NOT EXISTS descripcion_tipo_inventario VARCHAR(200) NULL;
ALTER TABLE public.item_inventario_ecuador ADD COLUMN IF NOT EXISTS referencia VARCHAR(100) NULL;
ALTER TABLE public.item_inventario_ecuador ADD COLUMN IF NOT EXISTS descripcion_item VARCHAR(500) NULL;
ALTER TABLE public.item_inventario_ecuador ADD COLUMN IF NOT EXISTS concepto VARCHAR(200) NULL;

-- 2) Stock: por granja o por granja+núcleo+galpón (alimento). Referencia a item_inventario_ecuador.
CREATE TABLE IF NOT EXISTS public.inventario_gestion_stock (
    id SERIAL PRIMARY KEY,
    company_id INT NOT NULL,
    pais_id INT NOT NULL,
    farm_id INT NOT NULL,
    nucleo_id VARCHAR(50) NULL,
    galpon_id VARCHAR(50) NULL,
    item_inventario_ecuador_id INT NOT NULL,
    quantity NUMERIC(18,3) NOT NULL DEFAULT 0,
    unit VARCHAR(20) NOT NULL DEFAULT 'kg',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT fk_igs_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE RESTRICT,
    CONSTRAINT fk_igs_pais FOREIGN KEY (pais_id) REFERENCES public.paises(pais_id) ON DELETE RESTRICT,
    CONSTRAINT fk_igs_farm FOREIGN KEY (farm_id) REFERENCES public.farms(id) ON DELETE RESTRICT,
    CONSTRAINT fk_igs_item_inventario_ecuador FOREIGN KEY (item_inventario_ecuador_id) REFERENCES public.item_inventario_ecuador(id) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_inventario_gestion_stock_farm_item_nucleo_galpon
    ON public.inventario_gestion_stock (farm_id, item_inventario_ecuador_id, nucleo_id, galpon_id);
CREATE INDEX IF NOT EXISTS ix_inventario_gestion_stock_company_id ON public.inventario_gestion_stock (company_id);
CREATE INDEX IF NOT EXISTS ix_inventario_gestion_stock_pais_id ON public.inventario_gestion_stock (pais_id);

-- 3) Movimientos (ingresos y traslados). Referencia a item_inventario_ecuador.
CREATE TABLE IF NOT EXISTS public.inventario_gestion_movimiento (
    id SERIAL PRIMARY KEY,
    company_id INT NOT NULL,
    pais_id INT NOT NULL,
    farm_id INT NOT NULL,
    nucleo_id VARCHAR(50) NULL,
    galpon_id VARCHAR(50) NULL,
    item_inventario_ecuador_id INT NOT NULL,
    quantity NUMERIC(18,3) NOT NULL,
    unit VARCHAR(20) NOT NULL DEFAULT 'kg',
    movement_type VARCHAR(30) NOT NULL,
    from_farm_id INT NULL,
    from_nucleo_id VARCHAR(50) NULL,
    from_galpon_id VARCHAR(50) NULL,
    reference VARCHAR(100) NULL,
    reason VARCHAR(500) NULL,
    transfer_group_id UUID NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by_user_id VARCHAR(128) NULL,
    CONSTRAINT fk_igm_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE RESTRICT,
    CONSTRAINT fk_igm_pais FOREIGN KEY (pais_id) REFERENCES public.paises(pais_id) ON DELETE RESTRICT,
    CONSTRAINT fk_igm_farm FOREIGN KEY (farm_id) REFERENCES public.farms(id) ON DELETE RESTRICT,
    CONSTRAINT fk_igm_item_inventario_ecuador FOREIGN KEY (item_inventario_ecuador_id) REFERENCES public.item_inventario_ecuador(id) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_igm_farm_item ON public.inventario_gestion_movimiento (farm_id, item_inventario_ecuador_id);
CREATE INDEX IF NOT EXISTS ix_igm_movement_type ON public.inventario_gestion_movimiento (movement_type);
CREATE INDEX IF NOT EXISTS ix_igm_transfer_group ON public.inventario_gestion_movimiento (transfer_group_id);
CREATE INDEX IF NOT EXISTS ix_igm_company_id ON public.inventario_gestion_movimiento (company_id);
CREATE INDEX IF NOT EXISTS ix_igm_pais_id ON public.inventario_gestion_movimiento (pais_id);

COMMENT ON TABLE public.inventario_gestion_stock IS 'Stock del módulo Gestión de Inventario (Panama/Ecuador). Alimento: granja+núcleo+galpón; otros: solo granja. Ítems desde item_inventario_ecuador.';
COMMENT ON TABLE public.inventario_gestion_movimiento IS 'Movimientos (ingresos/traslados/consumos) del módulo Gestión de Inventario. Ítems desde item_inventario_ecuador.';

-- Estado mostrado en histórico: Entrada planta, Entrada granja, Transferencia a granja, Transferencia a planta, Consumo.
ALTER TABLE public.inventario_gestion_movimiento ADD COLUMN IF NOT EXISTS estado VARCHAR(80) NULL;
COMMENT ON COLUMN public.inventario_gestion_movimiento.estado IS 'Estado para histórico: Entrada planta, Entrada granja, Transferencia a granja, Transferencia a planta, Consumo.';

-- Si ya tenía las tablas con catalog_item_id, ejecute antes un ALTER para migrar (opcional):
-- ALTER TABLE inventario_gestion_stock RENAME COLUMN catalog_item_id TO item_inventario_ecuador_id;
-- ALTER TABLE inventario_gestion_stock DROP CONSTRAINT IF EXISTS fk_igs_catalog_item;
-- ALTER TABLE inventario_gestion_stock ADD CONSTRAINT fk_igs_item_inventario_ecuador FOREIGN KEY (item_inventario_ecuador_id) REFERENCES public.item_inventario_ecuador(id) ON DELETE RESTRICT;
-- (y análogo para inventario_gestion_movimiento). Luego copiar datos de catalogo_items a item_inventario_ecuador si aplica.
