// src/ZooSanMarino.API/Controllers/ItemInventarioEcuadorController.cs
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/item-inventario-ecuador")]
[Tags("Config - Item Inventario Ecuador")]
public class ItemInventarioEcuadorController : ControllerBase
{
    private readonly IItemInventarioEcuadorService _service;

    public ItemInventarioEcuadorController(IItemInventarioEcuadorService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ItemInventarioEcuadorDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? q = null,
        [FromQuery] string? tipoItem = null,
        [FromQuery] bool? activo = null,
        CancellationToken ct = default)
    {
        var list = await _service.GetAllAsync(q, tipoItem, activo, ct);
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ItemInventarioEcuadorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        var item = await _service.GetByIdAsync(id, ct);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ItemInventarioEcuadorDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] ItemInventarioEcuadorCreateRequest req, CancellationToken ct = default)
    {
        try
        {
            var created = await _service.CreateAsync(req, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("código") || ex.Message.Contains("codigo") || ex.Message.Contains("mismo código"))
                return Conflict(new { message = ex.Message });
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ItemInventarioEcuadorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] ItemInventarioEcuadorUpdateRequest req, CancellationToken ct = default)
    {
        var updated = await _service.UpdateAsync(id, req, ct);
        return updated == null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, [FromQuery] bool hard = false, CancellationToken ct = default)
    {
        var ok = await _service.DeleteAsync(id, hard, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("carga-masiva")]
    [ProducesResponseType(typeof(ItemInventarioEcuadorCargaMasivaResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CargaMasiva([FromBody] List<ItemInventarioEcuadorCargaMasivaRow> filas, CancellationToken ct = default)
    {
        if (filas == null || filas.Count == 0)
            return BadRequest(new { message = "Debe enviar al menos una fila para la carga masiva." });
        try
        {
            var result = await _service.CargaMasivaAsync(filas, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("carga-masiva-excel")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ItemInventarioEcuadorCargaMasivaResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CargaMasivaExcel([FromForm] IFormFile file, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Debe adjuntar un archivo Excel (.xlsx)." });
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Formato inválido. Adjunte un archivo .xlsx." });

        try
        {
            using var stream = file.OpenReadStream();
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.FirstOrDefault();
            if (ws == null)
                return BadRequest(new { message = "El archivo no contiene hojas." });

            var firstRow = ws.FirstRowUsed();
            if (firstRow == null)
                return BadRequest(new { message = "La hoja está vacía." });

            var headerCells = firstRow.CellsUsed().ToList();
            if (headerCells.Count == 0)
                return BadRequest(new { message = "No se encontraron encabezados en la primera fila." });

            static string Norm(string s) => new string((s ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
                .ToArray())
                .Replace("  ", " ");

            var headers = headerCells
                .Select(c => new { Col = c.Address.ColumnNumber, Text = c.GetString() })
                .ToList();

            int? FindCol(Func<string, bool> pred)
                => headers.FirstOrDefault(h => pred(Norm(h.Text)))?.Col;

            var colGrupo = FindCol(h => h.Contains("grupo"));
            var colTipoInv = FindCol(h => h.Contains("tipo") && h.Contains("inventario") && !h.Contains("desc"));
            var colDescTipoInv = FindCol(h => h.Contains("desc") && h.Contains("tipo"));
            var colTipoInventario = FindCol(h => h == "tipo inventario" || (h.Contains("tipo") && h.Contains("inventario")));
            var colReferencia = FindCol(h => h.Contains("referencia"));
            var colDescItem = FindCol(h => h.Contains("desc") && h.Contains("item"));
            var colConcepto = FindCol(h => h.Contains("concepto"));
            var colUnidad = FindCol(h => h.Contains("unidad"));

            var dataStartRow = firstRow.RowNumber() + 1;
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? dataStartRow;

            string? Cell(int row, int? col)
            {
                if (col is null) return null;
                var v = ws.Cell(row, col.Value).GetString();
                return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            }

            var filas = new List<ItemInventarioEcuadorCargaMasivaRow>();
            for (var r = dataStartRow; r <= lastRow; r++)
            {
                var referencia = Cell(r, colReferencia);
                var descItem = Cell(r, colDescItem);
                var concepto = Cell(r, colConcepto);
                if (string.IsNullOrWhiteSpace(referencia) && string.IsNullOrWhiteSpace(descItem) && string.IsNullOrWhiteSpace(concepto))
                    continue;

                // En tu Excel, normalmente "Concepto" es el tipo (Alimento/Desinfectante/etc.)
                var tipoItem = concepto;

                filas.Add(new ItemInventarioEcuadorCargaMasivaRow(
                    Cell(r, colGrupo),
                    Cell(r, colTipoInv),
                    Cell(r, colDescTipoInv),
                    tipoItem,
                    referencia,
                    descItem,
                    concepto,
                    Cell(r, colUnidad)
                ));
            }

            if (filas.Count == 0)
                return BadRequest(new { message = "No se encontraron filas con datos. Verifique que el Excel tenga encabezados y filas debajo." });

            var result = await _service.CargaMasivaAsync(filas, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"No se pudo procesar el Excel: {ex.Message}" });
        }
    }
}
