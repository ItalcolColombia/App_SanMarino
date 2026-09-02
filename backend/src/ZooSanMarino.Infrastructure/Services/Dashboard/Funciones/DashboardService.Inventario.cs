// Dashboard/Funciones/DashboardService.Inventario.cs — panel de alimento e inventario.

using Npgsql;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Dashboard;

namespace ZooSanMarino.Infrastructure.Services;

public partial class DashboardService
{
    /// <summary>
    /// Umbral en KILOS a partir del cual un descuadre se considera real.
    ///
    /// <para>No es un número elegido al azar: medido el 20-ago-2026 en Panamá, la consulta sin
    /// umbral daba <b>23 galpones</b> cuando los que tenían kilos eran <b>8</b> — los otros 15
    /// entraban con un descuadre de ~1e-11, o sea cero con ruido de coma flotante.</para>
    /// </summary>
    private const decimal UmbralDescuadreKg = 1m;

    /// <inheritdoc />
    public async Task<DashboardInventarioDto> GetInventarioAsync(CancellationToken ct = default)
    {
        // Corte del servidor: sin el módulo en el menú no hay datos. Ocultar no es proteger.
        if (!await TieneModuloAsync(DashboardCalculos.ModulosPanel.AlimentoInventario, ct))
            return DashboardInventarioDto.Vacio();

        var alcance = await ResolverAlcanceAsync(ct);
        if (alcance.Vacio) return DashboardInventarioDto.Vacio();

        // ⚠️ El stock suma SÓLO ítems de alimento. `farm_product_inventory.quantity` guarda la
        // cantidad en la unidad que manda el CATÁLOGO —hay ítems en KG, LT, GALONES y unidades—, así
        // que sumar todo daría un número sin significado. `ILIKE` porque el catálogo tiene
        // 'alimento' y 'Alimento': la capitalización duplicada es un defecto conocido del dato.
        //
        // Los descuadres salen de fn_cuadre_alimento_engorde, que es la dueña del invariante. Acá no
        // se recalcula: se agrupa por galpón y se SEPARAN las dos señales —kilos y días en rojo—,
        // que miden problemas distintos y no se suman.
        var sql = $@"
WITH stock AS (
    SELECT f.name AS etiqueta, SUM(i.quantity)::numeric AS valor
    FROM public.farm_product_inventory i
    JOIN public.farms f ON f.id = i.farm_id
    JOIN public.catalogo_items ci ON ci.id = i.catalog_item_id
    WHERE i.company_id = @p_company_id
      AND i.active = true
      AND ci.item_type ILIKE 'alimento'
      AND i.farm_id = ANY(@p_granjas)
      -- 🔴 Una granja RESTRINGIDA queda fuera del stock, a propósito. El stock vive a nivel de
      -- GRANJA (`farm_product_inventory.farm_id`), no de galpón: mostrarle el total de la granja a
      -- quien sólo tiene concedido un galpón sería contarle de más. Sin forma de recortarlo al
      -- grant, la respuesta correcta es no mostrarlo — fail-closed. El panel avisa que el alcance
      -- está recortado (`alcanceRestringido` del resumen).
      AND NOT (i.farm_id = ANY(@p_scope_farm_ids))
    GROUP BY f.name
    HAVING SUM(i.quantity) <> 0
),
cuadre AS (
    SELECT c.granja AS granja_nombre,
           c.galpon_id,
           SUM(c.descuadre_kg)::numeric      AS descuadre_kg,
           SUM(c.filas_negativas)::int       AS filas_negativas,
           count(*)::int                     AS ciclos
    FROM public.fn_cuadre_alimento_engorde(@p_company_id) c
    WHERE c.galpon_id IS NOT NULL
      AND c.granja_id = ANY(@p_granjas)
      AND (
            NOT (c.granja_id = ANY(@p_scope_farm_ids))
            OR CASE
                 WHEN COALESCE(c.galpon_id, '') <> ''
                     THEN c.galpon_id = ANY(@p_scope_galpones)
                 WHEN COALESCE(c.nucleo_id, '') <> ''
                     THEN (c.granja_id::text || '|' || c.nucleo_id)
                          = ANY(@p_scope_nucleos)
                 ELSE false
               END
          )
    GROUP BY c.granja, c.galpon_id
),
con_problema AS (
    SELECT * FROM cuadre
    WHERE abs(descuadre_kg) >= {UmbralDescuadreKg} OR filas_negativas > 0
)
SELECT jsonb_build_object(
  'stockPorGranja', COALESCE((SELECT jsonb_agg(jsonb_build_object(
        'etiqueta', s.etiqueta, 'valor', round(s.valor, 2)) ORDER BY s.valor DESC)
      FROM stock s), '[]'::jsonb),
  'descuadres', COALESCE((SELECT jsonb_agg(jsonb_build_object(
        'granjaNombre',   p.granja_nombre,
        'galponId',       p.galpon_id,
        'descuadreKg',    round(p.descuadre_kg, 2),
        'filasNegativas', p.filas_negativas,
        'ciclosDelGalpon', p.ciclos)
      ORDER BY abs(p.descuadre_kg) DESC, p.filas_negativas DESC)
      FROM con_problema p), '[]'::jsonb),
  -- Las dos señales van CONTADAS APARTE. `descuadre_kg` son kilos que faltan o sobran;
  -- `filas_negativas` son días que cerraron en rojo con el total perfecto (mal el orden o la
  -- fecha de los ingresos). Un solo número que las mezcle asusta y no dice nada.
  'galponesConKilos',      (SELECT count(*) FROM con_problema WHERE abs(descuadre_kg) >= {UmbralDescuadreKg}),
  'galponesConDiasEnRojo', (SELECT count(*) FROM con_problema
                             WHERE filas_negativas > 0 AND abs(descuadre_kg) < {UmbralDescuadreKg})
)::text AS ""Value""";

        var dto = await ConsultarJsonAsync<InventarioJson>(sql, ParametrosAlcance(alcance), ct);
        if (dto is null) return DashboardInventarioDto.Vacio();

        return new DashboardInventarioDto(
            StockPorGranja: OVacia(dto.StockPorGranja),
            Descuadres: OVacia(dto.Descuadres),
            GalponesConKilos: dto.GalponesConKilos,
            GalponesConDiasEnRojo: dto.GalponesConDiasEnRojo);
    }

    /// <summary>Forma intermedia del jsonb del panel.</summary>
    private sealed class InventarioJson
    {
        public List<CategoriaDto>? StockPorGranja { get; set; }
        public List<DescuadreGalponDto>? Descuadres { get; set; }
        public int GalponesConKilos { get; set; }
        public int GalponesConDiasEnRojo { get; set; }
    }
}
