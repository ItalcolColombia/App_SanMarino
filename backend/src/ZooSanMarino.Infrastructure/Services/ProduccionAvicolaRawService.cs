// src/ZooSanMarino.Infrastructure/Services/ProduccionAvicolaRawService.cs
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Common;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class ProduccionAvicolaRawService : IProduccionAvicolaRawService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyResolver _companyResolver;

    public ProduccionAvicolaRawService(ZooSanMarinoContext context, ICurrentUser currentUser, ICompanyResolver companyResolver)
    {
        _context = context;
        _currentUser = currentUser;
        _companyResolver = companyResolver;
    }

    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.ActiveCompanyName))
        {
            var cid = await _companyResolver.GetCompanyIdByNameAsync(_currentUser.ActiveCompanyName);
            if (cid.HasValue) return cid.Value;
        }
        return _currentUser.CompanyId;
    }

    public async Task<ProduccionAvicolaRawDto> CreateAsync(CreateProduccionAvicolaRawDto dto)
    {
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
        var entity = new ProduccionAvicolaRaw
        {
            CompanyId = effectiveCompanyId,
            CodigoGuiaGenetica = dto.CodigoGuiaGenetica,
            AnioGuia = dto.AnioGuia,
            Raza = dto.Raza,
            Edad = dto.Edad,
            MortSemH = dto.MortSemH,
            RetiroAcH = dto.RetiroAcH,
            MortSemM = dto.MortSemM,
            RetiroAcM = dto.RetiroAcM,
            Hembras = dto.Hembras,
            Machos = dto.Machos,
            ConsAcH = dto.ConsAcH,
            ConsAcM = dto.ConsAcM,
            GrAveDiaH = dto.GrAveDiaH,
            GrAveDiaM = dto.GrAveDiaM,
            PesoH = dto.PesoH,
            PesoM = dto.PesoM,
            Uniformidad = dto.Uniformidad,
            HTotalAa = dto.HTotalAa,
            ProdPorcentaje = dto.ProdPorcentaje,
            HIncAa = dto.HIncAa,
            AprovSem = dto.AprovSem,
            PesoHuevo = dto.PesoHuevo,
            MasaHuevo = dto.MasaHuevo,
            GrasaPorcentaje = dto.GrasaPorcentaje,
            NacimPorcentaje = dto.NacimPorcentaje,
            PollitoAa = dto.PollitoAa,
            AlimH = dto.AlimH,
            KcalAveDiaH = dto.KcalAveDiaH,
            KcalAveDiaM = dto.KcalAveDiaM,
            KcalH = dto.KcalH,
            ProtH = dto.ProtH,
            AlimM = dto.AlimM,
            KcalM = dto.KcalM,
            ProtM = dto.ProtM,
            KcalSemH = dto.KcalSemH,
            ProtHSem = dto.ProtHSem,
            KcalSemM = dto.KcalSemM,
            ProtSemM = dto.ProtSemM,
            AprovAc = dto.AprovAc,
            GrHuevoT = dto.GrHuevoT,
            GrHuevoInc = dto.GrHuevoInc,
            GrPollito = dto.GrPollito,
            Valor1000 = dto.Valor1000,
            Valor150 = dto.Valor150,
            Apareo = dto.Apareo,
            PesoMh = dto.PesoMh
        };

        // Calcular campos derivados antes de guardar (según fórmulas de la guía)
        var edad = ParseInt(entity.Edad);
        var (prevH, prevM) = edad.HasValue ? await GetPreviousSexCountsAsync(entity, effectiveCompanyId, edad.Value) : (null, null);
        ApplyDerivedFields(entity, edad, prevH, prevM);

        _context.ProduccionAvicolaRaw.Add(entity);
        await _context.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<IEnumerable<ProduccionAvicolaRawDto>> GetAllAsync()
    {
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
        return await _context.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(x => x.CompanyId == effectiveCompanyId)
            .Select(MapToDtoExpression())
            .ToListAsync();
    }

    public async Task<ProduccionAvicolaRawDto?> GetByIdAsync(int id)
    {
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
        var entity = await _context.ProduccionAvicolaRaw
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == effectiveCompanyId);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<ProduccionAvicolaRawDto> UpdateAsync(UpdateProduccionAvicolaRawDto dto)
    {
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
        var entity = await _context.ProduccionAvicolaRaw
            .FirstOrDefaultAsync(x => x.Id == dto.Id && x.CompanyId == effectiveCompanyId);

        if (entity == null)
            throw new KeyNotFoundException($"ProduccionAvicolaRaw con ID {dto.Id} no encontrado");

        // Actualizar propiedades
        entity.CodigoGuiaGenetica = dto.CodigoGuiaGenetica;
        entity.AnioGuia = dto.AnioGuia;
        entity.Raza = dto.Raza;
        entity.Edad = dto.Edad;
        entity.MortSemH = dto.MortSemH;
        entity.RetiroAcH = dto.RetiroAcH;
        entity.MortSemM = dto.MortSemM;
        entity.RetiroAcM = dto.RetiroAcM;
        entity.Hembras = dto.Hembras;
        entity.Machos = dto.Machos;
        entity.ConsAcH = dto.ConsAcH;
        entity.ConsAcM = dto.ConsAcM;
        entity.GrAveDiaH = dto.GrAveDiaH;
        entity.GrAveDiaM = dto.GrAveDiaM;
        entity.PesoH = dto.PesoH;
        entity.PesoM = dto.PesoM;
        entity.Uniformidad = dto.Uniformidad;
        entity.HTotalAa = dto.HTotalAa;
        entity.ProdPorcentaje = dto.ProdPorcentaje;
        entity.HIncAa = dto.HIncAa;
        entity.AprovSem = dto.AprovSem;
        entity.PesoHuevo = dto.PesoHuevo;
        entity.MasaHuevo = dto.MasaHuevo;
        entity.GrasaPorcentaje = dto.GrasaPorcentaje;
        entity.NacimPorcentaje = dto.NacimPorcentaje;
        entity.PollitoAa = dto.PollitoAa;
        entity.AlimH = dto.AlimH;
        entity.KcalAveDiaH = dto.KcalAveDiaH;
        entity.KcalAveDiaM = dto.KcalAveDiaM;
        entity.KcalH = dto.KcalH;
        entity.ProtH = dto.ProtH;
        entity.AlimM = dto.AlimM;
        entity.KcalM = dto.KcalM;
        entity.ProtM = dto.ProtM;
        entity.KcalSemH = dto.KcalSemH;
        entity.ProtHSem = dto.ProtHSem;
        entity.KcalSemM = dto.KcalSemM;
        entity.ProtSemM = dto.ProtSemM;
        entity.AprovAc = dto.AprovAc;
        entity.GrHuevoT = dto.GrHuevoT;
        entity.GrHuevoInc = dto.GrHuevoInc;
        entity.GrPollito = dto.GrPollito;
        entity.Valor1000 = dto.Valor1000;
        entity.Valor150 = dto.Valor150;
        entity.Apareo = dto.Apareo;
        entity.PesoMh = dto.PesoMh;

        // Recalcular campos derivados antes de guardar
        var edad = ParseInt(entity.Edad);
        var (prevH, prevM) = edad.HasValue ? await GetPreviousSexCountsAsync(entity, effectiveCompanyId, edad.Value) : (null, null);
        ApplyDerivedFields(entity, edad, prevH, prevM);

        await _context.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
        var entity = await _context.ProduccionAvicolaRaw
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == effectiveCompanyId);

        if (entity == null)
            return false;

        _context.ProduccionAvicolaRaw.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ZooSanMarino.Application.DTOs.Common.PagedResult<ProduccionAvicolaRawDto>> SearchAsync(ProduccionAvicolaRawSearchRequest request)
    {
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
        var query = _context.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(x => x.CompanyId == effectiveCompanyId && x.DeletedAt == null);

        // Aplicar filtros
        if (!string.IsNullOrWhiteSpace(request.AnioGuia))
            query = query.Where(x => x.AnioGuia != null && x.AnioGuia.Contains(request.AnioGuia));

        if (!string.IsNullOrWhiteSpace(request.Raza))
            query = query.Where(x => x.Raza != null && x.Raza.Contains(request.Raza));

        if (!string.IsNullOrWhiteSpace(request.Edad))
            query = query.Where(x => x.Edad != null && x.Edad.Contains(request.Edad));

        if (request.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == request.CompanyId.Value);

        // Aplicar ordenamiento
        query = ApplyOrdering(query, request.SortBy, request.SortDesc);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(MapToDtoExpression())
            .ToListAsync();

        return new ZooSanMarino.Application.DTOs.Common.PagedResult<ProduccionAvicolaRawDto>
        {
            Items = items,
            Total = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<ProduccionAvicolaRawFilterOptionsDto> GetFilterOptionsAsync()
    {
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();

        var baseQuery = _context.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(x => x.CompanyId == effectiveCompanyId && x.DeletedAt == null);

        var anios = await baseQuery
            .Where(x => x.AnioGuia != null && x.AnioGuia != "")
            .Select(x => x.AnioGuia!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        var razas = await baseQuery
            .Where(x => x.Raza != null && x.Raza != "")
            .Select(x => x.Raza!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return new ProduccionAvicolaRawFilterOptionsDto(anios, razas);
    }

    private static IQueryable<ProduccionAvicolaRaw> ApplyOrdering(IQueryable<ProduccionAvicolaRaw> query, string? sortBy, bool sortDesc)
    {
        return sortBy?.ToLower() switch
        {
            "anioguia" => sortDesc ? query.OrderByDescending(x => x.AnioGuia) : query.OrderBy(x => x.AnioGuia),
            "raza" => sortDesc ? query.OrderByDescending(x => x.Raza) : query.OrderBy(x => x.Raza),
            "edad" => sortDesc ? query.OrderByDescending(x => x.Edad) : query.OrderBy(x => x.Edad),
            "mortsemh" => sortDesc ? query.OrderByDescending(x => x.MortSemH) : query.OrderBy(x => x.MortSemH),
            "mortsemm" => sortDesc ? query.OrderByDescending(x => x.MortSemM) : query.OrderBy(x => x.MortSemM),
            "consach" => sortDesc ? query.OrderByDescending(x => x.ConsAcH) : query.OrderBy(x => x.ConsAcH),
            "consacm" => sortDesc ? query.OrderByDescending(x => x.ConsAcM) : query.OrderBy(x => x.ConsAcM),
            "pesoh" => sortDesc ? query.OrderByDescending(x => x.PesoH) : query.OrderBy(x => x.PesoH),
            "pesom" => sortDesc ? query.OrderByDescending(x => x.PesoM) : query.OrderBy(x => x.PesoM),
            "uniformidad" => sortDesc ? query.OrderByDescending(x => x.Uniformidad) : query.OrderBy(x => x.Uniformidad),
            _ => sortDesc ? query.OrderByDescending(x => x.Id) : query.OrderBy(x => x.Id)
        };
    }

    private static ProduccionAvicolaRawDto MapToDto(ProduccionAvicolaRaw entity)
    {
        return new ProduccionAvicolaRawDto(
            entity.Id,
            entity.CompanyId,
            entity.CodigoGuiaGenetica,
            entity.AnioGuia,
            entity.Raza,
            entity.Edad,
            entity.MortSemH,
            entity.RetiroAcH,
            entity.MortSemM,
            entity.RetiroAcM,
            entity.Hembras,
            entity.Machos,
            entity.ConsAcH,
            entity.ConsAcM,
            entity.GrAveDiaH,
            entity.GrAveDiaM,
            entity.PesoH,
            entity.PesoM,
            entity.Uniformidad,
            entity.HTotalAa,
            entity.ProdPorcentaje,
            entity.HIncAa,
            entity.AprovSem,
            entity.PesoHuevo,
            entity.MasaHuevo,
            entity.GrasaPorcentaje,
            entity.NacimPorcentaje,
            entity.PollitoAa,
            entity.AlimH,
            entity.KcalAveDiaH,
            entity.KcalAveDiaM,
            entity.KcalH,
            entity.ProtH,
            entity.AlimM,
            entity.KcalM,
            entity.ProtM,
            entity.KcalSemH,
            entity.ProtHSem,
            entity.KcalSemM,
            entity.ProtSemM,
            entity.AprovAc,
            entity.GrHuevoT,
            entity.GrHuevoInc,
            entity.GrPollito,
            entity.Valor1000,
            entity.Valor150,
            entity.Apareo,
            entity.PesoMh,
            entity.CreatedAt,
            entity.UpdatedAt
        );
    }

    private static System.Linq.Expressions.Expression<Func<ProduccionAvicolaRaw, ProduccionAvicolaRawDto>> MapToDtoExpression()
    {
        return entity => new ProduccionAvicolaRawDto(
            entity.Id,
            entity.CompanyId,
            entity.CodigoGuiaGenetica,
            entity.AnioGuia,
            entity.Raza,
            entity.Edad,
            entity.MortSemH,
            entity.RetiroAcH,
            entity.MortSemM,
            entity.RetiroAcM,
            entity.Hembras,
            entity.Machos,
            entity.ConsAcH,
            entity.ConsAcM,
            entity.GrAveDiaH,
            entity.GrAveDiaM,
            entity.PesoH,
            entity.PesoM,
            entity.Uniformidad,
            entity.HTotalAa,
            entity.ProdPorcentaje,
            entity.HIncAa,
            entity.AprovSem,
            entity.PesoHuevo,
            entity.MasaHuevo,
            entity.GrasaPorcentaje,
            entity.NacimPorcentaje,
            entity.PollitoAa,
            entity.AlimH,
            entity.KcalAveDiaH,
            entity.KcalAveDiaM,
            entity.KcalH,
            entity.ProtH,
            entity.AlimM,
            entity.KcalM,
            entity.ProtM,
            entity.KcalSemH,
            entity.ProtHSem,
            entity.KcalSemM,
            entity.ProtSemM,
            entity.AprovAc,
            entity.GrHuevoT,
            entity.GrHuevoInc,
            entity.GrPollito,
            entity.Valor1000,
            entity.Valor150,
            entity.Apareo,
            entity.PesoMh,
            entity.CreatedAt,
            entity.UpdatedAt
        );
    }

    // =========================
    // Cálculos de guía genética
    // =========================

    private static void ApplyDerivedFields(ProduccionAvicolaRaw e, int? edad, decimal? prevHembras, decimal? prevMachos)
    {
        // CodigoGuiaGenetica = CONCAT(Raza, AnioGuia, Edad)
        if (!string.IsNullOrWhiteSpace(e.Raza) && !string.IsNullOrWhiteSpace(e.AnioGuia) && !string.IsNullOrWhiteSpace(e.Edad))
        {
            e.CodigoGuiaGenetica = $"{e.Raza.Trim()}{e.AnioGuia.Trim()}{e.Edad.Trim()}";
        }

        // Hembras/Machos (dependen de Edad y mortalidad semanal)
        if (edad.HasValue)
        {
            var mortH = ParsePercent(e.MortSemH) ?? 0m;
            var mortM = ParsePercent(e.MortSemM) ?? 0m;

            decimal? baseH = null;
            decimal? baseM = null;

            if (edad.Value == 1)
            {
                baseH = 10000m;
                baseM = 1400m;
            }
            else
            {
                baseH = prevHembras;
                baseM = prevMachos;
            }

            if (baseH.HasValue)
            {
                var hembras = baseH.Value - (baseH.Value * (mortH / 100m));
                e.Hembras = FormatNumber(hembras);
            }

            if (baseM.HasValue)
            {
                var machos = baseM.Value - (baseM.Value * (mortM / 100m));
                e.Machos = FormatNumber(machos);
            }

            // %Apareo = Machos / Hembras * 100
            var hNum = ParseNumber(e.Hembras);
            var mNum = ParseNumber(e.Machos);
            if (hNum.HasValue && mNum.HasValue && hNum.Value != 0m)
            {
                var apareoPct = (mNum.Value / hNum.Value) * 100m;
                e.Apareo = FormatNumber(apareoPct);
            }
        }

        // KcalSemH = KcalH * GrAveDiaH * 7 / 1000
        var kcalH = ParseNumber(e.KcalH);
        var grDiaH = ParseNumber(e.GrAveDiaH);
        if (kcalH.HasValue && grDiaH.HasValue)
        {
            e.KcalSemH = FormatNumber(kcalH.Value * grDiaH.Value * 7m / 1000m);
        }

        // ProtHSem = (ProtH/100) * GrAveDiaH * 7
        var protH = ParsePercent(e.ProtH);
        if (protH.HasValue && grDiaH.HasValue)
        {
            e.ProtHSem = FormatNumber((protH.Value / 100m) * grDiaH.Value * 7m);
        }

        // KcalSemM = KcalM * GrAveDiaM * 7 / 1000
        var kcalM = ParseNumber(e.KcalM);
        var grDiaM = ParseNumber(e.GrAveDiaM);
        if (kcalM.HasValue && grDiaM.HasValue)
        {
            e.KcalSemM = FormatNumber(kcalM.Value * grDiaM.Value * 7m / 1000m);
        }

        // ProtSemM = (ProtM/100) * GrAveDiaM * 7
        var protM = ParsePercent(e.ProtM);
        if (protM.HasValue && grDiaM.HasValue)
        {
            e.ProtSemM = FormatNumber((protM.Value / 100m) * grDiaM.Value * 7m);
        }

        // MasaHuevo = PesoHuevo * %Prod / 100
        var pesoHuevo = ParseNumber(e.PesoHuevo);
        var prodPct = ParsePercent(e.ProdPorcentaje);
        if (pesoHuevo.HasValue && prodPct.HasValue)
        {
            var masa = pesoHuevo.Value * prodPct.Value / 100m;
            e.MasaHuevo = FormatNumber(masa);
        }

        // AprovAc = HIncAA / HTotalAA * 100
        var hInc = ParseNumber(e.HIncAa);
        var hTotal = ParseNumber(e.HTotalAa);
        if (hInc.HasValue && hTotal.HasValue && hTotal.Value != 0m)
        {
            var aprovAc = (hInc.Value / hTotal.Value) * 100m;
            e.AprovAc = FormatNumber(aprovAc);
        }

        // GR/HuevoT, GR/HuevoInc, GR/Pollito (si Edad > 24)
        if (edad.HasValue && edad.Value > 24)
        {
            var consH = ParseNumber(e.ConsAcH);
            var consM = ParseNumber(e.ConsAcM);
            var apareoPct = ParseApareoPercent(e.Apareo); // %Apareo

            if (consH.HasValue && consM.HasValue && apareoPct.HasValue)
            {
                var totalCons = consH.Value + (consM.Value * (apareoPct.Value / 100m));

                if (hTotal.HasValue && hTotal.Value != 0m)
                {
                    e.GrHuevoT = FormatNumber(totalCons / hTotal.Value);
                }
                else
                {
                    e.GrHuevoT = "0";
                }

                if (hInc.HasValue && hInc.Value != 0m)
                {
                    e.GrHuevoInc = FormatNumber(totalCons / hInc.Value);
                }
                else
                {
                    e.GrHuevoInc = "0";
                }

                var pollito = ParseNumber(e.PollitoAa);
                if (pollito.HasValue && pollito.Value != 0m)
                {
                    e.GrPollito = FormatNumber(totalCons / pollito.Value);
                }
                else
                {
                    e.GrPollito = "0";
                }
            }
        }
        else
        {
            // Si Edad <= 24, según fórmula el resultado es 0
            if (edad.HasValue)
            {
                e.GrHuevoT = "0";
                e.GrHuevoInc = "0";
                e.GrPollito = "0";
            }
        }
    }

    private async Task<(decimal? Hembras, decimal? Machos)> GetPreviousSexCountsAsync(ProduccionAvicolaRaw e, int effectiveCompanyId, int edad)
    {
        if (edad <= 1) return (null, null);
        if (string.IsNullOrWhiteSpace(e.Raza) || string.IsNullOrWhiteSpace(e.AnioGuia)) return (null, null);

        var candidates = await _context.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == effectiveCompanyId &&
                x.Raza == e.Raza &&
                x.AnioGuia == e.AnioGuia &&
                x.Id != e.Id)
            .Select(x => new { x.Edad, x.Hembras, x.Machos })
            .ToListAsync();

        var prev = candidates
            .Select(x => new { Edad = ParseInt(x.Edad), x.Hembras, x.Machos })
            .Where(x => x.Edad.HasValue && x.Edad.Value < edad)
            .OrderByDescending(x => x.Edad!.Value)
            .FirstOrDefault();

        return prev == null ? (null, null) : (ParseNumber(prev.Hembras), ParseNumber(prev.Machos));
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = value.Trim();
        // soportar "4.3" "4,3" -> toma parte entera
        clean = clean.Replace(',', '.');
        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return (int)Math.Truncate(d);
        if (int.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var i))
            return i;
        return null;
    }

    private static decimal? ParseNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = value.Trim();

        // eliminar símbolos
        clean = clean.Replace(" ", "");

        // si viene con % solo quitamos el símbolo (el llamador decide si es %)
        clean = clean.Replace("%", "");

        // Soportar formato con coma/punto (miles/decimales)
        clean = NormalizeDecimalSeparators(clean);

        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static decimal? ParsePercent(string? value)
    {
        // Devuelve un número 0-100 (por ejemplo "12.5" o "12.5%" -> 12.5)
        if (string.IsNullOrWhiteSpace(value)) return null;
        var n = ParseNumber(value);
        return n;
    }

    private static decimal? ParseApareoPercent(string? value)
    {
        // Apareo puede venir como:
        // - "12.5" o "12.5%" (porcentaje)
        // - "0.125" (fracción)
        // - "1:8" (ratio) -> 12.5%
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = value.Trim();

        if (clean.Contains(':'))
        {
            var parts = clean.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                var a = ParseNumber(parts[0]);
                var b = ParseNumber(parts[1]);
                if (a.HasValue && b.HasValue && b.Value != 0m)
                    return (a.Value / b.Value) * 100m;
            }
        }

        var n = ParseNumber(clean);
        if (!n.HasValue) return null;

        // Heurística: si está entre 0 y 1, se asume fracción
        if (n.Value > 0m && n.Value <= 1m)
            return n.Value * 100m;

        return n.Value;
    }

    private static string NormalizeDecimalSeparators(string input)
    {
        // Si tiene '.' y ',' decidimos cuál es decimal por la última ocurrencia
        var hasDot = input.Contains('.');
        var hasComma = input.Contains(',');
        if (hasDot && hasComma)
        {
            var lastDot = input.LastIndexOf('.');
            var lastComma = input.LastIndexOf(',');
            if (lastComma > lastDot)
            {
                // coma decimal, punto miles
                return input.Replace(".", "").Replace(",", ".");
            }
            // punto decimal, coma miles
            return input.Replace(",", "");
        }
        if (hasComma && !hasDot)
        {
            // coma decimal
            return input.Replace(",", ".");
        }
        return input;
    }

    private static string FormatNumber(decimal value)
    {
        // Formato estable para guardar en columnas string
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
