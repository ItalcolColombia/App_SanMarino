-- Tabla para ítems de inventario (Ecuador/Panama) en Configuraciones.
-- Nota: Esta tabla también se crea en inventario_gestion_tables.sql (todo en uno).
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
