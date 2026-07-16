// src/ZooSanMarino.Application/Interfaces/IVacunacionRegistroService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IVacunacionRegistroService
{
    /// <summary>Confirma aplicado (a tiempo/tardío/adelantado, calculado server-side). Fecha = la del servidor al llamar.</summary>
    Task<VacunacionCronogramaItemDto> RegistrarAplicadoAsync(int cronogramaItemId, VacunacionRegistrarAplicadoRequest req, CancellationToken ct = default);

    /// <summary>Marca no aplicado; motivo obligatorio.</summary>
    Task<VacunacionCronogramaItemDto> RegistrarNoAplicadoAsync(int cronogramaItemId, VacunacionRegistrarNoAplicadoRequest req, CancellationToken ct = default);
}
