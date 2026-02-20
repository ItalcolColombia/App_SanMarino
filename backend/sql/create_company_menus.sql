-- Tabla de relación empresa-menú: qué ítems del menú tiene asignada cada empresa.
-- Ejecutar después de tener las tablas companies y menus (o menús según nombre en BD).

-- Si la tabla de empresas se llama "Companies" (EF por defecto con PascalCase), ajustar según convención real.
-- En este proyecto se usa snake_case (UseSnakeCaseNamingConvention), por tanto: companies, menus.

CREATE TABLE IF NOT EXISTS company_menus (
    company_id integer NOT NULL,
    menu_id integer NOT NULL,
    is_enabled boolean NOT NULL DEFAULT true,
    CONSTRAINT pk_company_menus PRIMARY KEY (company_id, menu_id),
    CONSTRAINT fk_company_menus_company FOREIGN KEY (company_id)
        REFERENCES companies (id) ON DELETE CASCADE,
    CONSTRAINT fk_company_menus_menu FOREIGN KEY (menu_id)
        REFERENCES menus (id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_company_menus_company_id ON company_menus (company_id);
CREATE INDEX IF NOT EXISTS ix_company_menus_menu_id ON company_menus (menu_id);

COMMENT ON TABLE company_menus IS 'Menús asignados a cada empresa; is_enabled permite activar/desactivar por empresa.';
