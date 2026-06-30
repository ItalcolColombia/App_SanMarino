using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.Common;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Sistema centralizado de tickets de soporte y requerimientos.
/// País y autor se infieren del contexto del request (ICurrentUser); nunca del body.
/// Regla de performance: ningún listado devuelve imágenes en Base64.
/// </summary>
[ApiController]
[Route("api/tickets")]
[Produces("application/json")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _service;

    public TicketsController(ITicketService service) => _service = service;

    // ───────────────────────── Solicitante ─────────────────────────

    /// <summary>Crea un ticket (estado inicial ABIERTO). Imágenes Base64 opcionales.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TicketDetailDto>> Create([FromBody] CreateTicketRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _service.CreateAsync(req, ct);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Bandeja del solicitante: solo sus tickets (filtro por año y estado).</summary>
    [HttpGet("mis-tickets")]
    [ProducesResponseType(typeof(PagedResult<TicketListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> MisTickets(
        [FromQuery] int?    anio     = null,
        [FromQuery] string? estado   = null,
        [FromQuery] string? tipo     = null,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.SearchMisTicketsAsync(
            new TicketSearchRequest(anio, estado, tipo, null, null, page, pageSize), ct));

    /// <summary>Detalle del ticket (notas + metadata de imágenes, sin Base64 inline).</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDetailDto>> GetById(long id, CancellationToken ct)
    {
        var dto = await _service.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Metadata de las imágenes del ticket (ligero, sin Base64).</summary>
    [HttpGet("{id:long}/imagenes")]
    [ProducesResponseType(typeof(IEnumerable<TicketImagenMetaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TicketImagenMetaDto>>> GetImagenes(long id, CancellationToken ct)
        => Ok(await _service.GetImagenesMetaAsync(id, ct));

    /// <summary>Devuelve UNA imagen en Base64 bajo demanda (carga perezosa del detalle).</summary>
    [HttpGet("{id:long}/imagenes/{imagenId:long}")]
    [ProducesResponseType(typeof(TicketImagenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketImagenDto>> GetImagen(long id, long imagenId, CancellationToken ct)
    {
        var dto = await _service.GetImagenAsync(id, imagenId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Agrega imágenes adicionales (Base64) a un ticket existente, de forma incremental.</summary>
    [HttpPost("{id:long}/imagenes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddImagenes(long id, [FromBody] AddTicketImagenesRequest req, CancellationToken ct)
    {
        var added = await _service.AddImagenesAsync(id, req, ct);
        return Ok(new { added });
    }

    /// <summary>Agrega una nota / respuesta a la bitácora (solicitante o resolutor).</summary>
    [HttpPost("{id:long}/notas")]
    [ProducesResponseType(typeof(TicketNotaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketNotaDto>> AddNota(long id, [FromBody] CreateTicketNotaRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _service.AddNotaAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // ───────────────────────── Adjuntos (documentos + links) ─────────────────────────

    /// <summary>Lista los adjuntos del ticket (documentos y links) — solo metadata.</summary>
    [HttpGet("{id:long}/adjuntos")]
    [ProducesResponseType(typeof(IEnumerable<TicketAdjuntoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TicketAdjuntoDto>>> GetAdjuntos(long id, CancellationToken ct)
        => Ok(await _service.GetAdjuntosAsync(id, ct));

    /// <summary>Adjunta un documento (Excel/PDF) en Base64.</summary>
    [HttpPost("{id:long}/documentos")]
    [ProducesResponseType(typeof(TicketAdjuntoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketAdjuntoDto>> AddDocumento(long id, [FromBody] AddTicketDocumentoRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _service.AddDocumentoAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Adjunta un link de documento externo (URL + título).</summary>
    [HttpPost("{id:long}/links")]
    [ProducesResponseType(typeof(TicketAdjuntoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketAdjuntoDto>> AddLink(long id, [FromBody] AddTicketLinkRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _service.AddLinkAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Descarga el contenido (Base64) de un documento adjunto.</summary>
    [HttpGet("{id:long}/adjuntos/{adjuntoId:long}/descargar")]
    [ProducesResponseType(typeof(TicketDocumentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDocumentoDto>> DescargarDocumento(long id, long adjuntoId, CancellationToken ct)
    {
        var dto = await _service.GetDocumentoAsync(id, adjuntoId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Elimina un adjunto (documento o link).</summary>
    [HttpDelete("{id:long}/adjuntos/{adjuntoId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAdjunto(long id, long adjuntoId, CancellationToken ct)
        => await _service.DeleteAdjuntoAsync(id, adjuntoId, ct) ? NoContent() : NotFound();

    // ───────────────────────── Resolutor ─────────────────────────

    /// <summary>Bandeja de gestión del resolutor (país inyectado del contexto).</summary>
    [HttpGet("gestion")]
    [ProducesResponseType(typeof(PagedResult<TicketListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> Gestion(
        [FromQuery] int?    anio     = null,
        [FromQuery] string? estado   = null,
        [FromQuery] string? tipo     = null,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.SearchGestionAsync(
            new TicketSearchRequest(anio, estado, tipo, null, null, page, pageSize), ct));

    /// <summary>Toma el ticket: asigna resolutor y, si está ABIERTO, pasa a EN_ANALISIS. Idempotente.</summary>
    [HttpPost("{id:long}/tomar")]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDetailDto>> Tomar(long id, CancellationToken ct)
    {
        var dto = await _service.TomarAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Cambia el estado del ticket (valida la máquina de estados) y registra nota en la bitácora.</summary>
    [HttpPatch("{id:long}/estado")]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDetailDto>> CambiarEstado(long id, [FromBody] CambiarEstadoTicketRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _service.CambiarEstadoAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>El solicitante confirma el cierre de un ticket SOLUCIONADO → CERRADO (cierre por ambas partes).</summary>
    [HttpPost("{id:long}/confirmar-cierre")]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDetailDto>> ConfirmarCierre(long id, [FromBody] ConfirmarCierreRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _service.ConfirmarCierreAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    // ───────────────────────── Super Admin ─────────────────────────

    /// <summary>Bandeja global del super admin (todos los tickets, sin filtro de empresa, con filtros opcionales).</summary>
    /// <remarks>Ruta "global" (no "admin"): AWS WAF AdminProtection bloquea cualquier path con /admin.</remarks>
    [HttpGet("global")]
    [ProducesResponseType(typeof(PagedResult<TicketListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> Admin(
        [FromQuery] int?    paisId         = null,
        [FromQuery] Guid?   assignedToGuid = null,
        [FromQuery] int?    companyId      = null,
        [FromQuery] int?    anio           = null,
        [FromQuery] string? estado         = null,
        [FromQuery] string? tipo           = null,
        [FromQuery] int     page           = 1,
        [FromQuery] int     pageSize       = 20,
        CancellationToken ct = default)
        => Ok(await _service.SearchAdminAsync(
            new TicketSearchRequest(anio, estado, tipo, paisId, companyId, page, pageSize, assignedToGuid), ct));

    /// <summary>Lista de resolutores con tickets asignados (para el dropdown de filtro del admin).</summary>
    /// <remarks>Ruta "global" (no "admin"): AWS WAF AdminProtection bloquea cualquier path con /admin.</remarks>
    [HttpGet("global/resolutores")]
    [ProducesResponseType(typeof(IReadOnlyList<ResolutorListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ResolutorListItemDto>>> GetResolutoresAdmin(CancellationToken ct)
        => Ok(await _service.GetResolutoresAdminAsync(ct));

    // ───────────────────────── Bandeja asignados ─────────────────────────

    /// <summary>Bandeja personal del resolutor: tickets asignados a mí.</summary>
    [HttpGet("asignados")]
    [ProducesResponseType(typeof(PagedResult<TicketListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> Asignados(
        [FromQuery] int?    anio     = null,
        [FromQuery] string? estado   = null,
        [FromQuery] string? tipo     = null,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetAsignadosAsync(
            new TicketSearchRequest(anio, estado, tipo, null, null, page, pageSize), ct));

    /// <summary>Transfiere un ticket de REQUERIMIENTO a DESARROLLO, reasignándolo.</summary>
    [HttpPost("{id:long}/transferir")]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDetailDto>> Transferir(
        long id, [FromBody] TransferirTicketRequest req, CancellationToken ct)
    {
        try
        {
            var dto = await _service.TransferirAsync(id, req, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    // ───────────────────────── Catálogos / utilidades ─────────────────────────

    /// <summary>Catálogos de tipos y estados para poblar los selects del frontend.</summary>
    [HttpGet("catalogos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult Catalogos()
        => Ok(new { tipos = TicketTipos.Todos, estados = TicketEstados.Todos });

    /// <summary>Eliminación lógica del ticket.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
        => await _service.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
