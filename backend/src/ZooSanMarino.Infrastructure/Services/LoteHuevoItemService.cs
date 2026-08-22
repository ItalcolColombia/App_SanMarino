// src/ZooSanMarino.Infrastructure/Services/LoteHuevoItemService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// F7.3 — qué tipos de huevo produce un lote. Espeja <see cref="LoteSiloService"/>, que es el patrón
/// canónico de N:M por lote en este repo, con UNA diferencia deliberada: la empresa se resuelve por
/// la GRANJA del lote y no por la empresa activa del token.
///
/// <para>
/// <b>Por qué esa diferencia.</b> El gate de guardado del seguimiento
/// (<c>ProduccionService.ValidarHuevoItemsAsync</c>) valida contra <c>farms.company_id</c>. Si acá
/// se ofreciera el catálogo de la empresa activa y ambas difirieran, el usuario podría declarar un
/// ítem que después el guardado rechaza con 400 — el mismo síntoma que la auditoría encontró entre
/// el selector del diario y su gate. Se resuelve por el mismo dato en los dos lados.
/// </para>
/// </summary>
public class LoteHuevoItemService : ILoteHuevoItemService
{
    /// <summary><c>catalogo_items.item_type</c> de los ítems de huevo.</summary>
    private const string ItemTypeHuevo = "huevo";

    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;

    public LoteHuevoItemService(ZooSanMarinoContext ctx, ICurrentUser current)
    {
        _ctx = ctx;
        _current = current;
    }

    /// <summary>
    /// Lote + empresa DUEÑA DE LA GRANJA (no la del token). Fail-closed: si el lote no existe o su
    /// granja no resuelve empresa, no se devuelve nada.
    /// </summary>
    private async Task<(int CompanyId, int LoteId)> EnsureLoteAsync(int loteId, CancellationToken ct)
    {
        var lote = await _ctx.Lotes.AsNoTracking()
            .Where(l => l.LoteId == loteId && l.DeletedAt == null)
            .Select(l => new { l.GranjaId })
            .FirstOrDefaultAsync(ct);

        if (lote is null)
            throw new InvalidOperationException($"El lote {loteId} no existe.");

        var companyId = await _ctx.Farms.AsNoTracking()
            .Where(f => f.Id == lote.GranjaId)
            .Select(f => (int?)f.CompanyId)
            .FirstOrDefaultAsync(ct);

        if (companyId is not > 0)
            throw new InvalidOperationException(
                $"No se pudo resolver la empresa de la granja {lote.GranjaId} del lote {loteId}.");

        // `loteId` y no `lote.LoteId`: la entidad lo declara `int?` (auto-incremento), pero el
        // `Where` de arriba ya garantizó la igualdad, así que el parámetro es el mismo valor sin nulable.
        return (companyId.Value, loteId);
    }

    /// <summary>Lee una clave de texto del metadata del catálogo, tolerando camelCase y snake_case.</summary>
    private static string? LeerMeta(JsonDocument? metadata, params string[] claves)
    {
        if (metadata is null || metadata.RootElement.ValueKind != JsonValueKind.Object) return null;
        foreach (var clave in claves)
        {
            if (!metadata.RootElement.TryGetProperty(clave, out var v)) continue;
            var s = v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString();
            if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
        }
        return null;
    }

    /// <summary>Lee una clave booleana del metadata del catálogo (solo <c>true</c> enciende).</summary>
    private static bool LeerMetaBool(JsonDocument? metadata, params string[] claves)
    {
        if (metadata is null || metadata.RootElement.ValueKind != JsonValueKind.Object) return false;
        foreach (var clave in claves)
        {
            if (metadata.RootElement.TryGetProperty(clave, out var v) && v.ValueKind == JsonValueKind.True)
                return true;
        }
        return false;
    }

    /// <summary>Forma común del DTO desde un ítem del catálogo, con o sin fila de declaración.</summary>
    private static LoteHuevoItemDto ADto(int filaId, int loteId, CatalogItem ci, bool activo) =>
        new(
            Id: filaId,
            LoteId: loteId,
            CatalogItemId: ci.Id,
            Codigo: ci.Codigo,
            Nombre: ci.Nombre,
            TipoHuevo: LeerMeta(ci.Metadata, "tipoHuevo", "tipo_huevo"),
            Um: LeerMeta(ci.Metadata, "um", "UM", "unidadMedida"),
            PrimeraPostura: LeerMetaBool(ci.Metadata,
                HuevoPrimeraPosturaCalculos.MetadataKeyPrimeraPostura,
                HuevoPrimeraPosturaCalculos.MetadataKeyPrimeraPosturaSnake),
            ItemActivo: ci.Activo,
            Activo: activo
        );

