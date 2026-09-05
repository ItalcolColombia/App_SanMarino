// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.AlimentoPostura.cs
// Inventario de alimento en la carga masiva de POSTURA (Levante + Producción).
//
// Por qué existe: la carga masiva escribía el consumo del día como un número suelto y NADIE lo
// descontaba, mientras el alta manual valida stock ANTES de persistir y descuenta en la misma
// transacción. Migrar un histórico dejaba la granja con kilos que en la realidad ya se consumieron,
// y no había forma de cargar las entradas de alimento por Excel.
//
// La lógica de inventario NO se reimplementa: el consumo del seguimiento delega en los MISMOS
// servicios del alta manual (IColombiaInventarioConsumoService a nivel granja para Colombia,
// IInventarioGestionService con núcleo/galpón para Ecuador/Panamá) y la hoja "Alimento" reusa el
// partial de engorde, que a su vez delega en la pantalla de gestión de inventario.
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Migracion;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MigracionService
{
    /// <summary>
    /// Todo lo que hace falta para mover inventario por un lote de postura: dónde vive su stock
    /// (según el flag <c>maneja_alimento_por_galpon</c> de empresa/granja) y a qué modelo de consumo
    /// descuenta (según el país del lote, igual que el alta manual).
    /// </summary>
    private sealed record ContextoInventarioPostura(
        int FarmId,
        string? NucleoId,
        string? GalponId,
        bool ManejaPorGalpon,
        UbicacionAlimento Posicion,
        ModeloInventarioConsumo Modelo,
        ModoUbicacionInventario Modo = ModoUbicacionInventario.Clasico)
    {
        /// <summary>¿El alimento de este lote se ubica en silos/bodegas en vez de granja/galpón?</summary>
        public bool PorSilo => Modo == ModoUbicacionInventario.PorSilo;

        /// <summary>¿El consumo de este lote descuenta de algún inventario automáticamente?</summary>
        public bool DescuentaConsumo => Modelo is ModeloInventarioConsumo.ModeloB or ModeloInventarioConsumo.ModeloBNivelGranja;
    }

    /// <summary>
    /// Resuelve el contexto de inventario del lote de postura. Devuelve <c>null</c> si el lote no
    /// existe (el llamador ya validó elegibilidad, así que en la práctica no ocurre).
    /// </summary>
    private async Task<ContextoInventarioPostura?> ResolverContextoInventarioPosturaAsync(int loteId, CancellationToken ct)
    {
        var lote = await _ctx.Lotes.AsNoTracking()
            .Where(l => l.LoteId == loteId)
            .Select(l => new { l.GranjaId, l.NucleoId, l.GalponId, l.PaisId })
            .FirstOrDefaultAsync(ct);
        if (lote is null) return null;

        var manejaPorGalpon = await ManejaAlimentoPorGalponAsync(lote.GranjaId, ct);

        // El modo sale del flag de la EMPRESA DUEÑA DE LA GRANJA, igual que en el alta manual
        // (ColombiaInventarioConsumoService.ResolverEmpresaYModoAsync). Nunca de un país ni de un nombre.
        var manejaPorSilo = await _ctx.Farms.AsNoTracking()
            .Where(f => f.Id == lote.GranjaId)
            .Join(_ctx.Set<Company>().AsNoTracking(), f => f.CompanyId, c => c.Id, (_, c) => c.ManejaInventarioPorSilo)
            .FirstOrDefaultAsync(ct);
        var modo = InventarioUbicacionSiloCalculos.ResolverModo(manejaPorSilo);

        // País efectivo: el del lote o, si no está poblado, el de la granja por su departamento —
        // misma cadena que usa SeguimientoLoteLevanteService.ResolverPaisIdLoteAsync.
        var paisId = lote.PaisId is > 0
            ? lote.PaisId
            : await _ctx.Farms.AsNoTracking()
                .Where(f => f.Id == lote.GranjaId)
                .Join(_ctx.Departamentos.AsNoTracking(),
                    f => f.DepartamentoId, d => d.DepartamentoId, (f, d) => (int?)d.PaisId)
                .FirstOrDefaultAsync(ct);

        var nucleo = string.IsNullOrWhiteSpace(lote.NucleoId) ? null : lote.NucleoId.Trim();
        var galpon = string.IsNullOrWhiteSpace(lote.GalponId) ? null : lote.GalponId.Trim();

        return new ContextoInventarioPostura(
            lote.GranjaId, nucleo, galpon, manejaPorGalpon,
            MigracionPosturaCalculos.PosicionStockDeLote(manejaPorGalpon, lote.GranjaId, nucleo, galpon),
            InventarioConsumoGate.ResolverModelo(paisId),
            modo);
    }

    /// <summary>
    /// Silos de la granja del lote para resolver las columnas de silo de la hoja "Datos": el índice
    /// por nombre/código (todos los activos de la granja) y la LISTA BLANCA del lote
    /// (<c>lote_silos</c>), que es la misma regla que aplica el formulario diario
    /// (<c>ConsumoSiloCalculos.ValidarClaves</c>).
    ///
    /// <para>
    /// Las dos listas son distintas a propósito: con la primera se traduce el nombre a id y se puede
    /// decir «ese silo no existe en esta granja»; con la segunda se distingue ese caso del silo que
    /// SÍ existe pero que el lote no consume, que lleva otro mensaje y otra acción del usuario.
    /// </para>
    /// </summary>
    private async Task<(Dictionary<string, List<(int Id, string Nombre)>> PorClave, HashSet<int> Asignados)>
        CargarSilosDelLoteAsync(ContextoInventarioPostura ctxInv, int loteId, CancellationToken ct)
    {
        if (!ctxInv.PorSilo)
            return (new Dictionary<string, List<(int, string)>>(), new HashSet<int>());

        var silos = await _ctx.FarmSilos.AsNoTracking()
            .Where(fs => fs.GranjaId == ctxInv.FarmId && fs.DeletedAt == null && fs.Activo)
            .Select(fs => new { fs.Id, fs.Nombre, fs.CodigoErpUbicacion })
            .ToListAsync(ct);

        var porClave = new Dictionary<string, List<(int, string)>>();
        foreach (var silo in silos)
        {
            Indexar(silo.Nombre, (silo.Id, silo.Nombre));
            Indexar(silo.CodigoErpUbicacion, (silo.Id, silo.Nombre));
        }

        var asignados = (await _ctx.LoteSilos.AsNoTracking()
            .Where(ls => ls.LoteId == loteId && ls.Activo)
            .Select(ls => ls.FarmSiloId)
            .ToListAsync(ct)).ToHashSet();

        return (porClave, asignados);

        void Indexar(string? texto, (int, string) valor)
        {
            var clave = MigracionCalculos.NormalizarClave(texto);
            if (string.IsNullOrEmpty(clave)) return;
            if (!porClave.TryGetValue(clave, out var lista)) porClave[clave] = lista = new List<(int, string)>();
            if (!lista.Contains(valor)) lista.Add(valor);
        }
    }

    /// <summary>
    /// Silos/bodegas que el lote tiene ASIGNADOS, para la hoja Referencias y los desplegables de la
    /// plantilla. Es exactamente la lista que el importador acepta, así que lo que se elige del
    /// desplegable siempre valida (el problema que tenía la hoja "Huevos" antes de acotarla al lote).
    /// </summary>
    private async Task<List<(int Id, string Nombre, string? Codigo)>> CargarSilosElegiblesDelLoteAsync(
        ContextoInventarioPostura ctxInv, int loteId, CancellationToken ct)
    {
        if (!ctxInv.PorSilo) return new List<(int, string, string?)>();

        return await _ctx.LoteSilos.AsNoTracking()
            .Where(ls => ls.LoteId == loteId && ls.Activo)
            .Join(_ctx.FarmSilos.AsNoTracking()
                    .Where(fs => fs.GranjaId == ctxInv.FarmId && fs.DeletedAt == null && fs.Activo),
                ls => ls.FarmSiloId, fs => fs.Id, (_, fs) => new { fs.Id, fs.Nombre, fs.CodigoErpUbicacion })
            .OrderBy(x => x.Nombre)
            .Select(x => new ValueTuple<int, string, string?>(x.Id, x.Nombre, x.CodigoErpUbicacion))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Lee la hoja "Alimento" de un archivo de postura. Es la MISMA hoja y el mismo parseo que en
    /// engorde; lo único propio es la ubicación por defecto y el nivel del stock.
    /// </summary>
    private Task<List<MovimientoAlimentoFila>> LeerHojaAlimentoPosturaAsync(
        IFormFile file, int companyId, ContextoInventarioPostura ctxInv,
        List<MigracionErrorDto> errores, CancellationToken ct)
        => LeerHojaAlimentoAsync(file, companyId, ctxInv.Posicion, ctxInv.ManejaPorGalpon, errores, ct, ctxInv.Modo);

    /// <summary>
    /// Consumo por ítem que aporta una fila del seguimiento, acumulado por posición de stock. Es lo
    /// que entra a la simulación de balance como SALIDA.
    /// </summary>
    private static void AcumularConsumoItems(
        IDictionary<PosicionAlimento, decimal> salidas, ContextoInventarioPostura ctxInv,
        IEnumerable<ItemSeguimientoDto> items)
    {
        foreach (var item in items)
            if (item.ItemInventarioEcuadorId is int itemId && item.Cantidad > 0)
            {
                // Con silo, la posición la manda el SILO DEL ÍTEM, no la ubicación del lote: dos
                // alimentos del mismo día pueden salir de silos distintos y el stock de cada uno es
                // una fila separada. Sin silo, la posición es la de siempre (delta cero).
                var posicion = ctxInv.PorSilo
                    ? (ctxInv.Posicion with { NucleoId = null, GalponId = null, SiloId = item.SiloId }).Normalizada()
                    : ctxInv.Posicion;
                MigracionAlimentoCalculos.Acumular(
                    salidas, new PosicionAlimento(posicion, itemId), (decimal)item.Cantidad);
            }
    }

    /// <summary>
    /// Descuenta del inventario el consumo de los días REALMENTE insertados/mergeados, delegando en
    /// el mismo camino que el alta manual. Los fallos se REPORTAN (no se tragan en un catch que solo
    /// loguea, que es lo que dejaba el galpón descuadrado sin ninguna señal).
    /// </summary>
    /// <param name="consumos">Consumo por fecha: los ítems de las filas que sí se persistieron.</param>
    /// <param name="referencia">Constructor de la referencia del movimiento (levante / producción).</param>
    private async Task DescontarConsumoSeguimientoAsync(
        ContextoInventarioPostura ctxInv,
        IReadOnlyList<(DateTime Fecha, long SeguimientoId, IReadOnlyList<ItemSeguimientoDto> Items)> consumos,
        Func<long, DateTime, string> referencia,
        List<MigracionErrorDto> fallos,
        CancellationToken ct)
    {
        if (consumos.Count == 0 || !ctxInv.DescuentaConsumo) return;

        foreach (var (fecha, seguimientoId, items) in consumos.OrderBy(c => c.Fecha))
        {
            // 🔴 La clave lleva el SILO. Es la misma `ItemConsumoKey` con la que el formulario diario
            // descuenta, y el silo forma parte de la identidad: dos filas del mismo alimento en silos
            // distintos son dos descuentos separados, no uno sumado (el índice único del stock es
            // `COALESCE(silo_id,0)`). Agrupar por itemId a secas los fusionaba y descontaba todo del
            // primero.
            var porItem = new Dictionary<ItemConsumoKey, decimal>();
            foreach (var item in items)
                if (item.ItemInventarioEcuadorId is int itemId && item.Cantidad > 0)
                {
                    var clave = new ItemConsumoKey(itemId, true, ctxInv.PorSilo ? item.SiloId : null);
                    porItem[clave] = porItem.GetValueOrDefault(clave) + (decimal)item.Cantidad;
                }
            if (porItem.Count == 0) continue;

            var refStr = referencia(seguimientoId, fecha);
            try
            {
                if (ctxInv.Modelo == ModeloInventarioConsumo.ModeloBNivelGranja)
                {
                    if (_colombiaConsumo is null) continue;
                    // Camino 2 (pass-through): los ítems salen del inventario unificado, así que la
                    // clave viaja con EsItemInventario=true y el servicio la valida contra la empresa
                    // dueña de la granja antes de descontar.
                    await _colombiaConsumo.AplicarConsumoAsync(ctxInv.FarmId, porItem, refStr, ct, fechaMovimiento: fecha);
                    await _ctx.SaveChangesAsync(ct); // el servicio no commitea: participa de la tx externa
                }
                else if (_inventarioGestion is not null)
                {
                    foreach (var kv in porItem)
                        await _inventarioGestion.RegistrarConsumoAsync(new InventarioGestionConsumoRequest(
                            ctxInv.FarmId, ctxInv.NucleoId, ctxInv.GalponId, kv.Key.Id, kv.Value, "kg", refStr, null,
                            FechaMovimiento: fecha, SiloId: kv.Key.SiloId), ct);
                }
            }
            catch (Exception ex)
            {
                fallos.Add(new(0, "Alimento", fecha.ToString("yyyy-MM-dd"),
                    $"El día {fecha:yyyy-MM-dd} se guardó pero su consumo NO se pudo descontar del inventario: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// Fechas del lote que YA tienen un registro de seguimiento y por lo tanto la función SQL va a
    /// OMITIR. Se consultan ANTES de invocarla para dos cosas: reportar <c>FilasOmitidas</c> de verdad
    /// (postura devolvía 0 siempre) y, sobre todo, NO volver a descontar su consumo — reimportar el
    /// mismo archivo descontaría el inventario dos veces.
    /// <para>
    /// Las filas "solo traslado" NO cuentan como existentes: la función las MERGEA con los datos del
    /// Excel, así que ese día sí se procesa y su consumo sí debe descontarse.
    /// </para>
    /// </summary>
    private async Task<HashSet<DateTime>> FechasYaCargadasAsync(
        TipoMigracion tipo, int loteId, IReadOnlyCollection<DateTime> fechas, CancellationToken ct)
    {
        var vacio = new HashSet<DateTime>();
        if (fechas.Count == 0) return vacio;

        var desde = fechas.Min().Date;
        var hasta = fechas.Max().Date;

        // Rango en DateTime crudo (no `.Fecha.Date >= x`): EF traduce `.Date` a `date_trunc`, que
        // trunca en la zona de la SESIÓN de la BD y pierde filas del borde. Se amplía un día a cada
        // lado y el recorte fino se hace en memoria, que es exacto.
        var rangoDesde = desde.AddDays(-1);
        var rangoHasta = hasta.AddDays(2);

        if (tipo == TipoMigracion.SeguimientoLevante)
        {
            var loteTxt = loteId.ToString();
            var filas = await _ctx.SeguimientoDiario.AsNoTracking()
                .Where(s => s.TipoSeguimiento == "levante"
                            && s.LoteId == loteTxt
                            && (s.ReproductoraId == null || s.ReproductoraId == "")
                            && s.Fecha >= rangoDesde && s.Fecha < rangoHasta)
                .Select(s => new { s.Fecha, s.EsTraslado, s.MortalidadHembras, s.MortalidadMachos,
                                   s.SelH, s.SelM, s.ConsumoKgHembras, s.ConsumoKgMachos })
                .ToListAsync(ct);

            foreach (var f in filas)
            {
                var esSoloTraslado = f.EsTraslado
                    && (f.MortalidadHembras ?? 0) == 0 && (f.MortalidadMachos ?? 0) == 0
                    && (f.SelH ?? 0) == 0 && (f.SelM ?? 0) == 0
                    && (f.ConsumoKgHembras ?? 0) == 0 && (f.ConsumoKgMachos ?? 0) == 0;
                if (!esSoloTraslado) vacio.Add(f.Fecha.Date);
            }
            return vacio;
        }

        var filasProd = await _ctx.SeguimientoProduccion.AsNoTracking()
            .Where(s => s.LoteId == loteId && s.Fecha >= rangoDesde && s.Fecha < rangoHasta)
            .Select(s => new { s.Fecha, s.EsTraslado, s.MortalidadH, s.MortalidadM, s.SelH, s.SelM,
                               s.ConsKgH, s.ConsKgM, s.HuevoTot, s.Metadata })
            .ToListAsync(ct);

        foreach (var f in filasProd)
        {
            var esSoloTraslado = f.EsTraslado
                && f.MortalidadH == 0 && f.MortalidadM == 0 && f.SelH == 0 && f.SelM == 0
                && f.ConsKgH == 0 && f.ConsKgM == 0 && f.HuevoTot == 0;
            // La fila del ARRASTRE de huevos del levante tampoco cuenta como "ya cargada": el Excel de
            // ese día se FUSIONA con ella (los huevos se suman), igual que en el alta manual. Contarla
            // hacía que el primer día de producción — que es justo el día del cierre — se omitiera en
            // silencio y se perdieran mortalidad, consumo y clasificación.
            if (!esSoloTraslado && !HuevosLevanteCalculos.PermiteMergeSeguimiento(f.Metadata))
                vacio.Add(f.Fecha.Date);
        }
        return vacio;
    }

    /// <summary>
    /// Filas de producción del lote que están a la espera del merge con el seguimiento del día: las
    /// creó el cierre del levante con los huevos arrastrados y todavía nadie registró ese día.
    /// Devuelve, por fecha, la clasificación ya arrastrada y su metadata (que trae la marca).
    /// </summary>
    private async Task<Dictionary<DateTime, (HuevosClasificacion Huevos, JsonDocument? Metadata)>> ArrastresPendientesAsync(
        int loteId, IReadOnlyCollection<DateTime> fechas, CancellationToken ct)
    {
        var mapa = new Dictionary<DateTime, (HuevosClasificacion, JsonDocument?)>();
        if (fechas.Count == 0) return mapa;

        var rangoDesde = fechas.Min().Date.AddDays(-1);
        var rangoHasta = fechas.Max().Date.AddDays(2);

        var filas = await _ctx.SeguimientoProduccion.AsNoTracking()
            .Where(s => s.LoteId == loteId && s.Fecha >= rangoDesde && s.Fecha < rangoHasta && s.Metadata != null)
            .Select(s => new
            {
                s.Fecha, s.Metadata,
                s.HuevoLimpio, s.HuevoTratado, s.HuevoSucio, s.HuevoDeforme, s.HuevoBlanco,
                s.HuevoDobleYema, s.HuevoPiso, s.HuevoPequeno, s.HuevoRoto, s.HuevoDesecho, s.HuevoOtro
            })
            .ToListAsync(ct);

        foreach (var f in filas)
        {
            if (!HuevosLevanteCalculos.PermiteMergeSeguimiento(f.Metadata)) continue;
            mapa[f.Fecha.Date] = (new HuevosClasificacion(
                Limpio: f.HuevoLimpio, Tratado: f.HuevoTratado, Sucio: f.HuevoSucio,
                Deforme: f.HuevoDeforme, Blanco: f.HuevoBlanco, DobleYema: f.HuevoDobleYema,
                Piso: f.HuevoPiso, Pequeno: f.HuevoPequeno, Roto: f.HuevoRoto,
                Desecho: f.HuevoDesecho, Otro: f.HuevoOtro), f.Metadata);
        }
        return mapa;
    }

    /// <summary>
    /// Ids de los registros de seguimiento del lote en las fechas indicadas, para armar la referencia
    /// del movimiento de inventario con el MISMO formato del alta manual
    /// (<c>Seguimiento lote levante #id fecha</c>). Se consulta DESPUÉS de la función SQL, que
    /// devuelve un conteo y no los ids.
    /// </summary>
    private async Task<Dictionary<DateTime, long>> IdsSeguimientoPorFechaAsync(
        TipoMigracion tipo, int loteId, IReadOnlyCollection<DateTime> fechas, CancellationToken ct)
    {
        var mapa = new Dictionary<DateTime, long>();
        if (fechas.Count == 0) return mapa;

        var rangoDesde = fechas.Min().Date.AddDays(-1);
        var rangoHasta = fechas.Max().Date.AddDays(2);
        var buscadas = fechas.Select(f => f.Date).ToHashSet();

        if (tipo == TipoMigracion.SeguimientoLevante)
        {
            var loteTxt = loteId.ToString();
            foreach (var f in await _ctx.SeguimientoDiario.AsNoTracking()
                         .Where(s => s.TipoSeguimiento == "levante" && s.LoteId == loteTxt
                                     && (s.ReproductoraId == null || s.ReproductoraId == "")
                                     && s.Fecha >= rangoDesde && s.Fecha < rangoHasta)
                         .Select(s => new { s.Id, s.Fecha })
                         .ToListAsync(ct))
                if (buscadas.Contains(f.Fecha.Date)) mapa[f.Fecha.Date] = f.Id;
            return mapa;
        }

        foreach (var f in await _ctx.SeguimientoProduccion.AsNoTracking()
                     .Where(s => s.LoteId == loteId && s.Fecha >= rangoDesde && s.Fecha < rangoHasta)
                     .Select(s => new { s.Id, s.Fecha })
                     .ToListAsync(ct))
            if (buscadas.Contains(f.Fecha.Date)) mapa[f.Fecha.Date] = f.Id;
        return mapa;
    }
}
