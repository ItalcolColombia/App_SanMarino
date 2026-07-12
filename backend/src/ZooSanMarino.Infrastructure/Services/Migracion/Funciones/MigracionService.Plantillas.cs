// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.Plantillas.cs
// Generación de plantillas .xlsx (EPPlus) por tipo de estructura. Cada plantilla tiene:
//   • Hoja "Datos"        → la llena el usuario (encabezados + validaciones/dropdowns).
//   • Hoja "Referencias"  → datos existentes de la empresa (para elegir de la lista).
//   • Hoja "Instrucciones"→ cómo completar.
using OfficeOpenXml;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MigracionService
{
    // ── Helpers EPPlus ───────────────────────────────────────────────────────
    private static void PonerEncabezados(ExcelWorksheet ws, params string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cells[1, i + 1];
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
        }
    }

    private static void EscribirColumnaRef(ExcelWorksheet wsRef, int col, string titulo, IEnumerable<string> valores)
    {
        wsRef.Cells[1, col].Value = titulo;
        wsRef.Cells[1, col].Style.Font.Bold = true;
        int r = 2;
        foreach (var v in valores) wsRef.Cells[r++, col].Value = v;
    }

    private static void DropdownRango(ExcelWorksheet ws, string colLetra, string rangoFormula, int hastaFila = 1000)
    {
        var dv = ws.DataValidations.AddListValidation($"{colLetra}2:{colLetra}{hastaFila}");
        dv.Formula.ExcelFormula = rangoFormula;
        dv.ShowErrorMessage = true;
        dv.ErrorTitle = "Valor inválido";
        dv.Error = "Elegí un valor de la lista desplegable.";
    }

    private static void DropdownInline(ExcelWorksheet ws, string colLetra, int hastaFila, params string[] valores)
    {
        var dv = ws.DataValidations.AddListValidation($"{colLetra}2:{colLetra}{hastaFila}");
        foreach (var v in valores) dv.Formula.Values.Add(v);
        dv.ShowErrorMessage = true;
    }

    private static void HojaInstrucciones(ExcelPackage pkg, string titulo, params string[] lineas)
    {
        var ws = pkg.Workbook.Worksheets.Add("Instrucciones");
        ws.Cells[1, 1].Value = titulo;
        ws.Cells[1, 1].Style.Font.Bold = true;
        ws.Cells[1, 1].Style.Font.Size = 14;
        int r = 3;
        foreach (var l in lineas) ws.Cells[r++, 1].Value = l;
        ws.Column(1).Width = 110;
    }

    private static byte[] Finalizar(ExcelPackage pkg)
    {
        foreach (var ws in pkg.Workbook.Worksheets)
            if (ws.Dimension is not null) ws.Cells.AutoFitColumns();
        return pkg.GetAsByteArray();
    }

    // ── Núcleos ──────────────────────────────────────────────────────────────
    private async Task<(byte[] Contenido, string NombreArchivo)> GenerarPlantillaNucleosAsync(int companyId, CancellationToken ct)
    {
        var granjas = await CargarGranjasAsync(companyId, ct);
        using var pkg = new ExcelPackage();

        var ws = pkg.Workbook.Worksheets.Add("Datos");
        PonerEncabezados(ws, "Granja", "Código Núcleo", "Nombre");

        var wsRef = pkg.Workbook.Worksheets.Add("Referencias");
        EscribirColumnaRef(wsRef, 1, "Granjas de la empresa", granjas.Select(g => g.Name));
        if (granjas.Count > 0) DropdownRango(ws, "A", $"Referencias!$A$2:$A${granjas.Count + 1}");

        HojaInstrucciones(pkg, "Migración de Núcleos",
            "Una fila por núcleo en la hoja 'Datos'.",
            "• Granja: elegí una de la lista (granjas existentes de tu empresa).",
            "• Código Núcleo: identificador del núcleo (lo definís vos; único dentro de la granja).",
            "• Nombre: nombre descriptivo del núcleo.",
            "Luego subí el archivo en el módulo de Migraciones (Validar para revisar, Importar para cargar).");

        return (Finalizar(pkg), $"Nucleos_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    // ── Galpones ─────────────────────────────────────────────────────────────
    private async Task<(byte[] Contenido, string NombreArchivo)> GenerarPlantillaGalponesAsync(int companyId, CancellationToken ct)
    {
        var granjas = await CargarGranjasAsync(companyId, ct);
        var nucleos = await CargarNucleosAsync(companyId, ct);
        var nombrePorGranja = granjas.ToDictionary(g => g.Id, g => g.Name);
        using var pkg = new ExcelPackage();

        var ws = pkg.Workbook.Worksheets.Add("Datos");
        PonerEncabezados(ws, "Granja", "Núcleo", "Código Galpón", "Nombre", "Ancho", "Largo", "Tipo Galpón");

        var wsRef = pkg.Workbook.Worksheets.Add("Referencias");
        EscribirColumnaRef(wsRef, 1, "Granjas de la empresa", granjas.Select(g => g.Name));
        // Tabla de núcleos por granja (para consultar el código correcto)
        wsRef.Cells[1, 3].Value = "Granja"; wsRef.Cells[1, 3].Style.Font.Bold = true;
        wsRef.Cells[1, 4].Value = "Código Núcleo"; wsRef.Cells[1, 4].Style.Font.Bold = true;
        wsRef.Cells[1, 5].Value = "Nombre Núcleo"; wsRef.Cells[1, 5].Style.Font.Bold = true;
        int rr = 2;
        foreach (var n in nucleos)
        {
            wsRef.Cells[rr, 3].Value = nombrePorGranja.TryGetValue(n.GranjaId, out var gn) ? gn : n.GranjaId.ToString();
            wsRef.Cells[rr, 4].Value = n.NucleoId;
            wsRef.Cells[rr, 5].Value = n.NucleoNombre;
            rr++;
        }
        if (granjas.Count > 0) DropdownRango(ws, "A", $"Referencias!$A$2:$A${granjas.Count + 1}");

        HojaInstrucciones(pkg, "Migración de Galpones",
            "Una fila por galpón en la hoja 'Datos'.",
            "• Granja: elegí una de la lista.",
            "• Núcleo: el CÓDIGO del núcleo (mirá la tabla de la hoja 'Referencias' para el código correcto de cada granja).",
            "• Código Galpón: opcional. Si lo dejás vacío, el sistema genera uno automáticamente (G0001, G0002…).",
            "• Nombre: obligatorio. Ancho/Largo/Tipo Galpón: opcionales.",
            "Luego subí el archivo en el módulo de Migraciones.");

        return (Finalizar(pkg), $"Galpones_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    // ── Granjas ──────────────────────────────────────────────────────────────
    private async Task<(byte[] Contenido, string NombreArchivo)> GenerarPlantillaGranjasAsync(int companyId, CancellationToken ct)
    {
        var departamentos = await CargarDepartamentosAsync(companyId, ct);
        var municipios = await CargarMunicipiosAsync(departamentos.Select(d => d.DepartamentoId).ToList(), ct);
        var regionales = await CargarRegionalesAsync(companyId, ct);
        var regionalesNombres = regionales.Select(r => r.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        var nombreDept = departamentos.ToDictionary(d => d.DepartamentoId, d => d.DepartamentoNombre);
        using var pkg = new ExcelPackage();

        var ws = pkg.Workbook.Worksheets.Add("Datos");
        PonerEncabezados(ws, "Nombre", "Departamento", "Ciudad", "Regional", "Estado");

        var wsRef = pkg.Workbook.Worksheets.Add("Referencias");
        EscribirColumnaRef(wsRef, 1, "Departamentos", departamentos.Select(d => d.DepartamentoNombre));
        EscribirColumnaRef(wsRef, 3, "Regionales", regionalesNombres);
        // Tabla Departamento → Municipio (para elegir la ciudad correcta)
        wsRef.Cells[1, 5].Value = "Departamento"; wsRef.Cells[1, 5].Style.Font.Bold = true;
        wsRef.Cells[1, 6].Value = "Municipio"; wsRef.Cells[1, 6].Style.Font.Bold = true;
        int rr = 2;
        foreach (var m in municipios)
        {
            wsRef.Cells[rr, 5].Value = nombreDept.TryGetValue(m.DepartamentoId, out var dn) ? dn : m.DepartamentoId.ToString();
            wsRef.Cells[rr, 6].Value = m.MunicipioNombre;
            rr++;
        }

        if (departamentos.Count > 0) DropdownRango(ws, "B", $"Referencias!$A$2:$A${departamentos.Count + 1}");
        if (regionalesNombres.Count > 0) DropdownRango(ws, "D", $"Referencias!$C$2:$C${regionalesNombres.Count + 1}");
        DropdownInline(ws, "E", 1000, "A", "I");

        HojaInstrucciones(pkg, "Migración de Granjas",
            "Una fila por granja en la hoja 'Datos'.",
            "• Nombre: obligatorio, único por empresa.",
            "• Departamento: elegí uno de la lista (según el país de tu empresa).",
            "• Ciudad: el municipio. Consultá la tabla Departamento→Municipio en 'Referencias' para el nombre correcto.",
            "• Regional: obligatoria; elegí una de la lista.",
            "• Estado: A (activa) o I (inactiva). Por defecto A.",
            "Luego subí el archivo en el módulo de Migraciones.");

        return (Finalizar(pkg), $"Granjas_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
