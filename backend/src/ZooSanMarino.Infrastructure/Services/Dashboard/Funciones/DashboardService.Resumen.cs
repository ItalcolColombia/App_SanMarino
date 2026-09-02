// Dashboard/Funciones/DashboardService.Resumen.cs — conteos generales del alcance.
//
// Namespace PLANO, igual que el ancla: es la MISMA clase repartida en archivos.

using ZooSanMarino.Application.DTOs.Dashboard;

namespace ZooSanMarino.Infrastructure.Services;

public partial class DashboardService
{
    /// <summary>
    /// Estados que cuentan como CERRADO. Se comparan en minúsculas porque la base no es consistente:
    /// levante y engorde guardan «Cerrado» y producción guarda «Cerrada». Comparar contra un literal
    /// dejaría fuera media tabla sin un solo error — se verificó contra los datos antes de escribirlo.
    /// </summary>
    private const string SqlNoCerrado =
        "lower(COALESCE({0}, '')) NOT IN ('cerrado', 'cerrada')";

    /// <inheritdoc />
    public async Task<DashboardResumenDto> GetResumenAsync(CancellationToken ct = default)
    {
        var alcance = await ResolverAlcanceAsync(ct);
        if (alcance.Vacio) return DashboardResumenDto.Vacio(alcance.Restringido);

        var noCerradoLevante = string.Format(SqlNoCerrado, "l.estado_cierre");
        var noCerradoEngorde = string.Format(SqlNoCerrado, "e.estado_operativo_lote");

        // Postura se cuenta sobre lote_postura_levante (trae ubicación y estado de cierre) con la
        // cascada de alcance del nivel LOTE; engorde sobre su tabla, gobernado por galpón/núcleo.
        var sql = $@"
SELECT jsonb_build_object(
  'posturaTotal',   (SELECT count(*) FROM public.lote_postura_levante l
                      WHERE l.company_id = @p_company_id AND l.deleted_at IS NULL
                        AND {PredicadoAlcance("l", "lote_id")}),
  'posturaActivos', (SELECT count(*) FROM public.lote_postura_levante l
                      WHERE l.company_id = @p_company_id AND l.deleted_at IS NULL
                        AND {noCerradoLevante}
                        AND {PredicadoAlcance("l", "lote_id")}),
  'engordeTotal',   (SELECT count(*) FROM public.lote_ave_engorde e
                      WHERE e.company_id = @p_company_id AND e.deleted_at IS NULL
                        AND {PredicadoAlcance("e", null)}),
  'engordeActivos', (SELECT count(*) FROM public.lote_ave_engorde e
                      WHERE e.company_id = @p_company_id AND e.deleted_at IS NULL
                        AND {noCerradoEngorde}
                        AND {PredicadoAlcance("e", null)})
)::text AS ""Value""";

        var filas = await ConsultarJsonAsync<ConteosResumen>(sql, ParametrosAlcance(alcance), ct)
                    ?? new ConteosResumen();

        return new DashboardResumenDto(
            Granjas: alcance.Granjas.Count,
            LotesPosturaActivos: filas.PosturaActivos,
            LotesPosturaTotal: filas.PosturaTotal,
            LotesEngordeActivos: filas.EngordeActivos,
            LotesEngordeTotal: filas.EngordeTotal,
            AlcanceRestringido: alcance.Restringido,
            GeneradoAt: DateTime.UtcNow);
    }

    /// <summary>Forma intermedia del jsonb del resumen.</summary>
    private sealed class ConteosResumen
    {
        public int PosturaTotal { get; set; }
        public int PosturaActivos { get; set; }
        public int EngordeTotal { get; set; }
        public int EngordeActivos { get; set; }
    }
}
