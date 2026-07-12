// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.Comun.cs
// Infraestructura compartida del módulo: lectura de Excel (EPPlus), construcción de resultados,
// registro de auditoría y helpers de celda. Reutilizable por todas las fases.
using System.Text.Json;
using OfficeOpenXml;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Migracion;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MigracionService
{
    /// <summary>Una fila de datos leída del Excel; celdas indexadas por encabezado normalizado.</summary>
    private sealed class FilaCruda
    {
        public int Numero { get; init; }
        public IReadOnlyDictionary<string, object?> Valores { get; init; } = new Dictionary<string, object?>();
        public object? this[string headerNormalizado] =>
            Valores.TryGetValue(headerNormalizado, out var v) ? v : null;
    }

    /// <summary>Primera celda no vacía entre varios encabezados alternativos (ya normalizados).</summary>
    private static object? Celda(FilaCruda fila, params string[] headersNormalizados)
    {
        foreach (var h in headersNormalizados)
        {
            var v = fila[h];
            if (!MigracionCalculos.EsVacia(v)) return v;
        }
        return null;
    }

    /// <summary>Lee la hoja indicada (o la primera) a filas materializadas; cierra el paquete.</summary>
    private static List<FilaCruda> LeerDatos(Stream stream, string nombreHoja)
    {
        using var package = new ExcelPackage(stream);
        var ws = package.Workbook.Worksheets
                     .FirstOrDefault(w => MigracionCalculos.NormalizarClave(w.Name) == MigracionCalculos.NormalizarClave(nombreHoja))
                 ?? package.Workbook.Worksheets.FirstOrDefault();

        var filas = new List<FilaCruda>();
        if (ws?.Dimension is null) return filas;

        int r0 = ws.Dimension.Start.Row, r1 = ws.Dimension.End.Row;
        int c0 = ws.Dimension.Start.Column, c1 = ws.Dimension.End.Column;

        var headers = new Dictionary<int, string>();
        for (int c = c0; c <= c1; c++)
        {
            var h = MigracionCalculos.NormalizarClave(ws.Cells[r0, c].Text);
            if (!string.IsNullOrEmpty(h) && !headers.ContainsValue(h)) headers[c] = h;
        }

        for (int r = r0 + 1; r <= r1; r++)
        {
            var celdas = new Dictionary<string, object?>();
            bool algo = false;
            foreach (var kv in headers)
            {
                var val = ws.Cells[r, kv.Key].Value;
                celdas[kv.Value] = val;
                if (!MigracionCalculos.EsVacia(val)) algo = true;
            }
            if (algo) filas.Add(new FilaCruda { Numero = r, Valores = celdas });
        }
        return filas;
    }

    // ── Resultados ──────────────────────────────────────────────────────────
    private static MigracionResultDto ResultadoConErrores(TipoMigracion tipo, bool dryRun, int total, IReadOnlyList<MigracionErrorDto> errores)
        => new(tipo.ToString(), false, total, 0, errores.Select(e => e.Fila).Where(f => f > 0).Distinct().Count(),
               "ConErrores", dryRun, errores);

    private static MigracionResultDto ResultadoVacio(TipoMigracion tipo, bool dryRun)
        => new(tipo.ToString(), false, 0, 0, 0, "ConErrores", dryRun,
               new[] { new MigracionErrorDto(0, "-", null, "La hoja 'Datos' no contiene filas para procesar.") });

    private static MigracionResultDto ResultadoOk(TipoMigracion tipo, bool dryRun, int total)
        => new(tipo.ToString(), true, total, dryRun ? 0 : total, 0,
               dryRun ? "Validado" : "Procesado", dryRun, Array.Empty<MigracionErrorDto>());

    private static string SerializarErrores(IReadOnlyList<MigracionErrorDto> errores) =>
        JsonSerializer.Serialize(errores);

    // ── Helpers de celda opcional (compartidos) ──────────────────────────────
    /// <summary>Entero ≥ 0 opcional: null si la celda está vacía; agrega error si es inválido.</summary>
    private static int? EnteroNoNegNull(FilaCruda fila, List<MigracionErrorDto> errores, string etiqueta, params string[] headers)
    {
        var cell = Celda(fila, headers);
        if (MigracionCalculos.EsVacia(cell)) return null;
        if (!MigracionCalculos.TryEntero(cell, out var v) || v < 0)
        { errores.Add(new(fila.Numero, etiqueta, MigracionCalculos.TextoLimpio(cell), $"{etiqueta}: se esperaba un entero ≥ 0.")); return null; }
        return v;
    }

    /// <summary>Fecha opcional: null si la celda está vacía; agrega error si es inválida.</summary>
    private static DateTime? FechaOpc(FilaCruda fila, List<MigracionErrorDto> errores, string etiqueta, params string[] headers)
    {
        var cell = Celda(fila, headers);
        if (MigracionCalculos.EsVacia(cell)) return null;
        if (!MigracionCalculos.TryFecha(cell, out var f))
        { errores.Add(new(fila.Numero, etiqueta, MigracionCalculos.TextoLimpio(cell), $"{etiqueta}: fecha inválida.")); return null; }
        return f;
    }

    /// <summary>Persiste el registro de auditoría de la corrida (solo en importación real).</summary>
    private async Task RegistrarAuditoriaAsync(
        TipoMigracion tipo, int companyId, string nombreArchivo,
        int total, int procesadas, int error, string estado, string? erroresJson, CancellationToken ct)
    {
        await _repo.RegistrarAsync(new MigracionMasiva
        {
            CompanyId = companyId,
            Tipo = tipo.ToString(),
            NombreArchivo = nombreArchivo,
            FilasTotales = total,
            FilasProcesadas = procesadas,
            FilasError = error,
            Estado = estado,
            ErroresJson = erroresJson,
            FechaProceso = DateTime.UtcNow,
            CreatedByUserId = _current.UserId
        }, ct);
    }
}
