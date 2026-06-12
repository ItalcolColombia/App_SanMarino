// MovimientoPolloEngorde/Funciones/MovimientoPolloEngordeService.Crud.cs
// CRUD del movimiento: crear, consultar, actualizar, cancelar, eliminar, completar (+ reversión de inventario y mapeo de errores).
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
    public async Task<MovimientoPolloEngordeDto> CreateAsync(CreateMovimientoPolloEngordeDto dto)
    {
        var tieneAveEngordeOrigen = dto.LoteAveEngordeOrigenId.HasValue;
        var tieneReproductoraOrigen = dto.LoteReproductoraAveEngordeOrigenId.HasValue;
        if (tieneAveEngordeOrigen == tieneReproductoraOrigen)
            throw new InvalidOperationException("Debe indicar exactamente un lote de origen: LoteAveEngorde o LoteReproductoraAveEngorde.");

        if (dto.CantidadHembras + dto.CantidadMachos + dto.CantidadMixtas <= 0)
            throw new InvalidOperationException("Las cantidades deben ser mayores a cero.");

        var movimiento = new MovimientoPolloEngorde
        {
            FechaMovimiento = dto.FechaMovimiento,
            TipoMovimiento = dto.TipoMovimiento,
            LoteAveEngordeOrigenId = dto.LoteAveEngordeOrigenId,
            LoteReproductoraAveEngordeOrigenId = dto.LoteReproductoraAveEngordeOrigenId,
            GranjaOrigenId = dto.GranjaOrigenId,
            NucleoOrigenId = dto.NucleoOrigenId,
            GalponOrigenId = dto.GalponOrigenId,
            LoteAveEngordeDestinoId = dto.LoteAveEngordeDestinoId,
            LoteReproductoraAveEngordeDestinoId = dto.LoteReproductoraAveEngordeDestinoId,
            GranjaDestinoId = dto.GranjaDestinoId,
            NucleoDestinoId = dto.NucleoDestinoId,
            GalponDestinoId = dto.GalponDestinoId,
            PlantaDestino = dto.PlantaDestino,
            CantidadHembras = dto.CantidadHembras,
            CantidadMachos = dto.CantidadMachos,
            CantidadMixtas = dto.CantidadMixtas,
            MotivoMovimiento = dto.MotivoMovimiento,
            Descripcion = dto.Descripcion,
            Observaciones = dto.Observaciones,
            Estado = "Pendiente",
            UsuarioMovimientoId = dto.UsuarioMovimientoId > 0 ? dto.UsuarioMovimientoId : _currentUser.UserId,
            FacturaId = dto.FacturaId,
            NumeroDespacho = dto.NumeroDespacho,
            EdadAves = dto.EdadAves,
            TotalPollosGalpon = dto.TotalPollosGalpon,
            Raza = dto.Raza,
            Placa = dto.Placa,
            HoraSalida = dto.HoraSalida,
            GuiaAgrocalidad = dto.GuiaAgrocalidad,
            Sellos = dto.Sellos,
            Ayuno = dto.Ayuno,
            Conductor = dto.Conductor,
            PesoBruto = dto.PesoBruto,
            PesoTara = dto.PesoTara,
            PesoBrutoGlobal = dto.PesoBrutoGlobal,
            PesoTaraGlobal = dto.PesoTaraGlobal,
            PesoNetoGlobal = dto.PesoNetoGlobal,
            PesoBrutoReal = dto.PesoBrutoRealIndividual,
            PesoTaraReal = dto.PesoTaraRealIndividual,
            // Peso individual: usa el prorrateado cuando lo provee CreateVentaGranjaDespachoAsync;
            // en movimientos simples calcula desde PesoBruto - PesoTara.
            PesoNeto = dto.PesoNetoIndividual
                ?? (dto.PesoBruto.HasValue && dto.PesoTara.HasValue ? dto.PesoBruto.Value - dto.PesoTara.Value : null),
            PromedioPesoAve = dto.PromedioPesoAveIndividual,
            CompanyId = _currentUser.CompanyId,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        // Para movimientos simples sin peso prorrateado, calcula el promedio por ave.
        if (!movimiento.PromedioPesoAve.HasValue && movimiento.PesoNeto.HasValue && movimiento.TotalAves > 0)
            movimiento.PromedioPesoAve = movimiento.PesoNeto.Value / movimiento.TotalAves;

        await RellenarOrigenDesdeLoteOrigenSiFaltaAsync(movimiento, dto);

        // Validación de disponibilidad antes de crear (aunque quede Pendiente, reserva cupo para evitar sobreventa).
        if (EsSalidaVenta(movimiento.TipoMovimiento))
        {
            await ValidarDisponibilidadParaCrearAsync(movimiento, dto.PermitirSobrante);
        }

        _ctx.MovimientoPolloEngorde.Add(movimiento);
        await _ctx.SaveChangesAsync();

        movimiento.NumeroMovimiento = $"MPE-{DateTime.UtcNow:yyyyMMdd}-{movimiento.Id:D6}";
        await _ctx.SaveChangesAsync();

        return (await GetByIdAsync(movimiento.Id))!;
    }

    private async Task ValidarDisponibilidadParaCrearAsync(MovimientoPolloEngorde m, bool permitirSobrante)
    {
        var qtyH = m.CantidadHembras;
        var qtyM = m.CantidadMachos;
        var qtyX = m.CantidadMixtas;
        if (qtyH + qtyM + qtyX <= 0) return;

        if (m.LoteAveEngordeOrigenId is { } aeId)
        {
            var resp = await GetAvesDisponiblesLotesAsync(new AvesDisponiblesLotesRequest
            {
                TipoLote = "LoteAveEngorde",
                LoteIds = new List<int> { aeId }
            });
            var row = resp.Items.FirstOrDefault()?.Disponibles;
            if (row == null) throw new InvalidOperationException("No se pudo calcular disponibilidad del lote (no existe o no pertenece a la compañía).");
            var excedente = IndicadorEcuadorCalculos.ExcedenteSobrante(qtyH, row.HembrasDisponibles, qtyM, row.MachosDisponibles, qtyX, row.MixtasDisponibles);
            if (excedente > 0)
            {
                if (!permitirSobrante)
                    throw new InvalidOperationException(
                        $"No hay aves suficientes disponibles en el lote '{row.NombreLote ?? aeId.ToString()}'. " +
                        $"Solicitado (H/M/X)={qtyH}/{qtyM}/{qtyX}; Disponible (H/M/X)={row.HembrasDisponibles}/{row.MachosDisponibles}/{row.MixtasDisponibles}. " +
                        $"Reservado Pendiente total={row.TotalReservadasPendiente}.");
                // Sobrante permitido (Parte B / R2): registrar excedente en el movimiento y acumularlo en el lote.
                m.AvesSobrante = excedente;
                var lote = await _ctx.LoteAveEngorde
                    .FirstOrDefaultAsync(l => l.LoteAveEngordeId == aeId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null);
                if (lote != null) lote.AvesSobrante += excedente;
            }
        }
        else if (m.LoteReproductoraAveEngordeOrigenId is { } repId)
        {
            var resp = await GetAvesDisponiblesLotesAsync(new AvesDisponiblesLotesRequest
            {
                TipoLote = "LoteReproductoraAveEngorde",
                LoteIds = new List<int> { repId }
            });
            var row = resp.Items.FirstOrDefault()?.Disponibles;
            if (row == null) throw new InvalidOperationException("No se pudo calcular disponibilidad del lote reproductora.");
            var excedente = IndicadorEcuadorCalculos.ExcedenteSobrante(qtyH, row.HembrasDisponibles, qtyM, row.MachosDisponibles, qtyX, row.MixtasDisponibles);
            if (excedente > 0)
            {
                if (!permitirSobrante)
                    throw new InvalidOperationException(
                        $"No hay aves suficientes disponibles en el lote reproductora '{row.NombreLote ?? repId.ToString()}'. " +
                        $"Solicitado (H/M/X)={qtyH}/{qtyM}/{qtyX}; Disponible (H/M/X)={row.HembrasDisponibles}/{row.MachosDisponibles}/{row.MixtasDisponibles}. " +
                        $"Reservado Pendiente total={row.TotalReservadasPendiente}.");
                m.AvesSobrante = excedente; // reproductora no acumula en lote padre
            }
        }
    }

    /// <summary>
    /// Si no viene granja/núcleo/galpón en el DTO, los toma del lote de origen (histórico y flujos que solo envían lote).
    /// </summary>
    private async Task RellenarOrigenDesdeLoteOrigenSiFaltaAsync(MovimientoPolloEngorde m, CreateMovimientoPolloEngordeDto dto)
    {
        if (m.GranjaOrigenId.HasValue)
            return;

        if (dto.LoteAveEngordeOrigenId is { } idAe)
        {
            var lae = await _ctx.LoteAveEngorde.AsNoTracking()
                .FirstOrDefaultAsync(l =>
                    l.LoteAveEngordeId == idAe && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null);
            if (lae != null)
            {
                m.GranjaOrigenId = lae.GranjaId;
                if (string.IsNullOrWhiteSpace(m.NucleoOrigenId)) m.NucleoOrigenId = lae.NucleoId;
                if (string.IsNullOrWhiteSpace(m.GalponOrigenId)) m.GalponOrigenId = lae.GalponId;
            }

            return;
        }

        if (dto.LoteReproductoraAveEngordeOrigenId is { } idRa)
        {
            var lrae = await _ctx.LoteReproductoraAveEngorde.AsNoTracking()
                .Include(r => r.LoteAveEngorde)
                .FirstOrDefaultAsync(r => r.Id == idRa);
            var lae = lrae?.LoteAveEngorde;
            if (lae != null && lae.CompanyId == _currentUser.CompanyId && lae.DeletedAt == null)
            {
                m.GranjaOrigenId = lae.GranjaId;
                if (string.IsNullOrWhiteSpace(m.NucleoOrigenId)) m.NucleoOrigenId = lae.NucleoId;
                if (string.IsNullOrWhiteSpace(m.GalponOrigenId)) m.GalponOrigenId = lae.GalponId;
            }
        }
    }

    public async Task<MovimientoPolloEngordeDto?> GetByIdAsync(int id)
    {
        var m = await _ctx.MovimientoPolloEngorde
            .AsNoTracking()
            .Include(x => x.LoteAveEngordeOrigen)
            .Include(x => x.LoteReproductoraAveEngordeOrigen)
            .Include(x => x.LoteAveEngordeDestino)
            .Include(x => x.LoteReproductoraAveEngordeDestino)
            .Include(x => x.GranjaOrigen)
            .Include(x => x.GranjaDestino)
            .Where(x => x.Id == id && x.CompanyId == _currentUser.CompanyId && x.DeletedAt == null)
            .FirstOrDefaultAsync();
        if (m == null) return null;
        return ToDto(m);
    }

    private static MovimientoPolloEngordeDto ToDto(MovimientoPolloEngorde m)
    {
        var loteOrigenNombre = m.LoteAveEngordeOrigenId.HasValue
            ? m.LoteAveEngordeOrigen?.LoteNombre
            : m.LoteReproductoraAveEngordeOrigen?.NombreLote;
        var loteDestinoNombre = m.LoteAveEngordeDestinoId.HasValue
            ? m.LoteAveEngordeDestino?.LoteNombre
            : m.LoteReproductoraAveEngordeDestinoId.HasValue ? m.LoteReproductoraAveEngordeDestino?.NombreLote : null;

        // Backward compat: registros históricos tienen PesoNeto/PromedioPesoAve null (antes eran computed/ignored).
        // En ese caso los calculamos sobre la marcha desde PesoBruto/PesoTara para no romper reportes existentes.
        var pesoNeto = m.PesoNeto
            ?? (m.PesoBruto.HasValue && m.PesoTara.HasValue ? m.PesoBruto.Value - m.PesoTara.Value : null);
        var promedioPesoAve = m.PromedioPesoAve
            ?? (pesoNeto.HasValue && m.TotalAves > 0 ? pesoNeto.Value / m.TotalAves : null);

        return new MovimientoPolloEngordeDto(
            m.Id,
            m.NumeroMovimiento,
            m.FechaMovimiento,
            m.TipoMovimiento,
            m.LoteAveEngordeOrigenId != null ? "AveEngorde" : "ReproductoraAveEngorde",
            m.LoteAveEngordeOrigenId ?? m.LoteReproductoraAveEngordeOrigenId,
            loteOrigenNombre,
            m.LoteAveEngordeDestinoId != null ? "AveEngorde" : (m.LoteReproductoraAveEngordeDestinoId != null ? "ReproductoraAveEngorde" : null),
            m.LoteAveEngordeDestinoId ?? m.LoteReproductoraAveEngordeDestinoId,
            loteDestinoNombre,
            m.GranjaOrigenId,
            m.GranjaOrigen?.Name,
            m.GranjaDestinoId,
            m.GranjaDestino?.Name,
            m.CantidadHembras,
            m.CantidadMachos,
            m.CantidadMixtas,
            m.TotalAves,
            m.Estado,
            m.MotivoMovimiento,
            m.Observaciones,
            m.UsuarioMovimientoId,
            m.UsuarioNombre,
            m.FechaProcesamiento,
            m.FechaCancelacion,
            m.CreatedAt,
            m.NumeroDespacho,
            m.EdadAves,
            m.TotalPollosGalpon,
            m.Raza,
            m.Placa,
            m.HoraSalida,
            m.GuiaAgrocalidad,
            m.Sellos,
            m.Ayuno,
            m.Conductor,
            m.PesoBruto,
            m.PesoTara,
            pesoNeto,
            promedioPesoAve,
            m.PesoBrutoGlobal,
            m.PesoTaraGlobal,
            m.PesoNetoGlobal,
            m.PesoBrutoReal,
            m.PesoTaraReal,
            m.FacturaId,
            m.AvesSobrante,
            m.EsVentaMixta
        );
    }

    public async Task<IEnumerable<MovimientoPolloEngordeDto>> GetAllAsync()
    {
        var list = await _ctx.MovimientoPolloEngorde
            .AsNoTracking()
            .Include(x => x.LoteAveEngordeOrigen)
            .Include(x => x.LoteReproductoraAveEngordeOrigen)
            .Include(x => x.LoteAveEngordeDestino)
            .Include(x => x.LoteReproductoraAveEngordeDestino)
            .Include(x => x.GranjaOrigen)
            .Include(x => x.GranjaDestino)
            .Where(x => x.CompanyId == _currentUser.CompanyId && x.DeletedAt == null)
            .OrderByDescending(x => x.FechaMovimiento)
            .ToListAsync();
        return list.Select(ToDto);
    }

    public async Task<PagedResultCommon> SearchAsync(MovimientoPolloEngordeSearchRequest request)
    {
        var query = _ctx.MovimientoPolloEngorde
            .AsNoTracking()
            .Where(x => x.CompanyId == _currentUser.CompanyId && x.DeletedAt == null);

        if (!string.IsNullOrEmpty(request.NumeroMovimiento))
            query = query.Where(x => x.NumeroMovimiento.Contains(request.NumeroMovimiento));
        if (!string.IsNullOrEmpty(request.TipoMovimiento))
            query = query.Where(x => x.TipoMovimiento == request.TipoMovimiento);
        if (!string.IsNullOrEmpty(request.Estado))
            query = query.Where(x => x.Estado == request.Estado);
        if (request.LoteAveEngordeOrigenId.HasValue)
            query = query.Where(x => x.LoteAveEngordeOrigenId == request.LoteAveEngordeOrigenId);
        if (request.LoteReproductoraAveEngordeOrigenId.HasValue)
            query = query.Where(x => x.LoteReproductoraAveEngordeOrigenId == request.LoteReproductoraAveEngordeOrigenId);

        if (request.GranjaOrigenId.HasValue)
        {
            var farmId = request.GranjaOrigenId.Value;
            query = query.Where(x =>
                x.GranjaOrigenId == farmId
                || (x.LoteAveEngordeOrigen != null && x.LoteAveEngordeOrigen.GranjaId == farmId)
                || (x.LoteReproductoraAveEngordeOrigen != null
                    && x.LoteReproductoraAveEngordeOrigen.LoteAveEngorde != null
                    && x.LoteReproductoraAveEngordeOrigen.LoteAveEngorde.GranjaId == farmId));
        }

        if (!string.IsNullOrWhiteSpace(request.NucleoOrigenId))
        {
            var nid = request.NucleoOrigenId;
            query = query.Where(x =>
                x.NucleoOrigenId == nid
                || (x.LoteAveEngordeOrigen != null && x.LoteAveEngordeOrigen.NucleoId == nid)
                || (x.LoteReproductoraAveEngordeOrigen != null
                    && x.LoteReproductoraAveEngordeOrigen.LoteAveEngorde != null
                    && x.LoteReproductoraAveEngordeOrigen.LoteAveEngorde.NucleoId == nid));
        }

        if (!string.IsNullOrWhiteSpace(request.GalponOrigenId))
        {
            var gpid = request.GalponOrigenId;
            query = query.Where(x =>
                x.GalponOrigenId == gpid
                || (x.LoteAveEngordeOrigen != null && x.LoteAveEngordeOrigen.GalponId == gpid)
                || (x.LoteReproductoraAveEngordeOrigen != null
                    && x.LoteReproductoraAveEngordeOrigen.LoteAveEngorde != null
                    && x.LoteReproductoraAveEngordeOrigen.LoteAveEngorde.GalponId == gpid));
        }

        if (request.GalponOrigenSinAsignar == true)
        {
            query = query.Where(x =>
                string.IsNullOrEmpty(x.GalponOrigenId)
                && (x.LoteAveEngordeOrigen == null || string.IsNullOrEmpty(x.LoteAveEngordeOrigen.GalponId))
                && (x.LoteReproductoraAveEngordeOrigen == null
                    || x.LoteReproductoraAveEngordeOrigen.LoteAveEngorde == null
                    || string.IsNullOrEmpty(x.LoteReproductoraAveEngordeOrigen.LoteAveEngorde.GalponId)));
        }

        if (request.FechaDesde.HasValue)
            query = query.Where(x => x.FechaMovimiento >= request.FechaDesde.Value);
        if (request.FechaHasta.HasValue)
            query = query.Where(x => x.FechaMovimiento <= request.FechaHasta.Value);

        var total = await query.CountAsync();

        var sortDesc = request.SortDesc;
        query = request.SortBy switch
        {
            "NumeroMovimiento" => sortDesc ? query.OrderByDescending(x => x.NumeroMovimiento) : query.OrderBy(x => x.NumeroMovimiento),
            "Estado" => sortDesc ? query.OrderByDescending(x => x.Estado) : query.OrderBy(x => x.Estado),
            _ => sortDesc ? query.OrderByDescending(x => x.FechaMovimiento) : query.OrderBy(x => x.FechaMovimiento)
        };

        var items = await query
            .Include(x => x.LoteAveEngordeOrigen)
            .Include(x => x.LoteReproductoraAveEngordeOrigen)
                .ThenInclude(r => r!.LoteAveEngorde)
            .Include(x => x.LoteAveEngordeDestino)
            .Include(x => x.LoteReproductoraAveEngordeDestino)
            .Include(x => x.GranjaOrigen)
            .Include(x => x.GranjaDestino)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var dtos = items.Select(ToDto).ToList();

        return new PagedResultCommon
        {
            Page = request.Page,
            PageSize = request.PageSize,
            Total = total,
            Items = dtos
        };
    }

    public async Task<MovimientoPolloEngordeDto?> UpdateAsync(int id, UpdateMovimientoPolloEngordeDto dto)
    {
        var m = await _ctx.MovimientoPolloEngorde
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == _currentUser.CompanyId && x.DeletedAt == null);
        if (m == null) return null;
        if (m.Estado != "Pendiente")
            throw new InvalidOperationException("Solo se pueden editar movimientos en estado Pendiente.");

        if (dto.FechaMovimiento.HasValue) m.FechaMovimiento = dto.FechaMovimiento.Value;
        if (dto.TipoMovimiento != null) m.TipoMovimiento = dto.TipoMovimiento;
        if (dto.GranjaOrigenId.HasValue) m.GranjaOrigenId = dto.GranjaOrigenId;
        if (dto.NucleoOrigenId != null) m.NucleoOrigenId = dto.NucleoOrigenId;
        if (dto.GalponOrigenId != null) m.GalponOrigenId = dto.GalponOrigenId;
        if (dto.GranjaDestinoId.HasValue) m.GranjaDestinoId = dto.GranjaDestinoId;
        if (dto.NucleoDestinoId != null) m.NucleoDestinoId = dto.NucleoDestinoId;
        if (dto.GalponDestinoId != null) m.GalponDestinoId = dto.GalponDestinoId;
        if (dto.PlantaDestino != null) m.PlantaDestino = dto.PlantaDestino;
        if (dto.CantidadHembras.HasValue) m.CantidadHembras = dto.CantidadHembras.Value;
        if (dto.CantidadMachos.HasValue) m.CantidadMachos = dto.CantidadMachos.Value;
        if (dto.CantidadMixtas.HasValue) m.CantidadMixtas = dto.CantidadMixtas.Value;
        if (dto.MotivoMovimiento != null) m.MotivoMovimiento = dto.MotivoMovimiento;
        if (dto.Observaciones != null) m.Observaciones = dto.Observaciones;
        if (dto.NumeroDespacho != null) m.NumeroDespacho = dto.NumeroDespacho;
        if (dto.EdadAves.HasValue) m.EdadAves = dto.EdadAves;
        if (dto.TotalPollosGalpon.HasValue) m.TotalPollosGalpon = dto.TotalPollosGalpon;
        if (dto.Raza != null) m.Raza = dto.Raza;
        if (dto.Placa != null) m.Placa = dto.Placa;
        if (dto.HoraSalida.HasValue) m.HoraSalida = dto.HoraSalida;
        if (dto.GuiaAgrocalidad != null) m.GuiaAgrocalidad = dto.GuiaAgrocalidad;
        if (dto.Sellos != null) m.Sellos = dto.Sellos;
        if (dto.Ayuno != null) m.Ayuno = dto.Ayuno;
        if (dto.Conductor != null) m.Conductor = dto.Conductor;
        if (dto.PesoBruto.HasValue) m.PesoBruto = dto.PesoBruto;
        if (dto.PesoTara.HasValue) m.PesoTara = dto.PesoTara;

        // G6: editar peso o cantidades invalida el peso individual prorrateado; recalcular
        // (y re-prorratear las líneas hermanas si el movimiento pertenece a una factura).
        var recalcularPeso = dto.PesoBruto.HasValue || dto.PesoTara.HasValue
            || dto.CantidadHembras.HasValue || dto.CantidadMachos.HasValue || dto.CantidadMixtas.HasValue;
        if (recalcularPeso)
            await ReprorratearPesoTrasEdicionAsync(m);

        m.UpdatedByUserId = _currentUser.UserId;
        m.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    /// <summary>
    /// Recalcula el peso individual tras una edición (G6). En facturas multi-línea, el
    /// PesoBruto/PesoTara del movimiento es el GLOBAL del camión clonado: se propaga a las líneas
    /// hermanas (aunque estén Completadas — el peso no afecta saldos de aves, solo liquidación) y se
    /// re-prorratea el individual de todas con el mismo algoritmo de la creación. En movimientos
    /// simples solo se recalculan PesoNeto y PromedioPesoAve.
    /// </summary>
    private async Task ReprorratearPesoTrasEdicionAsync(MovimientoPolloEngorde m)
    {
        if (m.PesoBruto.HasValue && m.PesoTara.HasValue && m.PesoBruto.Value < m.PesoTara.Value)
            throw new InvalidOperationException("El peso bruto no puede ser menor que el peso tara.");

        var esFacturaMultiLinea = false;
        List<MovimientoPolloEngorde> lineas = new() { m };
        if (m.FacturaId.HasValue)
        {
            // Mismo DbContext ⇒ identity map: la línea editada vuelve como la MISMA instancia trackeada.
            lineas = await _ctx.MovimientoPolloEngorde
                .Where(x => x.FacturaId == m.FacturaId &&
                            x.CompanyId == _currentUser.CompanyId &&
                            x.DeletedAt == null &&
                            x.Estado != "Cancelado")
                .OrderBy(x => x.Id)
                .ToListAsync();
            if (!lineas.Any(x => x.Id == m.Id)) lineas.Add(m);
            esFacturaMultiLinea = lineas.Count > 1;
        }

        if (!esFacturaMultiLinea)
        {
            // Movimiento simple: misma regla que CreateAsync (neto solo con bruto y tara presentes).
            m.PesoNeto = (m.PesoBruto.HasValue && m.PesoTara.HasValue)
                ? m.PesoBruto.Value - m.PesoTara.Value
                : null;
            m.PromedioPesoAve = (m.PesoNeto.HasValue && m.TotalAves > 0)
                ? m.PesoNeto.Value / m.TotalAves
                : null;
            return;
        }

        var pesoBruto = m.PesoBruto ?? 0d;
        var pesoTara = m.PesoTara ?? 0d;
        if (pesoBruto == 0d && pesoTara == 0d) return; // factura sin peso: nada que repartir.

        var pesoNetoGlobal = pesoBruto - pesoTara;
        var avesPorLinea = lineas.Select(x => x.TotalAves).ToList();
        var prorrateo = MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea(pesoBruto, pesoTara, avesPorLinea);

        for (int i = 0; i < lineas.Count; i++)
        {
            var l = lineas[i];
            l.PesoBruto = m.PesoBruto;
            l.PesoTara = m.PesoTara;
            l.PesoBrutoGlobal = m.PesoBruto;
            l.PesoTaraGlobal = m.PesoTara;
            l.PesoNetoGlobal = pesoNetoGlobal;
            l.PesoBrutoReal = prorrateo[i].Bruto;
            l.PesoTaraReal = prorrateo[i].Tara;
            l.PesoNeto = prorrateo[i].Neto;
            l.PromedioPesoAve = prorrateo[i].Promedio;
            if (l.Id != m.Id)
            {
                l.UpdatedByUserId = _currentUser.UserId;
                l.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    public async Task<bool> CancelAsync(int id, string motivo)
    {
        var m = await _ctx.MovimientoPolloEngorde
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == _currentUser.CompanyId && x.DeletedAt == null);
        if (m == null) return false;
        if (m.Estado != "Pendiente")
            throw new InvalidOperationException("Solo se pueden cancelar movimientos en estado Pendiente.");
        m.Estado = "Cancelado";
        m.FechaCancelacion = DateTime.UtcNow;
        m.Observaciones = AppendObservaciones(m.Observaciones, " | Cancelado: " + (motivo ?? ""));
        m.UpdatedByUserId = _currentUser.UserId;
        m.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> EliminarAsync(int id, string? motivo)
    {
        await using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            var m = await _ctx.MovimientoPolloEngorde
                .Include(x => x.LoteAveEngordeOrigen)
                .Include(x => x.LoteReproductoraAveEngordeOrigen)
                .Include(x => x.LoteAveEngordeDestino)
                .Include(x => x.LoteReproductoraAveEngordeDestino)
                .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == _currentUser.CompanyId && x.DeletedAt == null);
            if (m == null)
            {
                await tx.RollbackAsync();
                return false;
            }

            if (m.Estado == "Completado")
            {
                await EnsureLotesCargadosParaRevertirAsync(m);
                ValidarLotesPresentesParaRevertir(m);
                RevertirEfectoCompletadoEnLotes(m);
            }

            var nota = string.IsNullOrWhiteSpace(motivo) ? "(sin motivo)" : motivo.Trim();
            m.Estado = "Anulado";
            m.Observaciones = AppendObservaciones(m.Observaciones, " | Eliminado: " + nota);
            m.DeletedAt = DateTime.UtcNow;
            m.UpdatedByUserId = _currentUser.UserId;
            m.UpdatedAt = DateTime.UtcNow;
            await _ctx.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        }
        catch (DbUpdateException ex)
        {
            await RollbackIfNeededAsync(tx);
            throw MapDbUpdateToInvalidOperation(ex);
        }
        catch (InvalidOperationException)
        {
            await RollbackIfNeededAsync(tx);
            throw;
        }
    }

    private static async Task RollbackIfNeededAsync(IDbContextTransaction tx)
    {
        try
        {
            await tx.RollbackAsync();
        }
        catch
        {
            // Evita enmascarar la excepción original si el rollback falla.
        }
    }

    /// <summary>
    /// Expone el detalle de PostgreSQL (SqlState, constraint) para diagnóstico en producción.
    /// </summary>
    private static InvalidOperationException MapDbUpdateToInvalidOperation(DbUpdateException ex, string? operacion = null)
    {
        var detail = ex.InnerException is PostgresException pg
            ? $"{pg.SqlState}: {pg.MessageText}" +
              (string.IsNullOrEmpty(pg.ConstraintName) ? "" : $" (constraint: {pg.ConstraintName})")
            : (ex.InnerException?.Message ?? ex.Message);
        var prefix = string.IsNullOrWhiteSpace(operacion)
            ? "No se pudo guardar la eliminación del movimiento (reversión de aves en lotes o anulación). " +
              "Revise que el lote destino tenga aves suficientes para deshacer el traslado y que no haya restricciones en base de datos. "
            : operacion.TrimEnd() + " ";
        return new InvalidOperationException(prefix + "Detalle: " + detail, ex);
    }

    private static readonly string MensajeAyudaCorreccionCompletados =
        "No se pudo guardar la corrección de ventas completadas. " +
        "Causas frecuentes: (1) en la base de producción falta permitir estado 'Anulado' en el CHECK de movimiento_pollo_engorde " +
        "(ejecutar el script backend/sql/alter_movimiento_pollo_engorde_ck_estado_anulado.sql); " +
        "(2) en la base de producción el CHECK ck_mpe_cantidades exige (H+M+X) > 0 incluso para Anulados " +
        "(ejecutar el script backend/sql/alter_movimiento_pollo_engorde_ck_cantidades_allow_anulado.sql); " +
        "(3) error en un trigger sobre movimiento_pollo_engorde o lote_registro_historico_unificado. ";

    /// <summary>
    /// Si el Include no trajo el lote (FK inconsistente o filtro), carga explícita por id para poder revertir cantidades.
    /// </summary>
    private async Task EnsureLotesCargadosParaRevertirAsync(MovimientoPolloEngorde m)
    {
        if (m.LoteAveEngordeOrigenId is { } idOae && m.LoteAveEngordeOrigen == null)
        {
            m.LoteAveEngordeOrigen = await _ctx.LoteAveEngorde
                .FirstOrDefaultAsync(l => l.LoteAveEngordeId == idOae);
        }

        if (m.LoteReproductoraAveEngordeOrigenId is { } idOra && m.LoteReproductoraAveEngordeOrigen == null)
        {
            m.LoteReproductoraAveEngordeOrigen = await _ctx.LoteReproductoraAveEngorde
                .FirstOrDefaultAsync(l => l.Id == idOra);
        }

        if (m.LoteAveEngordeDestinoId is { } idDae && m.LoteAveEngordeDestino == null)
        {
            m.LoteAveEngordeDestino = await _ctx.LoteAveEngorde
                .FirstOrDefaultAsync(l => l.LoteAveEngordeId == idDae);
        }

        if (m.LoteReproductoraAveEngordeDestinoId is { } idDra && m.LoteReproductoraAveEngordeDestino == null)
        {
            m.LoteReproductoraAveEngordeDestino = await _ctx.LoteReproductoraAveEngorde
                .FirstOrDefaultAsync(l => l.Id == idDra);
        }
    }

    /// <summary>Evita guardar con reversión incompleta (FK sin fila en lote).</summary>
    private static void ValidarLotesPresentesParaRevertir(MovimientoPolloEngorde m)
    {
        if (m.LoteAveEngordeOrigenId.HasValue && m.LoteAveEngordeOrigen == null)
            throw new InvalidOperationException(
                "No se puede eliminar: no existe el lote ave engorde de origen en la base de datos. Corrija el movimiento o contacte soporte.");
        if (m.LoteReproductoraAveEngordeOrigenId.HasValue && m.LoteReproductoraAveEngordeOrigen == null)
            throw new InvalidOperationException(
                "No se puede eliminar: no existe el lote reproductora de origen en la base de datos.");
        if (m.LoteAveEngordeDestinoId.HasValue && m.LoteAveEngordeDestino == null)
            throw new InvalidOperationException(
                "No se puede eliminar: no existe el lote ave engorde de destino en la base de datos; no se puede revertir el traslado.");
        if (m.LoteReproductoraAveEngordeDestinoId.HasValue && m.LoteReproductoraAveEngordeDestino == null)
            throw new InvalidOperationException(
                "No se puede eliminar: no existe el lote reproductora de destino en la base de datos.");
    }

    /// <summary>Inverso de <see cref="CompleteAsync"/>: devuelve aves al origen y resta del destino.</summary>
    private static void RevertirEfectoCompletadoEnLotes(MovimientoPolloEngorde m)
    {
        // Destino primero (restar lo que se había sumado)
        if (m.LoteAveEngordeDestinoId.HasValue && m.LoteAveEngordeDestino != null)
        {
            var lote = m.LoteAveEngordeDestino;
            var h = (lote.HembrasL ?? 0) - m.CantidadHembras;
            var mach = (lote.MachosL ?? 0) - m.CantidadMachos;
            var mix = (lote.Mixtas ?? 0) - m.CantidadMixtas;
            if (h < 0 || mach < 0 || mix < 0)
                throw new InvalidOperationException(
                    "No se puede eliminar: el lote destino no tiene suficientes aves para revertir el movimiento (pudo haberse registrado otro movimiento).");
            lote.HembrasL = h;
            lote.MachosL = mach;
            lote.Mixtas = mix;
            if ((lote.AvesEncasetadas ?? 0) > 0 && (lote.HembrasL ?? 0) + (lote.MachosL ?? 0) + (lote.Mixtas ?? 0) == 0)
                lote.AvesEncasetadas = 0;
        }
        else if (m.LoteReproductoraAveEngordeDestinoId.HasValue && m.LoteReproductoraAveEngordeDestino != null)
        {
            var lote = m.LoteReproductoraAveEngordeDestino;
            var h = (lote.H ?? 0) - m.CantidadHembras;
            var mach = (lote.M ?? 0) - m.CantidadMachos;
            var mix = (lote.Mixtas ?? 0) - m.CantidadMixtas;
            if (h < 0 || mach < 0 || mix < 0)
                throw new InvalidOperationException(
                    "No se puede eliminar: el lote destino no tiene suficientes aves para revertir el movimiento.");
            lote.H = h;
            lote.M = mach;
            lote.Mixtas = mix;
        }

        // Origen: sumar de vuelta al inventario
        if (m.LoteAveEngordeOrigenId.HasValue && m.LoteAveEngordeOrigen != null)
        {
            var lote = m.LoteAveEngordeOrigen;
            var (efH, efM, efX) = CantidadesEfectivasEnLote(m); // venta Panamá: efH=H, efM=M, efX=0
            lote.HembrasL = (lote.HembrasL ?? 0) + efH;
            lote.MachosL = (lote.MachosL ?? 0) + efM;
            // Para ventas Panama, forzar Mixtas=0 al revertir (no acumular en campo que no aplica).
            lote.Mixtas = m.EsVentaMixta ? 0 : (lote.Mixtas ?? 0) + efX;
        }
        else if (m.LoteReproductoraAveEngordeOrigenId.HasValue && m.LoteReproductoraAveEngordeOrigen != null)
        {
            var lote = m.LoteReproductoraAveEngordeOrigen;
            lote.H = (lote.H ?? 0) + m.CantidadHembras;
            lote.M = (lote.M ?? 0) + m.CantidadMachos;
            lote.Mixtas = (lote.Mixtas ?? 0) + m.CantidadMixtas;
        }
    }

    public async Task<MovimientoPolloEngordeDto?> CompleteAsync(int id)
    {
        var m = await _ctx.MovimientoPolloEngorde
            .Include(x => x.LoteAveEngordeOrigen)
            .Include(x => x.LoteReproductoraAveEngordeOrigen)
            .Include(x => x.LoteAveEngordeDestino)
            .Include(x => x.LoteReproductoraAveEngordeDestino)
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == _currentUser.CompanyId && x.DeletedAt == null);
        if (m == null) return null;
        if (m.Estado != "Pendiente")
            throw new InvalidOperationException("Solo se pueden completar movimientos en estado Pendiente.");

        // Descontar del lote origen
        if (m.LoteAveEngordeOrigenId.HasValue && m.LoteAveEngordeOrigen != null)
        {
            var lote = m.LoteAveEngordeOrigen;
            var (efH, efM, efX) = CantidadesEfectivasEnLote(m); // venta Panamá: efH=H, efM=M, efX=0
            var h = (lote.HembrasL ?? 0) - efH;
            var mach = (lote.MachosL ?? 0) - efM;
            var mix = (lote.Mixtas ?? 0) - efX;
            lote.HembrasL = Math.Max(0, h);
            lote.MachosL = Math.Max(0, mach);
            // Para ventas Panama, forzar Mixtas=0 (campo no aplica; aves viven en HembrasL/MachosL).
            lote.Mixtas = m.EsVentaMixta ? 0 : Math.Max(0, mix);
            if ((lote.AvesEncasetadas ?? 0) > 0 && (lote.HembrasL ?? 0) + (lote.MachosL ?? 0) + (lote.Mixtas ?? 0) == 0)
                lote.AvesEncasetadas = 0;
        }
        else if (m.LoteReproductoraAveEngordeOrigenId.HasValue && m.LoteReproductoraAveEngordeOrigen != null)
        {
            var lote = m.LoteReproductoraAveEngordeOrigen;
            var h = (lote.H ?? 0) - m.CantidadHembras;
            var mach = (lote.M ?? 0) - m.CantidadMachos;
            var mix = (lote.Mixtas ?? 0) - m.CantidadMixtas;
            lote.H = Math.Max(0, h);
            lote.M = Math.Max(0, mach);
            lote.Mixtas = Math.Max(0, mix);
        }

        // Sumar al lote destino (si existe)
        if (m.LoteAveEngordeDestinoId.HasValue && m.LoteAveEngordeDestino != null)
        {
            var lote = m.LoteAveEngordeDestino;
            lote.HembrasL = (lote.HembrasL ?? 0) + m.CantidadHembras;
            lote.MachosL = (lote.MachosL ?? 0) + m.CantidadMachos;
            lote.Mixtas = (lote.Mixtas ?? 0) + m.CantidadMixtas;
        }
        else if (m.LoteReproductoraAveEngordeDestinoId.HasValue && m.LoteReproductoraAveEngordeDestino != null)
        {
            var lote = m.LoteReproductoraAveEngordeDestino;
            lote.H = (lote.H ?? 0) + m.CantidadHembras;
            lote.M = (lote.M ?? 0) + m.CantidadMachos;
            lote.Mixtas = (lote.Mixtas ?? 0) + m.CantidadMixtas;
        }

        m.Estado = "Completado";
        m.FechaProcesamiento = DateTime.UtcNow;
        m.UpdatedByUserId = _currentUser.UserId;
        m.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MovimientoPolloEngordeDto>> CompletarBatchAsync(IReadOnlyList<int> movimientoIds)
    {
        if (movimientoIds == null || movimientoIds.Count == 0)
            throw new InvalidOperationException("Debe indicar al menos un movimiento.");

        var ids = movimientoIds.Distinct().ToList();
        await using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            var list = new List<MovimientoPolloEngordeDto>();
            foreach (var id in ids)
            {
                var dto = await CompleteAsync(id);
                if (dto == null)
                    throw new InvalidOperationException($"Movimiento {id} no encontrado.");
                list.Add(dto);
            }

            await tx.CommitAsync();
            return list;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
