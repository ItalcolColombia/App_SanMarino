// MovimientoPolloEngorde/Funciones/MovimientoPolloEngordeService.OrganizarPeso.cs
// Organizar/recalcular peso prorrateado por lote en despachos.
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

public partial class MovimientoPolloEngordeService
{
    /// <inheritdoc />
    public async Task<OrganizarPesoResponse> OrganizarPesoAsync(OrganizarPesoRequest request)
    {
        var companyId = _currentUser.CompanyId;

        // Traer todas las ventas con peso registrado que aplican al scope pedido.
        var query = _ctx.MovimientoPolloEngorde
            .Where(m =>
                m.CompanyId == companyId
                && m.DeletedAt == null
                && (m.TipoMovimiento == "Venta" || m.TipoMovimiento == "Despacho" || m.TipoMovimiento == "Retiro")
                && (m.PesoBruto != null || m.PesoTara != null));

        if (request.GranjaId.HasValue)
        {
            var gId = request.GranjaId.Value;
            query = query.Where(m => m.GranjaOrigenId == gId);
        }

        if (!request.ReprocesarTodo)
            query = query.Where(m => m.PesoNetoGlobal == null);

        var movimientos = await query
            .OrderBy(m => m.NumeroDespacho ?? "")
            .ThenBy(m => m.Id)
            .ToListAsync();

        if (movimientos.Count == 0)
        {
            return new OrganizarPesoResponse
            {
                DryRun = request.DryRun,
                Mensaje = request.ReprocesarTodo
                    ? "No hay ventas con peso registrado en el scope indicado."
                    : "No hay ventas pendientes de organizar (todas ya tienen PesoNetoGlobal asignado). Use ReprocesarTodo=true para forzar.",
                DespachosProcesados = 0,
                MovimientosActualizados = 0,
                MovimientosOmitidos = 0
            };
        }

        // Agrupar por NumeroDespacho. Los que no tienen NumeroDespacho se tratan uno a uno (su propio "despacho").
        var grupos = movimientos
            .GroupBy(m => m.NumeroDespacho ?? $"__sin_despacho_{m.Id}__")
            .ToList();

        var despachos = new List<OrganizarPesoDespachoDetalle>();
        var totalActualizados = 0;
        var totalOmitidos = 0;

        foreach (var grupo in grupos)
        {
            var items = grupo.ToList();

            // El peso global lo tomamos del primer movimiento del grupo (deberían ser iguales en todos).
            var pesoBruto = items.FirstOrDefault(m => m.PesoBruto.HasValue)?.PesoBruto ?? 0d;
            var pesoTara = items.FirstOrDefault(m => m.PesoTara.HasValue)?.PesoTara ?? 0d;

            if (pesoBruto == 0d && pesoTara == 0d)
            {
                totalOmitidos += items.Count;
                continue;
            }

            if (pesoBruto < pesoTara)
                pesoTara = pesoBruto; // evitar neto negativo en datos corruptos; registrar sin error.

            var pesoNetoGlobal = pesoBruto - pesoTara;
            var totalAves = items.Sum(m => m.TotalAves);
            var pesoPorAve = totalAves > 0 ? pesoNetoGlobal / totalAves : 0d;

            var detalle = new OrganizarPesoDespachoDetalle
            {
                NumeroDespacho = items[0].NumeroDespacho,
                CantidadMovimientos = items.Count,
                TotalAves = totalAves,
                PesoBrutoGlobal = pesoBruto > 0 ? pesoBruto : null,
                PesoTaraGlobal = pesoTara > 0 ? pesoTara : null,
                PesoNetoGlobal = pesoNetoGlobal,
                PesoPorAve = pesoPorAve > 0 ? pesoPorAve : null,
                MovimientoIds = items.Select(m => m.Id).ToList()
            };
            despachos.Add(detalle);

            if (!request.DryRun)
            {
                foreach (var m in items)
                {
                    m.PesoBrutoGlobal = pesoBruto > 0 ? pesoBruto : null;
                    m.PesoTaraGlobal = pesoTara > 0 ? pesoTara : null;
                    m.PesoNetoGlobal = pesoNetoGlobal;
                    m.PesoNeto = pesoPorAve > 0 ? m.TotalAves * pesoPorAve : null;
                    m.PromedioPesoAve = pesoPorAve > 0 ? pesoPorAve : null;
                    m.UpdatedByUserId = _currentUser.UserId;
                    m.UpdatedAt = DateTime.UtcNow;
                }
            }

            totalActualizados += items.Count;
        }

        if (!request.DryRun && totalActualizados > 0)
            await _ctx.SaveChangesAsync();

        var modo = request.DryRun ? "Simulación" : "Aplicado";
        return new OrganizarPesoResponse
        {
            DryRun = request.DryRun,
            DespachosProcesados = despachos.Count,
            MovimientosActualizados = totalActualizados,
            MovimientosOmitidos = totalOmitidos,
            Mensaje = $"{modo}: {despachos.Count} despacho(s), {totalActualizados} movimiento(s) {(request.DryRun ? "a procesar" : "actualizados")}.",
            Despachos = despachos
        };
    }
}
