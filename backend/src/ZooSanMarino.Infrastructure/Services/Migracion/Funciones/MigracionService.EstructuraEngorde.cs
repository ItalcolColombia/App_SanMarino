// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.EstructuraEngorde.cs
// Línea Engorde · Lotes — patrón Estructura. Valida las filas del Excel (reporte completo,
// all-or-nothing) e inserta REUTILIZANDO ILoteAveEngordeService.CreateAsync (que valida granja/
// núcleo/galpón/guía genética y crea el HistorialLotePolloEngorde "Inicio") dentro de una transacción.
// No duplica reglas de negocio.
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MigracionService
{
    // Combinaciones válidas Raza+Año de las guías genéticas de la empresa (clásica + Ecuador activa).
    // Mismo criterio que LoteAveEngordeService.ExisteGuiaGeneticaRazaAnioAsync.
    private async Task<List<(string Raza, string Anio)>> CargarRazaAnioGuiaAsync(int companyId, CancellationToken ct)
    {
        var clasica = await _ctx.ProduccionAvicolaRaw.AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.DeletedAt == null && p.Raza != null && p.AnioGuia != null)
            .Select(p => new { p.Raza, p.AnioGuia })
            .Distinct().ToListAsync(ct);

        var ecuador = await _ctx.GuiaGeneticaEcuadorHeader.AsNoTracking()
            .Where(h => h.CompanyId == companyId && h.DeletedAt == null && h.Estado == "active")
            .Select(h => new { h.Raza, h.AnioGuia })
            .Distinct().ToListAsync(ct);

        var combos = new List<(string Raza, string Anio)>();
        var vistos = new HashSet<string>();
        void Add(string? raza, string? anio)
        {
            if (string.IsNullOrWhiteSpace(raza) || string.IsNullOrWhiteSpace(anio)) return;
            var r = raza.Trim();
            var a = anio.Trim();
            if (vistos.Add($"{r.ToLowerInvariant()}|{a}")) combos.Add((r, a));
        }
        foreach (var x in clasica) Add(x.Raza, x.AnioGuia);
        foreach (var x in ecuador) Add(x.Raza, x.AnioGuia.ToString());
        return combos.OrderBy(c => c.Raza).ThenBy(c => c.Anio).ToList();
    }

    // Clave de matching raza+año idéntica al servicio (raza .Trim().ToLower() + año como string).
    private static string ClaveRazaAnio(string raza, int anio) => $"{raza.Trim().ToLowerInvariant()}|{anio}";

    // ── Import ───────────────────────────────────────────────────────────────
    private async Task<MigracionResultDto> ProcesarLotesPolloEngordeAsync(IFormFile file, bool dryRun, int companyId, CancellationToken ct)
    {
        const TipoMigracion tipo = TipoMigracion.LotesPolloEngorde;
        using var stream = file.OpenReadStream();
        var filas = LeerDatos(stream, "Datos");
        if (filas.Count == 0) return ResultadoVacio(tipo, dryRun);

        var granjaPorNombre = (await CargarGranjasAsync(companyId, ct))
            .GroupBy(g => MigracionCalculos.NormalizarClave(g.Name)).ToDictionary(g => g.Key, g => g.First().Id);
        var allowedGranjaIds = (await _farmService.GetAllAsync(_current.UserGuid, companyId)).Select(f => f.Id).ToHashSet();
        var nucleoSet = (await CargarNucleosAsync(companyId, ct))
            .Select(n => $"{n.GranjaId}|{MigracionCalculos.NormalizarClave(n.NucleoId)}").ToHashSet();
        var galponPorClave = (await CargarGalponesAsync(companyId, ct))
            .GroupBy(g => MigracionCalculos.NormalizarClave(g.GalponId)).ToDictionary(g => g.Key, g => g.First());
        // Clave = raza normalizada + año (string de la guía). El año de fila (int) matchea vía su ToString.
        var razaAnioSet = (await CargarRazaAnioGuiaAsync(companyId, ct))
            .Select(c => $"{c.Raza.Trim().ToLowerInvariant()}|{c.Anio}").ToHashSet();

        var errores = new List<MigracionErrorDto>();
        var dtos = new List<CreateLoteAveEngordeDto>();
        var vistosNombre = new HashSet<string>();

        foreach (var fila in filas)
        {
            var nombre = MigracionCalculos.TextoLimpio(Celda(fila, "lote", "nombre lote", "nombre"));
            var granjaNombre = MigracionCalculos.TextoLimpio(Celda(fila, "granja"));
            var nucleoCodigo = MigracionCalculos.TextoLimpio(Celda(fila, "nucleo"));
            var galponCodigo = MigracionCalculos.TextoLimpio(Celda(fila, "galpon", "galpón"));
            var raza = MigracionCalculos.TextoLimpio(Celda(fila, "raza"));

            if (nombre is null) { errores.Add(new(fila.Numero, "Lote", null, "El nombre del lote es obligatorio.")); continue; }
            if (granjaNombre is null) { errores.Add(new(fila.Numero, "Granja", null, "La granja es obligatoria.")); continue; }
            if (!granjaPorNombre.TryGetValue(MigracionCalculos.NormalizarClave(granjaNombre), out var granjaId))
            { errores.Add(new(fila.Numero, "Granja", granjaNombre, "La granja no existe en la empresa.")); continue; }
            if (!allowedGranjaIds.Contains(granjaId))
            { errores.Add(new(fila.Numero, "Granja", granjaNombre, "La granja no está asignada a tu usuario.")); continue; }

            if (nucleoCodigo is not null && !nucleoSet.Contains($"{granjaId}|{MigracionCalculos.NormalizarClave(nucleoCodigo)}"))
            { errores.Add(new(fila.Numero, "Núcleo", nucleoCodigo, "El núcleo no existe en esa granja.")); continue; }

            if (galponCodigo is not null)
            {
                if (!galponPorClave.TryGetValue(MigracionCalculos.NormalizarClave(galponCodigo), out var g))
                { errores.Add(new(fila.Numero, "Galpón", galponCodigo, "El galpón no existe en la empresa.")); continue; }
                if (g.GranjaId != granjaId)
                { errores.Add(new(fila.Numero, "Galpón", galponCodigo, "El galpón no pertenece a la granja indicada.")); continue; }
                if (nucleoCodigo is not null && MigracionCalculos.NormalizarClave(g.NucleoId) != MigracionCalculos.NormalizarClave(nucleoCodigo))
                { errores.Add(new(fila.Numero, "Galpón", galponCodigo, "El galpón no pertenece al núcleo indicado.")); continue; }
                nucleoCodigo ??= g.NucleoId; // se infiere del galpón, igual que el servicio
            }

            if (raza is null) { errores.Add(new(fila.Numero, "Raza", null, "La raza es obligatoria.")); continue; }
            int e0 = errores.Count;
            var anio = EnteroNoNegNull(fila, errores, "Año Tabla", "ano tabla", "año tabla", "anio tabla genetica");
            if (errores.Count > e0) continue;
            if (anio is null or <= 0) { errores.Add(new(fila.Numero, "Año Tabla", null, "El año de tabla genética es obligatorio (> 0).")); continue; }
            if (!razaAnioSet.Contains(ClaveRazaAnio(raza, anio.Value)))
            { errores.Add(new(fila.Numero, "Raza / Año", $"{raza} / {anio}", "La combinación raza + año no existe en la guía genética de la empresa.")); continue; }

            var claveNombre = MigracionCalculos.NormalizarClave(nombre);
            if (!vistosNombre.Add(claveNombre)) { errores.Add(new(fila.Numero, "Lote", nombre, "Lote duplicado en el archivo (mismo nombre).")); continue; }

            int e1 = errores.Count;
            var fechaEncaset = FechaOpc(fila, errores, "Fecha Encaset", "fecha encaset", "fecha de encaset");
            var hembras = EnteroNoNegNull(fila, errores, "Hembras", "hembras", "hembras l");
            var machos = EnteroNoNegNull(fila, errores, "Machos", "machos", "machos l");
            var mixtas = EnteroNoNegNull(fila, errores, "Mixtas", "mixtas");
            var encasetadas = EnteroNoNegNull(fila, errores, "Aves Encasetadas", "aves encasetadas", "encasetadas");
            var pesoIniH = DobleOpc(fila, errores, "Peso Inicial H (g)", "peso inicial h (g)", "peso inicial h");
            var pesoIniM = DobleOpc(fila, errores, "Peso Inicial M (g)", "peso inicial m (g)", "peso inicial m");
            var edadInicial = EnteroNoNegNull(fila, errores, "Edad Inicial", "edad inicial");
            if (errores.Count > e1) continue;

            dtos.Add(new CreateLoteAveEngordeDto
            {
                LoteNombre = nombre,
                GranjaId = granjaId,
                NucleoId = nucleoCodigo,
                GalponId = galponCodigo,
                Raza = raza,
                AnoTablaGenetica = anio,
                FechaEncaset = fechaEncaset,
                HembrasL = hembras,
                MachosL = machos,
                Mixtas = mixtas,
                AvesEncasetadas = encasetadas,
                PesoInicialH = pesoIniH,
                PesoInicialM = pesoIniM,
                EdadInicial = edadInicial,
                Tecnico = MigracionCalculos.TextoLimpio(Celda(fila, "tecnico", "técnico")),
                LoteErp = MigracionCalculos.TextoLimpio(Celda(fila, "lote erp", "erp"))
            });
        }

        return await EjecutarImportacionAsync(tipo, dryRun, companyId, file.FileName, filas.Count, errores, dtos,
            dto => _loteAveEngordeService.CreateAsync(dto), ct);
    }

    // ── Plantilla ────────────────────────────────────────────────────────────
    private async Task<(byte[] Contenido, string NombreArchivo)> GenerarPlantillaLotesPolloEngordeAsync(int companyId, CancellationToken ct)
    {
        var granjas = await CargarGranjasAsync(companyId, ct);
        var nucleos = await CargarNucleosAsync(companyId, ct);
        var galpones = await CargarGalponesAsync(companyId, ct);
        var combos = await CargarRazaAnioGuiaAsync(companyId, ct);
        var nombrePorGranja = granjas.ToDictionary(g => g.Id, g => g.Name);
        var razas = combos.Select(c => c.Raza).Distinct().ToList();
        var anios = combos.Select(c => c.Anio).Distinct().OrderBy(a => a).ToList();

        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Datos");
        PonerEncabezados(ws, "Lote", "Granja", "Núcleo", "Galpón", "Raza", "Año Tabla", "Fecha Encaset",
            "Hembras", "Machos", "Mixtas", "Aves Encasetadas", "Peso Inicial H (g)", "Peso Inicial M (g)", "Edad Inicial", "Técnico", "Lote ERP");

        var wsRef = pkg.Workbook.Worksheets.Add("Referencias");
        EscribirColumnaRef(wsRef, 1, "Granjas de la empresa", granjas.Select(g => g.Name));
        // Núcleos por granja
        wsRef.Cells[1, 3].Value = "Granja"; wsRef.Cells[1, 3].Style.Font.Bold = true;
        wsRef.Cells[1, 4].Value = "Código Núcleo"; wsRef.Cells[1, 4].Style.Font.Bold = true;
        int rr = 2;
        foreach (var n in nucleos)
        {
            wsRef.Cells[rr, 3].Value = nombrePorGranja.TryGetValue(n.GranjaId, out var gn) ? gn : n.GranjaId.ToString();
            wsRef.Cells[rr, 4].Value = n.NucleoId;
            rr++;
        }
        // Galpones por granja
        wsRef.Cells[1, 6].Value = "Granja"; wsRef.Cells[1, 6].Style.Font.Bold = true;
        wsRef.Cells[1, 7].Value = "Código Galpón"; wsRef.Cells[1, 7].Style.Font.Bold = true;
        rr = 2;
        foreach (var g in galpones)
        {
            wsRef.Cells[rr, 6].Value = nombrePorGranja.TryGetValue(g.GranjaId, out var gn) ? gn : g.GranjaId.ToString();
            wsRef.Cells[rr, 7].Value = g.GalponId;
            rr++;
        }
        EscribirColumnaRef(wsRef, 9, "Razas (guía)", razas);
        EscribirColumnaRef(wsRef, 10, "Años (guía)", anios.Select(a => a));
        // Combinaciones válidas Raza + Año
        wsRef.Cells[1, 12].Value = "Raza + Año válidos (guía)"; wsRef.Cells[1, 12].Style.Font.Bold = true;
        rr = 2;
        foreach (var c in combos) { wsRef.Cells[rr, 12].Value = $"{c.Raza} — {c.Anio}"; rr++; }

        if (granjas.Count > 0) DropdownRango(ws, "B", $"Referencias!$A$2:$A${granjas.Count + 1}");
        if (razas.Count > 0) DropdownRango(ws, "E", $"Referencias!$I$2:$I${razas.Count + 1}");
        if (anios.Count > 0) DropdownRango(ws, "F", $"Referencias!$J$2:$J${anios.Count + 1}");

        HojaInstrucciones(pkg, "Migración de Lotes de Pollo Engorde",
            "Una fila por lote en la hoja 'Datos'.",
            "• Lote: obligatorio (nombre del lote).",
            "• Granja: elegí una de la lista (debe estar asignada a tu usuario).",
            "• Núcleo / Galpón: opcionales. Si ponés galpón, debe pertenecer a la granja (y al núcleo si lo indicás); el núcleo se infiere del galpón.",
            "• Raza + Año Tabla: obligatorios y deben existir juntos en la guía genética (mirá 'Raza + Año válidos (guía)' en 'Referencias').",
            "• Fecha Encaset: opcional (aaaa-mm-dd o dd/mm/aaaa). Hembras/Machos/Mixtas/Aves Encasetadas/Pesos/Edad Inicial: opcionales (≥ 0).",
            "Al importar se crea el lote y su registro histórico inicial de aves. Luego subí el archivo en el módulo de Migraciones.");

        return (Finalizar(pkg), $"LotesEngorde_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
