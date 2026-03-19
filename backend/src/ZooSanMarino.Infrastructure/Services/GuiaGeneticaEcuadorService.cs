using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class GuiaGeneticaEcuadorService : IGuiaGeneticaEcuadorService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyResolver _companyResolver;

    private static readonly string[] SexosValidos = ["mixto", "hembra", "macho"];

    static GuiaGeneticaEcuadorService()
    {
        ExcelPackage.License.SetNonCommercialPersonal("ZooSanMarino");
    }

    public GuiaGeneticaEcuadorService(
        ZooSanMarinoContext ctx,
        ICurrentUser currentUser,
        ICompanyResolver companyResolver)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _companyResolver = companyResolver;
    }

    private async Task<int> GetEffectiveCompanyIdAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_currentUser.ActiveCompanyName.Trim());
            if (byName.HasValue) return byName.Value;
        }

        return _currentUser.CompanyId;
    }

    private static string NormalizeEstado(string? estado)
    {
        var e = (estado ?? "active").Trim().ToLowerInvariant();
        return e is "active" or "inactive" ? e : "active";
    }

    private static string NormalizeSexo(string sexo)
    {
        var s = sexo.Trim().ToLowerInvariant();
        if (!SexosValidos.Contains(s))
            throw new ArgumentException($"Sexo no válido: {sexo}. Use mixto, hembra o macho.");
        return s;
    }

    private static string NormalizeHeaderText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        // Normalizar espacios raros del Excel (NBSP, saltos de línea, etc.)
        var t = raw.Trim()
            .Replace('\u00A0', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .ToLowerInvariant();

        t = t.Replace("%", "", StringComparison.Ordinal)
            .Replace("(", "", StringComparison.Ordinal)
            .Replace(")", "", StringComparison.Ordinal)
            .Replace(":", " ", StringComparison.Ordinal)
            .Replace(".", " ", StringComparison.Ordinal);

        // Reemplazar tildes antes de filtrar caracteres (así no se "pierden" letras como 'í')
        t = t.Replace("í", "i")
            .Replace("ó", "o")
            .Replace("á", "a")
            .Replace("é", "e")
            .Replace("ú", "u")
            .Replace("ñ", "n", StringComparison.Ordinal);

        // Dejar solo letras/números y espacios para facilitar matching (ej: "dia," -> "dia")
        t = Regex.Replace(t, @"[^a-z0-9 ]+", " ", RegexOptions.CultureInvariant);
        while (t.Contains("  ", StringComparison.Ordinal))
            t = t.Replace("  ", " ", StringComparison.Ordinal);
        return t.Trim();
    }

    /// <summary>Identifica columna por encabezado tolerante a mayúsculas, acentos y %.</summary>
    private static string? MapColumnKey(string? headerCell)
    {
        var n = NormalizeHeaderText(headerCell);
        if (n.Length == 0) return null;

        // Detectar cabecera exacta "dia" incluso si viene con signos o espacios: "Día:", "Día (g)", etc.
        if (Regex.IsMatch(n, @"\bdia\b", RegexOptions.CultureInvariant))
            return "dia";

        if (n.Contains("mortalidad", StringComparison.Ordinal) || n.Contains("seleccion", StringComparison.Ordinal))
            return "mortalidad";

        if (n.Contains("promedio", StringComparison.Ordinal) && n.Contains("ganancia", StringComparison.Ordinal))
            return "promedio_ganancia";

        if (n.Contains("ganancia", StringComparison.Ordinal) && n.Contains("diaria", StringComparison.Ordinal))
            return "ganancia_diaria";

        if (n.Contains("peso", StringComparison.Ordinal) && n.Contains("corporal", StringComparison.Ordinal))
            return "peso";

        if (n.Contains("cantidad", StringComparison.Ordinal) && n.Contains("alimento", StringComparison.Ordinal))
            return "cantidad_alimento";

        if (n.Contains("alimento", StringComparison.Ordinal) && n.Contains("acumulado", StringComparison.Ordinal))
            return "alimento_acumulado";

        if (Regex.IsMatch(n, @"\bca\b", RegexOptions.CultureInvariant))
            return "ca";

        return null;
    }

    private static int FindHeaderRow(ExcelWorksheet ws)
    {
        if (ws.Dimension == null) return 1;
        var endRow = ws.Dimension.End.Row;
        var endCol = ws.Dimension.End.Column;

        // Escanear primeras filas para encontrar dónde está la cabecera real de "Día"
        for (var r = 1; r <= Math.Min(endRow, 10); r++)
        {
            for (var c = 1; c <= endCol; c++)
            {
                var key = MapColumnKey(ws.Cells[r, c].Value?.ToString());
                if (key == "dia") return r;
            }
        }

        return 1;
    }

    private static Dictionary<int, string> BuildColumnMap(ExcelWorksheet ws, int headerRow)
    {
        var map = new Dictionary<int, string>();
        if (ws.Dimension == null) return map;

        for (var col = 1; col <= ws.Dimension.Columns; col++)
        {
            var key = MapColumnKey(ws.Cells[headerRow, col].Value?.ToString());
            if (key != null && !map.ContainsValue(key))
                map[col] = key;
        }

        return map;
    }

    private static bool TryParseDecimal(object? cell, out decimal value)
    {
        value = 0;
        if (cell == null) return true;
        if (cell is decimal d) { value = d; return true; }
        if (cell is double db) { value = (decimal)db; return true; }
        if (cell is float f) { value = (decimal)f; return true; }
        if (cell is int i) { value = i; return true; }
        if (cell is long l) { value = l; return true; }

        var s = cell.ToString()?.Trim() ?? "";
        if (s.Length == 0) return true;
        s = s.Replace("%", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        s = s.Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseInt(object? cell, out int value)
    {
        value = 0;
        if (cell == null) return false;
        if (cell is int i) { value = i; return true; }
        if (cell is long l) { value = (int)l; return l is >= int.MinValue and <= int.MaxValue; }
        if (cell is double db) { value = (int)Math.Round(db); return true; }
        if (cell is decimal d) { value = (int)Math.Round(d); return true; }

        var s = cell.ToString()?.Trim() ?? "";
        return int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadRow(
        ExcelWorksheet ws,
        int row,
        Dictionary<int, string> colMap,
        out int dia,
        out decimal peso,
        out decimal ganancia,
        out decimal promedio,
        out decimal cantidadAlim,
        out decimal alimAcum,
        out decimal ca,
        out decimal mortalidad,
        out string? error)
    {
        dia = 0;
        peso = ganancia = promedio = cantidadAlim = alimAcum = ca = mortalidad = 0;
        error = null;

        var diaEntry = colMap.FirstOrDefault(kv => kv.Value == "dia");
        if (!string.Equals(diaEntry.Value, "dia", StringComparison.Ordinal))
        {
            error = "Falta columna Día.";
            return false;
        }

        var diaCol = diaEntry.Key;
        if (!TryParseInt(ws.Cells[row, diaCol].Value, out dia) || dia < 1)
        {
            error = null;
            return false;
        }

        foreach (var (col, key) in colMap)
        {
            if (col == diaCol) continue;
            var cell = ws.Cells[row, col].Value;
            switch (key)
            {
                case "peso":
                    TryParseDecimal(cell, out peso);
                    break;
                case "ganancia_diaria":
                    TryParseDecimal(cell, out ganancia);
                    break;
                case "promedio_ganancia":
                    TryParseDecimal(cell, out promedio);
                    break;
                case "cantidad_alimento":
                    TryParseDecimal(cell, out cantidadAlim);
                    break;
                case "alimento_acumulado":
                    TryParseDecimal(cell, out alimAcum);
                    break;
                case "ca":
                    TryParseDecimal(cell, out ca);
                    break;
                case "mortalidad":
                    TryParseDecimal(cell, out mortalidad);
                    break;
            }
        }

        return true;
    }

    private async Task<GuiaGeneticaEcuadorHeader> GetOrCreateHeaderAsync(
        int companyId,
        string raza,
        int anioGuia,
        string estado,
        CancellationToken ct)
    {
        var razaTrim = raza.Trim();
        var header = await _ctx.GuiaGeneticaEcuadorHeader
            .FirstOrDefaultAsync(
                h => h.CompanyId == companyId && h.Raza == razaTrim && h.AnioGuia == anioGuia && h.DeletedAt == null,
                ct);

        var now = DateTime.UtcNow;
        var uid = _currentUser.UserId;

        if (header == null)
        {
            header = new GuiaGeneticaEcuadorHeader
            {
                Raza = razaTrim,
                AnioGuia = anioGuia,
                Estado = estado,
                CompanyId = companyId,
                CreatedByUserId = uid,
                CreatedAt = now
            };
            _ctx.GuiaGeneticaEcuadorHeader.Add(header);
            await _ctx.SaveChangesAsync(ct);
            return header;
        }

        header.Estado = estado;
        header.UpdatedByUserId = uid;
        header.UpdatedAt = now;
        await _ctx.SaveChangesAsync(ct);
        return header;
    }

    private async Task ReplaceDetallesSexoAsync(
        int headerId,
        int companyId,
        string sexo,
        IReadOnlyList<GuiaGeneticaEcuadorDetalle> nuevos,
        CancellationToken ct)
    {
        var existentes = await _ctx.GuiaGeneticaEcuadorDetalle
            .Where(d => d.GuiaGeneticaEcuadorHeaderId == headerId && d.Sexo == sexo)
            .ToListAsync(ct);

        if (existentes.Count > 0)
            _ctx.GuiaGeneticaEcuadorDetalle.RemoveRange(existentes);

        foreach (var d in nuevos)
        {
            d.GuiaGeneticaEcuadorHeaderId = headerId;
            d.Sexo = sexo;
            d.CompanyId = companyId;
            d.CreatedByUserId = _currentUser.UserId;
            d.CreatedAt = DateTime.UtcNow;
            d.UpdatedByUserId = null;
            d.UpdatedAt = null;
            d.DeletedAt = null;
        }

        _ctx.GuiaGeneticaEcuadorDetalle.AddRange(nuevos);
    }

    public async Task<GuiaGeneticaEcuadorFiltersDto> GetFiltersAsync(CancellationToken ct = default)
    {
        var cid = await GetEffectiveCompanyIdAsync(ct);
        var razas = await _ctx.GuiaGeneticaEcuadorHeader.AsNoTracking()
            .Where(h => h.CompanyId == cid && h.DeletedAt == null)
            .Select(h => h.Raza)
            .Distinct()
            .OrderBy(r => r)
            .ToListAsync(ct);

        var anos = await _ctx.GuiaGeneticaEcuadorHeader.AsNoTracking()
            .Where(h => h.CompanyId == cid && h.DeletedAt == null)
            .Select(h => h.AnioGuia)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(ct);

        return new GuiaGeneticaEcuadorFiltersDto(razas, anos);
    }

    public async Task<IEnumerable<int>> GetAnosPorRazaAsync(string raza, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(raza)) return Array.Empty<int>();

        var cid = await GetEffectiveCompanyIdAsync(ct);
        var razaTrim = raza.Trim();

        return await _ctx.GuiaGeneticaEcuadorHeader.AsNoTracking()
            .Where(h =>
                h.CompanyId == cid &&
                h.DeletedAt == null &&
                h.Estado == "active" &&
                EF.Functions.ILike(h.Raza, razaTrim))
            .Select(h => h.AnioGuia)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<string>> GetSexosCreadosAsync(string raza, int anioGuia, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(raza) || anioGuia <= 0)
            return Array.Empty<string>();

        var cid = await GetEffectiveCompanyIdAsync(ct);
        var razaTrim = raza.Trim();

        var headerId = await _ctx.GuiaGeneticaEcuadorHeader.AsNoTracking()
            .Where(h =>
                h.CompanyId == cid &&
                h.DeletedAt == null &&
                h.Estado == "active" &&
                h.AnioGuia == anioGuia &&
                EF.Functions.ILike(h.Raza, razaTrim))
            .Select(h => (int?)h.Id)
            .FirstOrDefaultAsync(ct);

        if (headerId is null)
            return Array.Empty<string>();

        var sexos = await _ctx.GuiaGeneticaEcuadorDetalle.AsNoTracking()
            .Where(d => d.GuiaGeneticaEcuadorHeaderId == headerId && d.DeletedAt == null)
            .Select(d => d.Sexo)
            .Distinct()
            .ToListAsync(ct);

        var orden = new[] { "mixto", "hembra", "macho" };
        return orden.Where(s => sexos.Contains(s)).ToList();
    }

    public async Task<IEnumerable<GuiaGeneticaEcuadorDetalleDto>> GetDatosAsync(string raza, int anioGuia, string sexo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(raza) || anioGuia <= 0 || string.IsNullOrWhiteSpace(sexo))
            return Array.Empty<GuiaGeneticaEcuadorDetalleDto>();

        var sexoN = NormalizeSexo(sexo);
        var cid = await GetEffectiveCompanyIdAsync(ct);
        var razaTrim = raza.Trim();

        var headerId = await _ctx.GuiaGeneticaEcuadorHeader.AsNoTracking()
            .Where(h =>
                h.CompanyId == cid &&
                h.DeletedAt == null &&
                h.AnioGuia == anioGuia &&
                EF.Functions.ILike(h.Raza, razaTrim))
            .Select(h => (int?)h.Id)
            .FirstOrDefaultAsync(ct);

        if (headerId is null)
            return Array.Empty<GuiaGeneticaEcuadorDetalleDto>();

        return await _ctx.GuiaGeneticaEcuadorDetalle.AsNoTracking()
            .Where(d => d.GuiaGeneticaEcuadorHeaderId == headerId && d.Sexo == sexoN && d.DeletedAt == null)
            .OrderBy(d => d.Dia)
            .Select(d => new GuiaGeneticaEcuadorDetalleDto(
                d.Sexo,
                d.Dia,
                d.PesoCorporalG,
                d.GananciaDiariaG,
                d.PromedioGananciaDiariaG,
                d.CantidadAlimentoDiarioG,
                d.AlimentoAcumuladoG,
                d.CA,
                d.MortalidadSeleccionDiaria))
            .ToListAsync(ct);
    }

    public async Task<GuiaGeneticaEcuadorImportResultDto> ImportExcelAsync(
        IFormFile file,
        string raza,
        int anioGuia,
        string estado,
        CancellationToken ct = default)
    {
        var errors = new List<string>();
        var validation = ValidateFile(file);
        if (!validation.IsValid)
            return new GuiaGeneticaEcuadorImportResultDto(false, 0, 0, 0, validation.Errors);

        if (string.IsNullOrWhiteSpace(raza) || anioGuia <= 1900 || anioGuia > 2100)
        {
            errors.Add("Raza y año de guía son obligatorios (año entre 1900 y 2100).");
            return new GuiaGeneticaEcuadorImportResultDto(false, 0, 0, 0, errors);
        }

        var estadoN = NormalizeEstado(estado);
        var cid = await GetEffectiveCompanyIdAsync(ct);

        int filasProcesadas = 0;
        int detallesInsertados = 0;
        int errorFilas = 0;

        await using var stream = file.OpenReadStream();
        using var package = new ExcelPackage(stream);

        var processedAnySheet = false;

        await using (var tx = await _ctx.Database.BeginTransactionAsync(ct))
        {
            try
            {
                var header = await GetOrCreateHeaderAsync(cid, raza, anioGuia, estadoN, ct);

                foreach (var sexo in SexosValidos)
                {
                    var ws = package.Workbook.Worksheets.FirstOrDefault(w =>
                        string.Equals(w.Name?.Trim(), sexo, StringComparison.OrdinalIgnoreCase));

                    if (ws?.Dimension == null)
                        continue;

                    processedAnySheet = true;
                    var headerRow = FindHeaderRow(ws);
                    var colMap = BuildColumnMap(ws, headerRow);
                    if (!colMap.ContainsValue("dia"))
                    {
                        errors.Add($"Hoja «{ws.Name}»: no se encontró columna Día.");
                        errorFilas++;
                        continue;
                    }

                    var diasVistos = new HashSet<int>();
                    var nuevos = new List<GuiaGeneticaEcuadorDetalle>();
                    var lastRow = ws.Dimension.End.Row;

                    for (var row = 2; row <= lastRow; row++)
                    {
                        if (!TryReadRow(ws, row, colMap, out var dia, out var peso, out var ganancia, out var promedio,
                                out var cantAlim, out var alimAcum, out var ca, out var mort, out var rowErr))
                        {
                            if (rowErr != null)
                                errors.Add($"Hoja «{ws.Name}» fila {row}: {rowErr}");
                            continue;
                        }

                        if (dia < 1)
                            continue;

                        filasProcesadas++;
                        if (!diasVistos.Add(dia))
                        {
                            errors.Add($"Hoja «{ws.Name}» fila {row}: día {dia} duplicado.");
                            errorFilas++;
                            continue;
                        }

                        nuevos.Add(new GuiaGeneticaEcuadorDetalle
                        {
                            Dia = dia,
                            PesoCorporalG = peso,
                            GananciaDiariaG = ganancia,
                            PromedioGananciaDiariaG = promedio,
                            CantidadAlimentoDiarioG = cantAlim,
                            AlimentoAcumuladoG = alimAcum,
                            CA = ca,
                            MortalidadSeleccionDiaria = mort
                        });
                    }

                    await ReplaceDetallesSexoAsync(header.Id, cid, sexo, nuevos, ct);
                    detallesInsertados += nuevos.Count;
                }

                if (!processedAnySheet)
                {
                    errors.Add("No se encontró ninguna hoja mixto, hembra o macho con datos.");
                    await tx.RollbackAsync(ct);
                    return new GuiaGeneticaEcuadorImportResultDto(false, 0, 0, 1, errors);
                }

                if (errors.Count > 0)
                {
                    await tx.RollbackAsync(ct);
                    return new GuiaGeneticaEcuadorImportResultDto(false, filasProcesadas, 0, errorFilas, errors);
                }

                await _ctx.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                errors.Add($"Error al importar: {ex.Message}");
                return new GuiaGeneticaEcuadorImportResultDto(false, filasProcesadas, detallesInsertados, errorFilas + 1, errors);
            }
        }

        var ok = errors.Count == 0;
        return new GuiaGeneticaEcuadorImportResultDto(ok, filasProcesadas, detallesInsertados, errorFilas, errors);
    }

    public async Task<GuiaGeneticaEcuadorHeaderDto> UpsertManualAsync(GuiaGeneticaEcuadorManualRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Raza) || request.AnioGuia <= 0)
            throw new ArgumentException("Raza y año son obligatorios.");

        var sexo = NormalizeSexo(request.Sexo);
        var estadoN = NormalizeEstado(request.Estado);
        if (request.Items == null || request.Items.Count == 0)
            throw new ArgumentException("Debe enviar al menos un ítem de detalle.");

        var cid = await GetEffectiveCompanyIdAsync(ct);
        await using var tx = await _ctx.Database.BeginTransactionAsync(ct);

        var header = await GetOrCreateHeaderAsync(cid, request.Raza, request.AnioGuia, estadoN, ct);

        var dias = new HashSet<int>();
        var nuevos = new List<GuiaGeneticaEcuadorDetalle>();
        foreach (var it in request.Items)
        {
            if (!dias.Add(it.Dia))
                throw new ArgumentException($"Día duplicado en el envío: {it.Dia}");

            nuevos.Add(new GuiaGeneticaEcuadorDetalle
            {
                Dia = it.Dia,
                PesoCorporalG = it.PesoCorporalG,
                GananciaDiariaG = it.GananciaDiariaG,
                PromedioGananciaDiariaG = it.PromedioGananciaDiariaG,
                CantidadAlimentoDiarioG = it.CantidadAlimentoDiarioG,
                AlimentoAcumuladoG = it.AlimentoAcumuladoG,
                CA = it.CA,
                MortalidadSeleccionDiaria = it.MortalidadSeleccionDiaria
            });
        }

        await ReplaceDetallesSexoAsync(header.Id, cid, sexo, nuevos, ct);
        await _ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new GuiaGeneticaEcuadorHeaderDto(header.Id, header.Raza, header.AnioGuia, header.Estado);
    }

    private static (bool IsValid, List<string> Errors) ValidateFile(IFormFile? file)
    {
        var err = new List<string>();
        if (file == null)
        {
            err.Add("No se ha proporcionado ningún archivo.");
            return (false, err);
        }

        if (file.Length == 0)
        {
            err.Add("El archivo está vacío.");
            return (false, err);
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not ".xlsx" and not ".xls")
            err.Add("Formato no válido. Use .xlsx o .xls.");

        const long max = 10 * 1024 * 1024;
        if (file.Length > max)
            err.Add("El archivo supera 10 MB.");

        return err.Count == 0 ? (true, err) : (false, err);
    }
}
