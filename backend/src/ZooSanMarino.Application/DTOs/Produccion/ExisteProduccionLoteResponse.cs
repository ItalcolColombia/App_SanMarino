// src/ZooSanMarino.Application/DTOs/Produccion/ExisteProduccionLoteResponse.cs
namespace ZooSanMarino.Application.DTOs.Produccion;

public record ExisteProduccionLoteResponse(
    bool Exists,
    int? ProduccionLoteId
);



