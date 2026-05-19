using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs.Cliente;
using ZooSanMarino.Application.DTOs.Common;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class ClienteService : IClienteService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyResolver _companyResolver;

    public ClienteService(ZooSanMarinoContext ctx, ICurrentUser currentUser, ICompanyResolver companyResolver)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _companyResolver = companyResolver;
    }

    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.ActiveCompanyName))
        {
            var cid = await _companyResolver.GetCompanyIdByNameAsync(_currentUser.ActiveCompanyName);
            if (cid.HasValue) return cid.Value;
        }
        return _currentUser.CompanyId;
    }

    public async Task<IEnumerable<ClienteDto>> GetAllAsync(CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        return await _ctx.Clientes
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.DeletedAt == null)
            .OrderBy(x => x.Nombre)
            .Select(x => ToDto(x))
            .ToListAsync(ct);
    }

    public async Task<ClienteDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var entity = await _ctx.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && x.DeletedAt == null, ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<PagedResult<ClienteDto>> SearchAsync(ClienteSearchRequest req, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();

        var query = _ctx.Clientes
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId);

        if (req.SoloActivos)
            query = query.Where(x => x.DeletedAt == null && x.Status == "A");

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.ToLower();
            query = query.Where(x =>
                x.Nombre.ToLower().Contains(s) ||
                x.NumeroIdentificacion.ToLower().Contains(s) ||
                (x.Correo != null && x.Correo.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(req.TipoCliente))
            query = query.Where(x => x.TipoCliente == req.TipoCliente);

        if (!string.IsNullOrWhiteSpace(req.Pais))
            query = query.Where(x => x.Pais == req.Pais);

        if (!string.IsNullOrWhiteSpace(req.TipoDocumento))
            query = query.Where(x => x.TipoDocumento == req.TipoDocumento);

        query = req.SortBy.ToLower() switch
        {
            "numero_identificacion" => req.SortDesc ? query.OrderByDescending(x => x.NumeroIdentificacion) : query.OrderBy(x => x.NumeroIdentificacion),
            "tipo_cliente"          => req.SortDesc ? query.OrderByDescending(x => x.TipoCliente)          : query.OrderBy(x => x.TipoCliente),
            _                       => req.SortDesc ? query.OrderByDescending(x => x.Nombre)               : query.OrderBy(x => x.Nombre)
        };

        var total = await query.LongCountAsync(ct);
        var items = await query
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(x => ToDto(x))
            .ToListAsync(ct);

        return new PagedResult<ClienteDto>
        {
            Page     = req.Page,
            PageSize = req.PageSize,
            Total    = total,
            Items    = items
        };
    }

    public async Task<ClienteDto> CreateAsync(CreateClienteRequest dto, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();

        var entity = new Cliente
        {
            CompanyId            = companyId,
            CreatedByUserId      = _currentUser.UserId,
            TipoDocumento        = dto.TipoDocumento,
            NumeroIdentificacion = dto.NumeroIdentificacion,
            Nombre               = dto.Nombre,
            Correo               = dto.Correo,
            Telefono             = dto.Telefono,
            TipoCliente          = dto.TipoCliente,
            Pais                 = dto.Pais,
            Provincia            = dto.Provincia,
            Distrito             = dto.Distrito,
            Planta               = dto.Planta,
            Zona                 = dto.Zona,
            Status               = "A"
        };

        _ctx.Clientes.Add(entity);
        await _ctx.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<ClienteDto?> UpdateAsync(int id, UpdateClienteRequest dto, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var entity = await _ctx.Clientes
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && x.DeletedAt == null, ct);

        if (entity is null) return null;

        entity.TipoDocumento        = dto.TipoDocumento;
        entity.NumeroIdentificacion = dto.NumeroIdentificacion;
        entity.Nombre               = dto.Nombre;
        entity.Correo               = dto.Correo;
        entity.Telefono             = dto.Telefono;
        entity.TipoCliente          = dto.TipoCliente;
        entity.Pais                 = dto.Pais;
        entity.Provincia            = dto.Provincia;
        entity.Distrito             = dto.Distrito;
        entity.Planta               = dto.Planta;
        entity.Zona                 = dto.Zona;
        entity.Status               = dto.Status;
        entity.UpdatedByUserId      = _currentUser.UserId;
        entity.UpdatedAt            = DateTime.UtcNow;

        await _ctx.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var entity = await _ctx.Clientes
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && x.DeletedAt == null, ct);

        if (entity is null) return false;

        entity.DeletedAt       = DateTime.UtcNow;
        entity.Status          = "I";
        entity.UpdatedByUserId = _currentUser.UserId;
        entity.UpdatedAt       = DateTime.UtcNow;

        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    private static ClienteDto ToDto(Cliente x) => new(
        x.Id,
        x.TipoDocumento,
        x.NumeroIdentificacion,
        x.Nombre,
        x.Correo,
        x.Telefono,
        x.TipoCliente,
        x.Pais,
        x.Provincia,
        x.Distrito,
        x.Planta,
        x.Zona,
        x.Status,
        x.CompanyId,
        x.CreatedByUserId,
        x.CreatedAt,
        x.UpdatedByUserId,
        x.UpdatedAt,
        x.DeletedAt
    );
}
