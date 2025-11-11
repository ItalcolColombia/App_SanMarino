using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Controlador para gestionar relaciones empresa-país y usuario-empresa-país
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CompanyPaisController : ControllerBase
{
    private readonly ICompanyPaisService _service;
    private readonly ICurrentUser _currentUser;

    public CompanyPaisController(ICompanyPaisService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Obtiene todas las relaciones empresa-país
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _service.GetAllCompanyPaisAsync());

    /// <summary>
    /// Obtiene todas las empresas asignadas a un país
    /// </summary>
    [HttpGet("pais/{paisId}/companies")]
    public async Task<IActionResult> GetCompaniesByPais(int paisId) =>
        Ok(await _service.GetCompaniesByPaisAsync(paisId));

    /// <summary>
    /// Obtiene todos los países asignados a una empresa
    /// </summary>
    [HttpGet("company/{companyId}/paises")]
    public async Task<IActionResult> GetPaisesByCompany(int companyId) =>
        Ok(await _service.GetPaisesByCompanyAsync(companyId));

    /// <summary>
    /// Obtiene todas las combinaciones empresa-país de un usuario
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserCompanyPais(Guid userId) =>
        Ok(await _service.GetUserCompanyPaisAsync(userId));

    /// <summary>
    /// Obtiene las combinaciones empresa-país del usuario actual
    /// </summary>
    [HttpGet("user/current")]
    public async Task<IActionResult> GetCurrentUserCompanyPais()
    {
        if (_currentUser.UserId == 0)
            return Unauthorized("Usuario no autenticado");

        // Convertir int UserId a Guid (el UserId en ICurrentUser es int pero en DB es Guid)
        // Obtener el UserId real del claim que es Guid
        var userIdClaim = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("No se pudo obtener el ID del usuario");
        }

        return Ok(await _service.GetUserCompanyPaisAsync(userId));
    }

    /// <summary>
    /// Asigna una empresa a un país
    /// </summary>
    [HttpPost("assign")]
    public async Task<IActionResult> AssignCompanyToPais([FromBody] AssignCompanyPaisDto dto)
    {
        try
        {
            var result = await _service.AssignCompanyToPaisAsync(dto);
            return CreatedAtAction(
                nameof(GetCompaniesByPais), 
                new { paisId = dto.PaisId }, 
                result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Remueve la asignación de una empresa a un país
    /// </summary>
    [HttpPost("remove")]
    public async Task<IActionResult> RemoveCompanyFromPais([FromBody] RemoveCompanyPaisDto dto)
    {
        try
        {
            var result = await _service.RemoveCompanyFromPaisAsync(dto);
            return result ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Asigna un usuario a una empresa en un país específico
    /// </summary>
    [HttpPost("user/assign")]
    public async Task<IActionResult> AssignUserToCompanyPais([FromBody] AssignUserCompanyPaisDto dto)
    {
        try
        {
            var result = await _service.AssignUserToCompanyPaisAsync(dto);
            return CreatedAtAction(
                nameof(GetUserCompanyPais), 
                new { userId = dto.UserId }, 
                result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Remueve la asignación de un usuario a una empresa-país
    /// </summary>
    [HttpPost("user/remove")]
    public async Task<IActionResult> RemoveUserFromCompanyPais([FromBody] RemoveUserCompanyPaisDto dto)
    {
        var result = await _service.RemoveUserFromCompanyPaisAsync(dto);
        return result ? NoContent() : NotFound();
    }

    /// <summary>
    /// Valida que una empresa pertenece a un país
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateCompanyPais([FromBody] AssignCompanyPaisDto dto)
    {
        var isValid = await _service.ValidateCompanyPaisAsync(dto.CompanyId, dto.PaisId);
        return Ok(new { isValid, companyId = dto.CompanyId, paisId = dto.PaisId });
    }
}

