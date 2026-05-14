// src/ZooSanMarino.Infrastructure/Services/ExportacionExcelService.cs
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

public class ExportacionExcelService : IExportacionExcelService
{
    // ── Colores ──────────────────────────────────────────────────────────────
    private static readonly Color _headerBg      = Color.FromArgb(0x42, 0x42, 0x42);
    private static readonly Color _headerFg      = Color.White;
    private static readonly Color _subBg         = Color.FromArgb(0xE0, 0xE0, 0xE0);
    private static readonly Color _guiaBg        = Color.FromArgb(0xE3, 0xF2, 0xFD);
    private static readonly Color _guiaFg        = Color.FromArgb(0x42, 0x42, 0x42);
    private static readonly Color _verde         = Color.FromArgb(0xC8, 0xE6, 0xC9);
    private static readonly Color _amarillo      = Color.FromArgb(0xFF, 0xF9, 0xC4);
    private static readonly Color _rojo          = Color.FromArgb(0xFF, 0xCD, 0xD2);
    private static readonly Color _infoBg        = Color.FromArgb(0xF5, 0xF5, 0xF5);

    static ExportacionExcelService()
    {
        ExcelPackage.License.SetNonCommercialPersonal("ItalGranja");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // LEVANTE
    // ═════════════════════════════════════════════════════════════════════════

    public byte[] ExportarReporteLevante(ReporteTecnicoLevanteCompletoDto reporte, ExportarExcelMetaDto meta)
    {
        using var pkg = new ExcelPackage();

        _AgregarHojaInformacion(pkg, meta);

        if (reporte.DatosSemanales.Any())
            _AgregarHojaLevanteRealVsGuia(pkg, reporte.DatosSemanales);

        if (reporte.DatosDiarios.Any())
            _AgregarHojaLevanteDiario(pkg, reporte.DatosDiarios);

        return pkg.GetAsByteArray();
    }

    private void _AgregarHojaLevanteRealVsGuia(ExcelPackage pkg, List<ReporteTecnicoLevanteSemanalDto> datos)
    {
        var ws = pkg.Workbook.Worksheets.Add("Real vs Guía");

        // ── Fila 1: título ────────────────────────────────────────────────────
        int totalCols = 22; // Sem, Fecha, SaldoH, SaldoM + 6 grupos × 3 (Real/Guía/Dif)

        ws.Cells[1, 1].Value = "REPORTE LEVANTE — Real vs Guía";
        ws.Cells[1, 1, 1, totalCols].Merge = true;
        _EstiloTitulo(ws.Cells[1, 1]);

        // ── Fila 2: grupos ────────────────────────────────────────────────────
        string[] subHeaders = {
            "Sem", "Fecha", "Saldo H", "Saldo M",
            "Real", "Guía", "Dif",
            "Real", "Guía", "Dif",
            "Real", "Guía", "Dif",
            "Real", "Guía", "Dif",
            "Real", "Guía", "Dif",
            "Real", "Guía", "Dif"
        };

        for (int i = 0; i < subHeaders.Length; i++)
        {
            ws.Cells[2, i + 1].Value = subHeaders[i];
            _EstiloSubHeader(ws.Cells[2, i + 1]);
        }

        // ── Datos ─────────────────────────────────────────────────────────────
        int row = 3;
        foreach (var s in datos)
        {
            // Fila REAL
            ws.Cells[row, 1].Value  = s.Semana;
            ws.Cells[row, 2].Value  = s.Fecha.ToString("dd/MM/yy");
            ws.Cells[row, 3].Value  = s.Hembra;
            ws.Cells[row, 4].Value  = s.SaldoMacho;
            ws.Cells[row, 5].Value  = _Fmt2(s.PorcMortH);
            ws.Cells[row, 8].Value  = _Fmt2(s.PorcMortM);
            ws.Cells[row, 11].Value = _Fmt2(s.ConsAcGrH);
            ws.Cells[row, 14].Value = _Fmt2(s.ConsAcGrM);
            ws.Cells[row, 17].Value = _Fmt2(s.PesoH);
            ws.Cells[row, 20].Value = _Fmt2(s.PesoM);
            _EstiloFila(ws.Cells[row, 1, row, totalCols], Color.White);

            // Fila GUÍA
            ws.Cells[row + 1, 6].Value  = _Fmt2(s.PorcMortHGUIA);
            ws.Cells[row + 1, 9].Value  = _Fmt2(s.PorcMortMGUIA);
            ws.Cells[row + 1, 12].Value = _Fmt2(s.ConsAcGrHGUIA);
            ws.Cells[row + 1, 15].Value = _Fmt2(s.ConsAcGrMGUIA);
            ws.Cells[row + 1, 18].Value = _Fmt2(s.PesoHGUIA);
            ws.Cells[row + 1, 21].Value = _Fmt2(s.PesoMGUIA);
            _EstiloFila(ws.Cells[row + 1, 1, row + 1, totalCols], _guiaBg, _guiaFg, italic: true);

            // Fila DIF
            ws.Cells[row + 2, 7].Value  = _Fmt2(s.DifMortH);
            ws.Cells[row + 2, 10].Value = _Fmt2(s.DifMortM);
            ws.Cells[row + 2, 13].Value = _Fmt2(s.PorcDifConsH);
            ws.Cells[row + 2, 16].Value = _Fmt2(s.DifConsM);
            ws.Cells[row + 2, 19].Value = _Fmt2(s.PorcDifPesoH);
            ws.Cells[row + 2, 22].Value = _Fmt2(s.PorcDifPesoM);
            _ColorearCeldaDif(ws, row + 2, 7,  s.DifMortH,   invert: true);
            _ColorearCeldaDif(ws, row + 2, 10, s.DifMortM,   invert: true);
            _ColorearCeldaDif(ws, row + 2, 13, s.PorcDifConsH);
            _ColorearCeldaDif(ws, row + 2, 16, s.DifConsM);
            _ColorearCeldaDif(ws, row + 2, 19, s.PorcDifPesoH);
            _ColorearCeldaDif(ws, row + 2, 22, s.PorcDifPesoM);

            row += 3;
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns(8, 30);
        ws.View.FreezePanes(3, 1);
    }

    private void _AgregarHojaLevanteDiario(ExcelPackage pkg, List<ReporteTecnicoDiarioLevanteDto> datos)
    {
        var ws = pkg.Workbook.Worksheets.Add("Diario");
        int totalCols = 12;

        ws.Cells[1, 1].Value = "REPORTE LEVANTE — Diario";
        ws.Cells[1, 1, 1, totalCols].Merge = true;
        _EstiloTitulo(ws.Cells[1, 1]);

        string[] headers = { "Fecha", "Edad Días", "Sem.", "Saldo H", "Mort H", "%Mort H", "ConsKg H", "Peso H", "Saldo M", "Mort M", "%Mort M", "ConsKg M" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cells[2, i + 1].Value = headers[i];
            _EstiloSubHeader(ws.Cells[2, i + 1]);
        }

        int row = 3;
        foreach (var d in datos)
        {
            ws.Cells[row, 1].Value  = d.Fecha.ToString("dd/MM/yy");
            ws.Cells[row, 2].Value  = d.EdadDias;
            ws.Cells[row, 3].Value  = d.EdadSemanas;
            ws.Cells[row, 4].Value  = d.SaldoHembras;
            ws.Cells[row, 5].Value  = d.MortalidadHembras;
            ws.Cells[row, 6].Value  = _Fmt2(d.PorcMortH);
            ws.Cells[row, 7].Value  = _Fmt2(d.ConsumoKgH);
            ws.Cells[row, 8].Value  = _Fmt2(d.PesoPromH);
            ws.Cells[row, 9].Value  = d.SaldoMachos;
            ws.Cells[row, 10].Value = d.MortalidadMachos;
            ws.Cells[row, 11].Value = _Fmt2(d.PorcMortM);
            ws.Cells[row, 12].Value = _Fmt2(d.ConsumoKgM);
            _EstiloFila(ws.Cells[row, 1, row, totalCols], row % 2 == 0 ? _infoBg : Color.White);
            row++;
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns(8, 25);
        ws.View.FreezePanes(3, 1);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PRODUCCIÓN TABS
    // ═════════════════════════════════════════════════════════════════════════

    public byte[] ExportarReporteProduccionTabs(ReporteTecnicoProduccionTabsDto reporte, ExportarExcelMetaDto meta)
    {
        using var pkg = new ExcelPackage();

        _AgregarHojaInformacion(pkg, meta);

        if (reporte.DiariosGalpon.Any())
            _AgregarHojaDiarioGalpon(pkg, reporte.DiariosGalpon);

        if (reporte.SemanalesGalpon.Any())
            _AgregarHojaSemanalGalpon(pkg, reporte.SemanalesGalpon);

        if (reporte.DiariosGeneral.Any())
            _AgregarHojaDiarioGeneral(pkg, reporte.DiariosGeneral);

        if (reporte.SemanalesGeneral.Any())
            _AgregarHojaSemanalGeneral(pkg, reporte.SemanalesGeneral);

        return pkg.GetAsByteArray();
    }

    private void _AgregarHojaDiarioGalpon(ExcelPackage pkg, List<ReporteDiarioGalponDto> datos)
    {
        var ws = pkg.Workbook.Worksheets.Add("Diario Galpón");
        int totalCols = 16;

        ws.Cells[1, 1].Value = "PRODUCCIÓN — Diario por Galpón";
        ws.Cells[1, 1, 1, totalCols].Merge = true;
        _EstiloTitulo(ws.Cells[1, 1]);

        string[] headers = {
            "Galpón", "Lote", "Fecha", "Sem.", "Edad",
            "Saldo H", "Saldo M", "Mort H", "Mort M", "%Mort",
            "ConsKg H", "ConsKg M", "Huevo Tot", "Huevo Inc", "%Postura", "Peso Huevo"
        };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cells[2, i + 1].Value = headers[i];
            _EstiloSubHeader(ws.Cells[2, i + 1]);
        }

        // Agrupamos por galpón
        var galponesOrdenados = datos
            .GroupBy(d => d.GalponId)
            .OrderBy(g => g.Key);

        int row = 3;
        foreach (var galpon in galponesOrdenados)
        {
            var rowsGalpon = galpon.OrderBy(d => d.Fecha).ToList();
            string galponNombre = rowsGalpon.First().GalponNombre;

            // Separador de galpón
            ws.Cells[row, 1].Value = $"── {galponNombre} ──";
            ws.Cells[row, 1, row, totalCols].Merge = true;
            ws.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0xBB, 0xDE, 0xFB));
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;

            foreach (var d in rowsGalpon)
            {
                // Fila REAL
                ws.Cells[row, 1].Value  = d.GalponNombre;
                ws.Cells[row, 2].Value  = d.LoteNombre;
                ws.Cells[row, 3].Value  = d.Fecha.ToString("dd/MM/yy");
                ws.Cells[row, 4].Value  = d.SemanaRelativa;
                ws.Cells[row, 5].Value  = d.EdadDias;
                ws.Cells[row, 6].Value  = d.SaldoHembras;
                ws.Cells[row, 7].Value  = d.SaldoMachos;
                ws.Cells[row, 8].Value  = d.MortalidadHembras;
                ws.Cells[row, 9].Value  = d.MortalidadMachos;
                ws.Cells[row, 10].Value = _Fmt2(d.PorcMortalidad);
                ws.Cells[row, 11].Value = _Fmt2(d.ConsKgH);
                ws.Cells[row, 12].Value = _Fmt2(d.ConsKgM);
                ws.Cells[row, 13].Value = d.HuevoTot;
                ws.Cells[row, 14].Value = d.HuevoInc;
                ws.Cells[row, 15].Value = _Fmt2(d.PorcentajePostura);
                ws.Cells[row, 16].Value = _Fmt2(d.PesoHuevo);
                _EstiloFila(ws.Cells[row, 1, row, totalCols], Color.White);

                // Fila GUÍA (si hay)
                if (d.PorcentajePosturaGuia.HasValue || d.PesoHuevoGuia.HasValue)
                {
                    ws.Cells[row + 1, 15].Value = _Fmt2(d.PorcentajePosturaGuia);
                    ws.Cells[row + 1, 16].Value = _Fmt2(d.PesoHuevoGuia);
                    _EstiloFila(ws.Cells[row + 1, 1, row + 1, totalCols], _guiaBg, _guiaFg, italic: true);

                    // Fila DIF
                    ws.Cells[row + 2, 15].Value = _Fmt2(d.DifPostura);
                    ws.Cells[row + 2, 16].Value = _Fmt2(d.DifPesoHuevo);
                    _ColorearCeldaDif(ws, row + 2, 15, d.DifPostura);
                    _ColorearCeldaDif(ws, row + 2, 16, d.DifPesoHuevo);
                    row += 3;
                }
                else
                {
                    row++;
                }
            }
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns(8, 28);
        ws.View.FreezePanes(3, 1);
    }

    private void _AgregarHojaSemanalGalpon(ExcelPackage pkg, List<ReporteSemanalGalponDto> datos)
    {
        var ws = pkg.Workbook.Worksheets.Add("Semanal Galpón");
        int totalCols = 16;

        ws.Cells[1, 1].Value = "PRODUCCIÓN — Semanal por Galpón";
        ws.Cells[1, 1, 1, totalCols].Merge = true;
        _EstiloTitulo(ws.Cells[1, 1]);

        string[] headers = {
            "Galpón", "Lote", "Sem.", "Edad Sem.",
            "Saldo Ini H", "Saldo Ini M", "Saldo Fin H", "Saldo Fin M",
            "Mort H", "Mort M", "%Mort",
            "ConsKg H", "ConsKg M",
            "Huevo Tot", "%Postura", "Peso Huevo"
        };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cells[2, i + 1].Value = headers[i];
            _EstiloSubHeader(ws.Cells[2, i + 1]);
        }

        var galponesOrdenados = datos
            .GroupBy(d => d.GalponId)
            .OrderBy(g => g.Key);

        int row = 3;
        foreach (var galpon in galponesOrdenados)
        {
            var rowsGalpon = galpon.OrderBy(d => d.Semana).ToList();
            string galponNombre = rowsGalpon.First().GalponNombre;

            ws.Cells[row, 1].Value = $"── {galponNombre} ──";
            ws.Cells[row, 1, row, totalCols].Merge = true;
            ws.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0xBB, 0xDE, 0xFB));
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;

            foreach (var d in rowsGalpon)
            {
                ws.Cells[row, 1].Value  = d.GalponNombre;
                ws.Cells[row, 2].Value  = d.LoteNombre;
                ws.Cells[row, 3].Value  = d.Semana;
                ws.Cells[row, 4].Value  = d.EdadSemanas;
                ws.Cells[row, 5].Value  = d.SaldoInicioHembras;
                ws.Cells[row, 6].Value  = d.SaldoInicioMachos;
                ws.Cells[row, 7].Value  = d.SaldoFinHembras;
                ws.Cells[row, 8].Value  = d.SaldoFinMachos;
                ws.Cells[row, 9].Value  = d.MortalidadHembrasSemanal;
                ws.Cells[row, 10].Value = d.MortalidadMachosSemanal;
                ws.Cells[row, 11].Value = _Fmt2(d.PorcMortalidadSemanal);
                ws.Cells[row, 12].Value = _Fmt2(d.ConsKgHSemanal);
                ws.Cells[row, 13].Value = _Fmt2(d.ConsKgMSemanal);
                ws.Cells[row, 14].Value = d.HuevoTotSemanal;
                ws.Cells[row, 15].Value = _Fmt2(d.PorcentajePosturaPromedio);
                ws.Cells[row, 16].Value = _Fmt2(d.PesoHuevoPromedio);
                _EstiloFila(ws.Cells[row, 1, row, totalCols], Color.White);

                if (d.PorcentajePosturaGuia.HasValue || d.PesoHuevoGuia.HasValue)
                {
                    ws.Cells[row + 1, 15].Value = _Fmt2(d.PorcentajePosturaGuia);
                    ws.Cells[row + 1, 16].Value = _Fmt2(d.PesoHuevoGuia);
                    _EstiloFila(ws.Cells[row + 1, 1, row + 1, totalCols], _guiaBg, _guiaFg, italic: true);

                    ws.Cells[row + 2, 15].Value = _Fmt2(d.DifPostura);
                    ws.Cells[row + 2, 16].Value = _Fmt2(d.DifPesoHuevo);
                    _ColorearCeldaDif(ws, row + 2, 15, d.DifPostura);
                    _ColorearCeldaDif(ws, row + 2, 16, d.DifPesoHuevo);
                    row += 3;
                }
                else
                {
                    row++;
                }
            }
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns(8, 28);
        ws.View.FreezePanes(3, 1);
    }

