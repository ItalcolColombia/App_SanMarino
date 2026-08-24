// src/ZooSanMarino.Infrastructure/Services/InventarioGestion/Funciones/InventarioGestionService.Traslado.cs
// Traslados de inventario: misma granja, inter-granja con transito (registro, recepcion, rechazo),
// listado, edicion de fecha, eliminacion, y el mapeo de etiquetas/estados que usa el listado.
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

public partial class InventarioGestionService
{
    public async Task<(InventarioGestionStockDto Origen, InventarioGestionStockDto Destino)> RegistrarTrasladoAsync(InventarioGestionTrasladoRequest req, CancellationToken ct = default)
    {
        if (req.Quantity <= 0) throw new InvalidOperationException("La cantidad debe ser positiva.");
        var item = await _db.ItemInventario.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.ItemInventarioEcuadorId, ct);
        if (item == null) throw new InvalidOperationException("El ítem de inventario no existe.");

        // Colombia (nivel granja): alimento sin núcleo/galpón → mismo camino que un ítem no-alimento
        // (traslado a nivel granja). EC/PA conservan el galpón-a-galpón para alimento.
        var (_, paisIdOrigen) = await GetFarmCompanyAndPaisAsync(req.FromFarmId, ct);
        var nivelGranja = await EsInventarioNivelGranjaAsync(req.FromFarmId, ct);
        var isAlimento = IsAlimento(item);
        var usaUbicacion = isAlimento && !nivelGranja;

        var mismaGranja = req.FromFarmId == req.ToFarmId;

        // Empresas que ubican por silo: el traslado es entre SILOS (o bodega -> silo) de la misma
        // granja. El nucleo/galpon no participa —un mismo silo puede alimentar a varios galpones—,
        // asi que ni se pide ni se persiste.
        var modoOrigen = await ResolverModoUbicacionAsync(req.FromFarmId, ct);
        if (modoOrigen == ModoUbicacionInventario.PorSilo)
        {
            var errorOrigenSilo = InventarioUbicacionSiloCalculos.ValidarUbicacion(
                modoOrigen, req.FromSiloId, req.FromGalponId, isAlimento);
            if (errorOrigenSilo is not null)
                throw new InvalidOperationException($"Origen: {errorOrigenSilo}");

            await ValidarSiloDeGranjaAsync(req.FromFarmId, req.FromSiloId!.Value, ct);

            if (mismaGranja)
            {
                var errorDestinoSilo = InventarioUbicacionSiloCalculos.ValidarUbicacion(
                    modoOrigen, req.ToSiloId, req.ToGalponId, isAlimento);
                if (errorDestinoSilo is not null)
                    throw new InvalidOperationException($"Destino: {errorDestinoSilo}");
                if (req.FromSiloId!.Value == req.ToSiloId!.Value)
                    throw new InvalidOperationException("El silo de destino debe ser distinto al de origen.");

                await ValidarSiloDeGranjaAsync(req.ToFarmId, req.ToSiloId!.Value, ct);
                return await RegistrarTrasladoMismaGranjaAsync(
                    req, item, null, null, null, null, req.FromSiloId, req.ToSiloId, ct);
            }

            // Inter-granja: el silo destino es solo una sugerencia hasta que el destino reciba el
            // transito, igual que el galpon destino en el camino clasico.
            return await RegistrarTrasladoInterGranjaTransitoAsync(req, item, isAlimento: false, ct);
        }

        if (mismaGranja)
        {
            if (!usaUbicacion)
                throw new InvalidOperationException(nivelGranja
                    ? "En Colombia el inventario es solo a nivel granja: no aplica traslado dentro de la misma granja. Use traslado entre granjas distintas."
                    : "Para ítems que no son alimento no aplica traslado entre galpones en la misma granja (el stock es solo a nivel granja). Use traslado entre granjas distintas si aplica.");
            if (string.IsNullOrWhiteSpace(req.FromNucleoId) || string.IsNullOrWhiteSpace(req.FromGalponId) ||
                string.IsNullOrWhiteSpace(req.ToNucleoId) || string.IsNullOrWhiteSpace(req.ToGalponId))
                throw new InvalidOperationException("Para alimento en la misma granja debe indicar Núcleo y Galpón de origen y destino.");
            var fn = req.FromNucleoId.Trim();
            var fg = req.FromGalponId.Trim();
            var tn = req.ToNucleoId.Trim();
            var tg = req.ToGalponId.Trim();
            if (string.Equals(fg, tg, StringComparison.Ordinal) && string.Equals(fn, tn, StringComparison.Ordinal))
                throw new InvalidOperationException("El galpón de destino debe ser distinto al de origen.");
            return await RegistrarTrasladoMismaGranjaAsync(req, item, fn, fg, tn, tg, null, null, ct);
        }

