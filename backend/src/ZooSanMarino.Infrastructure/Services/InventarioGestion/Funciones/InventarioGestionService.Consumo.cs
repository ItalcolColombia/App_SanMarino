using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Shared;
using ZooSanMarino.Application.DTOs.Galpones;
using ZooSanMarino.Application.Exceptions;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

// Registro de consumo, a nivel galpon (Ecuador/Panama) y a nivel granja (Colombia).
//
// Fase 3 (paso 2) - consumo/devolucion a NIVEL GRANJA (Colombia): el stock Colombia migrado a
// modelo B vive a nivel granja (nucleo_id/galpon_id = NULL), a diferencia de Ecuador/Panama
// (alimento exige nucleo+galpon). RegistrarConsumoNivelGranjaAsync es ADITIVO: NO cambia el
// comportamiento de RegistrarConsumoAsync/RegistrarIngresoAsync (que EXIGEN nucleo+galpon para
// alimento). Descuenta/repone SIEMPRE contra el stock (farm, item, nucleo=NULL, galpon=NULL), sin
// exigir galpon, y NO abre transaccion propia: participa de la IDbContextTransaction externa que
// abre el servicio de seguimiento (levante/produccion), igual que FarmInventoryConsumoService.
// Mantiene la validacion de stock (si insuficiente -> throw) para respetar el bloqueo atomico.
// Movimientos con MovementType 'Consumo'/'Ingreso' (como hoy Ecuador) y NucleoId/GalponId = NULL,
// aislados por company+pais de la granja.
public partial class InventarioGestionService
{
    public async Task<InventarioGestionStockDto> RegistrarConsumoAsync(InventarioGestionConsumoRequest req, CancellationToken ct = default)
    {
        if (req.Quantity <= 0) throw new InvalidOperationException("La cantidad de consumo debe ser positiva.");

        // La resolución de la ubicación vive en un solo lugar porque `ValidarStockConsumoAsync` la
        // necesita IDÉNTICA: si la validación previa buscara el stock en otra clave que el descuento,
        // aprobaría un consumo que después falla —o al revés— y volveríamos a tener dos verdades
        // sobre el mismo número.
        var (item, nucleoId, galponId, siloId) = await ResolverUbicacionConsumoAsync(req, ct);
        req = AjustarUbicacionRequest(req, item);

        // A2 — descuento ATÓMICO. Antes esto era read-modify-write:
        //     if (stock.Quantity < req.Quantity) throw;  stock.Quantity -= req.Quantity;
        // Dos consumos de 100 sobre un stock de 150 pasaban LOS DOS la validación y el saldo
        // terminaba en -50: se despachaba alimento que no existía. Ahora la condición viaja
        // DENTRO del UPDATE, así que el segundo consumo ve el saldo ya descontado.
        // La lectura es AsNoTracking() a propósito: una copia rastreada con la cantidad vieja
        // haría que el SaveChanges de abajo pisara el descuento.
        var stock = await BuscarStockSinRastreoAsync(req.FarmId, req.ItemInventarioEcuadorId, nucleoId, galponId, siloId, ct);
        if (stock == null)
            throw new StockInsuficienteException(StockAtomicoCalculos.MensajeStockInsuficiente);

        var (companyId, paisId) = await GetFarmCompanyAndPaisAsync(req.FarmId, ct);
        if (_current?.CompanyId > 0 && _current.CompanyId != companyId)
            throw new InvalidOperationException("La granja no pertenece a su empresa.");

        // El descuento y el movimiento que lo explica van juntos o no van: si el movimiento
        // fallara después del UPDATE, el stock bajaría sin ningún registro que lo justifique.
        await EnTransaccionAsync(async () =>
        {
            if (!await DescontarStockAtomicoAsync(stock.Id, req.Quantity, ct))
                throw new StockInsuficienteException(StockAtomicoCalculos.MensajeStockInsuficiente);

            _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
            {
                CompanyId = companyId,
                PaisId = paisId,
                FarmId = req.FarmId,
                NucleoId = nucleoId,
                GalponId = galponId,
                SiloId = siloId,
                ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
                Quantity = req.Quantity,
                // TK-2026-000019 — la del catálogo. Con `req.Unit ?? "kg"`, todo consumo disparado
                // por un seguimiento (que no manda unidad) quedaba en kilos.
                Unit = UnidadInventarioCalculos.Resolver(item.Unidad, req.Unit),
                MovementType = "Consumo",
                Estado = "Consumo",
                Reference = req.Reference?.Trim(),
                Reason = req.Reason?.Trim(),
                // Simetría con RegistrarIngresoAsync: sin fecha explícita se usa "ahora" (comportamiento
                // histórico); con fecha, el movimiento queda en el día real del consumo. Ancla a las
                // 18:00 (no a las 12:00 del ingreso) para no empatar el orden intra-día — ver F2.
                CreatedAt = ResolveMovimientoCreatedAt(req.FechaMovimiento, FechaMovimientoSeguimientoCalculos.AnclaConsumoUtc),
                CreatedByUserId = _current?.UserId.ToString()
            });
            await _db.SaveChangesAsync(ct);
        }, ct);

