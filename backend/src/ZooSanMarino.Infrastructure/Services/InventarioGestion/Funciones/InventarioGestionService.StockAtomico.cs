using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;

// Namespace PLANO aunque el archivo esté en subcarpeta (convención de CLAUDE.md):
// la clase sigue siendo UNA, así que DI, interfaz y comportamiento quedan intactos.
namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Primitivas ATÓMICAS de stock — items A1 y A2 de <c>fase_de_desarrollo/f0a_stock_atomico_plan.md</c>.
///
/// <para>
/// <b>El defecto que reemplazan.</b> Todos los caminos de stock hacían read-modify-write:
/// </para>
/// <code>
/// var stock = await _db.InventarioGestionStock.FirstOrDefaultAsync(...);   // lee
/// if (stock == null || stock.Quantity &lt; req.Quantity) throw ...;           // decide
/// stock.Quantity -= req.Quantity;                                          // escribe
/// </code>
/// <para>
/// Entre la decisión y el <c>SaveChanges</c> no hay nada que impida que otra operación pase por el
/// mismo camino. Dos consumos de 100 sobre un stock de 150 pasan <b>los dos</b> la validación y el
/// saldo termina en <b>−50</b>: se despachó alimento que no existía. Y en el lado de la suma, dos
/// ingresos concurrentes sobre una clave sin fila insertaban <b>dos</b> filas, de las cuales todas
/// las lecturas (<c>FirstOrDefault</c>) ven solo una.
/// </para>
///
/// <para>
/// La corrección es la misma en los dos casos: <b>que la condición y la escritura ocurran en la
/// misma sentencia SQL</b>. La base es el único punto donde se pueden serializar.
/// </para>
/// </summary>
public partial class InventarioGestionService
{
    /// <summary>
    /// Descuenta stock de forma atómica y condicional.
    ///
    /// <para>
    /// <c>UPDATE ... SET quantity = quantity - @q WHERE id = @id AND quantity &gt;= @q</c>: si otra
    /// transacción ya consumió el saldo, esta sentencia <b>no afecta ninguna fila</b> y el consumo se
    /// rechaza. Nunca puede dejar el saldo negativo, porque la condición se evalúa sobre el valor
    /// vigente en el momento de escribir, no sobre uno leído antes.
    /// </para>
    ///
    /// <para>
    /// ⚠️ El llamador tiene que haber leído la fila con <c>AsNoTracking()</c>. Si la entidad quedara
    /// en el change tracker con su cantidad vieja, un <c>SaveChangesAsync</c> posterior escribiría
    /// ese valor encima y desharía el descuento — reintroduciendo el bug por otro camino.
    /// </para>
    /// </summary>
    /// <returns><c>true</c> si el descuento se aplicó; <c>false</c> si no había saldo (o la fila ya no existe).</returns>
    private async Task<bool> DescontarStockAtomicoAsync(int stockId, decimal cantidad, CancellationToken ct)
    {
        if (!StockAtomicoCalculos.EsCantidadOperable(cantidad))
            throw new InvalidOperationException(StockAtomicoCalculos.MensajeCantidadNoPositiva);

        var filas = await _db.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE inventario_gestion_stock
                  SET quantity = quantity - {cantidad}, updated_at = now()
                WHERE id = {stockId} AND quantity >= {cantidad}",
            ct);

