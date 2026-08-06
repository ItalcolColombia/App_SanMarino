using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// <c>trg_lotes_sync_lote_postura_levante</c>, en su rama UPDATE, hacía
    /// <c>aves_h_actual = NEW.hembras_l</c> / <c>aves_m_actual = NEW.machos_l</c> sin condición.
    /// Es decir: <b>editar cualquier campo del lote</b> (el técnico, la regional, la fase, el
    /// código ERP…) devolvía el saldo de aves vivas al valor de encasetamiento, borrando todas
    /// las bajas que el seguimiento diario había ido descontando.
    ///
    /// <para>
    /// Medido en el lote S-369 (20.458 hembras): un solo UPDATE sobre <c>lotes</c> habría
    /// deshecho el descuento de 1.440 hembras y 1.036 machos, dejando el maestro en desacuerdo
    /// con <c>seguimiento_diario_levante</c> y con los reportes.
    /// </para>
    ///
    /// <para>
    /// Arreglo: el saldo VIVO deja de pisarse. Solo se ajusta por el <b>delta</b> del
    /// encasetamiento — corregir <c>hembras_l</c> de 10.000 a 10.100 sube el saldo en 100 y
    /// conserva las bajas ya aplicadas. Los campos <c>aves_*_inicial</c> siguen espejando
    /// <c>hembras_l</c>/<c>machos_l</c> como siempre. La rama INSERT no cambia: un lote nuevo
    /// nace con saldo = encasetamiento, que es lo correcto.
    /// </para>
    ///
    /// Idempotente: es un <c>CREATE OR REPLACE FUNCTION</c>; correrla dos veces deja lo mismo.
    /// </summary>
    public partial class ArreglarTriggerSyncLotePosturaLevanteNoPisarAvesVivas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CuerpoFuncion(pisarAvesVivas: false));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CuerpoFuncion(pisarAvesVivas: true));
        }

        /// <summary>
        /// Cuerpo de la función. <paramref name="pisarAvesVivas"/> = true reproduce el
        /// comportamiento anterior (lo usa <c>Down</c>); false es el arreglo.
        /// El resto del cuerpo es idéntico en las dos versiones — así el diff entre ir y volver
        /// es exactamente las dos líneas del saldo y nada más.
        /// </summary>
        private static string CuerpoFuncion(bool pisarAvesVivas)
        {
            var saldo = pisarAvesVivas
                ? @"            aves_h_actual       = NEW.hembras_l,
            aves_m_actual       = NEW.machos_l,"
                : @"            -- El saldo vivo NO se pisa: se corre por el delta del encasetamiento para que
            -- corregir hembras_l mueva el saldo lo mismo sin borrar las bajas ya descontadas.
            aves_h_actual       = GREATEST(0, COALESCE(aves_h_actual, NEW.hembras_l, 0)
                                              + COALESCE(NEW.hembras_l, 0) - COALESCE(OLD.hembras_l, 0)),
            aves_m_actual       = GREATEST(0, COALESCE(aves_m_actual, NEW.machos_l, 0)
                                              + COALESCE(NEW.machos_l, 0) - COALESCE(OLD.machos_l, 0)),";

            return $@"
CREATE OR REPLACE FUNCTION public.trg_lotes_sync_lote_postura_levante()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
BEGIN
    IF TG_OP = 'INSERT' THEN
        INSERT INTO public.lote_postura_levante (
            lote_nombre, granja_id, nucleo_id, galpon_id, regional, fecha_encaset,
            hembras_l, machos_l, peso_inicial_h, peso_inicial_m, unif_h, unif_m,
            mort_caja_h, mort_caja_m, raza, ano_tabla_genetica, linea, tipo_linea,
            codigo_guia_genetica, linea_genetica_id, tecnico, mixtas, peso_mixto,
            aves_encasetadas, edad_inicial, lote_erp, estado_traslado,
            pais_id, pais_nombre, empresa_nombre,
            lote_id, lote_padre_id,
            aves_h_inicial, aves_m_inicial, aves_h_actual, aves_m_actual,
            empresa_id, usuario_id, estado, etapa, edad, estado_cierre,
            company_id, created_by_user_id, created_at, updated_by_user_id, updated_at, deleted_at
        ) VALUES (
            NEW.lote_nombre, NEW.granja_id, NEW.nucleo_id, NEW.galpon_id, NEW.regional, NEW.fecha_encaset,
            NEW.hembras_l, NEW.machos_l, NEW.peso_inicial_h, NEW.peso_inicial_m, NEW.unif_h, NEW.unif_m,
            NEW.mort_caja_h, NEW.mort_caja_m, NEW.raza, NEW.ano_tabla_genetica, NEW.linea, NEW.tipo_linea,
            NEW.codigo_guia_genetica, NEW.linea_genetica_id, NEW.tecnico, NEW.mixtas, NEW.peso_mixto,
            NEW.aves_encasetadas, NEW.edad_inicial, NEW.lote_erp, NEW.estado_traslado,
            NEW.pais_id, NEW.pais_nombre, NEW.empresa_nombre,
            NEW.lote_id, NEW.lote_padre_id,
            NEW.hembras_l, NEW.machos_l, NEW.hembras_l, NEW.machos_l,
            NEW.company_id, NEW.created_by_user_id, NEW.fase, NEW.fase,
            COALESCE(NEW.edad_inicial, CASE WHEN NEW.fecha_encaset IS NOT NULL THEN GREATEST(0, (CURRENT_DATE - (NEW.fecha_encaset AT TIME ZONE 'utc')::date) / 7) ELSE 0 END),
            'Abierto',
            NEW.company_id, NEW.created_by_user_id, COALESCE(NEW.created_at, NOW() AT TIME ZONE 'utc'),
            NEW.updated_by_user_id, NEW.updated_at, NEW.deleted_at
        );
        RETURN NEW;
    ELSIF TG_OP = 'UPDATE' THEN
        UPDATE public.lote_postura_levante SET
            lote_nombre         = NEW.lote_nombre,
            granja_id           = NEW.granja_id,
            nucleo_id           = NEW.nucleo_id,
            galpon_id           = NEW.galpon_id,
            regional            = NEW.regional,
            fecha_encaset       = NEW.fecha_encaset,
            hembras_l           = NEW.hembras_l,
            machos_l            = NEW.machos_l,
            peso_inicial_h      = NEW.peso_inicial_h,
            peso_inicial_m      = NEW.peso_inicial_m,
            unif_h              = NEW.unif_h,
            unif_m              = NEW.unif_m,
            mort_caja_h         = NEW.mort_caja_h,
            mort_caja_m         = NEW.mort_caja_m,
            raza                = NEW.raza,
            ano_tabla_genetica  = NEW.ano_tabla_genetica,
            linea               = NEW.linea,
            tipo_linea          = NEW.tipo_linea,
            codigo_guia_genetica= NEW.codigo_guia_genetica,
            linea_genetica_id   = NEW.linea_genetica_id,
            tecnico             = NEW.tecnico,
            mixtas              = NEW.mixtas,
            peso_mixto          = NEW.peso_mixto,
            aves_encasetadas    = NEW.aves_encasetadas,
            edad_inicial        = NEW.edad_inicial,
            lote_erp            = NEW.lote_erp,
            estado_traslado     = NEW.estado_traslado,
            pais_id             = NEW.pais_id,
            pais_nombre         = NEW.pais_nombre,
            empresa_nombre      = NEW.empresa_nombre,
            lote_padre_id       = NEW.lote_padre_id,
            aves_h_inicial      = NEW.hembras_l,
            aves_m_inicial      = NEW.machos_l,
{saldo}
            empresa_id          = NEW.company_id,
            usuario_id          = COALESCE(NEW.updated_by_user_id, NEW.created_by_user_id),
            estado              = NEW.fase,
            etapa               = NEW.fase,
            edad                = COALESCE(NEW.edad_inicial, CASE WHEN NEW.fecha_encaset IS NOT NULL THEN GREATEST(0, (CURRENT_DATE - (NEW.fecha_encaset AT TIME ZONE 'utc')::date) / 7) ELSE 0 END),
            updated_by_user_id  = NEW.updated_by_user_id,
            updated_at          = COALESCE(NEW.updated_at, NOW() AT TIME ZONE 'utc'),
            deleted_at          = NEW.deleted_at
        WHERE lote_id = NEW.lote_id;

        -- Si no existía registro (ej: lote creado antes del trigger), crearlo
        IF NOT FOUND THEN
            INSERT INTO public.lote_postura_levante (
                lote_nombre, granja_id, nucleo_id, galpon_id, regional, fecha_encaset,
                hembras_l, machos_l, peso_inicial_h, peso_inicial_m, unif_h, unif_m,
                mort_caja_h, mort_caja_m, raza, ano_tabla_genetica, linea, tipo_linea,
                codigo_guia_genetica, linea_genetica_id, tecnico, mixtas, peso_mixto,
                aves_encasetadas, edad_inicial, lote_erp, estado_traslado,
                pais_id, pais_nombre, empresa_nombre,
                lote_id, lote_padre_id,
                aves_h_inicial, aves_m_inicial, aves_h_actual, aves_m_actual,
                empresa_id, usuario_id, estado, etapa, edad, estado_cierre,
                company_id, created_by_user_id, created_at, updated_by_user_id, updated_at, deleted_at
            ) VALUES (
                NEW.lote_nombre, NEW.granja_id, NEW.nucleo_id, NEW.galpon_id, NEW.regional, NEW.fecha_encaset,
                NEW.hembras_l, NEW.machos_l, NEW.peso_inicial_h, NEW.peso_inicial_m, NEW.unif_h, NEW.unif_m,
                NEW.mort_caja_h, NEW.mort_caja_m, NEW.raza, NEW.ano_tabla_genetica, NEW.linea, NEW.tipo_linea,
                NEW.codigo_guia_genetica, NEW.linea_genetica_id, NEW.tecnico, NEW.mixtas, NEW.peso_mixto,
                NEW.aves_encasetadas, NEW.edad_inicial, NEW.lote_erp, NEW.estado_traslado,
                NEW.pais_id, NEW.pais_nombre, NEW.empresa_nombre,
                NEW.lote_id, NEW.lote_padre_id,
                NEW.hembras_l, NEW.machos_l, NEW.hembras_l, NEW.machos_l,
                NEW.company_id, COALESCE(NEW.updated_by_user_id, NEW.created_by_user_id), NEW.fase, NEW.fase,
                COALESCE(NEW.edad_inicial, CASE WHEN NEW.fecha_encaset IS NOT NULL THEN GREATEST(0, (CURRENT_DATE - (NEW.fecha_encaset AT TIME ZONE 'utc')::date) / 7) ELSE 0 END), 'Abierto',
                NEW.company_id, NEW.created_by_user_id, COALESCE(NEW.created_at, NOW() AT TIME ZONE 'utc'),
                NEW.updated_by_user_id, COALESCE(NEW.updated_at, NOW() AT TIME ZONE 'utc'), NEW.deleted_at
            );
        END IF;
        RETURN NEW;
    END IF;
    RETURN NULL;
END;
$function$;
";
        }
    }
}
