-- Ticket 19ago26 (verenicemorales@sanmarino.com.co): el nucleo "Modulo IV" (543) de NIZA III
-- (granja 5) quedo SIN galpones, por eso no aparece en la tab Galpones ni ofrece galpones al
-- crear lotes. Los tres que debia tener (Galpon 1, 2 y 3) nunca se pudieron crear: el modal
-- proponia un galpon_id ya ocupado (PK GLOBAL) y el backend rechazaba el alta.
--
-- Este script crea los tres galpones faltantes. Es IDEMPOTENTE: no inserta si el nucleo ya tiene
-- un galpon activo con ese nombre, y elige ids libres (no reusa los borrados G0020/21/22).
-- Equivale exactamente a lo que escribe GalponService.CreateAsync desde la UI.
--
-- Alternativa (no aplicada): revivir G0020/G0021/G0022 -- los tres que se borraron el 18ago26 a
-- las 12:56 y que no tienen NI UNA fila dependiente (sin lotes, inventario ni produccion)--
-- repuntandolos al nucleo 543. Se descarto para no deshacer un borrado explicito del usuario.

BEGIN;

WITH libres AS (
    SELECT 'G' || lpad(n::text, 4, '0') AS galpon_id,
           row_number() OVER (ORDER BY n) AS rn
    FROM generate_series(1, 2000) AS n
    WHERE NOT EXISTS (SELECT 1 FROM galpones x WHERE x.galpon_id = 'G' || lpad(n::text, 4, '0'))
    LIMIT 3
),
faltantes AS (
    SELECT v.nombre, row_number() OVER (ORDER BY v.nombre) AS rn
    FROM (VALUES ('Galpon 1'), ('Galpon 2'), ('Galpon 3')) AS v(nombre)
    WHERE NOT EXISTS (
        SELECT 1 FROM galpones g
        WHERE g.granja_id = 5 AND g.nucleo_id = '543'
          AND g.galpon_nombre = v.nombre AND g.deleted_at IS NULL
    )
)
INSERT INTO galpones (galpon_id, nucleo_id, granja_id, galpon_nombre,
                      ancho, largo, tipo_galpon, company_id, created_by_user_id, created_at)
SELECT l.galpon_id, '543', 5, f.nombre,
       '10', '10', 'Abierto', 1, 1099716353, now()
FROM faltantes f
JOIN libres l ON l.rn = f.rn;

COMMIT;
