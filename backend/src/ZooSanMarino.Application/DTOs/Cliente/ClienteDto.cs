namespace ZooSanMarino.Application.DTOs.Cliente;

public sealed record ClienteDto(
    int       Id,
    string    TipoDocumento,
    string    NumeroIdentificacion,
    string    Nombre,
    string?   Correo,
    string?   Telefono,
    string?   TipoCliente,
    string?   Pais,
    string?   Provincia,
    string?   Distrito,
    string?   Planta,
    string?   Zona,
    string    Status,
    int       CompanyId,
    int       CreatedByUserId,
    DateTime  CreatedAt,
    int?      UpdatedByUserId,
    DateTime? UpdatedAt,
    DateTime? DeletedAt
);
