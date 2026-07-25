// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.Comun.cs
// Infraestructura compartida del módulo: validación de archivo, lectura de Excel (EPPlus) contra el
// esquema único (Application/Calculos/MigracionEsquemas.cs), construcción de resultados, registro de
// auditoría y helpers de celda. Reutilizable por todas las fases.
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using OfficeOpenXml;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Migracion;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MigracionService
{
    // ── Constantes de protección (F1) ─────────────────────────────────────────
    private const int MaxFilasPorArchivo = 5000;
    private const int MaxErroresReportados = 300;

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

    /// <summary>
    /// Valida que el archivo subido sea, a primera vista, un .xlsx real: extensión correcta y firma
    /// de archivo ZIP ("PK"). No abre el paquete completo (eso lo hace <see cref="LeerDatosConEsquema"/>);
    /// solo evita que un archivo corrupto o de otro formato llegue a EPPlus y explote con un 500 genérico.
    /// </summary>
    private static void ValidarArchivo(IFormFile file)
    {
        if (string.IsNullOrEmpty(file.FileName) || !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El archivo debe ser un Excel .xlsx válido (la extensión debe ser .xlsx).");

        using var stream = file.OpenReadStream();
        Span<byte> firma = stackalloc byte[2];
        int leidos = stream.Read(firma);
        if (leidos < 2 || firma[0] != 0x50 || firma[1] != 0x4B) // "PK" = firma de archivo ZIP (xlsx es un ZIP)
            throw new InvalidOperationException("El archivo debe ser un Excel .xlsx válido (el contenido no corresponde a un archivo .xlsx).");
    }

    /// <summary>
    /// Lee la hoja de datos del esquema indicado, validando estructura (paquete, hoja, encabezados,
    /// tope de filas) ANTES de materializar filas — evita cargar en memoria un archivo desproporcionado
    /// y reporta un único error claro en vez de N errores confusos por columna faltante. Acumula
    /// errores/advertencias en <paramref name="errores"/> (severidad "Error" vs "Advertencia").
    /// </summary>
    private static List<FilaCruda> LeerDatosConEsquema(Stream stream, EsquemaMigracion esquema, List<MigracionErrorDto> errores)
    {
        ExcelPackage? package = null;
        try
        {
            package = new ExcelPackage(stream);
        }
        catch (Exception)
        {
            errores.Add(new(0, "-", null, "El archivo no es un .xlsx válido o está dañado."));
            return new List<FilaCruda>();
        }

        using (package)
        {
            var hojaNormalizada = MigracionCalculos.NormalizarClave(esquema.Hoja);
            var ws = package.Workbook.Worksheets
                .FirstOrDefault(w => MigracionCalculos.NormalizarClave(w.Name) == hojaNormalizada);

            if (ws is null)
            {
                if (package.Workbook.Worksheets.Count == 1)
                {
                    ws = package.Workbook.Worksheets[0];
                }
                else
                {
                    errores.Add(new(0, "-", null, $"No se encontró la hoja '{esquema.Hoja}'."));
                    return new List<FilaCruda>();
                }
            }

            if (ws.Dimension is null) return new List<FilaCruda>();

            int r0 = ws.Dimension.Start.Row, r1 = ws.Dimension.End.Row;
            int c0 = ws.Dimension.Start.Column, c1 = ws.Dimension.End.Column;

            var headers = new Dictionary<int, string>();
            var headersVistos = new HashSet<string>();
            for (int c = c0; c <= c1; c++)
            {
                var h = MigracionCalculos.NormalizarClave(ws.Cells[r0, c].Text);
                if (string.IsNullOrEmpty(h)) continue;
                if (!headersVistos.Add(h))
                {
                    errores.Add(new(1, h, null, "Encabezado duplicado: se usa la primera aparición.", "Advertencia"));
                    continue;
                }
                headers[c] = h;
            }

            var (faltantes, desconocidos) = MigracionEsquemaCalculos.ValidarEncabezados(esquema, headers.Values.ToList());
            foreach (var titulo in faltantes)
                errores.Add(new(0, titulo, null, $"La columna '{titulo}' es obligatoria y no está en el archivo."));
            foreach (var header in desconocidos)
                errores.Add(new(1, header, null, $"La columna '{header}' no corresponde a la plantilla y será ignorada.", "Advertencia"));

            if (faltantes.Count > 0) return new List<FilaCruda>();

            int filasDeDatos = r1 - r0;
            if (filasDeDatos > esquema.MaxFilas)
            {
                errores.Add(new(0, "-", null, $"El archivo supera el máximo de {esquema.MaxFilas} filas de datos ({filasDeDatos})."));
                return new List<FilaCruda>();
            }

            var filas = new List<FilaCruda>();
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
    }

    // ── Resultados ──────────────────────────────────────────────────────────
    /// <summary>
    /// Construye el resultado a partir del acumulado de errores/advertencias. Si hay al menos un
    /// error real (Severidad=="Error") el resultado es un fallo ("ConErrores", nada se inserta); si
    /// SOLO hay advertencias, el resultado es exitoso (Validado/Procesado) y las advertencias viajan
    /// igual en <c>Errores</c>. <c>FilasError</c> cuenta filas DISTINTAS (Fila&gt;0) con Severidad=="Error".
    /// </summary>
    private static MigracionResultDto ResultadoConErrores(TipoMigracion tipo, bool dryRun, int total, IReadOnlyList<MigracionErrorDto> errores)
    {
        var (capados, totalReal) = MigracionEsquemaCalculos.LimitarErrores(errores, MaxErroresReportados);
        var hayErroresReales = errores.Any(e => e.Severidad == "Error");
        if (!hayErroresReales)
        {
            return new(tipo.ToString(), true, total, dryRun ? 0 : total, 0,
                dryRun ? "Validado" : "Procesado", dryRun, capados, 0, 0, totalReal);
        }

        var filasError = errores.Where(e => e.Severidad == "Error" && e.Fila > 0).Select(e => e.Fila).Distinct().Count();
        return new(tipo.ToString(), false, total, 0, filasError, "ConErrores", dryRun, capados, 0, 0, totalReal);
    }

    /// <summary>Resultado de un fallo real de inserción (excepción en el catch de la transacción/función BD).</summary>
    private static MigracionResultDto ResultadoFallido(TipoMigracion tipo, int total, IReadOnlyList<MigracionErrorDto> errores)
    {
        var (capados, totalReal) = MigracionEsquemaCalculos.LimitarErrores(errores, MaxErroresReportados);
        return new(tipo.ToString(), false, total, 0, total, "Fallido", false, capados, 0, 0, totalReal);
    }

    private static MigracionResultDto ResultadoVacio(TipoMigracion tipo, bool dryRun)
        => new(tipo.ToString(), false, 0, 0, 0, "ConErrores", dryRun,
               new[] { new MigracionErrorDto(0, "-", null, "La hoja 'Datos' no contiene filas para procesar.") });

    /// <summary>
    /// Resultado exitoso (sin errores reales). <paramref name="advertencias"/> (si las hay) viaja en
    /// <c>Errores</c>. <paramref name="procesadas"/> permite indicar el conteo real de filas insertadas
    /// (p.ej. el valor devuelto por la función plpgsql) en vez del default <c>dryRun ? 0 : total</c>.
    /// </summary>
    private static MigracionResultDto ResultadoOk(TipoMigracion tipo, bool dryRun, int total, IReadOnlyList<MigracionErrorDto>? advertencias = null, int? procesadas = null)
    {
        var lista = advertencias ?? Array.Empty<MigracionErrorDto>();
        var (capados, totalReal) = MigracionEsquemaCalculos.LimitarErrores(lista, MaxErroresReportados);
        var filasProcesadas = procesadas ?? (dryRun ? 0 : total);
        return new(tipo.ToString(), true, total, filasProcesadas, 0, dryRun ? "Validado" : "Procesado", dryRun, capados, 0, 0, totalReal);
    }

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

    /// <summary>Entero ≥ 0: 0 si la celda está vacía; agrega error si es inválido.</summary>
    private static int EnteroNoNeg(FilaCruda fila, List<MigracionErrorDto> errores, string etiqueta, params string[] headers)
    {
        var cell = Celda(fila, headers);
        if (MigracionCalculos.EsVacia(cell)) return 0;
        if (!MigracionCalculos.TryEntero(cell, out var v) || v < 0)
        { errores.Add(new(fila.Numero, etiqueta, MigracionCalculos.TextoLimpio(cell), $"{etiqueta}: se esperaba un entero ≥ 0.")); return 0; }
        return v;
    }

    /// <summary>Decimal ≥ 0 opcional: null si la celda está vacía; agrega error si es inválido.</summary>
    private static decimal? DecimalNoNeg(FilaCruda fila, List<MigracionErrorDto> errores, string etiqueta, params string[] headers)
    {
        var cell = Celda(fila, headers);
        if (MigracionCalculos.EsVacia(cell)) return null;
        if (!MigracionCalculos.TryDecimal(cell, out var v) || v < 0)
        { errores.Add(new(fila.Numero, etiqueta, MigracionCalculos.TextoLimpio(cell), $"{etiqueta}: se esperaba un número ≥ 0.")); return null; }
        return v;
    }

    /// <summary>Double opcional (sin restricción de signo): null si la celda está vacía; agrega error si es inválido.</summary>
    private static double? DobleOpc(FilaCruda fila, List<MigracionErrorDto> errores, string etiqueta, params string[] headers)
    {
        var cell = Celda(fila, headers);
        if (MigracionCalculos.EsVacia(cell)) return null;
        if (!MigracionCalculos.TryDecimal(cell, out var v))
        { errores.Add(new(fila.Numero, etiqueta, MigracionCalculos.TextoLimpio(cell), $"{etiqueta}: número inválido.")); return null; }
        return (double)v;
    }

    /// <summary>Double ≥ 0 opcional (espejo de Validators.min(0) del front): null si vacía; error si inválido o negativo.</summary>
    private static double? DobleNoNeg(FilaCruda fila, List<MigracionErrorDto> errores, string etiqueta, params string[] headers)
    {
        var cell = Celda(fila, headers);
        if (MigracionCalculos.EsVacia(cell)) return null;
        if (!MigracionCalculos.TryDecimal(cell, out var v) || v < 0)
        { errores.Add(new(fila.Numero, etiqueta, MigracionCalculos.TextoLimpio(cell), $"{etiqueta}: se esperaba un número ≥ 0.")); return null; }
        return (double)v;
    }

    /// <summary>Porcentaje opcional en [0,100] (espejo de Validators.min(0)+max(100) del front): null si vacía; error si está fuera de rango.</summary>
    private static double? Porcentaje0a100(FilaCruda fila, List<MigracionErrorDto> errores, string etiqueta, params string[] headers)
    {
        var cell = Celda(fila, headers);
        if (MigracionCalculos.EsVacia(cell)) return null;
        if (!MigracionCalculos.TryDecimal(cell, out var v) || v < 0 || v > 100)
        { errores.Add(new(fila.Numero, etiqueta, MigracionCalculos.TextoLimpio(cell), $"{etiqueta}: se esperaba un porcentaje entre 0 y 100.")); return null; }
        return (double)v;
    }

    /// <summary>
    /// Unidad de consumo de la fila: "kg" (default con celda vacía) o "qq". Texto no reconocido
    /// agrega error de fila y cae a "kg" (la fila igual se descarta por el corte de errores).
    /// </summary>
    private static string LeerUnidadConsumo(FilaCruda fila, List<MigracionErrorDto> errores)
    {
        var texto = MigracionCalculos.TextoLimpio(Celda(fila, "unidad consumo", "unidad", "unidad de consumo", "unidad medida"));
        var unidad = MigracionCalculos.NormalizarUnidadConsumo(texto);
        if (unidad is null)
        { errores.Add(new(fila.Numero, "Unidad Consumo", texto, "Unidad Consumo: use 'kg' o 'qq' (vacío = kg).")); return "kg"; }
        return unidad;
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

    private static MigracionResultDto ErrorContexto(TipoMigracion tipo, bool dryRun, string mensaje)
        => new(tipo.ToString(), false, 0, 0, 0, "ConErrores", dryRun, new[] { new MigracionErrorDto(0, "Lote", null, mensaje) });

    /// <summary>Persiste el registro de auditoría de la corrida (dry-run o real) a partir del resultado final.</summary>
    private async Task RegistrarAuditoriaAsync(TipoMigracion tipo, int companyId, string nombreArchivo, MigracionResultDto result, CancellationToken ct)
    {
        await _repo.RegistrarAsync(new MigracionMasiva
        {
            CompanyId = companyId,
            Tipo = tipo.ToString(),
            NombreArchivo = nombreArchivo,
            FilasTotales = result.FilasTotales,
            FilasProcesadas = result.FilasProcesadas,
            FilasError = result.FilasError,
            FilasOmitidas = result.FilasOmitidas,
            DuracionMs = result.DuracionMs,
            FueDryRun = result.FueDryRun,
            Estado = result.Estado,
            ErroresJson = result.Errores.Count > 0 ? SerializarErrores(result.Errores) : null,
            FechaProceso = DateTime.UtcNow,
            CreatedByUserId = _current.UserId
        }, ct);
    }
}
