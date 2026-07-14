// src/ZooSanMarino.Application/DTOs/Vacunacion/VacunacionRegistroDtos.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>Registrar aplicado. FechaAplicacion NO viaja en el request: la fija el servidor.</summary>
public record VacunacionRegistrarAplicadoRequest(
    string? MotivoDescripcion,
    int? AplicadoPorUserId,
    string? AplicadoPorNombreLibre
);

public record VacunacionRegistrarNoAplicadoRequest(
    string MotivoDescripcion
);
