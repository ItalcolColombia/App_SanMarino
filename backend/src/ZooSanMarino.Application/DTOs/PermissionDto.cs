namespace ZooSanMarino.Application.DTOs;

public record PermissionDto(
    int    Id,
    string Key,
    string? Description
);

public record CreatePermissionDto(
    string  Key,
    string? Description
);

public record UpdatePermissionDto(
    int     Id,
    string  Key,
    string? Description
);
