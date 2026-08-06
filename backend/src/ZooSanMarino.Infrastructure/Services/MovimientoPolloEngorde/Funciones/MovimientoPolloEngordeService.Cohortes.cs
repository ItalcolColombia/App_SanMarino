// MovimientoPolloEngorde/Funciones/MovimientoPolloEngordeService.Cohortes.cs
// Cohortes de aves recibidas por un lote de engorde: registro al COMPLETAR el traslado (edad y ubicación
// heredadas del lote origen), baja lógica al revertirlo, y lectura de las edades presentes en un lote.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Traslados;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoPolloEngordeService
{
    /// <summary>Largo máximo de <c>lote_engorde_aves_cohortes.observaciones</c>.</summary>
    private const int MaxObservacionesCohorteEngorde = 300;

    /// <summary>Ubicación y edad del lote ORIGEN de un traslado de engorde.</summary>
    private readonly record struct OrigenCohorteEngorde(
        int? LoteAveEngordeId, DateTime? FechaEncaset, string? Nombre,
        int? GranjaId, string? NucleoId, string? GalponId);

    /// <summary>
    /// Registra la cohorte de aves que ingresa al lote DESTINO conservando la edad y la ubicación del lote
    /// ORIGEN. Se llama desde <c>CompleteAsync</c>, que es el momento en que el maestro del destino sube.
    /// <para>
    /// Si el lote origen no tiene <c>fecha_encaset</c> NO se crea la cohorte y el traslado continúa: la edad
    /// heredada es informativa y nunca debe tumbar un movimiento (misma regla que en postura).
    /// </para>
    /// No persiste por sí sola: el <c>SaveChangesAsync</c> del llamador la incluye en la misma unidad de
    /// trabajo que el descuento/acreditación de aves.
    /// </summary>
    private async Task RegistrarCohorteDestinoEngordeAsync(MovimientoPolloEngorde m)
    {
        // Solo hay cohorte cuando las aves ENTRAN a un lote de engorde (venta/retiro no tienen destino).
        if (m.LoteAveEngordeDestinoId is not int destinoId) return;
        if (m.TotalAves <= 0) return;

        var origen = await ResolverOrigenCohorteEngordeAsync(m);
        if (origen.FechaEncaset is not DateTime encaset) return;

        var nombreOrigen = string.IsNullOrWhiteSpace(origen.Nombre) ? "otro lote" : origen.Nombre!.Trim();
        var observaciones = $"Traslado desde {nombreOrigen}";
        if (observaciones.Length > MaxObservacionesCohorteEngorde)
            observaciones = observaciones[..MaxObservacionesCohorteEngorde];

        _ctx.LoteEngordeAvesCohortes.Add(new LoteEngordeAvesCohorte
        {
            CompanyId = m.CompanyId,
            LoteAveEngordeId = destinoId,
            LoteAveEngordeOrigenId = origen.LoteAveEngordeId,
            MovimientoPolloEngordeId = m.Id,
            // Ubicación CONGELADA: la del movimiento si la trae, si no la del lote origen.
            GranjaOrigenId = m.GranjaOrigenId ?? origen.GranjaId,
            NucleoOrigenId = string.IsNullOrWhiteSpace(m.NucleoOrigenId) ? origen.NucleoId : m.NucleoOrigenId,
            GalponOrigenId = string.IsNullOrWhiteSpace(m.GalponOrigenId) ? origen.GalponId : m.GalponOrigenId,
            // La fecha del EVENTO (la que digita el usuario), no la de registro.
            FechaIngreso = DateOnly.FromDateTime(m.FechaMovimiento.Date),
            FechaEncasetCohorte = DateOnly.FromDateTime(encaset.Date),
            CantidadHembras = m.CantidadHembras,
            CantidadMachos = m.CantidadMachos,
            CantidadMixtas = m.CantidadMixtas,
            Observaciones = observaciones,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        });
    }

    /// <summary>Lote origen del traslado: Ave Engorde directo, o el lote padre si el origen es reproductora.</summary>
    private async Task<OrigenCohorteEngorde> ResolverOrigenCohorteEngordeAsync(MovimientoPolloEngorde m)
    {
        if (m.LoteAveEngordeOrigenId is int idAe)
        {
            var l = await _ctx.LoteAveEngorde.AsNoTracking()
                .Where(x => x.LoteAveEngordeId == idAe)
                .Select(x => new { x.LoteAveEngordeId, x.FechaEncaset, x.LoteNombre, x.GranjaId, x.NucleoId, x.GalponId })
                .FirstOrDefaultAsync();
            return l == null
                ? default
                : new OrigenCohorteEngorde(l.LoteAveEngordeId, l.FechaEncaset, l.LoteNombre, l.GranjaId, l.NucleoId, l.GalponId);
        }

        if (m.LoteReproductoraAveEngordeOrigenId is int idRa)
        {
            // Un lote reproductora hereda el encasetamiento de su lote Ave Engorde padre.
            var r = await _ctx.LoteReproductoraAveEngorde.AsNoTracking()
                .Include(x => x.LoteAveEngorde)
                .Where(x => x.Id == idRa)
                .Select(x => new
                {
                    x.LoteAveEngordeId,
                    FechaEncaset = x.LoteAveEngorde != null ? x.LoteAveEngorde.FechaEncaset : null,
                    Nombre = x.LoteAveEngorde != null ? x.LoteAveEngorde.LoteNombre : null,
                    GranjaId = x.LoteAveEngorde != null ? (int?)x.LoteAveEngorde.GranjaId : null,
                    NucleoId = x.LoteAveEngorde != null ? x.LoteAveEngorde.NucleoId : null,
                    GalponId = x.LoteAveEngorde != null ? x.LoteAveEngorde.GalponId : null
                })
                .FirstOrDefaultAsync();
            return r == null
                ? default
                : new OrigenCohorteEngorde(r.LoteAveEngordeId, r.FechaEncaset, r.Nombre, r.GranjaId, r.NucleoId, r.GalponId);
        }

        return default;
    }

    /// <summary>
    /// Da de baja LÓGICA las cohortes creadas por un movimiento (nunca las borra: el histórico se anula).
    /// Se llama al eliminar o cancelar el movimiento, en la misma transacción que revierte las aves.
    /// </summary>
    private async Task AnularCohortesDeMovimientoEngordeAsync(int movimientoId)
    {
        var cohortes = await _ctx.LoteEngordeAvesCohortes
            .Where(c => c.MovimientoPolloEngordeId == movimientoId && c.DeletedAt == null)
            .ToListAsync();

        foreach (var c in cohortes)
        {
            c.DeletedAt = DateTime.UtcNow;
            c.UpdatedByUserId = _currentUser.UserId;
            c.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Aves RECIBIDAS por traslado (cohortes vigentes) de cada lote, para subir el techo de venta.
    /// Sin cohortes devuelve un diccionario vacío ⇒ el llamador conserva el <c>Inicio</c> tal cual.
    /// </summary>
    private async Task<Dictionary<int, (int Hembras, int Machos, int Mixtas)>> RecibidasPorCohorteAsync(
        IReadOnlyCollection<int> loteIds)
    {
        if (loteIds.Count == 0) return new Dictionary<int, (int, int, int)>();

        var filas = await _ctx.LoteEngordeAvesCohortes.AsNoTracking()
            .Where(c => c.DeletedAt == null && loteIds.Contains(c.LoteAveEngordeId))
            .GroupBy(c => c.LoteAveEngordeId)
            .Select(g => new
            {
                LoteId = g.Key,
                Hembras = g.Sum(x => x.CantidadHembras),
                Machos = g.Sum(x => x.CantidadMachos),
                Mixtas = g.Sum(x => x.CantidadMixtas)
            })
            .ToListAsync();

        return filas.ToDictionary(f => f.LoteId, f => (f.Hembras, f.Machos, f.Mixtas));
    }

    /// <inheritdoc />
    public async Task<LoteCohortesDto?> GetCohortesLoteEngordeAsync(int loteAveEngordeId, CancellationToken ct = default)
    {
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteAveEngordeId
                        && l.CompanyId == _currentUser.CompanyId
                        && l.DeletedAt == null)
            .Select(l => new { l.LoteNombre, l.FechaEncaset, l.HembrasL, l.MachosL, l.Mixtas })
            .FirstOrDefaultAsync(ct);
        if (lote is null) return null;

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // Nombres de granja y galpón resueltos en la BD (LEFT JOIN), no en memoria.
        var filas = await (
            from c in _ctx.LoteEngordeAvesCohortes.AsNoTracking()
            join lo in _ctx.LoteAveEngorde.AsNoTracking() on c.LoteAveEngordeOrigenId equals lo.LoteAveEngordeId into loj
            from lo in loj.DefaultIfEmpty()
            join f in _ctx.Farms.AsNoTracking() on c.GranjaOrigenId equals f.Id into fj
            from f in fj.DefaultIfEmpty()
            where c.LoteAveEngordeId == loteAveEngordeId && c.DeletedAt == null
            orderby c.FechaIngreso descending, c.Id descending
            select new
            {
                c.Id,
                c.LoteAveEngordeOrigenId,
                LoteOrigenNombre = (string?)(lo == null ? null : lo.LoteNombre),
                GranjaOrigenNombre = (string?)(f == null ? null : f.Name),
                c.NucleoOrigenId,
                c.GalponOrigenId,
                c.FechaIngreso,
                c.FechaEncasetCohorte,
                c.CantidadHembras,
                c.CantidadMachos,
                c.CantidadMixtas,
                c.Observaciones
            }).ToListAsync(ct);

        var cohortes = filas.Select(f => new LoteCohorteDto(
            Id: f.Id,
            LoteOrigenId: f.LoteAveEngordeOrigenId,
            LoteOrigenNombre: f.LoteOrigenNombre,
            UbicacionOrigen: LoteCohortesCalculos.DescribirUbicacionOrigen(
                f.GranjaOrigenNombre, f.NucleoOrigenId, f.GalponOrigenId),
            FechaIngreso: f.FechaIngreso,
            FechaEncasetCohorte: f.FechaEncasetCohorte,
            EdadDias: LoteCohortesCalculos.EdadDias(f.FechaEncasetCohorte, hoy),
            EdadSemanas: LoteCohortesCalculos.EdadSemanas(f.FechaEncasetCohorte, hoy),
            CantidadHembras: f.CantidadHembras,
            CantidadMachos: f.CantidadMachos + f.CantidadMixtas, // engorde mixto: se muestra en el bucket de machos
            Observaciones: f.Observaciones
        )).ToList();

        DateOnly? encasetPropia = lote.FechaEncaset is DateTime fe ? DateOnly.FromDateTime(fe) : null;

        // Propias = saldo del maestro − lo recibido (aproximación documentada: las bajas son por lote).
        var recibidasH = filas.Sum(f => f.CantidadHembras);
        var recibidasM = filas.Sum(f => f.CantidadMachos + f.CantidadMixtas);

        return new LoteCohortesDto(
            LoteId: loteAveEngordeId,
            LoteNombre: lote.LoteNombre,
            FechaEncasetPropia: encasetPropia,
            EdadPropiaDias: encasetPropia is DateOnly ep ? LoteCohortesCalculos.EdadDias(ep, hoy) : null,
            EdadPropiaSemanas: encasetPropia is DateOnly ep2 ? LoteCohortesCalculos.EdadSemanas(ep2, hoy) : null,
            HembrasPropias: LoteCohortesCalculos.PropiasDelLote(lote.HembrasL ?? 0, recibidasH),
            MachosPropias: LoteCohortesCalculos.PropiasDelLote((lote.MachosL ?? 0) + (lote.Mixtas ?? 0), recibidasM),
            Cohortes: cohortes
        );
    }
}