        var list = (await GetStockAsync(req.FarmId, nucleoId, galponId, null, null, ct))
            .Where(x => x.SiloId == siloId).ToList();
        return list.FirstOrDefault(x => x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId)
            // `stock` se leyó con AsNoTracking ANTES del descuento, así que su Quantity es la
            // anterior: el DTO de respaldo tiene que restar la cantidad consumida a mano. Antes
            // esto salía solo porque la entidad rastreada ya venía decrementada en memoria.
            ?? new InventarioGestionStockDto(stock.Id, stock.FarmId, stock.NucleoId, stock.GalponId, stock.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.TipoItem ?? "alimento", stock.Quantity - req.Quantity, stock.Unit, null, null, null, stock.CreatedAt, null, stock.SiloId);
    }

    /// <summary>
    /// La fila de stock de nivel granja (nucleo/galpon NULL) de un ítem, discriminando por silo.
    ///
    /// <para>
    /// El silo se filtra SIEMPRE, también cuando es <c>null</c> (<c>silo_id IS NULL</c>): es la misma
    /// clave natural que fija el índice único <c>ux_inventario_gestion_stock_clave_natural</c> con su
    /// <c>COALESCE(silo_id,0)</c>. Para las empresas sin el flag —donde <c>silo_id</c> es NULL en el
    /// 100 % de las filas— la consulta devuelve exactamente lo mismo que antes de la Fase C; para las
    /// que ubican por silo, sin este filtro el consumo descontaría la primera fila que encuentre, que
    /// puede ser la de OTRO silo.
    /// </para>
    /// </summary>
    private IQueryable<InventarioGestionStock> StockNivelGranjaQuery(int farmId, int itemId, int? siloId)
    {
        var q = _db.InventarioGestionStock
            .Where(x => x.FarmId == farmId && x.ItemInventarioEcuadorId == itemId && x.NucleoId == null && x.GalponId == null);

        return siloId.HasValue
            ? q.Where(x => x.SiloId == siloId.Value)
            : q.Where(x => x.SiloId == null);
    }

    /// <summary>
    /// Fase 3 — consumo a nivel granja (Colombia): descuenta <c>inventario_gestion_stock</c> por
    /// (farm, item, nucleo=NULL, galpon=NULL) e inserta un movimiento <c>Consumo</c> sin ubicación
    /// estructurada. Lanza si no hay stock suficiente (bloqueo). No mueve nada de Ecuador/Panamá.
    ///
    /// <para>
    /// F4 (22-ago-2026): antes esto era <c>read-modify-write</c> sobre una fila RASTREADA
    /// (<c>stock.Quantity -= req.Quantity</c>, sin <c>SaveChanges</c> propio — el orquestador externo
    /// commiteaba todo junto). Sin concurrency token en la tabla, dos consumos concurrentes sobre la
    /// misma granja+ítem pasaban <b>los dos</b> la validación y el <c>UPDATE</c> final de EF escribía
    /// el absoluto en memoria: pérdida DETERMINISTA, no una carrera rara. Y el stock a nivel granja es
    /// UNO por (granja, ítem) compartido por TODOS los lotes de la granja — N tablets de la misma
    /// granja recuperando señal a la vez es el peor caso posible.
    /// </para>
    ///
    /// <para>
    /// Ahora adopta la forma que Ecuador/Panamá ya tiene al lado: lectura SIN rastreo
    /// (<see cref="BuscarStockSinRastreoAsync"/>) + descuento en una sola sentencia condicional
    /// (<see cref="DescontarStockAtomicoAsync"/>, <c>UPDATE ... WHERE quantity &gt;= @q</c>) + el
    /// movimiento, TODO dentro de <see cref="EnTransaccionAsync"/> (abre transacción sólo si no hay
    /// una ambiente — el mismo patrón que ya usa <c>RegistrarConsumoAsync</c>). Por eso este método SÍ
    /// llama su propio <c>SaveChangesAsync</c> ahora: los llamadores (levante/engorde/producción)
    /// persisten el seguimiento ANTES de invocar este camino, así que no hay nada ajeno que arrastrar.
    /// </para>
    /// </summary>
    public async Task RegistrarConsumoNivelGranjaAsync(InventarioGestionConsumoRequest req, CancellationToken ct = default)
    {
        if (req.Quantity <= 0) throw new InvalidOperationException("La cantidad de consumo debe ser positiva.");
        var item = await _db.ItemInventario.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.ItemInventarioEcuadorId, ct);
        if (item == null) throw new InvalidOperationException("El ítem de inventario no existe.");

        // Lectura SIN rastreo: es parte del contrato de DescontarStockAtomicoAsync. Una copia
        // rastreada con la cantidad vieja haría que un SaveChanges posterior pisara el descuento.
        var stock = await BuscarStockSinRastreoAsync(req.FarmId, req.ItemInventarioEcuadorId, null, null, req.SiloId, ct);
        if (stock == null || stock.Quantity < req.Quantity)
            throw new StockInsuficienteException(StockAtomicoCalculos.MensajeStockInsuficienteNivelGranja(
                item.Codigo, item.Nombre, req.FarmId, stock?.Quantity ?? 0m, req.Quantity));

        var (companyId, paisId) = await GetFarmCompanyAndPaisAsync(req.FarmId, ct);

        // El descuento y el movimiento que lo explica van juntos o no van: si el movimiento fallara
        // después del UPDATE, el stock bajaría sin ningún registro que lo justifique.
        await EnTransaccionAsync(async () =>
        {
            if (!await DescontarStockAtomicoAsync(stock.Id, req.Quantity, ct))
                // Rama de la carrera: la pre-lectura alcanzaba, pero otra transacción se llevó el
                // saldo antes que ésta. Mismo mensaje con nombre e ítem, no el genérico de EC/PA —
                // así el reporte de la carga masiva sigue diciendo qué faltó y dónde.
                throw new StockInsuficienteException(StockAtomicoCalculos.MensajeStockInsuficienteNivelGranja(
                    item.Codigo, item.Nombre, req.FarmId, stock.Quantity, req.Quantity));

            _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
            {
                CompanyId = companyId,
                PaisId = paisId,
                FarmId = req.FarmId,
                NucleoId = null,
                GalponId = null,
                ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
                // El silo del consumo (Fase C). Null en toda empresa sin el flag ⇒ movimiento idéntico
                // al de siempre; con silo, el kardex dice de qué silo salió el alimento.
                SiloId = req.SiloId,
                Quantity = req.Quantity,
                // TK-2026-000019 — la del catálogo (Colombia manda "kg" fijo en el request).
                Unit = UnidadInventarioCalculos.Resolver(item.Unidad, req.Unit),
                MovementType = "Consumo",
                Estado = "Consumo",
                Reference = req.Reference?.Trim(),
                Reason = req.Reason?.Trim(),
                // Simetría con RegistrarConsumoAsync: sin fecha explícita se usa "ahora" (lo que hacen
                // todos los llamadores históricos, así que su comportamiento no cambia); con fecha, el
                // movimiento queda en el día real del consumo — lo necesita la carga masiva, cuya
                // idempotencia se apoya en la fecha del movimiento. Ancla a las 18:00, no a las 12:00
                // del ingreso, para no empatar el orden intra-día — ver F2.
                CreatedAt = ResolveMovimientoCreatedAt(req.FechaMovimiento, FechaMovimientoSeguimientoCalculos.AnclaConsumoUtc),
                CreatedByUserId = _current?.UserId.ToString()
            });
            await _db.SaveChangesAsync(ct);
        }, ct);
    }
}
