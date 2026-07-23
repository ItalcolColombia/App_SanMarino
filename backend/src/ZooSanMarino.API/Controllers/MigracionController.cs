// src/ZooSanMarino.API/Controllers/MigracionController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.Migracion;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Módulo independiente de Migraciones Masivas (Postura). Orquesta la carga masiva por Excel
/// reutilizando las reglas de negocio de los módulos existentes. La empresa se resuelve del
/// header de empresa activa (X-Active-Company[-Id]) validado por el middleware.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Tags("Migracion")]
public class MigracionController : ControllerBase
{
    private readonly IMigracionService _svc;
    private readonly ILogger<MigracionController> _logger;

    public MigracionController(IMigracionService svc, ILogger<MigracionController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    /// <summary>Catálogo de tipos de migración soportados.</summary>
    [HttpGet("tipos")]
    [ProducesResponseType(typeof(IEnumerable<TipoMigracionInfoDto>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<TipoMigracionInfoDto>> GetTipos() => Ok(_svc.GetTipos());

    /// <summary>
    /// Historial paginado de auditoría de la empresa activa (opcionalmente por tipo).
    /// <paramref name="incluirValidaciones"/> (default true) incluye las corridas dry-run (/validar);
    /// en false solo muestra importaciones reales. <paramref name="pageSize"/> se acota a 1..100.
    /// </summary>
    [HttpGet("historial")]
    [ProducesResponseType(typeof(MigracionHistorialPagedDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MigracionHistorialPagedDto>> GetHistorial(
        [FromQuery] string? tipo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool incluirValidaciones = true,
        CancellationToken ct = default)
        => Ok(await _svc.GetHistorialAsync(tipo, page, pageSize, incluirValidaciones, ct));

    /// <summary>Errores/advertencias de una corrida puntual del historial (deserializados desde su auditoría).</summary>
    [HttpGet("historial/{id:int}/errores")]
    [ProducesResponseType(typeof(IEnumerable<MigracionErrorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetErroresDeCorrida(int id, CancellationToken ct)
    {
        var errores = await _svc.GetErroresAsync(id, ct);
        if (errores is null)
            return NotFound(new { message = $"No se encontró la corrida de migración {id}." });
        return Ok(errores);
    }

    /// <summary>Lotes elegibles para migración de históricos según las reglas de fase.</summary>
    [HttpGet("elegibles")]
    [ProducesResponseType(typeof(IEnumerable<LoteElegibleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetElegibles(
        [FromQuery] string tipo,
        [FromQuery] int? granjaId,
        [FromQuery] string? nucleoId,
        [FromQuery] string? galponId,
        CancellationToken ct)
    {
        if (!TryParseTipo(tipo, out var t))
            return BadRequest(new { message = $"Tipo de migración inválido: {tipo}" });

        var ctx = new MigracionContextoDto(granjaId, nucleoId, galponId, null);
        return await EjecutarAsync(async () => Ok(await _svc.GetElegiblesAsync(t, ctx, ct)), tipo);
    }

    /// <summary>Lotes reproductora del lote engorde indicado (selector opcional de Seguimiento Reproductora Engorde).</summary>
    [HttpGet("reproductoras")]
    [ProducesResponseType(typeof(IEnumerable<ReproductoraElegibleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReproductoras([FromQuery] int loteId, CancellationToken ct)
        => await EjecutarAsync(async () => Ok(await _svc.GetReproductorasElegiblesAsync(loteId, ct)), "reproductoras");

    /// <summary>Descarga la plantilla .xlsx del tipo indicado (generada por el sistema).</summary>
    [HttpGet("plantilla")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DescargarPlantilla(
        [FromQuery] string tipo,
        [FromQuery] int? granjaId,
        [FromQuery] string? nucleoId,
        [FromQuery] string? galponId,
        [FromQuery] int? loteId,
        [FromQuery] int? reproductoraId,
        CancellationToken ct)
    {
        if (!TryParseTipo(tipo, out var t))
            return BadRequest(new { message = $"Tipo de migración inválido: {tipo}" });

        var ctx = new MigracionContextoDto(granjaId, nucleoId, galponId, loteId, reproductoraId);
        return await EjecutarAsync(async () =>
        {
            var (contenido, nombre) = await _svc.GenerarPlantillaAsync(t, ctx, ct);
            return File(contenido, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
        }, tipo);
    }

    /// <summary>Valida el Excel (dry-run): no inserta, devuelve el reporte de errores completo (sin importación parcial).</summary>
    [HttpPost("validar")]
    [ProducesResponseType(typeof(MigracionResultDto), StatusCodes.Status200OK)]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public Task<IActionResult> Validar([FromForm] MigracionUploadForm form, CancellationToken ct)
        => ProcesarUpload(form, dryRun: true, ct);

    /// <summary>
    /// Importa el Excel: valida y, solo si no hay errores, inserta masivamente (all-or-nothing).
    /// Si <see cref="MigracionUploadForm.PermitirParcial"/> es true, inserta únicamente las filas
    /// válidas cuando el archivo tiene además filas con error (Estado "ProcesadoParcial" en la respuesta).
    /// </summary>
    [HttpPost("importar")]
    [ProducesResponseType(typeof(MigracionResultDto), StatusCodes.Status200OK)]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public Task<IActionResult> Importar([FromForm] MigracionUploadForm form, CancellationToken ct)
        => ProcesarUpload(form, dryRun: false, ct);

    // ───────────────────────────── helpers ─────────────────────────────

    private async Task<IActionResult> ProcesarUpload(MigracionUploadForm form, bool dryRun, CancellationToken ct)
    {
        if (form.File is null || form.File.Length == 0)
            return BadRequest(new { message = "No se ha proporcionado ningún archivo." });
        if (!TryParseTipo(form.Tipo, out var t))
            return BadRequest(new { message = $"Tipo de migración inválido: {form.Tipo}" });

        var ctx = new MigracionContextoDto(form.GranjaId, form.NucleoId, form.GalponId, form.LoteId, form.ReproductoraId);
        return await EjecutarAsync(async () =>
        {
            var res = dryRun
                ? await _svc.ValidarAsync(t, form.File!, ctx, ct)
                : await _svc.ImportarAsync(t, form.File!, ctx, form.PermitirParcial, ct);
            return Ok(res);
        }, form.Tipo);
    }

    private async Task<IActionResult> EjecutarAsync(Func<Task<IActionResult>> accion, string tipo)
    {
        try
        {
            return await accion();
        }
        catch (NotImplementedException ex)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en migración masiva ({Tipo})", tipo);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Error interno durante la migración." });
        }
    }

    private static bool TryParseTipo(string? tipo, out TipoMigracion parsed)
    {
        parsed = default;
        return !string.IsNullOrWhiteSpace(tipo)
            && Enum.TryParse(tipo, ignoreCase: true, out parsed)
            && Enum.IsDefined(typeof(TipoMigracion), parsed);
    }
}

/// <summary>Modelo de binding multipart para /validar y /importar (vive en la API por depender de IFormFile).</summary>
public class MigracionUploadForm
{
    public IFormFile? File { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int? GranjaId { get; set; }
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }
    public int? LoteId { get; set; }
    /// <summary>Solo Seguimiento Reproductora Engorde: lote reproductora elegido en pantalla (opcional).</summary>
    public int? ReproductoraId { get; set; }

    /// <summary>
    /// Solo aplica a /importar. Si true y hay filas válidas junto a filas con error, inserta SOLO
    /// las válidas (Estado "ProcesadoParcial"). Default false: comportamiento all-or-nothing.
    /// </summary>
    public bool PermitirParcial { get; set; }
}
