using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
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
    /// Fila de guía genética normalizada a texto, sea cual sea la tabla de origen. Preserva el
    /// contrato que ya consumen <see cref="ParseDouble"/>/<see cref="TryParseEdadNumerica"/>, así
    /// que agregar una fuente nueva no cambia un byte del parseo existente.
    /// </summary>
    private sealed record GuiaGeneticaFila(
        string Raza, string AnioGuia, string? Edad,
        string? GrAveDiaH, string? GrAveDiaM,
        string? RetiroAcH, string? RetiroAcM,
        string? PesoH, string? PesoM,
        string? MortSemH, string? MortSemM,
        string? Uniformidad, string? Valor1000);

    /// <summary>
    /// Candidatos de una raza+año, por empresa. Primero mira <c>guia_genetica_santa_reyes</c> (tabla
    /// dedicada, liviana, nace con Santa Reyes pero no está atada a esa empresa por nombre); si no
    /// tiene filas para esta empresa+raza+año, cae al comportamiento de siempre contra
    /// <c>ProduccionAvicolaRaw</c> (guia_genetica_sanmarino_colombia). Una empresa nunca tiene datos
    /// en las dos tablas para la misma raza, así que esto no mezcla ni prioriza por lógica de
    /// negocio — es puramente "¿dónde vive el dato de esta empresa?".
    /// </summary>
    private async Task<List<GuiaGeneticaFila>> ObtenerCandidatosAsync(int companyId, string razaNorm, string anio)
    {
        // Alias SOLO para la guía propia (ver RazaGuiaAliasCalculos): la caída a la compartida de
        // más abajo sigue usando razaNorm tal cual — delta cero para quien no tiene guía propia.
        var razaPropia = RazaGuiaAliasCalculos.AliasGuiaPropia(razaNorm);

        var propios = await _ctx.GuiaGeneticaSantaReyes
            .AsNoTracking()
            .Where(g =>
                g.CompanyId == companyId &&
                g.DeletedAt == null &&
                g.Raza.Trim().ToLower() == razaPropia &&
                g.AnioGuia.Trim() == anio)
            .Select(g => new GuiaGeneticaFila(
                g.Raza, g.AnioGuia, g.Edad.ToString(),
                g.GrAveDiaH.HasValue ? g.GrAveDiaH.Value.ToString(CultureInfo.InvariantCulture) : null, null,
                g.RetiroAcH.HasValue ? g.RetiroAcH.Value.ToString(CultureInfo.InvariantCulture) : null, null,
                null, null, null, null, null, null))
            .ToListAsync();

        if (propios.Count > 0) return propios;

        var compartidos = await _ctx.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(p =>
                p.CompanyId == companyId &&
                p.DeletedAt == null &&
                p.Raza != null && p.AnioGuia != null &&
                EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm) &&
                p.AnioGuia.Trim() == anio)
            .Select(p => new GuiaGeneticaFila(
                p.Raza!, p.AnioGuia!, p.Edad,
                p.GrAveDiaH, p.GrAveDiaM,
                p.RetiroAcH, p.RetiroAcM,
                p.PesoH, p.PesoM,
                p.MortSemH, p.MortSemM,
                p.Uniformidad, p.Valor1000))
            .ToListAsync();

        return compartidos;
    }

    /// <summary>
    /// Razas disponibles para la empresa: las de su guía propia MÁS las de la compartida, sin duplicar.
    ///
    /// <para>
    /// 🔴 <b>Lo que había antes cortaba a nivel EMPRESA, no de raza:</b>
    /// <c>if (propias.Count &gt; 0) return propias;</c>. Con 615 filas propias sembradas, la empresa
    /// entraba SIEMPRE por esa rama, así que una raza cargada en la guía compartida se importaba
    /// «OK», se listaba en el grid y <b>nunca aparecía en el selector de lotes</b>. No fallaba:
    /// mentía. Y era el único workaround aparente para una raza sin guía propia.
    /// </para>
    ///
    /// <para>
    /// <b>Delta cero para toda empresa SIN guía propia</b> —Sanmarino, Demo, Ecuador y Panamá, las
    /// cuatro medidas con 0 filas propias—: la lista compartida se devuelve tal cual, misma
    /// instancia y mismo orden. La combinación y su invariante viven en
    /// <see cref="GuiaGeneticaRazasCalculos.CombinarRazas"/>, con tests.
    /// </para>
    ///
    /// <para>
    /// Se consultan las dos fuentes siempre: para una empresa sin propias son exactamente las dos
    /// consultas que ya se hacían.
    /// </para>
    /// </summary>
    private async Task<List<string>> ObtenerRazasCrudoAsync(int companyId)
    {
        var propias = await _ctx.GuiaGeneticaSantaReyes
            .AsNoTracking()
            .Where(g => g.CompanyId == companyId && g.DeletedAt == null)
            .Select(g => g.Raza)
            .Distinct()
            .ToListAsync();

        var compartidas = await _ctx.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.DeletedAt == null && !string.IsNullOrWhiteSpace(p.Raza))
            .Select(p => p.Raza!)
            .Distinct()
            .ToListAsync();

        return GuiaGeneticaRazasCalculos.CombinarRazas(propias, compartidas);
    }

    /// <summary>
    /// Años (crudos, como texto) disponibles para una raza de la empresa — mismo criterio de
    /// <see cref="ObtenerCandidatosAsync"/>.
    ///
    /// <para>
    /// ✅ <b>Este método NO tiene el defecto de <see cref="ObtenerRazasCrudoAsync"/> y por eso no se
    /// toca.</b> Acá el corte es <b>por RAZA</b>, no por empresa: la primera consulta ya filtra
    /// <c>Raza == razaPropia</c>, así que una raza que sólo vive en la guía compartida devuelve cero
    /// propios y cae a la compartida como corresponde. Cada raza vive en una sola tabla; el
    /// <c>if (propios.Count &gt; 0)</c> pregunta «¿dónde vive el dato de ESTA raza?», que es la
    /// pregunta correcta. Unirlo también mezclaría años de dos guías distintas para la misma línea
    /// genética — un cambio de comportamiento sin defecto que lo justifique.
    /// </para>
    /// </summary>
    private async Task<List<string>> ObtenerAniosCrudoAsync(int companyId, string razaNorm)
    {
        var razaPropia = RazaGuiaAliasCalculos.AliasGuiaPropia(razaNorm);

        var propios = await _ctx.GuiaGeneticaSantaReyes
            .AsNoTracking()
            .Where(g => g.CompanyId == companyId && g.DeletedAt == null && g.Raza.Trim().ToLower() == razaPropia)
            .Select(g => g.AnioGuia)
            .Distinct()
            .ToListAsync();

        if (propios.Count > 0) return propios;

        return await _ctx.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(p => p.Raza != null && p.AnioGuia != null &&
                        p.CompanyId == companyId && p.DeletedAt == null &&
                        EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm))
            .Select(p => p.AnioGuia!)
            .Distinct()
            .ToListAsync();
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
            var candidatos = await ObtenerCandidatosAsync(effectiveCompanyId, raza.ToLower(), anio);

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
                PisoTermicoRequerido: DeterminarPisoTermico(edadObjetivo, fila.Valor1000),
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

        var guias = await ObtenerCandidatosAsync(effectiveCompanyId, razaNorm, ano);

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
                PisoTermicoRequerido: DeterminarPisoTermico(x.edad!.Value, x.g.Valor1000),
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

        var candidatos = await ObtenerCandidatosAsync(effectiveCompanyId, razaNorm, ano);
        return candidatos.Count > 0;
    }

    /// <summary>
    /// Obtiene las razas disponibles en las guías genéticas
    /// </summary>
    public async Task<IEnumerable<string>> ObtenerRazasDisponiblesAsync()
    {
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
        var razas = await ObtenerRazasCrudoAsync(effectiveCompanyId);

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

        var anos = await ObtenerAniosCrudoAsync(effectiveCompanyId, razaNorm);

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
    /// Determina si se requiere piso térmico basado en edad y el campo descriptivo libre
    /// (<c>valor_1000</c> en la tabla compartida; la tabla dedicada de Santa Reyes no lo tiene,
    /// así que para sus filas siempre llega <c>null</c> y solo pesa la edad).
    /// </summary>
    private static bool DeterminarPisoTermico(int edad, string? valor1000)
    {
        if (edad <= 3) return true;

        var extra = (valor1000 ?? string.Empty).ToLowerInvariant();
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

        var guias = await ObtenerCandidatosAsync(effectiveCompanyId, razaNorm, ano);

        return guias
            .Select(g => new { g, edad = TryParseEdadNumerica(g.Edad) })
            // Serie de PRODUCCION. El corte por numero (>= 26) deja fuera las filas de levante de
            // la guia de esquema completo, pero por si solo tambien descartaba "25P" -que parsea a
            // 25 y es justamente la fila que ABRE la produccion, con los acumulados reiniciados-.
            // Sin ella, la semana de transicion se quedaba sin consumo, peso ni mortalidad standard.
            // Ver GrafiaEdadGuiaCalculos.
            .Where(x => x.edad.HasValue && GrafiaEdadGuiaCalculos.EsSerieDeProduccion(x.g.Edad, x.edad!.Value))
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
                PisoTermicoRequerido: DeterminarPisoTermico(x.edad!.Value, x.g.Valor1000),
                Observaciones: $"Guía: {x.g.Raza} {x.g.AnioGuia}"
            ));
    }
}
