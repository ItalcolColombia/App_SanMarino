// Vacunacion/VacunacionCronogramaService.cs
// Partial 'ancla': campos, ctor y helpers estáticos compartidos.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class VacunacionCronogramaService : IVacunacionCronogramaService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly IFarmService _farmService;

    public VacunacionCronogramaService(ZooSanMarinoContext ctx, ICurrentUser currentUser, IFarmService farmService)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _farmService = farmService;
    }

    private static readonly HashSet<string> LineasValidas = new(StringComparer.Ordinal) { "Levante", "Produccion", "Engorde" };
    private static readonly HashSet<string> UnidadesValidas = new(StringComparer.Ordinal) { "Semana", "Dia", "Fecha" };

    private static void ValidarUnidadObjetivo(string unidadObjetivo, int? valorObjetivo, DateTime? fechaObjetivo)
    {
        if (!UnidadesValidas.Contains(unidadObjetivo))
            throw new InvalidOperationException($"unidadObjetivo inválida: '{unidadObjetivo}'. Debe ser Semana, Dia o Fecha.");

        if (unidadObjetivo is "Semana" or "Dia")
        {
            if (!valorObjetivo.HasValue || valorObjetivo.Value < 1)
                throw new InvalidOperationException($"valorObjetivo es obligatorio (>= 1) cuando unidadObjetivo = '{unidadObjetivo}'.");
            if (fechaObjetivo.HasValue)
                throw new InvalidOperationException("fechaObjetivo no debe indicarse cuando unidadObjetivo = Semana o Dia.");
        }
        else // Fecha
        {
            if (!fechaObjetivo.HasValue)
                throw new InvalidOperationException("fechaObjetivo es obligatoria cuando unidadObjetivo = Fecha.");
            if (valorObjetivo.HasValue)
                throw new InvalidOperationException("valorObjetivo no debe indicarse cuando unidadObjetivo = Fecha.");
        }
    }

    /// <summary>Franja calculada (fecha inicio/fin) de un ítem, dada la fecha de encaset de SU lote (por línea).</summary>
    private static (DateTime Inicio, DateTime Fin) CalcularFranja(VacunacionCronogramaItem item, DateTime? fechaEncaset)
    {
        var f = VacunacionCalculos.CalcularFranja(
            fechaEncaset, item.UnidadObjetivo, item.ValorObjetivo, item.FechaObjetivo,
            item.RangoDiasAntes, item.RangoDiasDespues);
        return (f.FechaInicio, f.FechaFin);
    }

    private static VacunacionRegistroAplicacionDto? MapRegistro(VacunacionRegistroAplicacion? r)
    {
        if (r is null) return null;
        return new VacunacionRegistroAplicacionDto(
            r.Id, r.Estado, r.FechaAplicacion, r.DiasDesviacion, r.Incumplido, r.MotivoDescripcion,
            r.UsuarioRegistraId, UsuarioRegistraNombre: null,
            r.AplicadoPorUserId, AplicadoPorUserNombre: null, r.AplicadoPorNombreLibre);
    }

    /// <summary>internal: reusado por VacunacionRegistroService para devolver el ítem actualizado tras registrar.</summary>
    internal static VacunacionCronogramaItemDto MapItem(
        VacunacionCronogramaItem item, DateTime? fechaEncaset, string loteNombre, string? granjaNombre,
        string itemInventarioNombre)
    {
        var (inicio, fin) = CalcularFranja(item, fechaEncaset);
        var loteId = item.LotePosturaLevanteId ?? item.LotePosturaProduccionId ?? item.LoteAveEngordeId
            ?? throw new InvalidOperationException($"Ítem de cronograma {item.Id} sin lote asociado (dato inconsistente).");

        return new VacunacionCronogramaItemDto(
            item.Id, item.LineaProductiva, loteId, loteNombre,
            item.GranjaId, granjaNombre, item.NucleoId, item.GalponId,
            item.ItemInventarioId, itemInventarioNombre,
            item.UnidadObjetivo, item.ValorObjetivo, item.FechaObjetivo,
            item.RangoDiasAntes, item.RangoDiasDespues, inicio, fin,
            item.Orden, item.Activo, item.Notas,
            MapRegistro(item.RegistroAplicacion));
    }
}