    /// <summary>Ordena Primera → Pnc → resto, y por nombre dentro de cada grupo.</summary>
    private static IEnumerable<LoteHuevoItemDto> Ordenar(IEnumerable<LoteHuevoItemDto> filas) =>
        filas
            .OrderBy(d => HuevoItemsCalculos.PesoTipoHuevo(d.TipoHuevo))
            .ThenBy(d => d.TipoHuevo ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Nombre, StringComparer.OrdinalIgnoreCase);

    public async Task<IEnumerable<LoteHuevoItemDto>> GetByLoteAsync(int loteId, CancellationToken ct = default)
    {
        await EnsureLoteAsync(loteId, ct);

        // Se traen también los ítems dados de baja en el catálogo: un lote que declaró un ítem que
        // después se desactivó tiene que seguir viéndolo (marcado con ItemActivo=false) en vez de
        // perderlo en silencio.
        var filas = await (
            from lhi in _ctx.LoteHuevoItems.AsNoTracking()
            where lhi.LoteId == loteId && lhi.Activo
            join ci in _ctx.CatalogItems.AsNoTracking() on lhi.CatalogItemId equals ci.Id
            select new { lhi.Id, Item = ci }
        ).ToListAsync(ct);

        return Ordenar(filas.Select(x => ADto(x.Id, loteId, x.Item, activo: true))).ToList();
    }

    public async Task<IEnumerable<LoteHuevoItemDto>> GetDisponiblesAsync(int loteId, CancellationToken ct = default)
    {
        var (companyId, _) = await EnsureLoteAsync(loteId, ct);

        // Solo ACTIVOS: ofrecer un ítem dado de baja para declararlo nuevo no tiene sentido. Los ya
        // declarados que se dieron de baja siguen apareciendo por GetByLoteAsync, que no filtra.
        var items = await _ctx.CatalogItems.AsNoTracking()
            .Where(ci => ci.CompanyId == companyId && ci.Activo && ci.ItemType == ItemTypeHuevo)
            .ToListAsync(ct);

        var yaDeclarados = await _ctx.LoteHuevoItems.AsNoTracking()
            .Where(lhi => lhi.LoteId == loteId && lhi.Activo)
            .Select(lhi => lhi.CatalogItemId)
            .ToListAsync(ct);
        var declarados = yaDeclarados.ToHashSet();

        return Ordenar(items.Select(ci => ADto(0, loteId, ci, activo: declarados.Contains(ci.Id)))).ToList();
    }

    public async Task<IEnumerable<LoteHuevoItemDto>> AsignarAsync(
        int loteId, AsignarHuevoItemsDto dto, CancellationToken ct = default)
    {
        var (companyId, _) = await EnsureLoteAsync(loteId, ct);
        var pedidos = (dto.CatalogItemIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToHashSet();

        if (pedidos.Count > 0)
        {
            // El ítem tiene que ser de huevo Y de la empresa dueña de la granja. Se exige `Activo`
            // al DECLARAR (no al leer): declarar un ítem dado de baja crearía una fila fija que el
            // operario no puede usar.
            var validos = await _ctx.CatalogItems.AsNoTracking()
                .Where(ci => pedidos.Contains(ci.Id)
                          && ci.CompanyId == companyId
                          && ci.Activo
                          && ci.ItemType == ItemTypeHuevo)
                .Select(ci => ci.Id)
                .ToListAsync(ct);

            var ajenos = pedidos.Except(validos).ToList();
            if (ajenos.Count > 0)
                throw new InvalidOperationException(
                    "Estos ítems no existen, no son de huevo, están inactivos o no pertenecen a la " +
                    $"empresa de la granja del lote: {string.Join(", ", ajenos)}.");
        }

        var actuales = await _ctx.LoteHuevoItems.Where(lhi => lhi.LoteId == loteId).ToListAsync(ct);
        var ahora = DateTime.UtcNow;

        foreach (var itemId in pedidos)
        {
            var fila = actuales.FirstOrDefault(a => a.CatalogItemId == itemId);
            if (fila is null)
            {
                _ctx.LoteHuevoItems.Add(new LoteHuevoItem
                {
                    CompanyId = companyId,
                    LoteId = loteId,
                    CatalogItemId = itemId,
                    Activo = true,
                    CreatedAt = ahora,
                    CreatedByUserId = _current.UserGuid
                });
            }
            else if (!fila.Activo)
            {
                fila.Activo = true;
            }
        }

        // Desactivar los que ya no se piden. Los seguimientos YA registrados conservan su desglose:
        // cada uno guarda su propia foto en metadata.huevoItems y no depende de esta tabla.
        foreach (var sobra in actuales.Where(a => a.Activo && !pedidos.Contains(a.CatalogItemId)))
            sobra.Activo = false;

        await _ctx.SaveChangesAsync(ct);
        return await GetByLoteAsync(loteId, ct);
    }
}
