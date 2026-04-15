using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations;

/// <summary>
/// Rellena / alinea espejo_huevo_produccion para lotes LPP con datos en produccion_diaria
/// o movimientos en traslado_huevos, restando traslados Completado (misma lógica que EspejoHuevoProduccionSyncService).
/// Incluye traslados con lote_id 'LPP-{id}' cuando lote_postura_produccion_id es null.
/// </summary>
public partial class BackfillEspejoHuevoProduccionData : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
WITH prod AS (
  SELECT
    pd.lote_postura_produccion_id AS lpp_id,
    COALESCE(SUM(pd.huevo_tot), 0)::bigint AS p_tot,
    COALESCE(SUM(pd.huevo_inc), 0)::bigint AS p_inc,
    COALESCE(SUM(pd.huevo_limpio), 0)::bigint AS p_limpio,
    COALESCE(SUM(pd.huevo_tratado), 0)::bigint AS p_trat,
    COALESCE(SUM(pd.huevo_sucio), 0)::bigint AS p_sucio,
    COALESCE(SUM(pd.huevo_deforme), 0)::bigint AS p_def,
    COALESCE(SUM(pd.huevo_blanco), 0)::bigint AS p_blanco,
    COALESCE(SUM(pd.huevo_doble_yema), 0)::bigint AS p_dy,
    COALESCE(SUM(pd.huevo_piso), 0)::bigint AS p_piso,
    COALESCE(SUM(pd.huevo_pequeno), 0)::bigint AS p_peq,
    COALESCE(SUM(pd.huevo_roto), 0)::bigint AS p_roto,
    COALESCE(SUM(pd.huevo_desecho), 0)::bigint AS p_des,
    COALESCE(SUM(pd.huevo_otro), 0)::bigint AS p_otro
  FROM public.produccion_diaria pd
  WHERE pd.lote_postura_produccion_id IS NOT NULL
  GROUP BY pd.lote_postura_produccion_id
),
mov_raw AS (
  SELECT
    COALESCE(
      t.lote_postura_produccion_id,
      CASE
        WHEN t.lote_id ~ '^LPP-[0-9]+$' THEN (regexp_match(t.lote_id, '^LPP-([0-9]+)$'))[1]::int
        ELSE NULL
      END
    ) AS lpp_id,
    t.cantidad_limpio,
    t.cantidad_tratado,
    t.cantidad_sucio,
    t.cantidad_deforme,
    t.cantidad_blanco,
    t.cantidad_doble_yema,
    t.cantidad_piso,
    t.cantidad_pequeno,
    t.cantidad_roto,
    t.cantidad_desecho,
    t.cantidad_otro
  FROM public.traslado_huevos t
  WHERE t.deleted_at IS NULL
    AND t.estado = 'Completado'
),
mov AS (
  SELECT
    lpp_id,
    COALESCE(SUM(cantidad_limpio), 0)::bigint AS t_limpio,
    COALESCE(SUM(cantidad_tratado), 0)::bigint AS t_trat,
    COALESCE(SUM(cantidad_sucio), 0)::bigint AS t_sucio,
    COALESCE(SUM(cantidad_deforme), 0)::bigint AS t_def,
    COALESCE(SUM(cantidad_blanco), 0)::bigint AS t_blanco,
    COALESCE(SUM(cantidad_doble_yema), 0)::bigint AS t_dy,
    COALESCE(SUM(cantidad_piso), 0)::bigint AS t_piso,
    COALESCE(SUM(cantidad_pequeno), 0)::bigint AS t_peq,
    COALESCE(SUM(cantidad_roto), 0)::bigint AS t_roto,
    COALESCE(SUM(cantidad_desecho), 0)::bigint AS t_des,
    COALESCE(SUM(cantidad_otro), 0)::bigint AS t_otro
  FROM mov_raw
  WHERE lpp_id IS NOT NULL
  GROUP BY lpp_id
),
targets AS (
  SELECT lpp_id FROM prod
  UNION
  SELECT lpp_id FROM mov
),
bf AS (
  SELECT
    lpp.lote_postura_produccion_id,
    lpp.company_id,
    LEAST(2147483647::bigint, GREATEST(0::bigint, COALESCE(p.p_tot, 0)))::int AS h_tot,
    LEAST(2147483647::bigint, GREATEST(0::bigint, COALESCE(p.p_inc, 0)))::int AS h_inc,
    LEAST(2147483647::bigint, GREATEST(0::bigint, COALESCE(p.p_limpio, 0)))::int AS h_limpio,
    LEAST(2147483647::bigint, GREATEST(0::bigint, COALESCE(p.p_trat, 0)))::int AS h_trat,
    LEAST(2147483647::bigint, GREATEST(0::bigint, COALESCE(p.p_sucio, 0)))::int AS h_sucio,
    LEAST(2147483647::bigint, GREATEST(0::bigint, COALESCE(p.p_def, 0)))::int AS h_def,
    LEAST(2147483647::bigint, GREATEST(0::bigint, COALESCE(p.p_blanco, 0)))::int AS h_blanco,
    LEAST(2147483647::bigint, GREATEST(0::bigint, COALESCE(p.p_dy, 0)))::int AS h_dy,
    LEAST(2147483647::bigint, GREATEST(0::bigint, COALESCE(p.p_piso, 0)))::int AS h_piso,
    LEAST(2147483647::bigint, GREATEST(0::bigint, COALESCE(p.p_peq, 0)))::int AS h_peq,
    LEAST(2147483647::bigint, GREATEST(0::bigint, COALESCE(p.p_roto, 0)))::int AS h_roto,
    LEAST(2147483647::bigint, GREATEST(0::bigint, COALESCE(p.p_des, 0)))::int AS h_des,
    LEAST(2147483647::bigint, GREATEST(0::bigint, COALESCE(p.p_otro, 0)))::int AS h_otro,
    COALESCE(m.t_limpio, 0) + COALESCE(m.t_trat, 0) + COALESCE(m.t_sucio, 0) + COALESCE(m.t_def, 0)
      + COALESCE(m.t_blanco, 0) + COALESCE(m.t_dy, 0) + COALESCE(m.t_piso, 0) + COALESCE(m.t_peq, 0)
      + COALESCE(m.t_roto, 0) + COALESCE(m.t_des, 0) + COALESCE(m.t_otro, 0) AS mov_tot,
    COALESCE(m.t_limpio, 0) + COALESCE(m.t_trat, 0) AS mov_inc,
    COALESCE(m.t_limpio, 0) AS t_limpio,
    COALESCE(m.t_trat, 0) AS t_trat,
    COALESCE(m.t_sucio, 0) AS t_sucio,
    COALESCE(m.t_def, 0) AS t_def,
    COALESCE(m.t_blanco, 0) AS t_blanco,
    COALESCE(m.t_dy, 0) AS t_dy,
    COALESCE(m.t_piso, 0) AS t_piso,
    COALESCE(m.t_peq, 0) AS t_peq,
    COALESCE(m.t_roto, 0) AS t_roto,
    COALESCE(m.t_des, 0) AS t_des,
    COALESCE(m.t_otro, 0) AS t_otro,
    COALESCE(p.p_tot, 0) AS pb_tot,
    COALESCE(p.p_inc, 0) AS pb_inc,
    COALESCE(p.p_limpio, 0) AS pb_limpio,
    COALESCE(p.p_trat, 0) AS pb_trat,
    COALESCE(p.p_sucio, 0) AS pb_sucio,
    COALESCE(p.p_def, 0) AS pb_def,
    COALESCE(p.p_blanco, 0) AS pb_blanco,
    COALESCE(p.p_dy, 0) AS pb_dy,
    COALESCE(p.p_piso, 0) AS pb_piso,
    COALESCE(p.p_peq, 0) AS pb_peq,
    COALESCE(p.p_roto, 0) AS pb_roto,
    COALESCE(p.p_des, 0) AS pb_des,
    COALESCE(p.p_otro, 0) AS pb_otro
  FROM targets tg
  INNER JOIN public.lote_postura_produccion lpp
    ON lpp.lote_postura_produccion_id = tg.lpp_id
   AND lpp.deleted_at IS NULL
  LEFT JOIN prod p ON p.lpp_id = tg.lpp_id
  LEFT JOIN mov m ON m.lpp_id = tg.lpp_id
)
INSERT INTO public.espejo_huevo_produccion (
  lote_postura_produccion_id,
  company_id,
  huevo_tot_historico,
  huevo_inc_historico,
  huevo_limpio_historico,
  huevo_tratado_historico,
  huevo_sucio_historico,
  huevo_deforme_historico,
  huevo_blanco_historico,
  huevo_doble_yema_historico,
  huevo_piso_historico,
  huevo_pequeno_historico,
  huevo_roto_historico,
  huevo_desecho_historico,
  huevo_otro_historico,
  huevo_tot_dinamico,
  huevo_inc_dinamico,
  huevo_limpio_dinamico,
  huevo_tratado_dinamico,
  huevo_sucio_dinamico,
  huevo_deforme_dinamico,
  huevo_blanco_dinamico,
  huevo_doble_yema_dinamico,
  huevo_piso_dinamico,
  huevo_pequeno_dinamico,
  huevo_roto_dinamico,
  huevo_desecho_dinamico,
  huevo_otro_dinamico,
  created_at,
  updated_at
)
SELECT
  bf.lote_postura_produccion_id,
  bf.company_id,
  bf.h_tot,
  bf.h_inc,
  bf.h_limpio,
  bf.h_trat,
  bf.h_sucio,
  bf.h_def,
  bf.h_blanco,
  bf.h_dy,
  bf.h_piso,
  bf.h_peq,
  bf.h_roto,
  bf.h_des,
  bf.h_otro,
  LEAST(2147483647::bigint, GREATEST(0::bigint, bf.pb_tot - bf.mov_tot))::int,
  LEAST(2147483647::bigint, GREATEST(0::bigint, bf.pb_inc - bf.mov_inc))::int,
  LEAST(2147483647::bigint, GREATEST(0::bigint, bf.pb_limpio - bf.t_limpio))::int,
  LEAST(2147483647::bigint, GREATEST(0::bigint, bf.pb_trat - bf.t_trat))::int,
  LEAST(2147483647::bigint, GREATEST(0::bigint, bf.pb_sucio - bf.t_sucio))::int,
  LEAST(2147483647::bigint, GREATEST(0::bigint, bf.pb_def - bf.t_def))::int,
  LEAST(2147483647::bigint, GREATEST(0::bigint, bf.pb_blanco - bf.t_blanco))::int,
  LEAST(2147483647::bigint, GREATEST(0::bigint, bf.pb_dy - bf.t_dy))::int,
  LEAST(2147483647::bigint, GREATEST(0::bigint, bf.pb_piso - bf.t_piso))::int,
  LEAST(2147483647::bigint, GREATEST(0::bigint, bf.pb_peq - bf.t_peq))::int,
  LEAST(2147483647::bigint, GREATEST(0::bigint, bf.pb_roto - bf.t_roto))::int,
  LEAST(2147483647::bigint, GREATEST(0::bigint, bf.pb_des - bf.t_des))::int,
  LEAST(2147483647::bigint, GREATEST(0::bigint, bf.pb_otro - bf.t_otro))::int,
  timezone('utc', now()),
  timezone('utc', now())
