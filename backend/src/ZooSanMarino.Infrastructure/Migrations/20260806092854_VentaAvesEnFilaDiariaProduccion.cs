using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// La venta de aves de PRODUCCIÓN deja su cantidad en la fila diaria.
    ///
    /// <para>
    /// Levante ya guardaba la venta en <c>seguimiento_diario_levante.venta_aves_cantidad</c>.
    /// Producción solo dejaba una nota de texto en <c>observaciones</c>, así que la venta era
    /// invisible para la grilla diaria y para cualquier reporte que reconstruyera el saldo desde
    /// esas filas: el saldo quedaba por encima del real en exactamente el total vendido
    /// (medido en el lote S-369: +114 hembras en un sublote y +224 en el otro).
    /// </para>
    ///
    /// <para>
    /// El split es por SEXO porque el saldo de producción se lleva por sexo — levante usa una sola
    /// columna sumada y por eso no sirve como modelo acá. El dueño del número sigue siendo
    /// <c>movimiento_aves</c>; estas columnas son su espejo denormalizado en la fila del día, y el
    /// backfill de abajo las deja alineadas con lo que ya está cargado.
    /// </para>
    ///
    /// Idempotente: <c>ADD COLUMN IF NOT EXISTS</c> y un backfill que solo toca filas en cero.
    /// </summary>
    public partial class VentaAvesEnFilaDiariaProduccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.seguimiento_diario_produccion
    ADD COLUMN IF NOT EXISTS venta_aves_hembras integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS venta_aves_machos  integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS venta_aves_motivo  text;
");

            // Backfill: las ventas ya registradas en movimiento_aves se copian a la fila del día.
            // Solo se tocan filas que están en cero, así que reejecutarla no duplica nada.
            migrationBuilder.Sql(@"
WITH ventas AS (
    SELECT m.lote_origen_id                                       AS lote_id,
           (m.fecha_movimiento AT TIME ZONE 'UTC')::date          AS dia,
           SUM(COALESCE(m.cantidad_hembras, 0))::int              AS h,
           SUM(COALESCE(m.cantidad_machos, 0))::int               AS m,
           string_agg(DISTINCT NULLIF(btrim(m.motivo_movimiento), ''), ' | ') AS motivo
      FROM public.movimiento_aves m
     WHERE m.tipo_movimiento = 'Venta'
       AND m.estado <> 'Cancelado'
       AND m.deleted_at IS NULL
       AND m.lote_origen_id IS NOT NULL
     GROUP BY 1, 2
)
UPDATE public.seguimiento_diario_produccion s
   SET venta_aves_hembras = v.h,
       venta_aves_machos  = v.m,
       venta_aves_motivo  = COALESCE(s.venta_aves_motivo, v.motivo)
  FROM ventas v
 WHERE s.lote_id = v.lote_id
   AND (s.fecha_registro AT TIME ZONE 'UTC')::date = v.dia
   AND s.deleted_at IS NULL
   AND COALESCE(s.venta_aves_hembras, 0) = 0
   AND COALESCE(s.venta_aves_machos, 0) = 0
   AND (v.h > 0 OR v.m > 0);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.seguimiento_diario_produccion
    DROP COLUMN IF EXISTS venta_aves_hembras,
    DROP COLUMN IF EXISTS venta_aves_machos,
    DROP COLUMN IF EXISTS venta_aves_motivo;
");
        }
    }
}
