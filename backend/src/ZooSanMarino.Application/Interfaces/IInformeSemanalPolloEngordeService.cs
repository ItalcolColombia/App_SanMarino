using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Informe Semanal Pollo de Engorde (Panamá). Lee fn_informe_semanal_pollo_engorde
/// (reales desde seguimiento_diario_aves_engorde + movimiento_pollo_engorde).
/// </summary>
public interface IInformeSemanalPolloEngordeService
{
    Task<InformeSemanalReporteDto> GenerarAsync(InformeSemanalRequest request, CancellationToken ct = default);
}
