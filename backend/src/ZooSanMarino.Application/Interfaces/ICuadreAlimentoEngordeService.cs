// src/ZooSanMarino.Application/Interfaces/ICuadreAlimentoEngordeService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Verifica el invariante del alimento de pollo engorde:
/// <c>saldo del ciclo activo == stock físico − movimientos posteriores al último seguimiento</c>.
/// <para>
/// El descuadre que originó el trabajo de jul-2026 lo detectó un humano de operación, semanas después
/// de producirse; nada en el sistema lo verificaba. Esto lo pone a la vista el mismo día.
/// </para>
/// </summary>
public interface ICuadreAlimentoEngordeService
{
    /// <summary>Cuadre de todos los galpones de la empresa activa.</summary>
    Task<CuadreAlimentoEngordeDto> ObtenerAsync(bool soloConProblemas = false, CancellationToken ct = default);
}
