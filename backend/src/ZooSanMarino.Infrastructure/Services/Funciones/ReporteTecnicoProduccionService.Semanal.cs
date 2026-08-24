// src/ZooSanMarino.Infrastructure/Services/Funciones/ReporteTecnicoProduccionService.Semanal.cs
// Consolidacion semanal del reporte consolidado (agrupa varios LPP del mismo lote base por semana completa).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteTecnicoProduccionService
{
    private async Task<List<ReporteTecnicoProduccionSemanalDto>> ConsolidarSemanalesConsolidadoAsync(
        List<ReporteTecnicoProduccionDiarioDto> datosConsolidados,
        DateTime fechaInicioProduccion,
        List<Lote> sublotes,
        CancellationToken ct)
    {
        // Para consolidación, solo consolidar semanas completas donde TODOS los sublotes tengan 7 días
        var semanasCompletas = new List<int>();
        
        var semanasUnicas = datosConsolidados.Select(d => d.Semana).Distinct().OrderBy(s => s).ToList();
        
        foreach (var semana in semanasUnicas)
        {
            var esCompleta = await EsSemanaCompletaConsolidadaAsync(semana, sublotes, fechaInicioProduccion, ct);
            if (esCompleta)
                semanasCompletas.Add(semana);
        }

        // Filtrar datos solo para semanas completas
        var datosFiltrados = datosConsolidados
            .Where(d => semanasCompletas.Contains(d.Semana))
            .ToList();

        return ConsolidarSemanales(datosFiltrados, fechaInicioProduccion);
    }

    private async Task<bool> EsSemanaCompletaConsolidadaAsync(
        int semana,
        List<Lote> sublotes,
        DateTime fechaInicioProduccion,
        CancellationToken ct)
    {
        foreach (var sublote in sublotes)
        {
            var loteProd = await ObtenerLoteProduccionAsync(sublote, ct);
            var fechaInicioSublote = loteProd?.FechaInicioProduccion ?? sublote.FechaEncaset ?? DateTime.Today;
            var loteIdSeguimiento = (loteProd ?? sublote).LoteId ?? sublote.LoteId;

            var edadInicioSemana = (semana - 1) * 7;
            var fechaInicioSemana = fechaInicioSublote.AddDays(edadInicioSemana);
            var fechaFinSemana = fechaInicioSemana.AddDays(6);

            var edadFinSemana = CalcularEdadDias(fechaInicioSublote, fechaFinSemana);
            if (edadFinSemana < 6)
                return false;

            if (!loteIdSeguimiento.HasValue)
                return false;
            var diasConDatos = await _ctx.SeguimientoDiario
                .AsNoTracking()
                .Where(s => s.TipoSeguimiento == TipoProduccion &&
                            s.LoteId == loteIdSeguimiento.Value.ToString() &&
                            s.Fecha >= fechaInicioSemana &&
                            s.Fecha <= fechaFinSemana)
                .CountAsync(ct);

            if (diasConDatos < 7)
                return false;
        }

        return true;
    }

    /// <summary>Consolida semanas para reporte con sublotes LPP (lote_postura_produccion).</summary>
    private async Task<List<ReporteTecnicoProduccionSemanalDto>> ConsolidarSemanalesConsolidadoLPPAsync(
        List<ReporteTecnicoProduccionDiarioDto> datosConsolidados,
        DateTime fechaInicioProduccion,
        List<LotePosturaProduccion> sublotesLpp,
        CancellationToken ct)
    {
        var semanasCompletas = new List<int>();
        var semanasUnicas = datosConsolidados.Select(d => d.Semana).Distinct().OrderBy(s => s).ToList();

        foreach (var semana in semanasUnicas)
        {
            var esCompleta = await EsSemanaCompletaConsolidadaLPPAsync(semana, sublotesLpp, fechaInicioProduccion, ct);
            if (esCompleta)
                semanasCompletas.Add(semana);
        }

        var datosFiltrados = datosConsolidados
            .Where(d => semanasCompletas.Contains(d.Semana))
            .ToList();

        return ConsolidarSemanales(datosFiltrados, fechaInicioProduccion);
    }

    private async Task<bool> EsSemanaCompletaConsolidadaLPPAsync(
        int semana,
        List<LotePosturaProduccion> sublotesLpp,
        DateTime fechaInicioProduccion,
        CancellationToken ct)
    {
        foreach (var sublote in sublotesLpp)
        {
            var lppId = sublote.LotePosturaProduccionId ?? 0;
            if (lppId <= 0) return false;

            var fechaInicioSublote = sublote.FechaInicioProduccion ?? sublote.FechaEncaset ?? DateTime.Today;
            var edadInicioSemana = Math.Max(0, (semana - 26) * 7);
            var fechaInicioSemana = fechaInicioSublote.AddDays(edadInicioSemana);
            var fechaFinSemana = fechaInicioSemana.AddDays(6);

            var edadFinSemana = CalcularEdadDias(fechaInicioSublote, fechaFinSemana);
            if (edadFinSemana < 6) return false;

            var diasConDatos = await _ctx.SeguimientoDiario
                .AsNoTracking()
                .Where(s => s.TipoSeguimiento == TipoProduccion &&
                            s.LotePosturaProduccionId == lppId &&
                            s.Fecha >= fechaInicioSemana &&
                            s.Fecha <= fechaFinSemana)
                .CountAsync(ct);

            if (diasConDatos < 7)
                return false;
        }

        return true;
    }

    /// <summary>Obtiene el lote en fase Producción (mismo lote o hijo). Opción B.</summary>
    private async Task<Lote?> ObtenerLoteProduccionAsync(Lote lote, CancellationToken ct = default)
    {
        if (lote.Fase == "Produccion" && lote.LoteId.HasValue)
            return lote;
        if (!lote.LoteId.HasValue) return null;
        return await _ctx.Lotes
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LotePadreId == lote.LoteId && l.Fase == "Produccion" && l.DeletedAt == null, ct);
    }
}
