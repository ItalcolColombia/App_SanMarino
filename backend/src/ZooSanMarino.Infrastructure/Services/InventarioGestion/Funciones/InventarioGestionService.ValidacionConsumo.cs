using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

// Namespace PLANO aunque el archivo esté en subcarpeta (convención de CLAUDE.md).
namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Validación PREVIA de stock para el consumo de alimento — el tercer validador, el que faltaba.
///
/// <para>
/// Ya existían <c>IColombiaInventarioConsumoService.ValidarStockConsumoAsync</c> (nivel granja) e
/// <c>IFarmInventoryConsumoService.ValidarStockConsumoAsync</c> (modelo A). Faltaba el de modelo B
/// <b>con ubicación</b> (núcleo + galpón o silo), que es el de Ecuador y Panamá — y por eso esos dos
/// países eran los únicos donde se podía cargar un consumo de un alimento sin stock: el seguimiento
/// se guardaba primero y el <c>catch</c> del bloque de inventario se comía el rechazo.
/// </para>
///
/// <para>
/// <b>Por qué es una comprobación previa y no un intento.</b> Porque el rechazo tiene que dejar la
/// base intacta. Si se descubriera al aplicar —después del <c>SaveChanges</c> del seguimiento— el
/// registro ya estaría guardado y habría que deshacerlo. Mismo patrón que Colombia: validar todos los
/// ítems, y recién entonces persistir.
/// </para>
///
/// <para>
/// <b>No reemplaza al descuento atómico.</b> Entre esta comprobación y el <c>UPDATE … WHERE quantity
/// &gt;= …</c> de <see cref="DescontarStockAtomicoAsync"/> hay una ventana en la que otro consumo
/// puede llevarse el saldo. Esa carrera la sigue cerrando el descuento; lo que agrega este validador
/// es que el caso NORMAL —no hay stock y no lo va a haber— se rechace antes de escribir nada y con un
/// mensaje que se entiende.
/// </para>
/// </summary>
public partial class InventarioGestionService
{
    /// <summary>
    /// Ubicación efectiva de un consumo: el ítem y la clave natural (núcleo, galpón, silo) contra la
    /// que se busca su stock. Extraído de <c>RegistrarConsumoAsync</c> para que el descuento y su
    /// validación previa resuelvan la MISMA clave.
    /// </summary>
    private async Task<(ItemInventario Item, string? NucleoId, string? GalponId, int? SiloId)>
        ResolverUbicacionConsumoAsync(InventarioGestionConsumoRequest req, CancellationToken ct)
    {
        var item = await _db.ItemInventario.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == req.ItemInventarioEcuadorId, ct);
        if (item == null) throw new InvalidOperationException("El ítem de inventario no existe.");
        var isAlimento = IsAlimento(item);

        // El lote consume de SU silo. La validacion de que el silo este entre los del lote es de la
        // Fase C (la hace el service de consumo del seguimiento diario, que es quien conoce el lote);
        // acá se garantiza lo que este camino sí puede garantizar: que el silo sea de esta granja.
        var modoConsumo = await ResolverModoUbicacionAsync(req.FarmId, ct);
        var errorSiloConsumo = InventarioUbicacionSiloCalculos.ValidarUbicacion(
            modoConsumo, req.SiloId, req.GalponId, isAlimento);
        if (errorSiloConsumo is not null) throw new InvalidOperationException(errorSiloConsumo);

        if (modoConsumo == ModoUbicacionInventario.PorSilo)
            await ValidarSiloDeGranjaAsync(req.FarmId, req.SiloId!.Value, ct);
        else if (isAlimento && (string.IsNullOrWhiteSpace(req.NucleoId) || string.IsNullOrWhiteSpace(req.GalponId)))
            throw new InvalidOperationException("Para ítem tipo alimento debe indicar Núcleo y Galpón.");

        var ajustado = AjustarUbicacionRequest(req, item);

        var (nucleoId, galponId, siloId) = InventarioUbicacionSiloCalculos.NormalizarUbicacion(
            modoConsumo,
            isAlimento && modoConsumo == ModoUbicacionInventario.Clasico ? ajustado.NucleoId!.Trim() : null,
            isAlimento && modoConsumo == ModoUbicacionInventario.Clasico ? ajustado.GalponId!.Trim() : null,
            ajustado.SiloId);

        return (item, nucleoId, galponId, siloId);
    }

    /// <summary>
    /// En modo clásico un ítem que NO es alimento no lleva ubicación: se limpia del request para que
    /// el movimiento quede a nivel granja. Es puro (no toca la base) y por eso se puede llamar dos
    /// veces sin costo.
    /// </summary>
    private InventarioGestionConsumoRequest AjustarUbicacionRequest(
        InventarioGestionConsumoRequest req, ItemInventario item)
    {
        if (!IsAlimento(item)
            && (!string.IsNullOrWhiteSpace(req.NucleoId) || !string.IsNullOrWhiteSpace(req.GalponId)))
            return req with { NucleoId = null, GalponId = null };
        return req;
    }

    /// <summary>
    /// Lanza si alguno de los ítems pedidos no tiene stock suficiente en su ubicación. No modifica
    /// nada. Los pedidos ≤ 0 se ignoran: no consumen.
    /// </summary>
    /// <param name="farmId">Granja del lote.</param>
    /// <param name="nucleoId">Núcleo del lote (modo clásico).</param>
    /// <param name="galponId">Galpón del lote (modo clásico); también es lo que nombra el mensaje.</param>
    /// <param name="byItem">Kilos pedidos por ítem de inventario.</param>
    /// <param name="siloId">Silo, en las empresas que manejan inventario por silo.</param>
    public async Task ValidarStockConsumoAsync(
        int farmId,
        string? nucleoId,
        string? galponId,
        IReadOnlyDictionary<int, decimal> byItem,
        int? siloId = null,
        CancellationToken ct = default)
    {
        var pedidos = byItem.Where(kv => kv.Value > 0).ToArray();
        if (pedidos.Length == 0) return;

        var ids = pedidos.Select(kv => kv.Key).ToArray();
        var nombres = await _db.ItemInventario.AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Nombre, ct);

        var disponible = new Dictionary<int, decimal>();
        var lineas = new List<StockConsumoValidacionCalculos.ItemPedido>(pedidos.Length);

        foreach (var kv in pedidos)
        {
            // Se resuelve la ubicación ítem por ítem con el MISMO camino que el descuento: un ítem
            // que no es alimento se guarda a nivel granja aunque el lote tenga galpón, y en las
            // empresas por silo la clave lleva el silo. Buscar todo contra una sola clave daría
            // falsos rechazos en cuanto el día mezcle alimento con un insumo.
            var sonda = new InventarioGestionConsumoRequest(
                farmId, nucleoId, galponId, kv.Key, kv.Value, "kg", null, null, SiloId: siloId);

            var (_, nuc, gal, sil) = await ResolverUbicacionConsumoAsync(sonda, ct);
            var stock = await BuscarStockSinRastreoAsync(farmId, kv.Key, nuc, gal, sil, ct);

            if (stock != null) disponible[kv.Key] = stock.Quantity;
            lineas.Add(new StockConsumoValidacionCalculos.ItemPedido(
                kv.Key, nombres.TryGetValue(kv.Key, out var n) ? n : null, kv.Value));
        }

        var motivo = StockConsumoValidacionCalculos.MotivoStockInsuficiente(
            lineas, disponible, string.IsNullOrWhiteSpace(galponId) ? null : $"el galpón {galponId.Trim()}");

        if (motivo is not null) throw new InvalidOperationException(motivo);
    }
}
