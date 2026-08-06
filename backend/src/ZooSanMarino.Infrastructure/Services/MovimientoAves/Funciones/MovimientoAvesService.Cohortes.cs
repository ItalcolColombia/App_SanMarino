// MovimientoAves/Funciones/MovimientoAvesService.Cohortes.cs
// Cohorte de aves recibidas por el lote destino cuando el traslado se registra desde el módulo
// «Movimientos de Aves» (movimientos MOV-*).
//
// Por qué existe: este camino acreditaba el lote destino y escribía la fila diaria, pero NUNCA creaba la
// cohorte — y encima calculaba la semana del ingreso con el encasetamiento del RECEPTOR, o sea que las aves
// entrantes adoptaban la edad del lote que las recibía. El traslado desde el seguimiento diario y la carga
// masiva sí la creaban; este era el único hueco. Sin la cohorte, un lote con aves de varias procedencias no
// puede decir cuántas trajo cada grupo ni con qué edad.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoAvesService
{
    /// <summary>Largo máximo de <c>lote_aves_cohortes.observaciones</c>.</summary>
    private const int MaxObservacionesCohorteMov = 300;

    /// <summary>
    /// Registra la cohorte del lote DESTINO conservando la edad y la ubicación del lote ORIGEN.
    /// <para>
    /// Se llama al PROCESAR el movimiento (cuando las aves entran de verdad al destino). Es idempotente
    /// por movimiento: si ya hay una cohorte vigente para este id no crea otra, así que reprocesar no
    /// duplica edades.
    /// </para>
    /// <para>
    /// Si el lote origen no tiene <c>fecha_encaset</c> NO se crea la cohorte y el movimiento continúa: la
    /// edad heredada es informativa y nunca debe tumbar un traslado (misma regla que en el traslado desde
    /// el seguimiento diario).
    /// </para>
    /// </summary>
    private async Task RegistrarCohorteDestinoMovimientoAsync(MovimientoAves movimiento)
    {
        if (movimiento.TipoMovimiento != "Traslado") return;
        if (movimiento.LoteDestinoId is not int loteDestinoId) return;
        if (movimiento.LoteOrigenId is not int loteOrigenId) return;
        if (movimiento.CantidadHembras + movimiento.CantidadMachos <= 0) return;

        var yaRegistrada = await _context.LoteAvesCohortes
            .AnyAsync(c => c.MovimientoAvesId == movimiento.Id && c.DeletedAt == null);
        if (yaRegistrada) return;

        var origen = await _context.Lotes.AsNoTracking()
            .Where(l => l.LoteId == loteOrigenId)
            .Select(l => new { l.FechaEncaset, l.LoteNombre, l.GranjaId, l.NucleoId, l.GalponId })
            .FirstOrDefaultAsync();
        if (origen?.FechaEncaset is not DateTime encaset) return;

        var nombreOrigen = string.IsNullOrWhiteSpace(origen.LoteNombre) ? $"lote {loteOrigenId}" : origen.LoteNombre.Trim();
        var observaciones = $"Traslado desde {nombreOrigen}";
        if (observaciones.Length > MaxObservacionesCohorteMov)
            observaciones = observaciones[..MaxObservacionesCohorteMov];

        _context.LoteAvesCohortes.Add(new LoteAvesCohorte
        {
            CompanyId = movimiento.CompanyId,
            LoteId = loteDestinoId,
            LoteOrigenId = loteOrigenId,
            MovimientoAvesId = movimiento.Id,
            // Ubicación CONGELADA: la que trae el movimiento si la tiene, si no la del lote origen.
            GranjaOrigenId = movimiento.GranjaOrigenId ?? origen.GranjaId,
            NucleoOrigenId = string.IsNullOrWhiteSpace(movimiento.NucleoOrigenId) ? origen.NucleoId : movimiento.NucleoOrigenId,
            GalponOrigenId = string.IsNullOrWhiteSpace(movimiento.GalponOrigenId) ? origen.GalponId : movimiento.GalponOrigenId,
            // La fecha del EVENTO (la que digita el usuario), no la de registro en el sistema.
            FechaIngreso = DateOnly.FromDateTime(movimiento.FechaMovimiento.Date),
            FechaEncasetCohorte = DateOnly.FromDateTime(encaset.Date),
            CantidadHembras = movimiento.CantidadHembras,
            CantidadMachos = movimiento.CantidadMachos,
            Observaciones = observaciones,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Da de baja LÓGICA las cohortes creadas por un movimiento (nunca las borra). Se usa al CANCELAR:
    /// las aves vuelven al origen, así que el receptor deja de tenerlas en sus edades.
    /// El borrado del movimiento ya hacía lo propio en <c>MovimientoAvesService.Crud</c>.
    /// </summary>
    private async Task AnularCohortesDeMovimientoAsync(int movimientoId)
    {
        var cohortes = await _context.LoteAvesCohortes
            .Where(c => c.MovimientoAvesId == movimientoId && c.DeletedAt == null)
            .ToListAsync();
        if (cohortes.Count == 0) return;

        foreach (var c in cohortes)
        {
            c.DeletedAt = DateTime.UtcNow;
            c.UpdatedByUserId = _currentUser.UserId;
            c.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }
}
