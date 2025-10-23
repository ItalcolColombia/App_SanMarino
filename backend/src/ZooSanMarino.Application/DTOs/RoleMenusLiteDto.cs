namespace ZooSanMarino.Application.DTOs;

public sealed record RoleMenusLiteDto(
    int RoleId,
    string RoleName,
    int[] MenuIds
);