FROM bf
ON CONFLICT (lote_postura_produccion_id) DO UPDATE SET
  company_id = EXCLUDED.company_id,
  huevo_tot_historico = EXCLUDED.huevo_tot_historico,
  huevo_inc_historico = EXCLUDED.huevo_inc_historico,
  huevo_limpio_historico = EXCLUDED.huevo_limpio_historico,
  huevo_tratado_historico = EXCLUDED.huevo_tratado_historico,
  huevo_sucio_historico = EXCLUDED.huevo_sucio_historico,
  huevo_deforme_historico = EXCLUDED.huevo_deforme_historico,
  huevo_blanco_historico = EXCLUDED.huevo_blanco_historico,
  huevo_doble_yema_historico = EXCLUDED.huevo_doble_yema_historico,
  huevo_piso_historico = EXCLUDED.huevo_piso_historico,
  huevo_pequeno_historico = EXCLUDED.huevo_pequeno_historico,
  huevo_roto_historico = EXCLUDED.huevo_roto_historico,
  huevo_desecho_historico = EXCLUDED.huevo_desecho_historico,
  huevo_otro_historico = EXCLUDED.huevo_otro_historico,
  huevo_tot_dinamico = EXCLUDED.huevo_tot_dinamico,
  huevo_inc_dinamico = EXCLUDED.huevo_inc_dinamico,
  huevo_limpio_dinamico = EXCLUDED.huevo_limpio_dinamico,
  huevo_tratado_dinamico = EXCLUDED.huevo_tratado_dinamico,
  huevo_sucio_dinamico = EXCLUDED.huevo_sucio_dinamico,
  huevo_deforme_dinamico = EXCLUDED.huevo_deforme_dinamico,
  huevo_blanco_dinamico = EXCLUDED.huevo_blanco_dinamico,
  huevo_doble_yema_dinamico = EXCLUDED.huevo_doble_yema_dinamico,
  huevo_piso_dinamico = EXCLUDED.huevo_piso_dinamico,
  huevo_pequeno_dinamico = EXCLUDED.huevo_pequeno_dinamico,
  huevo_roto_dinamico = EXCLUDED.huevo_roto_dinamico,
  huevo_desecho_dinamico = EXCLUDED.huevo_desecho_dinamico,
  huevo_otro_dinamico = EXCLUDED.huevo_otro_dinamico,
  updated_at = EXCLUDED.updated_at;
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Datos de backfill; no revertir automáticamente.
    }
}
