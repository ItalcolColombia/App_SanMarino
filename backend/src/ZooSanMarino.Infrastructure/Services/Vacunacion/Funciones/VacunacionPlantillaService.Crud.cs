// Vacunacion/Funciones/VacunacionPlantillaService.Crud.cs
// Alta, edición y baja de la plantilla y de sus ítems.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class VacunacionPlantillaService
{
    /// <inheritdoc />
    public async Task<List<VacunacionPlantillaDto>> GetAllAsync(
        string? lineaProductiva = null, bool soloActivas = false, CancellationToken ct = default)
    {
        var q = PlantillasDeLaEmpresa().AsNoTracking();

        var linea = (lineaProductiva ?? "").Trim();
        if (linea.Length > 0) q = q.Where(p => p.LineaProductiva == linea);
        if (soloActivas) q = q.Where(p => p.Activa);

        // El conteo va en la consulta, no contando listas en memoria: una empresa con muchos planes
        // arrastraría cientos de ítems que la lista ni muestra.
        return await q
            .OrderBy(p => p.LineaProductiva).ThenBy(p => p.Nombre)
            .Select(p => new VacunacionPlantillaDto(
                p.Id, p.Nombre, p.LineaProductiva, p.Raza, p.VigenteDesde, p.Activa, p.Notas,
                _ctx.VacunacionPlanPlantillaItem.Count(i => i.PlantillaId == p.Id && i.DeletedAt == null)))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<VacunacionPlantillaDetalleDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var p = await PlantillasDeLaEmpresa().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? null : await MapDetalleAsync(p, ct);
    }

    /// <summary>
    /// Las plantillas vivas de la empresa, en la forma mínima que necesita la regla de unicidad.
    ///
    /// <para>
    /// ⚠️ La conversión a <c>DateOnly</c> va <b>después</b> de materializar: Npgsql no puede traducir
    /// <c>DateOnly.FromDateTime</c> sobre una columna <c>date</c> y la consulta explota en runtime
    /// («Can only apply TimeOnly.FromDateTime on a timestamp or timestamptz column»). Compila igual,
    /// así que esto sólo se ve pegándole al endpoint.
    /// </para>
    /// </summary>
    private async Task<List<VacunacionPlantillaCalculos.PlantillaExistente>> CandidatasParaUnicidadAsync(CancellationToken ct)
    {
        var filas = await PlantillasDeLaEmpresa().AsNoTracking()
            .Select(p => new { p.Id, p.Nombre, p.LineaProductiva, p.Raza, p.VigenteDesde })
            .ToListAsync(ct);

        return filas
            .Select(p => new VacunacionPlantillaCalculos.PlantillaExistente(
                p.Id, p.Nombre, p.LineaProductiva, p.Raza,
                p.VigenteDesde is { } d ? DateOnly.FromDateTime(d) : null))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<VacunacionPlantillaDetalleDto> CreateAsync(VacunacionPlantillaCreateRequest req, CancellationToken ct = default)
    {
        var (linea, raza) = ValidarCabecera(req.Nombre, req.LineaProductiva, req.Raza);
        var vigente = req.VigenteDesde?.Date;

        var duplicada = VacunacionPlantillaCalculos.MotivoPlantillaDuplicada(
            await CandidatasParaUnicidadAsync(ct), linea, raza,
            vigente is null ? null : DateOnly.FromDateTime(vigente.Value));
        if (duplicada is not null) throw new InvalidOperationException(duplicada);

        var entity = new VacunacionPlanPlantilla
        {
            CompanyId = _currentUser.CompanyId,
            PaisId = _currentUser.PaisId,
            Nombre = req.Nombre.Trim(),
            LineaProductiva = linea,
            Raza = raza,
            VigenteDesde = vigente,
            Activa = true,
            Notas = req.Notas,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
        };

        _ctx.VacunacionPlanPlantilla.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        return await MapDetalleAsync(entity, ct);
    }

    /// <inheritdoc />
    public async Task<VacunacionPlantillaDetalleDto?> UpdateAsync(int id, VacunacionPlantillaUpdateRequest req, CancellationToken ct = default)
    {
        var entity = await PlantillasDeLaEmpresa().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;

        var (linea, raza) = ValidarCabecera(req.Nombre, req.LineaProductiva, req.Raza);
        var vigente = req.VigenteDesde?.Date;

        var duplicada = VacunacionPlantillaCalculos.MotivoPlantillaDuplicada(
            await CandidatasParaUnicidadAsync(ct), linea, raza,
            vigente is null ? null : DateOnly.FromDateTime(vigente.Value),
            idEditando: id);
        if (duplicada is not null) throw new InvalidOperationException(duplicada);

        // Cambiar la línea de una plantilla que ya tiene ítems dejaría, por ejemplo, un plan de
        // Engorde programado por semana: los ítems se validaron contra la línea anterior.
        if (!string.Equals(entity.LineaProductiva, linea, StringComparison.Ordinal))
        {
            var conflictivo = await ItemsDeLaPlantilla(id).AsNoTracking()
                .Select(i => i.UnidadObjetivo)
                .Distinct()
                .ToListAsync(ct);
            foreach (var unidad in conflictivo)
            {
                var motivo = VacunacionPlantillaCalculos.MotivoUnidadNoCorrespondeALinea(linea, unidad);
                if (motivo is not null)
                    throw new InvalidOperationException(
                        $"No se puede cambiar la línea a {linea}: la plantilla tiene ítems programados por {unidad}. {motivo}");
            }
        }

        entity.Nombre = req.Nombre.Trim();
        entity.LineaProductiva = linea;
        entity.Raza = raza;
        entity.VigenteDesde = vigente;
        entity.Activa = req.Activa;
        entity.Notas = req.Notas;
        entity.UpdatedByUserId = _currentUser.UserId;
        entity.UpdatedAt = DateTime.UtcNow;

        await _ctx.SaveChangesAsync(ct);
        return await MapDetalleAsync(entity, ct);
    }

    /// <summary>
    /// <inheritdoc />
    /// <para>
    /// Soft-delete <b>en cascada y con el mismo sello</b> (patrón V9.3): la plantilla y sus ítems
    /// comparten el <c>deleted_at</c>. Que las hijas queden ocultas no puede depender de que cada
    /// consulta se acuerde de encadenar el estado de la madre — así cada fila dice por sí sola que
    /// está borrada, y el sello común permite reconocer después qué se borró junto con qué.
    /// </para>
    /// </summary>
    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await PlantillasDeLaEmpresa().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return false;

        var sello = DateTime.UtcNow;
        var items = await ItemsDeLaPlantilla(id).ToListAsync(ct);

        entity.DeletedAt = sello;
        entity.UpdatedByUserId = _currentUser.UserId;
        entity.UpdatedAt = sello;

        foreach (var i in items)
        {
            i.DeletedAt = sello;
            i.UpdatedByUserId = _currentUser.UserId;
            i.UpdatedAt = sello;
        }

        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    // ─── Ítems ────────────────────────────────────────────────────────────────

    /// <summary>Ítems vivos de la plantilla en la forma que necesita la regla de duplicados.</summary>
    private async Task<List<VacunacionPlantillaCalculos.ItemExistente>> ItemsParaUnicidadAsync(int plantillaId, CancellationToken ct) =>
        await ItemsDeLaPlantilla(plantillaId).AsNoTracking()
            .Select(i => new VacunacionPlantillaCalculos.ItemExistente(i.Id, i.ItemInventarioId, i.UnidadObjetivo, i.ValorObjetivo))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<VacunacionPlantillaItemDto> AddItemAsync(int plantillaId, VacunacionPlantillaItemCreateRequest req, CancellationToken ct = default)
    {
        var plantilla = await PlantillasDeLaEmpresa().AsNoTracking().FirstOrDefaultAsync(x => x.Id == plantillaId, ct)
            ?? throw new InvalidOperationException($"Plantilla {plantillaId} no existe o no pertenece a la empresa activa.");

        var unidad = (req.UnidadObjetivo ?? "").Trim();
        ValidarItem(plantilla.LineaProductiva, await ItemsParaUnicidadAsync(plantillaId, ct),
            req.ItemInventarioId, unidad, req.ValorObjetivo, req.RangoDiasAntes, req.RangoDiasDespues, idEditando: null);

        var vacuna = await ResolverVacunaAsync(req.ItemInventarioId, ct);

        var entity = new VacunacionPlanPlantillaItem
        {
            CompanyId = _currentUser.CompanyId,
            PlantillaId = plantillaId,
            ItemInventarioId = req.ItemInventarioId,
            UnidadObjetivo = unidad,
            ValorObjetivo = req.ValorObjetivo,
            RangoDiasAntes = req.RangoDiasAntes,
            RangoDiasDespues = req.RangoDiasDespues,
            Orden = req.Orden,
            Notas = req.Notas,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
        };

        _ctx.VacunacionPlanPlantillaItem.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        return MapItem(entity, vacuna.Nombre);
    }

    /// <inheritdoc />
    public async Task<VacunacionPlantillaItemDto?> UpdateItemAsync(
        int plantillaId, int itemId, VacunacionPlantillaItemUpdateRequest req, CancellationToken ct = default)
    {
        var plantilla = await PlantillasDeLaEmpresa().AsNoTracking().FirstOrDefaultAsync(x => x.Id == plantillaId, ct);
        if (plantilla is null) return null;

        var entity = await ItemsDeLaPlantilla(plantillaId).FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (entity is null) return null;

        var unidad = (req.UnidadObjetivo ?? "").Trim();
        ValidarItem(plantilla.LineaProductiva, await ItemsParaUnicidadAsync(plantillaId, ct),
            req.ItemInventarioId, unidad, req.ValorObjetivo, req.RangoDiasAntes, req.RangoDiasDespues, idEditando: itemId);

        var vacuna = await ResolverVacunaAsync(req.ItemInventarioId, ct);

        entity.ItemInventarioId = req.ItemInventarioId;
        entity.UnidadObjetivo = unidad;
        entity.ValorObjetivo = req.ValorObjetivo;
        entity.RangoDiasAntes = req.RangoDiasAntes;
        entity.RangoDiasDespues = req.RangoDiasDespues;
        entity.Orden = req.Orden;
        entity.Notas = req.Notas;
        entity.UpdatedByUserId = _currentUser.UserId;
        entity.UpdatedAt = DateTime.UtcNow;

        await _ctx.SaveChangesAsync(ct);
        return MapItem(entity, vacuna.Nombre);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteItemAsync(int plantillaId, int itemId, CancellationToken ct = default)
    {
        var entity = await ItemsDeLaPlantilla(plantillaId).FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (entity is null) return false;

        entity.DeletedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = _currentUser.UserId;
        entity.UpdatedAt = entity.DeletedAt;

        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}
