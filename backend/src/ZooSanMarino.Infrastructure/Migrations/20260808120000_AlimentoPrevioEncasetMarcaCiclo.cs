using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Alimento que llega ANTES del encasetamiento: marca explícita de ciclo + auditoría de captura.
    ///
    /// <para>
    /// <b>El problema.</b> El alimento llega a la granja 2-7 días antes que los pollitos, pero
    /// <c>inventario_gestion_movimiento</c> tiene UNA sola fecha (<c>created_at</c>) y la que tipea el
    /// usuario la pisa. Contabilidad necesita la fecha REAL de llegada; la operación necesita que el
    /// alimento aparezca en el ciclo correcto. Hoy se resuelve falseando la fecha al primer día de
    /// consumo (Ecuador: 110 de 110 ciclos medidos), lo que destruye el dato contable Y la auditoría
    /// de cuándo se digitó.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué la fecha sola no alcanza.</b> La fn de engorde atribuye el alimento previo por
    /// ventana (<c>[encaset − dias_alimento_previo_encaset, …]</c>), pero 28 de 75 ciclos encadenados
    /// de Ecuador arrancan a menos de 10 días del cierre del anterior: ahí la llegada real cae DENTRO
    /// del ciclo viejo y el corte por fecha la descarta. <c>para_proximo_ciclo</c> es la atribución
    /// EXPLÍCITA que no depende de la ventana.
    /// </para>
    ///
    /// <para>
    /// <b>Qué hace.</b> Tres columnas y el trigger del espejo:
    /// <list type="bullet">
    ///   <item><c>inventario_gestion_movimiento.para_proximo_ciclo</c> — la marca.</item>
    ///   <item><c>inventario_gestion_movimiento.registrado_at</c> — instante REAL de captura, que
    ///     <c>created_at</c> dejó de poder guardar. NULL = fila anterior a la columna.</item>
    ///   <item><c>lote_registro_historico_unificado.para_proximo_ciclo</c> — espejo, poblado por el
    ///     trigger en el INSERT (el histórico se ANULA, nunca se borra: la marca tiene que viajar con
    ///     la fila o el saldo la perdería al eliminar el movimiento).</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>Cero cambio de comportamiento.</b> Ambas marcas nacen en <c>false</c> y ningún cálculo las
    /// lee todavía: sin usar la marca nueva, todo queda byte a byte igual. El trigger se reemplaza con
    /// <c>CREATE OR REPLACE</c> y su cuerpo es idéntico al vigente salvo la columna agregada al
    /// INSERT. Idempotente (<c>ADD COLUMN IF NOT EXISTS</c>) para tolerar reintentos de deploy.
    /// </para>
    ///
    /// <para>
    /// El <c>DEFAULT false</c> no reescribe la tabla: Postgres ≥ 11 guarda el default de las columnas
    /// nuevas en el catálogo (<c>attmissingval</c>) en vez de tocar cada fila.
    /// </para>
    /// </summary>
    public partial class AlimentoPrevioEncasetMarcaCiclo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE public.inventario_gestion_movimiento
                    ADD COLUMN IF NOT EXISTS para_proximo_ciclo boolean NOT NULL DEFAULT false;

                ALTER TABLE public.inventario_gestion_movimiento
                    ADD COLUMN IF NOT EXISTS registrado_at timestamptz NULL;

                ALTER TABLE public.lote_registro_historico_unificado
                    ADD COLUMN IF NOT EXISTS para_proximo_ciclo boolean NOT NULL DEFAULT false;

                COMMENT ON COLUMN public.inventario_gestion_movimiento.para_proximo_ciclo IS
                    'El alimento es para el PROXIMO encasetamiento de este galpon: atribucion explicita al ciclo siguiente, independiente de la ventana por fecha.';
                COMMENT ON COLUMN public.inventario_gestion_movimiento.registrado_at IS
                    'Instante REAL de captura. created_at guarda la fecha del movimiento que tipea el usuario, asi que no sirve de auditoria. NULL = fila anterior a la columna.';
                COMMENT ON COLUMN public.lote_registro_historico_unificado.para_proximo_ciclo IS
                    'Espejo de inventario_gestion_movimiento.para_proximo_ciclo (lo copia el trigger trg_lote_hist_desde_inventario_gestion).';
                """);

            migrationBuilder.Sql(TriggerConParaProximoCiclo);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(TriggerPrevio);

            migrationBuilder.Sql("""
                ALTER TABLE public.lote_registro_historico_unificado
                    DROP COLUMN IF EXISTS para_proximo_ciclo;

                ALTER TABLE public.inventario_gestion_movimiento
                    DROP COLUMN IF EXISTS registrado_at;

                ALTER TABLE public.inventario_gestion_movimiento
                    DROP COLUMN IF EXISTS para_proximo_ciclo;
                """);
        }

        /// <summary>
        /// Idéntico al vigente salvo <c>para_proximo_ciclo</c> en el INSERT del espejo. El trigger es
        /// AFTER INSERT: ningún UPDATE del origen se propaga solo, por eso el endpoint
        /// <c>PUT /ingresos/{id}/destino-ciclo</c> sincroniza la marca a mano.
        /// </summary>
        private const string TriggerConParaProximoCiclo = """
