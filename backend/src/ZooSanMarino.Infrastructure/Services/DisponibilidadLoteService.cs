// src/ZooSanMarino.Infrastructure/Services/DisponibilidadLoteService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.DTOs.Traslados;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class DisponibilidadLoteService : IDisponibilidadLoteService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IEspejoHuevoProduccionSyncService _espejoHuevoSync;

    public DisponibilidadLoteService(
        ZooSanMarinoContext context,
        ICurrentUser currentUser,
        IEspejoHuevoProduccionSyncService espejoHuevoSync)
    {
        _context = context;
        _currentUser = currentUser;
        _espejoHuevoSync = espejoHuevoSync;
    }

    public async Task<DisponibilidadLoteDto?> ObtenerDisponibilidadLoteAsync(string loteId)
    {
        // Convertir loteId string a int para buscar en Lotes
        if (!int.TryParse(loteId, out var loteIdInt))
        {
            return null;
        }

        // Obtener el lote
        var lote = await _context.Lotes
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Include(l => l.Galpon)
            .FirstOrDefaultAsync(l => 
                l.LoteId == loteIdInt && 
                l.CompanyId == _currentUser.CompanyId && 
                l.DeletedAt == null);

        if (lote == null)
        {
            return null;
        }

        // Opción B: producción = mismo lote con Fase "Produccion" o lote hijo con Fase "Produccion"
        Lote? loteProd = null;
        if (lote.Fase == "Produccion" && lote.LoteId.HasValue)
            loteProd = lote;
        else if (lote.LoteId.HasValue)
            loteProd = await _context.Lotes
                .AsNoTracking()
                .Include(l => l.Farm)
                .Include(l => l.Nucleo)
                .Include(l => l.Galpon)
                .FirstOrDefaultAsync(l => l.LotePadreId == lote.LoteId && l.Fase == "Produccion" && l.DeletedAt == null);

        if (loteProd != null)
        {
            return await ObtenerDisponibilidadHuevosAsync(loteProd);
        }
        return await ObtenerDisponibilidadAvesAsync(lote);
    }

    private async Task<DisponibilidadLoteDto> ObtenerDisponibilidadHuevosAsync(Lote loteProd)
    {
        if (!loteProd.LoteId.HasValue)
            throw new InvalidOperationException("Lote sin LoteId.");

        var loteIdStr = loteProd.LoteId.Value.ToString();

        // Seguimientos desde tabla unificada seguimiento_diario (tipo produccion) — alineado con seguimiento diario unificado.
        // Se agrega en la BD: antes se traían TODAS las filas del lote para sumarlas en memoria
        // (11 Sum sobre la lista completa). El GroupBy(1) traduce a un único SELECT con los 11
        // SUM, el COUNT y el MAX(fecha) — un solo viaje y sin materializar las filas.
        // SUM de Postgres ignora los NULL igual que `?? 0` en memoria, así que el número no cambia;
        // sobre cero filas la consulta no devuelve grupo, y ahí los totales quedan en 0 como antes.
        var agg = await _context.SeguimientoDiario
            .AsNoTracking()
            .Where(s => s.TipoSeguimiento == "produccion" && s.LoteId == loteIdStr)
            .GroupBy(s => 1)
            .Select(g => new
            {
                Limpio    = g.Sum(s => (int?)s.HuevoLimpio)    ?? 0,
                Tratado   = g.Sum(s => (int?)s.HuevoTratado)   ?? 0,
                Sucio     = g.Sum(s => (int?)s.HuevoSucio)     ?? 0,
                Deforme   = g.Sum(s => (int?)s.HuevoDeforme)   ?? 0,
                Blanco    = g.Sum(s => (int?)s.HuevoBlanco)    ?? 0,
                DobleYema = g.Sum(s => (int?)s.HuevoDobleYema) ?? 0,
                Piso      = g.Sum(s => (int?)s.HuevoPiso)      ?? 0,
                Pequeno   = g.Sum(s => (int?)s.HuevoPequeno)   ?? 0,
                Roto      = g.Sum(s => (int?)s.HuevoRoto)      ?? 0,
                Desecho   = g.Sum(s => (int?)s.HuevoDesecho)   ?? 0,
                Otro      = g.Sum(s => (int?)s.HuevoOtro)      ?? 0,
                Filas     = g.Count(),
                UltimaFecha = (DateTime?)g.Max(s => s.Fecha)
            })
            .FirstOrDefaultAsync();

        var totalLimpio    = agg?.Limpio    ?? 0;
        var totalTratado   = agg?.Tratado   ?? 0;
        var totalSucio     = agg?.Sucio     ?? 0;
        var totalDeforme   = agg?.Deforme   ?? 0;
        var totalBlanco    = agg?.Blanco    ?? 0;
        var totalDobleYema = agg?.DobleYema ?? 0;
        var totalPiso      = agg?.Piso      ?? 0;
        var totalPequeno   = agg?.Pequeno   ?? 0;
        var totalRoto      = agg?.Roto      ?? 0;
        var totalDesecho   = agg?.Desecho   ?? 0;
        var totalOtro      = agg?.Otro      ?? 0;

        // Traslados completados para restar: mismo criterio, un solo SELECT agregado.
        var aggTraslados = await _context.TrasladoHuevos
            .AsNoTracking()
            .Where(t => t.LoteId == loteIdStr && t.Estado == "Completado")
            .GroupBy(t => 1)
            .Select(g => new
            {
                Limpio    = g.Sum(t => (int?)t.CantidadLimpio)    ?? 0,
                Tratado   = g.Sum(t => (int?)t.CantidadTratado)   ?? 0,
                Sucio     = g.Sum(t => (int?)t.CantidadSucio)     ?? 0,
                Deforme   = g.Sum(t => (int?)t.CantidadDeforme)   ?? 0,
                Blanco    = g.Sum(t => (int?)t.CantidadBlanco)    ?? 0,
                DobleYema = g.Sum(t => (int?)t.CantidadDobleYema) ?? 0,
                Piso      = g.Sum(t => (int?)t.CantidadPiso)      ?? 0,
                Pequeno   = g.Sum(t => (int?)t.CantidadPequeno)   ?? 0,
                Roto      = g.Sum(t => (int?)t.CantidadRoto)      ?? 0,
                Desecho   = g.Sum(t => (int?)t.CantidadDesecho)   ?? 0,
                Otro      = g.Sum(t => (int?)t.CantidadOtro)      ?? 0
            })
            .FirstOrDefaultAsync();

        // Restar traslados completados
        totalLimpio    -= aggTraslados?.Limpio    ?? 0;
        totalTratado   -= aggTraslados?.Tratado   ?? 0;
        totalSucio     -= aggTraslados?.Sucio     ?? 0;
        totalDeforme   -= aggTraslados?.Deforme   ?? 0;
        totalBlanco    -= aggTraslados?.Blanco    ?? 0;
        totalDobleYema -= aggTraslados?.DobleYema ?? 0;
        totalPiso      -= aggTraslados?.Piso      ?? 0;
        totalPequeno   -= aggTraslados?.Pequeno   ?? 0;
        totalRoto      -= aggTraslados?.Roto      ?? 0;
        totalDesecho   -= aggTraslados?.Desecho   ?? 0;
        totalOtro      -= aggTraslados?.Otro      ?? 0;

        // Asegurar que no sean negativos
        totalLimpio = Math.Max(0, totalLimpio);
        totalTratado = Math.Max(0, totalTratado);
        totalSucio = Math.Max(0, totalSucio);
        totalDeforme = Math.Max(0, totalDeforme);
        totalBlanco = Math.Max(0, totalBlanco);
        totalDobleYema = Math.Max(0, totalDobleYema);
        totalPiso = Math.Max(0, totalPiso);
        totalPequeno = Math.Max(0, totalPequeno);
        totalRoto = Math.Max(0, totalRoto);
        totalDesecho = Math.Max(0, totalDesecho);
        totalOtro = Math.Max(0, totalOtro);

        var totalHuevos = totalLimpio + totalTratado + totalSucio + totalDeforme + 
                         totalBlanco + totalDobleYema + totalPiso + totalPequeno + 
                         totalRoto + totalDesecho + totalOtro;
        
        var totalHuevosIncubables = totalLimpio + totalTratado;

        var fechaUltimoRegistro = (agg?.Filas ?? 0) > 0
            ? agg!.UltimaFecha
            : (DateTime?)null;

        var diasEnProduccion = loteProd.FechaInicioProduccion.HasValue && loteProd.FechaInicioProduccion.Value != default
            ? (DateTime.Today - loteProd.FechaInicioProduccion.Value.Date).Days
            : 0;

        return new DisponibilidadLoteDto
        {
            LoteId = loteProd.LoteId ?? 0,
            LoteNombre = loteProd.LoteNombre,
            TipoLote = "Produccion",
            Aves = null,
            Huevos = new HuevosDisponiblesDto
            {
                TotalHuevos = totalHuevos,
                TotalHuevosIncubables = totalHuevosIncubables,
                Limpio = totalLimpio,
                Tratado = totalTratado,
                Sucio = totalSucio,
                Deforme = totalDeforme,
                Blanco = totalBlanco,
                DobleYema = totalDobleYema,
                Piso = totalPiso,
                Pequeno = totalPequeno,
                Roto = totalRoto,
                Desecho = totalDesecho,
                Otro = totalOtro,
                FechaUltimoRegistro = fechaUltimoRegistro,
                DiasEnProduccion = diasEnProduccion
            },
            GranjaId = loteProd.GranjaId,
            GranjaNombre = loteProd.Farm?.Name ?? string.Empty,
            NucleoId = loteProd.NucleoId,
            NucleoNombre = loteProd.Nucleo?.NucleoNombre,
            GalponId = loteProd.GalponId,
            GalponNombre = loteProd.Galpon?.GalponNombre
        };
    }

    private async Task<DisponibilidadLoteDto> ObtenerDisponibilidadAvesAsync(Lote lote)
    {
        var loteIdInt = lote.LoteId ?? 0;

        // Obtener aves iniciales del lote
        var hembrasIniciales = lote.HembrasL ?? 0;
        var machosIniciales = lote.MachosL ?? 0;

        // Calcular mortalidad acumulada desde tabla unificada seguimiento_diario (tipo levante) — alineado con seguimiento diario unificado
        var seguimientos = await _context.SeguimientoDiario
            .AsNoTracking()
            .Where(s => s.TipoSeguimiento == "levante" && s.LoteId == loteIdInt.ToString())
            .ToListAsync();

        var mortalidadAcumHembras = seguimientos.Sum(s => s.MortalidadHembras ?? 0);
        var mortalidadAcumMachos = seguimientos.Sum(s => s.MortalidadMachos ?? 0);

        // Obtener retiros acumulados desde movimientos de aves completados
        var retirosCompletados = await _context.MovimientoAves
            .AsNoTracking()
            .Where(m => 
                (m.LoteOrigenId == loteIdInt || m.InventarioOrigen != null && m.InventarioOrigen.LoteId == loteIdInt) &&
                m.Estado == "Completado")
            .ToListAsync();

        var retirosAcumHembras = retirosCompletados.Sum(m => m.CantidadHembras);
        var retirosAcumMachos = retirosCompletados.Sum(m => m.CantidadMachos);

        // Calcular aves vivas
        var hembrasVivas = Math.Max(0, hembrasIniciales - mortalidadAcumHembras - retirosAcumHembras);
        var machosVivos = Math.Max(0, machosIniciales - mortalidadAcumMachos - retirosAcumMachos);
        var totalAves = hembrasVivas + machosVivos;

        return new DisponibilidadLoteDto
        {
            LoteId = loteIdInt,
            LoteNombre = lote.LoteNombre,
            TipoLote = "Levante",
            Aves = new AvesDisponiblesDto
            {
                HembrasVivas = hembrasVivas,
                MachosVivos = machosVivos,
                TotalAves = totalAves,
                HembrasIniciales = hembrasIniciales,
                MachosIniciales = machosIniciales,
                MortalidadAcumuladaHembras = mortalidadAcumHembras,
                MortalidadAcumuladaMachos = mortalidadAcumMachos,
                RetirosAcumuladosHembras = retirosAcumHembras,
                RetirosAcumuladosMachos = retirosAcumMachos
            },
            Huevos = null,
            GranjaId = lote.GranjaId,
            GranjaNombre = lote.Farm?.Name ?? string.Empty,
            NucleoId = lote.NucleoId,
            NucleoNombre = lote.Nucleo?.NucleoNombre,
            GalponId = lote.GalponId,
            GalponNombre = lote.Galpon?.GalponNombre
        };
    }

    public async Task<bool> ValidarDisponibilidadAvesAsync(string loteId, int cantidadHembras, int cantidadMachos)
    {
        var disponibilidad = await ObtenerDisponibilidadLoteAsync(loteId);
        
        if (disponibilidad == null || disponibilidad.Aves == null)
        {
            return false;
        }

        return disponibilidad.Aves.HembrasVivas >= cantidadHembras &&
               disponibilidad.Aves.MachosVivos >= cantidadMachos;
    }

    public async Task<bool> ValidarDisponibilidadHuevosAsync(string loteId, Dictionary<string, int> cantidadesPorTipo)
    {
        var disponibilidad = await ObtenerDisponibilidadLoteAsync(loteId);
        
        if (disponibilidad == null || disponibilidad.Huevos == null)
        {
            return false;
        }

        var huevos = disponibilidad.Huevos;

        // Validar cada tipo de huevo
        if (cantidadesPorTipo.ContainsKey("Limpio") && cantidadesPorTipo["Limpio"] > huevos.Limpio)
            return false;
        if (cantidadesPorTipo.ContainsKey("Tratado") && cantidadesPorTipo["Tratado"] > huevos.Tratado)
            return false;
        if (cantidadesPorTipo.ContainsKey("Sucio") && cantidadesPorTipo["Sucio"] > huevos.Sucio)
            return false;
        if (cantidadesPorTipo.ContainsKey("Deforme") && cantidadesPorTipo["Deforme"] > huevos.Deforme)
            return false;
        if (cantidadesPorTipo.ContainsKey("Blanco") && cantidadesPorTipo["Blanco"] > huevos.Blanco)
            return false;
        if (cantidadesPorTipo.ContainsKey("DobleYema") && cantidadesPorTipo["DobleYema"] > huevos.DobleYema)
            return false;
        if (cantidadesPorTipo.ContainsKey("Piso") && cantidadesPorTipo["Piso"] > huevos.Piso)
            return false;
        if (cantidadesPorTipo.ContainsKey("Pequeno") && cantidadesPorTipo["Pequeno"] > huevos.Pequeno)
            return false;
        if (cantidadesPorTipo.ContainsKey("Roto") && cantidadesPorTipo["Roto"] > huevos.Roto)
            return false;
        if (cantidadesPorTipo.ContainsKey("Desecho") && cantidadesPorTipo["Desecho"] > huevos.Desecho)
            return false;
        if (cantidadesPorTipo.ContainsKey("Otro") && cantidadesPorTipo["Otro"] > huevos.Otro)
            return false;

        return true;
    }

    /// <inheritdoc />
    public async Task<DisponibilidadLoteDto?> ObtenerDisponibilidadLoteLPPAsync(int lotePosturaProduccionId)
    {
        var lpp = await _context.LotePosturaProduccion
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Include(l => l.Galpon)
            .FirstOrDefaultAsync(l =>
                l.LotePosturaProduccionId == lotePosturaProduccionId &&
                (l.EmpresaId == null || l.EmpresaId == _currentUser.CompanyId));

        if (lpp == null) return null;

        var espejo = await _context.EspejoHuevoProduccion
            .AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.LotePosturaProduccionId == lotePosturaProduccionId &&
                e.CompanyId == _currentUser.CompanyId);

        if (espejo == null)
        {
            await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lotePosturaProduccionId).ConfigureAwait(false);
            espejo = await _context.EspejoHuevoProduccion
                .AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    e.LotePosturaProduccionId == lotePosturaProduccionId &&
                    e.CompanyId == _currentUser.CompanyId);
        }

        if (espejo == null)
        {
            return new DisponibilidadLoteDto
            {
                LoteId = lpp.LoteId ?? lotePosturaProduccionId,
                LoteNombre = lpp.LoteNombre ?? string.Empty,
                TipoLote = "Produccion",
                LotePosturaProduccionId = lotePosturaProduccionId,
                Huevos = new HuevosDisponiblesDto
                {
                    TotalHuevos = 0,
                    TotalHuevosIncubables = 0,
                    Limpio = 0, Tratado = 0, Sucio = 0, Deforme = 0, Blanco = 0,
                    DobleYema = 0, Piso = 0, Pequeno = 0, Roto = 0, Desecho = 0, Otro = 0,
                    DiasEnProduccion = lpp.FechaInicioProduccion.HasValue
                        ? (int)(DateTime.Today - lpp.FechaInicioProduccion.Value.Date).TotalDays
                        : 0
                },
                GranjaId = lpp.GranjaId,
                GranjaNombre = lpp.Farm?.Name ?? string.Empty,
                NucleoId = lpp.NucleoId,
                NucleoNombre = lpp.Nucleo?.NucleoNombre,
                GalponId = lpp.GalponId,
                GalponNombre = lpp.Galpon?.GalponNombre
            };
        }

        var huevoItemsDisponibles = await ObtenerDisponibilidadHuevoItemsLPPAsync(lotePosturaProduccionId).ConfigureAwait(false);

        var diasProd = lpp.FechaInicioProduccion.HasValue
            ? (int)(DateTime.Today - lpp.FechaInicioProduccion.Value.Date).TotalDays
            : 0;

        var huevos = new HuevosDisponiblesDto
        {
            TotalHuevos = espejo.HuevoTotDinamico,
            TotalHuevosIncubables = espejo.HuevoIncDinamico,
            Limpio = espejo.HuevoLimpioDinamico,
            Tratado = espejo.HuevoTratadoDinamico,
            Sucio = espejo.HuevoSucioDinamico,
            Deforme = espejo.HuevoDeformeDinamico,
            Blanco = espejo.HuevoBlancoDinamico,
            DobleYema = espejo.HuevoDobleYemaDinamico,
            Piso = espejo.HuevoPisoDinamico,
            Pequeno = espejo.HuevoPequenoDinamico,
            Roto = espejo.HuevoRotoDinamico,
            Desecho = espejo.HuevoDesechoDinamico,
            Otro = espejo.HuevoOtroDinamico,
            DiasEnProduccion = diasProd
        };

        var historicoEspejo = new HuevosDisponiblesDto
        {
            TotalHuevos = espejo.HuevoTotHistorico,
            TotalHuevosIncubables = espejo.HuevoIncHistorico,
            Limpio = espejo.HuevoLimpioHistorico,
            Tratado = espejo.HuevoTratadoHistorico,
            Sucio = espejo.HuevoSucioHistorico,
            Deforme = espejo.HuevoDeformeHistorico,
            Blanco = espejo.HuevoBlancoHistorico,
            DobleYema = espejo.HuevoDobleYemaHistorico,
            Piso = espejo.HuevoPisoHistorico,
            Pequeno = espejo.HuevoPequenoHistorico,
            Roto = espejo.HuevoRotoHistorico,
            Desecho = espejo.HuevoDesechoHistorico,
            Otro = espejo.HuevoOtroHistorico,
            DiasEnProduccion = diasProd
        };

        return new DisponibilidadLoteDto
        {
            LoteId = lpp.LoteId ?? lotePosturaProduccionId,
            LoteNombre = lpp.LoteNombre ?? string.Empty,
            TipoLote = "Produccion",
            LotePosturaProduccionId = lotePosturaProduccionId,
            Huevos = huevos,
            HuevosHistoricoEspejo = historicoEspejo,
            HuevoItemsDisponibles = huevoItemsDisponibles,
            GranjaId = lpp.GranjaId,
            GranjaNombre = lpp.Farm?.Name ?? string.Empty,
            NucleoId = lpp.NucleoId,
            NucleoNombre = lpp.Nucleo?.NucleoNombre,
            GalponId = lpp.GalponId,
            GalponNombre = lpp.Galpon?.GalponNombre
        };
    }

    /// <inheritdoc />
    public async Task<bool> ValidarDisponibilidadHuevosLPPAsync(int lotePosturaProduccionId, Dictionary<string, int> cantidadesPorTipo)
    {
        var disp = await ObtenerDisponibilidadLoteLPPAsync(lotePosturaProduccionId);
        if (disp?.Huevos == null) return false;
        var h = disp.Huevos;
        if (cantidadesPorTipo.ContainsKey("Limpio") && cantidadesPorTipo["Limpio"] > h.Limpio) return false;
        if (cantidadesPorTipo.ContainsKey("Tratado") && cantidadesPorTipo["Tratado"] > h.Tratado) return false;
        if (cantidadesPorTipo.ContainsKey("Sucio") && cantidadesPorTipo["Sucio"] > h.Sucio) return false;
        if (cantidadesPorTipo.ContainsKey("Deforme") && cantidadesPorTipo["Deforme"] > h.Deforme) return false;
        if (cantidadesPorTipo.ContainsKey("Blanco") && cantidadesPorTipo["Blanco"] > h.Blanco) return false;
        if (cantidadesPorTipo.ContainsKey("DobleYema") && cantidadesPorTipo["DobleYema"] > h.DobleYema) return false;
        if (cantidadesPorTipo.ContainsKey("Piso") && cantidadesPorTipo["Piso"] > h.Piso) return false;
        if (cantidadesPorTipo.ContainsKey("Pequeno") && cantidadesPorTipo["Pequeno"] > h.Pequeno) return false;
        if (cantidadesPorTipo.ContainsKey("Roto") && cantidadesPorTipo["Roto"] > h.Roto) return false;
        if (cantidadesPorTipo.ContainsKey("Desecho") && cantidadesPorTipo["Desecho"] > h.Desecho) return false;
        if (cantidadesPorTipo.ContainsKey("Otro") && cantidadesPorTipo["Otro"] > h.Otro) return false;
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HuevoItemSeguimientoDto>> ObtenerDisponibilidadHuevoItemsLPPAsync(int lotePosturaProduccionId)
    {
        var producidoMetadata = await _context.SeguimientoProduccion
            .AsNoTracking()
            .Where(s => s.LotePosturaProduccionId == lotePosturaProduccionId && s.Metadata != null)
            .Select(s => s.Metadata)
            .ToListAsync()
            .ConfigureAwait(false);

        var transferidoMetadata = await _context.TrasladoHuevos
            .AsNoTracking()
            .Where(t =>
                t.LotePosturaProduccionId == lotePosturaProduccionId
                && t.Estado == "Completado"
                && t.DeletedAt == null
                && t.Metadata != null)
            .Select(t => t.Metadata)
            .ToListAsync()
            .ConfigureAwait(false);

        var producidos = producidoMetadata
            .Where(m => m != null)
            .SelectMany(m => HuevoItemsCalculos.LeerDeMetadata(m!.RootElement))
            .ToList();

        var transferidos = transferidoMetadata
            .Where(m => m != null)
            .SelectMany(m => HuevoItemsCalculos.LeerDeMetadata(m!.RootElement))
            .ToList();

        return HuevoItemsCalculos.CalcularDisponibilidad(producidos, transferidos);
    }

    /// <inheritdoc />
    public async Task<bool> ValidarDisponibilidadHuevoItemsLPPAsync(int lotePosturaProduccionId, IReadOnlyList<HuevoItemSeguimientoDto> solicitados)
    {
        if (solicitados.Count == 0) return false;

        var disponibles = await ObtenerDisponibilidadHuevoItemsLPPAsync(lotePosturaProduccionId).ConfigureAwait(false);
        var disponiblePorItem = disponibles.ToDictionary(d => d.CatalogItemId, d => d.Cantidad);

        foreach (var solicitado in solicitados)
        {
            var disponible = disponiblePorItem.GetValueOrDefault(solicitado.CatalogItemId);
            if (solicitado.Cantidad > disponible) return false;
        }

        return true;
    }
}

