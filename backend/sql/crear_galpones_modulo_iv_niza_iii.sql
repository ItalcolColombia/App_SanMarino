-- Espejo de la migracion EF 20260820055219_SeedGalponesModuloIvNizaIii (el despliegue la aplica solo;
-- este archivo sirve para correrlo a mano en DB Studio si hace falta antes del deploy).
--
-- Ticket 19ago26 (verenicemorales@sanmarino.com.co): el nucleo "Modulo IV" de NIZA III quedo SIN
-- galpones, por eso no aparece en la tab Galpones --su desplegable se deriva de los galpones
-- cargados-- ni ofrece galpones al crear lotes. Los tres que debia tener nunca se pudieron crear:
-- el modal proponia un galpon_id ya ocupado (la PK es GLOBAL) y el backend rechazaba el alta.
--
-- Identidad POR NOMBRE (los ids difieren local<->prod). Fail-open: sin la granja/nucleo, NOTICE y
-- RETURN. Idempotente: no hace nada si el nucleo ya tiene 3 galpones activos, y salta el que ya
-- exista por nombre. El Id se elige libre en ejecucion, igual que GalponService.
--
-- Alternativa (no aplicada): revivir G0020/G0021/G0022 --los tres borrados el 18ago26 12:56, sin
-- ni una fila dependiente-- repuntandolos al nucleo. Se descarto para no deshacer un borrado
-- explicito del usuario.

DO $$
DECLARE
    v_company  integer;
    v_granja   integer;
    v_nucleo   varchar(64);
    v_activos  integer;
    v_nombre   text;
    v_n        integer;
    v_id       varchar(64);
    v_creados  integer := 0;
BEGIN
    SELECT c.id INTO v_company
    FROM public.companies c
    WHERE lower(trim(c.name)) = 'agroavicola sanmarino'
    LIMIT 1;

    IF v_company IS NULL THEN
        RAISE NOTICE 'SeedGalponesModuloIvNizaIii: no existe la empresa Agroavicola Sanmarino; nada que hacer.';
        RETURN;
    END IF;

    SELECT f.id INTO v_granja
    FROM public.farms f
    WHERE f.company_id = v_company
      AND lower(trim(f.name)) = 'niza iii'
      AND f.deleted_at IS NULL
    LIMIT 1;

    IF v_granja IS NULL THEN
        RAISE NOTICE 'SeedGalponesModuloIvNizaIii: no existe la granja NIZA III activa; nada que hacer.';
        RETURN;
    END IF;

    -- Se acepta la grafia vieja 'Modulo IV -' (se renombro a 'Modulo IV' el 18ago26).
    SELECT n.nucleo_id INTO v_nucleo
    FROM public.nucleos n
    WHERE n.granja_id = v_granja
      AND n.deleted_at IS NULL
      AND lower(trim(trailing ' -' FROM trim(n.nucleo_nombre))) = 'modulo iv'
    LIMIT 1;

    IF v_nucleo IS NULL THEN
        RAISE NOTICE 'SeedGalponesModuloIvNizaIii: la granja NIZA III no tiene el nucleo Modulo IV activo; nada que hacer.';
        RETURN;
    END IF;

    SELECT count(*) INTO v_activos
    FROM public.galpones g
    WHERE g.granja_id = v_granja AND g.nucleo_id = v_nucleo AND g.deleted_at IS NULL;

    IF v_activos >= 3 THEN
        RAISE NOTICE 'SeedGalponesModuloIvNizaIii: el nucleo ya tiene % galpon(es) activo(s); no se toca nada.', v_activos;
        RETURN;
    END IF;

    FOREACH v_nombre IN ARRAY ARRAY['Galpon 1', 'Galpon 2', 'Galpon 3']
    LOOP
        CONTINUE WHEN EXISTS (
            SELECT 1 FROM public.galpones g
            WHERE g.granja_id = v_granja AND g.nucleo_id = v_nucleo
              AND g.deleted_at IS NULL
              AND lower(trim(g.galpon_nombre)) = lower(v_nombre)
        );

        -- Id libre: el proximo despues del maximo global 'Gnnnn', avanzando si estuviera ocupado
        -- (la PK es global, incluye borrados y todas las empresas). Misma regla que el backend.
        SELECT coalesce(max((regexp_match(g.galpon_id, '^G([0-9]+)$'))[1]::int), 0) + 1
          INTO v_n
        FROM public.galpones g
        WHERE g.galpon_id ~ '^G[0-9]+$';

        LOOP
            v_id := 'G' || lpad(v_n::text, 4, '0');
            EXIT WHEN NOT EXISTS (SELECT 1 FROM public.galpones x WHERE x.galpon_id = v_id);
            v_n := v_n + 1;
        END LOOP;

        INSERT INTO public.galpones
            (galpon_id, nucleo_id, granja_id, galpon_nombre,
             ancho, largo, tipo_galpon, company_id, created_by_user_id, created_at)
        VALUES
            (v_id, v_nucleo, v_granja, v_nombre,
             NULL, NULL, 'Abierto', v_company, 0, now());

        v_creados := v_creados + 1;
    END LOOP;

    RAISE NOTICE 'SeedGalponesModuloIvNizaIii: % galpon(es) creado(s) en el nucleo % de la granja %.',
        v_creados, v_nucleo, v_granja;
END $$;
