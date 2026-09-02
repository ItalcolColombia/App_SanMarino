using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Sync;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Lo que devuelve <c>DespacharAsync</c> (y cada <c>Crear*Async</c> por módulo): el efecto YA
/// ocurrió (la entidad está creada); esto sólo le dice a <see cref="SyncPushService.AplicarUnaAsync"/>
/// si hay que registrar el estado como <c>aplicada</c> o <c>requiere_cuadre</c> (F7).
/// </summary>
internal readonly record struct DespachoResultado(int? EntidadId, string? RespuestaJson, bool EsCuadre, string? DetalleCuadre)
{
    public static DespachoResultado Aplicado(int? entidadId, string? respuestaJson) =>
        new(entidadId, respuestaJson, false, null);

    public static DespachoResultado RequiereCuadre(int? entidadId, string? respuestaJson, string detalle) =>
        new(entidadId, respuestaJson, true, detalle);
}

/// <summary>
/// Aplica lotes de operaciones capturadas sin red (PWA F3).
///
/// ## Las tres reglas que ordenan este service
///
/// 1. **El registro de idempotencia y el efecto commitean juntos.** Si no comparten transacción hay
///    una ventana en la que el efecto quedó aplicado y la marca no: el reintento vuelve a aplicar.
///    En capturas de campo eso es mortalidad contada doble.
/// 2. **La empresa sale de la operación, no del header.** Una captura hecha el sábado en la empresa A
///    no puede aplicarse en la B porque el lunes el usuario abrió otra empresa.
/// 3. **Un rechazo NO se registra.** Ver <see cref="RegistrarSoloLoAplicado"/>.
/// </summary>
public partial class SyncPushService : ISyncPushService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly ISeguimientoLoteLevanteService _levante;
    private readonly IProduccionService _produccion;
    private readonly ISeguimientoDiarioEngordeService _engorde;
    private readonly ISeguimientoDiarioLoteReproductoraService _reproductoraEngorde;
    /// <summary>H4 — alta de gastos de inventario capturada sin red.</summary>
    private readonly IInventarioGastoService _gastos;
    private readonly ILogger<SyncPushService>? _logger;

    public SyncPushService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        ISeguimientoLoteLevanteService levante,
        IProduccionService produccion,
        ISeguimientoDiarioEngordeService engorde,
        ISeguimientoDiarioLoteReproductoraService reproductoraEngorde,
        IInventarioGastoService gastos,
        ILogger<SyncPushService>? logger = null)
    {
        _ctx = ctx;
        _current = current;
        _levante = levante;
        _produccion = produccion;
        _engorde = engorde;
        _reproductoraEngorde = reproductoraEngorde;
        _gastos = gastos;
        _logger = logger;
    }

    /// <summary>
    /// **Por qué un rechazo no deja fila en <c>sync_operaciones</c>.**
    ///
    /// El registro existe para no repetir un EFECTO. Un rechazo no tuvo efecto, así que no hay nada
    /// que proteger — y grabarlo sería peor: una operación rechazada hoy por
    /// <c>empresa_no_autorizada</c> (porque justo estaban corrigiendo la asignación del usuario)
    /// quedaría rechazada **para siempre**, respondiendo el rechazo viejo desde el registro aunque el
    /// mundo ya se hubiera arreglado. Re-evaluar en cada intento es lo correcto: el rechazo es una
    /// función del estado del mundo, no un hecho consumado.
    /// </summary>
    private const string RegistrarSoloLoAplicado =
        "Solo las operaciones aplicadas dejan registro de idempotencia.";

    public async Task<SyncPushResponse> PushAsync(SyncPushRequest request, CancellationToken ct = default)
    {
        var respuesta = new SyncPushResponse();
        var operaciones = request?.Operaciones ?? new List<SyncOperacionRequest>();

        var empresasDelUsuario = await ResolverEmpresasDelUsuarioAsync(ct);
        var vistosEnLote = new List<string>();
        var ahoraUtc = DateTime.UtcNow;

        foreach (var op in operaciones)
        {
            var veredicto = SyncPushCalculos.EvaluarOperacion(
                op.ClientOpId, op.Tipo, op.CompanyId, op.CapturadoAtDispositivo,
                ahoraUtc, empresasDelUsuario, vistosEnLote, _current.CompanyId);

            if (veredicto.Decision == SyncPushCalculos.Decision.Rechazar)
            {
                respuesta.Resultados.Add(Rechazo(op.ClientOpId, veredicto));
                continue;
            }

            var clientOpId = Guid.Parse(op.ClientOpId!.Trim());
            vistosEnLote.Add(clientOpId.ToString());

            respuesta.Resultados.Add(await AplicarUnaAsync(op, clientOpId, ct));
        }

        return respuesta;
    }

    /// <summary>
    /// Empresas que el usuario tiene HOY, al momento de sincronizar.
    ///
    /// Fail-closed a propósito: sin `UserGuid` en el token la lista queda vacía y **todo** se rechaza
    /// como <c>empresa_no_autorizada</c>. Tampoco se le da paso libre al super admin: acá el daño de
    /// un error es una escritura en la empresa equivocada, así que la única fuente es la asignación
    /// real (<c>user_companies</c>).
    /// </summary>
    private async Task<IReadOnlyCollection<int>> ResolverEmpresasDelUsuarioAsync(CancellationToken ct)
    {
        if (_current.UserGuid is not { } userGuid || userGuid == Guid.Empty)
        {
            return Array.Empty<int>();
        }

        return await _ctx.UserCompanies
            .AsNoTracking()
            .Where(uc => uc.UserId == userGuid)
            .Select(uc => uc.CompanyId)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Aplica una operación ya validada: replay si estaba registrada, efecto + registro en una sola
    /// transacción si no.
    /// </summary>
    private async Task<SyncOperacionResultado> AplicarUnaAsync(
        SyncOperacionRequest op, Guid clientOpId, CancellationToken ct)
    {
        var yaRegistrada = await BuscarRegistroAsync(clientOpId, ct);
        if (yaRegistrada is not null)
        {
            return DesdeRegistro(yaRegistrada);
        }

        await using var tx = await _ctx.Database.BeginTransactionAsync(ct);
        try
        {
            var despacho = await DespacharAsync(op, ct);

            // F7: si el módulo tuvo que reintentar sin ítems por falta de stock, esto NO es una
            // aplicación limpia — va a la bandeja de cuadre, con el detalle de qué faltó.
            var estado = despacho.EsCuadre ? SyncPushCalculos.Estados.RequiereCuadre : SyncPushCalculos.Estados.Aplicada;

            _ctx.SyncOperaciones.Add(new SyncOperacion
            {
                ClientOpId = clientOpId,
                Tipo = op.Tipo!,
                // B5 en el camino de sync: el autor lo pone el servidor desde el token.
                UserId = _current.UserId,
                CompanyId = op.CompanyId,
                DeviceId = Recortar(op.DeviceId, 80),
                CapturadoAtDispositivo = op.CapturadoAtDispositivo?.ToUniversalTime(),
                Estado = estado,
                ErrorCodigo = despacho.EsCuadre ? SyncPushCalculos.Errores.DivergenciaStock : null,
                Detalle = despacho.EsCuadre ? despacho.DetalleCuadre : null,
                RespuestaJson = despacho.RespuestaJson,
                EntidadId = despacho.EntidadId,
                RecibidoAt = DateTime.UtcNow
            });

            await _ctx.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new SyncOperacionResultado
            {
                ClientOpId = clientOpId.ToString(),
                Estado = estado,
                ErrorCodigo = despacho.EsCuadre ? SyncPushCalculos.Errores.DivergenciaStock : null,
                Detalle = despacho.EsCuadre ? despacho.DetalleCuadre : null,
                EntidadId = despacho.EntidadId,
                Replay = false
            };
        }
        catch (DbUpdateException ex) when (EsViolacionDeUnicidad(ex))
        {
            // Carrera real: otro envío del MISMO dispositivo (o un reintento que el cliente creyó
            // perdido) ganó la transacción. El UNIQUE de la base es lo que la corta; acá solo hay que
            // devolver lo que ese ganador registró.
            await tx.RollbackAsync(ct);
            _ctx.ChangeTracker.Clear();

            var registro = await BuscarRegistroAsync(clientOpId, ct);
            return registro is not null
                ? DesdeRegistro(registro)
                : Rechazo(clientOpId.ToString(),
                    SyncPushCalculos.Veredicto.Rechazar(SyncPushCalculos.Errores.ErrorInterno, "Conflicto al registrar la operación."));
        }
        catch (InvalidOperationException ex)
        {
            // Regla de negocio del service (lote inexistente, duplicado por fecha…). No deja registro.
            await tx.RollbackAsync(ct);
            _ctx.ChangeTracker.Clear();
            return Rechazo(clientOpId.ToString(), SyncPushCalculos.DesdeReglaDeNegocio(ex.Message));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _ctx.ChangeTracker.Clear();
            _logger?.LogError(ex, "Fallo al aplicar la operación offline {ClientOpId}", clientOpId);
            return Rechazo(clientOpId.ToString(),
                SyncPushCalculos.Veredicto.Rechazar(SyncPushCalculos.Errores.ErrorInterno, "No se pudo aplicar la operación."));
        }
    }

    private Task<SyncOperacion?> BuscarRegistroAsync(Guid clientOpId, CancellationToken ct) =>
        _ctx.SyncOperaciones.AsNoTracking().FirstOrDefaultAsync(x => x.ClientOpId == clientOpId, ct);

    /// <summary>Devuelve la respuesta guardada TAL CUAL: un reintento debe ver lo mismo que el envío que llegó.</summary>
    private static SyncOperacionResultado DesdeRegistro(SyncOperacion registro) => new()
    {
        ClientOpId = registro.ClientOpId.ToString(),
        Estado = registro.Estado,
        ErrorCodigo = registro.ErrorCodigo,
        // F7: en un replay de requiere_cuadre el detalle es lo único que le dice al humano QUÉ
        // faltó — sin esto, reenviar dos veces el mismo push (el caso normal de una tablet que
        // reintenta hasta confirmar) le mostraría el motivo la primera vez y nada la segunda.
        Detalle = registro.Detalle,
        EntidadId = registro.EntidadId,
        Replay = true
    };

    private static SyncOperacionResultado Rechazo(string? clientOpId, SyncPushCalculos.Veredicto veredicto) => new()
    {
        ClientOpId = clientOpId ?? string.Empty,
        Estado = SyncPushCalculos.Estados.Rechazada,
        ErrorCodigo = veredicto.ErrorCodigo,
        Detalle = veredicto.Detalle,
        Replay = false
    };

    /// <summary>23505 = unique_violation de Postgres.</summary>
    private static bool EsViolacionDeUnicidad(DbUpdateException ex) =>
        ex.InnerException?.GetType().GetProperty("SqlState")?.GetValue(ex.InnerException) as string == "23505";

    private static string? Recortar(string? valor, int max) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim()[..Math.Min(valor.Trim().Length, max)];

    /// <summary>
    /// F7 — ¿el request trae algún ítem de inventario? Si NO trae ninguno, una
    /// <c>StockInsuficienteException</c> no puede venir de un array de ítems (no hay descuento que
    /// reintentar sin ellos) — el <c>when</c> del catch en cada <c>Crear*Async</c> se apoya en esto
    /// para no capturar algo que en realidad no tiene reintento posible.
    /// </summary>
    private static bool TraeItems(params IReadOnlyCollection<ItemSeguimientoDto>?[] bloques) =>
        bloques.Any(b => b is { Count: > 0 });

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);
}
