using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixMissingClientesTableAndMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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

ALTER TABLE public.clientes ADD COLUMN IF NOT EXISTS tipo_documento character varying(50) NOT NULL;
ALTER TABLE public.clientes ADD COLUMN IF NOT EXISTS numero_identificacion character varying(100) NOT NULL;
ALTER TABLE public.clientes ADD COLUMN IF NOT EXISTS nombre character varying(200) NOT NULL;
ALTER TABLE public.clientes ADD COLUMN IF NOT EXISTS correo character varying(200);
ALTER TABLE public.clientes ADD COLUMN IF NOT EXISTS telefono character varying(50);
ALTER TABLE public.clientes ADD COLUMN IF NOT EXISTS tipo_cliente character varying(50);
ALTER TABLE public.clientes ADD COLUMN IF NOT EXISTS pais character varying(100);
ALTER TABLE public.clientes ADD COLUMN IF NOT EXISTS provincia character varying(100);
ALTER TABLE public.clientes ADD COLUMN IF NOT EXISTS distrito character varying(100);
ALTER TABLE public.clientes ADD COLUMN IF NOT EXISTS planta character varying(100);
ALTER TABLE public.clientes ADD COLUMN IF NOT EXISTS zona character varying(100);
ALTER TABLE public.clientes ADD COLUMN IF NOT EXISTS status character varying(1) NOT NULL DEFAULT 'A';
ALTER TABLE public.clientes ADD COLUMN IF NOT EXISTS company_id integer NOT NULL;
ALTER TABLE public.clientes ADD COLUMN IF NOT EXISTS created_by_user_id integer NOT NULL;
ALTER TABLE public.clientes ADD COLUMN IF NOT EXISTS created_at timestamp with time zone NOT NULL DEFAULT timezone('utc', now());
ALTER TABLE public.clientes ADD COLUMN IF NOT EXISTS updated_by_user_id integer;
ALTER TABLE public.clientes ADD COLUMN IF NOT EXISTS updated_at timestamp with time zone;
ALTER TABLE public.clientes ADD COLUMN IF NOT EXISTS deleted_at timestamp with time zone;

CREATE INDEX IF NOT EXISTS ix_clientes_company_status ON public.clientes (company_id, status);
CREATE UNIQUE INDEX IF NOT EXISTS ux_clientes_company_nro_identificacion ON public.clientes (company_id, numero_identificacion);

INSERT INTO menus (label, icon, route, parent_id, ""order"", is_active, key, sort_order, is_group, created_at, updated_at)
SELECT
    'Gestión de Clientes',
    'users',
    '/config/clientes',
    NULL,
    COALESCE((SELECT MAX(m.""order"") FROM menus m WHERE m.parent_id IS NULL), 0) + 1,
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
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM company_menus WHERE menu_id = (SELECT id FROM menus WHERE route = '/config/clientes' LIMIT 1);
DELETE FROM role_menus WHERE menu_id = (SELECT id FROM menus WHERE route = '/config/clientes' LIMIT 1);
DELETE FROM menus WHERE route = '/config/clientes';
DROP TABLE IF EXISTS public.clientes;
");
        }
    }
}
