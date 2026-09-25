using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.GestionVeterinaria;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Agenda de visitas técnicas y tareas territoriales. La empresa, el usuario y su alcance de
/// granja/núcleo/galpón/lote se resuelven en servidor; el cliente nunca decide su propio scope.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
[Tags("Gestión veterinaria — visitas y tareas")]
public class GestionVeterinariaController : ControllerBase
{
    private readonly IGestionVeterinariaService _service;
    public GestionVeterinariaController(IGestionVeterinariaService service) => _service = service;

    [HttpGet("mi-mapa")]
    public async Task<ActionResult<IReadOnlyList<VeterinariaGranjaDto>>> GetMiMapa(CancellationToken ct)
        => Ok(await _service.GetMiMapaAsync(ct));

    [HttpGet("resumen")]
    public async Task<ActionResult<VeterinariaResumenDto>> GetResumen(CancellationToken ct)
        => Ok(await _service.GetResumenAsync(ct));

    [HttpGet("visitas")]
    public async Task<ActionResult<IReadOnlyList<VisitaTecnicaDto>>> GetVisitas(CancellationToken ct)
        => Ok(await _service.GetVisitasAsync(ct));

    [HttpPost("visitas")]
    public Task<IActionResult> CreateVisita([FromBody] VisitaTecnicaCreateRequest req, CancellationToken ct)
        => Run(async () => StatusCode(StatusCodes.Status201Created, await _service.CreateVisitaAsync(req, ct)));

    [HttpPut("visitas/{id:long}")]
    public Task<IActionResult> UpdateVisita(long id, [FromBody] VisitaTecnicaUpdateRequest req, CancellationToken ct)
        => Run(async () => ResultOrNotFound(await _service.UpdateVisitaAsync(id, req, ct)));

    [HttpPost("visitas/{id:long}/realizar")]
    public Task<IActionResult> RealizarVisita(long id, [FromBody] RealizarVisitaRequest req, CancellationToken ct)
        => Run(async () => ResultOrNotFound(await _service.RealizarVisitaAsync(id, req, ct)));

    [HttpPost("visitas/{id:long}/cancelar")]
    public Task<IActionResult> CancelarVisita(long id, CancellationToken ct)
        => Run(async () => ResultOrNotFound(await _service.CancelarVisitaAsync(id, ct)));

    [HttpGet("tareas")]
    public async Task<ActionResult<IReadOnlyList<TareaCampoDto>>> GetTareasCreadas(CancellationToken ct)
        => Ok(await _service.GetTareasCreadasAsync(ct));

    [HttpGet("mis-tareas")]
    public async Task<ActionResult<IReadOnlyList<TareaCampoDto>>> GetMisTareas(
        [FromQuery] bool incluirCerradas, CancellationToken ct)
        => Ok(await _service.GetMisTareasAsync(incluirCerradas, ct));

    [HttpGet("mis-tareas/inicio")]
    public async Task<ActionResult<GestionVeterinariaInicioDto>> GetInicio(CancellationToken ct)
        => Ok(await _service.GetInicioAsync(ct));

    [HttpGet("tareas/{id:long}")]
    public async Task<ActionResult<TareaCampoDto>> GetTarea(long id, CancellationToken ct)
    {
        var dto = await _service.GetTareaAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("tareas")]
    public Task<IActionResult> CreateTarea([FromBody] TareaCampoCreateRequest req, CancellationToken ct)
        => Run(async () => StatusCode(StatusCodes.Status201Created, await _service.CreateTareaAsync(req, ct)));

    [HttpPut("tareas/{id:long}")]
    public Task<IActionResult> UpdateTarea(long id, [FromBody] TareaCampoUpdateRequest req, CancellationToken ct)
        => Run(async () => ResultOrNotFound(await _service.UpdateTareaAsync(id, req, ct)));

    [HttpPost("tareas/{id:long}/cumplir")]
    public Task<IActionResult> CumplirTarea(long id, [FromBody] CumplirTareaCampoRequest req, CancellationToken ct)
        => Run(async () => ResultOrNotFound(await _service.CumplirTareaAsync(id, req, ct)));

    [HttpPost("tareas/{id:long}/reabrir")]
    public Task<IActionResult> ReabrirTarea(long id, CancellationToken ct)
        => Run(async () => ResultOrNotFound(await _service.ReabrirTareaAsync(id, ct)));

    [HttpPost("tareas/{id:long}/cancelar")]
    public Task<IActionResult> CancelarTarea(long id, CancellationToken ct)
        => Run(async () => ResultOrNotFound(await _service.CancelarTareaAsync(id, ct)));

    [HttpGet("tareas/{id:long}/evidencias")]
    public async Task<ActionResult<IReadOnlyList<TareaCampoEvidenciaMetaDto>>> GetEvidencias(long id, CancellationToken ct)
        => Ok(await _service.GetEvidenciasAsync(id, ct));

    [HttpGet("tareas/{id:long}/evidencias/{evidenciaId:long}")]
    public async Task<ActionResult<TareaCampoEvidenciaDto>> GetEvidencia(long id, long evidenciaId, CancellationToken ct)
    {
        var dto = await _service.GetEvidenciaAsync(id, evidenciaId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    private IActionResult ResultOrNotFound<T>(T? dto) where T : class => dto is null ? NotFound() : Ok(dto);

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