    private void _AgregarHojaDiarioGeneral(ExcelPackage pkg, List<ReporteGeneralDiarioDto> datos)
    {
        var ws = pkg.Workbook.Worksheets.Add("Diario General");
        int totalCols = 13;

        ws.Cells[1, 1].Value = "PRODUCCIÓN — Diario General (Consolidado)";
        ws.Cells[1, 1, 1, totalCols].Merge = true;
        _EstiloTitulo(ws.Cells[1, 1]);

        string[] headers = {
            "Fecha", "Sem.", "Edad", "Saldo H", "Saldo M",
            "Mort H", "Mort M", "ConsKg H", "ConsKg M",
            "Huevo Tot", "%Postura", "Peso Huevo", "Postura Guía"
        };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cells[2, i + 1].Value = headers[i];
            _EstiloSubHeader(ws.Cells[2, i + 1]);
        }

        int row = 3;
        foreach (var d in datos.OrderBy(x => x.Fecha))
        {
            ws.Cells[row, 1].Value  = d.Fecha.ToString("dd/MM/yy");
            ws.Cells[row, 2].Value  = d.SemanaRelativa;
            ws.Cells[row, 3].Value  = d.EdadDias;
            ws.Cells[row, 4].Value  = d.SaldoTotalHembras;
            ws.Cells[row, 5].Value  = d.SaldoTotalMachos;
            ws.Cells[row, 6].Value  = d.MortalidadTotalHembras;
            ws.Cells[row, 7].Value  = d.MortalidadTotalMachos;
            ws.Cells[row, 8].Value  = _Fmt2(d.ConsKgHTotalKg);
            ws.Cells[row, 9].Value  = _Fmt2(d.ConsKgMTotalKg);
            ws.Cells[row, 10].Value = d.HuevosTotTotal;
            ws.Cells[row, 11].Value = _Fmt2(d.PorcentajePosturaPromedio);
            ws.Cells[row, 12].Value = _Fmt2(d.PesoHuevoPromedio);
            ws.Cells[row, 13].Value = _Fmt2(d.PorcentajePosturaGuia);
            _EstiloFila(ws.Cells[row, 1, row, totalCols], Color.White);

            if (d.DifPostura.HasValue)
            {
                ws.Cells[row + 1, 11].Value = _Fmt2(d.DifPostura);
                _ColorearCeldaDif(ws, row + 1, 11, d.DifPostura);
                _EstiloFila(ws.Cells[row + 1, 1, row + 1, totalCols], _guiaBg, _guiaFg);
                row += 2;
            }
            else
            {
                row++;
            }
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns(8, 25);
        ws.View.FreezePanes(3, 1);
    }

