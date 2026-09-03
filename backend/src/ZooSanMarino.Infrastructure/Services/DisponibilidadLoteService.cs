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

    /// <summary>
    /// Disponibilidad de un lote de postura: aves y huevos JUNTOS, no una u otra.
    ///
    /// <para>
    /// Antes esto elegía una rama según <c>lote.Fase</c> y devolvía la otra en <c>null</c>. Dos
    /// defectos encadenados, los dos medidos el 2-sep-2026:
    /// </para>
    /// <list type="number">
    /// <item><c>lote.Fase</c> NO dice en qué fase está el lote: el paso a producción no escribe esa
    /// columna. Los lotes con huevos (13 y 14, 1,54 M y 2,09 M) estaban en <c>fase='Levante'</c> y
    /// nunca llegaban al camino de huevos.</item>
    /// <item>Como la rama de huevos dejaba <c>Aves = null</c>, y
    /// <see cref="ValidarDisponibilidadAvesAsync"/> devuelve <c>false</c> con <c>Aves == null</c>,
    /// todo lote ruteado a huevos tenía los traslados de aves <b>bloqueados</b>. Alcanzaba a A374A y
    /// A374B con 35.372 aves entre las dos y cero filas de producción.</item>
    /// </list>
    ///
    /// <para>
    /// Ahora: la fase sale de <c>FaseLoteCalculos.ResolverFaseVisible</c> —la regla canónica del
    /// repo, levante cerrado <b>y</b> LPP viva—, <c>Aves</c> se informa siempre y <c>Huevos</c>
    /// cuando hay LPP. El front dejó de gatear por <c>TipoLote</c> y gatea por la presencia del
    /// bloque, que es lo que esas pantallas preguntan de verdad.
    /// </para>
    /// </summary>
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

        // La LPP del propio lote y, si no tiene, la del lote hijo (K345A tiene la suya y su hijo
        // K345B tiene otra: se busca primero la propia para no cruzar los dos ciclos).
        var lotePosturaProduccionId = await ResolverLotePosturaProduccionIdAsync(lote);

        // Levante cerrado: la otra mitad de la regla canónica.
        var estadoCierreLevante = await _context.LotePosturaLevante
            .AsNoTracking()
            .Where(l => l.LoteId == lote.LoteId && l.DeletedAt == null)
            .Select(l => l.EstadoCierre)
            .FirstOrDefaultAsync();

        var tipoLote = FaseLoteCalculos.ResolverFaseVisible(
            estadoCierreLevante,
            DisponibilidadLoteCalculos.InformaHuevos(lotePosturaProduccionId));

        var aves = await ObtenerAvesDisponiblesAsync(lote, lotePosturaProduccionId);

        var (huevos, historicoEspejo, huevoItems) =
            DisponibilidadLoteCalculos.InformaHuevos(lotePosturaProduccionId)
                ? await ObtenerHuevosDisponiblesAsync(lote, lotePosturaProduccionId!.Value)
                : (null, null, null);

        return new DisponibilidadLoteDto
        {
            LoteId = lote.LoteId ?? loteIdInt,
            LoteNombre = lote.LoteNombre,
            TipoLote = tipoLote,
            LotePosturaProduccionId = lotePosturaProduccionId,
            Aves = aves,
            Huevos = huevos,
            HuevosHistoricoEspejo = historicoEspejo,
            HuevoItemsDisponibles = huevoItems,
            GranjaId = lote.GranjaId,
            GranjaNombre = lote.Farm?.Name ?? string.Empty,
            NucleoId = lote.NucleoId,
            NucleoNombre = lote.Nucleo?.NucleoNombre,
            GalponId = lote.GalponId,
            GalponNombre = lote.Galpon?.GalponNombre
        };
    }

    /// <summary>
    /// LPP viva del lote: la propia primero, si no la del lote hijo. Orden determinista para que,
    /// si algún día hubiera más de una (hoy la relación es 1:1, medido), se elija siempre la misma.
    /// </summary>
    private async Task<int?> ResolverLotePosturaProduccionIdAsync(Lote lote)
    {
        if (!lote.LoteId.HasValue) return null;

        var propia = await _context.LotePosturaProduccion
            .AsNoTracking()
            .Where(l => l.LoteId == lote.LoteId
                        && l.DeletedAt == null
                        && (l.EmpresaId == null || l.EmpresaId == _currentUser.CompanyId))
            .OrderBy(l => l.LotePosturaProduccionId)
            .Select(l => l.LotePosturaProduccionId)
            .FirstOrDefaultAsync();

        if (propia.HasValue && propia.Value > 0) return propia;

        return await (
            from hijo in _context.Lotes.AsNoTracking()
            join lpp in _context.LotePosturaProduccion.AsNoTracking()
                on hijo.LoteId equals lpp.LoteId
            where hijo.LotePadreId == lote.LoteId
                  && hijo.DeletedAt == null
                  && lpp.DeletedAt == null
                  && (lpp.EmpresaId == null || lpp.EmpresaId == _currentUser.CompanyId)
            orderby lpp.LotePosturaProduccionId
            select lpp.LotePosturaProduccionId).FirstOrDefaultAsync();
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
    /// Bloque de huevos del lote, desde el espejo del LPP. Devuelve también el histórico y el
    /// desglose por ítems, igual que el camino por LPP.
    ///
    /// <para>
    /// La ubicación del DTO la sigue poniendo el llamador desde el LOTE, no desde el LPP:
    /// <c>TrasladosController</c> usa ese <c>GranjaId</c> como granja origen del movimiento y,
    /// aunque hoy los dos coincidan en los datos, apoyarse en eso sería frágil.
    /// </para>
    /// </summary>
    private async Task<(HuevosDisponiblesDto? Huevos, HuevosDisponiblesDto? Historico, IReadOnlyList<HuevoItemSeguimientoDto>? Items)>
        ObtenerHuevosDisponiblesAsync(Lote loteProd, int lotePosturaProduccionId)
    {
        var diasEnProduccion = loteProd.FechaInicioProduccion.HasValue && loteProd.FechaInicioProduccion.Value != default
            ? (DateTime.Today - loteProd.FechaInicioProduccion.Value.Date).Days
            : 0;

        var espejo = await ObtenerEspejoHuevoAsync(lotePosturaProduccionId).ConfigureAwait(false);

        if (espejo == null)
        {
            // Hay LPP pero el espejo no se pudo materializar: se informa el bloque en cero en vez de
            // null, porque la producción existe aunque todavía no tenga cifras.
            return (ArmarHuevos(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, diasEnProduccion), null, null);
        }

        // Fecha del último registro: el espejo no la guarda, sale de la tabla de producción.
        var fechaUltimoRegistro = await _context.SeguimientoProduccion
            .AsNoTracking()
            .Where(s => s.LotePosturaProduccionId == lotePosturaProduccionId)
            .MaxAsync(s => (DateTime?)s.Fecha);

        var huevos = ArmarHuevos(
            espejo.HuevoTotDinamico, espejo.HuevoIncDinamico, espejo.HuevoLimpioDinamico,
            espejo.HuevoTratadoDinamico, espejo.HuevoSucioDinamico, espejo.HuevoDeformeDinamico,
            espejo.HuevoBlancoDinamico, espejo.HuevoDobleYemaDinamico, espejo.HuevoPisoDinamico,
            espejo.HuevoPequenoDinamico, espejo.HuevoRotoDinamico, espejo.HuevoDesechoDinamico,
            espejo.HuevoOtroDinamico, diasEnProduccion, fechaUltimoRegistro);

        var historico = ArmarHuevos(
            espejo.HuevoTotHistorico, espejo.HuevoIncHistorico, espejo.HuevoLimpioHistorico,
            espejo.HuevoTratadoHistorico, espejo.HuevoSucioHistorico, espejo.HuevoDeformeHistorico,
            espejo.HuevoBlancoHistorico, espejo.HuevoDobleYemaHistorico, espejo.HuevoPisoHistorico,
            espejo.HuevoPequenoHistorico, espejo.HuevoRotoHistorico, espejo.HuevoDesechoHistorico,
            espejo.HuevoOtroHistorico, diasEnProduccion, fechaUltimoRegistro);

        var items = await ObtenerDisponibilidadHuevoItemsLPPAsync(lotePosturaProduccionId).ConfigureAwait(false);

        return (huevos, historico, items);
    }

    /// <summary>
    /// Bloque de aves del lote. Se informa SIEMPRE — también en producción, donde antes iba en
    /// <c>null</c> y por eso <see cref="ValidarDisponibilidadAvesAsync"/> bloqueaba los traslados
    /// de aves de lotes que tenían decenas de miles.
    ///
    /// <para>
    /// 🔴 Cuando el lote tiene LPP se descuenta además la mortalidad y la selección de
    /// <c>seguimiento_diario_produccion</c>. La fórmula anterior solo miraba
    /// <c>seguimiento_diario_levante</c> y sobrestimaba el saldo de todo lote que ya hubiera pasado
    /// a producción: medido, 620 aves en el lote 13 y 1.411 en el 14. El número autoriza traslados,
    /// así que la diferencia importaba. Un lote sin LPP pasa 0 en esos dos términos y el resultado
    /// queda idéntico al de antes.
    /// </para>
    /// </summary>
    private async Task<AvesDisponiblesDto> ObtenerAvesDisponiblesAsync(Lote lote, int? lotePosturaProduccionId)
    {
        var loteIdInt = lote.LoteId ?? 0;

        var hembrasIniciales = lote.HembrasL ?? 0;
        var machosIniciales = lote.MachosL ?? 0;

        // Bajas de levante — una sola consulta agregada en vez de traer las filas.
        // Antes solo se sumaba la mortalidad: la selección (11.032 aves medidas) y el error de
        // sexaje (834) quedaban afuera aunque también salieron del lote.
        var lev = await _context.SeguimientoDiario
            .AsNoTracking()
            .Where(s => s.TipoSeguimiento == "levante" && s.LoteId == loteIdInt.ToString())
            .GroupBy(_ => 1)
            .Select(g => new
            {
                MortH = g.Sum(s => (int?)s.MortalidadHembras) ?? 0,
                MortM = g.Sum(s => (int?)s.MortalidadMachos) ?? 0,
                SelH = g.Sum(s => (int?)s.SelH) ?? 0,
                SelM = g.Sum(s => (int?)s.SelM) ?? 0,
                ErrH = g.Sum(s => (int?)s.ErrorSexajeHembras) ?? 0,
                ErrM = g.Sum(s => (int?)s.ErrorSexajeMachos) ?? 0
            })
            .FirstOrDefaultAsync();

        var bajasLevanteHembras = DisponibilidadLoteCalculos.BajasEtapa(
            lev?.MortH ?? 0, lev?.SelH ?? 0, lev?.ErrH ?? 0);
        var bajasLevanteMachos = DisponibilidadLoteCalculos.BajasEtapa(
            lev?.MortM ?? 0, lev?.SelM ?? 0, lev?.ErrM ?? 0);

        // Bajas de producción: solo si el lote llegó a producción.
        var bajasProdHembras = 0;
        var bajasProdMachos = 0;

        if (DisponibilidadLoteCalculos.InformaHuevos(lotePosturaProduccionId))
        {
            var prod = await _context.SeguimientoProduccion
                .AsNoTracking()
                .Where(s => s.LotePosturaProduccionId == lotePosturaProduccionId!.Value)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    MortH = g.Sum(s => (int?)s.MortalidadH) ?? 0,
                    MortM = g.Sum(s => (int?)s.MortalidadM) ?? 0,
                    SelH = g.Sum(s => (int?)s.SelH) ?? 0,
                    SelM = g.Sum(s => (int?)s.SelM) ?? 0,
                    ErrH = g.Sum(s => (int?)s.ErrorSexajeHembras) ?? 0,
                    ErrM = g.Sum(s => (int?)s.ErrorSexajeMachos) ?? 0
                })
                .FirstOrDefaultAsync();

            bajasProdHembras = DisponibilidadLoteCalculos.BajasEtapa(
                prod?.MortH ?? 0, prod?.SelH ?? 0, prod?.ErrH ?? 0);
            bajasProdMachos = DisponibilidadLoteCalculos.BajasEtapa(
                prod?.MortM ?? 0, prod?.SelM ?? 0, prod?.ErrM ?? 0);
        }

        // Retiros: movimientos Completados que salieron del lote. Es independiente de la fase.
        // Se deja tal como estaba (materializa y suma en memoria) a propósito: el filtro cruza una
        // navegación (`InventarioOrigen`) dentro de un OR, y agregarlo en la BD no se pudo verificar
        // contra el motor en esta entrega. Son pocas filas por lote (15 en toda la copia local), así
        // que el costo es despreciable y no se arriesga una traducción que no se probó.
        var retirosCompletados = await _context.MovimientoAves
            .AsNoTracking()
            .Where(m =>
                (m.LoteOrigenId == loteIdInt || m.InventarioOrigen != null && m.InventarioOrigen.LoteId == loteIdInt) &&
                m.Estado == "Completado")
            .ToListAsync();

        var retirosAcumHembras = retirosCompletados.Sum(m => m.CantidadHembras);
        var retirosAcumMachos = retirosCompletados.Sum(m => m.CantidadMachos);

        var hembrasVivas = DisponibilidadLoteCalculos.AvesVivas(
            hembrasIniciales, bajasLevanteHembras, bajasProdHembras, retirosAcumHembras);
        var machosVivos = DisponibilidadLoteCalculos.AvesVivas(
            machosIniciales, bajasLevanteMachos, bajasProdMachos, retirosAcumMachos);

        return new AvesDisponiblesDto
        {
            HembrasVivas = hembrasVivas,
            MachosVivos = machosVivos,
            TotalAves = hembrasVivas + machosVivos,
            HembrasIniciales = hembrasIniciales,
            MachosIniciales = machosIniciales,
            // Se informa la baja TOTAL de las dos etapas: es la que explica el saldo que se muestra.
            MortalidadAcumuladaHembras = bajasLevanteHembras + bajasProdHembras,
            MortalidadAcumuladaMachos = bajasLevanteMachos + bajasProdMachos,
            RetirosAcumuladosHembras = retirosAcumHembras,
            RetirosAcumuladosMachos = retirosAcumMachos
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

