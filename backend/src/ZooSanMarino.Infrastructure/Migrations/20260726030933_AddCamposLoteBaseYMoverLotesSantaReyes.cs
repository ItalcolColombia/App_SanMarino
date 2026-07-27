using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Corrige el destino de los 10 lotes del Excel de Santa Reyes
    /// (<c>Requerimiento Santa reyes/Lotes.xlsx</c>): el seed
    /// <c>20260725190000_SeedEmpresaSantaReyes</c> los creo como lotes de SEGUIMIENTO
    /// (<c>public.lotes</c> + espejos), pero el Excel no trae aves de encasetamiento —
    /// son definiciones de lote, es decir <b>LOTE BASE</b> (<c>lote_postura_base</c>).
    ///
    /// Hace tres cosas, todas idempotentes:
    /// <list type="number">
    /// <item><b>Schema:</b> agrega a <c>lote_postura_base</c> los campos que el Excel trae y
    /// la tabla no tenia: <c>descripcion_erp</c>, <c>raza</c>, <c>tipo_linea</c>,
    /// <c>fecha_encaset</c> (date). Nullable y neutrales (cualquier empresa puede usarlos).</item>
    /// <item><b>Limpieza:</b> borra los lotes-seguimiento del seed en Santa Reyes (espejos
    /// produccion→levante→etapa→lotes; el historico cascadea por FK) SOLO si siguen como los
    /// dejo el seed (created_by 1, sin aves, sin seguimientos). En Demo borra los lotes de
    /// PRUEBA con nombres del cliente en su granja LA ESPERANZA — acotado por
    /// <c>created_at &lt; 2026-07-20</c> para no tocar lo que el cliente cree evaluando —
    /// incluyendo sus seguimientos de levante/produccion.</item>
    /// <item><b>Seed correcto:</b> inserta los 10 lote base de Santa Reyes con los datos
    /// VERBATIM del Excel (codigo/descripcion de centro de costo, raza, tipo de linea y fecha
    /// de encasetamiento; cantidades en 0 porque el Excel no trae aves).</item>
    /// </list>
    ///
    /// Cero ids hardcodeados: empresa/granja/pais por nombre (los ids difieren local↔prod).
    /// En prod la cadena corre completa en el mismo deploy: el seed crea los lotes y esta
    /// migracion los reubica de inmediato.
    /// </summary>
    public partial class AddCamposLoteBaseYMoverLotesSantaReyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) SCHEMA — idempotente (regla del repo: AddColumn → ADD COLUMN IF NOT EXISTS)
            migrationBuilder.Sql(@"
ALTER TABLE public.lote_postura_base ADD COLUMN IF NOT EXISTS descripcion_erp character varying(200);
ALTER TABLE public.lote_postura_base ADD COLUMN IF NOT EXISTS raza            character varying(80);
ALTER TABLE public.lote_postura_base ADD COLUMN IF NOT EXISTS tipo_linea      character varying(80);
ALTER TABLE public.lote_postura_base ADD COLUMN IF NOT EXISTS fecha_encaset   date;
");

            // 2) DATA — limpieza + seed correcto
            migrationBuilder.Sql(@"
DO $fixlb$
DECLARE
    v_sr_company_id   integer;
    v_demo_company_id integer;
    v_sr_farm_id      integer;
    v_pais_id         integer;
    v_n               integer;
    c_nombres constant text[] := ARRAY[
        'LOTE 216','LOTE 217','LOTE 218','LOTE 221','LOTE 222',
        'LOTE 223','LOTE 227','LOTE 229','LOTE 231','LOTE 234'];
BEGIN
    SELECT c.id INTO v_sr_company_id
    FROM public.companies c WHERE c.name = 'Santa Reyes' ORDER BY c.id LIMIT 1;

    SELECT c.id INTO v_demo_company_id
    FROM public.companies c WHERE c.name = 'Demo' ORDER BY c.id LIMIT 1;

    -- =============================================================================
    -- A) SANTA REYES — quitar los lotes-seguimiento que creo el seed.
    --    Guardas: siguen intactos como los dejo el seed (created_by 1, sin aves) y
    --    SIN seguimientos registrados; si alguien ya capturo datos, ese lote NO se toca.
    --    Orden de borrado: lpp (FK RESTRICT a lpl; se caza por lote_id Y por
    --    lote_postura_levante_id, porque el cierre de un levante crea un lpp con
    --    lote_id NULL) → lpl → etapa → lotes. historico_lote_postura,
    --    espejo_huevo_produccion y liquidacion_cierre_lote_levante cascadean por FK.
    -- =============================================================================
    IF v_sr_company_id IS NOT NULL THEN
        SELECT f.id INTO v_sr_farm_id
        FROM public.farms f
        WHERE f.company_id = v_sr_company_id AND f.name = 'La Esperanza'
        ORDER BY f.id LIMIT 1;

        IF v_sr_farm_id IS NOT NULL THEN
            CREATE TEMP TABLE tmp_lotes_seed_sr AS
            SELECT l.lote_id
            FROM public.lotes l
            WHERE l.company_id = v_sr_company_id
              AND l.granja_id  = v_sr_farm_id
              AND l.lote_nombre = ANY(c_nombres)
              AND l.created_by_user_id = 1
              AND l.hembras_l IS NULL
              AND l.machos_l  IS NULL
              AND NOT EXISTS (SELECT 1 FROM public.seguimiento_diario_levante s
                               WHERE s.lote_id = l.lote_id::text OR s.lote_id_int = l.lote_id)
              AND NOT EXISTS (SELECT 1 FROM public.seguimiento_diario_produccion s
                               WHERE s.lote_id = l.lote_id);

            DELETE FROM public.lote_postura_produccion p
             WHERE EXISTS (SELECT 1 FROM tmp_lotes_seed_sr t WHERE p.lote_id = t.lote_id)
                OR p.lote_postura_levante_id IN (
                       SELECT lpl.lote_postura_levante_id
                       FROM public.lote_postura_levante lpl
                       JOIN tmp_lotes_seed_sr t ON lpl.lote_id = t.lote_id);
            DELETE FROM public.lote_postura_levante    x USING tmp_lotes_seed_sr t WHERE x.lote_id = t.lote_id;
            DELETE FROM public.lote_etapa_levante      e USING tmp_lotes_seed_sr t WHERE e.lote_id = t.lote_id;
            DELETE FROM public.lotes                   l USING tmp_lotes_seed_sr t WHERE l.lote_id = t.lote_id;
            GET DIAGNOSTICS v_n = ROW_COUNT;
            RAISE NOTICE 'AddCamposLoteBaseYMoverLotesSantaReyes: % lote(s) seguimiento del seed eliminados en Santa Reyes.', v_n;
            DROP TABLE tmp_lotes_seed_sr;
        END IF;
    END IF;

    -- =============================================================================
    -- B) DEMO — limpiar los lotes de PRUEBA con nombres del cliente (granja
    --    LA ESPERANZA de Demo, creados 2026-07-17 preparando Santa Reyes) y sus
    --    seguimientos. La guarda created_at < 2026-07-20 protege cualquier lote que
    --    el cliente cree evaluando en Demo despues del 25-jul.
    -- =============================================================================
    IF v_demo_company_id IS NOT NULL THEN
        CREATE TEMP TABLE tmp_lotes_prueba_demo AS
        SELECT l.lote_id
        FROM public.lotes l
        JOIN public.farms f ON f.id = l.granja_id
        WHERE l.company_id = v_demo_company_id
          AND f.name ILIKE 'la esperanza'
          AND l.lote_nombre = ANY(c_nombres)
          AND l.created_at < TIMESTAMPTZ '2026-07-20 00:00:00+00';

        DELETE FROM public.seguimiento_diario_levante s USING tmp_lotes_prueba_demo t
          WHERE s.lote_id = t.lote_id::text OR s.lote_id_int = t.lote_id;
        DELETE FROM public.seguimiento_diario_produccion s USING tmp_lotes_prueba_demo t
          WHERE s.lote_id = t.lote_id;
        DELETE FROM public.lote_postura_produccion p
         WHERE EXISTS (SELECT 1 FROM tmp_lotes_prueba_demo t WHERE p.lote_id = t.lote_id)
            OR p.lote_postura_levante_id IN (
                   SELECT lpl.lote_postura_levante_id
                   FROM public.lote_postura_levante lpl
                   JOIN tmp_lotes_prueba_demo t ON lpl.lote_id = t.lote_id);
        DELETE FROM public.lote_postura_levante    x USING tmp_lotes_prueba_demo t WHERE x.lote_id = t.lote_id;
        DELETE FROM public.lote_etapa_levante      e USING tmp_lotes_prueba_demo t WHERE e.lote_id = t.lote_id;
        DELETE FROM public.lotes                   l USING tmp_lotes_prueba_demo t WHERE l.lote_id = t.lote_id;
        GET DIAGNOSTICS v_n = ROW_COUNT;
        RAISE NOTICE 'AddCamposLoteBaseYMoverLotesSantaReyes: % lote(s) de prueba eliminados en Demo.', v_n;
        DROP TABLE tmp_lotes_prueba_demo;
    END IF;

    -- =============================================================================
    -- C) SANTA REYES — los 10 LOTE BASE con los datos VERBATIM del Excel del cliente
    --    (Lotes.xlsx). Cantidades en 0: el Excel no trae aves de encasetamiento.
    --    erp_create queda NULL (el Excel tampoco trae la fecha de creacion en ERP).
    --    Nota ortografia: 'Desc. Ccostos' trae BABCOCK y la columna Raza BABCOK; se
    --    respeta tal cual viene.
    -- =============================================================================
    IF v_sr_company_id IS NOT NULL AND v_sr_farm_id IS NOT NULL THEN
        SELECT p.pais_id INTO v_pais_id
        FROM public.paises p
        WHERE lower(p.pais_nombre) = 'colombia'
        ORDER BY p.pais_id LIMIT 1;

        INSERT INTO public.lote_postura_base
               (lote_nombre, codigo_erp, descripcion_erp, raza, tipo_linea, fecha_encaset,
                cantidad_hembras, cantidad_machos, cantidad_mixtas,
                pais_id, farm_id, erp_create,
                company_id, created_by_user_id, created_at)
        SELECT v.lote_nombre, v.codigo_erp, v.descripcion_erp, v.raza, v.tipo_linea, v.fecha_encaset,
               0, 0, 0,
               v_pais_id, v_sr_farm_id, NULL,
               v_sr_company_id, 1, now()
        FROM (VALUES
            ('LOTE 216', 'G3002216', 'LOTE 216 BABCOK BROWN',  'BABCOK BROWN',  'ROJA',   DATE '2024-11-22'),
            ('LOTE 217', 'G3001217', 'LOTE 217 LOHMANN LSL',   'LOHMANN LSL',   'BLANCA', DATE '2024-11-14'),
            ('LOTE 218', 'G3002218', 'LOTE 218 BABCOK BROWN',  'BABCOK BROWN',  'ROJA',   DATE '2025-01-24'),
            ('LOTE 221', 'G3001221', 'LOTE 221 LOHMANN LSL',   'LOHMANN LSL',   'BLANCA', DATE '2025-04-11'),
            ('LOTE 222', 'G3002222', 'LOTE 222 BABCOCK BROWN', 'BABCOK BROWN',  'ROJA',   DATE '2025-05-09'),
            ('LOTE 223', 'G3002223', 'LOTE 223 BABCOCK BROWN', 'BABCOK BROWN',  'ROJA',   DATE '2025-06-24'),
            ('LOTE 227', 'G3001227', 'LOTE 227 LOHMANN LSL',   'LOHMANN LSL',   'BLANCA', DATE '2025-08-26'),
            ('LOTE 229', 'G3001229', 'LOTE 229 LOHMANN BROWN', 'LOHMANN BROWN', 'ROJA',   DATE '2025-11-07'),
            ('LOTE 231', 'G3001231', 'LOTE 231 LOHMANN LSL',   'LOHMANN LSL',   'BLANCA', DATE '2026-01-14'),
            ('LOTE 234', 'G3004234', 'LOTE 234 HY LINE',       'HY LINE',       'ROJA',   DATE '2026-02-24')
        ) AS v(lote_nombre, codigo_erp, descripcion_erp, raza, tipo_linea, fecha_encaset)
        WHERE NOT EXISTS (SELECT 1 FROM public.lote_postura_base b
                           WHERE b.company_id = v_sr_company_id
                             AND b.lote_nombre = v.lote_nombre
                             AND b.deleted_at IS NULL);
        GET DIAGNOSTICS v_n = ROW_COUNT;
        RAISE NOTICE 'AddCamposLoteBaseYMoverLotesSantaReyes: % lote(s) base creados en Santa Reyes.', v_n;

        -- Alineacion si la fila ya existia (2a pasada o creada a mano): la guarda
        -- IS DISTINCT FROM deja el UPDATE en 0 filas cuando ya esta todo correcto.
        UPDATE public.lote_postura_base b
           SET codigo_erp      = v.codigo_erp,
               descripcion_erp = v.descripcion_erp,
               raza            = v.raza,
               tipo_linea      = v.tipo_linea,
               fecha_encaset   = v.fecha_encaset,
               farm_id         = v_sr_farm_id
          FROM (VALUES
            ('LOTE 216', 'G3002216', 'LOTE 216 BABCOK BROWN',  'BABCOK BROWN',  'ROJA',   DATE '2024-11-22'),
            ('LOTE 217', 'G3001217', 'LOTE 217 LOHMANN LSL',   'LOHMANN LSL',   'BLANCA', DATE '2024-11-14'),
            ('LOTE 218', 'G3002218', 'LOTE 218 BABCOK BROWN',  'BABCOK BROWN',  'ROJA',   DATE '2025-01-24'),
            ('LOTE 221', 'G3001221', 'LOTE 221 LOHMANN LSL',   'LOHMANN LSL',   'BLANCA', DATE '2025-04-11'),
            ('LOTE 222', 'G3002222', 'LOTE 222 BABCOCK BROWN', 'BABCOK BROWN',  'ROJA',   DATE '2025-05-09'),
            ('LOTE 223', 'G3002223', 'LOTE 223 BABCOCK BROWN', 'BABCOK BROWN',  'ROJA',   DATE '2025-06-24'),
            ('LOTE 227', 'G3001227', 'LOTE 227 LOHMANN LSL',   'LOHMANN LSL',   'BLANCA', DATE '2025-08-26'),
            ('LOTE 229', 'G3001229', 'LOTE 229 LOHMANN BROWN', 'LOHMANN BROWN', 'ROJA',   DATE '2025-11-07'),
            ('LOTE 231', 'G3001231', 'LOTE 231 LOHMANN LSL',   'LOHMANN LSL',   'BLANCA', DATE '2026-01-14'),
            ('LOTE 234', 'G3004234', 'LOTE 234 HY LINE',       'HY LINE',       'ROJA',   DATE '2026-02-24')
          ) AS v(lote_nombre, codigo_erp, descripcion_erp, raza, tipo_linea, fecha_encaset)
         WHERE b.company_id = v_sr_company_id
           AND b.lote_nombre = v.lote_nombre
           AND b.deleted_at IS NULL
           AND (b.codigo_erp      IS DISTINCT FROM v.codigo_erp
             OR b.descripcion_erp IS DISTINCT FROM v.descripcion_erp
             OR b.raza            IS DISTINCT FROM v.raza
             OR b.tipo_linea      IS DISTINCT FROM v.tipo_linea
             OR b.fecha_encaset   IS DISTINCT FROM v.fecha_encaset
             OR b.farm_id         IS DISTINCT FROM v_sr_farm_id);
    END IF;
END
$fixlb$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort: quita los lote base sembrados por esta migracion (los lotes
            // seguimiento eliminados en Up() no se pueden resucitar; los recrearia el
            // seed 20260725190000 si se re-aplicara).
            migrationBuilder.Sql(@"
DELETE FROM public.lote_postura_base b
 USING public.companies c
 WHERE c.id = b.company_id
   AND c.name = 'Santa Reyes'
   AND b.created_by_user_id = 1
   AND b.lote_nombre IN ('LOTE 216','LOTE 217','LOTE 218','LOTE 221','LOTE 222',
                         'LOTE 223','LOTE 227','LOTE 229','LOTE 231','LOTE 234');
");

            migrationBuilder.Sql(@"
ALTER TABLE public.lote_postura_base DROP COLUMN IF EXISTS descripcion_erp;
ALTER TABLE public.lote_postura_base DROP COLUMN IF EXISTS raza;
ALTER TABLE public.lote_postura_base DROP COLUMN IF EXISTS tipo_linea;
ALTER TABLE public.lote_postura_base DROP COLUMN IF EXISTS fecha_encaset;
");
        }
    }
}
