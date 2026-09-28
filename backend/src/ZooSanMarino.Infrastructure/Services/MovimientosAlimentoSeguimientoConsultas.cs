using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Consulta compartida por Levante y Producción. PostgreSQL filtra el alcance y devuelve cada día
/// ya clasificado; el backend sólo deserializa el contrato JSON.
/// </summary>
internal static class MovimientosAlimentoSeguimientoConsultas
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<IReadOnlyList<MovimientoAlimentoSeguimientoDto>> ConsultarAsync(
        ZooSanMarinoContext context,
        int companyId,
        int loteId,
        int farmId,
        string? nucleoId,
        string? galponId,
        MovimientosAlimentoSeguimientoCalculos.RangoFechas rango,
        CancellationToken ct = default)
    {
        var dias = await ConsultarPorDiaAsync(
            context, companyId, loteId, farmId, nucleoId, galponId, rango, ct).ConfigureAwait(false);

        return dias
            .SelectMany(d => d.Ingresos.Concat(d.Traslados))
            .OrderBy(m => m.Fecha)
            .ThenBy(m => m.Id)
            .ToList();
    }

    public static async Task<IReadOnlyList<ResumenMovimientosAlimentoDiaDto>> ConsultarPorDiaAsync(
        ZooSanMarinoContext context,
        int companyId,
        int loteId,
        int farmId,
        string? nucleoId,
        string? galponId,
        MovimientosAlimentoSeguimientoCalculos.RangoFechas rango,
        CancellationToken ct = default)
    {
        if (companyId <= 0 || loteId <= 0 || farmId <= 0)
            return [];

        var filas = await context.Database
            .SqlQueryRaw<MovimientoAlimentoDiaSqlRow>(
                "SELECT * FROM public.fn_movimientos_alimento_postura_diario({0}::int, {1}::int, {2}::int, {3}::text, {4}::text, {5}::date, {6}::date)",
                companyId,
                loteId,
                farmId,
                (object?)nucleoId ?? DBNull.Value,
                (object?)galponId ?? DBNull.Value,
                rango.Desde.Date,
                rango.Hasta.Date)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return filas.Select(MapearFila).ToList();
    }

    internal static ResumenMovimientosAlimentoDiaDto MapearFila(MovimientoAlimentoDiaSqlRow fila)
        => new(
            fila.Fecha.Date,
            DeserializarLista<MovimientoAlimentoSeguimientoDto>(fila.IngresosJson),
            DeserializarLista<MovimientoAlimentoSeguimientoDto>(fila.TrasladosJson),
            DeserializarLista<string>(fila.ReferenciasJson));

    private static IReadOnlyList<T> DeserializarLista<T>(string json)
        => JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];

    internal sealed class MovimientoAlimentoDiaSqlRow
    {
        public DateTime Fecha { get; set; }
        public string IngresosJson { get; set; } = "[]";
        public string TrasladosJson { get; set; } = "[]";
        public string ReferenciasJson { get; set; } = "[]";
    }
}