    private void _AgregarHojaSemanalGeneral(ExcelPackage pkg, List<ReporteGeneralSemanalDto> datos)
    {
        var ws = pkg.Workbook.Worksheets.Add("Semanal General");
        int totalCols = 14;

        ws.Cells[1, 1].Value = "PRODUCCIÓN — Semanal General (Consolidado)";
        ws.Cells[1, 1, 1, totalCols].Merge = true;
        _EstiloTitulo(ws.Cells[1, 1]);

        string[] headers = {
            "Sem.", "Edad Sem.", "Saldo Ini H", "Saldo Ini M", "Saldo Fin H", "Saldo Fin M",
            "Mort H", "Mort M", "ConsKg H", "ConsKg M",
            "Huevo Tot", "%Postura", "Peso Huevo", "Postura Guía"
        };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cells[2, i + 1].Value = headers[i];
            _EstiloSubHeader(ws.Cells[2, i + 1]);
        }

        int row = 3;
        foreach (var d in datos.OrderBy(x => x.Semana))
        {
            ws.Cells[row, 1].Value  = d.Semana;
            ws.Cells[row, 2].Value  = d.EdadSemanas;
            ws.Cells[row, 3].Value  = d.SaldoInicioHembras;
            ws.Cells[row, 4].Value  = d.SaldoInicioMachos;
            ws.Cells[row, 5].Value  = d.SaldoFinHembras;
            ws.Cells[row, 6].Value  = d.SaldoFinMachos;
            ws.Cells[row, 7].Value  = d.MortalidadTotalHembras;
            ws.Cells[row, 8].Value  = d.MortalidadTotalMachos;
            ws.Cells[row, 9].Value  = _Fmt2(d.ConsKgHTotal);
            ws.Cells[row, 10].Value = _Fmt2(d.ConsKgMTotal);
            ws.Cells[row, 11].Value = d.HuevosTotTotal;
            ws.Cells[row, 12].Value = _Fmt2(d.PorcentajePosturaPromedio);
            ws.Cells[row, 13].Value = _Fmt2(d.PesoHuevoPromedio);
            ws.Cells[row, 14].Value = _Fmt2(d.PorcentajePosturaGuia);
            _EstiloFila(ws.Cells[row, 1, row, totalCols], Color.White);

            if (d.DifPostura.HasValue || d.DifPesoHuevo.HasValue)
            {
                ws.Cells[row + 1, 12].Value = _Fmt2(d.DifPostura);
                ws.Cells[row + 1, 13].Value = _Fmt2(d.DifPesoHuevo);
                _ColorearCeldaDif(ws, row + 1, 12, d.DifPostura);
                _ColorearCeldaDif(ws, row + 1, 13, d.DifPesoHuevo);
                _EstiloFila(ws.Cells[row + 1, 1, row + 1, totalCols], _guiaBg, _guiaFg);
                row += 2;
            }
            else
            {
                row++;
            }
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns(8, 25);
        ws.View.FreezePanes(3, 1);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // HOJA INFORMACIÓN (común)
    // ═════════════════════════════════════════════════════════════════════════

    private void _AgregarHojaInformacion(ExcelPackage pkg, ExportarExcelMetaDto meta)
    {
        var ws = pkg.Workbook.Worksheets.Add("Información");

        ws.Cells[1, 1].Value = "INFORMACIÓN DEL REPORTE";
        ws.Cells[1, 1, 1, 2].Merge = true;
        _EstiloTitulo(ws.Cells[1, 1]);

        int row = 2;
        _InfoFila(ws, row++, "Fase / Etapa",    meta.Etapa);
        _InfoFila(ws, row++, "Lote Base",        meta.LoteBaseNombre);
        _InfoFila(ws, row++, "Lote / Sublote",   meta.LoteSubloteNombre ?? "—");
        _InfoFila(ws, row++, "Granja",           meta.GranjaNombre    ?? "—");
        _InfoFila(ws, row++, "Núcleo",           meta.NucleoNombre    ?? "—");
        _InfoFila(ws, row++, "Fecha Inicio",     meta.FechaInicio?.ToString("dd/MM/yyyy") ?? "—");
        _InfoFila(ws, row++, "Fecha Fin",        meta.FechaFin?.ToString("dd/MM/yyyy")    ?? "—");
        _InfoFila(ws, row++, "Total Aves Inicio", meta.TotalAvesInicio?.ToString() ?? "—");
        _InfoFila(ws, row++, "Periodicidad",     meta.Periodicidad ?? "—");
        _InfoFila(ws, row++, "Fecha Descarga",   DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));

        ws.Column(1).Width = 22;
        ws.Column(2).Width = 30;
    }

