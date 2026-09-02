// Dashboard/Funciones/DashboardService.Cumplimiento.cs — vacunación pendiente y cuadres sin resolver.

using Npgsql;
using NpgsqlTypes;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Dashboard;

namespace ZooSanMarino.Infrastructure.Services;

public partial class DashboardService
{
    /// <summary>Días hacia adelante que cuentan como «próximo a vencer». El default de la fn.</summary>
    private const int HorizonteVacunacionDias = 7;

    /// <inheritdoc />
    public async Task<DashboardCumplimientoDto> GetCumplimientoAsync(CancellationToken ct = default)
    {
        // Corte del servidor: sin ninguno de los módulos del panel en el menú, no hay datos.
        if (!await TieneModuloAsync(DashboardCalculos.ModulosPanel.Cumplimiento, ct))
            return DashboardCumplimientoDto.Vacio();

        var alcance = await ResolverAlcanceAsync(ct);
        if (alcance.Vacio) return DashboardCumplimientoDto.Vacio();

        // fn_vacunacion_pendientes se llama TAL CUAL: ya resuelve el alcance granular con los mismos
        // 4 arrays y ya clasifica la situación (Vencido / EnFranja / Proximo). Reimplementar acá el
        // conteo sería una segunda fórmula para el mismo número — la regla que el repo tiene
        // prohibido romper. Lo único que se hace es agrupar lo que devuelve.
        var sql = @"
WITH pendientes AS (
    SELECT * FROM public.fn_vacunacion_pendientes(
        @p_user_guid, @p_company_id, @p_pais_id, @p_hoy,
        @p_scope_farm_ids, @p_scope_nucleos, @p_scope_galpones, @p_scope_lotes, @p_horizonte)
    WHERE granja_id = ANY(@p_granjas)
),
por_granja AS (
    SELECT COALESCE(NULLIF(granja_nombre, ''), '(sin granja)') AS etiqueta,
           count(*)::numeric AS valor
    FROM pendientes
    GROUP BY 1
)
SELECT jsonb_build_object(
  -- 'EnFranja' cuenta como vencida a los efectos del tablero: la fecha ya llegó y no se aplicó.
  'vacunacionVencida', (SELECT count(*) FROM pendientes WHERE situacion IN ('Vencido', 'EnFranja')),
  'vacunacionProxima', (SELECT count(*) FROM pendientes WHERE situacion = 'Proximo'),
  'cuadresSinResueltos', (
      SELECT count(*) FROM public.sync_operaciones o
      WHERE o.company_id = @p_company_id
        AND o.estado = 'requiere_cuadre'
        AND o.cuadre_resuelto_at IS NULL),
  'vacunacionPorGranja', COALESCE((SELECT jsonb_agg(jsonb_build_object(
        'etiqueta', g.etiqueta, 'valor', g.valor) ORDER BY g.valor DESC, g.etiqueta)
      FROM por_granja g), '[]'::jsonb)
)::text AS ""Value""";

        var parametros = new List<NpgsqlParameter>(ParametrosAlcance(alcance))
        {
            new NpgsqlParameter("p_user_guid", NpgsqlDbType.Uuid) { Value = alcance.UserGuid },
            new NpgsqlParameter("p_pais_id", NpgsqlDbType.Integer)
            {
                Value = (object?)_current.PaisId ?? DBNull.Value
            },
            new NpgsqlParameter("p_hoy", NpgsqlDbType.Date)
            {
                Value = DateOnly.FromDateTime(DateTime.UtcNow)
            },
            new NpgsqlParameter("p_horizonte", NpgsqlDbType.Integer) { Value = HorizonteVacunacionDias }
        };

        var dto = await ConsultarJsonAsync<CumplimientoJson>(sql, parametros.ToArray(), ct);
        if (dto is null) return DashboardCumplimientoDto.Vacio();

        return new DashboardCumplimientoDto(
            VacunacionVencida: dto.VacunacionVencida,
            VacunacionProxima: dto.VacunacionProxima,
            CuadresSinResolver: dto.CuadresSinResueltos,
            VacunacionPorGranja: OVacia(dto.VacunacionPorGranja));
    }

    /// <summary>Forma intermedia del jsonb del panel.</summary>
    private sealed class CumplimientoJson
    {
        public int VacunacionVencida { get; set; }
        public int VacunacionProxima { get; set; }
        public int CuadresSinResueltos { get; set; }
        public List<CategoriaDto>? VacunacionPorGranja { get; set; }
    }
}
