// Separación (reserva) de alimento y aves: crear, reescribir, liberar y consultar lo comprometido.
// Partial de ValidacionSeguimientoService (namespace plano).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ValidacionSeguimientoService
{
    /// <summary>
    /// Separa el alimento y las aves de un registro.
    ///
    /// <para>
    /// <b>Reescribe, no diferencia.</b> Primero libera lo que ese seguimiento tuviera activo y después
    /// escribe lo nuevo. Esa es toda la ventaja del modelo: como nunca se descontó nada, editar no
    /// necesita calcular <c>nuevo − viejo</c> ni emitir devoluciones. El invariante está fijado en
    /// <c>ReservaSeguimientoCalculosTests.EnEdicion_ElResultadoNoDependeDeLoQueHabiaAntes</c>.
    /// </para>
    /// </summary>
    public async Task SepararAsync(SeparacionSeguimientoContexto contexto, CancellationToken ct = default)
    {
        if (contexto is null || !ModuloSeguimiento.EsValido(contexto.Modulo)) return;

        // La reserva se guarda con el literal CANÓNICO: el service de engorde de Ecuador separaba como
        // ENGORDE_EC y el front valida como ENGORDE, así que la reserva no se encontraba al validar.
        var modulo = ModuloSeguimiento.Canonico(contexto.Modulo);

        await LiberarAsync(modulo, contexto.SeguimientoId, ct);

        // 🔴 El stock se valida ACÁ, que es por donde pasan los cinco módulos.
        //
        // Los services gatean su validación previa con `!separa`, así que en la única empresa que
        // separa (la que tiene el flag encendido) NO se validaba stock ni al alta ni a la edición: el
        // único freno quedaba en el front, que además se apaga al editar. Un lote podía comprometer
        // alimento que otro ya tenía separado y enterarse recién al validar, cuando el decremento
        // atómico falla — el escenario exacto que la separación venía a evitar.
        //
        // Va DESPUÉS de `LiberarAsync` a propósito: al editar, la reserva vieja de este mismo registro
        // ya se soltó, así que el disponible no se cuenta a sí mismo. Y solo corre en el camino que
        // separa: con el flag apagado nadie llama a este método, de modo que las demás empresas quedan
        // exactamente como estaban.
        if (_inventarioGestion is not null &&
            InventarioConsumoGate.ResolverModelo(contexto.PaisId) == ModeloInventarioConsumo.ModeloB)
        {
            foreach (var grupo in ReservaSeguimientoCalculos.LineasDeAlimento(contexto.ConsumoPorItem)
                                    .Where(l => l.Kg > 0)
                                    .GroupBy(l => l.Item.SiloId))
            {
                await _inventarioGestion.ValidarStockConsumoAsync(
                    contexto.FarmId, contexto.NucleoId?.Trim(), contexto.GalponId?.Trim(),
                    grupo.ToDictionary(l => l.Item.Id, l => l.Kg),
                    siloId: grupo.Key,
                    ct: ct);
            }
        }

        var ahora = DateTimeOffset.UtcNow;
        var quien = _current.UserId > 0 ? _current.UserId.ToString() : null;
        var companyId = _current.CompanyId;

        foreach (var linea in ReservaSeguimientoCalculos.LineasDeAlimento(contexto.ConsumoPorItem))
        {
            _ctx.SeguimientoReservaAlimento.Add(new SeguimientoReservaAlimento
            {
                CompanyId = companyId,
                PaisId = contexto.PaisId,
                FarmId = contexto.FarmId,
                NucleoId = contexto.NucleoId?.Trim(),
                GalponId = contexto.GalponId?.Trim(),
                SiloId = linea.Item.SiloId,
                ItemInventarioEcuadorId = linea.Item.Id,
                EsItemInventario = linea.Item.EsItemInventario,
                OrigenModulo = modulo,
                OrigenSeguimientoId = contexto.SeguimientoId,
                LoteRef = contexto.LoteRef,
                FechaSeguimiento = contexto.FechaSeguimiento,
                CantidadKg = linea.Kg,
                Unit = "kg",
                Estado = EstadoReservaSeguimiento.Activa,
                CreatedAt = ahora,
                CreatedByUserId = quien
            });
        }

        // Un día sin bajas no genera fila: una reserva de cero no compromete nada y solo ensucia.
        if (!contexto.Aves.EstaVacia)
        {
            _ctx.SeguimientoReservaAves.Add(new SeguimientoReservaAves
            {
                CompanyId = companyId,
                OrigenModulo = modulo,
                OrigenSeguimientoId = contexto.SeguimientoId,
                LoteRefInt = contexto.LoteRefInt,
                LoteRef = contexto.LoteRef,
                FechaSeguimiento = contexto.FechaSeguimiento,
                Hembras = contexto.Aves.Hembras,
                Machos = contexto.Aves.Machos,
                Mixtas = contexto.Aves.Mixtas,
                Estado = EstadoReservaSeguimiento.Activa,
                CreatedAt = ahora,
                CreatedByUserId = quien
            });
        }

        await _ctx.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Libera lo que un registro tenía separado.
    ///
    /// <para>
    /// Las filas no se borran: pasan a <c>LIBERADA</c>. Dejar el rastro es lo que permite reconstruir
    /// después por qué un galpón mostró cierto disponible un día dado — borrarlas haría que una
    /// separación equivocada desapareciera sin dejar huella.
    /// </para>
    /// </summary>
    public async Task LiberarAsync(string modulo, long seguimientoId, CancellationToken ct = default)
    {
        modulo = ModuloSeguimiento.Canonico(modulo);
        var ahora = DateTimeOffset.UtcNow;

        var alimento = await _ctx.SeguimientoReservaAlimento
            .Where(r => r.OrigenModulo == modulo && r.OrigenSeguimientoId == seguimientoId
                     && r.Estado == EstadoReservaSeguimiento.Activa)
            .ToListAsync(ct);
        foreach (var r in alimento)
        {
            r.Estado = EstadoReservaSeguimiento.Liberada;
            r.LiberadaAt = ahora;
        }

        var aves = await _ctx.SeguimientoReservaAves
            .Where(r => r.OrigenModulo == modulo && r.OrigenSeguimientoId == seguimientoId
                     && r.Estado == EstadoReservaSeguimiento.Activa)
            .ToListAsync(ct);
        foreach (var r in aves)
        {
            r.Estado = EstadoReservaSeguimiento.Liberada;
            r.LiberadaAt = ahora;
        }

        if (alimento.Count > 0 || aves.Count > 0)
            await _ctx.SaveChangesAsync(ct);
    }

    /// <summary>
    /// ¿El registro pasó alguna vez por la separación? Mira los tres estados, no solo el activo: una
    /// reserva <c>LIBERADA</c> también prueba que el registro nació bajo doble validación.
    ///
    /// <para>
    /// Sirve para distinguir un registro <b>anterior</b> al encendido del flag —validado porque
    /// descontó al guardar, sin una sola fila de reserva— de uno que se validó por este mecanismo.
    /// La diferencia importa: al primero no se le puede quitar la validación sin dejar el número
    /// mintiendo.
    /// </para>
    /// </summary>
    private async Task<bool> TieneAlgunaReservaAsync(string modulo, long seguimientoId, CancellationToken ct)
    {
        modulo = ModuloSeguimiento.Canonico(modulo);

        return await _ctx.SeguimientoReservaAlimento.AsNoTracking()
                   .AnyAsync(r => r.OrigenModulo == modulo && r.OrigenSeguimientoId == seguimientoId, ct)
            || await _ctx.SeguimientoReservaAves.AsNoTracking()
                   .AnyAsync(r => r.OrigenModulo == modulo && r.OrigenSeguimientoId == seguimientoId, ct);
    }

    /// <summary>
    /// Aves comprometidas de un lote. Sin esto, un traslado o una venta pueden despachar aves que un
    /// registro sin validar ya dio de baja.
    /// </summary>
    public async Task<ReservaAvesLineas> ReservadoDeAvesAsync(string modulo, int loteId, CancellationToken ct = default)
    {
        modulo = ModuloSeguimiento.Canonico(modulo);

        var filas = await _ctx.SeguimientoReservaAves.AsNoTracking()
            .Where(r => r.OrigenModulo == modulo && r.LoteRefInt == loteId
                     && r.Estado == EstadoReservaSeguimiento.Activa)
            .Select(r => new { r.Hembras, r.Machos, r.Mixtas })
            .ToListAsync(ct);

        return new ReservaAvesLineas(
            filas.Sum(f => f.Hembras),
            filas.Sum(f => f.Machos),
            filas.Sum(f => f.Mixtas));
    }
}
