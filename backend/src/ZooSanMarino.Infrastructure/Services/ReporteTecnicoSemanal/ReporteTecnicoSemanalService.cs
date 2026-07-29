// Ancla del servicio Reporte Técnico Semanal (Sanmarino postura).
// Convención del repo: partial class por concern en Funciones/ (Levante, Produccion);
// la interfaz va SOLO aquí; namespace PLANO ZooSanMarino.Infrastructure.Services.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.ReporteTecnicoSemanal;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Reporte Técnico Semanal (dos opciones: Levante 1-25 / Producción 25+) por
/// lote base, un tab por sublote (galpón) + consolidado, comparado contra la
/// guía genética (guia_genetica_sanmarino_colombia).
/// El cálculo semanal vive en la BD (fn_reporte_semanal_levante_extras y
/// fn_indicadores_produccion_postura); las derivadas y la consolidación en
/// <see cref="ReporteTecnicoSemanalCalculos"/> (puro, testeado).
/// </summary>
public partial class ReporteTecnicoSemanalService : IReporteTecnicoSemanalService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;
    private readonly ILocationScopeResolver _scopeResolver;

    public ReporteTecnicoSemanalService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        ICompanyResolver companyResolver,
        ILocationScopeResolver scopeResolver)
    {
        _ctx = ctx;
        _current = current;
        _companyResolver = companyResolver;
        _scopeResolver = scopeResolver;
    }

    /// <summary>Empresa efectiva: header X-Active-Company (por nombre) o claim. Fail-closed si no resuelve.</summary>
    private async Task<int> ResolveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim());
            if (byName.HasValue && byName.Value > 0) return byName.Value;
        }
        var claimId = _current.CompanyId;
        if (claimId > 0) return claimId;
        throw new InvalidOperationException(
            "No se pudo determinar la compañía activa (CompanyId). Verifique sesión o header X-Active-Company-Id.");
    }

    private async Task<LotePosturaBase> ResolverLoteBaseAsync(int lotePosturaBaseId, int companyId, CancellationToken ct)
    {
        var loteBase = await _ctx.LotePosturaBases
            .AsNoTracking()
            .FirstOrDefaultAsync(b =>
                b.LotePosturaBaseId == lotePosturaBaseId
                && b.CompanyId == companyId
                && b.DeletedAt == null, ct);

        return loteBase ?? throw new ArgumentException(
            $"No se encontró el lote base {lotePosturaBaseId}. Verifique que pertenezca a su compañía activa.");
    }

    /// <summary>
    /// Filas crudas de la guía genética de la empresa para una (raza, año),
    /// indexadas por semana (edad parseada con tolerancia). Devuelve vacío si
    /// falta raza/año o no hay guía cargada.
    /// </summary>
    /// <param name="preferirVarianteProduccion">
    /// Desempate para las semanas que tienen DOS filas en la guía.
    /// La semana 25 aparece dos veces: <c>'25'</c> (cierre de LEVANTE) y
    /// <c>'25P'</c> (arranque de PRODUCCIÓN), y ambas parsean a 25. Traen
    /// valores muy distintos —retiro acumulado hembras 4,03 vs 0,10— así que
    /// tomar la equivocada rompe la monotonía de la guía (la semana 24 va en
    /// 3,93 y la 25 caería a 0,10) y hace que el Detalle se contradiga con el
    /// Resumen, que sí desempata.
    /// <c>false</c> en levante ⇒ gana la puramente numérica;
    /// <c>true</c> en producción ⇒ gana la del sufijo.
    /// </param>
    private async Task<Dictionary<int, ProduccionAvicolaRaw>> CargarGuiaPorSemanaAsync(
        int companyId, string? raza, int? anioGuia, bool preferirVarianteProduccion, CancellationToken ct)
    {
        var resultado = new Dictionary<int, ProduccionAvicolaRaw>();
        if (string.IsNullOrWhiteSpace(raza) || !anioGuia.HasValue) return resultado;

        var razaNorm = raza.Trim().ToLower();
        var anioNorm = anioGuia.Value.ToString();

        var filas = await _ctx.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(g => g.CompanyId == companyId
                        && g.DeletedAt == null
                        && g.Raza != null && g.Raza.Trim().ToLower() == razaNorm
                        && g.AnioGuia != null && g.AnioGuia.Trim() == anioNorm)
            .ToListAsync(ct);

        // El orden de la consulta NO está garantizado: sin este desempate la
        // fila que gana depende del plan y del orden físico de la tabla.
        var ordenadas = filas
            .OrderBy(f => EsEdadPuramenteNumerica(f.Edad) != preferirVarianteProduccion ? 0 : 1)
            .ThenBy(f => f.Id);

        foreach (var fila in ordenadas)
        {
            var semana = ReporteTecnicoSemanalCalculos.ParseEdadSemana(fila.Edad);
            if (semana.HasValue && !resultado.ContainsKey(semana.Value))
                resultado[semana.Value] = fila;
        }
        return resultado;
    }

    /// <summary>true si la edad de la guía son solo dígitos ('25'), false si trae sufijo ('25P').</summary>
    private static bool EsEdadPuramenteNumerica(string? edad)
        => !string.IsNullOrWhiteSpace(edad) && edad.Trim().All(char.IsDigit);

    private sealed record NombresUbicacion(
        Dictionary<int, (string Nombre, string? Municipio)> Granjas,
        Dictionary<(string NucleoId, int GranjaId), string> Nucleos,
        Dictionary<string, string> Galpones);

    private async Task<NombresUbicacion> CargarNombresUbicacionAsync(
        IReadOnlyCollection<int> granjaIds,
        IReadOnlyCollection<string> nucleoIds,
        IReadOnlyCollection<string> galponIds,
        CancellationToken ct)
    {
        var farms = await _ctx.Farms.AsNoTracking()
            .Where(f => granjaIds.Contains(f.Id))
            .Select(f => new { f.Id, f.Name, f.MunicipioId })
            .ToListAsync(ct);

        var municipioIds = farms.Select(f => f.MunicipioId).Distinct().ToList();
        var municipios = await _ctx.Municipios.AsNoTracking()
            .Where(m => municipioIds.Contains(m.MunicipioId))
            .ToDictionaryAsync(m => m.MunicipioId, m => m.MunicipioNombre, ct);

        var granjas = farms.ToDictionary(
            f => f.Id,
            f => (f.Name, municipios.TryGetValue(f.MunicipioId, out var mn) ? mn : (string?)null));

        var nucleos = await _ctx.Nucleos.AsNoTracking()
            .Where(n => granjaIds.Contains(n.GranjaId) && nucleoIds.Contains(n.NucleoId))
            .ToDictionaryAsync(n => ValueTuple.Create(n.NucleoId, n.GranjaId), n => n.NucleoNombre, ct);

        var galpones = await _ctx.Galpones.AsNoTracking()
            .Where(g => galponIds.Contains(g.GalponId))
            .ToDictionaryAsync(g => g.GalponId, g => g.GalponNombre, ct);

        return new NombresUbicacion(granjas, nucleos, galpones);
    }

    private static void CompletarNombres(ReporteSemanalTabHeaderDto header, NombresUbicacion nombres)
    {
        if (header.GranjaId.HasValue && nombres.Granjas.TryGetValue(header.GranjaId.Value, out var g))
        {
            header.GranjaNombre = g.Nombre;
            header.Municipio = g.Municipio;
        }
        if (!string.IsNullOrWhiteSpace(header.NucleoId) && header.GranjaId.HasValue
            && nombres.Nucleos.TryGetValue((header.NucleoId!, header.GranjaId.Value), out var nn))
        {
            header.NucleoNombre = nn;
        }
        if (!string.IsNullOrWhiteSpace(header.GalponId)
            && nombres.Galpones.TryGetValue(header.GalponId!, out var gn))
        {
            header.GalponNombre = gn;
        }
    }

    /// <summary>Recorta la lista de semanas de cada tab al rango pedido (los acumulados ya vienen completos).</summary>
    private static List<T> FiltrarRango<T>(List<T> semanas, int? desde, int? hasta, Func<T, int> semanaDe)
        => semanas.Where(s => (!desde.HasValue || semanaDe(s) >= desde.Value)
                           && (!hasta.HasValue || semanaDe(s) <= hasta.Value)).ToList();
}
