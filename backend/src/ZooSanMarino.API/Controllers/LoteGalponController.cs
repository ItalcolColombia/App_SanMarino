// src/ZooSanMarino.API/Controllers/LoteGalponController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;
[ApiController]
[Route("api/[controller]")]
public class LoteGalponController : ControllerBase
{
    private readonly ILoteGalponService _svc;
    public LoteGalponController(ILoteGalponService svc) => _svc = svc;

    [HttpGet] public async Task<IActionResult> GetAll() => Ok(await _svc.GetAllAsync());

    [HttpGet("{loteId}/{repId}/{galponId}")]
    public async Task<IActionResult> GetById(int loteId, string repId, string galponId) =>  // Changed from string to int
        (await _svc.GetByIdAsync(loteId, repId, galponId)) is LoteGalponDto dto  // Changed from loteId
          ? Ok(dto) : NotFound();

    [HttpPost] public async Task<IActionResult> Create(CreateLoteGalponDto dto)
    {
        var crt = await _svc.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById),
            new { loteId=crt.LoteId, repId=crt.ReproductoraId, galponId=crt.GalponId }, crt);
    }

    [HttpPut("{loteId}/{repId}/{galponId}")]
    public async Task<IActionResult> Update(int loteId, string repId, string galponId, UpdateLoteGalponDto dto)  // Changed from string to int
    {
        if (dto.LoteId!=loteId||dto.ReproductoraId!=repId||dto.GalponId!=galponId)  // Changed from loteId
            return BadRequest();
        return (await _svc.UpdateAsync(dto)) is LoteGalponDto upd
          ? Ok(upd) : NotFound();
    }

    [HttpDelete("{loteId}/{repId}/{galponId}")]
    public async Task<IActionResult> Delete(int loteId, string repId, string galponId) =>  // Changed from string to int
        (await _svc.DeleteAsync(loteId, repId, galponId)) ? NoContent() : NotFound();  // Changed from loteId
}
