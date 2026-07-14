// src/ZooSanMarino.Infrastructure/Services/Migracion/Funciones/MigracionService.Estructura.cs
// Fase 1 — Estructura: Granjas, Núcleos, Galpones. Valida las filas del Excel (reporte completo,
// all-or-nothing por defecto, parcial opt-in) e inserta REUTILIZANDO los servicios existentes
// (IFarmService/INucleoService/IGalponService) dentro de una transacción, respetando el orden FK.
// No duplica reglas de negocio.
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Farms;
using ZooSanMarino.Application.DTOs.Migracion;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MigracionService
{
    // ── Cargadores de datos de referencia (para plantillas y validación) ─────
    private async Task<List<Farm>> CargarGranjasAsync(int companyId, CancellationToken ct) =>
        await _ctx.Farms.AsNoTracking()
            .Where(f => f.CompanyId == companyId && f.DeletedAt == null)
            .OrderBy(f => f.Name).ToListAsync(ct);

    private async Task<List<Nucleo>> CargarNucleosAsync(int companyId, CancellationToken ct) =>
        await _ctx.Nucleos.AsNoTracking()
            .Where(n => n.CompanyId == companyId && n.DeletedAt == null)
            .ToListAsync(ct);

    private async Task<List<Galpon>> CargarGalponesAsync(int companyId, CancellationToken ct) =>
        await _ctx.Galpones.AsNoTracking()
            .Where(g => g.CompanyId == companyId && g.DeletedAt == null)
            .ToListAsync(ct);

    private async Task<List<Departamento>> CargarDepartamentosAsync(int companyId, CancellationToken ct)
    {
        var paises = await _ctx.CompanyPaises.AsNoTracking()
            .Where(cp => cp.CompanyId == companyId).Select(cp => cp.PaisId).ToListAsync(ct);
        var q = _ctx.Departamentos.AsNoTracking();
        if (paises.Count > 0) q = q.Where(d => paises.Contains(d.PaisId));
        return await q.OrderBy(d => d.DepartamentoNombre).ToListAsync(ct);
    }

    private async Task<List<Municipio>> CargarMunicipiosAsync(IReadOnlyCollection<int> departamentoIds, CancellationToken ct) =>
        departamentoIds.Count == 0
            ? new List<Municipio>()
            : await _ctx.Municipios.AsNoTracking()
                .Where(m => departamentoIds.Contains(m.DepartamentoId))
                .OrderBy(m => m.MunicipioNombre).ToListAsync(ct);

    // Key de la lista maestra que representa las "regionales" (se crean dinámicamente por empresa).
    private const string RegionOptionKey = "region_option_key";

    // Las regionales NO viven en la tabla `regionales` (histórica/vacía): son opciones de la lista maestra
    // 'region_option_key' de la empresa. Misma fuente que el form de granjas (MasterListService.GetByKeyAsync);
    // `farms.regional_id` guarda el Id de la opción.
    private async Task<List<MasterListOption>> CargarRegionalesAsync(int companyId, CancellationToken ct) =>
        await _ctx.MasterListOptions.AsNoTracking()
            .Where(o => _ctx.MasterLists.Any(ml =>
                ml.Id == o.MasterListId &&
                ml.Key == RegionOptionKey &&
                (ml.CompanyId == companyId || ml.CompanyId == null)))
            .OrderBy(o => o.Order)
            .ToListAsync(ct);

    // ── Runner de importación (valida → dry-run corta → inserta en transacción, parcial opt-in) ─
    private async Task<MigracionResultDto> EjecutarImportacionAsync<TDto>(
        TipoMigracion tipo, bool dryRun, bool permitirParcial, string nombreArchivo,
        int total, List<MigracionErrorDto> errores, List<TDto> dtos,
        Func<TDto, Task> insertar, CancellationToken ct)
    {
        if (total == 0 && errores.Count == 0) return ResultadoVacio(tipo, dryRun);

        var hayErroresReales = errores.Any(e => e.Severidad == "Error");
        var puedeInsertarParcial = hayErroresReales && !dryRun && permitirParcial && dtos.Count > 0;

        if (hayErroresReales && !puedeInsertarParcial)
            return ResultadoConErrores(tipo, dryRun, total, errores);

        if (dryRun) return ResultadoOk(tipo, dryRun, total, errores);

        await using var tx = await _ctx.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var dto in dtos) await insertar(dto);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            var err = new List<MigracionErrorDto>(errores) { new(0, "-", null, $"Error al insertar: {ex.Message}") };
            return ResultadoFallido(tipo, total, err);
        }

        if (puedeInsertarParcial)
        {
            var filasError = errores.Where(e => e.Severidad == "Error" && e.Fila > 0).Select(e => e.Fila).Distinct().Count();
            var (capados, totalReal) = MigracionEsquemaCalculos.LimitarErrores(errores, MaxErroresReportados);
            return new MigracionResultDto(tipo.ToString(), true, total, dtos.Count, filasError, "ProcesadoParcial", dryRun, capados, 0, 0, totalReal);
        }

        return ResultadoOk(tipo, dryRun, total, errores);
    }

    // ── Núcleos ──────────────────────────────────────────────────────────────
    private async Task<MigracionResultDto> ProcesarNucleosAsync(IFormFile file, bool dryRun, bool permitirParcial, int companyId, CancellationToken ct)
    {
        const TipoMigracion tipo = TipoMigracion.Nucleos;
        var errores = new List<MigracionErrorDto>();
        using var stream = file.OpenReadStream();
        var filas = LeerDatosConEsquema(stream, MigracionEsquemas.Para(tipo), errores);
        if (errores.Any(e => e.Severidad == "Error")) return ResultadoConErrores(tipo, dryRun, filas.Count, errores);
        if (filas.Count == 0 && errores.Count == 0) return ResultadoVacio(tipo, dryRun);

        var granjaPorNombre = (await CargarGranjasAsync(companyId, ct))
            .GroupBy(g => MigracionCalculos.NormalizarClave(g.Name)).ToDictionary(g => g.Key, g => g.First().Id);
        var nucleosExistentes = (await CargarNucleosAsync(companyId, ct))
            .Select(n => $"{n.GranjaId}|{MigracionCalculos.NormalizarClave(n.NucleoId)}").ToHashSet();

        var dtos = new List<CreateNucleoDto>();
        var vistos = new HashSet<string>();

        foreach (var fila in filas)
        {
            var granjaNombre = MigracionCalculos.TextoLimpio(Celda(fila, "granja"));
            var codigo = MigracionCalculos.TextoLimpio(Celda(fila, "codigo nucleo", "codigo"));
            var nombre = MigracionCalculos.TextoLimpio(Celda(fila, "nombre"));

            if (granjaNombre is null) { errores.Add(new(fila.Numero, "Granja", null, "La granja es obligatoria.")); continue; }
            if (!granjaPorNombre.TryGetValue(MigracionCalculos.NormalizarClave(granjaNombre), out var granjaId))
            { errores.Add(new(fila.Numero, "Granja", granjaNombre, "La granja no existe en la empresa.")); continue; }
            if (codigo is null) { errores.Add(new(fila.Numero, "Código Núcleo", null, "El código de núcleo es obligatorio.")); continue; }
            if (nombre is null) { errores.Add(new(fila.Numero, "Nombre", null, "El nombre es obligatorio.")); continue; }

            var clave = $"{granjaId}|{MigracionCalculos.NormalizarClave(codigo)}";
            if (!vistos.Add(clave)) { errores.Add(new(fila.Numero, "Código Núcleo", codigo, "Núcleo duplicado en el archivo (misma granja y código).")); continue; }
            if (nucleosExistentes.Contains(clave)) { errores.Add(new(fila.Numero, "Código Núcleo", codigo, "El núcleo ya existe en la empresa.")); continue; }

            dtos.Add(new CreateNucleoDto(granjaId, codigo, nombre));
        }

        return await EjecutarImportacionAsync(tipo, dryRun, permitirParcial, file.FileName, filas.Count, errores, dtos,
            dto => _nucleoService.CreateAsync(dto), ct);
    }

    // ── Galpones ─────────────────────────────────────────────────────────────
    private async Task<MigracionResultDto> ProcesarGalponesAsync(IFormFile file, bool dryRun, bool permitirParcial, int companyId, CancellationToken ct)
    {
        const TipoMigracion tipo = TipoMigracion.Galpones;
        var errores = new List<MigracionErrorDto>();
        using var stream = file.OpenReadStream();
        var filas = LeerDatosConEsquema(stream, MigracionEsquemas.Para(tipo), errores);
        if (errores.Any(e => e.Severidad == "Error")) return ResultadoConErrores(tipo, dryRun, filas.Count, errores);
        if (filas.Count == 0 && errores.Count == 0) return ResultadoVacio(tipo, dryRun);

        var granjaPorNombre = (await CargarGranjasAsync(companyId, ct))
            .GroupBy(g => MigracionCalculos.NormalizarClave(g.Name)).ToDictionary(g => g.Key, g => g.First().Id);
        var nucleoSet = (await CargarNucleosAsync(companyId, ct))
            .Select(n => $"{n.GranjaId}|{MigracionCalculos.NormalizarClave(n.NucleoId)}").ToHashSet();
        var galponesExistentes = (await CargarGalponesAsync(companyId, ct))
            .Select(g => MigracionCalculos.NormalizarClave(g.GalponId)).ToHashSet();

        var dtos = new List<CreateGalponDto>();
        var vistosCodigo = new HashSet<string>();

        foreach (var fila in filas)
        {
            var granjaNombre = MigracionCalculos.TextoLimpio(Celda(fila, "granja"));
            var nucleoCodigo = MigracionCalculos.TextoLimpio(Celda(fila, "nucleo"));
            var galponCodigo = MigracionCalculos.TextoLimpio(Celda(fila, "codigo galpon", "codigo"));
            var nombre = MigracionCalculos.TextoLimpio(Celda(fila, "nombre"));
            var ancho = MigracionCalculos.TextoLimpio(Celda(fila, "ancho"));
            var largo = MigracionCalculos.TextoLimpio(Celda(fila, "largo"));
            var tipoGalpon = MigracionCalculos.TextoLimpio(Celda(fila, "tipo galpon", "tipo"));

            if (granjaNombre is null) { errores.Add(new(fila.Numero, "Granja", null, "La granja es obligatoria.")); continue; }
            if (!granjaPorNombre.TryGetValue(MigracionCalculos.NormalizarClave(granjaNombre), out var granjaId))
            { errores.Add(new(fila.Numero, "Granja", granjaNombre, "La granja no existe en la empresa.")); continue; }
            if (nucleoCodigo is null) { errores.Add(new(fila.Numero, "Núcleo", null, "El núcleo es obligatorio.")); continue; }
            if (!nucleoSet.Contains($"{granjaId}|{MigracionCalculos.NormalizarClave(nucleoCodigo)}"))
            { errores.Add(new(fila.Numero, "Núcleo", nucleoCodigo, "El núcleo no existe en esa granja.")); continue; }
            if (nombre is null) { errores.Add(new(fila.Numero, "Nombre", null, "El nombre es obligatorio.")); continue; }

            if (galponCodigo is not null)
            {
                var k = MigracionCalculos.NormalizarClave(galponCodigo);
                if (!vistosCodigo.Add(k)) { errores.Add(new(fila.Numero, "Código Galpón", galponCodigo, "Código de galpón duplicado en el archivo.")); continue; }
                if (galponesExistentes.Contains(k)) { errores.Add(new(fila.Numero, "Código Galpón", galponCodigo, "El código de galpón ya existe en la empresa.")); continue; }
            }

            dtos.Add(new CreateGalponDto(galponCodigo ?? string.Empty, nombre, nucleoCodigo, granjaId, ancho, largo, tipoGalpon));
        }

        return await EjecutarImportacionAsync(tipo, dryRun, permitirParcial, file.FileName, filas.Count, errores, dtos,
            dto => _galponService.CreateAsync(dto), ct);
    }

    // ── Granjas ──────────────────────────────────────────────────────────────
    private async Task<MigracionResultDto> ProcesarGranjasAsync(IFormFile file, bool dryRun, bool permitirParcial, int companyId, CancellationToken ct)
    {
        const TipoMigracion tipo = TipoMigracion.Granjas;
        var errores = new List<MigracionErrorDto>();
        using var stream = file.OpenReadStream();
        var filas = LeerDatosConEsquema(stream, MigracionEsquemas.Para(tipo), errores);
        if (errores.Any(e => e.Severidad == "Error")) return ResultadoConErrores(tipo, dryRun, filas.Count, errores);
        if (filas.Count == 0 && errores.Count == 0) return ResultadoVacio(tipo, dryRun);

        var departamentos = await CargarDepartamentosAsync(companyId, ct);
        var deptPorNombre = departamentos
            .GroupBy(d => MigracionCalculos.NormalizarClave(d.DepartamentoNombre)).ToDictionary(g => g.Key, g => g.First().DepartamentoId);
        var muniPorClave = (await CargarMunicipiosAsync(departamentos.Select(d => d.DepartamentoId).ToList(), ct))
            .GroupBy(m => $"{m.DepartamentoId}|{MigracionCalculos.NormalizarClave(m.MunicipioNombre)}").ToDictionary(g => g.Key, g => g.First().MunicipioId);
        var regionalPorNombre = (await CargarRegionalesAsync(companyId, ct))
            .Where(o => !string.IsNullOrWhiteSpace(o.Value))
            .GroupBy(o => MigracionCalculos.NormalizarClave(o.Value)).ToDictionary(g => g.Key, g => g.First().Id);
        var granjasExistentes = (await CargarGranjasAsync(companyId, ct))
            .Select(g => MigracionCalculos.NormalizarClave(g.Name)).ToHashSet();

        var dtos = new List<CreateFarmDto>();
        var vistosNombre = new HashSet<string>();

        foreach (var fila in filas)
        {
            var nombre = MigracionCalculos.TextoLimpio(Celda(fila, "nombre"));
            var deptNombre = MigracionCalculos.TextoLimpio(Celda(fila, "departamento"));
            var ciudadNombre = MigracionCalculos.TextoLimpio(Celda(fila, "ciudad", "municipio"));
            var regionalNombre = MigracionCalculos.TextoLimpio(Celda(fila, "regional"));
            var estado = MigracionCalculos.NormalizarEstado(MigracionCalculos.TextoLimpio(Celda(fila, "estado")));

            if (nombre is null) { errores.Add(new(fila.Numero, "Nombre", null, "El nombre es obligatorio.")); continue; }
            if (deptNombre is null) { errores.Add(new(fila.Numero, "Departamento", null, "El departamento es obligatorio.")); continue; }
            if (!deptPorNombre.TryGetValue(MigracionCalculos.NormalizarClave(deptNombre), out var deptId))
            { errores.Add(new(fila.Numero, "Departamento", deptNombre, "El departamento no existe (o no pertenece al país de la empresa).")); continue; }
            if (ciudadNombre is null) { errores.Add(new(fila.Numero, "Ciudad", null, "La ciudad/municipio es obligatoria.")); continue; }
            if (!muniPorClave.TryGetValue($"{deptId}|{MigracionCalculos.NormalizarClave(ciudadNombre)}", out var muniId))
            { errores.Add(new(fila.Numero, "Ciudad", ciudadNombre, "La ciudad no existe en el departamento indicado.")); continue; }

            if (regionalNombre is null) { errores.Add(new(fila.Numero, "Regional", null, "La regional es obligatoria.")); continue; }
            if (!regionalPorNombre.TryGetValue(MigracionCalculos.NormalizarClave(regionalNombre), out var regionalId))
            { errores.Add(new(fila.Numero, "Regional", regionalNombre, "La regional no existe en la empresa.")); continue; }

            var claveNombre = MigracionCalculos.NormalizarClave(nombre);
            if (!vistosNombre.Add(claveNombre)) { errores.Add(new(fila.Numero, "Nombre", nombre, "Granja duplicada en el archivo.")); continue; }
            if (granjasExistentes.Contains(claveNombre)) { errores.Add(new(fila.Numero, "Nombre", nombre, "Ya existe una granja con ese nombre en la empresa.")); continue; }

            dtos.Add(new CreateFarmDto(
                Name: nombre, CompanyId: companyId, Status: estado,
                RegionalId: regionalId, RegionalOptionId: null,
                DepartamentoId: deptId, CiudadId: muniId));
        }

        return await EjecutarImportacionAsync(tipo, dryRun, permitirParcial, file.FileName, filas.Count, errores, dtos,
            dto => _farmService.CreateAsync(dto), ct);
    }
}
