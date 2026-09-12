using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// «Un seguimiento por lote y por día» en levante y producción pasa de índices únicos a triggers
    /// que leen <c>companies.permite_multiples_seguimientos_diarios</c> en el momento
    /// (<c>fn_trg_seguimiento_unico_por_dia</c>).
    ///
    /// <para>
    /// <b>Por qué.</b> El flag se enciende desde la pantalla de Empresas, pero la BD no se enteraba:
    /// </para>
    /// <list type="bullet">
    ///   <item><c>ux_seguimiento_diario_produccion_lote_dia_utc</c> y <c>ux_sdlr_tipo_lote_rep_dia_utc</c>
    ///   tenían horneados como literales los <c>company_id</c> con el flag al correr
    ///   <c>20260905015934</c>: encenderlo en otra empresa no cambiaba nada.</item>
    ///   <item><c>ix_seguimiento_diario_produccion_lote_id_fecha_registro</c> y
    ///   <c>uq_sdlr_tipo_lote_rep_fecha</c> son únicos por INSTANTE, y los dos formularios guardan la
    ///   fecha a mediodía UTC: el segundo registro del día chocaba siempre, con o sin flag.</item>
    /// </list>
    ///
    /// <para>
    /// <b>Qué queda.</b> Los dos índices por instante siguen, con el mismo nombre, pero NO únicos
    /// (sirven a las lecturas). Los dos por día se borran. Cada tabla gana un trigger BEFORE
    /// INSERT/UPDATE que, si la empresa de la fila no tiene el flag, toma un advisory lock por
    /// (lote, día) y rechaza con 23505 si ya hay otra fila ese día. En levante solo queda libre el
    /// tipo <c>levante</c>; reproductora y el tipo legacy <c>produccion</c> no cambian. Se conserva la
    /// exclusión del id 1090 (Demo) de <c>20260828120000</c>.
    /// </para>
    ///
    /// <para>
    /// Idempotente: <c>DROP ... IF EXISTS</c>, <c>CREATE INDEX IF NOT EXISTS</c>,
    /// <c>CREATE OR REPLACE FUNCTION</c>, <c>DROP TRIGGER IF EXISTS</c>. No toca datos.
    /// </para>
    ///
    /// Plan: <c>fase_de_desarrollo/seguimiento_varios_por_dia_flag_dinamico_plan.md</c>.
    /// </summary>
    public partial class SeguimientoUnicoPorDiaSigueFlagEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- ── fn_trg_seguimiento_unico_por_dia: PRODUCCIÓN ─────────────────────────────────────
                CREATE OR REPLACE FUNCTION public.fn_trg_seguimiento_produccion_unico_por_dia()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $fn$
                DECLARE
                    v_dia date;
                BEGIN
                    IF TG_OP = 'UPDATE'
                       AND NEW.lote_id IS NOT DISTINCT FROM OLD.lote_id
                       AND NEW.company_id IS NOT DISTINCT FROM OLD.company_id
                       AND (NEW.fecha_registro AT TIME ZONE 'UTC')::date
                           IS NOT DISTINCT FROM (OLD.fecha_registro AT TIME ZONE 'UTC')::date THEN
                        RETURN NEW;
                    END IF;

                    IF NEW.lote_id IS NULL OR NEW.fecha_registro IS NULL THEN
                        RETURN NEW;
                    END IF;

                    IF NEW.company_id IS NOT NULL AND EXISTS (
                        SELECT 1 FROM public.companies c
                         WHERE c.id = NEW.company_id AND c.permite_multiples_seguimientos_diarios
                    ) THEN
                        RETURN NEW;
                    END IF;

                    v_dia := (NEW.fecha_registro AT TIME ZONE 'UTC')::date;
                    PERFORM pg_advisory_xact_lock(
                        hashtext('seguimiento_diario_produccion'),
                        hashtext(NEW.lote_id::text || '|' || v_dia::text));

                    IF EXISTS (
                        SELECT 1 FROM public.seguimiento_diario_produccion s
                         WHERE s.lote_id = NEW.lote_id
                           AND s.fecha_registro >= (v_dia::timestamp AT TIME ZONE 'UTC')
                           AND s.fecha_registro <  ((v_dia + 1)::timestamp AT TIME ZONE 'UTC')
                           AND s.id IS DISTINCT FROM NEW.id
                    ) THEN
                        RAISE EXCEPTION 'Ya existe un seguimiento de produccion para el lote % en la fecha %.', NEW.lote_id, v_dia
                            USING ERRCODE = 'unique_violation',
                                  CONSTRAINT = 'ux_seguimiento_diario_produccion_lote_dia_utc';
                    END IF;

                    RETURN NEW;
                END
                $fn$;

                -- ── fn_trg_seguimiento_unico_por_dia: LEVANTE (clave tipo+lote+reproductora+día) ─────
                CREATE OR REPLACE FUNCTION public.fn_trg_seguimiento_levante_unico_por_dia()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $fn$
                DECLARE
                    v_dia date;
                    v_rep text;
                BEGIN
                    IF NEW.id = 1090 THEN
                        RETURN NEW;
                    END IF;

                    IF TG_OP = 'UPDATE'
                       AND NEW.tipo_seguimiento IS NOT DISTINCT FROM OLD.tipo_seguimiento
                       AND NEW.lote_id IS NOT DISTINCT FROM OLD.lote_id
                       AND COALESCE(NEW.reproductora_id, '') = COALESCE(OLD.reproductora_id, '')
                       AND NEW.company_id IS NOT DISTINCT FROM OLD.company_id
                       AND (NEW.fecha AT TIME ZONE 'UTC')::date
                           IS NOT DISTINCT FROM (OLD.fecha AT TIME ZONE 'UTC')::date THEN
                        RETURN NEW;
                    END IF;

                    IF NEW.tipo_seguimiento IS NULL OR NEW.lote_id IS NULL OR NEW.fecha IS NULL THEN
                        RETURN NEW;
                    END IF;

                    IF NEW.tipo_seguimiento = 'levante' AND NEW.company_id IS NOT NULL AND EXISTS (
                        SELECT 1 FROM public.companies c
                         WHERE c.id = NEW.company_id AND c.permite_multiples_seguimientos_diarios
                    ) THEN
                        RETURN NEW;
                    END IF;

                    v_dia := (NEW.fecha AT TIME ZONE 'UTC')::date;
                    v_rep := COALESCE(NEW.reproductora_id, '');
                    PERFORM pg_advisory_xact_lock(
                        hashtext('seguimiento_diario_levante'),
                        hashtext(NEW.tipo_seguimiento || '|' || NEW.lote_id || '|' || v_rep || '|' || v_dia::text));

                    IF EXISTS (
                        SELECT 1 FROM public.seguimiento_diario_levante s
                         WHERE s.tipo_seguimiento = NEW.tipo_seguimiento
                           AND s.lote_id = NEW.lote_id
                           AND COALESCE(s.reproductora_id, '') = v_rep
                           AND s.fecha >= (v_dia::timestamp AT TIME ZONE 'UTC')
                           AND s.fecha <  ((v_dia + 1)::timestamp AT TIME ZONE 'UTC')
                           AND s.id <> 1090
                           AND s.id IS DISTINCT FROM NEW.id
                    ) THEN
                        RAISE EXCEPTION 'Ya existe un seguimiento % para el lote % en la fecha %.', NEW.tipo_seguimiento, NEW.lote_id, v_dia
                            USING ERRCODE = 'unique_violation',
                                  CONSTRAINT = 'ux_sdlr_tipo_lote_rep_dia_utc';
                    END IF;

                    RETURN NEW;
                END
                $fn$;

                -- ── Índices: los únicos por día se van; los por instante quedan NO únicos ────────────
                DROP INDEX IF EXISTS public.ux_seguimiento_diario_produccion_lote_dia_utc;
                DROP INDEX IF EXISTS public.ux_sdlr_tipo_lote_rep_dia_utc;

                DROP INDEX IF EXISTS public.ix_seguimiento_diario_produccion_lote_id_fecha_registro;
                CREATE INDEX IF NOT EXISTS ix_seguimiento_diario_produccion_lote_id_fecha_registro
                    ON public.seguimiento_diario_produccion (lote_id, fecha_registro);

                DROP INDEX IF EXISTS public.uq_sdlr_tipo_lote_rep_fecha;
                CREATE INDEX IF NOT EXISTS uq_sdlr_tipo_lote_rep_fecha
                    ON public.seguimiento_diario_levante (tipo_seguimiento, lote_id, COALESCE(reproductora_id, ''), fecha);

                -- ── Triggers ─────────────────────────────────────────────────────────────────────────
                DROP TRIGGER IF EXISTS trg_seguimiento_produccion_unico_por_dia ON public.seguimiento_diario_produccion;
                CREATE TRIGGER trg_seguimiento_produccion_unico_por_dia
                    BEFORE INSERT OR UPDATE ON public.seguimiento_diario_produccion
                    FOR EACH ROW EXECUTE FUNCTION public.fn_trg_seguimiento_produccion_unico_por_dia();

                DROP TRIGGER IF EXISTS trg_seguimiento_levante_unico_por_dia ON public.seguimiento_diario_levante;
                CREATE TRIGGER trg_seguimiento_levante_unico_por_dia
                    BEFORE INSERT OR UPDATE ON public.seguimiento_diario_levante
                    FOR EACH ROW EXECUTE FUNCTION public.fn_trg_seguimiento_levante_unico_por_dia();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Vuelve a los índices únicos. Fail-soft como 20260828120000: si mientras tanto una empresa
            // con el flag cargó dos registros el mismo día, el índice que no puede crearse queda como
            // WARNING en vez de tirar el arranque.
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_seguimiento_produccion_unico_por_dia ON public.seguimiento_diario_produccion;
                DROP TRIGGER IF EXISTS trg_seguimiento_levante_unico_por_dia ON public.seguimiento_diario_levante;
                DROP FUNCTION IF EXISTS public.fn_trg_seguimiento_produccion_unico_por_dia();
                DROP FUNCTION IF EXISTS public.fn_trg_seguimiento_levante_unico_por_dia();

                DO $mig$
                DECLARE
                    v_ids text;
                BEGIN
                    SELECT string_agg(id::text, ',') INTO v_ids
                      FROM companies
                     WHERE permite_multiples_seguimientos_diarios = true;

                    -- PRODUCCIÓN, por instante
                    IF EXISTS (SELECT 1 FROM seguimiento_diario_produccion
                                GROUP BY lote_id, fecha_registro HAVING count(*) > 1) THEN
                        RAISE WARNING 'seguimiento_diario_produccion: hay filas con el mismo (lote, fecha_registro); el indice NO vuelve a ser unico.';
                    ELSE
                        EXECUTE 'DROP INDEX IF EXISTS ix_seguimiento_diario_produccion_lote_id_fecha_registro';
                        EXECUTE 'CREATE UNIQUE INDEX IF NOT EXISTS ix_seguimiento_diario_produccion_lote_id_fecha_registro
                                     ON seguimiento_diario_produccion (lote_id, fecha_registro)';
                    END IF;

                    -- PRODUCCIÓN, por día
                    BEGIN
                        IF v_ids IS NULL THEN
                            EXECUTE 'CREATE UNIQUE INDEX IF NOT EXISTS ux_seguimiento_diario_produccion_lote_dia_utc
                                         ON seguimiento_diario_produccion (lote_id, ((fecha_registro AT TIME ZONE ''UTC'')::date))';
                        ELSE
                            EXECUTE format('CREATE UNIQUE INDEX IF NOT EXISTS ux_seguimiento_diario_produccion_lote_dia_utc
                                         ON seguimiento_diario_produccion (lote_id, ((fecha_registro AT TIME ZONE ''UTC'')::date))
                                      WHERE company_id IS NULL OR company_id NOT IN (%s)', v_ids);
                        END IF;
                    EXCEPTION WHEN unique_violation THEN
                        RAISE WARNING 'seguimiento_diario_produccion: hay dias duplicados fuera de las empresas con el flag; ux_seguimiento_diario_produccion_lote_dia_utc NO se creo.';
                    END;

                    -- LEVANTE, por instante
                    IF EXISTS (SELECT 1 FROM seguimiento_diario_levante
                                GROUP BY tipo_seguimiento, lote_id, COALESCE(reproductora_id, ''), fecha HAVING count(*) > 1) THEN
                        RAISE WARNING 'seguimiento_diario_levante: hay filas con el mismo (tipo, lote, reproductora, fecha); uq_sdlr_tipo_lote_rep_fecha NO vuelve a ser unico.';
                    ELSE
                        EXECUTE 'DROP INDEX IF EXISTS uq_sdlr_tipo_lote_rep_fecha';
                        EXECUTE 'CREATE UNIQUE INDEX IF NOT EXISTS uq_sdlr_tipo_lote_rep_fecha
                                     ON seguimiento_diario_levante (tipo_seguimiento, lote_id, COALESCE(reproductora_id, ''''), fecha)';
                    END IF;

                    -- LEVANTE, por día
                    BEGIN
                        IF v_ids IS NULL THEN
                            EXECUTE 'CREATE UNIQUE INDEX IF NOT EXISTS ux_sdlr_tipo_lote_rep_dia_utc
                                         ON seguimiento_diario_levante (tipo_seguimiento, lote_id, COALESCE(reproductora_id, ''''), ((fecha AT TIME ZONE ''UTC'')::date))
                                      WHERE id NOT IN (1090)';
                        ELSE
                            EXECUTE format('CREATE UNIQUE INDEX IF NOT EXISTS ux_sdlr_tipo_lote_rep_dia_utc
                                         ON seguimiento_diario_levante (tipo_seguimiento, lote_id, COALESCE(reproductora_id, ''''), ((fecha AT TIME ZONE ''UTC'')::date))
                                      WHERE id NOT IN (1090)
                                        AND (tipo_seguimiento <> ''levante'' OR company_id IS NULL OR company_id NOT IN (%s))', v_ids);
                        END IF;
                    EXCEPTION WHEN unique_violation THEN
                        RAISE WARNING 'seguimiento_diario_levante: hay dias duplicados fuera de las empresas con el flag; ux_sdlr_tipo_lote_rep_dia_utc NO se creo.';
                    END;
                END
                $mig$;
                """);
        }
    }
}
