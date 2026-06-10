// MovimientoPolloEngordePanama/MovimientoPolloEngordePanamaService.cs
// Procesos de venta de pollo engorde específicos de Panamá (lógica separada por país).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public sealed class MovimientoPolloEngordePanamaService : IMovimientoPolloEngordePanamaService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly IMovimientoPolloEngordeService _shared;

    public MovimientoPolloEngordePanamaService(
        ZooSanMarinoContext ctx,
        ICurrentUser currentUser,
        IMovimientoPolloEngordeService shared)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _shared = shared;
    }

    /// <inheritdoc />
    public async Task<VentaGranjaDespachoResultDto> CreateVentaPanamaDespachoAsync(CreateVentaPanamaDespachoDto dto)
    {
        if (dto.Lineas == null || dto.Lineas.Count == 0)
            throw new InvalidOperationException("Debe indicar al menos una línea.");

        var lineas = dto.Lineas
            .Where(l => l.CantidadHembras + l.CantidadMachos > 0)
            .ToList();
        if (lineas.Count == 0)
            throw new InvalidOperationException("Debe asignar hembras/machos en al menos un lote.");

        var idsLote = lineas.Select(l => l.LoteAveEngordeOrigenId).ToList();
        if (idsLote.Count != idsLote.Distinct().Count())
            throw new InvalidOperationException("No puede repetirse el mismo lote en más de una línea.");

        // Disponibilidad: en venta Panamá, H+M se asignan SOBRE las mixtas del lote.
        var disp = await _shared.GetAvesDisponiblesLotesAsync(new AvesDisponiblesLotesRequest
        {
            TipoLote = "LoteAveEngorde",
            LoteIds = idsLote
        });
        var dispById = (disp.Items ?? new List<AvesDisponiblesLotePorIdDto>())
            .Where(x => x.Disponibles != null)
            .ToDictionary(x => x.LoteId, x => x.Disponibles!);
        foreach (var linea in lineas)
        {
            if (!dispById.TryGetValue(linea.LoteAveEngordeOrigenId, out var d))
                throw new InvalidOperationException($"No se pudo calcular disponibilidad del lote {linea.LoteAveEngordeOrigenId}.");
            var pedidoMixtas = linea.CantidadHembras + linea.CantidadMachos;
            if (pedidoMixtas > d.MixtasDisponibles)
                throw new InvalidOperationException(
                    $"No hay mixtas suficientes en el lote '{d.NombreLote ?? linea.LoteAveEngordeOrigenId.ToString()}'. " +
                    $"Asignado H+M={pedidoMixtas}; Mixtas disponibles={d.MixtasDisponibles}.");
        }

        // Peso prorrateado por línea (mismo cálculo que la venta por granja).
        var pesoBrutoGlobal = dto.PesoBruto ?? 0d;
        var pesoTaraGlobal = dto.PesoTara ?? 0d;
        var tienePeso = dto.PesoBruto.HasValue || dto.PesoTara.HasValue;
        if (tienePeso && pesoBrutoGlobal < pesoTaraGlobal)
            throw new InvalidOperationException("El peso bruto no puede ser menor que el peso tara.");
        var pesoNetoGlobal = pesoBrutoGlobal - pesoTaraGlobal;
        var avesPorLinea = lineas.Select(l => l.CantidadHembras + l.CantidadMachos).ToList();
        var prorrateo = tienePeso
            ? MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea(pesoBrutoGlobal, pesoTaraGlobal, avesPorLinea)
            : new MovimientoPolloEngordeCalculos.PesoLineaProrrateado[lineas.Count];

        var facturaId = Guid.NewGuid();
        var totalPollos = dto.TotalPollosGalpon.HasValue ? (int?)Math.Round(dto.TotalPollosGalpon.Value) : null;
        var usuarioId = dto.UsuarioMovimientoId > 0 ? dto.UsuarioMovimientoId : _currentUser.UserId;

        await using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            var nuevos = new List<MovimientoPolloEngorde>(lineas.Count);
            for (int i = 0; i < lineas.Count; i++)
            {
                var l = lineas[i];
                var m = new MovimientoPolloEngorde
                {
                    FechaMovimiento = dto.FechaMovimiento,
                    TipoMovimiento = dto.TipoMovimiento,
                    LoteAveEngordeOrigenId = l.LoteAveEngordeOrigenId,
                    GranjaOrigenId = l.GranjaOrigenId ?? dto.GranjaOrigenId,
                    NucleoOrigenId = l.NucleoOrigenId,
                    GalponOrigenId = l.GalponOrigenId,
                    // Split asignado sobre mixtas: se guarda en H/M (reporte) y el stock sale de mixtas.
                    CantidadHembras = l.CantidadHembras,
                    CantidadMachos = l.CantidadMachos,
                    CantidadMixtas = 0,
                    EsVentaMixta = true,
                    MotivoMovimiento = dto.MotivoMovimiento,
                    Descripcion = dto.Descripcion,
                    Observaciones = dto.Observaciones,
                    Estado = "Pendiente",
                    UsuarioMovimientoId = usuarioId,
                    FacturaId = facturaId,
                    NumeroDespacho = dto.NumeroDespacho,
                    EdadAves = dto.EdadAves,
                    TotalPollosGalpon = totalPollos,
                    Raza = dto.Raza,
                    Placa = dto.Placa,
                    HoraSalida = dto.HoraSalida,
                    GuiaAgrocalidad = dto.GuiaAgrocalidad,
                    Sellos = dto.Sellos,
                    Ayuno = dto.Ayuno,
                    Conductor = dto.Conductor,
                    PesoBruto = dto.PesoBruto,
                    PesoTara = dto.PesoTara,
                    PesoBrutoGlobal = tienePeso ? dto.PesoBruto : null,
                    PesoTaraGlobal = tienePeso ? dto.PesoTara : null,
                    PesoNetoGlobal = tienePeso ? pesoNetoGlobal : null,
                    PesoBrutoReal = prorrateo[i].Bruto,
                    PesoTaraReal = prorrateo[i].Tara,
                    PesoNeto = prorrateo[i].Neto,
                    PromedioPesoAve = prorrateo[i].Promedio,
                    CompanyId = _currentUser.CompanyId,
                    CreatedByUserId = _currentUser.UserId,
                    CreatedAt = DateTime.UtcNow
                };
                _ctx.MovimientoPolloEngorde.Add(m);
                nuevos.Add(m);
            }

            await _ctx.SaveChangesAsync();
            foreach (var m in nuevos)
                m.NumeroMovimiento = $"MPE-{DateTime.UtcNow:yyyyMMdd}-{m.Id:D6}";
            await _ctx.SaveChangesAsync();
            await tx.CommitAsync();

            var result = new VentaGranjaDespachoResultDto();
            foreach (var m in nuevos)
            {
                var d = await _shared.GetByIdAsync(m.Id);
                if (d != null) result.Movimientos.Add(d);
            }
            return result;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
