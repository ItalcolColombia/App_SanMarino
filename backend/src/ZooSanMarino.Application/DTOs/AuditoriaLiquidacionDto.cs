// src/ZooSanMarino.Application/DTOs/AuditoriaLiquidacionDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Alcance de la auditoría de liquidación (corrida de pollo engorde Ecuador).
/// El back parsea el Excel correcto a un diccionario clave→valor y lo pasa, junto
/// con este alcance, a fn_auditoria_liquidacion_engorde (toda la lógica vive en BD).
/// </summary>
public record AuditoriaLiquidacionRequest(
    int GranjaId,
    string? NucleoId = null,
    string? LoteCodigo = null
);

/// <summary>
/// Aplica la corrección sugerida: carga <see cref="KgTotal"/> kg en los despachos sin peso de la
/// corrida (distribuido por aves). Endpoint gateado por el permiso 'liquidacion.aplicar_correccion'.
/// </summary>
public record AplicarCorreccionRequest(
    int GranjaId,
    string? NucleoId,
    string? LoteCodigo,
    decimal KgTotal
);
