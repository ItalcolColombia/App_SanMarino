// Doble validación de los seguimientos diarios: separación al guardar, descuento al validar.
// Archivo ANCLA: usings, campos, ctor, la interfaz y los helpers compartidos entre concerns.
// El resto vive en Funciones/ como partial de esta misma clase (namespace PLANO).
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ValidacionSeguimientoService : IValidacionSeguimientoService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly IInventarioGestionService? _inventarioGestion;
    private readonly IColombiaInventarioConsumoService? _colombiaConsumoB;
    private readonly ILogger<ValidacionSeguimientoService>? _logger;

    public ValidacionSeguimientoService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        IInventarioGestionService? inventarioGestion = null,
        IColombiaInventarioConsumoService? colombiaConsumoB = null,
        ILogger<ValidacionSeguimientoService>? logger = null)
    {
        _ctx = ctx;
        _current = current;
        _inventarioGestion = inventarioGestion;
        _colombiaConsumoB = colombiaConsumoB;
        _logger = logger;
    }

    /// <summary>Permiso de validar por módulo. Reproductora reusa el que ya existía.</summary>
    public static string PermisoValidar(string modulo) => modulo switch
    {
        ModuloSeguimiento.Levante => "seguimiento_levante.validar",
        ModuloSeguimiento.Produccion => "seguimiento_produccion.validar",
        ModuloSeguimiento.Reproductora => "seguimiento_reproductora_engorde.confirmar",
        _ => "seguimiento_engorde.validar"
    };

    /// <summary>
    /// Permiso para DESHACER una validación. Es distinto del de validar a propósito: deshacer devuelve
    /// inventario y aves ya descontados, que es una operación de corrección, no de captura.
    /// </summary>
    public static string PermisoDesvalidar(string modulo) => modulo switch
    {
        ModuloSeguimiento.Levante => "seguimiento_levante.desvalidar",
        ModuloSeguimiento.Produccion => "seguimiento_produccion.desvalidar",
        _ => "seguimiento_engorde.desvalidar"
    };

    // ─── Flag de la empresa ───────────────────────────────────────────────────

    /// <summary>
    /// ¿La empresa activa opera con doble validación?
    ///
    /// <para>
    /// <b>Fail-closed:</b> si no se resuelve la empresa, devuelve <c>false</c>. Ese default es el que
    /// deja el comportamiento previo intacto — con <c>true</c> por error, un seguimiento dejaría de
    /// descontar sin que nadie lo haya pedido.
    /// </para>
    /// </summary>
    public async Task<bool> RequiereValidacionAsync(CancellationToken ct = default)
        => await RequiereValidacionAsync(_current.CompanyId, ct);

    private async Task<bool> RequiereValidacionAsync(int companyId, CancellationToken ct)
    {
        if (companyId <= 0) return false;
        return await _ctx.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => c.RequiereValidacionSeguimientoDiario)
            .FirstOrDefaultAsync(ct);
    }

    // ─── Estado del registro, por módulo ──────────────────────────────────────

    /// <summary>
    /// Fecha y estado de validación de un registro, resuelto contra la tabla de su módulo.
    ///
    /// <para>
    /// Reproductora lee <c>confirmado</c> y no una columna nueva: esa misma columna la consulta
    /// <c>fn_cruce_reproductora_a_engorde</c> para decidir qué días cruzan a pollo engorde, así que
    /// duplicarla dejaría el cruce mirando un flag que ya nadie escribe.
    /// </para>
    /// </summary>
    private async Task<(bool Existe, bool Validado, DateOnly Fecha, int LoteRefInt)> LeerEstadoAsync(
        string modulo, long seguimientoId, CancellationToken ct)
    {
        switch (modulo)
        {
            case ModuloSeguimiento.Levante:
            {
                var r = await _ctx.SeguimientoDiario.AsNoTracking()
                    .Where(s => s.Id == seguimientoId)
                    .Select(s => new { s.Validado, s.Fecha, s.LotePosturaLevanteId, s.LoteId })
                    .FirstOrDefaultAsync(ct);
                if (r is null) return (false, false, default, 0);
                var loteRef = r.LotePosturaLevanteId ?? (int.TryParse(r.LoteId, out var li) ? li : 0);
                return (true, r.Validado, DateOnly.FromDateTime(r.Fecha), loteRef);
            }
            case ModuloSeguimiento.Produccion:
            {
                var r = await _ctx.SeguimientoProduccion.AsNoTracking()
                    .Where(s => s.Id == seguimientoId && s.DeletedAt == null)
                    .Select(s => new { s.Validado, s.Fecha, s.LotePosturaProduccionId })
                    .FirstOrDefaultAsync(ct);
                if (r is null) return (false, false, default, 0);
                return (true, r.Validado, DateOnly.FromDateTime(r.Fecha), r.LotePosturaProduccionId ?? 0);
            }
            case ModuloSeguimiento.Engorde:
            {
                var r = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
                    .Where(s => s.Id == seguimientoId)
                    .Select(s => new { s.Validado, s.Fecha, s.LoteAveEngordeId })
                    .FirstOrDefaultAsync(ct);
                if (r is null) return (false, false, default, 0);
                return (true, r.Validado, DateOnly.FromDateTime(r.Fecha), r.LoteAveEngordeId);
            }
            case ModuloSeguimiento.EngordeEcuador:
            {
                var r = await _ctx.SeguimientoDiarioAvesEngordeEcuador.AsNoTracking()
                    .Where(s => s.Id == seguimientoId)
                    .Select(s => new { s.Validado, s.Fecha, s.LoteAveEngordeId })
                    .FirstOrDefaultAsync(ct);
                if (r is null) return (false, false, default, 0);
                return (true, r.Validado, DateOnly.FromDateTime(r.Fecha), r.LoteAveEngordeId);
            }
            case ModuloSeguimiento.Reproductora:
            {
                var r = await _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
                    .Where(s => s.Id == seguimientoId)
                    .Select(s => new { s.Confirmado, s.Fecha, s.LoteReproductoraAveEngordeId })
                    .FirstOrDefaultAsync(ct);
                if (r is null) return (false, false, default, 0);
                return (true, r.Confirmado, DateOnly.FromDateTime(r.Fecha), r.LoteReproductoraAveEngordeId);
            }
            default:
                return (false, false, default, 0);
        }
    }

    /// <summary>Escribe la marca de validación en la tabla del módulo. Devuelve false si no existe.</summary>
    private async Task<bool> MarcarValidadoAsync(string modulo, long seguimientoId, bool validado, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        var quien = _current.UserId > 0 ? _current.UserId.ToString() : null;

        switch (modulo)
        {
            case ModuloSeguimiento.Levante:
            {
                var e = await _ctx.SeguimientoDiario.FirstOrDefaultAsync(s => s.Id == seguimientoId, ct);
                if (e is null) return false;
                e.Validado = validado;
                e.ValidadoAt = validado ? ahora : null;
                e.ValidadoPor = validado ? quien : null;
                e.UpdatedAt = ahora;
                break;
            }
            case ModuloSeguimiento.Produccion:
            {
                var e = await _ctx.SeguimientoProduccion.FirstOrDefaultAsync(s => s.Id == seguimientoId, ct);
                if (e is null) return false;
                e.Validado = validado;
                e.ValidadoAt = validado ? ahora : null;
                e.ValidadoPor = validado ? quien : null;
                e.UpdatedAt = ahora;
                break;
            }
            case ModuloSeguimiento.Engorde:
            {
                var e = await _ctx.SeguimientoDiarioAvesEngorde.FirstOrDefaultAsync(s => s.Id == seguimientoId, ct);
                if (e is null) return false;
                e.Validado = validado;
                e.ValidadoAt = validado ? ahora : null;
                e.ValidadoPor = validado ? quien : null;
                e.UpdatedAt = ahora;
                break;
            }
            case ModuloSeguimiento.EngordeEcuador:
            {
                var e = await _ctx.SeguimientoDiarioAvesEngordeEcuador.FirstOrDefaultAsync(s => s.Id == seguimientoId, ct);
                if (e is null) return false;
                e.Validado = validado;
                e.ValidadoAt = validado ? ahora : null;
                e.ValidadoPor = validado ? quien : null;
                e.UpdatedAt = ahora;
                break;
            }
            case ModuloSeguimiento.Reproductora:
            {
                // Escribir `confirmado` es lo que dispara trg_cruce_reproductora_engorde: validar acá
                // es, además del descuento, lo que habilita el cruce hacia pollo engorde.
                var e = await _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde
                    .FirstOrDefaultAsync(s => s.Id == seguimientoId, ct);
                if (e is null) return false;
                e.Confirmado = validado;
                e.ConfirmadoAt = validado ? ahora : null;
                e.ConfirmadoPor = validado ? quien : null;
                e.UpdatedAt = ahora;
                break;
            }
            default:
                return false;
        }

        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Registros sin validar de un lote, con su fecha. Base del semáforo y del bloqueo.</summary>
    private async Task<IReadOnlyList<(long Id, DateOnly Fecha)>> LeerPendientesDelLoteAsync(
        string modulo, int loteId, CancellationToken ct)
    {
        switch (modulo)
        {
            case ModuloSeguimiento.Levante:
            {
                var filas = await _ctx.SeguimientoDiario.AsNoTracking()
                    .Where(s => !s.Validado && s.TipoSeguimiento == "levante"
                             && (s.LotePosturaLevanteId == loteId || s.LoteId == loteId.ToString()))
                    .Select(s => new { s.Id, s.Fecha })
                    .ToListAsync(ct);
                return filas.Select(f => (f.Id, DateOnly.FromDateTime(f.Fecha))).ToList();
            }
            case ModuloSeguimiento.Produccion:
            {
                var filas = await _ctx.SeguimientoProduccion.AsNoTracking()
                    .Where(s => !s.Validado && s.DeletedAt == null
                             && (s.LotePosturaProduccionId == loteId || s.LoteId == loteId))
                    .Select(s => new { s.Id, s.Fecha })
                    .ToListAsync(ct);
                return filas.Select(f => ((long)f.Id, DateOnly.FromDateTime(f.Fecha))).ToList();
            }
            case ModuloSeguimiento.Engorde:
            {
                var filas = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
                    .Where(s => !s.Validado && s.LoteAveEngordeId == loteId)
                    .Select(s => new { s.Id, s.Fecha })
                    .ToListAsync(ct);
                return filas.Select(f => (f.Id, DateOnly.FromDateTime(f.Fecha))).ToList();
            }
            case ModuloSeguimiento.EngordeEcuador:
            {
                var filas = await _ctx.SeguimientoDiarioAvesEngordeEcuador.AsNoTracking()
                    .Where(s => !s.Validado && s.LoteAveEngordeId == loteId)
                    .Select(s => new { s.Id, s.Fecha })
                    .ToListAsync(ct);
                return filas.Select(f => (f.Id, DateOnly.FromDateTime(f.Fecha))).ToList();
            }
            case ModuloSeguimiento.Reproductora:
            {
                var filas = await _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
                    .Where(s => !s.Confirmado && s.LoteReproductoraAveEngordeId == loteId)
                    .Select(s => new { s.Id, s.Fecha })
                    .ToListAsync(ct);
                return filas.Select(f => (f.Id, DateOnly.FromDateTime(f.Fecha))).ToList();
            }
            default:
                return Array.Empty<(long, DateOnly)>();
        }
    }

    /// <summary>Hoy en UTC. Aislado para poder razonar el semáforo sin depender del reloj del server.</summary>
    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Reconstruye el diccionario tipado que esperan los servicios de inventario.</summary>
    private static Dictionary<ItemConsumoKey, decimal> AItemConsumo(IEnumerable<SeguimientoReservaAlimento> reservas)
    {
        var mapa = new Dictionary<ItemConsumoKey, decimal>();
        foreach (var r in reservas)
        {
            var key = new ItemConsumoKey(r.ItemInventarioEcuadorId, r.EsItemInventario, r.SiloId);
            mapa[key] = mapa.GetValueOrDefault(key) + r.CantidadKg;
        }
        return mapa;
    }
}