    private static void _InfoFila(ExcelWorksheet ws, int row, string clave, string valor)
    {
        ws.Cells[row, 1].Value = clave;
        ws.Cells[row, 2].Value = valor;
        ws.Cells[row, 1].Style.Font.Bold = true;
        ws.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        ws.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0xEE, 0xEE, 0xEE));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // HELPERS DE ESTILO
    // ═════════════════════════════════════════════════════════════════════════

    private static void _EstiloTitulo(ExcelRange cell)
    {
        cell.Style.Font.Bold     = true;
        cell.Style.Font.Size     = 14;
        cell.Style.Font.Color.SetColor(Color.White);
        cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
        cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0x42, 0x42, 0x42));
        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    }

    private static void _EstiloSubHeader(ExcelRange cell)
    {
        cell.Style.Font.Bold     = true;
        cell.Style.Font.Size     = 10;
        cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
        cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0xE0, 0xE0, 0xE0));
        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        cell.Style.WrapText = true;
    }

    private static void _EstiloFila(ExcelRange range, Color bg, Color? fg = null, bool italic = false)
    {
        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
        range.Style.Fill.BackgroundColor.SetColor(bg);
        if (fg.HasValue)
            range.Style.Font.Color.SetColor(fg.Value);
        if (italic)
            range.Style.Font.Italic = true;
        range.Style.Border.Bottom.Style = ExcelBorderStyle.Hair;
    }

    private void _ColorearCeldaDif(ExcelWorksheet ws, int row, int col, double? dif, bool invert = false)
    {
        var cell = ws.Cells[row, col];
        cell.Style.Fill.PatternType = ExcelFillStyle.Solid;

        if (!dif.HasValue)
        {
            cell.Style.Fill.BackgroundColor.SetColor(Color.White);
            return;
        }

        double abs = Math.Abs(dif.Value);
        // Para mortalidad, un valor negativo (real < guía) es mejor
        bool esBueno = invert ? dif.Value <= 0 : dif.Value >= 0;

        Color color;
        if (abs <= 5)
            color = esBueno ? _verde : _verde;   // dentro de rango → siempre verde
        else if (abs <= 15)
            color = _amarillo;
        else
            color = _rojo;

        cell.Style.Fill.BackgroundColor.SetColor(color);
        cell.Style.Font.Bold = abs > 15;
    }

    private static object? _Fmt2(double? v) =>
        v.HasValue ? Math.Round(v.Value, 2) : (object?)null;

    private static object? _Fmt2(double v) => Math.Round(v, 2);
}
