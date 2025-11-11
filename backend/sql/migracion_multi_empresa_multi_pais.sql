-- =====================================================
-- MIGRACIÓN: Multi-Empresa y Multi-País
-- =====================================================
-- Este script crea las tablas y relaciones necesarias
-- para soportar multi-empresa y multi-país

-- 1. Crear tabla company_pais (relación muchos a muchos entre empresas y países)
CREATE TABLE IF NOT EXISTS company_pais (
    company_id INTEGER NOT NULL,
    pais_id INTEGER NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE,
    
    CONSTRAINT pk_company_pais PRIMARY KEY (company_id, pais_id),
    CONSTRAINT fk_company_pais_company FOREIGN KEY (company_id) 
        REFERENCES companies(id) ON DELETE CASCADE,
    CONSTRAINT fk_company_pais_pais FOREIGN KEY (pais_id) 
        REFERENCES paises(pais_id) ON DELETE CASCADE
);

-- Índices para mejorar rendimiento
CREATE INDEX IF NOT EXISTS idx_company_pais_company_id ON company_pais(company_id);
CREATE INDEX IF NOT EXISTS idx_company_pais_pais_id ON company_pais(pais_id);

-- 2. Modificar tabla user_companies para incluir pais_id
-- Primero, verificar si la columna ya existe
DO $$
DECLARE
    constraint_name TEXT;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'user_companies' 
        AND column_name = 'pais_id'
    ) THEN
        -- Agregar columna pais_id (temporalmente nullable para poder actualizar datos)
        ALTER TABLE user_companies ADD COLUMN pais_id INTEGER;
        
        -- Actualizar registros existentes con un país por defecto
        -- Primero, asegurarse de que existe al menos un país
        IF NOT EXISTS (SELECT 1 FROM paises WHERE pais_id = 1) THEN
            -- Crear país por defecto si no existe
            INSERT INTO paises (pais_id, pais_nombre) 
            VALUES (1, 'País por Defecto') 
            ON CONFLICT (pais_id) DO NOTHING;
        END IF;
        
        -- Asignar país por defecto a todos los registros existentes
        UPDATE user_companies SET pais_id = 1 WHERE pais_id IS NULL;
        
        -- Ahora hacer la columna NOT NULL
        ALTER TABLE user_companies ALTER COLUMN pais_id SET NOT NULL;
        
        -- Agregar foreign key
        ALTER TABLE user_companies 
            ADD CONSTRAINT fk_user_companies_pais 
            FOREIGN KEY (pais_id) REFERENCES paises(pais_id) ON DELETE CASCADE;
        
        -- Eliminar la clave primaria antigua (obtener el nombre real de la constraint)
        SELECT constraint_name INTO constraint_name
        FROM information_schema.table_constraints
        WHERE table_name = 'user_companies'
          AND constraint_type = 'PRIMARY KEY';
        
        IF constraint_name IS NOT NULL THEN
            EXECUTE format('ALTER TABLE user_companies DROP CONSTRAINT %I', constraint_name);
        END IF;
        
        -- Crear nueva clave primaria compuesta
        ALTER TABLE user_companies 
            ADD CONSTRAINT pk_user_companies 
            PRIMARY KEY (user_id, company_id, pais_id);
        
        -- Crear índices
        CREATE INDEX IF NOT EXISTS idx_user_companies_user_id ON user_companies(user_id);
        CREATE INDEX IF NOT EXISTS idx_user_companies_company_pais ON user_companies(company_id, pais_id);
    END IF;
END $$;

-- 3. Asegurar que existe un país por defecto
INSERT INTO paises (pais_id, pais_nombre) 
VALUES (1, 'País por Defecto') 
ON CONFLICT (pais_id) DO NOTHING;

-- 4. Validar que todas las empresas tengan al menos un país asignado
-- Si una empresa no tiene países asignados, asignarla al país por defecto (ID=1)
INSERT INTO company_pais (company_id, pais_id, created_at)
SELECT DISTINCT c.id, 1, CURRENT_TIMESTAMP
FROM companies c
WHERE NOT EXISTS (
    SELECT 1 FROM company_pais cp WHERE cp.company_id = c.id
)
ON CONFLICT (company_id, pais_id) DO NOTHING;

-- 5. Validar que todos los user_companies tengan una relación válida empresa-país
-- Actualizar user_companies para asegurar que la combinación empresa-país existe en company_pais
UPDATE user_companies uc
SET pais_id = (
    SELECT cp.pais_id 
    FROM company_pais cp 
    WHERE cp.company_id = uc.company_id 
    LIMIT 1
)
WHERE NOT EXISTS (
    SELECT 1 FROM company_pais cp 
    WHERE cp.company_id = uc.company_id 
    AND cp.pais_id = uc.pais_id
);

-- 6. Comentarios en las tablas
COMMENT ON TABLE company_pais IS 'Relación muchos a muchos entre empresas y países. Permite que una empresa opere en múltiples países.';
COMMENT ON COLUMN user_companies.pais_id IS 'País asociado a la relación usuario-empresa. Permite que un usuario esté asignado a una empresa en un país específico.';

-- =====================================================
-- NOTAS IMPORTANTES:
-- =====================================================
-- 1. Este script es idempotente (se puede ejecutar múltiples veces)
-- 2. Ajusta el país por defecto (ID=1) según tus necesidades
-- 3. Revisa y ajusta los datos existentes después de ejecutar este script
-- 4. Considera crear un país por defecto si no existe:
--    INSERT INTO paises (pais_id, pais_nombre) VALUES (1, 'País por Defecto') ON CONFLICT DO NOTHING;
-- =====================================================

