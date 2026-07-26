using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Servicio de indicadores semanales de producción.
/// El CÁLCULO por semana se realiza en la BD (fn_indicadores_produccion_postura): este servicio
/// resuelve compañía y lote (LPP o legacy), determina la disponibilidad de guía genética, DELEGA
/// el cálculo a la función SQL (SqlQueryRaw) y arma la respuesta con el mismo contrato de siempre.
/// Alineado con ProduccionService en la resolución de lote y la fuente de datos.
/// </summary>
public class IndicadoresProduccionService : IIndicadoresProduccionService
{
    private readonly ZooSanMarinoContext _context;
    private readonly IGuiaGeneticaService _guiaGeneticaService;
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyResolver _companyResolver;

    public IndicadoresProduccionService(
        ZooSanMarinoContext context,
        IGuiaGeneticaService guiaGeneticaService,
        ICurrentUser currentUser,
        ICompanyResolver companyResolver)
    {
        _context = context;
        _guiaGeneticaService = guiaGeneticaService;
        _currentUser = currentUser;
        _companyResolver = companyResolver;
    }

    /// <summary>
    /// Resuelve CompanyId activo: header X-Active-Company-Id (por nombre) o claim del usuario.
    /// Misma lógica que el resto del proyecto para filtrado por compañía.
    /// </summary>
    private async Task<int> ResolveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_currentUser.ActiveCompanyName.Trim());
            if (byName.HasValue && byName.Value > 0)
                return byName.Value;
        }
        var claimId = _currentUser.CompanyId;
        if (claimId > 0)
            return claimId;
        throw new InvalidOperationException("No se pudo determinar la compañía activa (CompanyId). Verifique sesión o header X-Active-Company-Id.");
    }

    /// <summary>
    /// Contexto del lote en producción resuelto a partir del request: parámetros que se le pasan a
    /// las funciones SQL (LPP o legacy, excluyentes) y raza/año para la comparación contra guía.
    /// </summary>
    private readonly record struct LoteProduccionResuelto(
        string Raza,
        int? AnoTablaGenetica,
        int? LppParam,
        int? LoteIdParam);

    /// <summary>
    /// Resuelve el lote en producción del request (LPP prioritario, legacy por LoteId) validando
    /// existencia, pertenencia a la compañía y fecha de referencia, y devuelve raza/año + los
    /// parámetros para las funciones SQL. Lanza <see cref="ArgumentException"/> con el mismo mensaje
    /// de siempre cuando no se puede resolver.
    /// <para>Compartido por los indicadores semanales y por el desglose de clasificación de huevos
    /// por ítems, que deben resolver EXACTAMENTE el mismo lote.</para>
    /// </summary>
    private async Task<LoteProduccionResuelto> ResolverLoteProduccionAsync(
        IndicadoresProduccionRequest request, int companyId)
    {
        string raza;
        int? anoTablaGenetica;
        int? lppParam = null;
        int? loteIdParam = null;

        if (request.LotePosturaProduccionId.HasValue && request.LotePosturaProduccionId.Value > 0)
        {
            var lppId = request.LotePosturaProduccionId.Value;
            var lpp = await _context.LotePosturaProduccion
                .AsNoTracking()
                .FirstOrDefaultAsync(l =>
                    l.LotePosturaProduccionId == lppId
                    && l.CompanyId == companyId
                    && l.DeletedAt == null);

            if (lpp == null)
            {
                throw new ArgumentException(
                    $"No se encontró lote postura producción {lppId}. " +
                    "Verifique que pertenezca a su compañía activa.");
            }

            // Validar que exista fecha de referencia (encaset/producción), igual que antes.
            DateTime? fechaRef = null;
            if (lpp.LotePosturaLevanteId.HasValue)
            {
                fechaRef = await _context.LotePosturaLevante
                    .AsNoTracking()
                    .Where(x => x.LotePosturaLevanteId == lpp.LotePosturaLevanteId && x.DeletedAt == null)
                    .Select(x => x.FechaEncaset)
                    .FirstOrDefaultAsync();
            }
            if (!fechaRef.HasValue && lpp.FechaEncaset.HasValue)
                fechaRef = lpp.FechaEncaset;
            if (!fechaRef.HasValue && lpp.FechaInicioProduccion.HasValue)
                fechaRef = lpp.FechaInicioProduccion;

            if (!fechaRef.HasValue)
            {
                throw new ArgumentException(
                    $"El lote postura producción {lppId} no tiene fecha de inicio de producción ni fecha de encaset. " +
                    "Necesaria para calcular semanas.");
            }

            raza = lpp.Raza ?? "";
            anoTablaGenetica = lpp.AnoTablaGenetica;
            lppParam = lppId;
        }
        else
        {
            // ─── Flujo legacy: Lote en fase Producción ───
            var loteId = request.LoteId;
            if (loteId <= 0)
            {
                throw new ArgumentException(
                    "Debe especificar LoteId (legacy) o LotePosturaProduccionId (flujo LPP) para obtener indicadores semanales.");
            }

            var loteProd = await _context.Lotes
                .AsNoTracking()
                .Where(l =>
                    l.CompanyId == companyId
                    && l.DeletedAt == null
                    && l.Fase == "Produccion"
                    && l.LotePadreId == loteId)
                .OrderBy(l => l.LoteId)
                .FirstOrDefaultAsync();

            if (loteProd == null)
            {
                loteProd = await _context.Lotes
                    .AsNoTracking()
                    .Where(l =>
                        l.CompanyId == companyId
                        && l.DeletedAt == null
                        && l.Fase == "Produccion"
                        && l.LoteId == loteId)
                    .FirstOrDefaultAsync();
            }

            if (loteProd == null)
            {
                throw new ArgumentException(
                    $"No se encontró lote en producción para el lote {loteId}. " +
                    "Verifique que: 1) El lote esté en fase Producción, 2) Pertenezca a su compañía activa, 3) Exista registro inicial de producción.");
            }

            var fechaReferencia = loteProd.FechaInicioProduccion;
            if (!fechaReferencia.HasValue && loteProd.LotePadreId.HasValue)
            {
                fechaReferencia = await _context.Lotes
                    .AsNoTracking()
                    .Where(l => l.LoteId == loteProd.LotePadreId && l.DeletedAt == null)
                    .Select(l => l.FechaEncaset)
                    .FirstOrDefaultAsync();
            }
            if (!fechaReferencia.HasValue)
            {
                throw new ArgumentException(
                    $"El lote en producción {loteProd.LoteId} no tiene fecha de inicio de producción ni fecha de encaset (lote padre). " +
                    "Necesaria para calcular semanas.");
            }

            raza = loteProd.Raza ?? "";
            anoTablaGenetica = loteProd.AnoTablaGenetica;
            if ((string.IsNullOrWhiteSpace(raza) || !anoTablaGenetica.HasValue) && loteProd.LotePadreId.HasValue)
            {
                var padre = await _context.Lotes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.LoteId == loteProd.LotePadreId && l.DeletedAt == null);
                if (padre != null)
                {
                    raza = padre.Raza ?? "";
                    anoTablaGenetica = padre.AnoTablaGenetica;
                }
            }

            loteIdParam = loteProd.LoteId!.Value;
        }

        return new LoteProduccionResuelto(raza, anoTablaGenetica, lppParam, loteIdParam);
    }

    /// <summary>
    /// Obtiene indicadores semanales de producción.
    /// Resuelve lote (LPP o legacy) para validar existencia y obtener raza/año (guía), y delega el
    /// cálculo semana a semana en fn_indicadores_produccion_postura (misma aritmética que antes).
    /// </summary>
    public async Task<IndicadoresProduccionResponse> ObtenerIndicadoresSemanalesAsync(IndicadoresProduccionRequest request)
    {
        // ─── 1) Resolver compañía (igual que en el resto del módulo Producción) ───
        int companyId;
        try
        {
            companyId = await ResolveCompanyIdAsync();
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message, ex);
        }

        // ─── 2) Resolver lote en producción (LPP o legacy) para validar y obtener raza/año ───
        var (raza, anoTablaGenetica, lppParam, loteIdParam) = await ResolverLoteProduccionAsync(request, companyId);

        // ─── 3) Disponibilidad de guía genética (mismo criterio que antes) ───
        var tieneGuiaGenetica = !string.IsNullOrWhiteSpace(raza) && anoTablaGenetica.HasValue;
        var hayFilasGuia = false;
        if (tieneGuiaGenetica && anoTablaGenetica.HasValue)
        {
            try
            {
                var guias = await _guiaGeneticaService.ObtenerGuiaGeneticaProduccionAsync(raza, anoTablaGenetica.Value);
                hayFilasGuia = guias.Any();
            }
            catch
            {
                tieneGuiaGenetica = false;
            }
        }

        // ─── 4) Delegar el cálculo por semana a la BD (fn_indicadores_produccion_postura) ───
        var fechaDesde = request.FechaDesde?.Date;
        var fechaHasta = request.FechaHasta?.Date;
        var rows = await _context.Database
            .SqlQueryRaw<IndicadorProduccionSemanalBdRow>(
                "SELECT * FROM fn_indicadores_produccion_postura({0}::int, {1}::int, {2}::int, {3}::int, {4}::int, {5}::date, {6}::date)",
                companyId,
                (object?)lppParam ?? DBNull.Value,
                (object?)loteIdParam ?? DBNull.Value,
                (object?)request.SemanaDesde ?? DBNull.Value,
                (object?)request.SemanaHasta ?? DBNull.Value,
                (object?)fechaDesde ?? DBNull.Value,
                (object?)fechaHasta ?? DBNull.Value)
            .ToListAsync()
            .ConfigureAwait(false);

        // ─── 5) Armar la respuesta (contrato intacto) ───
        return IndicadoresProduccionCalculos.BuildResponse(
            rows, tieneGuiaGenetica, hayFilasGuia, raza, anoTablaGenetica);
    }

    /// <summary>
    /// Desglose de la clasificación de huevos POR ÍTEM (Primera/Pnc) por semana, para las empresas
    /// que clasifican con <c>metadata → huevoItems</c> en lugar de las 11 columnas fijas.
    /// <para>
    /// Endpoint HERMANO de los indicadores semanales: MISMO request, MISMA compañía activa y MISMA
    /// resolución de lote (<see cref="ResolverLoteProduccionAsync"/>), delegando el desglose en
    /// <c>fn_clasificacion_huevo_items_produccion</c>, que usa la misma fórmula de semana → las
    /// semanas casan 1:1 con la grilla de indicadores.
    /// </para>
    /// No exige el flag de empresa para LEER: si el lote no tiene desglose en metadata, la lista
    /// vuelve vacía (nunca error).
    /// </summary>
    public async Task<List<ClasificacionHuevoItemSemanaDto>> ObtenerClasificacionHuevoItemsAsync(
        IndicadoresProduccionRequest request)
    {
        // ─── 1) Resolver compañía (igual que los indicadores semanales) ───
        int companyId;
        try
        {
            companyId = await ResolveCompanyIdAsync();
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message, ex);
        }

        // ─── 2) Resolver lote en producción (LPP o legacy) — mismas validaciones/mensajes ───
        var lote = await ResolverLoteProduccionAsync(request, companyId);

        // ─── 3) Delegar el desglose a la BD (fn_clasificacion_huevo_items_produccion) ───
        var fechaDesde = request.FechaDesde?.Date;
        var fechaHasta = request.FechaHasta?.Date;
        return await _context.Database
            .SqlQueryRaw<ClasificacionHuevoItemSemanaDto>(
                "SELECT * FROM fn_clasificacion_huevo_items_produccion({0}::int, {1}::int, {2}::int, {3}::int, {4}::int, {5}::date, {6}::date)",
                companyId,
                (object?)lote.LppParam ?? DBNull.Value,
                (object?)lote.LoteIdParam ?? DBNull.Value,
                (object?)request.SemanaDesde ?? DBNull.Value,
                (object?)request.SemanaHasta ?? DBNull.Value,
                (object?)fechaDesde ?? DBNull.Value,
                (object?)fechaHasta ?? DBNull.Value)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<IndicadorProduccionSemanalDto?> ObtenerIndicadorSemanaAsync(int loteId, int semana)
    {
        var request = new IndicadoresProduccionRequest(loteId, SemanaDesde: semana, SemanaHasta: semana);
        var response = await ObtenerIndicadoresSemanalesAsync(request);
        return response.Indicadores.FirstOrDefault();
    }
}