        return StockAtomicoCalculos.DescuentoAplicado(filas);
    }

    /// <summary>
    /// Suma stock creando la fila si no existe, en UNA sola sentencia.
    ///
    /// <para>
    /// <c>INSERT ... ON CONFLICT (clave natural) DO UPDATE SET quantity = stock.quantity + EXCLUDED.quantity</c>.
    /// Con el índice único que crea la migración <c>AddStockClaveNaturalUnica</c>, dos ingresos
    /// concurrentes sobre una clave sin fila dejan <b>una</b> fila con la suma, en vez de dos filas de
    /// las cuales una queda invisible.
    /// </para>
    ///
    /// <para>
    /// El <c>ON CONFLICT</c> nombra la <b>expresión</b> del índice (<c>COALESCE(nucleo_id,'')</c>), no
    /// las columnas: Postgres exige que el inferidor del conflicto coincida exactamente con el índice,
    /// y ese índice es de expresión justamente porque <c>NULL</c> no colisiona con <c>NULL</c>.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>Esta expresión y el índice son UNA sola cosa.</b> La migración
    /// <c>AddInventarioPorSiloEnStockYMovimiento</c> le sumó <c>COALESCE(silo_id, 0)</c> a
    /// <c>ux_inventario_gestion_stock_clave_natural</c>, y los dos se tocaron en el mismo commit: si
    /// se cambia uno sin el otro, Postgres no encuentra el índice inferido y <b>todo ingreso de toda
    /// empresa</b> revienta con «no unique or exclusion constraint matching the ON CONFLICT
    /// specification». Con el flag <c>maneja_inventario_por_silo</c> apagado, <paramref name="siloId"/>
    /// es siempre <c>null</c> ⇒ <c>COALESCE(silo_id,0)</c> vale 0 constante ⇒ la clave es
    /// <b>equivalente</b> a la anterior y ningún saldo se parte.
    /// </para>
    /// </summary>
    /// <param name="siloId">
    /// Silo o bodega de la granja (solo empresas con inventario por silo). <c>null</c> = ubicación
    /// clásica por núcleo/galpón, que es lo que escriben todas las demás empresas.
    /// </param>
    /// <param name="unidad">
    /// La del CATÁLOGO del ítem, resuelta por el llamador con
    /// <see cref="UnidadInventarioCalculos.Resolver"/>. El <c>DO UPDATE</c> la escribe también sobre
    /// la fila existente: es lo que corrige TK-2026-000019.
    /// </param>
    /// <returns>La fila de stock resultante, ya con la cantidad acumulada.</returns>
    private async Task<InventarioGestionStock> SumarStockAtomicoAsync(
        int companyId,
        int paisId,
        int farmId,
        string? nucleoId,
        string? galponId,
        int itemInventarioId,
        decimal cantidad,
        string unidad,
        int? siloId,
        CancellationToken ct)
    {
        if (!StockAtomicoCalculos.EsCantidadOperable(cantidad))
            throw new InvalidOperationException("La cantidad debe ser positiva.");

        var filas = await _db.InventarioGestionStock
            .FromSql(
                $@"INSERT INTO inventario_gestion_stock
                       (company_id, pais_id, farm_id, nucleo_id, galpon_id, silo_id,
                        item_inventario_ecuador_id, quantity, unit, created_at, updated_at)
                   VALUES ({companyId}, {paisId}, {farmId}, {nucleoId}, {galponId}, {siloId},
                        {itemInventarioId}, {cantidad}, {unidad}, now(), now())
                   ON CONFLICT (farm_id, item_inventario_ecuador_id,
                                COALESCE(nucleo_id, ''), COALESCE(galpon_id, ''),
                                COALESCE(silo_id, 0))
                   DO UPDATE SET quantity   = inventario_gestion_stock.quantity + EXCLUDED.quantity,
                                 -- TK-2026-000019: la unidad viene del catálogo del ítem (la
                                 -- resuelve el llamador), así que la fila existente se REALINEA en
                                 -- vez de conservar el 'kg' con el que nació. Antes esto no se
                                 -- pisaba y una fila torcida se quedaba torcida para siempre.
                                 unit       = EXCLUDED.unit,
                                 updated_at = now()
                   RETURNING *")
            .AsNoTracking()
            .ToListAsync(ct);

        // El RETURNING de un upsert siempre devuelve exactamente una fila; si no, algo cambió en el
        // esquema y es mejor enterarse acá que corromper el saldo en silencio.
        if (filas.Count != 1)
            throw new InvalidOperationException(
                $"El upsert de stock devolvió {filas.Count} filas (se esperaba 1). Revise el índice único de la clave natural.");

        return filas[0];
    }

    /// <summary>
    /// Busca la fila de stock de una clave natural, SIN rastreo.
    ///
    /// <para>
    /// El <c>AsNoTracking()</c> es parte del contrato de <see cref="DescontarStockAtomicoAsync"/>, no
    /// una optimización: la fila se va a modificar por SQL crudo, y una copia rastreada con el valor
    /// viejo haría que el <c>SaveChanges</c> siguiente pisara el descuento.
    /// </para>
    /// </summary>
    private Task<InventarioGestionStock?> BuscarStockSinRastreoAsync(
        int farmId,
        int itemInventarioId,
        string? nucleoId,
        string? galponId,
        int? siloId,
        CancellationToken ct) =>
        _db.InventarioGestionStock
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.FarmId == farmId &&
                     x.ItemInventarioEcuadorId == itemInventarioId &&
                     x.NucleoId == nucleoId &&
                     x.GalponId == galponId &&
                     // Con silo nulo traduce a `silo_id IS NULL`, que es la fila de siempre: la
                     // búsqueda tiene que discriminar el silo o un consumo descontaría del saldo
                     // de otro silo del mismo ítem.
                     x.SiloId == siloId,
                ct);

    /// <summary>
    /// Ejecuta <paramref name="operacion"/> dentro de una transacción, abriéndola <b>solo si no hay
    /// una ambiente</b>.
    ///
    /// <para>
    /// El descuento atómico y el <c>SaveChangesAsync</c> que graba el movimiento tienen que ser
    /// atómicos <b>entre sí</b>: si el movimiento fallara después del <c>UPDATE</c>, el stock bajaría
    /// sin ningún registro que lo explique, y el descuadre aparecería semanas más tarde sin rastro de
    /// su origen.
    /// </para>
    ///
    /// <para>
    /// La comprobación de <c>CurrentTransaction</c> no es defensiva por las dudas: varios de estos
    /// métodos se invocan desde services que ya abrieron su propia transacción (el consumo disparado
    /// por un seguimiento diario, entre otros), y anidar <c>BeginTransactionAsync</c> lanza.
    /// </para>
    /// </summary>
    private async Task EnTransaccionAsync(Func<Task> operacion, CancellationToken ct)
    {
        if (_db.Database.CurrentTransaction is not null)
        {
            // Ya hay una transacción del llamador: su commit/rollback cubre esta operación.
            await operacion();
            return;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await operacion();
        await tx.CommitAsync(ct);
    }
}
