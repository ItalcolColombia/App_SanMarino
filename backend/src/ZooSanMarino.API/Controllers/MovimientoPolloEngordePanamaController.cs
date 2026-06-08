// src/ZooSanMarino.API/Controllers/MovimientoPolloEngordePanamaController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Tags("Movimiento de Pollo Engorde — Panamá")]
public class MovimientoPolloEngordePanamaController : ControllerBase
{
    private readonly IMovimientoPolloEngordePanamaService _service;

    public MovimientoPolloEngordePanamaController(IMovimientoPolloEngordePanamaService service)
    {
        _service = service;
    }

    /// <summary>
    /// Venta Panamá por galpón: crea un movimiento Pendiente por cada lote con cantidad asignada
    /// (H/M sobre las mixtas), compartiendo cabecera de despacho/factura, en una transacción.
    /// </summary>
    [HttpPost("venta-despacho")]
    [ProducesResponseType(typeof(VentaGranjaDespachoResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostVentaPanamaDespacho([FromBody] CreateVentaPanamaDespachoDto dto)
    {
        if (dto is null) return BadRequest(new { error = "Body requerido." });
        try
        {
            var res = await _service.CreateVentaPanamaDespachoAsync(dto);
            return StatusCode(StatusCodes.Status201Created, res);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
