// src/ZooSanMarino.Infrastructure/Services/DisponibilidadLoteService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs.Traslados;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class DisponibilidadLoteService : IDisponibilidadLoteService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;

    public DisponibilidadLoteService(ZooSanMarinoContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
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

        // Seguimientos desde tabla unificada seguimiento_diario (tipo produccion) — alineado con seguimiento diario unificado
        var seguimientos = await _context.SeguimientoDiario
            .AsNoTracking()
            .Where(s => s.TipoSeguimiento == "produccion" && s.LoteId == loteIdStr)
            .ToListAsync();

        // Calcular totales acumulados por tipo de huevo
        var totalLimpio = seguimientos.Sum(s => s.HuevoLimpio ?? 0);
        var totalTratado = seguimientos.Sum(s => s.HuevoTratado ?? 0);
        var totalSucio = seguimientos.Sum(s => s.HuevoSucio ?? 0);
        var totalDeforme = seguimientos.Sum(s => s.HuevoDeforme ?? 0);
        var totalBlanco = seguimientos.Sum(s => s.HuevoBlanco ?? 0);
        var totalDobleYema = seguimientos.Sum(s => s.HuevoDobleYema ?? 0);
        var totalPiso = seguimientos.Sum(s => s.HuevoPiso ?? 0);
        var totalPequeno = seguimientos.Sum(s => s.HuevoPequeno ?? 0);
        var totalRoto = seguimientos.Sum(s => s.HuevoRoto ?? 0);
        var totalDesecho = seguimientos.Sum(s => s.HuevoDesecho ?? 0);
        var totalOtro = seguimientos.Sum(s => s.HuevoOtro ?? 0);

        // Obtener traslados completados para restar
        var trasladosCompletados = await _context.TrasladoHuevos
            .AsNoTracking()
            .Where(t => t.LoteId == loteIdStr && t.Estado == "Completado")
            .ToListAsync();

        // Restar traslados completados
        totalLimpio -= trasladosCompletados.Sum(t => t.CantidadLimpio);
        totalTratado -= trasladosCompletados.Sum(t => t.CantidadTratado);
        totalSucio -= trasladosCompletados.Sum(t => t.CantidadSucio);
        totalDeforme -= trasladosCompletados.Sum(t => t.CantidadDeforme);
        totalBlanco -= trasladosCompletados.Sum(t => t.CantidadBlanco);
        totalDobleYema -= trasladosCompletados.Sum(t => t.CantidadDobleYema);
        totalPiso -= trasladosCompletados.Sum(t => t.CantidadPiso);
        totalPequeno -= trasladosCompletados.Sum(t => t.CantidadPequeno);
        totalRoto -= trasladosCompletados.Sum(t => t.CantidadRoto);
        totalDesecho -= trasladosCompletados.Sum(t => t.CantidadDesecho);
        totalOtro -= trasladosCompletados.Sum(t => t.CantidadOtro);

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

        var fechaUltimoRegistro = seguimientos.Count > 0
            ? seguimientos.Max(s => s.Fecha)
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
}