        return await RegistrarTrasladoInterGranjaTransitoAsync(req, item, usaUbicacion, ct);
    }

    /// <summary>Traslado entre galpones de la misma granja: descuenta origen y suma destino en una sola operación (2 movimientos).</summary>
    private async Task<(InventarioGestionStockDto Origen, InventarioGestionStockDto Destino)> RegistrarTrasladoMismaGranjaAsync(
        InventarioGestionTrasladoRequest req,
        ItemInventario item,
        string? fromNucleoId,
        string? fromGalponId,
        string? toNucleoId,
        string? toGalponId,
        int? fromSiloId,
        int? toSiloId,
        CancellationToken ct)
    {
        // A2 — lectura sin rastreo: la fila se modifica por SQL atómico más abajo, y una copia
        // rastreada con la cantidad vieja haría que el SaveChanges posterior pisara el descuento.
        var stockOrigen = await BuscarStockSinRastreoAsync(req.FromFarmId, req.ItemInventarioEcuadorId, fromNucleoId, fromGalponId, fromSiloId, ct);
        if (stockOrigen == null)
            throw new InvalidOperationException("No hay stock suficiente en el origen para el traslado.");

        var (companyIdTo, paisIdTo) = await GetFarmCompanyAndPaisAsync(req.ToFarmId, ct);
        var transferGroupId = Guid.NewGuid();
        // TK-2026-000019 — el traslado no cambia de unidad por el camino: la del catálogo.
        var unidadTraslado = UnidadInventarioCalculos.Resolver(item.Unidad, req.Unit);
        DateTimeOffset movAt = default;

        // Las dos patas del traslado y sus dos movimientos son UNA unidad: descontar el origen sin
        // acreditar el destino (o sin dejar el movimiento que lo explica) crea un descuadre entre
        // granjas que después no se puede reconstruir.
        InventarioGestionStock stockDestino = null!;

        await EnTransaccionAsync(async () =>
        {
            if (!await DescontarStockAtomicoAsync(stockOrigen.Id, req.Quantity, ct))
                throw new InvalidOperationException("No hay stock suficiente en el origen para el traslado.");

            stockDestino = await SumarStockAtomicoAsync(
                companyIdTo, paisIdTo, req.ToFarmId, toNucleoId, toGalponId,
                req.ItemInventarioEcuadorId, req.Quantity, unidadTraslado, toSiloId, ct);

            movAt = await RegistrarMovimientosTrasladoMismaGranjaAsync(
                req, fromNucleoId, fromGalponId, toNucleoId, toGalponId, fromSiloId, toSiloId,
                stockOrigen, unidadTraslado, companyIdTo, paisIdTo, transferGroupId, ct);
        }, ct);

        // Traslado dentro de la misma granja: se movió alimento en DOS galpones.
        await RefrescarSaldoAlimentoEngordeAsync(stockOrigen.CompanyId, req.FromFarmId, fromNucleoId, fromGalponId, "TrasladoSalida", ct);
        await RefrescarSaldoAlimentoEngordeAsync(companyIdTo, req.ToFarmId, toNucleoId, toGalponId, "TrasladoEntrada", ct);

        var listOrigen = (await GetStockAsync(req.FromFarmId, fromNucleoId, fromGalponId, null, null, ct))
            // Con silo, la granja tiene varias filas del mismo item (una por silo): hay que quedarse
            // con la del silo del movimiento o el DTO mostraria el saldo de otro silo.
            .Where(x => x.SiloId == fromSiloId).ToList();
        var listDestino = (await GetStockAsync(req.ToFarmId, toNucleoId, toGalponId, null, null, ct))
            .Where(x => x.SiloId == toSiloId).ToList();
        // `stockOrigen` se leyó ANTES del descuento (AsNoTracking), así que el DTO de respaldo
        // resta a mano; `stockDestino` viene del RETURNING del upsert, o sea ya acumulado.
        var dtoOrigen = listOrigen.FirstOrDefault(x => x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId) ?? new InventarioGestionStockDto(stockOrigen.Id, stockOrigen.FarmId, stockOrigen.NucleoId, stockOrigen.GalponId, stockOrigen.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.TipoItem ?? "alimento", stockOrigen.Quantity - req.Quantity, stockOrigen.Unit, null, null, null, stockOrigen.CreatedAt);
        var dtoDestino = listDestino.FirstOrDefault(x => x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId) ?? new InventarioGestionStockDto(stockDestino.Id, stockDestino.FarmId, stockDestino.NucleoId, stockDestino.GalponId, stockDestino.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.TipoItem ?? "alimento", stockDestino.Quantity, stockDestino.Unit, null, null, null, stockDestino.CreatedAt);

        // Un traslado toca DOS galpones: cada uno tiene su propio ciclo vigente.
        var avisoOrigen  = await EvaluarAvisoFechaFueraDeCicloAsync(stockOrigen.CompanyId, req.FromFarmId, fromNucleoId, fromGalponId, movAt, ct);
        var avisoDestino = await EvaluarAvisoFechaFueraDeCicloAsync(companyIdTo, req.ToFarmId, toNucleoId, toGalponId, movAt, ct);
        if (avisoOrigen  is not null) dtoOrigen  = dtoOrigen  with { AvisoFechaFueraDeCiclo = avisoOrigen };
        if (avisoDestino is not null) dtoDestino = dtoDestino with { AvisoFechaFueraDeCiclo = avisoDestino };

        return (dtoOrigen, dtoDestino);
    }

    /// <summary>
    /// Graba los dos movimientos (salida y entrada) de un traslado dentro de la misma granja y
    /// devuelve la fecha con la que quedaron.
    ///
    /// <para>
    /// Extraído de <c>RegistrarTrasladoMismaGranjaAsync</c> al hacer el traslado atómico: este cuerpo
    /// tiene que ejecutarse DENTRO de la transacción, y así se lee sin anidar cincuenta líneas en una
    /// lambda. No cambia ni un valor respecto de la versión anterior.
    /// </para>
    /// </summary>
    private async Task<DateTimeOffset> RegistrarMovimientosTrasladoMismaGranjaAsync(
        InventarioGestionTrasladoRequest req,
        string? fromNucleoId,
        string? fromGalponId,
        string? toNucleoId,
        string? toGalponId,
        int? fromSiloId,
        int? toSiloId,
        InventarioGestionStock stockOrigen,
        string unidad,
        int companyIdTo,
        int paisIdTo,
        Guid transferGroupId,
        CancellationToken ct)
    {
        var estadoTraslado = string.Equals(req.DestinoTipo?.Trim(), "planta", StringComparison.OrdinalIgnoreCase)
            ? "Transferencia a planta"
            : "Transferencia a granja";
        var movAt = ResolveMovimientoCreatedAt(req.FechaMovimiento);
        _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
        {
            CompanyId = stockOrigen.CompanyId,
            PaisId = stockOrigen.PaisId,
            FarmId = req.FromFarmId,
            NucleoId = fromNucleoId,
            GalponId = fromGalponId,
            SiloId = fromSiloId,
            ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
            Quantity = req.Quantity,
            // Las dos patas del traslado llevan la unidad del CATÁLOGO (TK-2026-000019). Antes cada
            // una copiaba la de su fila de stock, así que una fila torcida seguía escribiendo
            // movimientos torcidos.
            Unit = unidad,
            MovementType = "TrasladoSalida",
            Estado = estadoTraslado,
            FromFarmId = req.ToFarmId,
            // Convencion existente: en la fila de SALIDA los campos From* guardan el OTRO extremo
            // (el destino). El silo sigue el mismo criterio para no inventar una segunda regla.
            FromNucleoId = toNucleoId,
            FromGalponId = toGalponId,
            FromSiloId = toSiloId,
            Reference = req.Reference?.Trim(),
            Reason = req.Reason?.Trim(),
            TransferGroupId = transferGroupId,
            CreatedAt = movAt,
            CreatedByUserId = _current?.UserId.ToString()
        });
        _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
        {
            CompanyId = companyIdTo,
            PaisId = paisIdTo,
            FarmId = req.ToFarmId,
            NucleoId = toNucleoId,
            GalponId = toGalponId,
            SiloId = toSiloId,
            ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
            Quantity = req.Quantity,
            Unit = unidad,
            MovementType = "TrasladoEntrada",
            Estado = estadoTraslado,
            FromFarmId = req.FromFarmId,
            FromNucleoId = fromNucleoId,
            FromGalponId = fromGalponId,
            FromSiloId = fromSiloId,
            Reference = req.Reference?.Trim(),
            Reason = req.Reason?.Trim(),
            TransferGroupId = transferGroupId,
            CreatedAt = movAt,
            CreatedByUserId = _current?.UserId.ToString()
        });

        await _db.SaveChangesAsync(ct);
        return movAt;
    }

    /// <summary>
    /// Traslado entre granjas distintas: descuenta origen de inmediato y registra salida en tránsito.
    /// La recepción en destino solo suma stock (no vuelve a descontar origen).
    /// Registros antiguos con movement_type TrasladoInterGranjaPendiente siguen descontando origen al recibir.
    /// </summary>
    private async Task<(InventarioGestionStockDto Origen, InventarioGestionStockDto Destino)> RegistrarTrasladoInterGranjaTransitoAsync(
        InventarioGestionTrasladoRequest req,
        ItemInventario item,
        bool isAlimento,
        CancellationToken ct)
    {
        string? fromNucleoId = null;
        string? fromGalponId = null;
        string? toNucleoHint = null;
        string? toGalponHint = null;

        if (isAlimento)
        {
            if (string.IsNullOrWhiteSpace(req.FromNucleoId) || string.IsNullOrWhiteSpace(req.FromGalponId))
                throw new InvalidOperationException("Para alimento debe indicar Núcleo y Galpón de origen.");
            fromNucleoId = req.FromNucleoId!.Trim();
            fromGalponId = req.FromGalponId!.Trim();
            toNucleoHint = string.IsNullOrWhiteSpace(req.ToNucleoId) ? null : req.ToNucleoId.Trim();
            toGalponHint = string.IsNullOrWhiteSpace(req.ToGalponId) ? null : req.ToGalponId.Trim();
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(req.FromNucleoId) || !string.IsNullOrWhiteSpace(req.FromGalponId) ||
                !string.IsNullOrWhiteSpace(req.ToNucleoId) || !string.IsNullOrWhiteSpace(req.ToGalponId))
                throw new InvalidOperationException("Para ítems que no son alimento el traslado entre granjas es solo a nivel granja (sin Núcleo/Galpón).");
        }

        // El silo del origen ya lo valido RegistrarTrasladoAsync; el del destino es una SUGERENCIA
        // hasta que la granja destino reciba el transito (igual que el galpon destino).
        var fromSiloId = req.FromSiloId;
        var toSiloHint = req.ToSiloId;

        // A2 — descuento atómico. Lectura sin rastreo (ver BuscarStockSinRastreoAsync).
        var stockOrigen = await BuscarStockSinRastreoAsync(req.FromFarmId, req.ItemInventarioEcuadorId, fromNucleoId, fromGalponId, fromSiloId, ct);
        if (stockOrigen == null)
            throw new InvalidOperationException("No hay stock suficiente en el origen para registrar el traslado a otra granja.");

        var transferGroupId = Guid.NewGuid();
        var movAt = ResolveMovimientoCreatedAt(req.FechaMovimiento);
        // TK-2026-000019 — la unidad del catálogo, no la de la fila de origen (que puede arrastrar
        // el 'kg' con el que nació) ni la del request.
        var unidadTransito = UnidadInventarioCalculos.Resolver(item.Unidad, req.Unit);

        // El descuento del origen y el movimiento de tránsito que lo explica van juntos: si el
        // movimiento fallara, el alimento saldría de la granja origen sin quedar en tránsito en
        // ningún lado, y el destino nunca podría recibirlo.
        await EnTransaccionAsync(async () =>
        {
            if (!await DescontarStockAtomicoAsync(stockOrigen.Id, req.Quantity, ct))
                throw new InvalidOperationException("No hay stock suficiente en el origen para registrar el traslado a otra granja.");

            _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
            {
                CompanyId = stockOrigen.CompanyId,
                PaisId = stockOrigen.PaisId,
                FarmId = req.FromFarmId,
                NucleoId = fromNucleoId,
                GalponId = fromGalponId,
                SiloId = fromSiloId,
                ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
                Quantity = req.Quantity,
                Unit = unidadTransito,
                MovementType = "TrasladoInterGranjaSalida",
                Estado = "Tránsito",
                FromFarmId = req.ToFarmId,
                FromNucleoId = toNucleoHint,
                FromGalponId = toGalponHint,
                FromSiloId = toSiloHint,
                Reference = req.Reference?.Trim(),
                Reason = req.Reason?.Trim(),
                TransferGroupId = transferGroupId,
                CreatedAt = movAt,
                CreatedByUserId = _current?.UserId.ToString()
            });

            await _db.SaveChangesAsync(ct);
        }, ct);
        // Solo el galpón ORIGEN pierde alimento acá; el destino recién suma al recibir el tránsito.
        await RefrescarSaldoAlimentoEngordeAsync(stockOrigen.CompanyId, req.FromFarmId, fromNucleoId, fromGalponId, "TrasladoInterGranjaSalida", ct);

        var listOrigen = (await GetStockAsync(req.FromFarmId, fromNucleoId, fromGalponId, null, null, ct))
            .Where(x => x.SiloId == fromSiloId).ToList();
        var dtoOrigen = listOrigen.FirstOrDefault(x => x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId)
            // `stockOrigen` se leyó ANTES del descuento (AsNoTracking): el respaldo resta a mano.
            ?? new InventarioGestionStockDto(stockOrigen.Id, stockOrigen.FarmId, stockOrigen.NucleoId, stockOrigen.GalponId, stockOrigen.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.TipoItem ?? "alimento", stockOrigen.Quantity - req.Quantity, stockOrigen.Unit, null, null, null, stockOrigen.CreatedAt);
        var itemTypeOut = item.Concepto ?? item.TipoItem ?? "alimento";
        var dtoDestinoPendiente = new InventarioGestionStockDto(
            0,
            req.ToFarmId,
            toNucleoHint,
            toGalponHint,
            req.ItemInventarioEcuadorId,
            item.Codigo,
            item.Nombre,
            itemTypeOut,
            0,
            unidadTransito,
            null,
            null,
            null,
            null);
        return (dtoOrigen, dtoDestinoPendiente);
    }

    public async Task<List<InventarioGestionTransitoPendienteDto>> GetTransitosPendientesAsync(int? farmIdDestino = null, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            return new List<InventarioGestionTransitoPendienteDto>();

        var candidatos = await _db.InventarioGestionMovimientos
            .AsNoTracking()
            .Include(x => x.ItemInventario)
            .Include(x => x.Farm)
            .Where(x => x.CompanyId == companyId.Value && x.TransferGroupId != null &&
                (x.MovementType == "TrasladoInterGranjaPendiente" || x.MovementType == "TrasladoInterGranjaSalida"))
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync(ct);

        var gruposConEntrada = (await _db.InventarioGestionMovimientos
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId.Value && x.MovementType == "TrasladoInterGranjaEntrada" && x.TransferGroupId != null)
            .Select(x => x.TransferGroupId!.Value)
            .ToListAsync(ct)).ToHashSet();

        var filtradas = candidatos.Where(s => s.TransferGroupId.HasValue && !gruposConEntrada.Contains(s.TransferGroupId.Value));
        if (farmIdDestino.HasValue)
            filtradas = filtradas.Where(s => s.FromFarmId == farmIdDestino.Value);

        var farmIds = filtradas.SelectMany(s => new[] { s.FarmId, s.FromFarmId ?? 0 }).Where(id => id > 0).Distinct().ToList();
        var farmNames = await _db.Farms.AsNoTracking().Where(f => farmIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Name, ct);

        return filtradas.Select(s =>
        {
            farmNames.TryGetValue(s.FarmId, out var fromName);
            var toId = s.FromFarmId ?? 0;
            farmNames.TryGetValue(toId, out var toName);
            var pendienteDespachoOrigen = string.Equals(s.MovementType, "TrasladoInterGranjaPendiente", StringComparison.Ordinal);
            return new InventarioGestionTransitoPendienteDto(
                s.TransferGroupId!.Value,
                s.Id,
                s.FarmId,
                fromName,
                toId,
                toName,
                s.NucleoId,
                s.GalponId,
                s.FromNucleoId,
                s.FromGalponId,
                s.ItemInventarioEcuadorId,
                s.ItemInventario.Codigo,
                s.ItemInventario.Nombre,
                s.Quantity,
                s.Unit,
                s.CreatedAt,
                pendienteDespachoOrigen);
        }).ToList();
    }

    public async Task<InventarioGestionRecepcionTransitoResultDto> RegistrarRecepcionTransitoAsync(InventarioGestionRecepcionTransitoRequest req, CancellationToken ct = default)
    {
        var salida = await _db.InventarioGestionMovimientos
            .Include(x => x.ItemInventario)
            .Include(x => x.Farm)
            .FirstOrDefaultAsync(x => x.TransferGroupId == req.TransferGroupId &&
                (x.MovementType == "TrasladoInterGranjaPendiente" || x.MovementType == "TrasladoInterGranjaSalida"), ct);
        if (salida == null)
            throw new InvalidOperationException("No se encontró el movimiento de traslado inter-granja para el grupo indicado.");

        var yaEntrada = await _db.InventarioGestionMovimientos.AnyAsync(
            x => x.TransferGroupId == req.TransferGroupId && x.MovementType == "TrasladoInterGranjaEntrada", ct);
        if (yaEntrada)
            throw new InvalidOperationException("Este traslado ya fue recibido en destino.");

        if (salida.FromFarmId != req.ToFarmId)
            throw new InvalidOperationException("La granja de recepción debe ser la granja destino del traslado.");

        var item = salida.ItemInventario;
        var (companyIdTo, paisIdTo) = await GetFarmCompanyAndPaisAsync(req.ToFarmId, ct);

        // Colombia (nivel granja): recepción de alimento sin núcleo/galpón. EC/PA sin cambios.
        // Con Distribucion (alimento por galpón) lo recibido se reparte entre varios galpones de la granja destino.
        var isAlimento = IsAlimento(item);
        var modoDestino = await ResolverModoUbicacionAsync(req.ToFarmId, ct);
        var porSilo = modoDestino == ModoUbicacionInventario.PorSilo;
        var usaUbicacion = !porSilo && isAlimento && !await EsInventarioNivelGranjaAsync(req.ToFarmId, ct);
        var (destinos, errorDistribucion) = ZooSanMarino.Application.Calculos.InventarioGestionRecepcionDistribucionCalculos.Resolver(
            req.Distribucion, req.ToNucleoId, req.ToGalponId, usaUbicacion, salida.Quantity, porSilo, req.ToSiloId);
        if (errorDistribucion != null)
            throw new InvalidOperationException(errorDistribucion);

        // Solo el camino distribuido valida pertenencia (el de una ubicación conserva su comportamiento previo).
        if (destinos.Count > 1 && usaUbicacion)
            await ValidarGalponesDeGranjaAsync(req.ToFarmId, destinos, ct);

        // Cada silo del reparto tiene que ser de la granja destino. Se valida ANTES de escribir: un
        // silo ajeno acreditaría stock en la granja equivocada y el descuadre aparecería después,
        // sin rastro de su origen.
        if (porSilo)
            foreach (var d in destinos)
                await ValidarSiloDeGranjaAsync(req.ToFarmId, d.SiloId!.Value, ct);

        if (salida.CompanyId != companyIdTo)
            throw new InvalidOperationException("La granja destino no pertenece a la misma empresa que la salida.");

        // Un asiento (stock + movimiento) por ubicación de destino: uno solo en el camino clásico,
        // N cuando la recepción se distribuye entre galpones.
        var ahora = DateTimeOffset.UtcNow;
        var stocksDestino = new List<InventarioGestionStock>(destinos.Count);
        var movimientosEntrada = new List<InventarioGestionMovimiento>(destinos.Count);
        var distribuida = destinos.Count > 1;

        // A1/A2 — toda la recepción es UNA unidad: el descuento del origen, las N acreditaciones de
        // destino y sus N movimientos. Antes, cada pata se resolvía por separado con
        // buscar-o-insertar y read-modify-write; con la recepción distribuida eso significaba que
        // dos destinos que apuntaran al MISMO galpón creaban dos filas de stock (la segunda
        // invisible), porque ninguna de las dos consultas veía la fila que la otra estaba por
        // insertar. El upsert lo resuelve acumulando.
        await EnTransaccionAsync(async () =>
        {
        // Solicitud nueva: aquí se descuenta origen. Registro antiguo (Salida): el descuento ya se hizo al enviar.
        if (string.Equals(salida.MovementType, "TrasladoInterGranjaPendiente", StringComparison.Ordinal))
        {
            var stockOrigen = await BuscarStockSinRastreoAsync(salida.FarmId, salida.ItemInventarioEcuadorId, salida.NucleoId, salida.GalponId, salida.SiloId, ct);
            if (stockOrigen == null)
                throw new InvalidOperationException("No hay stock suficiente en origen para completar la recepción (verifique disponibilidad).");
            if (!await DescontarStockAtomicoAsync(stockOrigen.Id, salida.Quantity, ct))
                throw new InvalidOperationException("No hay stock suficiente en origen para completar la recepción (verifique disponibilidad).");
            salida.MovementType = "TrasladoInterGranjaSalida";
            salida.Estado = "Tránsito";
        }

        for (var i = 0; i < destinos.Count; i++)
        {
            var destino = destinos[i];
            var stockDestino = await SumarStockAtomicoAsync(
                companyIdTo, paisIdTo, req.ToFarmId, destino.NucleoId, destino.GalponId,
                salida.ItemInventarioEcuadorId, destino.Quantity,
                // TK-2026-000019 — la unidad del catálogo, no la que traía el movimiento de salida.
                UnidadInventarioCalculos.Resolver(item.Unidad, salida.Unit), destino.SiloId, ct);
            stocksDestino.Add(stockDestino);

            var movEntrada = new InventarioGestionMovimiento
            {
                CompanyId = companyIdTo,
                PaisId = paisIdTo,
                FarmId = req.ToFarmId,
                NucleoId = destino.NucleoId,
                GalponId = destino.GalponId,
                SiloId = destino.SiloId,
                ItemInventarioEcuadorId = salida.ItemInventarioEcuadorId,
                Quantity = destino.Quantity,
                Unit = stockDestino.Unit,
                MovementType = "TrasladoInterGranjaEntrada",
                Estado = "Recibido desde tránsito",
                FromFarmId = salida.FarmId,
                FromNucleoId = salida.NucleoId,
                FromGalponId = salida.GalponId,
                FromSiloId = salida.SiloId,
                Reference = salida.Reference,
                Reason = distribuida
                    ? $"Recepción traslado inter-granja (distribución {i + 1}/{destinos.Count})"
                    : "Recepción traslado inter-granja",
                TransferGroupId = req.TransferGroupId,
                CreatedAt = ahora,
                CreatedByUserId = _current?.UserId.ToString()
            };
            _db.InventarioGestionMovimientos.Add(movEntrada);
            movimientosEntrada.Add(movEntrada);
        }

        await _db.SaveChangesAsync(ct);
        }, ct);

        // La recepción puede repartirse entre VARIOS galpones del destino: refrescar todos.
        foreach (var ubic in movimientosEntrada
                     .Select(m => (m.NucleoId, m.GalponId, m.MovementType))
                     .Distinct())
            await RefrescarSaldoAlimentoEngordeAsync(companyIdTo, req.ToFarmId, ubic.NucleoId, ubic.GalponId, ubic.MovementType, ct);

        var farmDest = await _db.Farms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == req.ToFarmId, ct);

        string? origenNn = null;
        string? origenGn = null;
        if (salida.NucleoId != null)
            origenNn = await _db.Nucleos.AsNoTracking().Where(n => n.NucleoId == salida.NucleoId && n.GranjaId == salida.FarmId).Select(n => n.NucleoNombre).FirstOrDefaultAsync(ct);
        if (salida.GalponId != null)
            origenGn = await _db.Galpones.AsNoTracking().Where(g => g.GalponId == salida.GalponId && g.GranjaId == salida.FarmId).Select(g => g.GalponNombre).FirstOrDefaultAsync(ct);

        var dtosStock = new List<InventarioGestionStockDto>(destinos.Count);
        var dtosMov = new List<InventarioGestionMovimientoDto>(destinos.Count);

        for (var i = 0; i < destinos.Count; i++)
        {
            var destino = destinos[i];
            var stockDestino = stocksDestino[i];
            var movEntrada = movimientosEntrada[i];

            var list = await GetStockAsync(req.ToFarmId, destino.NucleoId, destino.GalponId, null, null, ct);
            dtosStock.Add(list.FirstOrDefault(x => x.ItemInventarioEcuadorId == salida.ItemInventarioEcuadorId)
                ?? new InventarioGestionStockDto(stockDestino.Id, stockDestino.FarmId, stockDestino.NucleoId, stockDestino.GalponId, stockDestino.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.Concepto ?? item.TipoItem ?? "alimento", stockDestino.Quantity, stockDestino.Unit, null, null, null, stockDestino.CreatedAt));

            string? nn = null;
            string? gn = null;
            if (destino.NucleoId != null)
                nn = await _db.Nucleos.AsNoTracking().Where(n => n.NucleoId == destino.NucleoId && n.GranjaId == req.ToFarmId).Select(n => n.NucleoNombre).FirstOrDefaultAsync(ct);
            if (destino.GalponId != null)
                gn = await _db.Galpones.AsNoTracking().Where(g => g.GalponId == destino.GalponId && g.GranjaId == req.ToFarmId).Select(g => g.GalponNombre).FirstOrDefaultAsync(ct);

            dtosMov.Add(new InventarioGestionMovimientoDto(
                movEntrada.Id,
                movEntrada.FarmId,
                movEntrada.NucleoId,
                movEntrada.GalponId,
                movEntrada.ItemInventarioEcuadorId,
                item.Codigo,
                item.Nombre,
                item.Concepto ?? item.TipoItem ?? "alimento",
                movEntrada.Quantity,
                movEntrada.Unit,
                movEntrada.MovementType,
                movEntrada.Estado,
                movEntrada.FromFarmId,
                movEntrada.FromNucleoId,
                movEntrada.FromGalponId,
                movEntrada.Reference,
                movEntrada.Reason,
                movEntrada.CreatedAt,
                farmDest?.Name,
                nn,
                gn,
                movEntrada.TransferGroupId,
                salida.Farm.Name,
                origenNn,
                origenGn,
                "Traslado entre granjas (recepción)",
                item.Concepto,
                item.TipoItem,
                movEntrada.ParaProximoCiclo,
                movEntrada.RegistradoAt));
        }

        return new InventarioGestionRecepcionTransitoResultDto(dtosStock, dtosMov);
    }

    /// <summary>
    /// Valida que cada (núcleo, galpón) de una recepción distribuida exista realmente en la granja destino.
    /// Solo se aplica al camino distribuido: el de una sola ubicación conserva su comportamiento histórico.
    /// </summary>
    private async Task ValidarGalponesDeGranjaAsync(
        int farmId,
        IReadOnlyList<ZooSanMarino.Application.Calculos.InventarioGestionRecepcionDistribucionCalculos.Destino> destinos,
        CancellationToken ct)
    {
        var galponesGranja = await _db.Galpones.AsNoTracking()
            .Where(g => g.GranjaId == farmId)
            .Select(g => new { g.GalponId, g.NucleoId })
            .ToListAsync(ct);

        foreach (var destino in destinos)
        {
            var existe = galponesGranja.Any(g =>
                string.Equals(g.GalponId, destino.GalponId, StringComparison.Ordinal) &&
                string.Equals(g.NucleoId, destino.NucleoId, StringComparison.Ordinal));
            if (!existe)
                throw new InvalidOperationException($"El galpón {destino.GalponId} no pertenece al núcleo {destino.NucleoId} de la granja destino.");
        }
    }

    public async Task RechazarTransitoPendienteAsync(InventarioGestionRechazoTransitoRequest req, CancellationToken ct = default)
    {
        var pendiente = await _db.InventarioGestionMovimientos
            .FirstOrDefaultAsync(x => x.TransferGroupId == req.TransferGroupId && x.MovementType == "TrasladoInterGranjaPendiente", ct);
        if (pendiente == null)
            throw new InvalidOperationException("No hay solicitud pendiente para rechazar (puede estar ya recibida o rechazada).");

        var yaEntrada = await _db.InventarioGestionMovimientos.AnyAsync(
            x => x.TransferGroupId == req.TransferGroupId && x.MovementType == "TrasladoInterGranjaEntrada", ct);
        if (yaEntrada)
            throw new InvalidOperationException("Este traslado ya fue recibido en destino.");

        pendiente.MovementType = "TrasladoInterGranjaRechazado";
        pendiente.Estado = "Rechazado destino";
        var extra = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim();
        pendiente.Reason = extra != null
            ? $"{pendiente.Reason ?? ""} | Rechazo destino: {extra}".Trim()
            : (pendiente.Reason ?? "Rechazado destino");

        // El rechazo cancela la salida, así que su fila del histórico tiene que quedar ANULADA.
        // Cambiarle el `movement_type` al movimiento NO alcanza: el trigger que llena el histórico es
        // solo AFTER INSERT, así que la fila conserva su `tipo_evento` original —
        // `TrasladoInterGranjaPendiente` mapea a INV_TRASLADO_SALIDA— y el saldo del galpón de origen
        // seguiría descontando un alimento que nunca salió.
        await AnularHistoricoDelMovimientoAsync(pendiente, ct);

        await _db.SaveChangesAsync(ct);
        await RefrescarSaldoAlimentoEngordeAsync(
            pendiente.CompanyId, pendiente.FarmId, pendiente.NucleoId, pendiente.GalponId, "TrasladoSalida", ct);
    }

    private static void ApplyUbicacionMovimientoFilter(
        ref IQueryable<InventarioGestionMovimiento> query,
        string? nucleoId,
        string? galponId)
    {
        if (string.IsNullOrWhiteSpace(nucleoId))
            query = query.Where(x => x.NucleoId == null || x.NucleoId == "");
        else
        {
            var n = nucleoId.Trim();
            query = query.Where(x => x.NucleoId == n);
        }

        if (string.IsNullOrWhiteSpace(galponId))
            query = query.Where(x => x.GalponId == null || x.GalponId == "");
        else
        {
            var g = galponId.Trim();
            query = query.Where(x => x.GalponId == g);
        }
    }

    private static string MapTipoOperacionLabel(string movementType) => movementType switch
    {
        "Ingreso" => "Ingreso",
        "Consumo" => "Consumo",
        "TrasladoSalida" => "Traslado (salida entre galpones)",
        "TrasladoEntrada" => "Traslado (entrada entre galpones)",
        "TrasladoInterGranjaPendiente" => "Traslado entre granjas (solicitud pendiente)",
        "TrasladoInterGranjaSalida" => "Traslado entre granjas (en tránsito)",
        "TrasladoInterGranjaEntrada" => "Traslado entre granjas (recepción)",
        "TrasladoInterGranjaRechazado" => "Traslado entre granjas (rechazado)",
        "AjusteStock" => "Ajuste manual de stock",
        "EliminacionStock" => "Eliminación de registro de stock",
        _ => movementType
    };

    /// <summary>Inverso de <see cref="MapTipoOperacionLabel"/> para filtro por etiqueta.</summary>
    private static string? ResolveMovementTypeFromTipoOperacionLabel(string label) => label switch
    {
        "Ingreso" => "Ingreso",
        "Consumo" => "Consumo",
        "Traslado (salida entre galpones)" => "TrasladoSalida",
        "Traslado (entrada entre galpones)" => "TrasladoEntrada",
        "Traslado entre granjas (solicitud pendiente)" => "TrasladoInterGranjaPendiente",
        "Traslado entre granjas (en tránsito)" => "TrasladoInterGranjaSalida",
        "Traslado entre granjas (recepción)" => "TrasladoInterGranjaEntrada",
        "Traslado entre granjas (rechazado)" => "TrasladoInterGranjaRechazado",
        "Ajuste manual de stock" => "AjusteStock",
        "Eliminación de registro de stock" => "EliminacionStock",
        _ => null
    };

    /// <summary>
    /// Tipos de movimiento que representan la "salida" de un traslado (son el registro primario del par/grupo).
    /// Para misma-granja: TrasladoSalida. Para inter-granja: TrasladoInterGranjaSalida | TrasladoInterGranjaPendiente | TrasladoInterGranjaRechazado.
    /// </summary>
    private static readonly HashSet<string> TrasladoSalidaTypes = new(StringComparer.Ordinal)
    {
        "TrasladoSalida",
        "TrasladoInterGranjaSalida",
        "TrasladoInterGranjaPendiente",
        "TrasladoInterGranjaRechazado"
    };

    private static readonly HashSet<string> TrasladoEntradaTypes = new(StringComparer.Ordinal)
    {
        "TrasladoEntrada",
        "TrasladoInterGranjaEntrada"
    };

    private static string MapEstadoTraslado(string movementType) => movementType switch
    {
        "TrasladoSalida" or "TrasladoEntrada" => "Completado",
        "TrasladoInterGranjaSalida" => "En tránsito",
        "TrasladoInterGranjaPendiente" => "Pendiente despacho",
        "TrasladoInterGranjaEntrada" => "Completado",
        "TrasladoInterGranjaRechazado" => "Rechazado",
        _ => movementType
    };

    public async Task<List<InventarioGestionTrasladoListDto>> GetTrasladosAsync(
        int? farmId = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        string? search = null,
        string? itemTipoItem = null,
        string? nucleoId = null,
        string? galponId = null,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            return new List<InventarioGestionTrasladoListDto>();

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);
        if (allowedFarmIds.Count == 0)
            return new List<InventarioGestionTrasladoListDto>();

        var salidaTypes = TrasladoSalidaTypes.ToList();

        // Movimientos "salida" (registro primario del traslado)
        var query = _db.InventarioGestionMovimientos
            .AsNoTracking()
            .Include(x => x.ItemInventario)
            .Include(x => x.Farm)
            .Where(x => x.CompanyId == companyId.Value
                        && salidaTypes.Contains(x.MovementType)
                        && (allowedFarmIds.Contains(x.FarmId) || (x.FromFarmId.HasValue && allowedFarmIds.Contains(x.FromFarmId.Value))));

        if (farmId.HasValue)
            query = query.Where(x => x.FarmId == farmId.Value || x.FromFarmId == farmId.Value);

        if (fechaDesde.HasValue)
        {
            var start = fechaDesde.Value.Date;
            query = query.Where(x => x.CreatedAt >= start);
        }

        if (fechaHasta.HasValue)
        {
            var end = fechaHasta.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < end);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventario.Codigo ?? "").ToLower().Contains(s) ||
                (x.ItemInventario.Nombre ?? "").ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(itemTipoItem))
        {
            var t = itemTipoItem.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventario.Concepto != null && x.ItemInventario.Concepto.Trim().ToLower() == t) ||
                (x.ItemInventario.TipoItem != null && x.ItemInventario.TipoItem.Trim().ToLower() == t));
        }

        if (!string.IsNullOrWhiteSpace(nucleoId))
            query = query.Where(x => x.NucleoId == nucleoId || x.FromNucleoId == nucleoId);

        if (!string.IsNullOrWhiteSpace(galponId))
            query = query.Where(x => x.GalponId == galponId || x.FromGalponId == galponId);

        var salidas = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(2000)
            .ToListAsync(ct);

        if (salidas.Count == 0)
            return new List<InventarioGestionTrasladoListDto>();

        // Cargar entradas correspondientes por TransferGroupId
        var groupIds = salidas
            .Where(x => x.TransferGroupId.HasValue)
            .Select(x => x.TransferGroupId!.Value)
            .Distinct()
            .ToList();

        var entradaTypes = TrasladoEntradaTypes.ToList();
        // Un grupo puede tener VARIAS entradas (recepción de tránsito distribuida entre galpones):
        // se agrupa y se toma la primera; la fila del traslado muestra el destino guardado en la salida.
        var entradas = groupIds.Count > 0
            ? (await _db.InventarioGestionMovimientos
                    .AsNoTracking()
                    .Where(x => x.TransferGroupId.HasValue && groupIds.Contains(x.TransferGroupId!.Value) && entradaTypes.Contains(x.MovementType))
                    .ToListAsync(ct))
                .GroupBy(x => x.TransferGroupId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Id).First())
            : new Dictionary<Guid, InventarioGestionMovimiento>();

        // Cargar nombres de granjas (origen + destino)
        var allFarmIds = salidas
            .SelectMany(x => new[] { x.FarmId, x.FromFarmId ?? 0 })
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        var farmNames = await _db.Farms.AsNoTracking()
            .Where(f => allFarmIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Name, ct);

        // Cargar nombres de núcleos y galpones
        var nucleoIds = salidas
            .SelectMany(x => new[] { x.NucleoId, x.FromNucleoId }.Where(n => !string.IsNullOrWhiteSpace(n)))
            .Distinct()
            .ToList();
        var nucleoRows = nucleoIds.Count > 0
            ? await _db.Nucleos.AsNoTracking()
                .Where(n => nucleoIds.Contains(n.NucleoId) && allFarmIds.Contains(n.GranjaId))
                .ToListAsync(ct)
            : new List<Nucleo>();
        var nucleoDict = nucleoRows.ToDictionary(n => (n.NucleoId, n.GranjaId), n => n.NucleoNombre);

        var galponIds = salidas
            .SelectMany(x => new[] { x.GalponId, x.FromGalponId }.Where(g => !string.IsNullOrWhiteSpace(g)))
            .Distinct()
            .ToList();
        var galponRows = galponIds.Count > 0
            ? await _db.Galpones.AsNoTracking()
                .Where(g => galponIds.Contains(g.GalponId) && allFarmIds.Contains(g.GranjaId))
                .ToListAsync(ct)
            : new List<Galpon>();
        var galponDict = galponRows.ToDictionary(g => (g.GalponId, g.GranjaId), g => g.GalponNombre);

        // La fila de salida guarda su propio silo (ORIGEN) y el del otro extremo en from_silo_id
        // (DESTINO), igual que hace con núcleo/galpón. Un solo viaje para los dos.
        var siloNombres = await NombresDeSilosAsync(
            salidas.SelectMany(s => new[] { s.SiloId, s.FromSiloId }), ct);

        return salidas.Select(s =>
        {
            farmNames.TryGetValue(s.FarmId, out var fromGranjaName);
            var toFarmId = s.FromFarmId ?? 0;
            farmNames.TryGetValue(toFarmId, out var toGranjaName);

            string? fromNucleoNombre = s.NucleoId != null && nucleoDict.TryGetValue((s.NucleoId, s.FarmId), out var fnn) ? fnn : null;
            string? fromGalponNombre = s.GalponId != null && galponDict.TryGetValue((s.GalponId, s.FarmId), out var fgn) ? fgn : null;
            string? toNucleoNombre = s.FromNucleoId != null && nucleoDict.TryGetValue((s.FromNucleoId, toFarmId), out var tnn) ? tnn : null;
            string? toGalponNombre = s.FromGalponId != null && galponDict.TryGetValue((s.FromGalponId, toFarmId), out var tgn) ? tgn : null;

            int? entradaId = s.TransferGroupId.HasValue && entradas.TryGetValue(s.TransferGroupId.Value, out var entrada) ? entrada.Id : null;
            var estado = MapEstadoTraslado(s.MovementType);

            return new InventarioGestionTrasladoListDto(
                s.TransferGroupId ?? Guid.Empty,
                s.Id,
                entradaId,
                s.FarmId,
                fromGranjaName,
                s.NucleoId,
                fromNucleoNombre,
                s.GalponId,
                fromGalponNombre,
                toFarmId,
                toGranjaName,
                s.FromNucleoId,
                toNucleoNombre,
                s.FromGalponId,
                toGalponNombre,
                s.ItemInventarioEcuadorId,
                s.ItemInventario.Codigo,
                s.ItemInventario.Nombre,
                s.ItemInventario.Concepto ?? s.ItemInventario.TipoItem ?? "alimento",
                s.ItemInventario.TipoItem ?? "alimento",
                s.Quantity,
                s.Unit,
                s.Reference,
                s.Reason,
                estado,
                s.CreatedAt,
                s.CreatedAt,
                s.SiloId,
                s.SiloId.HasValue && siloNombres.TryGetValue(s.SiloId.Value, out var fsn) ? fsn : null,
                s.FromSiloId,
                s.FromSiloId.HasValue && siloNombres.TryGetValue(s.FromSiloId.Value, out var tsn) ? tsn : null);
        }).ToList();
    }

    public async Task<InventarioGestionTrasladoListDto> ActualizarFechaTrasladoAsync(
        Guid transferGroupId,
        InventarioGestionActualizarFechaTrasladoRequest req,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);

        var movimientos = await _db.InventarioGestionMovimientos
            .Where(x => x.TransferGroupId == transferGroupId && x.CompanyId == companyId.Value)
            .ToListAsync(ct);

        if (movimientos.Count == 0)
            throw new InvalidOperationException("No se encontró el traslado indicado.");

        var salida = movimientos.FirstOrDefault(x => TrasladoSalidaTypes.Contains(x.MovementType));
        if (salida == null)
            throw new InvalidOperationException("El TransferGroupId no corresponde a un traslado.");

        if (!allowedFarmIds.Contains(salida.FarmId) && !(salida.FromFarmId.HasValue && allowedFarmIds.Contains(salida.FromFarmId.Value)))
            throw new InvalidOperationException("No tiene acceso a este traslado.");

        var nuevaFecha = ResolveMovimientoCreatedAt(req.FechaMovimiento);
        foreach (var mov in movimientos)
            mov.CreatedAt = nuevaFecha;

        await _db.SaveChangesAsync(ct);

        // Sincronizar fecha_operacion en tabla espejo lote_registro_historico_unificado
        var movIds = movimientos.Select(m => m.Id).ToList();
        var histTraslado = await _db.LoteRegistroHistoricoUnificados
            .Where(h => h.OrigenTabla == "inventario_gestion_movimiento" && movIds.Contains(h.OrigenId))
            .ToListAsync(ct);
        if (histTraslado.Count > 0)
        {
            var fechaDate = nuevaFecha.UtcDateTime.Date;
            foreach (var h in histTraslado)
                h.FechaOperacion = fechaDate;
            await _db.SaveChangesAsync(ct);
        }

        // Correr la fecha de un traslado mueve el alimento de día: refrescar los galpones tocados
        // (salida y entrada pueden ser distintos, y el grupo puede repartirse en varios).
        foreach (var ubic in movimientos
                     .Select(m => (m.CompanyId, m.FarmId, m.NucleoId, m.GalponId, m.MovementType))
                     .Distinct())
            await RefrescarSaldoAlimentoEngordeAsync(ubic.CompanyId, ubic.FarmId, ubic.NucleoId, ubic.GalponId, ubic.MovementType, ct);

        // Recargar y retornar el DTO actualizado
        var result = await GetTrasladosAsync(farmId: salida.FarmId, ct: ct);
        return result.FirstOrDefault(x => x.TransferGroupId == transferGroupId)
            ?? throw new InvalidOperationException("Error al recargar el traslado actualizado.");
    }

    /// <summary>
    /// Elimina todos los movimientos de un TransferGroupId.
    /// No modifica stock. Marca anulado=true en lote_registro_historico_unificado (auditoría)
    /// y elimina físicamente todos los registros de inventario_gestion_movimiento del grupo.
    /// </summary>
    public async Task EliminarTrasladoAsync(Guid transferGroupId, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);

        var movimientos = await _db.InventarioGestionMovimientos
            .Where(x => x.TransferGroupId == transferGroupId && x.CompanyId == companyId.Value)
            .ToListAsync(ct);
        if (movimientos.Count == 0)
            throw new InvalidOperationException("No se encontró el traslado indicado.");

        var salida = movimientos.FirstOrDefault(x => TrasladoSalidaTypes.Contains(x.MovementType));
        if (salida == null)
            throw new InvalidOperationException("El TransferGroupId no corresponde a un traslado.");

        if (!allowedFarmIds.Contains(salida.FarmId) &&
            !(salida.FromFarmId.HasValue && allowedFarmIds.Contains(salida.FromFarmId.Value)))
            throw new InvalidOperationException("No tiene acceso a este traslado.");

        // Marcar anulado en tabla espejo para todos los movimientos del grupo
        var movIds = movimientos.Select(m => m.Id).ToList();
        var histElimTraslado = await _db.LoteRegistroHistoricoUnificados
            .Where(h => h.OrigenTabla == "inventario_gestion_movimiento" && movIds.Contains(h.OrigenId))
            .ToListAsync(ct);
        foreach (var h in histElimTraslado)
            h.Anulado = true;

        _db.InventarioGestionMovimientos.RemoveRange(movimientos);
        await _db.SaveChangesAsync(ct);

        // Un grupo de traslado toca salida y entrada, y puede repartirse en varios galpones.
        foreach (var ubic in movimientos
                     .Select(m => (m.CompanyId, m.FarmId, m.NucleoId, m.GalponId, m.MovementType))
                     .Distinct())
            await RefrescarSaldoAlimentoEngordeAsync(ubic.CompanyId, ubic.FarmId, ubic.NucleoId, ubic.GalponId, ubic.MovementType, ct);
    }
}
