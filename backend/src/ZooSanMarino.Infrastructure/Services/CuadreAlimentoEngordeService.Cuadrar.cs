// src/ZooSanMarino.Infrastructure/Services/CuadreAlimentoEngordeService.Cuadrar.cs
// «Cuadrar galpón»: cerrar el descuadre de alimento desde la propia pestaña que lo señala.
//
// Hasta el 25-ago-2026 la pestaña de Cuadre era de solo lectura: mostraba el número y no ofrecía
// nada para arreglarlo. Peor: uno de los dos lados NO TENIA ARREGLO POSIBLE desde ninguna pantalla,
// porque los ajustes de stock se espejan como INV_OTRO y la tabla diaria no los ve.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class CuadreAlimentoEngordeService
{
    /// <summary>Tipo de movimiento que corrige la TABLA DIARIA hacia arriba, sin tocar stock.</summary>
    private const string MovimientoAjusteTablaEntrada = "AjusteCuadreTablaEntrada";

    /// <summary>Tipo de movimiento que corrige la TABLA DIARIA hacia abajo, sin tocar stock.</summary>
    private const string MovimientoAjusteTablaSalida = "AjusteCuadreTablaSalida";

    /// <summary>Etiqueta del movimiento en los listados de inventario.</summary>
    private const string EstadoAjusteCuadre = "Ajuste de cuadre";

    /// <inheritdoc />
    public async Task<CuadrarGalponAlimentoResultDto> CuadrarGalponAsync(
        CuadrarGalponAlimentoRequest req, CancellationToken ct = default)
    {
        var companyId = await ResolverCompanyIdAsync();
        if (companyId <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        // ── La fila del cuadre es la fuente de los tres números. Se lee de la MISMA función que
        //    pinta la pantalla: si el operador ve 12.720 y el backend calculara otra cosa, el ajuste
        //    cerraría un descuadre distinto del que se quiso cerrar.
        var fila = (await ObtenerAsync(soloConProblemas: false, ct))
            .Galpones.FirstOrDefault(g => g.LoteAveEngordeId == req.LoteAveEngordeId);

        if (fila is null)
            throw new InvalidOperationException(
                "El galpón no está en el cuadre de la empresa activa (o su ciclo ya no es el vigente). " +
                "Actualice la pantalla e intente de nuevo.");

        var plan = AjusteCuadreAlimentoCalculos.Planificar(
            saldoTablaKg: fila.SaldoTablaKg,
            movPostKg: fila.MovPostKg,
            stockKg: fila.StockKg,
            kilosRealesKg: req.KilosRealesKg,
            // Sin esto el ajuste dejaría el galpón descuadrado POR EL MONTO RESERVADO, después de
            // una pantalla que dijo «cuadrado». Ver AjusteCuadreAlimentoCalculos.Planificar.
            reservadoActivoKg: fila.ReservadoActivoKg);

        var rechazo = AjusteCuadreAlimentoCalculos.Rechazo(plan, req.Motivo);
        if (rechazo is not null)
            throw new InvalidOperationException(rechazo);

        var motivo = req.Motivo.Trim();
        var resumen = AjusteCuadreAlimentoCalculos.Describir(plan);

        // 🔴 Los DOS lados van en UNA transacción. Si el ajuste de stock quedara aplicado y el de la
        // tabla fallara, el galpón terminaría MÁS descuadrado que antes — y encima después de una
        // pantalla que dijo «cuadrado». Es la misma razón por la que `AnularMovimientoHistoricoAsync`
        // no separa la reversión del stock de la anulación del histórico.
        // Ambas escrituras usan el MISMO `ZooSanMarinoContext` del scope (el propio y el de
        // `_inventario`), así que la transacción abierta acá las cubre a las dos.
        await using (var tx = await _db.Database.BeginTransactionAsync(ct))
        {
            // ── Lado STOCK. Se delega en InventarioGestionService.ActualizarStockAsync, que es el
            //    dueño de la mutación de stock: valida empresa/país/granja asignada, escribe el
            //    `AjusteStock` con su motivo y respeta la unidad del catálogo. Duplicar esa lógica
            //    acá sería una segunda fórmula para el mismo número, que es como este módulo se
            //    rompió antes.
            if (plan.TocaStock)
            {
                var stock = await BuscarStockDelGalponAsync(fila, req.ItemInventarioEcuadorId, ct);
                if (stock is null)
                    throw new InvalidOperationException(
                        "Ese ítem no tiene registro de stock en el galpón, así que no hay nada que ajustar. " +
                        "Créelo desde la pestaña Stock y vuelva a cuadrar.");

                // 🔴 Se aplica el DELTA al ítem elegido, NO los kilos totales.
                // `fila.StockKg` es la suma de TODOS los ítems del galpón (así lo agrupa
                // `fn_cuadre_alimento_engorde`), así que escribir ahí el total del galpón en un solo
                // ítem inflaría el stock por lo que valen los demás. Con un solo ítem con saldo —el
                // caso normal— las dos formas coinciden, que es justo lo que haría pasar el defecto
                // por alto hasta el primer galpón con dos alimentos.
                var nuevaCantidadItem = stock.Quantity + plan.DeltaStockKg;

                if (nuevaCantidadItem < 0)
                    throw new InvalidOperationException(
                        $"La corrección descuenta {Math.Abs(plan.DeltaStockKg):N1} kg pero el ítem elegido " +
                        $"solo tiene {stock.Quantity:N1} kg en el galpón. Elija el ítem que realmente sobra, " +
                        "o corrija primero los otros ítems desde la pestaña Stock.");

                await _inventario.ActualizarStockAsync(
                    stock.Id,
                    new InventarioGestionStockUpdateRequest(
                        Quantity: nuevaCantidadItem,
                        Unit: null,                  // la manda el catálogo del ítem (TK-2026-000019)
                        Reason: $"Cuadre de galpón. {motivo}"),
                    ct);
            }

            // ── Lado TABLA DIARIA. Un movimiento que el stock NO ve, a propósito: acá el inventario
            //    ya tenía razón. El trigger AFTER INSERT lo espeja en el histórico con su
            //    `tipo_evento` propio, y `fn_seguimiento_diario_engorde` (v17) lo lee en sus 5 CTE.
            if (plan.TocaTabla)
                await EscribirAjusteDeTablaAsync(
                    fila, req.ItemInventarioEcuadorId, plan.DeltaTablaKg, motivo, resumen, ct);

            await tx.CommitAsync(ct);
        }

        return new CuadrarGalponAlimentoResultDto(
            Granja: fila.Granja,
            NucleoId: fila.NucleoId,
            GalponId: fila.GalponId,
            LoteNombre: fila.LoteNombre,
            SaldoTablaAntesKg: plan.SaldoTablaKg,
            StockAntesKg: plan.StockKg,
            MovPostKg: plan.MovPostKg,
            KilosRealesKg: plan.KilosRealesKg,
            DeltaStockKg: plan.TocaStock ? plan.DeltaStockKg : 0m,
            DeltaTablaKg: plan.TocaTabla ? plan.DeltaTablaKg : 0m,
            DescuadreAntesKg: plan.DescuadreAntesKg,
            DescuadreDespuesKg: plan.DescuadreDespuesKg,
            Resumen: resumen);
    }

    /// <summary>
    /// Fila de stock del ítem en la ubicación exacta del galpón que reporta el cuadre.
    /// <para>
    /// Se compara con <c>TRIM</c> del mismo modo que <c>fn_cuadre_alimento_engorde</c>: la fn agrupa
    /// el stock por <c>COALESCE(TRIM(...), '')</c>, y buscar acá sin recortar dejaría fuera las filas
    /// con espacios que la fn sí sumó.
    /// </para>
    /// </summary>
    private Task<InventarioGestionStock?> BuscarStockDelGalponAsync(
        CuadreAlimentoEngordeFilaDto fila, int itemId, CancellationToken ct) =>
        _db.InventarioGestionStock
            .Where(s => s.FarmId == fila.GranjaId
                     && s.ItemInventarioEcuadorId == itemId
                     && (s.NucleoId == null ? "" : s.NucleoId.Trim()) == fila.NucleoId
                     && (s.GalponId == null ? "" : s.GalponId.Trim()) == fila.GalponId)
            .OrderByDescending(s => s.Quantity)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Escribe el movimiento que mueve SOLO la tabla diaria.
    ///
    /// <para>
    /// <b>La fecha es la del último seguimiento, no hoy.</b> Un movimiento fechado después de
    /// <c>seg_max</c> es, por definición del cuadre, un «movimiento posterior»: se restaría del
    /// esperado y el galpón quedaría igual de descuadrado. Fechándolo en el último día del ciclo, la
    /// corrección entra en la tabla y el invariante cierra.
    /// </para>
    ///
    /// <para>
    /// <b>No pasa por la ventana de fechas retroactivas</b> (<c>VentanaFechaRegistroGuard</c>) y es
    /// correcto: esa guarda existe para las fechas que <b>tipea una persona</b>, y esta la deriva el
    /// sistema de un dato que ya está en la BD. El operador no elige la fecha.
    /// </para>
    ///
    /// <para>
    /// La cantidad se guarda en valor ABSOLUTO y el signo lo lleva el tipo, igual que
    /// <c>TrasladoEntrada</c>/<c>TrasladoSalida</c>. Es deliberado: <c>AjusteStock</c> guarda
    /// <c>Math.Abs</c> y pierde el signo, y por eso no se puede revertir automáticamente. Acá el
    /// signo vive en el tipo, que nadie puede perder.
    /// </para>
    /// </summary>
    private async Task EscribirAjusteDeTablaAsync(
        CuadreAlimentoEngordeFilaDto fila, int itemId, decimal deltaKg,
        string motivo, string resumen, CancellationToken ct)
    {
        var item = await _db.ItemInventario.AsNoTracking()
            .Where(i => i.Id == itemId)
            .Select(i => new { i.Id, i.Unidad })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("El ítem de inventario indicado no existe.");

        // El país se toma de una fila de stock del MISMO galpón: es el valor con el que ya están
        // escritos todos los movimientos de esa ubicación, así que el ajuste queda consistente con
        // sus vecinos sin volver a derivarlo por granja → departamento → país.
        var paisId = await _db.InventarioGestionStock.AsNoTracking()
            .Where(s => s.FarmId == fila.GranjaId
                     && (s.NucleoId == null ? "" : s.NucleoId.Trim()) == fila.NucleoId
                     && (s.GalponId == null ? "" : s.GalponId.Trim()) == fila.GalponId)
            .Select(s => (int?)s.PaisId)
            .FirstOrDefaultAsync(ct)
            ?? await _db.InventarioGestionStock.AsNoTracking()
                .Where(s => s.FarmId == fila.GranjaId)
                .Select(s => (int?)s.PaisId)
                .FirstOrDefaultAsync(ct);

        var fecha = new DateTimeOffset(
            fila.UltimoSeguimiento.Year, fila.UltimoSeguimiento.Month, fila.UltimoSeguimiento.Day,
            12, 0, 0, TimeSpan.Zero);

        _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
        {
            CompanyId = fila.CompanyId,
            PaisId = paisId ?? 0,
            FarmId = fila.GranjaId,
            NucleoId = fila.NucleoId,
            GalponId = fila.GalponId,
            ItemInventarioEcuadorId = item.Id,
            Quantity = Math.Abs(deltaKg),
            Unit = UnidadInventarioCalculos.Resolver(item.Unidad, "kg"),
            MovementType = deltaKg > 0 ? MovimientoAjusteTablaEntrada : MovimientoAjusteTablaSalida,
            Estado = EstadoAjusteCuadre,
            // La referencia es lo que la tabla diaria muestra en la columna Documento del día, así
            // que dice lo mismo que vio el operador antes de confirmar.
            Reference = "Ajuste de cuadre",
            Reason = $"{resumen} Motivo: {motivo}",
            CreatedAt = fecha,
            CreatedByUserId = _current?.UserId.ToString()
        });

        await _db.SaveChangesAsync(ct);
    }
}
