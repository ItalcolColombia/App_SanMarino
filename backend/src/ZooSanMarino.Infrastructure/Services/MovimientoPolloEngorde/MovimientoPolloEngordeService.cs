// MovimientoPolloEngorde/MovimientoPolloEngordeService.cs
// Partial 'ancla': campos, ctor, constantes y helpers estáticos compartidos.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using PagedResultCommon = ZooSanMarino.Application.DTOs.Common.PagedResult<ZooSanMarino.Application.DTOs.MovimientoPolloEngordeDto>;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoPolloEngordeService : IMovimientoPolloEngordeService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;

    public MovimientoPolloEngordeService(ZooSanMarinoContext ctx, ICurrentUser currentUser)
    {
        _ctx = ctx;
        _currentUser = currentUser;
    }

    /// <summary>Must match observaciones column max length in EF configuration (1000).</summary>
    private const int MaxObservacionesLen = 1000;

    private static bool EsSalidaVenta(string? tipoMovimiento)
        => tipoMovimiento == "Venta" || tipoMovimiento == "Despacho" || tipoMovimiento == "Retiro";

    /// <summary>Appends text without exceeding DB column length (keeps suffix when truncating).</summary>
    private static string AppendObservaciones(string? existing, string suffix)
    {
        var combined = (existing ?? "") + suffix;
        if (combined.Length <= MaxObservacionesLen) return combined;
        return combined[^MaxObservacionesLen..];
    }

    /// <summary>
    /// Cantidades que realmente afectan el stock del lote (descuento/crédito/reserva). En ventas
    /// Panamá (<see cref="MovimientoPolloEngorde.EsVentaMixta"/>) las aves viven en HembrasL/MachosL,
    /// así que el efecto es (H, M, 0) — se descuenta de cada columna directamente; Mixtas no cambia.
    /// </summary>
    private static (int H, int M, int X) CantidadesEfectivasEnLote(MovimientoPolloEngorde m)
        => m.EsVentaMixta
            ? (m.CantidadHembras, m.CantidadMachos, 0)
            : (m.CantidadHembras, m.CantidadMachos, m.CantidadMixtas);
}