CREATE OR REPLACE FUNCTION public.trg_lote_hist_desde_inventario_gestion()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_lote INTEGER;
    v_tipo VARCHAR(40);
    v_item_txt VARCHAR(400);
    v_acum NUMERIC(18, 3);
    v_hist_id BIGINT;
BEGIN
    v_lote := public.fn_lote_ave_engorde_id_desde_ubicacion(
        NEW.farm_id, NEW.nucleo_id, NEW.galpon_id
    );
    v_tipo := public.fn_tipo_evento_inventario(NEW.movement_type);

    SELECT CONCAT(i.codigo, ' — ', i.nombre)
    INTO v_item_txt
    FROM public.item_inventario_ecuador i
    WHERE i.id = NEW.item_inventario_ecuador_id;

    INSERT INTO public.lote_registro_historico_unificado (
        company_id, lote_ave_engorde_id, farm_id, nucleo_id, galpon_id,
        fecha_operacion, tipo_evento, origen_tabla, origen_id,
        movement_type_original, item_inventario_ecuador_id, item_resumen,
        cantidad_kg, unidad, referencia, numero_documento,
        acumulado_entradas_alimento_kg, para_proximo_ciclo
    ) VALUES (
        NEW.company_id,
        v_lote,
        NEW.farm_id,
        NEW.nucleo_id,
        NEW.galpon_id,
        (NEW.created_at AT TIME ZONE 'UTC')::DATE,
        v_tipo,
        'inventario_gestion_movimiento',
        NEW.id,
        NEW.movement_type,
        NEW.item_inventario_ecuador_id,
        v_item_txt,
        NEW.quantity,
        NEW.unit,
        NEW.reference,
        NULL,
        NULL,
        COALESCE(NEW.para_proximo_ciclo, FALSE)
    )
    RETURNING id INTO v_hist_id;

    IF v_lote IS NOT NULL AND v_tipo IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA') THEN
        v_acum := public.fn_acumulado_entradas_alimento(v_lote, v_hist_id);
        UPDATE public.lote_registro_historico_unificado
        SET acumulado_entradas_alimento_kg = v_acum
        WHERE id = v_hist_id;
    END IF;

    RETURN NEW;
END;
$$;
""";

        /// <summary>Cuerpo vigente antes de esta migración, verbatim (verificado contra la BD).</summary>
        private const string TriggerPrevio = """
CREATE OR REPLACE FUNCTION public.trg_lote_hist_desde_inventario_gestion()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_lote INTEGER;
    v_tipo VARCHAR(40);
    v_item_txt VARCHAR(400);
    v_acum NUMERIC(18, 3);
    v_hist_id BIGINT;
BEGIN
    v_lote := public.fn_lote_ave_engorde_id_desde_ubicacion(
        NEW.farm_id, NEW.nucleo_id, NEW.galpon_id
    );
    v_tipo := public.fn_tipo_evento_inventario(NEW.movement_type);

    SELECT CONCAT(i.codigo, ' — ', i.nombre)
    INTO v_item_txt
    FROM public.item_inventario_ecuador i
    WHERE i.id = NEW.item_inventario_ecuador_id;

    INSERT INTO public.lote_registro_historico_unificado (
        company_id, lote_ave_engorde_id, farm_id, nucleo_id, galpon_id,
        fecha_operacion, tipo_evento, origen_tabla, origen_id,
        movement_type_original, item_inventario_ecuador_id, item_resumen,
        cantidad_kg, unidad, referencia, numero_documento,
        acumulado_entradas_alimento_kg
    ) VALUES (
        NEW.company_id,
        v_lote,
        NEW.farm_id,
        NEW.nucleo_id,
        NEW.galpon_id,
        (NEW.created_at AT TIME ZONE 'UTC')::DATE,
        v_tipo,
        'inventario_gestion_movimiento',
        NEW.id,
        NEW.movement_type,
        NEW.item_inventario_ecuador_id,
        v_item_txt,
        NEW.quantity,
        NEW.unit,
        NEW.reference,
        NULL,
        NULL
    )
    RETURNING id INTO v_hist_id;

    IF v_lote IS NOT NULL AND v_tipo IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA') THEN
        v_acum := public.fn_acumulado_entradas_alimento(v_lote, v_hist_id);
        UPDATE public.lote_registro_historico_unificado
        SET acumulado_entradas_alimento_kg = v_acum
        WHERE id = v_hist_id;
    END IF;

    RETURN NEW;
END;
$$;
""";
    }
}
