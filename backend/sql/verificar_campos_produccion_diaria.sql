-- ============================================================
-- SCRIPT DE VERIFICACIÓN: Campos de la tabla produccion_diaria
-- ============================================================
-- Este script muestra todos los campos actuales y verifica
-- cuáles campos necesarios están presentes o faltan
-- ============================================================

-- 1. Verificar si la tabla existe
SELECT 
    CASE 
        WHEN EXISTS (
            SELECT FROM information_schema.tables 
            WHERE table_name = 'produccion_diaria'
        ) 
        THEN '✓ La tabla produccion_diaria EXISTE'
        ELSE '✗ La tabla produccion_diaria NO EXISTE'
    END AS estado_tabla;

-- 2. Mostrar TODOS los campos de la tabla produccion_diaria
SELECT 
    '=== CAMPOS ACTUALES EN produccion_diaria ===' AS seccion;

SELECT 
    column_name AS nombre_campo,
    data_type AS tipo_dato,
    is_nullable AS permite_null,
    column_default AS valor_defecto,
    character_maximum_length AS longitud_maxima
FROM information_schema.columns 
WHERE table_name = 'produccion_diaria' 
ORDER BY ordinal_position;

-- 3. Campos REQUERIDOS según la entidad SeguimientoProduccion
SELECT 
    '=== CAMPOS REQUERIDOS (según SeguimientoProduccion) ===' AS seccion;

WITH campos_requeridos AS (
    SELECT 'id' AS campo, 'SERIAL/INTEGER' AS tipo, true AS requerido
    UNION ALL SELECT 'fecha_registro', 'TIMESTAMP WITH TIME ZONE', true
    UNION ALL SELECT 'lote_id', 'TEXT', true
    UNION ALL SELECT 'mortalidad_hembras', 'INTEGER', true
    UNION ALL SELECT 'mortalidad_machos', 'INTEGER', true
    UNION ALL SELECT 'sel_h', 'INTEGER', true
    UNION ALL SELECT 'cons_kg_h', 'DOUBLE PRECISION', true
    UNION ALL SELECT 'cons_kg_m', 'DOUBLE PRECISION', true
    UNION ALL SELECT 'huevo_tot', 'INTEGER', true
    UNION ALL SELECT 'huevo_inc', 'INTEGER', true
    UNION ALL SELECT 'tipo_alimento', 'TEXT', true
    UNION ALL SELECT 'observaciones', 'TEXT', false
    UNION ALL SELECT 'peso_huevo', 'DOUBLE PRECISION', true
    UNION ALL SELECT 'etapa', 'INTEGER', true
)
SELECT 
    cr.campo,
    cr.tipo,
    cr.requerido,
    CASE 
        WHEN EXISTS (
            SELECT 1 FROM information_schema.columns 
            WHERE table_name = 'produccion_diaria' 
            AND column_name = cr.campo
        ) 
        THEN '✓ PRESENTE' 
        ELSE '✗ FALTA' 
    END AS estado
FROM campos_requeridos cr
ORDER BY 
    CASE WHEN cr.requerido THEN 0 ELSE 1 END,
    cr.campo;

-- 4. Análisis detallado: Campos que FALTAN
SELECT 
    '=== CAMPOS QUE FALTAN ===' AS seccion;

WITH campos_requeridos AS (
    SELECT 'sel_h' AS campo
    UNION ALL SELECT 'cons_kg_h'
    UNION ALL SELECT 'cons_kg_m'
    UNION ALL SELECT 'tipo_alimento'
    UNION ALL SELECT 'etapa'
    UNION ALL SELECT 'peso_huevo'
)
SELECT 
    cr.campo AS campo_faltante,
    CASE cr.campo
        WHEN 'sel_h' THEN 'INTEGER NOT NULL DEFAULT 0 - Selección hembras retiradas'
        WHEN 'cons_kg_h' THEN 'DOUBLE PRECISION NOT NULL DEFAULT 0 - Consumo alimento hembras (kg)'
        WHEN 'cons_kg_m' THEN 'DOUBLE PRECISION NOT NULL DEFAULT 0 - Consumo alimento machos (kg)'
        WHEN 'tipo_alimento' THEN 'TEXT NOT NULL DEFAULT ''Standard'' - Tipo de alimento'
        WHEN 'etapa' THEN 'INTEGER NOT NULL DEFAULT 1 - Etapa (1: 25-33, 2: 34-50, 3: >50)'
        WHEN 'peso_huevo' THEN 'DOUBLE PRECISION NOT NULL DEFAULT 0 - Peso promedio del huevo (g)'
    END AS descripcion
FROM campos_requeridos cr
WHERE NOT EXISTS (
    SELECT 1 FROM information_schema.columns 
    WHERE table_name = 'produccion_diaria' 
    AND column_name = cr.campo
)
ORDER BY cr.campo;

-- 5. Índices actuales en la tabla
SELECT 
    '=== ÍNDICES EN produccion_diaria ===' AS seccion;

SELECT 
    indexname AS nombre_indice,
    indexdef AS definicion
FROM pg_indexes 
WHERE tablename = 'produccion_diaria'
ORDER BY indexname;

-- 6. Resumen final
SELECT 
    '=== RESUMEN ===' AS seccion;

SELECT 
    (SELECT COUNT(*) FROM information_schema.columns 
     WHERE table_name = 'produccion_diaria') AS total_campos_actuales,
    (SELECT COUNT(*) FROM (
        SELECT 'sel_h' UNION ALL SELECT 'cons_kg_h' UNION ALL SELECT 'cons_kg_m' 
        UNION ALL SELECT 'tipo_alimento' UNION ALL SELECT 'etapa' UNION ALL SELECT 'peso_huevo'
    ) AS req
    WHERE EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'produccion_diaria' 
        AND column_name = req.column_name
    )) AS campos_requeridos_presentes,
    (SELECT COUNT(*) FROM (
        SELECT 'sel_h' UNION ALL SELECT 'cons_kg_h' UNION ALL SELECT 'cons_kg_m' 
        UNION ALL SELECT 'tipo_alimento' UNION ALL SELECT 'etapa' UNION ALL SELECT 'peso_huevo'
    ) AS req
    WHERE NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'produccion_diaria' 
        AND column_name = req.column_name
    )) AS campos_requeridos_faltantes;

-- ============================================================
-- COMANDO ALTERNATIVO SIMPLE (copiar y pegar directamente):
-- ============================================================
-- SELECT 
--     column_name,
--     data_type,
--     is_nullable,
--     column_default
-- FROM information_schema.columns 
-- WHERE table_name = 'produccion_diaria' 
-- ORDER BY ordinal_position;



