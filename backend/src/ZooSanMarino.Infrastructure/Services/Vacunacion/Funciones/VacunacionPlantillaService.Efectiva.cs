// Vacunacion/Funciones/VacunacionPlantillaService.Efectiva.cs
// SOLO LECTURA: qué plantilla le tocaría a un lote y por qué. No escribe cronograma — eso es W2.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class VacunacionPlantillaService
{
    /// <summary>Lo que hace falta del lote para resolver su plantilla, sea de la línea que sea.</summary>
    private readonly record struct LoteParaPlantilla(string LoteNombre, string? Raza, DateTime? FechaEncaset);

    /// <summary>
    /// Lote de la línea pedida, <b>de la empresa activa</b>. Devuelve <c>null</c> si no existe o es de
    /// otra empresa: la vista previa no puede ser un camino para enterarse de lotes ajenos.
    /// </summary>
    private async Task<LoteParaPlantilla?> ResolverLoteAsync(string lineaProductiva, int loteId, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId;

        return lineaProductiva switch
        {
            "Levante" => await _ctx.LotePosturaLevante.AsNoTracking()
                .Where(l => l.LotePosturaLevanteId == loteId && l.CompanyId == companyId)
                .Select(l => (LoteParaPlantilla?)new LoteParaPlantilla(l.LoteNombre, l.Raza, l.FechaEncaset))
                .FirstOrDefaultAsync(ct),

            "Produccion" => await _ctx.LotePosturaProduccion.AsNoTracking()
                .Where(l => l.LotePosturaProduccionId == loteId && l.CompanyId == companyId)
                .Select(l => (LoteParaPlantilla?)new LoteParaPlantilla(l.LoteNombre, l.Raza, l.FechaEncaset))
                .FirstOrDefaultAsync(ct),

            "Engorde" => await _ctx.LoteAveEngorde.AsNoTracking()
                .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId)
                .Select(l => (LoteParaPlantilla?)new LoteParaPlantilla(l.LoteNombre, l.Raza, l.FechaEncaset))
                .FirstOrDefaultAsync(ct),

            _ => throw new InvalidOperationException($"lineaProductiva inválida: '{lineaProductiva}'. Debe ser Levante, Produccion o Engorde."),
        };
    }

    /// <inheritdoc />
    public async Task<VacunacionPlantillaEfectivaDto> GetEfectivaAsync(string lineaProductiva, int loteId, CancellationToken ct = default)
    {
        var linea = (lineaProductiva ?? "").Trim();
        if (!LineasValidas.Contains(linea))
            throw new InvalidOperationException($"lineaProductiva inválida: '{lineaProductiva}'. Debe ser Levante, Produccion o Engorde.");

        var lote = await ResolverLoteAsync(linea, loteId, ct)
            ?? throw new InvalidOperationException($"Lote {linea} {loteId} no existe o no pertenece a la empresa activa.");

        // La cabecera completa, porque el nombre y la vigencia se necesitan para explicar la elección.
        var plantillas = await PlantillasDeLaEmpresa().AsNoTracking()
            .Where(p => p.LineaProductiva == linea)
            .Select(p => new { p.Id, p.Nombre, p.LineaProductiva, p.Raza, p.VigenteDesde, p.Activa })
            .ToListAsync(ct);

        var candidatas = plantillas
            .Select(p => new VacunacionPlantillaCalculos.Candidata(
                p.Id, p.LineaProductiva, p.Raza,
                p.VigenteDesde == null ? null : DateOnly.FromDateTime(p.VigenteDesde.Value),
                p.Activa))
            .ToList();

        var encaset = lote.FechaEncaset is { } f ? DateOnly.FromDateTime(f) : (DateOnly?)null;
        var elegida = VacunacionPlantillaCalculos.ResolverEfectiva(candidatas, linea, lote.Raza, encaset);

        var nombreElegida = elegida is { } e ? plantillas.FirstOrDefault(p => p.Id == e.Id)?.Nombre : null;
        var motivo = VacunacionPlantillaCalculos.DescribirResolucion(
            candidatas, linea, lote.Raza, encaset, elegida?.Id, nombreElegida);

        VacunacionPlantillaDetalleDto? detalle = null;
        if (elegida is { } sel)
        {
            var entidad = await PlantillasDeLaEmpresa().AsNoTracking().FirstOrDefaultAsync(p => p.Id == sel.Id, ct);
            if (entidad is not null) detalle = await MapDetalleAsync(entidad, ct);
        }

        return new VacunacionPlantillaEfectivaDto(
            linea, loteId, lote.LoteNombre, lote.Raza, lote.FechaEncaset, detalle, motivo);
    }
}
