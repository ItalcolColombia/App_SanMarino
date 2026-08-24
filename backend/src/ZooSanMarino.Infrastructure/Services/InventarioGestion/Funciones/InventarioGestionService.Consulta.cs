// src/ZooSanMarino.Infrastructure/Services/InventarioGestion/Funciones/InventarioGestionService.Consulta.cs
// Datos para filtros y lectura de stock: filtros disponibles, historico de filtros, listado de stock.
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
    public async Task<InventarioGestionFilterDataDto> GetFilterDataAsync(CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId is null or <= 0)
        {
            return new InventarioGestionFilterDataDto(
                Array.Empty<FarmDto>(),
                Array.Empty<FarmDto>(),
                Array.Empty<NucleoDto>(),
                Array.Empty<NucleoDto>(),
                Array.Empty<GalponLiteDto>(),
                Array.Empty<GalponLiteDto>(),
                CompanyManejaAlimentoPorGalpon: false,
                Silos: Array.Empty<InventarioGestionSiloDto>());
        }

        var cid = companyId.Value;

        // Defaults GLOBALES de la empresa: manejo de alimento (el front resuelve el efectivo por
        // granja) y si el inventario se ubica en SILOS en vez de galpones.
        var companyFlags = await _db.Set<Company>().AsNoTracking()
            .Where(c => c.Id == cid)
            .Select(c => new { c.ManejaAlimentoPorGalpon, c.ManejaInventarioPorSilo })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        var companyManejaPorGalpon = companyFlags?.ManejaAlimentoPorGalpon ?? false;
        var companyManejaPorSilo = companyFlags?.ManejaInventarioPorSilo ?? false;

        // Todas las granjas de la empresa (destino: traslado inter-granja, procedencia en ingreso "otra granja"/bodega, etc.)
        var farmsDestino = (await _farmService.GetAllAsync(userId: null, companyId: cid).ConfigureAwait(false)).ToList();
        var allowedDestinoIds = farmsDestino.Select(f => f.Id).ToHashSet();

        var (farmsOrigen, nucleosOrigen, galponesOrigen) = await LoadOrigenUbicacionUsuarioEnEmpresaAsync(cid, ct).ConfigureAwait(false);

        var nucleosAll = (await _nucleoService.GetAllAsync().ConfigureAwait(false)).ToList();
        var nucleosDestino = allowedDestinoIds.Count > 0
            ? nucleosAll.Where(n => allowedDestinoIds.Contains(n.GranjaId)).ToList()
            : new List<NucleoDto>();

        var galponesDetailAll = (await _galponService.GetAllAsync().ConfigureAwait(false)).ToList();
        var galponesDetailDestino = allowedDestinoIds.Count > 0
            ? galponesDetailAll.Where(g => allowedDestinoIds.Contains(g.GranjaId)).ToList()
            : new List<GalponDetailDto>();

        var galponesDestino = galponesDetailDestino
            .Select(g => new GalponLiteDto(g.GalponId, g.GalponNombre, g.NucleoId, g.GranjaId)).ToList();

        // Silos de las granjas visibles (origen + destino: la recepción de tránsito elige el silo de
        // la granja que recibe). Con el flag apagado no se consulta nada: cero costo para el resto.
        var silos = new List<InventarioGestionSiloDto>();
        if (companyManejaPorSilo)
        {
            var farmIdsConSilos = farmsOrigen.Select(f => f.Id)
                .Concat(allowedDestinoIds)
                .Distinct()
                .ToList();

            if (farmIdsConSilos.Count > 0)
            {
                silos = await _db.FarmSilos.AsNoTracking()
                    .Where(fs => farmIdsConSilos.Contains(fs.GranjaId) && fs.DeletedAt == null && fs.Activo)
                    // Mismo orden que GetSilosElegiblesAsync (bodega al final, silos por número de
                    // catálogo) para que el front los pinte igual en el selector y en la grilla.
                    .OrderBy(fs => fs.GranjaId)
                    .ThenBy(fs => fs.Tipo == Domain.Entities.FarmSilo.TipoBodega ? 1 : 0)
                    .ThenBy(fs => _db.SiloCatalogo
                        .Where(sc => sc.Id == fs.SiloCatalogoId)
                        .Select(sc => (int?)sc.Numero)
                        .FirstOrDefault() ?? int.MaxValue)
                    .ThenBy(fs => fs.Nombre)
                    .Select(fs => new InventarioGestionSiloDto(
                        fs.Id, fs.GranjaId, fs.Nombre, fs.Tipo, fs.CodigoErpUbicacion, fs.CodigoBodega))
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
            }
        }

        return new InventarioGestionFilterDataDto(
            FarmsOrigen: farmsOrigen,
            FarmsDestino: farmsDestino,
            NucleosOrigen: nucleosOrigen,
            NucleosDestino: nucleosDestino,
            GalponesOrigen: galponesOrigen,
            GalponesDestino: galponesDestino,
            CompanyManejaAlimentoPorGalpon: companyManejaPorGalpon,
            Silos: silos,
            CompanyManejaInventarioPorSilo: companyManejaPorSilo);
    }

    public async Task<InventarioGestionHistoricoFiltrosDto> GetHistoricoFiltrosAsync(CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId is null or <= 0)
        {
            return new InventarioGestionHistoricoFiltrosDto(
                Array.Empty<InventarioGestionLoteFiltroDto>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                TiposOperacionFiltroLabels,
                Array.Empty<FarmDto>(),
                Array.Empty<NucleoDto>(),
                Array.Empty<GalponLiteDto>());
        }

        var cid = companyId.Value;
        var (farmsOrigenHist, nucleosOrigenHist, galponesOrigenHist) =
            await LoadOrigenUbicacionUsuarioEnEmpresaAsync(cid, ct).ConfigureAwait(false);
        var allowedFarmIds = farmsOrigenHist.Select(f => f.Id).ToHashSet();
        var paisId = await GetEffectivePaisIdAsync(null, ct);

        if (allowedFarmIds.Count == 0)
        {
            return new InventarioGestionHistoricoFiltrosDto(
                Array.Empty<InventarioGestionLoteFiltroDto>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                TiposOperacionFiltroLabels,
                farmsOrigenHist,
                nucleosOrigenHist,
                galponesOrigenHist);
        }

        // Lotes por granja asignada: la empresa se toma de farms.company_id (muchas filas legacy tienen lotes.company_id desalineado).
        var lotesQuery = _db.Lotes.AsNoTracking()
            .Join(_db.Farms.AsNoTracking(),
                l => l.GranjaId,
                f => f.Id,
                (l, f) => new { l, f })
            .Where(x => x.l.DeletedAt == null
                        && allowedFarmIds.Contains(x.l.GranjaId)
                        && x.f.CompanyId == cid
                        && x.f.DeletedAt == null);
        if (paisId > 0)
            lotesQuery = lotesQuery.Where(x => x.l.PaisId == null || x.l.PaisId == paisId);

        var lotes = await lotesQuery
            .OrderByDescending(x => x.l.FechaEncaset)
            .ThenBy(x => x.l.LoteNombre)
            .Select(x => new InventarioGestionLoteFiltroDto(
                x.l.LoteId!.Value,
                x.l.LoteNombre,
                x.l.Fase,
                x.l.GranjaId,
                x.l.NucleoId,
                x.l.GalponId))
            .ToListAsync(ct);

        var movBase = _db.InventarioGestionMovimientos.AsNoTracking()
            .Where(m => m.CompanyId == cid && allowedFarmIds.Contains(m.FarmId));
        if (paisId > 0)
            movBase = movBase.Where(m => m.PaisId == paisId);

        var itemsJoin = _db.ItemInventario.AsNoTracking();

        var conceptos = await (
            from m in movBase
            join i in itemsJoin on m.ItemInventarioEcuadorId equals i.Id
            where i.Concepto != null && i.Concepto != ""
            select i.Concepto!.Trim()
        ).Distinct().ToListAsync(ct);
        conceptos.Sort(StringComparer.OrdinalIgnoreCase);

        var tiposItem = await (
            from m in movBase
            join i in itemsJoin on m.ItemInventarioEcuadorId equals i.Id
            where i.TipoItem != null && i.TipoItem != ""
            select i.TipoItem.Trim()
        ).Distinct().ToListAsync(ct);
        tiposItem.Sort(StringComparer.OrdinalIgnoreCase);

        var estados = await movBase
            .Where(m => m.Estado != null && m.Estado != "")
            .Select(m => m.Estado!.Trim())
            .Distinct()
            .ToListAsync(ct);
        estados.Sort(StringComparer.OrdinalIgnoreCase);

        var movementTypes = await movBase
            .Where(m => m.MovementType != null && m.MovementType != "")
            .Select(m => m.MovementType.Trim())
            .Distinct()
            .ToListAsync(ct);
        movementTypes.Sort(StringComparer.OrdinalIgnoreCase);

        var unidades = await movBase
            .Where(m => m.Unit != null && m.Unit != "")
            .Select(m => m.Unit.Trim())
            .Distinct()
            .ToListAsync(ct);
        unidades.Sort(StringComparer.OrdinalIgnoreCase);

        return new InventarioGestionHistoricoFiltrosDto(
            lotes,
            conceptos,
            tiposItem,
            estados,
            movementTypes,
            unidades,
            TiposOperacionFiltroLabels,
            farmsOrigenHist,
            nucleosOrigenHist,
            galponesOrigenHist);
    }

    public async Task<List<InventarioGestionStockDto>> GetStockAsync(
        int? farmId = null,
        string? nucleoId = null,
        string? galponId = null,
        string? itemType = null,
        string? search = null,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var paisId = await GetEffectivePaisIdAsync(farmId, ct);
        if (companyId == null || companyId.Value <= 0)
            return new List<InventarioGestionStockDto>();

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);
        if (allowedFarmIds.Count == 0)
            return new List<InventarioGestionStockDto>();

        var query = _db.InventarioGestionStock
            .AsNoTracking()
            .Include(x => x.ItemInventario)
            .Include(x => x.Farm)
            .Where(x => x.CompanyId == companyId.Value && allowedFarmIds.Contains(x.FarmId));
        if (paisId > 0) query = query.Where(x => x.PaisId == paisId);
        if (farmId.HasValue) query = query.Where(x => x.FarmId == farmId.Value);
        if (!string.IsNullOrWhiteSpace(nucleoId)) query = query.Where(x => x.NucleoId == nucleoId);
        if (!string.IsNullOrWhiteSpace(galponId)) query = query.Where(x => x.GalponId == galponId);
        if (!string.IsNullOrWhiteSpace(itemType))
        {
            var it = itemType.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventario.Concepto != null &&
                 x.ItemInventario.Concepto.Trim().ToLower() == it) ||
                (x.ItemInventario.TipoItem != null &&
                 x.ItemInventario.TipoItem.Trim().ToLower() == it));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventario.Codigo ?? "").ToLower().Contains(s) ||
                (x.ItemInventario.Nombre ?? "").ToLower().Contains(s));
        }

        var list = await query.OrderBy(x => x.Farm.Name).ThenBy(x => x.NucleoId).ThenBy(x => x.GalponId).ThenBy(x => x.ItemInventario.Nombre).ToListAsync(ct);

        var nucleos = await _db.Nucleos.AsNoTracking().Where(n => list.Select(x => x.NucleoId).Contains(n.NucleoId)).ToDictionaryAsync(n => (n.NucleoId, n.GranjaId), n => n.NucleoNombre, ct);
        var galpones = await _db.Galpones.AsNoTracking().Where(g => list.Select(x => x.GalponId).Contains(g.GalponId)).ToDictionaryAsync(g => (g.GalponId, g.GranjaId), g => g.GalponNombre, ct);
        var silos = await NombresDeSilosAsync(list.Select(x => x.SiloId), ct);

        // Separación pendiente de validar: kilos que un seguimiento diario ya comprometió pero que
        // todavía no se descontaron. Se resuelve acá, en la ÚNICA consulta que responde el saldo, para
        // que ninguna pantalla tenga que acordarse de restarlo por su cuenta.
        var farmsEnVista = list.Select(x => x.FarmId).Distinct().ToList();
        var reservas = farmsEnVista.Count == 0
            ? new List<(int FarmId, int ItemId, string? NucleoId, string? GalponId, int? SiloId, decimal Kg)>()
            : (await _db.SeguimientoReservaAlimento.AsNoTracking()
                .Where(r => farmsEnVista.Contains(r.FarmId) && r.Estado == EstadoReservaSeguimiento.Activa)
                .GroupBy(r => new { r.FarmId, r.ItemInventarioEcuadorId, r.NucleoId, r.GalponId, r.SiloId })
                .Select(g => new
                {
                    g.Key.FarmId, g.Key.ItemInventarioEcuadorId, g.Key.NucleoId, g.Key.GalponId, g.Key.SiloId,
                    Kg = g.Sum(r => r.CantidadKg)
                })
                .ToListAsync(ct))
                .Select(g => (FarmId: g.FarmId, ItemId: g.ItemInventarioEcuadorId, NucleoId: g.NucleoId, GalponId: g.GalponId, SiloId: g.SiloId, Kg: g.Kg))
                .ToList();

        static string NormUbic(string? v) => (v ?? "").Trim();
        var reservadoPorUbicacion = reservas.ToDictionary(
            r => (r.FarmId, r.ItemId, NormUbic(r.NucleoId), NormUbic(r.GalponId), r.SiloId ?? 0),
            r => r.Kg);

        return list.Select(x =>
        {
            var reservado = reservadoPorUbicacion.TryGetValue(
                (x.FarmId, x.ItemInventarioEcuadorId, NormUbic(x.NucleoId), NormUbic(x.GalponId), x.SiloId ?? 0),
                out var kgReservado) ? kgReservado : 0m;
            string? nucleoNombre = x.NucleoId != null && nucleos.TryGetValue((x.NucleoId, x.FarmId), out var nn) ? nn : null;
            string? galponNombre = x.GalponId != null && galpones.TryGetValue((x.GalponId, x.FarmId), out var gn) ? gn : null;
            var itemTypeOut = x.ItemInventario.Concepto ?? x.ItemInventario.TipoItem ?? "alimento";
            return new InventarioGestionStockDto(
                x.Id, x.FarmId, x.NucleoId, x.GalponId, x.ItemInventarioEcuadorId,
                x.ItemInventario.Codigo, x.ItemInventario.Nombre, itemTypeOut,
                // TK-2026-000019 — la unidad la manda el CATÁLOGO, no la columna de la fila. La fila
                // arrastra el default 'kg' de cuando se creó y nadie la sincronizaba, así que un
                // producto creado en litros salía en kilos en esta misma pantalla. El backfill
                // realinea la columna, pero la proyección no depende de que se haya corrido.
                x.Quantity, UnidadInventarioCalculos.Resolver(x.ItemInventario.Unidad, x.Unit),
                x.Farm.Name, nucleoNombre, galponNombre, x.CreatedAt,
                AvisoFechaFueraDeCiclo: null,
                SiloId: x.SiloId,
                SiloNombre: x.SiloId.HasValue && silos.TryGetValue(x.SiloId.Value, out var sn) ? sn : null,
                // DisponibleKg ya no se pasa: es una propiedad DERIVADA del DTO (Quantity − ReservadoKg,
                // la misma cuenta que hacía ReservaSeguimientoCalculos.DisponibleAlimento). Como
                // parámetro, los nueve sitios que arman este DTO a mano para las respuestas de ingreso,
                // traslado y consumo lo dejaban en 0 y el front habría leído «no hay nada».
                ReservadoKg: reservado);
        }).ToList();
    }

    private static bool IsAlimento(ItemInventario item)
    {
        var concept = item.Concepto;
        if (!string.IsNullOrWhiteSpace(concept))
            return string.Equals(concept.Trim(), "alimento", StringComparison.OrdinalIgnoreCase);
        return string.Equals(item.TipoItem?.Trim(), "alimento", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ¿El alimento de esta granja se maneja a NIVEL GRANJA (sin núcleo/galpón)? Negación del flag
    /// CONFIGURABLE por empresa/granja: <c>manejaPorGalpon = farm.ManejaAlimentoPorGalpon ??
    /// company.ManejaAlimentoPorGalpon</c> (ver <see cref="AlimentoNivelResolver"/>). Reemplaza la
    /// decisión previa por país (Colombia=granja); el seed de la migración preserva ese comportamiento.
    /// Solo aplica a alimento; otros conceptos van siempre a nivel granja.
    /// </summary>
    private async Task<bool> EsInventarioNivelGranjaAsync(int farmId, CancellationToken ct)
    {
        var flags = await _db.Farms.AsNoTracking()
            .Where(f => f.Id == farmId)
            .Join(_db.Set<Company>().AsNoTracking(), f => f.CompanyId, c => c.Id,
                (f, c) => new { Farm = f.ManejaAlimentoPorGalpon, Company = c.ManejaAlimentoPorGalpon })
            .FirstOrDefaultAsync(ct);
        // Sin datos → nivel granja (comportamiento seguro: no exige galpón).
        if (flags == null) return true;
        return !ZooSanMarino.Application.Calculos.AlimentoNivelResolver.ManejaPorGalpon(flags.Farm, flags.Company);
    }

    private async Task<(int CompanyId, int PaisId)> GetFarmCompanyAndPaisAsync(int farmId, CancellationToken ct)
    {
        var farm = await _db.Farms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == farmId, ct);
        if (farm == null) throw new InvalidOperationException($"La granja {farmId} no existe.");
        var departamento = await _db.Set<Departamento>().AsNoTracking().FirstOrDefaultAsync(d => d.DepartamentoId == farm.DepartamentoId, ct);
        if (departamento == null) throw new InvalidOperationException($"No se encontró el departamento de la granja {farmId}.");
        return (farm.CompanyId, departamento.PaisId);
    }
}
