using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Módulos de permisos: el catálogo (qué permisos agrupa cada módulo) y qué módulos tiene cada empresa.
/// Todo cambio se materializa en <c>company_permissions</c>; el login y los gates no cambian.
/// </summary>
/// <remarks>
/// <b>Lecturas abiertas a cualquier sesión</b>, igual que <c>GET api/Permission</c> y
/// <c>GET api/Company/{id}/permissions</c>: el modal de Roles las usa para agrupar la lista.
/// <b>Catálogo → <c>AdminAplicacion</c></b> (lo comparten todas las empresas).
/// <b>Módulos de una empresa → <c>AdminEmpresas</c></b> (mismo gate que <c>PUT api/Company/{id}/permissions</c>).
/// Plan: <c>fase_de_desarrollo/modulos_permisos_por_empresa_plan.md</c>.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public class PermissionModuleController : ControllerBase
{
    private static readonly Regex KeyValida = new("^[a-z0-9_]{2,60}$", RegexOptions.Compiled);

    private readonly IPermissionModuleService _svc;

    public PermissionModuleController(IPermissionModuleService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _svc.GetAllAsync());

    [HttpPost]
    [Authorize(Policy = "AdminAplicacion")]
    public async Task<IActionResult> Create([FromBody] CreatePermissionModuleDto dto)
    {
        var key = (dto?.Key ?? string.Empty).Trim().ToLowerInvariant();
        if (!KeyValida.IsMatch(key))
            return BadRequest(new { message = "La key debe tener 2 a 60 caracteres: minúsculas, números o guion bajo." });
        if (string.IsNullOrWhiteSpace(dto!.Nombre) || dto.Nombre.Trim().Length > 120)
            return BadRequest(new { message = "El nombre es obligatorio (máximo 120 caracteres)." });

        var creado = await _svc.CreateAsync(dto with { Key = key });
        return creado is null
            ? Conflict(new { message = $"Ya existe un módulo con la key '{key}'." })
            : Ok(creado);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "AdminAplicacion")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePermissionModuleDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Nombre) || dto.Nombre.Trim().Length > 120)
            return BadRequest(new { message = "El nombre es obligatorio (máximo 120 caracteres)." });

        var actualizado = await _svc.UpdateAsync(id, dto);
        return actualizado is null ? NotFound() : Ok(actualizado);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "AdminAplicacion")]
    public async Task<IActionResult> Delete(int id) => await _svc.DeleteAsync(id) switch
    {
        EliminarModuloResultado.NoExiste => NotFound(),
        EliminarModuloResultado.EnUso => Conflict(new
        {
            message = "Hay empresas con este módulo prendido. Apagalo en esas empresas antes de eliminarlo."
        }),
        _ => NoContent()
    };

    /// <summary>Reemplaza los permisos del módulo y recalcula los permisos de las empresas.</summary>
    [HttpPut("{id:int}/permissions")]
    [Authorize(Policy = "AdminAplicacion")]
    public async Task<IActionResult> SetPermissions(int id, [FromBody] SetPermissionModulePermissionsRequest request)
    {
        if (request is null) return BadRequest();
        var cambio = await _svc.SetPermissionsAsync(id, request);
        return cambio is null ? NotFound() : Ok(cambio);
    }

    /// <summary>Módulos del catálogo con su estado para la empresa.</summary>
    [HttpGet("company/{companyId:int}")]
    public async Task<IActionResult> GetForCompany(int companyId) =>
        Ok(await _svc.GetForCompanyAsync(companyId));

    /// <summary>Fija los módulos prendidos de la empresa (los no enviados se apagan).</summary>
    [HttpPut("company/{companyId:int}")]
    [Authorize(Policy = "AdminEmpresas")]
    public async Task<IActionResult> SetForCompany(int companyId, [FromBody] SetCompanyPermissionModulesRequest request)
    {
        if (request is null) return BadRequest();
        var cambio = await _svc.SetForCompanyAsync(companyId, request);
        return cambio is null ? NotFound() : Ok(cambio);
    }
}
