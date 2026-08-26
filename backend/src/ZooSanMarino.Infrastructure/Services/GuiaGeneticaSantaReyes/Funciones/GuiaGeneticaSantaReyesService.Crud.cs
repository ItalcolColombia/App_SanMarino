// src/ZooSanMarino.Infrastructure/Services/GuiaGeneticaSantaReyes/Funciones/GuiaGeneticaSantaReyesService.Crud.cs
// Listado paginado, consulta por id, alta, edición y BAJA SUAVE.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using PagedResultCommon = ZooSanMarino.Application.DTOs.Common.PagedResult<ZooSanMarino.Application.DTOs.GuiaGeneticaSantaReyesDto>;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class GuiaGeneticaSantaReyesService
{
    /// <inheritdoc />
    public async Task<PagedResultCommon> SearchAsync(
        GuiaGeneticaSantaReyesSearchRequest request, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();

        // Pedir de más devuelve el TOPE, nunca el default (ver PaginacionCalculos). Tope de tabla
        // MAESTRA: la guía entera son 615 filas, así que el grid puede traerla de una sola vez.
        var page = PaginacionCalculos.NormalizarPage(request.Page);
        var pageSize = PaginacionCalculos.NormalizarPageSize(
            request.PageSize, PaginacionCalculos.MaximoCatalogoMaestro);

        var query = Vivas(companyId).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Raza))
        {
            var raza = request.Raza.Trim();
            query = query.Where(g => EF.Functions.ILike(g.Raza, $"%{raza}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.AnioGuia))
        {
            var anio = request.AnioGuia.Trim();
            query = query.Where(g => g.AnioGuia.Contains(anio));
        }

        if (request.EdadDesde.HasValue)
            query = query.Where(g => g.Edad >= request.EdadDesde.Value);

        if (request.EdadHasta.HasValue)
            query = query.Where(g => g.Edad <= request.EdadHasta.Value);

        query = AplicarOrden(query, request.SortBy, request.SortDesc);

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToDtoExpression())
            .ToListAsync(ct);

        return new PagedResultCommon
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Orden pedido, o el orden NATURAL de la guía (raza, año, semana) — que es como se lee en papel
    /// y como la espera el grid. El desempate por <c>Edad</c> está siempre presente para que la
    /// paginación sea estable: sin un criterio total, dos páginas consecutivas pueden repetir u
    /// omitir filas.
    /// </summary>
    private static IQueryable<GuiaGeneticaSantaReyes> AplicarOrden(
        IQueryable<GuiaGeneticaSantaReyes> query, string? sortBy, bool sortDesc)
    {
        var ordenada = sortBy?.Trim().ToLowerInvariant() switch
        {
            "raza" => sortDesc ? query.OrderByDescending(g => g.Raza) : query.OrderBy(g => g.Raza),
            "anioguia" or "anio_guia" => sortDesc ? query.OrderByDescending(g => g.AnioGuia) : query.OrderBy(g => g.AnioGuia),
            "edad" => sortDesc ? query.OrderByDescending(g => g.Edad) : query.OrderBy(g => g.Edad),
            "prodporcentaje" or "prod_porcentaje" => sortDesc ? query.OrderByDescending(g => g.ProdPorcentaje) : query.OrderBy(g => g.ProdPorcentaje),
            "retiroach" or "retiro_ac_h" => sortDesc ? query.OrderByDescending(g => g.RetiroAcH) : query.OrderBy(g => g.RetiroAcH),
            "gravediah" or "gr_ave_dia_h" => sortDesc ? query.OrderByDescending(g => g.GrAveDiaH) : query.OrderBy(g => g.GrAveDiaH),
            _ => query.OrderBy(g => g.Raza).ThenBy(g => g.AnioGuia)
        };

        return ordenada.ThenBy(g => g.Edad).ThenBy(g => g.Id);
    }

    /// <inheritdoc />
    public async Task<GuiaGeneticaSantaReyesDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();

        return await Vivas(companyId)
            .AsNoTracking()
            .Where(g => g.Id == id)
            .Select(MapToDtoExpression())
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<GuiaGeneticaSantaReyesDto> CreateAsync(
        CreateGuiaGeneticaSantaReyesDto dto, CancellationToken ct = default)
    {
        ValidarClaveNatural(dto.Raza, dto.AnioGuia, dto.Edad);

        var companyId = await GetEffectiveCompanyIdAsync();

        var entidad = new GuiaGeneticaSantaReyes
        {
            CompanyId = companyId,
            CreatedByUserId = _currentUser.UserId,
            ProdPorcentaje = dto.ProdPorcentaje,
            RetiroAcH = dto.RetiroAcH,
            GrAveDiaH = dto.GrAveDiaH
        };

        AplicarClaveNatural(entidad, dto.Raza, dto.AnioGuia, dto.Edad);

        if (await ExisteCodigoAsync(companyId, entidad.CodigoGuiaGenetica, idExcluido: 0, ct))
            throw new InvalidOperationException(MensajeCodigoDuplicado(entidad.CodigoGuiaGenetica));

        _ctx.GuiaGeneticaSantaReyes.Add(entidad);
        await _ctx.SaveChangesAsync(ct);

        return MapToDto(entidad);
    }

    /// <inheritdoc />
    public async Task<GuiaGeneticaSantaReyesDto> UpdateAsync(
        UpdateGuiaGeneticaSantaReyesDto dto, CancellationToken ct = default)
    {
        ValidarClaveNatural(dto.Raza, dto.AnioGuia, dto.Edad);

        var companyId = await GetEffectiveCompanyIdAsync();

        var entidad = await Vivas(companyId).FirstOrDefaultAsync(g => g.Id == dto.Id, ct)
            ?? throw new KeyNotFoundException(
                $"No existe una línea de guía genética con ID {dto.Id} para la empresa activa.");

        entidad.ProdPorcentaje = dto.ProdPorcentaje;
        entidad.RetiroAcH = dto.RetiroAcH;
        entidad.GrAveDiaH = dto.GrAveDiaH;

        // Recalcula el código: cambiar raza/año/edad NO puede dejar la clave natural apuntando al
        // valor viejo, o el próximo import crearía un duplicado en vez de actualizar esta fila.
        AplicarClaveNatural(entidad, dto.Raza, dto.AnioGuia, dto.Edad);

        if (await ExisteCodigoAsync(companyId, entidad.CodigoGuiaGenetica, idExcluido: entidad.Id, ct))
            throw new InvalidOperationException(MensajeCodigoDuplicado(entidad.CodigoGuiaGenetica));

        entidad.UpdatedByUserId = _currentUser.UserId;

        await _ctx.SaveChangesAsync(ct);

        return MapToDto(entidad);
    }

    /// <summary>
    /// 🔴 Baja SUAVE. <b>Nunca <c>Remove()</c></b>: el UNIQUE
    /// <c>ux_guia_genetica_santa_reyes_codigo</c> está filtrado por <c>deleted_at IS NULL</c>
    /// justamente para que dar de baja una línea deje libre su código y se la pueda recrear. Además,
    /// la guía es el insumo de los indicadores técnicos: un borrado en duro se lleva la trazabilidad
    /// de con qué números se calculó un histórico.
    /// </summary>
    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();

        var entidad = await Vivas(companyId).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (entidad is null) return false;

        entidad.DeletedAt = DateTime.UtcNow;
        entidad.UpdatedByUserId = _currentUser.UserId;

        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Validación mínima del alta/edición. La raza es <b>texto libre</b> a propósito (F2.6 del plan):
    /// no se valida contra una lista de razas conocidas porque sin guía cargada no habría ninguna, y
    /// ese es el <i>deadlock de arranque</i> que hoy vuelve inservible la pantalla de Ecuador.
    /// </summary>
    private static void ValidarClaveNatural(string? raza, string? anioGuia, int edad)
    {
        if (string.IsNullOrWhiteSpace(raza))
            throw new ArgumentException("La raza es obligatoria.", nameof(raza));

        if (string.IsNullOrWhiteSpace(anioGuia))
            throw new ArgumentException("El año de la guía es obligatorio.", nameof(anioGuia));

        if (edad <= 0)
            throw new ArgumentException("La semana (edad) debe ser mayor que cero.", nameof(edad));
    }
}
