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

        // Gate B8 — ningún lote liquidado puede entrar en el despacho (la copia congelada
        // dejaría de reflejar las ventas). Mismo criterio que la venta por granja.
        var loteCerrado = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId.HasValue && idsLote.Contains(l.LoteAveEngordeId.Value)
                     && l.DeletedAt == null
                     && l.EstadoOperativoLote.ToLower() == "cerrado")
            .Select(l => l.LoteNombre)
            .FirstOrDefaultAsync();
        if (loteCerrado is not null)
            LiquidacionCongeladaGateCalculos.ValidarEscritura(
                LiquidacionCongeladaGateCalculos.EstadoCerrado,
                OperacionLoteEngordeLiquidado.MovimientoAves,
                loteNombre: loteCerrado);

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

        // Peso báscula obligatorio: la venta Panamá también es una venta. Salvo que la empresa
        // tenga peso DIFERIDO (la báscula llega al día siguiente): ahí la venta puede nacer sin
        // peso y queda Pendiente hasta que se carga al confirmarla.
        var granjaCabecera = dto.GranjaOrigenId ?? lineas.Select(l => l.GranjaOrigenId).FirstOrDefault(g => g.HasValue);
        var pesoDiferido = await EmpresaPermitePesoDiferidoAsync(granjaCabecera);
        MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta(
            "Venta", dto.PesoBruto, dto.PesoTara, pesoDiferido);

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

    /// <summary>
    /// ¿La empresa DUEÑA de la granja del despacho tiene el peso báscula diferido?
    /// Se resuelve por DATOS (<c>farms.company_id</c>), no por país ni por la empresa activa del
    /// token: la granja es el ancla del despacho. <b>Fail-closed</b>: sin granja, granja inexistente
    /// o empresa sin el flag ⇒ <c>false</c> ⇒ peso obligatorio (comportamiento histórico).
    /// Gemelo del helper homónimo de <c>MovimientoPolloEngordeService</c> (este service no es partial
    /// de aquél y sólo comparte la interfaz pública).
    /// </summary>
    private async Task<bool> EmpresaPermitePesoDiferidoAsync(int? granjaId)
    {
        if (granjaId is not int id) return false;
        return await _ctx.Farms
            .Where(f => f.Id == id)
            .Join(_ctx.Companies, f => f.CompanyId, c => c.Id, (_, c) => c.VentaEngordePesoDiferido)
            .FirstOrDefaultAsync();
    }
}
