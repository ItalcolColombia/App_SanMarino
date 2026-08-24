using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Chequeos de existencia de guía genética compartidos entre <see cref="GuiaGeneticaService"/> y
/// <see cref="LoteService"/> (que valida raza/año al crear/editar un lote sin pasar por ese
/// servicio). Misma regla en los dos lados: primero <c>guia_genetica_santa_reyes</c> (tabla
/// dedicada); si la empresa no tiene filas ahí, cae a <c>guia_genetica_sanmarino_colombia</c>
/// (<c>ProduccionAvicolaRaw</c>), el comportamiento de siempre.
/// </summary>
public static class GuiaGeneticaLookup
{
    /// <summary>¿La empresa tiene alguna fila de guía genética cargada, en cualquiera de las dos tablas?</summary>
    public static async Task<bool> TieneGuiaAsync(ZooSanMarinoContext ctx, int companyId)
    {
        var enPropia = await ctx.GuiaGeneticaSantaReyes
            .AsNoTracking()
            .AnyAsync(g => g.CompanyId == companyId && g.DeletedAt == null);
        if (enPropia) return true;

        return await ctx.ProduccionAvicolaRaw
            .AsNoTracking()
            .AnyAsync(p => p.CompanyId == companyId && p.DeletedAt == null);
    }

    /// <summary>¿Existe la combinación (raza, año) para la empresa, en cualquiera de las dos tablas?</summary>
    public static async Task<bool> ExisteAsync(ZooSanMarinoContext ctx, int companyId, string razaNorm, string anio)
    {
        // Alias SOLO para la guía propia (ver RazaGuiaAliasCalculos): la consulta a la compartida
        // sigue usando razaNorm tal cual, para no cambiarle el resultado a ninguna otra empresa.
        var razaPropia = RazaGuiaAliasCalculos.AliasGuiaPropia(razaNorm);

        var enPropia = await ctx.GuiaGeneticaSantaReyes
            .AsNoTracking()
            .AnyAsync(g =>
                g.CompanyId == companyId &&
                g.DeletedAt == null &&
                g.Raza.Trim().ToLower() == razaPropia &&
                g.AnioGuia.Trim() == anio);
        if (enPropia) return true;

        return await ctx.ProduccionAvicolaRaw
            .AsNoTracking()
            .AnyAsync(p =>
                p.CompanyId == companyId &&
                p.DeletedAt == null &&
                p.Raza != null &&
                p.AnioGuia != null &&
                EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm) &&
                p.AnioGuia.Trim() == anio);
    }

    /// <summary>
    /// Filas de la guía <b>PROPIA</b> de la empresa (<c>guia_genetica_santa_reyes</c>), con la forma
    /// de <c>ProduccionAvicolaRaw</c>. <b>Lista vacía</b> si la empresa no tiene guía propia — que
    /// es el caso de todas menos Santa Reyes.
    ///
    /// <para>
    /// <b>Para qué existe separada de <see cref="ObtenerFilasCompatiblesAsync"/>.</b> Los reportes
    /// técnicos (<c>ReporteTecnicoService</c>, <c>ReporteTecnicoProduccionService</c>) traen la guía
    /// con consultas propias a <c>ProduccionAvicolaRaw</c> que <b>no son todas iguales entre sí</b>:
    /// unas filtran <c>deleted_at</c> y otras no, unas usan <c>LIKE</c> y otras <c>=</c>. Sustituirlas
    /// por una consulta unificada las cambiaría a todas para Sanmarino, Panamá y Ecuador, que hoy no
    /// tienen guía propia y no deberían notar absolutamente nada. Con este método el reporte pregunta
    /// primero por la guía propia y, si no hay, corre <b>su</b> consulta de siempre, sin tocarla:
    /// delta cero para quien no tiene guía propia, por construcción y no por revisión.
    /// </para>
    /// </summary>
    public static async Task<List<ProduccionAvicolaRaw>> ObtenerFilasPropiasAsync(
        ZooSanMarinoContext ctx, int companyId, string razaNorm, string anio,
        CancellationToken ct = default)
    {
        var razaPropia = RazaGuiaAliasCalculos.AliasGuiaPropia(razaNorm);

        var propias = await ctx.GuiaGeneticaSantaReyes
            .AsNoTracking()
            .Where(g =>
                g.CompanyId == companyId &&
                g.DeletedAt == null &&
                g.Raza.Trim().ToLower() == razaPropia &&
                g.AnioGuia.Trim() == anio)
            .ToListAsync(ct);

        return propias.Select(ATransitoria).ToList();
    }

    /// <summary>
    /// Filas de guía de una raza+año, con la MISMA forma que devolvía siempre
    /// <c>ProduccionAvicolaRaw</c> (para no tocar el resto de liquidaciones/reportes que ya
    /// consumen esa forma). Si la empresa tiene la guía en la tabla dedicada, arma filas
    /// transitorias (no se agregan al <see cref="ZooSanMarinoContext"/>, no se guardan) con los
    /// campos que esa tabla SÍ tiene — <c>peso_h</c>, <c>uniformidad</c> y <c>cons_ac_h</c> quedan
    /// <c>null</c> porque el Excel de origen no los trae, igual que ya pasa hoy con cualquier fila
    /// incompleta de la guía compartida.
    /// </summary>
    public static async Task<List<ProduccionAvicolaRaw>> ObtenerFilasCompatiblesAsync(
        ZooSanMarinoContext ctx, int companyId, string razaNorm, string anio)
    {
        var propias = await ObtenerFilasPropiasAsync(ctx, companyId, razaNorm, anio);
        if (propias.Count > 0) return propias;

        return await ctx.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(p =>
                p.CompanyId == companyId &&
                p.DeletedAt == null &&
                p.Raza != null && p.Raza.Trim().ToLower() == razaNorm &&
                p.AnioGuia != null && p.AnioGuia.Trim() == anio)
            .ToListAsync();
    }

    /// <summary>
    /// Proyección de una fila de la guía propia a la forma de <c>ProduccionAvicolaRaw</c>. La fila
    /// es TRANSITORIA: no se agrega al contexto y no se guarda nunca.
    /// </summary>
    private static ProduccionAvicolaRaw ATransitoria(GuiaGeneticaSantaReyes g) => new()
    {
        Id = -g.Id, // negativo: nunca colisiona con un id real, y deja claro en un debug que es transitorio
        CompanyId = g.CompanyId,
        Raza = g.Raza,
        AnioGuia = g.AnioGuia,
        Edad = g.Edad.ToString(CultureInfo.InvariantCulture),
        ProdPorcentaje = g.ProdPorcentaje?.ToString(CultureInfo.InvariantCulture),
        RetiroAcH = g.RetiroAcH?.ToString(CultureInfo.InvariantCulture),
        GrAveDiaH = g.GrAveDiaH?.ToString(CultureInfo.InvariantCulture),
        CodigoGuiaGenetica = g.CodigoGuiaGenetica,
        CreatedByUserId = g.CreatedByUserId,
        CreatedAt = g.CreatedAt
    };
}
