namespace ZooSanMarino.Application.DTOs.Cliente;

public sealed record CreateClienteRequest(
    string  TipoDocumento,
    string  NumeroIdentificacion,
    string  Nombre,
    string? Correo,
    string? Telefono,
    string? TipoCliente,
    string? Pais,
    string? Provincia,
    string? Distrito,
    string? Planta,
    string? Zona
);
