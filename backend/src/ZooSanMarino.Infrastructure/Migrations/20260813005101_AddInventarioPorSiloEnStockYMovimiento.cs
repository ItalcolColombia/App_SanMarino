using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Fase B del plan <c>fase_de_desarrollo/santa_reyes_silos_bodegas_inventario_plan.md</c>:
    /// el inventario deja de moverse «sobre el galpón» y pasa a moverse sobre <b>silos</b> y una
    /// <b>bodega</b> de granja para las empresas con <c>maneja_inventario_por_silo</c>.
    ///
    /// <para>
    /// 🔴 <b>Lo delicado de esta migración no son las columnas: es el swap del índice único.</b>
    /// <c>ux_inventario_gestion_stock_clave_natural</c> está cableado <b>por expresión</b> en el
    /// <c>ON CONFLICT</c> de <c>SumarStockAtomicoAsync</c>
    /// (<c>InventarioGestion/Funciones/InventarioGestionService.StockAtomico.cs</c>), y Postgres exige
    /// que el inferidor del conflicto coincida <b>exactamente</b> con el índice. Índice y sentencia
    /// van en el MISMO commit: desalineados, <b>todo ingreso de toda empresa</b> falla con
    /// «no unique or exclusion constraint matching the ON CONFLICT specification».
    /// </para>
    ///
    /// <para>
    /// Para las empresas con el flag apagado <c>silo_id</c> es siempre <c>NULL</c>, así que
    /// <c>COALESCE(silo_id,0)</c> es la constante 0 y la clave nueva es <b>equivalente</b> a la
    /// anterior: ningún saldo se parte ni se fusiona. Lo verifica
    /// <c>backend/sql/verificar_paridad_stock_clave_natural.sql</c> corrido antes y después.
    /// </para>
    ///
    /// <para>Todo el <c>Up()</c> es idempotente (regla de CLAUDE.md §🗄️).</para>
    /// </summary>
    public partial class AddInventarioPorSiloEnStockYMovimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1) Columnas de ubicación ────────────────────────────────────────────────
            // Aditivas y nulables: una fila escrita por una empresa con el flag apagado queda
            // exactamente como hoy.
            migrationBuilder.Sql(@"
ALTER TABLE public.inventario_gestion_stock            ADD COLUMN IF NOT EXISTS silo_id      integer NULL;
ALTER TABLE public.inventario_gestion_movimiento       ADD COLUMN IF NOT EXISTS silo_id      integer NULL;
ALTER TABLE public.inventario_gestion_movimiento       ADD COLUMN IF NOT EXISTS from_silo_id integer NULL;
ALTER TABLE public.lote_registro_historico_unificado   ADD COLUMN IF NOT EXISTS silo_id      integer NULL;
");

            // ── 2) FKs contra el catálogo de silos de la granja ─────────────────────────
            // El espejo histórico NO lleva FK, igual que su farm_id/galpon_id: es una tabla de
            // trazabilidad que sobrevive al borrado del origen (invariante «el histórico se anula,
            // nunca se abandona»).
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_igs_silo') THEN
        ALTER TABLE public.inventario_gestion_stock
            ADD CONSTRAINT fk_igs_silo FOREIGN KEY (silo_id)
            REFERENCES public.farm_silos(id) ON DELETE RESTRICT;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_igm_silo') THEN
        ALTER TABLE public.inventario_gestion_movimiento
            ADD CONSTRAINT fk_igm_silo FOREIGN KEY (silo_id)
            REFERENCES public.farm_silos(id) ON DELETE RESTRICT;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_igm_from_silo') THEN
        ALTER TABLE public.inventario_gestion_movimiento
            ADD CONSTRAINT fk_igm_from_silo FOREIGN KEY (from_silo_id)
            REFERENCES public.farm_silos(id) ON DELETE RESTRICT;
    END IF;
END $$;
");

            // ── 3) 🔴 SWAP DEL ÍNDICE ÚNICO DE LA CLAVE NATURAL ─────────────────────────
            //
            // El COALESCE no es cosmético (misma razón que en AddStockClaveNaturalUnica): dentro de
            // un índice único, NULL nunca es igual a otro NULL. Sin el COALESCE, dos filas de un
            // mismo silo con núcleo/galpón nulos no colisionarían y el saldo se volvería a partir.
            //
            // El texto de la expresión tiene que ser el MISMO que el del ON CONFLICT de
            // SumarStockAtomicoAsync. Si algún día se toca uno, se toca el otro en el mismo commit.
            migrationBuilder.Sql(@"
DROP INDEX IF EXISTS ux_inventario_gestion_stock_clave_natural;

CREATE UNIQUE INDEX IF NOT EXISTS ux_inventario_gestion_stock_clave_natural
    ON public.inventario_gestion_stock
    (farm_id, item_inventario_ecuador_id,
     COALESCE(nucleo_id, ''), COALESCE(galpon_id, ''), COALESCE(silo_id, 0));
");

            // Apoyo para las lecturas por silo (`x.SiloId == siloId`), que el índice de expresión
            // de arriba no puede resolver — misma razón por la que se conservó
            // ix_inventario_gestion_stock_farm_item_nucleo_galpon.
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_inventario_gestion_stock_farm_item_silo
    ON public.inventario_gestion_stock (farm_id, item_inventario_ecuador_id, silo_id);
");

            // ── 4) El espejo histórico dice en qué silo pasó ────────────────────────────
            //
            // Cambio ADITIVO: la atribución de lote no se toca (sigue saliendo de
            // fn_lote_ave_engorde_id_desde_ubicacion, que es de engorde y para postura devuelve
            // NULL igual que hoy). Las empresas con el flag apagado escriben NULL en silo_id, que
            // es exactamente lo que hay hoy. Espejo actualizado en
            // backend/sql/create_lote_registro_historico_unificado.sql.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.trg_lote_hist_desde_inventario_gestion()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
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
        company_id, lote_ave_engorde_id, farm_id, nucleo_id, galpon_id, silo_id,
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
        NEW.silo_id,
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
$function$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Se devuelve el índice a su forma anterior ANTES de soltar la columna: al revés,
            // Postgres rechazaría el DROP COLUMN por la dependencia del índice.
            migrationBuilder.Sql(@"
DROP INDEX IF EXISTS ux_inventario_gestion_stock_clave_natural;
DROP INDEX IF EXISTS ix_inventario_gestion_stock_farm_item_silo;

CREATE UNIQUE INDEX IF NOT EXISTS ux_inventario_gestion_stock_clave_natural
    ON public.inventario_gestion_stock
    (farm_id, item_inventario_ecuador_id, COALESCE(nucleo_id, ''), COALESCE(galpon_id, ''));

ALTER TABLE public.inventario_gestion_stock          DROP CONSTRAINT IF EXISTS fk_igs_silo;
ALTER TABLE public.inventario_gestion_movimiento     DROP CONSTRAINT IF EXISTS fk_igm_silo;
ALTER TABLE public.inventario_gestion_movimiento     DROP CONSTRAINT IF EXISTS fk_igm_from_silo;

ALTER TABLE public.inventario_gestion_stock          DROP COLUMN IF EXISTS silo_id;
ALTER TABLE public.inventario_gestion_movimiento     DROP COLUMN IF EXISTS silo_id;
ALTER TABLE public.inventario_gestion_movimiento     DROP COLUMN IF EXISTS from_silo_id;
ALTER TABLE public.lote_registro_historico_unificado DROP COLUMN IF EXISTS silo_id;
");

            // El trigger vuelve a su versión sin silo_id.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.trg_lote_hist_desde_inventario_gestion()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
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
        NEW.company_id, v_lote, NEW.farm_id, NEW.nucleo_id, NEW.galpon_id,
        (NEW.created_at AT TIME ZONE 'UTC')::DATE, v_tipo,
        'inventario_gestion_movimiento', NEW.id, NEW.movement_type,
        NEW.item_inventario_ecuador_id, v_item_txt, NEW.quantity, NEW.unit,
        NEW.reference, NULL, NULL, COALESCE(NEW.para_proximo_ciclo, FALSE)
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
$function$;
");
        }
    }
}
