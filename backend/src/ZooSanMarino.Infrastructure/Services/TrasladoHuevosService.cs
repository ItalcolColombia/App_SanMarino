// src/ZooSanMarino.Infrastructure/Services/TrasladoHuevosService.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.DTOs.Traslados;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class TrasladoHuevosService : ITrasladoHuevosService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDisponibilidadLoteService _disponibilidadService;
    private readonly IEspejoHuevoProduccionSyncService _espejoHuevoSync;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TrasladoHuevosService> _logger;

    public TrasladoHuevosService(
        ZooSanMarinoContext context,
        ICurrentUser currentUser,
        IDisponibilidadLoteService disponibilidadService,
        IEspejoHuevoProduccionSyncService espejoHuevoSync,
        IHttpContextAccessor httpContextAccessor,
        ILogger<TrasladoHuevosService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _disponibilidadService = disponibilidadService;
        _espejoHuevoSync = espejoHuevoSync;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private static string? ResolverNombreUsuarioDesdeClaims(IHttpContextAccessor http)
    {
        var user = http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return null;
        return user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.FindFirst("name")?.Value
            ?? user.FindFirst("preferred_username")?.Value
            ?? user.FindFirst(ClaimTypes.GivenName)?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("email")?.Value;
    }

    /// <summary>
    /// D6 — los ítems del traslado tienen que existir como ítem de huevo del catálogo de la empresa
    /// dueña de la granja del lote, y esa empresa tiene que clasificar por ítems.
    ///
    /// <para>
    /// <b>Deliberadamente NO valida la lista blanca del lote (F7.3).</b> Un traslado mueve lo que YA
    /// se produjo: si la declaración del lote cambió después de registrar la producción, exigir la
    /// lista de HOY dejaría huevos reales atrapados sin poder moverlos. La contención correcta acá
    /// es la disponibilidad, que se calcula sobre lo efectivamente producido.
    /// </para>
    /// </summary>
    private async Task ValidarCatalogoHuevoItemsAsync(
        int lotePosturaProduccionId, IReadOnlyCollection<HuevoItemSeguimientoDto> huevoItems)
    {
        var companyId = await _context.LotePosturaProduccion.AsNoTracking()
            .Where(l => l.LotePosturaProduccionId == lotePosturaProduccionId)
            .Join(_context.Farms.AsNoTracking(), l => l.GranjaId, f => f.Id, (_, f) => (int?)f.CompanyId)
            .FirstOrDefaultAsync();

        if (companyId is not > 0)
            throw new InvalidOperationException(
                $"No se pudo resolver la empresa de la granja del lote de producción {lotePosturaProduccionId}.");

        var permite = await _context.Companies.AsNoTracking()
            .Where(c => c.Id == companyId.Value)
            .Select(c => (bool?)c.ClasificacionHuevoPorItems)
            .FirstOrDefaultAsync();
        if (permite != true)
            throw new InvalidOperationException(
                "La empresa de este lote no tiene habilitada la clasificación de huevos por ítems; use las cantidades por tipo.");

        var ids = huevoItems.Select(i => i.CatalogItemId).Distinct().ToArray();
        var existentes = await _context.CatalogItems.AsNoTracking()
            .Where(ci => ci.CompanyId == companyId.Value && ci.ItemType == "huevo" && ci.Activo && ids.Contains(ci.Id))
            .Select(ci => ci.Id)
            .ToListAsync();

        var faltantes = ids.Except(existentes).ToArray();
        if (faltantes.Length > 0)
            throw new InvalidOperationException(
                $"Los siguientes ítems no existen como ítem de huevo ACTIVO del catálogo de la empresa: {string.Join(", ", faltantes)}.");
    }

    public async Task<TrasladoHuevosDto> CrearTrasladoHuevosAsync(CrearTrasladoHuevosDto dto, int usuarioId)
    {
        // Clasificación por ítems (Santa Reyes): sin flujo legacy equivalente — exige LPP porque es
        // el único flujo que produce huevoItems (F7, modal-seguimiento-diario de producción).
        var usaHuevoItems = dto.HuevoItems is { Count: > 0 };
        if (usaHuevoItems && dto.LotePosturaProduccionId is null)
            throw new InvalidOperationException("La clasificación de huevos por ítems requiere lotePosturaProduccionId.");

        if (usaHuevoItems)
        {
            var errorValidacion = HuevoItemsCalculos.Validar(dto.HuevoItems);
            if (errorValidacion != null)
                throw new InvalidOperationException(errorValidacion);

            // D6 — este camino NO validaba contra el catálogo ni exigía el flag de empresa: lo único
            // que lo frenaba era la disponibilidad, y un ítem con Cantidad = 0 la pasa (el chequeo es
            // `solicitado > disponible`). Se alinea con el gate del seguimiento.
            await ValidarCatalogoHuevoItemsAsync(dto.LotePosturaProduccionId!.Value, dto.HuevoItems!);
        }

        var cantidadesPorTipo = new Dictionary<string, int>
        {
            { "Limpio", dto.CantidadLimpio },
            { "Tratado", dto.CantidadTratado },
            { "Sucio", dto.CantidadSucio },
            { "Deforme", dto.CantidadDeforme },
            { "Blanco", dto.CantidadBlanco },
            { "DobleYema", dto.CantidadDobleYema },
            { "Piso", dto.CantidadPiso },
            { "Pequeno", dto.CantidadPequeno },
            { "Roto", dto.CantidadRoto },
            { "Desecho", dto.CantidadDesecho },
            { "Otro", dto.CantidadOtro }
        };

        int granjaOrigenId;
        string loteIdStr;
        int? lotePosturaProduccionId = dto.LotePosturaProduccionId;

        if (lotePosturaProduccionId.HasValue)
        {
            // Flujo LPP: validar desde espejo (legacy) o desde disponible por ítem (Santa Reyes).
            var hayDisponibilidad = usaHuevoItems
                ? await _disponibilidadService.ValidarDisponibilidadHuevoItemsLPPAsync(lotePosturaProduccionId.Value, dto.HuevoItems!)
                : await _disponibilidadService.ValidarDisponibilidadHuevosLPPAsync(lotePosturaProduccionId.Value, cantidadesPorTipo);
            if (!hayDisponibilidad)
                throw new InvalidOperationException("No hay suficientes huevos disponibles para este traslado");

            var lpp = await _context.LotePosturaProduccion
                .AsNoTracking()
                .Include(l => l.Farm)
                .FirstOrDefaultAsync(l => l.LotePosturaProduccionId == lotePosturaProduccionId.Value);
            if (lpp == null)
                throw new InvalidOperationException($"Lote LPP {lotePosturaProduccionId} no encontrado");

            granjaOrigenId = lpp.GranjaId;
            // lote_id referencia el lote unificado (lotes.lote_id); lote_postura_produccion_id es el vínculo LPP.
            loteIdStr = lpp.LoteId.HasValue
                ? lpp.LoteId.Value.ToString()
                : $"LPP-{lotePosturaProduccionId.Value}";
        }
        else
        {
            // Flujo legacy: LoteId
            var hayDisponibilidad = await _disponibilidadService.ValidarDisponibilidadHuevosAsync(dto.LoteId, cantidadesPorTipo);
            if (!hayDisponibilidad)
                throw new InvalidOperationException("No hay suficientes huevos disponibles para este traslado");

            var loteIdInt = int.TryParse(dto.LoteId, out var id) ? id : 0;
            var lote = await _context.Lotes
                .AsNoTracking()
                .Include(l => l.Farm)
                .FirstOrDefaultAsync(l =>
                    l.LoteId == loteIdInt &&
                    l.CompanyId == _currentUser.CompanyId &&
                    l.DeletedAt == null);
            if (lote == null)
                throw new InvalidOperationException($"Lote {dto.LoteId} no encontrado");

            granjaOrigenId = lote.GranjaId;
            loteIdStr = dto.LoteId;
        }

        var traslado = new TrasladoHuevos
        {
            FechaTraslado = dto.FechaTraslado,
            TipoOperacion = dto.TipoOperacion,
            LoteId = loteIdStr,
            LotePosturaProduccionId = lotePosturaProduccionId,
            GranjaOrigenId = granjaOrigenId,
            GranjaDestinoId = dto.GranjaDestinoId,
            LoteDestinoId = dto.LoteDestinoId,
            TipoDestino = dto.TipoDestino,
            Motivo = dto.Motivo,
            Descripcion = dto.Descripcion,
            // Clasificación por ítems: las 11 quedan en 0 (mismo criterio que producción, F7) y el
            // desglose real viaja en Metadata. Flujo legacy: byte a byte igual que siempre.
            CantidadLimpio = usaHuevoItems ? 0 : dto.CantidadLimpio,
            CantidadTratado = usaHuevoItems ? 0 : dto.CantidadTratado,
            CantidadSucio = usaHuevoItems ? 0 : dto.CantidadSucio,
            CantidadDeforme = usaHuevoItems ? 0 : dto.CantidadDeforme,
            CantidadBlanco = usaHuevoItems ? 0 : dto.CantidadBlanco,
            CantidadDobleYema = usaHuevoItems ? 0 : dto.CantidadDobleYema,
            CantidadPiso = usaHuevoItems ? 0 : dto.CantidadPiso,
            CantidadPequeno = usaHuevoItems ? 0 : dto.CantidadPequeno,
            CantidadRoto = usaHuevoItems ? 0 : dto.CantidadRoto,
            CantidadDesecho = usaHuevoItems ? 0 : dto.CantidadDesecho,
            CantidadOtro = usaHuevoItems ? 0 : dto.CantidadOtro,
            TotalHuevos = usaHuevoItems ? HuevoItemsCalculos.SumarTotal(dto.HuevoItems) : dto.TotalHuevos,
            Metadata = usaHuevoItems ? HuevoItemsCalculos.EscribirEnMetadata(null, dto.HuevoItems) : null,
            Estado = "Pendiente",
            UsuarioTrasladoId = usuarioId,
            UsuarioNombre = ResolverNombreUsuarioDesdeClaims(_httpContextAccessor),
            Observaciones = dto.Observaciones,
            CompanyId = _currentUser.CompanyId,
            CreatedByUserId = usuarioId,
            CreatedAt = DateTime.UtcNow
        };

        // Crear, numerar y procesar en UNA transacción. Los dos motivos son
        // distintos y los dos son necesarios:
        //
        // 1. `numero_traslado` es UNIQUE y la entidad arranca con string.Empty.
        //    Antes esto eran dos SaveChanges sueltos: el primero COMMITEABA la
        //    fila con '' y recién el segundo escribía "HUE-…". Dos creates
        //    concurrentes chocaban en ''. Dentro de la transacción el '' nunca
        //    se hace visible, así que el segundo create espera y pasa. Es
        //    exactamente el caso de una cola offline subiendo varios traslados
        //    al recuperar señal.
        //
        // 2. Si el procesamiento falla, no puede quedar un traslado creado
        //    "Pendiente" mientras el cliente recibe 201: el que lo cargó se va
        //    convencido de que descontó los huevos y nadie se entera. Peor con
        //    un cliente que reintenta —cada reintento crearía otro traslado—.
        //    Todo o nada: si no se procesó, no existe, y reintentar es seguro.
        await using var transaccion = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);

        _context.TrasladoHuevos.Add(traslado);
        await _context.SaveChangesAsync();

        traslado.NumeroTraslado = traslado.GenerarNumeroTraslado();
        await _context.SaveChangesAsync();

        if (!await ProcesarTrasladoAsync(traslado.Id).ConfigureAwait(false))
        {
            await transaccion.RollbackAsync().ConfigureAwait(false);
            throw new InvalidOperationException(
                "No se pudo aplicar el descuento de huevos: el traslado no se registró. " +
                "Volvé a intentarlo; si sigue fallando, revisá la disponibilidad del lote.");
        }

        await transaccion.CommitAsync().ConfigureAwait(false);

        return await ToDtoAsync(traslado);
    }

    public async Task<bool> ProcesarTrasladoAsync(int trasladoId)
    {
        var traslado = await _context.TrasladoHuevos
            .FirstOrDefaultAsync(t => 
                t.Id == trasladoId && 
                t.CompanyId == _currentUser.CompanyId && 
                t.DeletedAt == null);

        if (traslado == null || traslado.Estado != "Pendiente")
        {
            return false;
        }

        try
        {
            traslado.Procesar();
            await _context.SaveChangesAsync();

            if (traslado.LotePosturaProduccionId.HasValue)
                await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(traslado.LotePosturaProduccionId.Value).ConfigureAwait(false);
            else
                await AplicarDescuentoEnProduccionDiariaAsync(traslado);

            // Las reducciones se calculan automáticamente en DisponibilidadLoteService
            // al restar los traslados completados de los totales acumulados
            return true;
        }
        catch (Exception ex)
        {
            // Antes acá había un `catch { return false; }` pelado: el motivo real
            // se perdía y el POST devolvía 201 igual. Un traslado que no descontó
            // y nadie puede diagnosticar es peor que uno que falla fuerte.
            _logger.LogError(ex,
                "Falló el procesamiento del traslado de huevos {TrasladoId} (lote {LoteId}, LPP {Lpp})",
                trasladoId, traslado.LoteId, traslado.LotePosturaProduccionId);
            return false;
        }
    }

    /// <summary>
    /// Aplica descuento en el registro diario de producción (tabla unificada seguimiento_diario) restando los huevos trasladados.
    /// Alineado con el seguimiento diario unificado de huevos por lote.
    /// </summary>
    private async Task AplicarDescuentoEnProduccionDiariaAsync(TrasladoHuevos traslado)
    {
        var fechaTraslado = traslado.FechaTraslado.Date;
        var loteIdStr = traslado.LoteId;
        if (string.IsNullOrEmpty(loteIdStr)) return;

        var registroExistente = await _context.SeguimientoDiario
            .Where(s => s.TipoSeguimiento == "produccion" && s.LoteId == loteIdStr && s.Fecha.Date == fechaTraslado)
            .FirstOrDefaultAsync();

        if (registroExistente != null)
        {
            registroExistente.HuevoLimpio = Math.Max(0, (registroExistente.HuevoLimpio ?? 0) - traslado.CantidadLimpio);
            registroExistente.HuevoTratado = Math.Max(0, (registroExistente.HuevoTratado ?? 0) - traslado.CantidadTratado);
            registroExistente.HuevoSucio = Math.Max(0, (registroExistente.HuevoSucio ?? 0) - traslado.CantidadSucio);
            registroExistente.HuevoDeforme = Math.Max(0, (registroExistente.HuevoDeforme ?? 0) - traslado.CantidadDeforme);
            registroExistente.HuevoBlanco = Math.Max(0, (registroExistente.HuevoBlanco ?? 0) - traslado.CantidadBlanco);
            registroExistente.HuevoDobleYema = Math.Max(0, (registroExistente.HuevoDobleYema ?? 0) - traslado.CantidadDobleYema);
            registroExistente.HuevoPiso = Math.Max(0, (registroExistente.HuevoPiso ?? 0) - traslado.CantidadPiso);
            registroExistente.HuevoPequeno = Math.Max(0, (registroExistente.HuevoPequeno ?? 0) - traslado.CantidadPequeno);
            registroExistente.HuevoRoto = Math.Max(0, (registroExistente.HuevoRoto ?? 0) - traslado.CantidadRoto);
            registroExistente.HuevoDesecho = Math.Max(0, (registroExistente.HuevoDesecho ?? 0) - traslado.CantidadDesecho);
            registroExistente.HuevoOtro = Math.Max(0, (registroExistente.HuevoOtro ?? 0) - traslado.CantidadOtro);

            var limpio = registroExistente.HuevoLimpio ?? 0;
            var tratado = registroExistente.HuevoTratado ?? 0;
            var sucio = registroExistente.HuevoSucio ?? 0;
            var deforme = registroExistente.HuevoDeforme ?? 0;
            var blanco = registroExistente.HuevoBlanco ?? 0;
            var dobleYema = registroExistente.HuevoDobleYema ?? 0;
            var piso = registroExistente.HuevoPiso ?? 0;
            var pequeno = registroExistente.HuevoPequeno ?? 0;
            var roto = registroExistente.HuevoRoto ?? 0;
            var desecho = registroExistente.HuevoDesecho ?? 0;
            var otro = registroExistente.HuevoOtro ?? 0;
            registroExistente.HuevoTot = limpio + tratado + sucio + deforme + blanco + dobleYema + piso + pequeno + roto + desecho + otro;
            registroExistente.HuevoInc = limpio + tratado;

            var obsTraslado = $"Descuento por traslado {traslado.NumeroTraslado}";
            registroExistente.Observaciones = string.IsNullOrEmpty(registroExistente.Observaciones)
                ? obsTraslado
                : $"{registroExistente.Observaciones} | {obsTraslado}";
            registroExistente.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
        else
        {
            var registroDescuento = new SeguimientoDiario
            {
                TipoSeguimiento = "produccion",
                LoteId = loteIdStr,
                Fecha = fechaTraslado,
                HuevoLimpio = -traslado.CantidadLimpio,
                HuevoTratado = -traslado.CantidadTratado,
                HuevoSucio = -traslado.CantidadSucio,
                HuevoDeforme = -traslado.CantidadDeforme,
                HuevoBlanco = -traslado.CantidadBlanco,
                HuevoDobleYema = -traslado.CantidadDobleYema,
                HuevoPiso = -traslado.CantidadPiso,
                HuevoPequeno = -traslado.CantidadPequeno,
                HuevoRoto = -traslado.CantidadRoto,
                HuevoDesecho = -traslado.CantidadDesecho,
                HuevoOtro = -traslado.CantidadOtro,
                HuevoTot = -traslado.TotalHuevos,
                HuevoInc = -(traslado.CantidadLimpio + traslado.CantidadTratado),
                Observaciones = $"Registro de descuento por traslado {traslado.NumeroTraslado} - {traslado.TipoOperacion}",
                CreatedAt = DateTime.UtcNow
            };

            _context.SeguimientoDiario.Add(registroDescuento);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Ajusta la producción diaria (tabla unificada seguimiento_diario) cuando se edita un traslado.
    /// </summary>
    private async Task AjustarProduccionDiariaPorEdicionAsync(TrasladoHuevos traslado, Dictionary<string, int> cantidadesOriginales)
    {
        var fechaTraslado = traslado.FechaTraslado.Date;
        var loteIdStr = traslado.LoteId;
        if (string.IsNullOrEmpty(loteIdStr)) return;

        var registroExistente = await _context.SeguimientoDiario
            .Where(s => s.TipoSeguimiento == "produccion" && s.LoteId == loteIdStr && s.Fecha.Date == fechaTraslado)
            .FirstOrDefaultAsync();

        if (registroExistente != null)
        {
            registroExistente.HuevoLimpio = (registroExistente.HuevoLimpio ?? 0) + cantidadesOriginales["Limpio"];
            registroExistente.HuevoTratado = (registroExistente.HuevoTratado ?? 0) + cantidadesOriginales["Tratado"];
            registroExistente.HuevoSucio = (registroExistente.HuevoSucio ?? 0) + cantidadesOriginales["Sucio"];
            registroExistente.HuevoDeforme = (registroExistente.HuevoDeforme ?? 0) + cantidadesOriginales["Deforme"];
            registroExistente.HuevoBlanco = (registroExistente.HuevoBlanco ?? 0) + cantidadesOriginales["Blanco"];
            registroExistente.HuevoDobleYema = (registroExistente.HuevoDobleYema ?? 0) + cantidadesOriginales["DobleYema"];
            registroExistente.HuevoPiso = (registroExistente.HuevoPiso ?? 0) + cantidadesOriginales["Piso"];
            registroExistente.HuevoPequeno = (registroExistente.HuevoPequeno ?? 0) + cantidadesOriginales["Pequeno"];
            registroExistente.HuevoRoto = (registroExistente.HuevoRoto ?? 0) + cantidadesOriginales["Roto"];
            registroExistente.HuevoDesecho = (registroExistente.HuevoDesecho ?? 0) + cantidadesOriginales["Desecho"];
            registroExistente.HuevoOtro = (registroExistente.HuevoOtro ?? 0) + cantidadesOriginales["Otro"];

            registroExistente.HuevoLimpio = Math.Max(0, (registroExistente.HuevoLimpio ?? 0) - traslado.CantidadLimpio);
            registroExistente.HuevoTratado = Math.Max(0, (registroExistente.HuevoTratado ?? 0) - traslado.CantidadTratado);
            registroExistente.HuevoSucio = Math.Max(0, (registroExistente.HuevoSucio ?? 0) - traslado.CantidadSucio);
            registroExistente.HuevoDeforme = Math.Max(0, (registroExistente.HuevoDeforme ?? 0) - traslado.CantidadDeforme);
            registroExistente.HuevoBlanco = Math.Max(0, (registroExistente.HuevoBlanco ?? 0) - traslado.CantidadBlanco);
            registroExistente.HuevoDobleYema = Math.Max(0, (registroExistente.HuevoDobleYema ?? 0) - traslado.CantidadDobleYema);
            registroExistente.HuevoPiso = Math.Max(0, (registroExistente.HuevoPiso ?? 0) - traslado.CantidadPiso);
            registroExistente.HuevoPequeno = Math.Max(0, (registroExistente.HuevoPequeno ?? 0) - traslado.CantidadPequeno);
            registroExistente.HuevoRoto = Math.Max(0, (registroExistente.HuevoRoto ?? 0) - traslado.CantidadRoto);
            registroExistente.HuevoDesecho = Math.Max(0, (registroExistente.HuevoDesecho ?? 0) - traslado.CantidadDesecho);
            registroExistente.HuevoOtro = Math.Max(0, (registroExistente.HuevoOtro ?? 0) - traslado.CantidadOtro);

            var limpio = registroExistente.HuevoLimpio ?? 0;
            var tratado = registroExistente.HuevoTratado ?? 0;
            registroExistente.HuevoTot = limpio + tratado + (registroExistente.HuevoSucio ?? 0) + (registroExistente.HuevoDeforme ?? 0) +
                                         (registroExistente.HuevoBlanco ?? 0) + (registroExistente.HuevoDobleYema ?? 0) +
                                         (registroExistente.HuevoPiso ?? 0) + (registroExistente.HuevoPequeno ?? 0) +
                                         (registroExistente.HuevoRoto ?? 0) + (registroExistente.HuevoDesecho ?? 0) + (registroExistente.HuevoOtro ?? 0);
            registroExistente.HuevoInc = limpio + tratado;

            var obsAjuste = $"Ajuste por edición de traslado {traslado.NumeroTraslado}";
            registroExistente.Observaciones = string.IsNullOrEmpty(registroExistente.Observaciones)
                ? obsAjuste
                : $"{registroExistente.Observaciones} | {obsAjuste}";
            registroExistente.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
        else
        {
            var limpioA = Math.Max(0, -traslado.CantidadLimpio);
            var tratadoA = Math.Max(0, -traslado.CantidadTratado);
            var registroAjuste = new SeguimientoDiario
            {
                TipoSeguimiento = "produccion",
                LoteId = loteIdStr,
                Fecha = fechaTraslado,
                HuevoLimpio = limpioA,
                HuevoTratado = tratadoA,
                HuevoSucio = Math.Max(0, -traslado.CantidadSucio),
                HuevoDeforme = Math.Max(0, -traslado.CantidadDeforme),
                HuevoBlanco = Math.Max(0, -traslado.CantidadBlanco),
                HuevoDobleYema = Math.Max(0, -traslado.CantidadDobleYema),
                HuevoPiso = Math.Max(0, -traslado.CantidadPiso),
                HuevoPequeno = Math.Max(0, -traslado.CantidadPequeno),
                HuevoRoto = Math.Max(0, -traslado.CantidadRoto),
                HuevoDesecho = Math.Max(0, -traslado.CantidadDesecho),
                HuevoOtro = Math.Max(0, -traslado.CantidadOtro),
                Observaciones = $"Ajuste por edición de traslado {traslado.NumeroTraslado}",
                CreatedAt = DateTime.UtcNow
            };
            var st = (registroAjuste.HuevoSucio ?? 0) + (registroAjuste.HuevoDeforme ?? 0) + (registroAjuste.HuevoBlanco ?? 0) +
                     (registroAjuste.HuevoDobleYema ?? 0) + (registroAjuste.HuevoPiso ?? 0) + (registroAjuste.HuevoPequeno ?? 0) +
                     (registroAjuste.HuevoRoto ?? 0) + (registroAjuste.HuevoDesecho ?? 0) + (registroAjuste.HuevoOtro ?? 0);
            registroAjuste.HuevoTot = limpioA + tratadoA + st;
            registroAjuste.HuevoInc = limpioA + tratadoA;

            _context.SeguimientoDiario.Add(registroAjuste);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> CancelarTrasladoAsync(int trasladoId, string motivo)
    {
        var traslado = await _context.TrasladoHuevos
            .FirstOrDefaultAsync(t => 
                t.Id == trasladoId && 
                t.CompanyId == _currentUser.CompanyId && 
                t.DeletedAt == null);

        if (traslado == null)
        {
            return false;
        }

        // Si ya está cancelado, no hacer nada
        if (traslado.Estado == "Cancelado")
        {
            return true;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);
        try
        {
            // Solo si ya estaba Completado se había descontado inventario (LPP: espejo *_dinamico; legacy: seguimiento_diario).
            // Pendiente: no devolver aquí (evita sumar huevos si nunca se procesó el traslado).
            if (traslado.Estado == "Completado")
                await DevolverHuevosAlInventarioAsync(traslado).ConfigureAwait(false);

            // Cancelar el traslado (modificar el método Cancelar para permitir cancelar completados)
            // Como el método de dominio no permite cancelar completados, lo hacemos manualmente
            if (traslado.Estado == "Completado")
            {
                // Permitir cancelar traslados completados para devolver huevos
                traslado.Estado = "Cancelado";
                traslado.FechaCancelacion = DateTime.UtcNow;
                var motivoCompleto = $"Cancelado (traslado completado): {motivo}";
                traslado.Observaciones = string.IsNullOrEmpty(traslado.Observaciones)
                    ? motivoCompleto
                    : $"{traslado.Observaciones} | {motivoCompleto}";
            }
            else
            {
                // Usar el método de dominio para cancelar traslados pendientes
                traslado.Cancelar(motivo);
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);

            // Espejo LPP: al pasar a Cancelado, el movimiento ya no entra en la suma de traslados Completado;
            // RecalcularEspejoHuevoProduccionAsync vuelve a poner *_dinamico = producción − movimientos (solo Completado).
            if (traslado.LotePosturaProduccionId.HasValue)
            {
                _context.ChangeTracker.Clear();
                await _espejoHuevoSync.RecalcularEspejoHuevoProduccionAsync(traslado.LotePosturaProduccionId.Value).ConfigureAwait(false);
            }

            await transaction.CommitAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"Error al cancelar el traslado: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Devuelve los huevos al inventario cuando se cancela un traslado.
    /// Si LPP: el espejo se recalcula en <see cref="CancelarTrasladoAsync"/> tras guardar estado Cancelado.
    /// Si legacy: suma en seguimiento_diario.
    /// </summary>
    private async Task DevolverHuevosAlInventarioAsync(TrasladoHuevos traslado)
    {
        if (traslado.LotePosturaProduccionId.HasValue)
            return;

        var fechaTraslado = traslado.FechaTraslado.Date;
        var loteIdStr = traslado.LoteId;
        if (string.IsNullOrEmpty(loteIdStr)) return;

        var registroExistente = await _context.SeguimientoDiario
            .Where(s => s.TipoSeguimiento == "produccion" && s.LoteId == loteIdStr && s.Fecha.Date == fechaTraslado)
            .FirstOrDefaultAsync();

        if (registroExistente != null)
        {
            registroExistente.HuevoLimpio = (registroExistente.HuevoLimpio ?? 0) + traslado.CantidadLimpio;
            registroExistente.HuevoTratado = (registroExistente.HuevoTratado ?? 0) + traslado.CantidadTratado;
            registroExistente.HuevoSucio = (registroExistente.HuevoSucio ?? 0) + traslado.CantidadSucio;
            registroExistente.HuevoDeforme = (registroExistente.HuevoDeforme ?? 0) + traslado.CantidadDeforme;
            registroExistente.HuevoBlanco = (registroExistente.HuevoBlanco ?? 0) + traslado.CantidadBlanco;
            registroExistente.HuevoDobleYema = (registroExistente.HuevoDobleYema ?? 0) + traslado.CantidadDobleYema;
            registroExistente.HuevoPiso = (registroExistente.HuevoPiso ?? 0) + traslado.CantidadPiso;
            registroExistente.HuevoPequeno = (registroExistente.HuevoPequeno ?? 0) + traslado.CantidadPequeno;
            registroExistente.HuevoRoto = (registroExistente.HuevoRoto ?? 0) + traslado.CantidadRoto;
            registroExistente.HuevoDesecho = (registroExistente.HuevoDesecho ?? 0) + traslado.CantidadDesecho;
            registroExistente.HuevoOtro = (registroExistente.HuevoOtro ?? 0) + traslado.CantidadOtro;

            var limpio = registroExistente.HuevoLimpio ?? 0;
            var tratado = registroExistente.HuevoTratado ?? 0;
            registroExistente.HuevoTot = limpio + tratado + (registroExistente.HuevoSucio ?? 0) + (registroExistente.HuevoDeforme ?? 0) +
                                         (registroExistente.HuevoBlanco ?? 0) + (registroExistente.HuevoDobleYema ?? 0) +
                                         (registroExistente.HuevoPiso ?? 0) + (registroExistente.HuevoPequeno ?? 0) +
                                         (registroExistente.HuevoRoto ?? 0) + (registroExistente.HuevoDesecho ?? 0) + (registroExistente.HuevoOtro ?? 0);
            registroExistente.HuevoInc = limpio + tratado;

            var obsCancelacion = $"Huevos devueltos por cancelación de traslado {traslado.NumeroTraslado}";
            registroExistente.Observaciones = string.IsNullOrEmpty(registroExistente.Observaciones)
                ? obsCancelacion
                : $"{registroExistente.Observaciones} | {obsCancelacion}";
            registroExistente.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
        else
        {
            var registroDevolucion = new SeguimientoDiario
            {
                TipoSeguimiento = "produccion",
                LoteId = loteIdStr,
                Fecha = fechaTraslado,
                HuevoLimpio = traslado.CantidadLimpio,
                HuevoTratado = traslado.CantidadTratado,
                HuevoSucio = traslado.CantidadSucio,
                HuevoDeforme = traslado.CantidadDeforme,
                HuevoBlanco = traslado.CantidadBlanco,
                HuevoDobleYema = traslado.CantidadDobleYema,
                HuevoPiso = traslado.CantidadPiso,
                HuevoPequeno = traslado.CantidadPequeno,
                HuevoRoto = traslado.CantidadRoto,
                HuevoDesecho = traslado.CantidadDesecho,
                HuevoOtro = traslado.CantidadOtro,
                Observaciones = $"Huevos devueltos por cancelación de traslado {traslado.NumeroTraslado}",
                CreatedAt = DateTime.UtcNow
            };
            var st = (registroDevolucion.HuevoSucio ?? 0) + (registroDevolucion.HuevoDeforme ?? 0) + (registroDevolucion.HuevoBlanco ?? 0) +
                     (registroDevolucion.HuevoDobleYema ?? 0) + (registroDevolucion.HuevoPiso ?? 0) + (registroDevolucion.HuevoPequeno ?? 0) +
                     (registroDevolucion.HuevoRoto ?? 0) + (registroDevolucion.HuevoDesecho ?? 0) + (registroDevolucion.HuevoOtro ?? 0);
            registroDevolucion.HuevoTot = (registroDevolucion.HuevoLimpio ?? 0) + (registroDevolucion.HuevoTratado ?? 0) + st;
            registroDevolucion.HuevoInc = (registroDevolucion.HuevoLimpio ?? 0) + (registroDevolucion.HuevoTratado ?? 0);

            _context.SeguimientoDiario.Add(registroDevolucion);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<TrasladoHuevosDto?> ObtenerTrasladoPorIdAsync(int trasladoId)
    {
        var traslado = await _context.TrasladoHuevos
            .AsNoTracking()
            .FirstOrDefaultAsync(t => 
                t.Id == trasladoId && 
                t.CompanyId == _currentUser.CompanyId && 
                t.DeletedAt == null);

        if (traslado == null)
        {
            return null;
        }

        return await ToDtoAsync(traslado);
    }

    public async Task<TrasladoHuevosDto> ActualizarTrasladoHuevosAsync(int trasladoId, ActualizarTrasladoHuevosDto dto, int usuarioId)
    {
        var traslado = await _context.TrasladoHuevos
            .FirstOrDefaultAsync(t => 
                t.Id == trasladoId && 
                t.CompanyId == _currentUser.CompanyId && 
                t.DeletedAt == null);

        if (traslado == null)
        {
            throw new InvalidOperationException($"Traslado {trasladoId} no encontrado");
        }

        if (traslado.Estado != "Pendiente")
        {
            throw new InvalidOperationException($"Solo se pueden actualizar traslados en estado 'Pendiente'. El traslado actual está en estado '{traslado.Estado}'");
        }

        // Si se actualizan las cantidades, validar disponibilidad
        // Necesitamos considerar que las cantidades originales ya están descontadas,
        // así que solo validamos la diferencia
        if (dto.CantidadLimpio.HasValue || dto.CantidadTratado.HasValue || dto.CantidadSucio.HasValue ||
            dto.CantidadDeforme.HasValue || dto.CantidadBlanco.HasValue || dto.CantidadDobleYema.HasValue ||
            dto.CantidadPiso.HasValue || dto.CantidadPequeno.HasValue || dto.CantidadRoto.HasValue ||
            dto.CantidadDesecho.HasValue || dto.CantidadOtro.HasValue)
        {
            // Calcular las nuevas cantidades (usar valores actuales si no se proporcionan)
            var nuevasCantidades = new Dictionary<string, int>();
            nuevasCantidades["Limpio"] = dto.CantidadLimpio ?? traslado.CantidadLimpio;
            nuevasCantidades["Tratado"] = dto.CantidadTratado ?? traslado.CantidadTratado;
            nuevasCantidades["Sucio"] = dto.CantidadSucio ?? traslado.CantidadSucio;
            nuevasCantidades["Deforme"] = dto.CantidadDeforme ?? traslado.CantidadDeforme;
            nuevasCantidades["Blanco"] = dto.CantidadBlanco ?? traslado.CantidadBlanco;
            nuevasCantidades["DobleYema"] = dto.CantidadDobleYema ?? traslado.CantidadDobleYema;
            nuevasCantidades["Piso"] = dto.CantidadPiso ?? traslado.CantidadPiso;
            nuevasCantidades["Pequeno"] = dto.CantidadPequeno ?? traslado.CantidadPequeno;
            nuevasCantidades["Roto"] = dto.CantidadRoto ?? traslado.CantidadRoto;
            nuevasCantidades["Desecho"] = dto.CantidadDesecho ?? traslado.CantidadDesecho;
            nuevasCantidades["Otro"] = dto.CantidadOtro ?? traslado.CantidadOtro;

            // Calcular la diferencia: si las nuevas cantidades son mayores, necesitamos validar disponibilidad
            // Si son menores, no hay problema porque estamos devolviendo huevos
            var cantidadesParaValidar = new Dictionary<string, int>();
            foreach (var tipo in nuevasCantidades.Keys)
            {
                var cantidadOriginal = tipo switch
                {
                    "Limpio" => traslado.CantidadLimpio,
                    "Tratado" => traslado.CantidadTratado,
                    "Sucio" => traslado.CantidadSucio,
                    "Deforme" => traslado.CantidadDeforme,
                    "Blanco" => traslado.CantidadBlanco,
                    "DobleYema" => traslado.CantidadDobleYema,
                    "Piso" => traslado.CantidadPiso,
                    "Pequeno" => traslado.CantidadPequeno,
                    "Roto" => traslado.CantidadRoto,
                    "Desecho" => traslado.CantidadDesecho,
                    "Otro" => traslado.CantidadOtro,
                    _ => 0
                };

                var cantidadNueva = nuevasCantidades[tipo];
                var diferencia = cantidadNueva - cantidadOriginal;

                // Solo validar si la diferencia es positiva (estamos pidiendo más huevos)
                if (diferencia > 0)
                {
                    cantidadesParaValidar[tipo] = diferencia;
                }
            }

            if (cantidadesParaValidar.Count > 0)
            {
                bool hayDisponibilidad;
                if (traslado.LotePosturaProduccionId.HasValue)
                    hayDisponibilidad = await _disponibilidadService.ValidarDisponibilidadHuevosLPPAsync(traslado.LotePosturaProduccionId.Value, cantidadesParaValidar);
                else
                    hayDisponibilidad = await _disponibilidadService.ValidarDisponibilidadHuevosAsync(traslado.LoteId, cantidadesParaValidar);

                if (!hayDisponibilidad)
                {
                    throw new InvalidOperationException("No hay suficientes huevos disponibles para esta actualización. Las nuevas cantidades exceden la disponibilidad actual.");
                }
            }
        }

        // Actualizar campos solo si se proporcionan
        if (dto.FechaTraslado.HasValue)
        {
            traslado.FechaTraslado = dto.FechaTraslado.Value;
        }

        if (!string.IsNullOrEmpty(dto.TipoOperacion))
        {
            traslado.TipoOperacion = dto.TipoOperacion;
        }

        // Guardar cantidades originales ANTES de actualizar (para poder revertir el descuento)
        var cantidadesOriginales = new Dictionary<string, int>
        {
            { "Limpio", traslado.CantidadLimpio },
            { "Tratado", traslado.CantidadTratado },
            { "Sucio", traslado.CantidadSucio },
            { "Deforme", traslado.CantidadDeforme },
            { "Blanco", traslado.CantidadBlanco },
            { "DobleYema", traslado.CantidadDobleYema },
            { "Piso", traslado.CantidadPiso },
            { "Pequeno", traslado.CantidadPequeno },
            { "Roto", traslado.CantidadRoto },
            { "Desecho", traslado.CantidadDesecho },
            { "Otro", traslado.CantidadOtro }
        };

        // Actualizar cantidades
        if (dto.CantidadLimpio.HasValue && dto.CantidadLimpio.Value != traslado.CantidadLimpio)
        {
            traslado.CantidadLimpio = dto.CantidadLimpio.Value;
        }
        if (dto.CantidadTratado.HasValue && dto.CantidadTratado.Value != traslado.CantidadTratado)
        {
            traslado.CantidadTratado = dto.CantidadTratado.Value;
        }
        if (dto.CantidadSucio.HasValue && dto.CantidadSucio.Value != traslado.CantidadSucio)
        {
            traslado.CantidadSucio = dto.CantidadSucio.Value;
        }
        if (dto.CantidadDeforme.HasValue && dto.CantidadDeforme.Value != traslado.CantidadDeforme)
        {
            traslado.CantidadDeforme = dto.CantidadDeforme.Value;
        }
        if (dto.CantidadBlanco.HasValue && dto.CantidadBlanco.Value != traslado.CantidadBlanco)
        {
            traslado.CantidadBlanco = dto.CantidadBlanco.Value;
        }
        if (dto.CantidadDobleYema.HasValue && dto.CantidadDobleYema.Value != traslado.CantidadDobleYema)
        {
            traslado.CantidadDobleYema = dto.CantidadDobleYema.Value;
        }
        if (dto.CantidadPiso.HasValue && dto.CantidadPiso.Value != traslado.CantidadPiso)
        {
            traslado.CantidadPiso = dto.CantidadPiso.Value;
        }
        if (dto.CantidadPequeno.HasValue && dto.CantidadPequeno.Value != traslado.CantidadPequeno)
        {
            traslado.CantidadPequeno = dto.CantidadPequeno.Value;
        }
        if (dto.CantidadRoto.HasValue && dto.CantidadRoto.Value != traslado.CantidadRoto)
        {
            traslado.CantidadRoto = dto.CantidadRoto.Value;
        }
        if (dto.CantidadDesecho.HasValue && dto.CantidadDesecho.Value != traslado.CantidadDesecho)
        {
            traslado.CantidadDesecho = dto.CantidadDesecho.Value;
        }
        if (dto.CantidadOtro.HasValue && dto.CantidadOtro.Value != traslado.CantidadOtro)
        {
            traslado.CantidadOtro = dto.CantidadOtro.Value;
        }

        // Recalcular TotalHuevos tras editar las cantidades legacy. Desde F10 (migración
        // 20260821030415) TotalHuevos pasó de calcularse en vivo (suma de las 11 columnas) a ser
        // columna persistida, fijada solo al crear el traslado. Sin este recálculo, editar las
        // cantidades de un traslado Pendiente dejaba el total desactualizado — y ese valor stale
        // es el que usan el espejo de producción, el descuento diario y el listado al completar el
        // traslado. Solo aplica al camino legacy: si el traslado usa HuevoItems (Metadata != null)
        // las 11 columnas son 0 por diseño y este método no las toca.
        if (traslado.Metadata == null &&
            (dto.CantidadLimpio.HasValue || dto.CantidadTratado.HasValue || dto.CantidadSucio.HasValue ||
             dto.CantidadDeforme.HasValue || dto.CantidadBlanco.HasValue || dto.CantidadDobleYema.HasValue ||
             dto.CantidadPiso.HasValue || dto.CantidadPequeno.HasValue || dto.CantidadRoto.HasValue ||
             dto.CantidadDesecho.HasValue || dto.CantidadOtro.HasValue))
        {
            traslado.TotalHuevos = traslado.CantidadLimpio + traslado.CantidadTratado + traslado.CantidadSucio +
                                    traslado.CantidadDeforme + traslado.CantidadBlanco + traslado.CantidadDobleYema +
                                    traslado.CantidadPiso + traslado.CantidadPequeno + traslado.CantidadRoto +
                                    traslado.CantidadDesecho + traslado.CantidadOtro;
        }

        // Actualizar destino
        if (dto.GranjaDestinoId.HasValue)
        {
            traslado.GranjaDestinoId = dto.GranjaDestinoId.Value;
        }
        else if (dto.GranjaDestinoId == null && dto.TipoOperacion == "Venta")
        {
            // Si cambia a venta, limpiar destino
            traslado.GranjaDestinoId = null;
            traslado.LoteDestinoId = null;
            traslado.TipoDestino = null;
        }

        if (dto.LoteDestinoId != null)
        {
            traslado.LoteDestinoId = dto.LoteDestinoId;
        }

        if (!string.IsNullOrEmpty(dto.TipoDestino))
        {
            traslado.TipoDestino = dto.TipoDestino;
        }

        // Actualizar motivo y descripción
        if (dto.Motivo != null)
        {
            traslado.Motivo = dto.Motivo;
        }

        if (dto.Descripcion != null)
        {
            traslado.Descripcion = dto.Descripcion;
        }

        if (dto.Observaciones != null)
        {
            traslado.Observaciones = dto.Observaciones;
        }

        // Actualizar auditoría
        traslado.UpdatedByUserId = usuarioId;
        traslado.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await ToDtoAsync(traslado);
    }

    public async Task<IEnumerable<TrasladoHuevosDto>> ObtenerTrasladosPorLoteAsync(string loteId)
    {
        var companyId = _currentUser.CompanyId;
        var baseQuery = _context.TrasladoHuevos
            .AsNoTracking()
            .Where(t => t.CompanyId == companyId && t.DeletedAt == null);

        List<TrasladoHuevos> traslados;

        // La UI LPP usa la clave "LPP-{lotePosturaProduccionId}"; los registros nuevos guardan lote_id = id de lotes.
        if (!string.IsNullOrEmpty(loteId)
            && loteId.StartsWith("LPP-", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(loteId.AsSpan(4), out var lppIdFromKey))
        {
            traslados = await baseQuery
                .Where(t => t.LotePosturaProduccionId == lppIdFromKey || t.LoteId == loteId)
                .OrderByDescending(t => t.FechaTraslado)
                .ToListAsync();
        }
        else if (int.TryParse(loteId, out var loteInt))
        {
            var lppIdsForLote = await _context.LotePosturaProduccion
                .AsNoTracking()
                .Where(lpp => lpp.LoteId == loteInt)
                .Select(lpp => lpp.LotePosturaProduccionId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToListAsync();

            traslados = await baseQuery
                .Where(t =>
                    t.LoteId == loteId
                    || (t.LotePosturaProduccionId.HasValue && lppIdsForLote.Contains(t.LotePosturaProduccionId.Value)))
                .OrderByDescending(t => t.FechaTraslado)
                .ToListAsync();
        }
        else
        {
            traslados = await baseQuery
                .Where(t => t.LoteId == loteId)
                .OrderByDescending(t => t.FechaTraslado)
                .ToListAsync();
        }

        // Obtener información de granjas y lotes para todos los traslados
        var granjaIds = traslados
            .SelectMany(t => new[] { t.GranjaOrigenId, t.GranjaDestinoId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var loteIds = traslados
            .Where(t => !string.IsNullOrEmpty(t.LoteId))
            .Select(t => int.TryParse(t.LoteId, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var granjas = await _context.Farms
            .AsNoTracking()
            .Where(f => granjaIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Name);

        var lotes = await _context.Lotes
            .AsNoTracking()
            .Where(l => loteIds.Contains(l.LoteId ?? 0))
            .Where(l => l.LoteId.HasValue)
            .ToDictionaryAsync(l => l.LoteId!.Value, l => l.LoteNombre ?? string.Empty);

        var lppIds = traslados.Where(t => t.LotePosturaProduccionId.HasValue).Select(t => t.LotePosturaProduccionId!.Value).Distinct().ToList();
        Dictionary<int, string>? lppNombres = null;
        if (lppIds.Count > 0)
        {
            var lppList = await _context.LotePosturaProduccion.AsNoTracking().Where(l => lppIds.Contains(l.LotePosturaProduccionId ?? 0)).ToListAsync();
            lppNombres = lppList.Where(l => l.LotePosturaProduccionId.HasValue).ToDictionary(l => l.LotePosturaProduccionId!.Value, l => l.LoteNombre ?? string.Empty);
        }

        return traslados.Select(t => ToDtoSync(t, granjas, lotes, lppNombres));
    }

    private async Task<TrasladoHuevosDto> ToDtoAsync(TrasladoHuevos traslado)
    {
        string loteNombre;
        if (traslado.LotePosturaProduccionId.HasValue)
        {
            var lpp = await _context.LotePosturaProduccion
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LotePosturaProduccionId == traslado.LotePosturaProduccionId.Value);
            loteNombre = lpp?.LoteNombre ?? string.Empty;
        }
        else
        {
            var loteIdInt = int.TryParse(traslado.LoteId, out var id) ? id : 0;
            var lote = await _context.Lotes
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == loteIdInt);
            loteNombre = lote?.LoteNombre ?? string.Empty;
        }

        var granjaOrigen = await _context.Farms
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == traslado.GranjaOrigenId);

        Farm? granjaDestino = null;
        if (traslado.GranjaDestinoId.HasValue)
        {
            granjaDestino = await _context.Farms
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == traslado.GranjaDestinoId.Value);
        }

        return new TrasladoHuevosDto
        {
            Id = traslado.Id,
            NumeroTraslado = traslado.NumeroTraslado,
            FechaTraslado = traslado.FechaTraslado,
            TipoOperacion = traslado.TipoOperacion,
            LoteId = traslado.LoteId,
            LotePosturaProduccionId = traslado.LotePosturaProduccionId,
            LoteNombre = loteNombre,
            GranjaOrigenId = traslado.GranjaOrigenId,
            GranjaOrigenNombre = granjaOrigen?.Name ?? string.Empty,
            GranjaDestinoId = traslado.GranjaDestinoId,
            GranjaDestinoNombre = granjaDestino?.Name,
            LoteDestinoId = traslado.LoteDestinoId,
            TipoDestino = traslado.TipoDestino,
            Motivo = traslado.Motivo,
            Descripcion = traslado.Descripcion,
            CantidadLimpio = traslado.CantidadLimpio,
            CantidadTratado = traslado.CantidadTratado,
            CantidadSucio = traslado.CantidadSucio,
            CantidadDeforme = traslado.CantidadDeforme,
            CantidadBlanco = traslado.CantidadBlanco,
            CantidadDobleYema = traslado.CantidadDobleYema,
            CantidadPiso = traslado.CantidadPiso,
            CantidadPequeno = traslado.CantidadPequeno,
            CantidadRoto = traslado.CantidadRoto,
            CantidadDesecho = traslado.CantidadDesecho,
            CantidadOtro = traslado.CantidadOtro,
            TotalHuevos = traslado.TotalHuevos,
            HuevoItems = traslado.Metadata != null ? HuevoItemsCalculos.LeerDeMetadata(traslado.Metadata.RootElement) : null,
            Estado = traslado.Estado,
            UsuarioTrasladoId = traslado.UsuarioTrasladoId,
            UsuarioNombre = traslado.UsuarioNombre,
            FechaProcesamiento = traslado.FechaProcesamiento,
            FechaCancelacion = traslado.FechaCancelacion,
            Observaciones = traslado.Observaciones,
            CreatedAt = traslado.CreatedAt,
            UpdatedAt = traslado.UpdatedAt
        };
    }

    private TrasladoHuevosDto ToDtoSync(TrasladoHuevos traslado, Dictionary<int, string>? granjas = null, Dictionary<int, string>? lotes = null, Dictionary<int, string>? lppNombres = null)
    {
        string loteNombre;
        if (traslado.LotePosturaProduccionId.HasValue && lppNombres != null && lppNombres.TryGetValue(traslado.LotePosturaProduccionId.Value, out var lppNom))
            loteNombre = lppNom;
        else
        {
            var loteIdInt = int.TryParse(traslado.LoteId, out var id) ? id : 0;
            loteNombre = lotes != null && loteIdInt > 0 && lotes.ContainsKey(loteIdInt) ? lotes[loteIdInt] : string.Empty;
        }
        
        var granjaOrigenNombre = granjas != null && granjas.ContainsKey(traslado.GranjaOrigenId)
            ? granjas[traslado.GranjaOrigenId]
            : string.Empty;
        
        var granjaDestinoNombre = granjas != null && traslado.GranjaDestinoId.HasValue && granjas.ContainsKey(traslado.GranjaDestinoId.Value)
            ? granjas[traslado.GranjaDestinoId.Value]
            : string.Empty;

        // Versión síncrona con información de relaciones precargadas
        return new TrasladoHuevosDto
        {
            Id = traslado.Id,
            NumeroTraslado = traslado.NumeroTraslado,
            FechaTraslado = traslado.FechaTraslado,
            TipoOperacion = traslado.TipoOperacion,
            LoteId = traslado.LoteId,
            LotePosturaProduccionId = traslado.LotePosturaProduccionId,
            LoteNombre = loteNombre,
            GranjaOrigenId = traslado.GranjaOrigenId,
            GranjaOrigenNombre = granjaOrigenNombre,
            GranjaDestinoId = traslado.GranjaDestinoId,
            GranjaDestinoNombre = granjaDestinoNombre,
            LoteDestinoId = traslado.LoteDestinoId,
            TipoDestino = traslado.TipoDestino,
            Motivo = traslado.Motivo,
            Descripcion = traslado.Descripcion,
            CantidadLimpio = traslado.CantidadLimpio,
            CantidadTratado = traslado.CantidadTratado,
            CantidadSucio = traslado.CantidadSucio,
            CantidadDeforme = traslado.CantidadDeforme,
            CantidadBlanco = traslado.CantidadBlanco,
            CantidadDobleYema = traslado.CantidadDobleYema,
            CantidadPiso = traslado.CantidadPiso,
            CantidadPequeno = traslado.CantidadPequeno,
            CantidadRoto = traslado.CantidadRoto,
            CantidadDesecho = traslado.CantidadDesecho,
            CantidadOtro = traslado.CantidadOtro,
            TotalHuevos = traslado.TotalHuevos,
            HuevoItems = traslado.Metadata != null ? HuevoItemsCalculos.LeerDeMetadata(traslado.Metadata.RootElement) : null,
            Estado = traslado.Estado,
            UsuarioTrasladoId = traslado.UsuarioTrasladoId,
            UsuarioNombre = traslado.UsuarioNombre,
            FechaProcesamiento = traslado.FechaProcesamiento,
            FechaCancelacion = traslado.FechaCancelacion,
            Observaciones = traslado.Observaciones,
            CreatedAt = traslado.CreatedAt,
            UpdatedAt = traslado.UpdatedAt
        };
    }
}

