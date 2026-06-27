using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAplicarCorreccionLiquidacion : Migration
    {
        // Permiso 'liquidacion.aplicar_correccion' (seed idempotente, patrón permissions(key,description))
        // + función que aplica la corrección sugerida del verificador (carga peso faltante en los
        // despachos sin peso de una corrida, distribuido por aves, auditado). Idempotente.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PERM_SQL);
            migrationBuilder.Sql(FN_SQL, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS public.fn_aplicar_correccion_despachos_sin_peso(INT, INT, TEXT, TEXT, NUMERIC, INT);", suppressTransaction: true);
            migrationBuilder.Sql(DOWN_PERM_SQL);
        }

        private const string PERM_SQL = @"INSERT INTO public.permissions (key, description)
SELECT 'liquidacion.aplicar_correccion', 'Liquidacion: aplicar correccion sugerida del verificador (cargar peso faltante de despachos sin peso)'
WHERE NOT EXISTS (SELECT 1 FROM public.permissions p WHERE p.key = 'liquidacion.aplicar_correccion');";

        private const string DOWN_PERM_SQL = @"DELETE FROM public.role_permissions WHERE permission_id IN (SELECT id FROM public.permissions WHERE key = 'liquidacion.aplicar_correccion');
DELETE FROM public.menu_permissions WHERE permission_id IN (SELECT id FROM public.permissions WHERE key = 'liquidacion.aplicar_correccion');
DELETE FROM public.permissions WHERE key = 'liquidacion.aplicar_correccion';";

        private const string FN_SQL = @"-- ============================================================================
-- fn_aplicar_correccion_despachos_sin_peso — Aplica la corrección sugerida por el
-- verificador de liquidación: carga el peso faltante en los despachos sin peso
-- (peso_neto y báscula en NULL) de una corrida, distribuyendo p_kg_total entre
-- ellos proporcional a las aves. Escribe peso_neto + peso_neto_global y audita
-- (updated_at / updated_by_user_id). NO es STABLE (modifica datos).
-- Pensada para llamarse desde un endpoint gateado por el permiso
-- 'liquidacion.aplicar_correccion'. Transaccional (un solo UPDATE).
-- ============================================================================

CREATE OR REPLACE FUNCTION public.fn_aplicar_correccion_despachos_sin_peso(
    p_company_id  INT,
    p_granja_id   INT,
    p_nucleo_id   TEXT,
    p_lote_codigo TEXT,
    p_kg_total    NUMERIC,
    p_user_id     INT
)
RETURNS JSONB
LANGUAGE plpgsql
AS $$
DECLARE
    v_lotes      INT[];
    v_total_aves INT;
    v_aplicados  JSONB;
BEGIN
    IF p_kg_total IS NULL OR p_kg_total <= 0 THEN
        RETURN jsonb_build_object('ok', false, 'error', 'El total de kg a aplicar debe ser mayor a 0.');
    END IF;

    SELECT array_agg(lote_ave_engorde_id)
      INTO v_lotes
    FROM public.lote_ave_engorde
    WHERE company_id = p_company_id
      AND granja_id  = p_granja_id
      AND (p_nucleo_id   IS NULL OR nucleo_id   = p_nucleo_id)
      AND (p_lote_codigo IS NULL OR lote_nombre LIKE p_lote_codigo || '%')
      AND deleted_at IS NULL;

    IF v_lotes IS NULL THEN
        RETURN jsonb_build_object('ok', false, 'error', 'No se encontraron lotes en el alcance indicado.');
    END IF;

    -- Aves de los despachos SIN peso (mismo criterio que el detector MOV_SIN_PESO)
    SELECT coalesce(sum(cantidad_hembras + cantidad_machos + cantidad_mixtas), 0)
      INTO v_total_aves
    FROM public.movimiento_pollo_engorde
    WHERE lote_ave_engorde_origen_id = ANY(v_lotes)
      AND estado = 'Completado' AND deleted_at IS NULL
      AND tipo_movimiento IN ('Venta','Despacho','Retiro')
      AND peso_neto IS NULL AND (peso_bruto IS NULL OR peso_tara IS NULL);

    IF v_total_aves = 0 THEN
        RETURN jsonb_build_object('ok', false,
            'error', 'No hay despachos sin peso para corregir en este alcance (puede que ya se haya aplicado).');
    END IF;

    -- Distribuir p_kg_total proporcional a las aves y escribir peso_neto (auditado)
    WITH objetivo AS (
        SELECT id, (cantidad_hembras + cantidad_machos + cantidad_mixtas) AS aves
        FROM public.movimiento_pollo_engorde
        WHERE lote_ave_engorde_origen_id = ANY(v_lotes)
          AND estado = 'Completado' AND deleted_at IS NULL
          AND tipo_movimiento IN ('Venta','Despacho','Retiro')
          AND peso_neto IS NULL AND (peso_bruto IS NULL OR peso_tara IS NULL)
    ),
    upd AS (
        UPDATE public.movimiento_pollo_engorde m
        SET peso_neto          = round((p_kg_total * o.aves / v_total_aves)::numeric, 2),
            peso_neto_global   = round((p_kg_total * o.aves / v_total_aves)::numeric, 2),
            updated_at         = now(),
            updated_by_user_id = p_user_id
        FROM objetivo o
        WHERE m.id = o.id
        RETURNING m.id, o.aves, m.peso_neto
    )
    SELECT jsonb_agg(jsonb_build_object('id', id, 'aves', aves, 'pesoAsignado', peso_neto) ORDER BY id)
      INTO v_aplicados
    FROM upd;

    RETURN jsonb_build_object(
        'ok',          true,
        'kgTotal',     p_kg_total,
        'avesTotales', v_total_aves,
        'movimientos', jsonb_array_length(v_aplicados),
        'aplicados',   v_aplicados
    );
END;
$$;
";
    }
}
