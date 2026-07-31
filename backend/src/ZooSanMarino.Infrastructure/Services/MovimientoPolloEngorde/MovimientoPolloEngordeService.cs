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

    /// <summary>Peso báscula obligatorio en ventas — delega en el cálculo puro compartido.</summary>
    private static void ValidarPesoObligatorioEnVenta(string? tipoMovimiento, double? pesoBruto, double? pesoTara, bool pesoDiferidoPermitido = false)
        => MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta(tipoMovimiento, pesoBruto, pesoTara, pesoDiferidoPermitido);

    /// <summary>
    /// ¿La empresa DUEÑA de la granja tiene el peso báscula diferido (<c>venta_engorde_peso_diferido</c>)?
    /// Se resuelve por DATOS (<c>farms.company_id</c>), no por país ni por la empresa activa del token:
    /// la granja de origen es el ancla de la venta. <b>Fail-closed</b>: sin granja, granja inexistente
    /// o empresa sin el flag ⇒ <c>false</c> ⇒ peso obligatorio (comportamiento histórico).
    /// </summary>
    private async Task<bool> EmpresaPermitePesoDiferidoAsync(int? granjaId)
    {
        if (granjaId is not int id) return false;
        return await _ctx.Farms
            .Where(f => f.Id == id)
            .Join(_ctx.Companies, f => f.CompanyId, c => c.Id, (_, c) => c.VentaEngordePesoDiferido)
            .FirstOrDefaultAsync();
    }

    /// <summary>Appends text without exceeding DB column length (keeps suffix when truncating).</summary>
    private static string AppendObservaciones(string? existing, string suffix)
    {
        var combined = (existing ?? "") + suffix;
        if (combined.Length <= MaxObservacionesLen) return combined;
        return combined[^MaxObservacionesLen..];
    }

    /// <summary>
    /// Gate B8 — liquidación congelada: ningún movimiento/venta puede tocar un lote LIQUIDADO
    /// (crear, editar, cancelar, eliminar o completar mutan <c>lote_ave_engorde</c> y el histórico
    /// que alimenta la copia). Recibe los lotes involucrados (origen y/o destino) y lanza con el
    /// PRIMER lote cerrado que encuentre, identificándolo por nombre.
    /// <para>
    /// <paramref name="omitirGateLiquidado"/> es el bypass EXPLÍCITO de la corrección de aves
    /// disponibles (<c>CorreccionAvesDisponiblesEngordeService</c>), que existe justamente para
    /// reparar lotes liquidados y re-congela la copia al terminar.
    /// </para>
    /// </summary>
    private async Task ValidarLotesNoLiquidadosAsync(IEnumerable<int?> loteIds, bool omitirGateLiquidado = false)
    {
        if (omitirGateLiquidado) return;
        var ids = loteIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0) return;

        var cerrado = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId.HasValue && ids.Contains(l.LoteAveEngordeId.Value)
                     && l.DeletedAt == null
                     && l.EstadoOperativoLote.ToLower() == "cerrado")
            .Select(l => l.LoteNombre)
            .FirstOrDefaultAsync();
        if (cerrado is not null)
            LiquidacionCongeladaGateCalculos.ValidarEscritura(
                LiquidacionCongeladaGateCalculos.EstadoCerrado,
                OperacionLoteEngordeLiquidado.MovimientoAves,
                loteNombre: cerrado);
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
