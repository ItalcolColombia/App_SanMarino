// src/ZooSanMarino.Infrastructure/Services/GuiaGeneticaSantaReyes/Funciones/GuiaGeneticaSantaReyesService.Import.cs
// Import Excel IDEMPOTENTE (upsert por clave natural) + plantilla descargable.
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class GuiaGeneticaSantaReyesService
{
    static GuiaGeneticaSantaReyesService()
    {
        // EPPlus 8 exige declarar la licencia antes del primer uso, igual que los otros 7 services
        // del repo que generan o leen Excel.
        ExcelPackage.License.SetNonCommercialPersonal("ZooSanMarino");
    }

    /// <summary>
    /// Import idempotente.
    ///
    /// <para>
    /// 🔴 <b>El upsert va por <c>codigo_guia_genetica = Raza+AnioGuia+Edad</c></b>, contra el UNIQUE
    /// parcial <c>ux_guia_genetica_santa_reyes_codigo (company_id, codigo_guia_genetica)
    /// WHERE deleted_at IS NULL AND codigo_guia_genetica IS NOT NULL</c>. Reimportar el mismo archivo
    /// <b>actualiza, no duplica</b>: la 2ª pasada da <c>Insertados = 0</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Qué se hace distinto del import de la tabla compartida</b>
    /// (<c>ExcelImportService.ImportProduccionAvicolaFromExcelAsync</c>): allá, si el archivo trae un
    /// valor <i>distinto</i> del guardado, la fila se rechaza con «los campos difieren» y el usuario
    /// no tiene forma de corregir su guía desde el Excel. Acá un valor distinto <b>actualiza</b>,
    /// que es lo que el usuario espera de un import de guía genética; lo que no cambia, no se toca.
    /// </para>
    ///
    /// <para>
    /// El archivo se procesa <b>entero antes de guardar</b> y se guarda de una sola vez: si una fila
    /// del medio revienta, no queda media guía cargada.
    /// </para>
    /// </summary>
    public async Task<GuiaGeneticaSantaReyesImportResultDto> ImportarExcelAsync(
        Stream contenido, string nombreArchivo, long tamanoBytes, CancellationToken ct = default)
    {
        var errores = new List<GuiaGeneticaSantaReyesImportErrorDto>();

        var motivoArchivo = ValidarArchivo(nombreArchivo, tamanoBytes);
        if (motivoArchivo is not null)
            return Fracaso(motivoArchivo);

        var companyId = await GetEffectiveCompanyIdAsync();

        using var package = new ExcelPackage(contenido);

        var hoja = package.Workbook.Worksheets.FirstOrDefault();
        if (hoja?.Dimension is null)
            return Fracaso("El archivo Excel no tiene hojas con datos.");

        var mapaColumnas = MapearColumnas(hoja);
        var faltantes = ColumnasObligatorias.Where(c => !mapaColumnas.ContainsValue(c)).ToList();
        if (faltantes.Count > 0)
            return Fracaso(
                $"El archivo no tiene las columnas obligatorias: {string.Join(", ", faltantes)}. " +
                "Descargue la plantilla para ver el formato esperado.");

        // 1) Leer y validar TODO el archivo antes de tocar la base.
        var filaFinal = hoja.Dimension.Rows;
        var filas = new List<(int NumeroFila, FilaImportGuiaSantaReyes Fila)>();
        var totalFilas = 0;
        var vacias = 0;

        for (var fila = 2; fila <= filaFinal; fila++)
        {
            ct.ThrowIfCancellationRequested();
            totalFilas++;

            var interpretada = GuiaGeneticaSantaReyesCalculos.InterpretarFila(
                Celda(hoja, mapaColumnas, fila, GuiaGeneticaSantaReyesCalculos.ColumnaRaza),
                Celda(hoja, mapaColumnas, fila, GuiaGeneticaSantaReyesCalculos.ColumnaAnioGuia),
                Celda(hoja, mapaColumnas, fila, GuiaGeneticaSantaReyesCalculos.ColumnaEdad),
                Celda(hoja, mapaColumnas, fila, GuiaGeneticaSantaReyesCalculos.ColumnaProdPorcentaje),
                Celda(hoja, mapaColumnas, fila, GuiaGeneticaSantaReyesCalculos.ColumnaRetiroAcH),
                Celda(hoja, mapaColumnas, fila, GuiaGeneticaSantaReyesCalculos.ColumnaGrAveDiaH));

            if (interpretada.EsVacia)
            {
                vacias++;
                continue;
            }

            if (interpretada.Fila is null)
            {
                errores.Add(new GuiaGeneticaSantaReyesImportErrorDto(fila, interpretada.Motivo!));
                continue;
            }

            filas.Add((fila, interpretada.Fila));
        }

        // 2) Un archivo puede repetir la misma clave natural en dos filas. Gana la ÚLTIMA (es lo que
        //    el usuario ve al final de su hoja) y la anterior se reporta, en vez de que las dos
        //    peleen contra el UNIQUE dentro del mismo SaveChanges.
        var porCodigo = new Dictionary<string, (int NumeroFila, FilaImportGuiaSantaReyes Fila)>(StringComparer.Ordinal);
        foreach (var entrada in filas)
        {
            if (porCodigo.TryGetValue(entrada.Fila.Codigo, out var previa))
            {
                errores.Add(new GuiaGeneticaSantaReyesImportErrorDto(
                    previa.NumeroFila,
                    $"La línea «{previa.Fila.Codigo}» está repetida en el archivo (también en la fila " +
                    $"{entrada.NumeroFila}). Se tomó la última."));
            }

            porCodigo[entrada.Fila.Codigo] = entrada;
        }

        if (porCodigo.Count == 0)
            return new GuiaGeneticaSantaReyesImportResultDto(
                Success: false, totalFilas, 0, 0, vacias, errores);

        // 3) Traer de una sola consulta lo que ya existe para esos códigos (la BD filtra, no el
        //    backend: con 615 filas por empresa y varias empresas, traer todo y filtrar en memoria
        //    es la receta conocida para colgar el endpoint).
        var codigos = porCodigo.Keys.ToList();
        var existentes = await Vivas(companyId)
            .Where(g => g.CodigoGuiaGenetica != null && codigos.Contains(g.CodigoGuiaGenetica!))
            .ToDictionaryAsync(g => g.CodigoGuiaGenetica!, g => g, StringComparer.Ordinal, ct);

        var insertados = 0;
        var actualizados = 0;
        var omitidos = vacias;

        foreach (var (_, fila) in porCodigo.Values)
        {
            existentes.TryGetValue(fila.Codigo, out var actual);

            var metricasActuales = actual is null
                ? (MetricasGuiaSantaReyes?)null
                : new MetricasGuiaSantaReyes(actual.ProdPorcentaje, actual.RetiroAcH, actual.GrAveDiaH);

            switch (GuiaGeneticaSantaReyesCalculos.DecidirAccion(metricasActuales, fila.Metricas))
            {
                case AccionImportGuiaSantaReyes.Insertar:
                    var nueva = new GuiaGeneticaSantaReyes
                    {
                        CompanyId = companyId,
                        CreatedByUserId = _currentUser.UserId,
                        ProdPorcentaje = fila.Metricas.ProdPorcentaje,
                        RetiroAcH = fila.Metricas.RetiroAcH,
                        GrAveDiaH = fila.Metricas.GrAveDiaH
                    };
                    AplicarClaveNatural(nueva, fila.Raza, fila.AnioGuia, fila.Edad);
                    _ctx.GuiaGeneticaSantaReyes.Add(nueva);
                    insertados++;
                    break;

                case AccionImportGuiaSantaReyes.Actualizar:
                    actual!.ProdPorcentaje = fila.Metricas.ProdPorcentaje;
                    actual.RetiroAcH = fila.Metricas.RetiroAcH;
                    actual.GrAveDiaH = fila.Metricas.GrAveDiaH;
                    actual.UpdatedByUserId = _currentUser.UserId;
                    actualizados++;
                    break;

                default:
                    // Idéntica a lo guardado: no se toca. Reescribirla marcaría 615 filas como
                    // modificadas en cada reimport y ensuciaría updated_at de toda la guía.
                    omitidos++;
                    break;
            }
        }

        if (insertados > 0 || actualizados > 0)
            await _ctx.SaveChangesAsync(ct);

        return new GuiaGeneticaSantaReyesImportResultDto(
            Success: errores.Count == 0,
            TotalFilas: totalFilas,
            Insertados: insertados,
            Actualizados: actualizados,
            Omitidos: omitidos,
            Errores: errores);
    }

    /// <summary>
    /// Plantilla del import: los seis encabezados en snake_case (los mismos nombres que las columnas
    /// en base) y dos filas de ejemplo, la segunda con <c>prod_porcentaje</c> <b>vacío</b> a
    /// propósito — para que se vea que una métrica sin dato se deja en blanco y NO se escribe 0.
    /// </summary>
    public byte[] GenerarPlantillaExcel()
    {
        using var package = new ExcelPackage();
        var hoja = package.Workbook.Worksheets.Add("GuiaGenetica");

        var encabezados = GuiaGeneticaSantaReyesCalculos.ColumnasPlantilla;
        for (var i = 0; i < encabezados.Count; i++)
        {
            hoja.Cells[1, i + 1].Value = encabezados[i];
            hoja.Cells[1, i + 1].Style.Font.Bold = true;
        }

        var ejemplos = new[]
        {
            new object?[] { "Babcock Brown", "2026", 18, 5.9, 0.0, 95.0 },
            new object?[] { "Criolla", "2026", 101, null, 8.4, 108.0 }
        };

        for (var f = 0; f < ejemplos.Length; f++)
        {
            for (var c = 0; c < ejemplos[f].Length; c++)
            {
                hoja.Cells[f + 2, c + 1].Value = ejemplos[f][c];
            }
        }

        hoja.Cells[hoja.Dimension.Address].AutoFitColumns();

        return package.GetAsByteArray();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Auxiliares del import
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Sin estas tres columnas no hay clave natural que calcular.</summary>
    private static readonly string[] ColumnasObligatorias =
    {
        GuiaGeneticaSantaReyesCalculos.ColumnaRaza,
        GuiaGeneticaSantaReyesCalculos.ColumnaAnioGuia,
        GuiaGeneticaSantaReyesCalculos.ColumnaEdad
    };

    /// <summary>Extensión y tamaño, con el mismo criterio del import compartido.</summary>
    private static string? ValidarArchivo(string? nombreArchivo, long tamanoBytes)
    {
        if (tamanoBytes <= 0) return "El archivo está vacío.";

        if (tamanoBytes > MaxTamanoArchivoBytes)
            return $"El archivo es demasiado grande. Tamaño máximo permitido: {MaxTamanoArchivoBytes / (1024 * 1024)} MB.";

        var extension = Path.GetExtension(nombreArchivo ?? string.Empty).ToLowerInvariant();
        if (!ExtensionesAdmitidas.Contains(extension))
            return $"Formato de archivo no válido. Se permiten: {string.Join(", ", ExtensionesAdmitidas)}.";

        return null;
    }

    /// <summary>Columna del Excel ⇒ nombre canónico. Lo que no se reconoce, se ignora.</summary>
    private static Dictionary<int, string> MapearColumnas(ExcelWorksheet hoja)
    {
        var mapa = new Dictionary<int, string>();
        if (hoja.Dimension is null) return mapa;

        for (var col = 1; col <= hoja.Dimension.Columns; col++)
        {
            var canonico = GuiaGeneticaSantaReyesCalculos.MapearEncabezado(
                hoja.Cells[1, col].Value?.ToString());

            // Si el archivo repite una columna, manda la primera: es la que el usuario ve primero.
            if (canonico is not null && !mapa.ContainsValue(canonico)) mapa[col] = canonico;
        }

        return mapa;
    }

    private static string? Celda(ExcelWorksheet hoja, Dictionary<int, string> mapa, int fila, string columnaCanonica)
    {
        foreach (var (indice, canonico) in mapa)
        {
            if (canonico == columnaCanonica) return hoja.Cells[fila, indice].Value?.ToString()?.Trim();
        }

        return null;
    }

    private static GuiaGeneticaSantaReyesImportResultDto Fracaso(string motivo) =>
        new(
            Success: false,
            TotalFilas: 0,
            Insertados: 0,
            Actualizados: 0,
            Omitidos: 0,
            Errores: new[] { new GuiaGeneticaSantaReyesImportErrorDto(0, motivo) });
}
