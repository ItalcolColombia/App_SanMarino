namespace ZooSanMarino.Application.DTOs.Shared;


public record CompanyLiteDto(
    int Id,
    string Name,
    string[] VisualPermissions,   // ← antes: string?
    bool MobileAccess,
    string? Identifier = null
);