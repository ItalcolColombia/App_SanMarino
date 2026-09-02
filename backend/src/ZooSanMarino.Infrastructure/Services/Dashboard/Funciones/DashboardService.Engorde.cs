// Dashboard/Funciones/DashboardService.Engorde.cs — panel de pollo engorde.

using Npgsql;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Dashboard;

namespace ZooSanMarino.Infrastructure.Services;

public partial class DashboardService
{
    /// <inheritdoc />
    public async Task<DashboardEngordeDto> GetEngordeAsync(
        DateOnly? desde, DateOnly? hasta, CancellationToken ct = default)
    {
        // Corte del servidor: sin el módulo en el menú no hay datos. Ocultar no es proteger.
        if (!await TieneModuloAsync(DashboardCalculos.ModulosPanel.Engorde, ct))
            return DashboardEngordeDto.Vacio();

        var alcance = await ResolverAlcanceAsync(ct);
        if (alcance.Vacio) return DashboardEngordeDto.Vacio();

        var periodo = SanearPeriodo(desde, hasta);

        // ⚠️ El alcance de engorde va por GALPÓN/NÚCLEO (segundo argumento en null):
        // `lote_ave_engorde` no tiene FK a `lotes`, así que su id no pertenece al espacio de
        // `p_scope_lotes`. Cruzarlos daría acceso por casualidad cuando los números coincidan.
        //
        // El peso es un PROMEDIO SIMPLE de lo cargado ese día, no ponderado por aves: ponderarlo
        // exige el saldo de aves, que es de fn_seguimiento_diario_engorde. La pantalla lo dice.
        var sql = $@"
WITH lotes_alcance AS (
    SELECT e.lote_ave_engorde_id
    FROM public.lote_ave_engorde e
    WHERE e.company_id = @p_company_id AND e.deleted_at IS NULL
      AND e.lote_ave_engorde_id IS NOT NULL
      AND {PredicadoAlcance("e", null)}
),
diario AS (
    SELECT (s.fecha AT TIME ZONE 'UTC')::date AS dia,
           SUM(COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0))::numeric AS muertas,
           SUM(COALESCE(s.consumo_kg_hembras, 0) + COALESCE(s.consumo_kg_machos, 0))::numeric AS consumo,
           AVG(NULLIF(COALESCE(s.peso_prom_hembras, 0) + COALESCE(s.peso_prom_machos, 0), 0))::numeric AS peso
    FROM public.seguimiento_diario_aves_engorde s
    JOIN lotes_alcance la ON la.lote_ave_engorde_id = s.lote_ave_engorde_id
    WHERE (s.fecha AT TIME ZONE 'UTC')::date BETWEEN @p_desde AND @p_hasta
    GROUP BY 1
),
por_granja AS (
    SELECT f.name AS etiqueta, count(*)::numeric AS valor
    FROM public.lote_ave_engorde e
    JOIN public.farms f ON f.id = e.granja_id
    WHERE e.company_id = @p_company_id AND e.deleted_at IS NULL
      AND lower(COALESCE(e.estado_operativo_lote, '')) NOT IN ('cerrado', 'cerrada')
      AND {PredicadoAlcance("e", null)}
    GROUP BY f.name
)
SELECT jsonb_build_object(
  'mortalidadDiaria', COALESCE((SELECT jsonb_agg(jsonb_build_object(
        'fecha', to_char(d.dia, 'YYYY-MM-DD'), 'valor', d.muertas) ORDER BY d.dia)
      FROM diario d), '[]'::jsonb),
  'consumoDiarioKg', COALESCE((SELECT jsonb_agg(jsonb_build_object(
        'fecha', to_char(d.dia, 'YYYY-MM-DD'), 'valor', round(d.consumo, 2)) ORDER BY d.dia)
      FROM diario d), '[]'::jsonb),
  -- Un día sin ningún peso cargado NO entra en la serie: sale como hueco, no como cero.
  'pesoPromedioDiario', COALESCE((SELECT jsonb_agg(jsonb_build_object(
        'fecha', to_char(d.dia, 'YYYY-MM-DD'), 'valor', round(d.peso, 1)) ORDER BY d.dia)
      FROM diario d WHERE d.peso IS NOT NULL), '[]'::jsonb),
  'lotesPorGranja', COALESCE((SELECT jsonb_agg(jsonb_build_object(
        'etiqueta', g.etiqueta, 'valor', g.valor) ORDER BY g.valor DESC, g.etiqueta)
      FROM por_granja g), '[]'::jsonb),
  'totalMortalidad', COALESCE((SELECT SUM(d.muertas) FROM diario d), 0),
  'totalConsumoKg',  COALESCE((SELECT round(SUM(d.consumo), 2) FROM diario d), 0),
  'diasConRegistro', (SELECT count(*) FROM diario)
)::text AS ""Value""";

        var parametros = new List<NpgsqlParameter>(ParametrosAlcance(alcance));
        parametros.AddRange(ParametrosPeriodo(periodo));

        var dto = await ConsultarJsonAsync<EngordeJson>(sql, parametros.ToArray(), ct);
        if (dto is null) return DashboardEngordeDto.Vacio();

        return new DashboardEngordeDto(
            MortalidadDiaria: OVacia(dto.MortalidadDiaria),
            ConsumoDiarioKg: OVacia(dto.ConsumoDiarioKg),
            PesoPromedioDiario: OVacia(dto.PesoPromedioDiario),
            LotesPorGranja: OVacia(dto.LotesPorGranja),
            TotalMortalidad: dto.TotalMortalidad,
            TotalConsumoKg: dto.TotalConsumoKg,
            DiasConRegistro: dto.DiasConRegistro);
    }

    /// <summary>Forma intermedia del jsonb del panel.</summary>
    private sealed class EngordeJson
    {
        public List<PuntoDiaDto>? MortalidadDiaria { get; set; }
        public List<PuntoDiaDto>? ConsumoDiarioKg { get; set; }
        public List<PuntoDiaDto>? PesoPromedioDiario { get; set; }
        public List<CategoriaDto>? LotesPorGranja { get; set; }
        public decimal TotalMortalidad { get; set; }
        public decimal TotalConsumoKg { get; set; }
        public int DiasConRegistro { get; set; }
    }
}
