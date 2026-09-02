// Dashboard/Funciones/DashboardService.Postura.cs — panel de postura (levante + producción).

using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Dashboard;

namespace ZooSanMarino.Infrastructure.Services;

public partial class DashboardService
{
    /// <inheritdoc />
    public async Task<DashboardPosturaDto> GetPosturaAsync(
        DateOnly? desde, DateOnly? hasta, CancellationToken ct = default)
    {
        var ocultaMachos = await OcultaMachosEnPosturaAsync(ct);

        // Corte del servidor: sin el módulo en el menú no hay datos, aunque el front lo pida.
        // Ocultar no es proteger.
        if (!await TieneModuloAsync(DashboardCalculos.ModulosPanel.Postura, ct))
            return DashboardPosturaDto.Vacio(ocultaMachos);

        var alcance = await ResolverAlcanceAsync(ct);
        if (alcance.Vacio) return DashboardPosturaDto.Vacio(ocultaMachos);

        var periodo = SanearPeriodo(desde, hasta);

        // Machos: la empresa que no los maneja en postura tampoco los suma. El dato existe en el
        // modelo (lo consumen saldos e históricos de otras empresas) — acá sólo no se muestra.
        var mortalidad = ocultaMachos
            ? "s.mortalidad_hembras"
            : "s.mortalidad_hembras + s.mortalidad_machos";

        // ⚠️ La fecha se agrupa con `AT TIME ZONE 'UTC'` a propósito: `fecha_registro` es
        // timestamptz y `date_trunc` usaría la zona de la SESIÓN, que en LINQ no se puede fijar. Las
        // fechas puras del repo se anclan a mediodía UTC, así que la fecha UTC es la intencional.
        var sql = $@"
WITH lotes_alcance AS (
    SELECT l.lote_id
    FROM public.lote_postura_levante l
    WHERE l.company_id = @p_company_id AND l.deleted_at IS NULL AND l.lote_id IS NOT NULL
      AND {PredicadoAlcance("l", "lote_id")}
),
diario AS (
    SELECT (s.fecha_registro AT TIME ZONE 'UTC')::date AS dia,
           SUM({mortalidad})::numeric AS muertas,
           SUM(s.huevo_tot)::numeric  AS huevos
    FROM public.seguimiento_diario_produccion s
    JOIN lotes_alcance la ON la.lote_id = s.lote_id
    WHERE (s.fecha_registro AT TIME ZONE 'UTC')::date BETWEEN @p_desde AND @p_hasta
    GROUP BY 1
),
por_granja AS (
    SELECT f.name AS etiqueta, count(*)::numeric AS valor
    FROM public.lote_postura_levante l
    JOIN public.farms f ON f.id = l.granja_id
    WHERE l.company_id = @p_company_id AND l.deleted_at IS NULL
      AND lower(COALESCE(l.estado_cierre, '')) NOT IN ('cerrado', 'cerrada')
      AND {PredicadoAlcance("l", "lote_id")}
    GROUP BY f.name
)
SELECT jsonb_build_object(
  'mortalidadDiaria', COALESCE((SELECT jsonb_agg(jsonb_build_object(
        'fecha', to_char(d.dia, 'YYYY-MM-DD'), 'valor', d.muertas) ORDER BY d.dia)
      FROM diario d), '[]'::jsonb),
  'huevoDiario', COALESCE((SELECT jsonb_agg(jsonb_build_object(
        'fecha', to_char(d.dia, 'YYYY-MM-DD'), 'valor', d.huevos) ORDER BY d.dia)
      FROM diario d), '[]'::jsonb),
  'lotesPorGranja', COALESCE((SELECT jsonb_agg(jsonb_build_object(
        'etiqueta', g.etiqueta, 'valor', g.valor) ORDER BY g.valor DESC, g.etiqueta)
      FROM por_granja g), '[]'::jsonb),
  'totalMortalidad', COALESCE((SELECT SUM(d.muertas) FROM diario d), 0),
  'totalHuevo',      COALESCE((SELECT SUM(d.huevos)  FROM diario d), 0),
  'diasConRegistro', (SELECT count(*) FROM diario)
)::text AS ""Value""";

        var parametros = new List<NpgsqlParameter>(ParametrosAlcance(alcance));
        parametros.AddRange(ParametrosPeriodo(periodo));

        var dto = await ConsultarJsonAsync<PosturaJson>(sql, parametros.ToArray(), ct);
        if (dto is null) return DashboardPosturaDto.Vacio(ocultaMachos);

        return new DashboardPosturaDto(
            MortalidadDiaria: OVacia(dto.MortalidadDiaria),
            HuevoDiario: OVacia(dto.HuevoDiario),
            LotesPorGranja: OVacia(dto.LotesPorGranja),
            TotalMortalidad: dto.TotalMortalidad,
            TotalHuevo: dto.TotalHuevo,
            DiasConRegistro: dto.DiasConRegistro,
            OcultaMachos: ocultaMachos);
    }

    /// <summary>
    /// Flag <c>oculta_machos_en_postura</c> de la empresa activa. Fail-closed: ante cualquier duda
    /// (sin empresa, sin fila) responde <c>false</c>, que es el comportamiento clásico — mostrar la
    /// columna de machos, como siempre hicieron todas las empresas menos Santa Reyes.
    /// </summary>
    private async Task<bool> OcultaMachosEnPosturaAsync(CancellationToken ct)
    {
        var companyId = _current.CompanyId;
        if (companyId <= 0) return false;

        return await _ctx.Companies
            .AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => c.OcultaMachosEnPostura)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Forma intermedia del jsonb del panel.</summary>
    private sealed class PosturaJson
    {
        public List<PuntoDiaDto>? MortalidadDiaria { get; set; }
        public List<PuntoDiaDto>? HuevoDiario { get; set; }
        public List<CategoriaDto>? LotesPorGranja { get; set; }
        public decimal TotalMortalidad { get; set; }
        public decimal TotalHuevo { get; set; }
        public int DiasConRegistro { get; set; }
    }
}
