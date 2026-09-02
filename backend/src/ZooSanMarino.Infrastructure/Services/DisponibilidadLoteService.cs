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

    /// <summary>
    /// Lee el espejo de huevos del LPP y, si todavía no existe, lo manda a recalcular una vez.
    /// El espejo (<c>espejo_huevo_produccion</c>) es la fórmula ÚNICA de «huevos disponibles»:
    /// <c>*_historico</c> es todo lo producido y <c>*_dinamico</c> es lo que queda tras descontar
    /// los traslados/ventas Completados. Los dos caminos de este service —por lote y por LPP—
    /// pasan por acá para no tener dos aritméticas del mismo número.
    /// </summary>
    private async Task<EspejoHuevoProduccion?> ObtenerEspejoHuevoAsync(int lotePosturaProduccionId)
    {
        var espejo = await _context.EspejoHuevoProduccion
            .AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.LotePosturaProduccionId == lotePosturaProduccionId &&
                e.CompanyId == _currentUser.CompanyId);

        if (espejo != null) return espejo;

        await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(lotePosturaProduccionId).ConfigureAwait(false);

        return await _context.EspejoHuevoProduccion
            .AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.LotePosturaProduccionId == lotePosturaProduccionId &&
                e.CompanyId == _currentUser.CompanyId);
    }

    /// <summary>Arma el bloque de huevos a partir de una cara del espejo (dinámico o histórico).</summary>
    private static HuevosDisponiblesDto ArmarHuevos(
        int total, int incubables, int limpio, int tratado, int sucio, int deforme, int blanco,
        int dobleYema, int piso, int pequeno, int roto, int desecho, int otro,
        int diasEnProduccion, DateTime? fechaUltimoRegistro = null) => new()
    {
        TotalHuevos = total,
        TotalHuevosIncubables = incubables,
        Limpio = limpio,
        Tratado = tratado,
        Sucio = sucio,
        Deforme = deforme,
        Blanco = blanco,
        DobleYema = dobleYema,
        Piso = piso,
        Pequeno = pequeno,
        Roto = roto,
        Desecho = desecho,
        Otro = otro,
        DiasEnProduccion = diasEnProduccion,
        FechaUltimoRegistro = fechaUltimoRegistro
    };

    /// <summary>
    /// Disponibilidad de huevos de un lote en producción.
    ///
    /// ⚠️ Hasta el 2-sep-2026 esto devolvía CERO siempre: sumaba sobre la entidad
    /// <c>SeguimientoDiario</c>, que por <c>ToTable</c> apunta a <c>seguimiento_diario_levante</c>,
    /// filtrando <c>TipoSeguimiento == "produccion"</c> — una condición que en esa tabla no puede
    /// cumplirse (medido: todas sus filas son 'levante'). La producción real vive en
    /// <c>seguimiento_diario_produccion</c>, indexada por <c>LotePosturaProduccionId</c>.
    ///
    /// En vez de re-sumar acá (sería una tercera fórmula del mismo número, justo lo que CLAUDE.md
    /// prohíbe), se resuelve el LPP del lote y se lee el espejo, que es el mismo origen que ya
    /// usaba el camino por LPP.
    ///
    /// Lo que NO cambia: granja, núcleo, galpón y nombre siguen saliendo del LOTE, no del LPP.
    /// `TrasladosController` usa `disponibilidad.GranjaId` como granja origen del movimiento, y
    /// aunque hoy los dos coincidan en los datos, tomarlos del LPP sería apoyarse en eso.
    /// </summary>
    private async Task<DisponibilidadLoteDto> ObtenerDisponibilidadHuevosAsync(Lote loteProd)
    {
        if (!loteProd.LoteId.HasValue)
            throw new InvalidOperationException("Lote sin LoteId.");

        var diasEnProduccion = loteProd.FechaInicioProduccion.HasValue && loteProd.FechaInicioProduccion.Value != default
            ? (DateTime.Today - loteProd.FechaInicioProduccion.Value.Date).Days
            : 0;

        // Empresa por datos + orden determinista: si un lote llegara a tener más de un LPP vivo
        // (hoy la relación es 1:1), se toma siempre el mismo y no uno al azar.
        var lotePosturaProduccionId = await _context.LotePosturaProduccion
            .AsNoTracking()
            .Where(l => l.LoteId == loteProd.LoteId
                        && l.DeletedAt == null
                        && (l.EmpresaId == null || l.EmpresaId == _currentUser.CompanyId))
            .OrderBy(l => l.LotePosturaProduccionId)
            .Select(l => l.LotePosturaProduccionId)
            .FirstOrDefaultAsync();

        var espejo = lotePosturaProduccionId.HasValue && lotePosturaProduccionId.Value > 0
            ? await ObtenerEspejoHuevoAsync(lotePosturaProduccionId.Value)
            : null;

        HuevosDisponiblesDto huevos;
        HuevosDisponiblesDto? historicoEspejo = null;
        IReadOnlyList<HuevoItemSeguimientoDto>? huevoItems = null;

        if (espejo == null)
        {
            // Sin LPP o sin espejo no hay producción que informar: mismo bloque en cero que antes.
            huevos = ArmarHuevos(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, diasEnProduccion);
        }
        else
        {
            // Fecha del último registro: el espejo no la guarda, sale de la tabla de producción.
            var fechaUltimoRegistro = await _context.SeguimientoProduccion
                .AsNoTracking()
                .Where(s => s.LotePosturaProduccionId == lotePosturaProduccionId!.Value)
                .MaxAsync(s => (DateTime?)s.Fecha);

            huevos = ArmarHuevos(
                espejo.HuevoTotDinamico, espejo.HuevoIncDinamico, espejo.HuevoLimpioDinamico,
                espejo.HuevoTratadoDinamico, espejo.HuevoSucioDinamico, espejo.HuevoDeformeDinamico,
                espejo.HuevoBlancoDinamico, espejo.HuevoDobleYemaDinamico, espejo.HuevoPisoDinamico,
                espejo.HuevoPequenoDinamico, espejo.HuevoRotoDinamico, espejo.HuevoDesechoDinamico,
                espejo.HuevoOtroDinamico, diasEnProduccion, fechaUltimoRegistro);

            historicoEspejo = ArmarHuevos(
                espejo.HuevoTotHistorico, espejo.HuevoIncHistorico, espejo.HuevoLimpioHistorico,
                espejo.HuevoTratadoHistorico, espejo.HuevoSucioHistorico, espejo.HuevoDeformeHistorico,
                espejo.HuevoBlancoHistorico, espejo.HuevoDobleYemaHistorico, espejo.HuevoPisoHistorico,
                espejo.HuevoPequenoHistorico, espejo.HuevoRotoHistorico, espejo.HuevoDesechoHistorico,
                espejo.HuevoOtroHistorico, diasEnProduccion, fechaUltimoRegistro);

            huevoItems = await ObtenerDisponibilidadHuevoItemsLPPAsync(lotePosturaProduccionId!.Value)
                .ConfigureAwait(false);
        }

        return new DisponibilidadLoteDto
        {
            LoteId = loteProd.LoteId ?? 0,
            LoteNombre = loteProd.LoteNombre,
            TipoLote = "Produccion",
            LotePosturaProduccionId = lotePosturaProduccionId,
            Aves = null,
            Huevos = huevos,
            HuevosHistoricoEspejo = historicoEspejo,
            HuevoItemsDisponibles = huevoItems,
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

        // Misma lectura del espejo que usa el camino por lote (una sola fórmula del número).
        var espejo = await ObtenerEspejoHuevoAsync(lotePosturaProduccionId).ConfigureAwait(false);

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

