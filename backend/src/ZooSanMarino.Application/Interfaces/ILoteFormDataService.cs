// src/ZooSanMarino.Application/Interfaces/ILoteFormDataService.cs
using ZooSanMarino.Application.DTOs.Lotes;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio que devuelve en una sola llamada todos los catálogos necesarios
/// para el formulario de crear/editar lote (granjas, núcleos, galpones, técnicos, compañías, razas).
/// </summary>
public interface ILoteFormDataService
{
    Task<LoteFormDataDto> GetFormDataAsync(CancellationToken ct = default);
}
