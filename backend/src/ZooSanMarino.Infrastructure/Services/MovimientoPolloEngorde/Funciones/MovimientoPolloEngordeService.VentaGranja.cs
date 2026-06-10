// MovimientoPolloEngorde/Funciones/MovimientoPolloEngordeService.VentaGranja.cs
// Venta por granja: despacho multi-lote en una sola operación.
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
    public async Task<VentaGranjaDespachoResultDto> CreateVentaGranjaDespachoAsync(CreateVentaGranjaDespachoDto dto)
    {
        if (dto.Lineas == null || dto.Lineas.Count == 0)
            throw new InvalidOperationException("Debe indicar al menos una línea.");

        var lineas = dto.Lineas
            .Where(l => l.CantidadHembras + l.CantidadMachos + l.CantidadMixtas > 0)
            .ToList();
        if (lineas.Count == 0)
            throw new InvalidOperationException("Debe indicar al menos una línea con cantidades mayores a cero.");

        var idsLote = lineas.Select(l => l.LoteAveEngordeOrigenId).ToList();
        if (idsLote.Count != idsLote.Distinct().Count())
            throw new InvalidOperationException("No puede repetirse el mismo lote en más de una línea.");

        // Validación previa: disponibilidad por lote considerando reservas Pendiente.
        var disp = await GetAvesDisponiblesLotesAsync(new AvesDisponiblesLotesRequest
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
            // Con PermitirSobrante=true no se rechaza: el sobrante por línea lo calcula y persiste CreateAsync (Parte B / R2).
            if (!dto.PermitirSobrante &&
                (linea.CantidadHembras > d.HembrasDisponibles || linea.CantidadMachos > d.MachosDisponibles || linea.CantidadMixtas > d.MixtasDisponibles))
            {
                throw new InvalidOperationException(
                    $"No hay aves suficientes disponibles para el lote '{d.NombreLote ?? linea.LoteAveEngordeOrigenId.ToString()}'. " +
                    $"Solicitado (H/M/X)={linea.CantidadHembras}/{linea.CantidadMachos}/{linea.CantidadMixtas}; " +
                    $"Disponible (H/M/X)={d.HembrasDisponibles}/{d.MachosDisponibles}/{d.MixtasDisponibles}. " +
                    $"Reservado Pendiente total={d.TotalReservadasPendiente}.");
            }
        }

        // Calcular peso global y prorrateado por ave antes de entrar a la transacción.
        var pesoBrutoGlobal = dto.PesoBruto ?? 0d;
        var pesoTaraGlobal = dto.PesoTara ?? 0d;
        var tienePeso = dto.PesoBruto.HasValue || dto.PesoTara.HasValue;

        if (tienePeso && pesoBrutoGlobal < pesoTaraGlobal)
            throw new InvalidOperationException("El peso bruto no puede ser menor que el peso tara.");

        var pesoNetoGlobal = pesoBrutoGlobal - pesoTaraGlobal;
        var n = lineas.Count;

        // Prorrateo de peso bruto/tara/neto por línea (con ajuste de residuo); cálculo puro y testeable.
        var avesPorLinea = lineas
            .Select(l => l.CantidadHembras + l.CantidadMachos + l.CantidadMixtas)
            .ToList();
        var prorrateo = tienePeso
            ? MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea(pesoBrutoGlobal, pesoTaraGlobal, avesPorLinea)
            : new MovimientoPolloEngordeCalculos.PesoLineaProrrateado[n];

        // Factura única del despacho (Parte C / R3.3): todas las líneas comparten este UID.
        var facturaId = Guid.NewGuid();

        await using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            var results = new List<MovimientoPolloEngordeDto>();
            for (int i = 0; i < n; i++)
            {
                var linea  = lineas[i];
                var single = new CreateMovimientoPolloEngordeDto
                {
                    FacturaId = facturaId,
                    PermitirSobrante = dto.PermitirSobrante,
                    FechaMovimiento = dto.FechaMovimiento,
                    TipoMovimiento = dto.TipoMovimiento,
                    LoteAveEngordeOrigenId = linea.LoteAveEngordeOrigenId,
                    LoteReproductoraAveEngordeOrigenId = null,
                    GranjaOrigenId = linea.GranjaOrigenId ?? dto.GranjaOrigenId,
                    NucleoOrigenId = linea.NucleoOrigenId,
                    GalponOrigenId = linea.GalponOrigenId,
                    LoteAveEngordeDestinoId = null,
                    LoteReproductoraAveEngordeDestinoId = null,
                    CantidadHembras = linea.CantidadHembras,
                    CantidadMachos = linea.CantidadMachos,
                    CantidadMixtas = linea.CantidadMixtas,
                    MotivoMovimiento = dto.MotivoMovimiento,
                    Descripcion = dto.Descripcion,
                    Observaciones = dto.Observaciones,
                    UsuarioMovimientoId = dto.UsuarioMovimientoId,
                    NumeroDespacho = dto.NumeroDespacho,
                    EdadAves = dto.EdadAves,
                    TotalPollosGalpon = dto.TotalPollosGalpon.HasValue
                        ? (int?)Math.Round(dto.TotalPollosGalpon.Value)
                        : null,
                    Raza = dto.Raza,
                    Placa = dto.Placa,
                    HoraSalida = dto.HoraSalida,
                    GuiaAgrocalidad = dto.GuiaAgrocalidad,
                    Sellos = dto.Sellos,
                    Ayuno = dto.Ayuno,
                    Conductor = dto.Conductor,
                    PesoBruto = dto.PesoBruto,
                    PesoTara = dto.PesoTara,
                    // Peso global del despacho: idéntico en todos los movimientos generados.
                    PesoBrutoGlobal = tienePeso ? dto.PesoBruto : null,
                    PesoTaraGlobal  = tienePeso ? dto.PesoTara  : null,
                    PesoNetoGlobal  = tienePeso ? pesoNetoGlobal : null,
                    // Peso real prorrateado con ajuste de residuo de redondeo.
                    PesoBrutoRealIndividual    = prorrateo[i].Bruto,
                    PesoTaraRealIndividual     = prorrateo[i].Tara,
                    PesoNetoIndividual         = prorrateo[i].Neto,
                    PromedioPesoAveIndividual  = prorrateo[i].Promedio,
                };
                var created = await CreateAsync(single);
                results.Add(created);
            }

            await tx.CommitAsync();
            return new VentaGranjaDespachoResultDto { Movimientos = results };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
