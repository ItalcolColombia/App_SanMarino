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

    // ─── Carga histórica ──────────────────────────────────────────────────────

    /// <summary>
    /// Anidable a propósito: si un import llama a otro, apagar el interno no puede apagar el externo.
    /// Por eso es un contador y no un bool.
    /// </summary>
    private int _cargaHistorica;

    /// <inheritdoc />
    public IDisposable ModoCargaHistorica()
    {
        _cargaHistorica++;
        return new AlcanceCargaHistorica(this);
    }

    private sealed class AlcanceCargaHistorica : IDisposable
    {
        private ValidacionSeguimientoService? _duenio;
        public AlcanceCargaHistorica(ValidacionSeguimientoService duenio) => _duenio = duenio;

        public void Dispose()
        {
            // Idempotente: un `using` que se disponga dos veces no puede desbalancear el contador.
            if (_duenio is null) return;
            if (_duenio._cargaHistorica > 0) _duenio._cargaHistorica--;
            _duenio = null;
        }
    }

    private async Task<bool> RequiereValidacionAsync(int companyId, CancellationToken ct)
    {
        // Dentro de una carga histórica la empresa se comporta como si no usara doble validación:
        // los días importados ya ocurrieron, así que descuentan al guardar y nacen validados.
        if (_cargaHistorica > 0) return false;
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
    private async Task<(bool Existe, bool Validado, DateOnly Fecha, int LoteRefInt, int CompanyId)> LeerEstadoAsync(
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
                if (r is null) return (false, false, default, 0, 0);
                var loteRef = r.LotePosturaLevanteId ?? (int.TryParse(r.LoteId, out var li) ? li : 0);
                var companyLev = r.LotePosturaLevanteId.HasValue
                    ? await _ctx.LotePosturaLevante.AsNoTracking()
                        .Where(l => l.LotePosturaLevanteId == r.LotePosturaLevanteId.Value)
                        .Select(l => l.CompanyId).FirstOrDefaultAsync(ct)
                    : await _ctx.Lotes.AsNoTracking()
                        .Where(l => l.LoteId == loteRef)
                        .Select(l => l.CompanyId).FirstOrDefaultAsync(ct);
                return (true, r.Validado, DateOnly.FromDateTime(r.Fecha), loteRef, companyLev);
            }
            case ModuloSeguimiento.Produccion:
            {
                // `company_id` está en la propia fila de producción (es la única tabla de seguimiento
                // que lo lleva), así que no hace falta ir al maestro para saber de quién es.
                var r = await _ctx.SeguimientoProduccion.AsNoTracking()
                    .Where(s => s.Id == seguimientoId && s.DeletedAt == null)
                    .Select(s => new { s.Validado, s.Fecha, s.LotePosturaProduccionId, s.CompanyId })
                    .FirstOrDefaultAsync(ct);
                if (r is null) return (false, false, default, 0, 0);
                return (true, r.Validado, DateOnly.FromDateTime(r.Fecha), r.LotePosturaProduccionId ?? 0, r.CompanyId);
            }
            // Los DOS módulos de engorde leen la misma tabla: `SeguimientoAvesEngordeEcuadorService`
            // persiste en `_ctx.SeguimientoDiarioAvesEngorde` igual que el de Colombia/Panamá. La tabla
            // partida `seguimiento_diario_aves_engorde_ecuador` no es la fuente —no existe en todos los
            // entornos, y donde existe está vacía—, así que apuntar ahí devolvía "no existe" para un
            // registro que sí estaba.
            case ModuloSeguimiento.Engorde:
            case ModuloSeguimiento.EngordeEcuador:
            {
                var r = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
                    .Where(s => s.Id == seguimientoId)
                    .Select(s => new { s.Validado, s.Fecha, s.LoteAveEngordeId })
                    .FirstOrDefaultAsync(ct);
                if (r is null) return (false, false, default, 0, 0);
                var companyEng = await _ctx.LoteAveEngorde.AsNoTracking()
                    .Where(l => l.LoteAveEngordeId == r.LoteAveEngordeId)
                    .Select(l => l.CompanyId).FirstOrDefaultAsync(ct);
                return (true, r.Validado, DateOnly.FromDateTime(r.Fecha), r.LoteAveEngordeId, companyEng);
            }
            case ModuloSeguimiento.Reproductora:
            {
                var r = await _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
                    .Where(s => s.Id == seguimientoId)
                    .Select(s => new { s.Confirmado, s.Fecha, s.LoteReproductoraAveEngordeId })
                    .FirstOrDefaultAsync(ct);
                if (r is null) return (false, false, default, 0, 0);
                // El lote de reproductora no lleva empresa propia: cuelga del lote de engorde.
                var companyRep = await _ctx.LoteReproductoraAveEngorde.AsNoTracking()
                    .Where(l => l.Id == r.LoteReproductoraAveEngordeId)
                    .Join(_ctx.LoteAveEngorde.AsNoTracking(),
                        l => l.LoteAveEngordeId, e => e.LoteAveEngordeId, (l, e) => e.CompanyId)
                    .FirstOrDefaultAsync(ct);
                return (true, r.Confirmado, DateOnly.FromDateTime(r.Fecha), r.LoteReproductoraAveEngordeId, companyRep);
            }
            default:
                return (false, false, default, 0, 0);
        }
    }

    /// <summary>
    /// El registro es de la empresa activa. <b>Fail-closed</b>: una empresa que no se resuelve (0) o que
    /// no coincide se trata como «no existe».
    ///
    /// <para>
    /// Sin esto, <c>ValidarAsync</c> y <c>DesvalidarAsync</c> buscaban el registro <b>solo por id</b>
    /// —el mensaje decía «no pertenece a la compañía» pero nada lo comprobaba—, así que un usuario con
    /// el permiso de validar podía aplicar el consumo y el descuento de aves de un lote de otra
    /// empresa. Regla 3 de la guía multi-empresa: la empresa efectiva sale de los datos.
    /// </para>
    /// </summary>
    private bool EsDeLaEmpresaActiva(int companyIdDelRegistro) =>
        companyIdDelRegistro > 0 && companyIdDelRegistro == _current.CompanyId;

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
            // Ídem LeerEstadoAsync: una sola tabla para los dos módulos de engorde.
            case ModuloSeguimiento.Engorde:
            case ModuloSeguimiento.EngordeEcuador:
            {
                var e = await _ctx.SeguimientoDiarioAvesEngorde.FirstOrDefaultAsync(s => s.Id == seguimientoId, ct);
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

    /// <summary>
    /// Registros sin validar de un lote, con su fecha. Base del semáforo y del bloqueo.
    ///
    /// <para>
    /// <b>Acotado por empresa, fail-closed.</b> Antes buscaba SOLO por id de lote, igual que hacían
    /// <c>ValidarAsync</c>/<c>DesvalidarAsync</c> antes de <see cref="EsDeLaEmpresaActiva"/>: un
    /// usuario podía pedir los pendientes de un lote de otra empresa y recibir sus fechas, y el
    /// bloqueo del alta se calculaba con datos ajenos. Era inocuo mientras UNA sola empresa tenía el
    /// flag encendido; deja de serlo con la segunda, que ya está planificada.
    /// </para>
    ///
    /// <para>
    /// Un lote cuya empresa no se resuelve (0) o que no es la activa devuelve <b>vacío</b>. Eso no
    /// afloja el bloqueo: <c>AsegurarPuedeRegistrarDiaAsync</c> ya corta antes por
    /// <c>RequiereValidacionAsync</c>, que también depende de la empresa activa.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<(long Id, DateOnly Fecha)>> LeerPendientesDelLoteAsync(
        string modulo, int loteId, CancellationToken ct)
    {
        var companyActiva = _current.CompanyId;

        switch (modulo)
        {
            case ModuloSeguimiento.Levante:
            {
                // El id puede venir como lote de postura-levante o como el `lote_id` legado (texto),
                // así que la empresa se resuelve por el mismo camino que usa LeerEstadoAsync.
                if (!EsDeLaEmpresaActiva(await LeerCompanyDelLoteLevanteAsync(loteId, ct)))
                    return Array.Empty<(long, DateOnly)>();

                var filas = await _ctx.SeguimientoDiario.AsNoTracking()
                    .Where(s => !s.Validado && s.TipoSeguimiento == "levante"
                             && (s.LotePosturaLevanteId == loteId || s.LoteId == loteId.ToString()))
                    .Select(s => new { s.Id, s.Fecha })
                    .ToListAsync(ct);
                return filas.Select(f => (f.Id, DateOnly.FromDateTime(f.Fecha))).ToList();
            }
            case ModuloSeguimiento.Produccion:
            {
                // Producción es la única tabla de seguimiento que lleva `company_id` en la propia
                // fila, así que se filtra ahí mismo: acota las FILAS, no sólo el lote.
                if (companyActiva <= 0) return Array.Empty<(long, DateOnly)>();

                var filas = await _ctx.SeguimientoProduccion.AsNoTracking()
                    .Where(s => !s.Validado && s.DeletedAt == null
                             && s.CompanyId == companyActiva
                             && (s.LotePosturaProduccionId == loteId || s.LoteId == loteId))
                    .Select(s => new { s.Id, s.Fecha })
                    .ToListAsync(ct);
                return filas.Select(f => ((long)f.Id, DateOnly.FromDateTime(f.Fecha))).ToList();
            }
            // Ídem: la tabla partida devolvía SIEMPRE vacío (o 42P01 donde ni existe), así que el
            // semáforo no pintaba nada y el bloqueo por vencidos nunca se disparaba en engorde.
            case ModuloSeguimiento.Engorde:
            case ModuloSeguimiento.EngordeEcuador:
            {
                var companyEng = await _ctx.LoteAveEngorde.AsNoTracking()
                    .Where(l => l.LoteAveEngordeId == loteId && l.DeletedAt == null)
                    .Select(l => l.CompanyId).FirstOrDefaultAsync(ct);
                if (!EsDeLaEmpresaActiva(companyEng)) return Array.Empty<(long, DateOnly)>();

                var filas = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
                    .Where(s => !s.Validado && s.LoteAveEngordeId == loteId)
                    .Select(s => new { s.Id, s.Fecha })
                    .ToListAsync(ct);
                return filas.Select(f => (f.Id, DateOnly.FromDateTime(f.Fecha))).ToList();
            }
            case ModuloSeguimiento.Reproductora:
            {
                // El lote de reproductora no lleva empresa propia: cuelga del lote de engorde.
                var companyRep = await _ctx.LoteReproductoraAveEngorde.AsNoTracking()
                    .Where(l => l.Id == loteId)
                    .Join(_ctx.LoteAveEngorde.AsNoTracking(),
                        l => l.LoteAveEngordeId, e => e.LoteAveEngordeId, (l, e) => e.CompanyId)
                    .FirstOrDefaultAsync(ct);
                if (!EsDeLaEmpresaActiva(companyRep)) return Array.Empty<(long, DateOnly)>();

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

    /// <summary>
    /// Empresa dueña de un lote de levante. El id llega indistintamente como
    /// <c>lote_postura_levante_id</c> o como el <c>lote_id</c> legado, así que se prueban los dos
    /// maestros en el mismo orden que <see cref="LeerEstadoAsync"/>. Devuelve 0 si no resuelve.
    /// </summary>
    private async Task<int> LeerCompanyDelLoteLevanteAsync(int loteId, CancellationToken ct)
    {
        var company = await _ctx.LotePosturaLevante.AsNoTracking()
            .Where(l => l.LotePosturaLevanteId == loteId)
            .Select(l => l.CompanyId).FirstOrDefaultAsync(ct);
        if (company > 0) return company;

        return await _ctx.Lotes.AsNoTracking()
            .Where(l => l.LoteId == loteId)
            .Select(l => l.CompanyId).FirstOrDefaultAsync(ct);
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
