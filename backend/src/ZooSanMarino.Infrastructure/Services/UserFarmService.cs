using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class UserFarmService : IUserFarmService
{
    private readonly ZooSanMarinoContext _ctx;

    public UserFarmService(ZooSanMarinoContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<UserFarmsResponseDto> GetUserFarmsAsync(Guid userId)
    {
        var user = await _ctx.Users
            .AsNoTracking()
            .Include(u => u.UserFarms)
                .ThenInclude(uf => uf.Farm)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            throw new InvalidOperationException($"Usuario con ID {userId} no encontrado.");

        var farms = user.UserFarms.Select(uf => new UserFarmDto(
            uf.UserId,
            uf.FarmId,
            $"{user.firstName} {user.surName}".Trim(),
            uf.Farm.Name,
            uf.IsAdmin,
            uf.IsDefault,
            uf.CreatedAt,
            uf.CreatedByUserId
        )).ToArray();

        return new UserFarmsResponseDto(
            user.Id,
            $"{user.firstName} {user.surName}".Trim(),
            farms
        );
    }

    public async Task<FarmUsersResponseDto> GetFarmUsersAsync(int farmId)
    {
        var farm = await _ctx.Farms
            .AsNoTracking()
            .Include(f => f.UserFarms)
                .ThenInclude(uf => uf.User)
            .FirstOrDefaultAsync(f => f.Id == farmId);

        if (farm == null)
            throw new InvalidOperationException($"Granja con ID {farmId} no encontrada.");

        var users = farm.UserFarms.Select(uf => new UserFarmDto(
            uf.UserId,
            uf.FarmId,
            $"{uf.User.firstName} {uf.User.surName}".Trim(),
            farm.Name,
            uf.IsAdmin,
            uf.IsDefault,
            uf.CreatedAt,
            uf.CreatedByUserId
        )).ToArray();

        return new FarmUsersResponseDto(
            farm.Id,
            farm.Name,
            users
        );
    }

    public async Task<UserFarmDto> CreateUserFarmAsync(CreateUserFarmDto dto, Guid createdByUserId)
    {
        // Validar que el usuario existe
        var userExists = await _ctx.Users.AnyAsync(u => u.Id == dto.UserId);
        if (!userExists)
            throw new InvalidOperationException($"Usuario con ID {dto.UserId} no encontrado.");

        // Validar que la granja existe
        var farmExists = await _ctx.Farms.AnyAsync(f => f.Id == dto.FarmId);
        if (!farmExists)
            throw new InvalidOperationException($"Granja con ID {dto.FarmId} no encontrada.");

        // Validar que no existe la asociación
        var exists = await _ctx.UserFarms.AnyAsync(uf => uf.UserId == dto.UserId && uf.FarmId == dto.FarmId);
        if (exists)
            throw new InvalidOperationException("La asociación usuario-granja ya existe.");

        // Si se marca como default, desmarcar otros defaults del usuario
        if (dto.IsDefault)
        {
            var existingDefaults = await _ctx.UserFarms
                .Where(uf => uf.UserId == dto.UserId && uf.IsDefault)
                .ToListAsync();
            
            foreach (var existing in existingDefaults)
            {
                existing.IsDefault = false;
            }
        }

        var userFarm = new UserFarm
        {
            UserId = dto.UserId,
            FarmId = dto.FarmId,
            IsAdmin = dto.IsAdmin,
            IsDefault = dto.IsDefault,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = createdByUserId
        };

        _ctx.UserFarms.Add(userFarm);
        await _ctx.SaveChangesAsync();

        // Obtener los datos completos para el DTO
        var result = await _ctx.UserFarms
            .AsNoTracking()
            .Include(uf => uf.User)
            .Include(uf => uf.Farm)
            .FirstOrDefaultAsync(uf => uf.UserId == dto.UserId && uf.FarmId == dto.FarmId);

        return new UserFarmDto(
            result!.UserId,
            result.FarmId,
            $"{result.User.firstName} {result.User.surName}".Trim(),
            result.Farm.Name,
            result.IsAdmin,
            result.IsDefault,
            result.CreatedAt,
            result.CreatedByUserId
        );
    }

    public async Task<UserFarmDto?> UpdateUserFarmAsync(Guid userId, int farmId, UpdateUserFarmDto dto)
    {
        var userFarm = await _ctx.UserFarms
            .Include(uf => uf.User)
            .Include(uf => uf.Farm)
            .FirstOrDefaultAsync(uf => uf.UserId == userId && uf.FarmId == farmId);

        if (userFarm == null)
            return null;

        // Actualizar campos si se proporcionan
        if (dto.IsAdmin.HasValue)
            userFarm.IsAdmin = dto.IsAdmin.Value;

        if (dto.IsDefault.HasValue)
        {
            if (dto.IsDefault.Value)
            {
                // Desmarcar otros defaults del usuario
                var existingDefaults = await _ctx.UserFarms
                    .Where(uf => uf.UserId == userId && uf.FarmId != farmId && uf.IsDefault)
                    .ToListAsync();
                
                foreach (var existing in existingDefaults)
                {
                    existing.IsDefault = false;
                }
            }
            userFarm.IsDefault = dto.IsDefault.Value;
        }

        await _ctx.SaveChangesAsync();

        return new UserFarmDto(
            userFarm.UserId,
            userFarm.FarmId,
            $"{userFarm.User.firstName} {userFarm.User.surName}".Trim(),
            userFarm.Farm.Name,
            userFarm.IsAdmin,
            userFarm.IsDefault,
            userFarm.CreatedAt,
            userFarm.CreatedByUserId
        );
    }

    public async Task<bool> DeleteUserFarmAsync(Guid userId, int farmId)
    {
        var userFarm = await _ctx.UserFarms
            .FirstOrDefaultAsync(uf => uf.UserId == userId && uf.FarmId == farmId);

        if (userFarm == null)
            return false;

        _ctx.UserFarms.Remove(userFarm);
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<UserFarmDto[]> AssociateUserFarmsAsync(AssociateUserFarmsDto dto, Guid createdByUserId)
    {
        var results = new List<UserFarmDto>();

        foreach (var farmId in dto.FarmIds)
        {
            try
            {
                var createDto = new CreateUserFarmDto(dto.UserId, farmId, dto.IsAdmin, dto.IsDefault);
                var result = await CreateUserFarmAsync(createDto, createdByUserId);
                results.Add(result);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("ya existe"))
            {
                // Si ya existe, actualizar en su lugar
                var updateDto = new UpdateUserFarmDto(dto.IsAdmin, dto.IsDefault);
                var updated = await UpdateUserFarmAsync(dto.UserId, farmId, updateDto);
                if (updated != null)
                    results.Add(updated);
            }
        }

        return results.ToArray();
    }

    public async Task<UserFarmDto[]> AssociateFarmUsersAsync(AssociateFarmUsersDto dto, Guid createdByUserId)
    {
        var results = new List<UserFarmDto>();

        foreach (var userId in dto.UserIds)
        {
            try
            {
                var createDto = new CreateUserFarmDto(userId, dto.FarmId, dto.IsAdmin, false);
                var result = await CreateUserFarmAsync(createDto, createdByUserId);
                results.Add(result);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("ya existe"))
            {
                // Si ya existe, actualizar en su lugar
                var updateDto = new UpdateUserFarmDto(dto.IsAdmin, null);
                var updated = await UpdateUserFarmAsync(userId, dto.FarmId, updateDto);
                if (updated != null)
                    results.Add(updated);
            }
        }

        return results.ToArray();
    }

    public async Task<UserFarmDto[]> ReplaceUserFarmsAsync(Guid userId, int[] farmIds, Guid createdByUserId)
    {
        // Eliminar asociaciones existentes
        var existing = await _ctx.UserFarms.Where(uf => uf.UserId == userId).ToListAsync();
        _ctx.UserFarms.RemoveRange(existing);
        await _ctx.SaveChangesAsync();

        // Crear nuevas asociaciones
        var results = new List<UserFarmDto>();
        for (int i = 0; i < farmIds.Length; i++)
        {
            var isDefault = i == 0; // Primera granja como default
            var createDto = new CreateUserFarmDto(userId, farmIds[i], false, isDefault);
            var result = await CreateUserFarmAsync(createDto, createdByUserId);
            results.Add(result);
        }

        return results.ToArray();
    }

    public async Task<UserFarmDto[]> ReplaceFarmUsersAsync(int farmId, Guid[] userIds, Guid createdByUserId)
    {
        // Eliminar asociaciones existentes
        var existing = await _ctx.UserFarms.Where(uf => uf.FarmId == farmId).ToListAsync();
        _ctx.UserFarms.RemoveRange(existing);
        await _ctx.SaveChangesAsync();

        // Crear nuevas asociaciones
        var results = new List<UserFarmDto>();
        foreach (var userId in userIds)
        {
            var createDto = new CreateUserFarmDto(userId, farmId, false, false);
            var result = await CreateUserFarmAsync(createDto, createdByUserId);
            results.Add(result);
        }

        return results.ToArray();
    }

    public async Task<bool> HasUserAccessToFarmAsync(Guid userId, int farmId)
    {
        // Verificar acceso directo
        var directAccess = await _ctx.UserFarms
            .AnyAsync(uf => uf.UserId == userId && uf.FarmId == farmId);

        if (directAccess)
            return true;

        // Verificar acceso a través de compañías
        var companyAccess = await _ctx.UserFarms
            .Where(uf => uf.UserId == userId)
            .Join(_ctx.Farms, uf => uf.FarmId, f => f.Id, (uf, f) => f)
            .Join(_ctx.UserCompanies, f => f.CompanyId, uc => uc.CompanyId, (f, uc) => new { f.Id, uc.UserId })
            .AnyAsync(x => x.UserId == userId && x.Id == farmId);

        return companyAccess;
    }

    public async Task<UserFarmLiteDto[]> GetUserAccessibleFarmsAsync(Guid userId)
    {
        // Obtener granjas con acceso directo
        var directFarms = await _ctx.UserFarms
            .AsNoTracking()
            .Where(uf => uf.UserId == userId)
            .Include(uf => uf.Farm)
            .Select(uf => new UserFarmLiteDto(
                uf.FarmId,
                uf.Farm.Name,
                uf.IsAdmin,
                uf.IsDefault
            ))
            .ToArrayAsync();

        // Obtener granjas a través de compañías
        var companyFarms = await _ctx.UserCompanies
            .AsNoTracking()
            .Where(uc => uc.UserId == userId)
            .Join(_ctx.Farms, uc => uc.CompanyId, f => f.CompanyId, (uc, f) => f)
            .Where(f => !_ctx.UserFarms.Any(uf => uf.UserId == userId && uf.FarmId == f.Id))
            .Select(f => new UserFarmLiteDto(
                f.Id,
                f.Name,
                false, // No es admin por acceso directo
                false  // No es default por acceso directo
            ))
            .ToArrayAsync();

        return directFarms.Concat(companyFarms).ToArray();
    }
}
