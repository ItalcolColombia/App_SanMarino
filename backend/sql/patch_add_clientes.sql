-- Idempotent patch to ensure clientes table and menu exist
CREATE TABLE IF NOT EXISTS public.clientes (
    id integer PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
    tipo_documento character varying(50) NOT NULL,
    numero_identificacion character varying(100) NOT NULL,
    nombre character varying(200) NOT NULL,
    correo character varying(200),
    telefono character varying(50),
    tipo_cliente character varying(50),
    pais character varying(100),
    provincia character varying(100),
    distrito character varying(100),
    planta character varying(100),
    zona character varying(100),
    status character varying(1) NOT NULL DEFAULT 'A',
    company_id integer NOT NULL,
    created_by_user_id integer NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT timezone('utc', now()),
    updated_by_user_id integer,
    updated_at timestamp with time zone,
    deleted_at timestamp with time zone
);

CREATE INDEX IF NOT EXISTS ix_clientes_company_status ON public.clientes (company_id, status);
CREATE UNIQUE INDEX IF NOT EXISTS ux_clientes_company_nro_identificacion ON public.clientes (company_id, numero_identificacion);

-- Insert menu and assignments (guarded by NOT EXISTS in migration)
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, key, sort_order, is_group, created_at, updated_at)
SELECT
    'Gestión de Clientes',
    'users',
    '/config/clientes',
    NULL,
    COALESCE((SELECT MAX(m."order") FROM menus m WHERE m.parent_id IS NULL), 0) + 1,
    true,
    'gestion_clientes',
    0,
    false,
    timezone('utc', now()),
    timezone('utc', now())
WHERE NOT EXISTS (
    SELECT 1 FROM menus WHERE route = '/config/clientes'
);

INSERT INTO role_menus (role_id, menu_id)
SELECT DISTINCT rm.role_id, nuevo.id
FROM menus nuevo
CROSS JOIN (
    SELECT DISTINCT rm2.role_id
    FROM role_menus rm2
    INNER JOIN menus m ON m.id = rm2.menu_id
    WHERE m.route = '/config'
) rm
WHERE nuevo.route = '/config/clientes'
  AND NOT EXISTS (
      SELECT 1 FROM role_menus ex
      WHERE ex.role_id = rm.role_id AND ex.menu_id = nuevo.id
  );

INSERT INTO company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT DISTINCT
    cm.company_id,
    nuevo.id,
    true,
    cm.sort_order + 1,
    cm.parent_menu_id
FROM company_menus cm
INNER JOIN menus m ON m.id = cm.menu_id AND m.route = '/config'
INNER JOIN menus nuevo ON nuevo.route = '/config/clientes'
WHERE NOT EXISTS (
    SELECT 1 FROM company_menus ex
    WHERE ex.company_id = cm.company_id AND ex.menu_id = nuevo.id
);
