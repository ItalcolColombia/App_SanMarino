using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class GuiaGeneticaService : IGuiaGeneticaService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyResolver _companyResolver;

    public GuiaGeneticaService(ZooSanMarinoContext ctx, ICurrentUser currentUser, ICompanyResolver companyResolver)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _companyResolver = companyResolver;
    }

    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        // Prioridad: nombre de compañía activa (header/frontend storage) -> CompanyId del token
        if (!string.IsNullOrWhiteSpace(_currentUser.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_currentUser.ActiveCompanyName.Trim());
            if (byName.HasValue) return byName.Value;
        }

        return _currentUser.CompanyId;
    }

    /// <summary>
    /// Obtiene los datos de guía genética para una raza, año y edad específicos
    /// </summary>
    public async Task<GuiaGeneticaResponse> ObtenerGuiaGeneticaAsync(GuiaGeneticaRequest request)
    {
        try
        {
            var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
            var raza = (request.Raza ?? string.Empty).Trim();
            var anio = request.AnoTabla.ToString(CultureInfo.InvariantCulture);
            var edadObjetivo = request.Edad;

            // 1) Traer posibles candidatos por raza/año ignorando mayúsculas y espacios
            var candidatos = await _ctx.ProduccionAvicolaRaw
                .AsNoTracking()
                .Where(p =>
                    p.CompanyId == effectiveCompanyId &&
                    p.DeletedAt == null &&
                    p.Raza != null && p.AnioGuia != null &&
                    EF.Functions.Like(p.Raza.Trim().ToLower(), raza.ToLower()) &&
                    p.AnioGuia.Trim() == anio
                )
                .ToListAsync();

            if (candidatos.Count == 0)
            {
                return new GuiaGeneticaResponse(
                    Existe: false,
                    Datos: null,
                    Mensaje: $"No se encontró guía genética para Raza: {request.Raza}, Año: {request.AnoTabla}."
                );
            }

            // 2) Intentar machear EDAD con tolerancia de formatos (25, 25.0, 025, 'sem 25', etc.)
            var fila = candidatos
                .FirstOrDefault(p => EdadCoincide(p.Edad, edadObjetivo));

            // 3) Si no existe exacto, buscar por "edad más cercana" (opcional)
            if (fila == null)
            {
                // Buscar todas las que tengan edad numérica válida y elegir la exacta o la más cercana
                var conEdadNumerica = candidatos
                    .Select(p => new { p, edadNum = TryParseEdadNumerica(p.Edad) })
                    .Where(x => x.edadNum.HasValue)
                    .OrderBy(x => Math.Abs(x.edadNum!.Value - edadObjetivo))
                    .ToList();

                fila = conEdadNumerica.FirstOrDefault(x => x.edadNum!.Value == edadObjetivo)?.p
                    ?? conEdadNumerica.FirstOrDefault()?.p;
            }

            if (fila == null)
            {
                return new GuiaGeneticaResponse(
                    Existe: false,
                    Datos: null,
                    Mensaje: $"No se encontró edad {edadObjetivo} para Raza: {request.Raza}, Año: {request.AnoTabla}."
                );
            }

            // 4) Mapear/parsear campos de la fila seleccionada
            // CORREGIDO: usar ConsAcH/ConsAcM en lugar de GrAveDiaH/M
            var datos = new GuiaGeneticaDto(
                Edad: edadObjetivo,
                ConsumoHembras: ParseDouble(fila.GrAveDiaH),    // GrAveDiaH - Gramos por ave por día hembras
                ConsumoMachos: ParseDouble(fila.GrAveDiaM),     // GrAveDiaM - Gramos por ave por día machos
                RetiroAcumuladoHembras: ParseDouble(fila.RetiroAcH),  // RetiroAcH
                RetiroAcumuladoMachos: ParseDouble(fila.RetiroAcM),   // RetiroAcM
                PesoHembras: ParseDouble(fila.PesoH),         // peso_h
                PesoMachos: ParseDouble(fila.PesoM),          // peso_m
                MortalidadHembras: ParseDouble(fila.MortSemH), // mort_sem_h
                MortalidadMachos: ParseDouble(fila.MortSemM),  // mort_sem_m
                Uniformidad: ParseDouble(fila.Uniformidad),  // uniformidad
                PisoTermicoRequerido: DeterminarPisoTermico(edadObjetivo, fila),
                Observaciones: $"Guía: {fila.Raza} {fila.AnioGuia}"
            );

            return new GuiaGeneticaResponse(
                Existe: true,
                Datos: datos,
                Mensaje: "Guía genética encontrada exitosamente"
            );
        }
        catch (Exception ex)
        {
            return new GuiaGeneticaResponse(
                Existe: false,
                Datos: null,
                Mensaje: $"Error al obtener guía genética: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Obtiene múltiples edades de una guía genética
    /// </summary>
    public async Task<IEnumerable<GuiaGeneticaDto>> ObtenerGuiaGeneticaRangoAsync(string raza, int anoTabla, int edadDesde, int edadHasta)
    {
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
        var razaNorm = (raza ?? string.Empty).Trim().ToLower();
        var ano = anoTabla.ToString(CultureInfo.InvariantCulture);

        var guias = await _ctx.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(p =>
                p.CompanyId == effectiveCompanyId &&
                p.DeletedAt == null &&
                p.Raza != null && p.AnioGuia != null &&
                EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm) &&
                p.AnioGuia.Trim() == ano
            )
            .ToListAsync();

        return guias
            .Select(g => new { g, edad = TryParseEdadNumerica(g.Edad) })
            .Where(x => x.edad.HasValue && x.edad!.Value >= edadDesde && x.edad!.Value <= edadHasta)
            .OrderBy(x => x.edad!.Value)
            .Select(x => new GuiaGeneticaDto(
                Edad: x.edad!.Value,
                ConsumoHembras: ParseDouble(x.g.GrAveDiaH),
                ConsumoMachos: ParseDouble(x.g.GrAveDiaM),
                RetiroAcumuladoHembras: ParseDouble(x.g.RetiroAcH),
                RetiroAcumuladoMachos: ParseDouble(x.g.RetiroAcM),
                PesoHembras: ParseDouble(x.g.PesoH),
                PesoMachos: ParseDouble(x.g.PesoM),
                MortalidadHembras: ParseDouble(x.g.MortSemH),
                MortalidadMachos: ParseDouble(x.g.MortSemM),
                Uniformidad: ParseDouble(x.g.Uniformidad),
                PisoTermicoRequerido: DeterminarPisoTermico(x.edad!.Value, x.g),
                Observaciones: $"Guía: {x.g.Raza} {x.g.AnioGuia}"
            ));
    }

    /// <summary>
    /// Verifica si existe una guía genética para los parámetros dados
    /// </summary>
    public async Task<bool> ExisteGuiaGeneticaAsync(string raza, int anoTabla)
    {
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
        var razaNorm = (raza ?? string.Empty).Trim().ToLower();
        var ano = anoTabla.ToString(CultureInfo.InvariantCulture);

        return await _ctx.ProduccionAvicolaRaw
            .AsNoTracking()
            .AnyAsync(p =>
                p.CompanyId == effectiveCompanyId &&
                p.DeletedAt == null &&
                p.Raza != null && p.AnioGuia != null &&
                EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm) &&
                p.AnioGuia.Trim() == ano
            );
    }

    /// <summary>
    /// Obtiene las razas disponibles en las guías genéticas
    /// </summary>
    public async Task<IEnumerable<string>> ObtenerRazasDisponiblesAsync()
    {
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
        var razas = await _ctx.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(p =>
                p.CompanyId == effectiveCompanyId &&
                p.DeletedAt == null &&
                !string.IsNullOrWhiteSpace(p.Raza))
            .Select(p => p.Raza!)
            .Distinct()
            .ToListAsync();

        return razas
            .Select(r => r.Trim())
            .Where(r => r.Length >= 2)
            .OrderBy(r => r);
    }

    /// <summary>
    /// Obtiene los años disponibles para una raza específica
    /// </summary>
    public async Task<IEnumerable<int>> ObtenerAnosDisponiblesAsync(string raza)
    {
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
        var razaNorm = (raza ?? string.Empty).Trim().ToLower();

        var anos = await _ctx.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(p => p.Raza != null &&
                        p.AnioGuia != null &&
                        p.CompanyId == effectiveCompanyId &&
                        p.DeletedAt == null &&
                        EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm))
            .Select(p => p.AnioGuia!)
            .Distinct()
            .ToListAsync();

        return anos
            .Where(ano => int.TryParse(ano, out _))
            .Select(ano => int.Parse(ano, CultureInfo.InvariantCulture))
            .OrderByDescending(a => a)
            .ToList();
    }

    // ================== MÉTODOS PRIVADOS ==================

    /// <summary>
    /// Parsea un string a double de forma segura (admite coma y punto)
    /// </summary>
    private static double ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0.0;

        var clean = value.Trim()
            .Replace(" ", "")
            .Replace(",", ".");

        if (double.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return result;

        return 0.0;
    }

    /// <summary>
    /// Devuelve true si la "Edad" de la fila coincide con la edad objetivo con tolerancia de formato.
    /// </summary>
    private static bool EdadCoincide(string? edadStr, int edadObjetivo)
    {
        var num = TryParseEdadNumerica(edadStr);
        return num.HasValue && num.Value == edadObjetivo;
    }

    /// <summary>
    /// Intenta extraer un número de semanas desde un string de edad (e.g., "25", "25.0", "SEM 25", "Semana 25")
    /// </summary>
    private static int? TryParseEdadNumerica(string? edadStr)
    {
        if (string.IsNullOrWhiteSpace(edadStr)) return null;

        var s = edadStr.Trim();

        // 1) Si es número (incluye 25, 25.0, 025)
        var sNorm = s.Replace(",", "."); // por si viene "25,0"
        if (double.TryParse(sNorm, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
        {
            // Redondear/truncar hacia entero
            var n = (int)Math.Round(d, MidpointRounding.AwayFromZero);
            return n;
        }

        // 2) Buscar dígitos dentro del texto (e.g., "SEM 25", "Semana 25", etc.)
        var m = Regex.Match(s, @"(\d+)");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n2))
            return n2;

        return null;
    }

    /// <summary>
    /// Determina si se requiere piso térmico basado en edad y campos descriptivos.
    /// </summary>
    private static bool DeterminarPisoTermico(int edad, ProduccionAvicolaRaw guia)
    {
        if (edad <= 3) return true;

        var extra = (guia.Valor1000 ?? string.Empty).ToLowerInvariant();
        if (extra.Contains("termico") || extra.Contains("calor") || extra.Contains("temperatura"))
            return true;

        return false;
    }

    /// <summary>
    /// Obtiene datos de guía genética a partir de la semana 26 (edad >= 26)
    /// Para uso en liquidación técnica de producción
    /// </summary>
    public async Task<IEnumerable<GuiaGeneticaDto>> ObtenerGuiaGeneticaProduccionAsync(string raza, int anoTabla)
    {
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
        var razaNorm = (raza ?? string.Empty).Trim().ToLower();
        var ano = anoTabla.ToString(CultureInfo.InvariantCulture);

        var guias = await _ctx.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(p =>
                p.CompanyId == effectiveCompanyId &&
                p.DeletedAt == null &&
                p.Raza != null && p.AnioGuia != null &&
                EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm) &&
                p.AnioGuia.Trim() == ano
            )
            .ToListAsync();

        return guias
            .Select(g => new { g, edad = TryParseEdadNumerica(g.Edad) })
            .Where(x => x.edad.HasValue && x.edad!.Value >= 26) // Solo semanas >= 26
            .OrderBy(x => x.edad!.Value)
            .Select(x => new GuiaGeneticaDto(
                Edad: x.edad!.Value,
                ConsumoHembras: ParseDouble(x.g.GrAveDiaH),
                ConsumoMachos: ParseDouble(x.g.GrAveDiaM),
                RetiroAcumuladoHembras: ParseDouble(x.g.RetiroAcH),
                RetiroAcumuladoMachos: ParseDouble(x.g.RetiroAcM),
                PesoHembras: ParseDouble(x.g.PesoH),
                PesoMachos: ParseDouble(x.g.PesoM),
                MortalidadHembras: ParseDouble(x.g.MortSemH),
                MortalidadMachos: ParseDouble(x.g.MortSemM),
                Uniformidad: ParseDouble(x.g.Uniformidad),
                PisoTermicoRequerido: DeterminarPisoTermico(x.edad!.Value, x.g),
                Observaciones: $"Guía: {x.g.Raza} {x.g.AnioGuia}"
            ));
    }
}