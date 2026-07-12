// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.VentaEngorde.cs
// Línea Engorde · Venta (histórico). Elegibilidad reutiliza ElegiblesEngordeAsync (lotes no cerrados).
// Parse/validación en C#; la INSERCIÓN la hace la función plpgsql fn_migracion_venta_engorde
// (estado 'Completado' + numero MPE + descuento del contador del lote una vez; el trigger de BD
// escribe el histórico VENTA_AVES). Idempotente en la función por (lote + fecha + cantidades).
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MigracionService
{
    // ── Import ───────────────────────────────────────────────────────────────
    private async Task<MigracionResultDto> ProcesarVentaEngordeAsync(IFormFile file, bool dryRun, int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        const TipoMigracion tipo = TipoMigracion.VentaPolloEngorde;
        if (ctx.LoteId is not int loteId) return ErrorContexto(tipo, dryRun, "Seleccioná un lote de engorde antes de importar.");
        var (lote, errLote) = await ResolverLoteEngordeAsync(companyId, loteId, ct);
        if (lote is null) return ErrorContexto(tipo, dryRun, errLote!);

        using var stream = file.OpenReadStream();
        var filas = LeerDatos(stream, "Datos");
        if (filas.Count == 0) return ResultadoVacio(tipo, dryRun);

        var errores = new List<MigracionErrorDto>();
        var filasJson = new List<Dictionary<string, object?>>();

        foreach (var fila in filas)
        {
            if (!MigracionCalculos.TryFecha(Celda(fila, "fecha"), out var fecha))
            { errores.Add(new(fila.Numero, "Fecha", null, "Fecha inválida o faltante.")); continue; }

            int e0 = errores.Count;
            var cantH = EnteroNoNeg(fila, errores, "Cantidad H", "cantidad h", "cant h", "hembras");
            var cantM = EnteroNoNeg(fila, errores, "Cantidad M", "cantidad m", "cant m", "machos");
            var cantX = EnteroNoNeg(fila, errores, "Cantidad Mixtas", "cantidad mixtas", "cant mixtas", "mixtas");
            var pesoBruto = DobleOpc(fila, errores, "Peso Bruto (kg)", "peso bruto (kg)", "peso bruto");
            var pesoTara = DobleOpc(fila, errores, "Peso Tara (kg)", "peso tara (kg)", "peso tara");
            var edad = EnteroNoNegNull(fila, errores, "Edad Aves", "edad aves", "edad");
            if (errores.Count > e0) continue;

            if (cantH + cantM + cantX <= 0)
            { errores.Add(new(fila.Numero, "Cantidad", null, "Debe vender al menos un ave (Cantidad H/M/Mixtas).")); continue; }
            if (pesoBruto is double b && pesoTara is double t && b < t)
            { errores.Add(new(fila.Numero, "Peso", $"{b}/{t}", "El peso bruto no puede ser menor que la tara.")); continue; }

            filasJson.Add(new Dictionary<string, object?>
            {
                ["lote_id"] = loteId,
                ["fecha"] = fecha.ToString("yyyy-MM-dd"),
                ["cant_h"] = cantH, ["cant_m"] = cantM, ["cant_x"] = cantX,
                ["motivo"] = MigracionCalculos.TextoLimpio(Celda(fila, "motivo")),
                ["observaciones"] = MigracionCalculos.TextoLimpio(Celda(fila, "observaciones")),
                ["peso_bruto"] = pesoBruto, ["peso_tara"] = pesoTara,
                ["edad_aves"] = edad,
                ["raza"] = MigracionCalculos.TextoLimpio(Celda(fila, "raza")),
                ["placa"] = MigracionCalculos.TextoLimpio(Celda(fila, "placa"))
            });
        }

        return await EjecutarHistoricoAsync(tipo, dryRun, companyId, file.FileName, filas.Count, errores, filasJson,
            json => _ctx.Database.SqlQueryRaw<int>(
                "SELECT public.fn_migracion_venta_engorde({0}, {1}, {2}::jsonb) AS \"Value\"",
                companyId, _current.UserId, json).FirstAsync(ct), ct);
    }

    // ── Plantilla ────────────────────────────────────────────────────────────
    private async Task<(byte[] Contenido, string NombreArchivo)> GenerarPlantillaVentaEngordeAsync(int companyId, MigracionContextoDto ctx, CancellationToken ct)
    {
        if (ctx.LoteId is not int loteId)
            throw new ArgumentException("Seleccioná un lote de engorde para descargar su plantilla.");
        var (lote, errLote) = await ResolverLoteEngordeAsync(companyId, loteId, ct);
        if (lote is null) throw new InvalidOperationException(errLote!);

        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Datos");
        PonerEncabezados(ws, "Fecha", "Cantidad H", "Cantidad M", "Cantidad Mixtas", "Motivo",
            "Peso Bruto (kg)", "Peso Tara (kg)", "Edad Aves", "Raza", "Placa", "Observaciones");

        HojaInstrucciones(pkg, $"Migración Venta Engorde — Lote {lote.LoteNombre} (id {loteId})",
            "Una fila por venta en la hoja 'Datos'. Todas las filas corresponden a ESTE lote.",
            "• Fecha: obligatoria (aaaa-mm-dd o dd/mm/aaaa).",
            "• Cantidad H / M / Mixtas: enteros ≥ 0 (vacío = 0); debe venderse al menos un ave por fila.",
            "• Peso Bruto / Peso Tara: opcionales, en kg (bruto ≥ tara). El neto y el promedio por ave se calculan.",
            "• Motivo / Edad Aves / Raza / Placa / Observaciones: opcionales.",
            "La venta se carga en estado 'Completado' y descuenta las aves del lote. Es idempotente: la misma venta (fecha + cantidades) no se duplica.");

        return (Finalizar(pkg), $"VentaEngorde_Lote{loteId}_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
