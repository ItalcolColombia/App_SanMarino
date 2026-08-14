// src/ZooSanMarino.Infrastructure/Services/Silos/FarmSiloService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Silos y bodegas de una granja. Empresa efectiva resuelta SIEMPRE por <c>farms.company_id</c>
/// (fail-closed): una granja de otra empresa no se toca aunque el token diga otra cosa.
/// </summary>
public class FarmSiloService : IFarmSiloService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;

    public FarmSiloService(ZooSanMarinoContext ctx, ICurrentUser current)
    {
        _ctx = ctx;
        _current = current;
    }

    /// <summary>
    /// La granja tiene que existir y ser de la empresa activa. Devuelve su <c>company_id</c>.
    /// Fail-closed: ante cualquier duda lanza en vez de dejar pasar el registro.
    /// </summary>
    private async Task<int> EnsureGranjaDeEmpresaAsync(int granjaId, CancellationToken ct)
    {
        var companyId = await _ctx.Farms.AsNoTracking()
            .Where(f => f.Id == granjaId && f.DeletedAt == null)
            .Select(f => (int?)f.CompanyId)
            .FirstOrDefaultAsync(ct);

        if (companyId is null)
            throw new InvalidOperationException($"La granja {granjaId} no existe.");
        if (companyId.Value != _current.CompanyId)
            throw new InvalidOperationException($"La granja {granjaId} no pertenece a la empresa activa.");

        return companyId.Value;
    }

    /// <summary>Proyección con los conteos de uso (galpones y lotes que lo declaran).</summary>
    private IQueryable<FarmSiloDto> ProyectarAsync(IQueryable<FarmSilo> q) =>
        from fs in q
        join f in _ctx.Farms.AsNoTracking() on fs.GranjaId equals f.Id into fg
        from farm in fg.DefaultIfEmpty()
        join sc in _ctx.SiloCatalogo.AsNoTracking() on fs.SiloCatalogoId equals sc.Id into scg
        from cat in scg.DefaultIfEmpty()
        select new FarmSiloDto(
            fs.Id,
            fs.CompanyId,
            fs.GranjaId,
            farm != null ? farm.Name : null,
            fs.SiloCatalogoId,
            cat != null ? cat.Numero : (int?)null,
            fs.Nombre,
            fs.Tipo,
            fs.CodigoErpUbicacion,
            fs.Descripcion,
            fs.CentroOperacion,
            fs.CodigoBodega,
            fs.Activo,
            _ctx.GalponSilos.Count(gs => gs.FarmSiloId == fs.Id && gs.Activo),
            _ctx.LoteSilos.Count(ls => ls.FarmSiloId == fs.Id && ls.Activo)
        );

    public async Task<IEnumerable<FarmSiloDto>> GetAsync(int? granjaId = null, bool soloActivos = false, CancellationToken ct = default)
    {
        var companyId = _current.CompanyId;

        var q = _ctx.FarmSilos.AsNoTracking()
            .Where(fs => fs.CompanyId == companyId && fs.DeletedAt == null);

        if (granjaId.HasValue) q = q.Where(fs => fs.GranjaId == granjaId.Value);
        if (soloActivos) q = q.Where(fs => fs.Activo);

        var filas = await ProyectarAsync(q).ToListAsync(ct);

        // Orden en memoria: la clave (bodegas primero, luego silos por número) es lógica pura y no
        // vale la pena traducirla a SQL para un listado de decenas de filas.
        return filas
            .OrderBy(x => SiloCalculos.ClaveOrden(x.Tipo, x.Numero, x.Nombre).Grupo)
            .ThenBy(x => SiloCalculos.ClaveOrden(x.Tipo, x.Numero, x.Nombre).Numero)
            .ThenBy(x => x.Nombre)
            .ToList();
    }

    public async Task<FarmSiloDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var companyId = _current.CompanyId;
        var q = _ctx.FarmSilos.AsNoTracking()
            .Where(fs => fs.Id == id && fs.CompanyId == companyId && fs.DeletedAt == null);
        return await ProyectarAsync(q).FirstOrDefaultAsync(ct);
    }

    public async Task<FarmSiloDto> CreateAsync(CreateFarmSiloDto dto, CancellationToken ct = default)
    {
        var companyId = await EnsureGranjaDeEmpresaAsync(dto.GranjaId, ct);

        var error = SiloCalculos.ValidarAltaFarmSilo(dto.Tipo, dto.SiloCatalogoId, dto.Nombre);
        if (error is not null) throw new InvalidOperationException(error);

        var tipo = SiloCalculos.NormalizarTipo(dto.Tipo)!;
        string nombre;

        if (tipo == SiloCalculos.TipoSilo)
        {
            var cat = await _ctx.SiloCatalogo.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == dto.SiloCatalogoId!.Value
                                       && s.CompanyId == companyId
                                       && s.DeletedAt == null, ct);
            if (cat is null)
                throw new InvalidOperationException($"El silo {dto.SiloCatalogoId} no existe en la lista maestra de la empresa.");

            // El nombre lo manda el catálogo: así "Silo 4" significa lo mismo en toda la empresa.
            nombre = cat.Nombre;

            var yaCatalogo = await _ctx.FarmSilos
                .AnyAsync(fs => fs.GranjaId == dto.GranjaId && fs.SiloCatalogoId == cat.Id && fs.DeletedAt == null, ct);
            if (yaCatalogo)
                throw new InvalidOperationException($"La granja ya tiene asignado '{cat.Nombre}'.");
        }
        else
        {
            nombre = dto.Nombre!.Trim();
        }

        var yaNombre = await _ctx.FarmSilos
            .AnyAsync(fs => fs.GranjaId == dto.GranjaId && fs.Nombre == nombre && fs.DeletedAt == null, ct);
        if (yaNombre)
            throw new InvalidOperationException($"La granja ya tiene una ubicación llamada '{nombre}'.");

        var entity = new FarmSilo
        {
            CompanyId = companyId,
            GranjaId = dto.GranjaId,
            SiloCatalogoId = tipo == SiloCalculos.TipoSilo ? dto.SiloCatalogoId : null,
            Nombre = nombre,
            Tipo = tipo,
            CodigoErpUbicacion = Trim(dto.CodigoErpUbicacion),
            Descripcion = Trim(dto.Descripcion),
            CentroOperacion = Trim(dto.CentroOperacion),
            CodigoBodega = Trim(dto.CodigoBodega),
            Activo = dto.Activo,
            CreatedAt = DateTime.UtcNow
        };

        _ctx.FarmSilos.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        return (await GetByIdAsync(entity.Id, ct))!;
    }

    public async Task<FarmSiloDto?> UpdateAsync(int id, UpdateFarmSiloDto dto, CancellationToken ct = default)
    {
        var companyId = _current.CompanyId;

        var entity = await _ctx.FarmSilos
            .FirstOrDefaultAsync(fs => fs.Id == id && fs.CompanyId == companyId && fs.DeletedAt == null, ct);
        if (entity is null) return null;

        // El nombre de un SILO lo manda el catálogo: renombrarlo por granja rompería que "Silo 4"
        // signifique lo mismo en toda la empresa. Solo la bodega se renombra.
        if (!string.IsNullOrWhiteSpace(dto.Nombre))
        {
            if (entity.SiloCatalogoId is > 0)
                throw new InvalidOperationException(
                    "El nombre de un silo sale de la lista maestra; cámbielo allí para que aplique a todas las granjas.");

            var nombre = dto.Nombre!.Trim();
            var choca = await _ctx.FarmSilos
                .AnyAsync(fs => fs.GranjaId == entity.GranjaId && fs.Nombre == nombre && fs.Id != id && fs.DeletedAt == null, ct);
            if (choca)
                throw new InvalidOperationException($"La granja ya tiene una ubicación llamada '{nombre}'.");
            entity.Nombre = nombre;
        }

        if (dto.CodigoErpUbicacion is not null) entity.CodigoErpUbicacion = Trim(dto.CodigoErpUbicacion);
        if (dto.Descripcion is not null) entity.Descripcion = Trim(dto.Descripcion);
        if (dto.CentroOperacion is not null) entity.CentroOperacion = Trim(dto.CentroOperacion);
        if (dto.CodigoBodega is not null) entity.CodigoBodega = Trim(dto.CodigoBodega);
        if (dto.Activo.HasValue) entity.Activo = dto.Activo.Value;

        entity.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var companyId = _current.CompanyId;

        var entity = await _ctx.FarmSilos
            .FirstOrDefaultAsync(fs => fs.Id == id && fs.CompanyId == companyId && fs.DeletedAt == null, ct);
        if (entity is null) return false;

        await EnsureSinUsoAsync(entity, ct);

        entity.DeletedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.Activo = false;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Un silo con galpones o lotes colgando no se da de baja: el galpón se quedaría sin ubicaciones
    /// que ofrecer y el lote sin de dónde consumir, los dos sin ningún mensaje que lo explique.
    /// <para>
    /// El guard por stock/movimientos entra con la Fase B, cuando <c>inventario_gestion_stock</c>
    /// gane <c>silo_id</c>; hoy ningún movimiento puede apuntar a un silo, así que esta comprobación
    /// es completa para el estado actual del esquema.
    /// </para>
    /// </summary>
    private async Task EnsureSinUsoAsync(FarmSilo entity, CancellationToken ct)
    {
        var galpones = await _ctx.GalponSilos.CountAsync(gs => gs.FarmSiloId == entity.Id && gs.Activo, ct);
        var lotes = await _ctx.LoteSilos.CountAsync(ls => ls.FarmSiloId == entity.Id && ls.Activo, ct);

        if (galpones > 0 || lotes > 0)
            throw new InvalidOperationException(
                $"No se puede eliminar '{entity.Nombre}': lo usan {galpones} galpón(es) y {lotes} lote(s). Quite esas asignaciones o márquelo como inactivo.");
    }

    public async Task<IEnumerable<FarmSiloDto>> AsignarDesdeCatalogoAsync(AsignarSilosGranjaDto dto, CancellationToken ct = default)
    {
        var companyId = await EnsureGranjaDeEmpresaAsync(dto.GranjaId, ct);
        var pedidos = (dto.SiloCatalogoIds ?? Array.Empty<int>()).Distinct().ToHashSet();

        // Los ids pedidos tienen que existir en el catálogo de ESTA empresa.
        var catalogo = await _ctx.SiloCatalogo.AsNoTracking()
            .Where(s => s.CompanyId == companyId && s.DeletedAt == null && pedidos.Contains(s.Id))
            .ToListAsync(ct);

        var faltantes = pedidos.Except(catalogo.Select(c => c.Id)).ToList();
        if (faltantes.Count > 0)
            throw new InvalidOperationException(
                $"Estos silos no existen en la lista maestra de la empresa: {string.Join(", ", faltantes)}.");

        var actuales = await _ctx.FarmSilos
            .Where(fs => fs.GranjaId == dto.GranjaId && fs.DeletedAt == null && fs.SiloCatalogoId != null)
            .ToListAsync(ct);

        var ahora = DateTime.UtcNow;

        // Altas: los del catálogo que la granja todavía no tiene.
        var yaTiene = actuales.Select(a => a.SiloCatalogoId!.Value).ToHashSet();
        foreach (var cat in catalogo.Where(c => !yaTiene.Contains(c.Id)))
        {
            _ctx.FarmSilos.Add(new FarmSilo
            {
                CompanyId = companyId,
                GranjaId = dto.GranjaId,
                SiloCatalogoId = cat.Id,
                Nombre = cat.Nombre,
                Tipo = SiloCalculos.TipoSilo,
                Activo = true,
                CreatedAt = ahora
            });
        }

        // Bajas: los que la granja tiene y ya no se piden. Si alguno está en uso, se aborta ENTERA
        // la operación — dejar la mitad aplicada sería peor que no hacer nada.
        foreach (var sobra in actuales.Where(a => !pedidos.Contains(a.SiloCatalogoId!.Value)))
        {
            await EnsureSinUsoAsync(sobra, ct);
            sobra.DeletedAt = ahora;
            sobra.UpdatedAt = ahora;
            sobra.Activo = false;
        }

        // Bodega de la granja (la «granja global»): una sola, se crea si se pidió y no existe.
        if (dto.CrearBodega)
        {
            var tieneBodega = await _ctx.FarmSilos
                .AnyAsync(fs => fs.GranjaId == dto.GranjaId && fs.SiloCatalogoId == null && fs.DeletedAt == null, ct);
            if (!tieneBodega)
            {
                var nombreBodega = string.IsNullOrWhiteSpace(dto.NombreBodega) ? "Bodega" : dto.NombreBodega!.Trim();
                _ctx.FarmSilos.Add(new FarmSilo
                {
                    CompanyId = companyId,
                    GranjaId = dto.GranjaId,
                    SiloCatalogoId = null,
                    Nombre = nombreBodega,
                    Tipo = SiloCalculos.TipoBodega,
                    Activo = true,
                    CreatedAt = ahora
                });
            }
        }

        await _ctx.SaveChangesAsync(ct);
        return await GetAsync(dto.GranjaId, soloActivos: false, ct);
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
