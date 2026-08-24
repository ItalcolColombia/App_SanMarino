// file: src/ZooSanMarino.Infrastructure/Services/Funciones/LoteService.Crud.cs
// Alta, edicion y baja (soft/hard) de un lote, con sus validaciones (fecha de encaset, nombre duplicado).
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

using ZooSanMarino.Application.Calculos;       // GuiaGeneticaRequisitoCalculos (logica pura)
using ZooSanMarino.Application.DTOs;           // LoteDto, Create/Update
using ZooSanMarino.Application.DTOs.Lotes;     // LoteDetailDto, LoteSearchRequest, TrasladoLoteRequestDto, TrasladoLoteResponseDto, HistorialTrasladoLoteDto
using CommonDtos = ZooSanMarino.Application.DTOs.Common;
using AppInterfaces = ZooSanMarino.Application.Interfaces;

using FarmLiteDto   = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using NucleoLiteDto = ZooSanMarino.Application.DTOs.Shared.NucleoLiteDto;
using GalponLiteDto = ZooSanMarino.Application.DTOs.Shared.GalponLiteDto;

using ZooSanMarino.Domain.Entities;
using HistorialTrasladoLote = ZooSanMarino.Domain.Entities.HistorialTrasladoLote;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class LoteService
{
    public async Task<LoteDetailDto> CreateAsync(CreateLoteDto dto)
    {
        var companyId = await GetEffectiveCompanyIdAsync();

        // REQ-011a/REQ-009c: anti-encaset-futuro (mismo patrón que ProduccionService.cs:147-150)
        ValidarFechaEncasetNoFutura(dto.FechaEncaset);

        // La base de datos generará automáticamente el loteId
        // No necesitamos generar IDs manualmente

        await EnsureFarmExists(dto.GranjaId, companyId);

        // REQ-009c: lote duplicado (mismo nombre en la misma compañía+granja+galpón, entre lotes activos)
        await EnsureLoteNombreNoDuplicadoAsync(companyId, dto.GranjaId, dto.LoteNombre, dto.GalponId, excludeLoteId: null);

        // Guía genética CONDICIONAL: si la empresa todavía no cargó su guía (0 filas vivas, en
        // guia_genetica_santa_reyes O en produccion_avicola_raw) Raza/Año son opcionales — raza
        // de texto libre — y no se verifica existencia; apenas carga la guía vuelve a regir la
        // validación de siempre. GuiaGeneticaLookup mira las dos tablas (ver su doc-comment).
        var companyTieneGuia = await GuiaGeneticaLookup.TieneGuiaAsync(_ctx, companyId);

        var errorGuia = GuiaGeneticaRequisitoCalculos.ValidarSeleccion(companyTieneGuia, dto.Raza, dto.AnoTablaGenetica);
        if (errorGuia is not null)
            throw new InvalidOperationException(errorGuia);

        if (GuiaGeneticaRequisitoCalculos.DebeVerificarExistenciaEnGuia(companyTieneGuia, dto.Raza, dto.AnoTablaGenetica))
        {
            // Validar que (Raza, Año tabla) exista en la guía genética de la compañía actual
            var razaNorm = dto.Raza!.Trim().ToLower();
            var anio = dto.AnoTablaGenetica!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var existeGuia = await GuiaGeneticaLookup.ExisteAsync(_ctx, companyId, razaNorm, anio);

            if (!existeGuia)
                throw new InvalidOperationException(
                    GuiaGeneticaRequisitoCalculos.MensajeGuiaInexistente(dto.Raza, dto.AnoTablaGenetica));
        }

        string? nucleoId = string.IsNullOrWhiteSpace(dto.NucleoId) ? null : dto.NucleoId.Trim();
        string? galponId = string.IsNullOrWhiteSpace(dto.GalponId) ? null : dto.GalponId.Trim();

        // Alcance granular (fix QA M1): validar la ubicación ANTES de persistir
        await EnsureUbicacionEnScopeAsync(dto.GranjaId, nucleoId, galponId);

        // Si viene Galpón, validamos pertenencia y, si falta, derivamos NucleoId del galpón
        if (!string.IsNullOrWhiteSpace(galponId))
        {
            var g = await _ctx.Galpones
                .AsNoTracking()
                .SingleOrDefaultAsync(x =>
                    x.GalponId == galponId &&
                    x.CompanyId == companyId);

            if (g is null)
                throw new InvalidOperationException("Galpón no existe o no pertenece a la compañía.");

            if (g.GranjaId != dto.GranjaId)
                throw new InvalidOperationException("Galpón no pertenece a la granja indicada.");

            if (!string.IsNullOrWhiteSpace(nucleoId) &&
                !string.Equals(g.NucleoId, nucleoId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Galpón no pertenece al núcleo indicado.");

            nucleoId ??= g.NucleoId;
        }

        // Si viene Núcleo, validar que existe en la granja
        if (!string.IsNullOrWhiteSpace(nucleoId))
        {
            var n = await _ctx.Nucleos
                .AsNoTracking()
                .SingleOrDefaultAsync(x =>
                    x.NucleoId == nucleoId &&
                    x.GranjaId == dto.GranjaId
                // Si Nucleo tiene CompanyId, añadir filtro CompanyId == _current.CompanyId
                );

            if (n is null)
                throw new InvalidOperationException("Núcleo no existe en la granja (o no pertenece a la compañía).");
        }

        // Fase: la que indique el DTO y, si no viene, la Opción B de siempre (>= 26 semanas
        // desde el encaset → Producción). Sin `dto.Fase` el resultado es idéntico al anterior.
        var fechaEncasetUtc = dto.FechaEncaset?.ToUniversalTime();
        var semanasDesdeEncaset = CalcularSemanasDesdeEncaset(fechaEncasetUtc, DateTime.UtcNow);
        var fase = FaseLoteCalculos.Resolver(dto.Fase, semanasDesdeEncaset);

        // Sesión: usuario, empresa y país (desde storage/headers)
        var paisNombre = (string?)null;
        if (_current.PaisId.HasValue)
        {
            var pais = await _ctx.Paises.AsNoTracking()
                .Where(p => p.PaisId == _current.PaisId.Value)
                .Select(p => new { p.PaisNombre })
                .FirstOrDefaultAsync();
            paisNombre = pais?.PaisNombre;
        }

        var ent = new Lote
        {
            LoteNombre = (dto.LoteNombre ?? string.Empty).Trim(),
            LotePosturaBaseId = dto.LotePosturaBaseId,
            GranjaId = dto.GranjaId,
            NucleoId = nucleoId,
            GalponId = galponId,

            Regional = dto.Regional,
            FechaEncaset = fechaEncasetUtc,

            HembrasL = dto.HembrasL,
            MachosL = dto.MachosL,

            PesoInicialH = dto.PesoInicialH,
            PesoInicialM = dto.PesoInicialM,
            UnifH = dto.UnifH,
            UnifM = dto.UnifM,

            MortCajaH = dto.MortCajaH,
            MortCajaM = dto.MortCajaM,

            // Sin guía cargada la raza es texto libre (se guarda con trim; vacía → null)
            Raza = GuiaGeneticaRequisitoCalculos.ResolverRazaAGuardar(companyTieneGuia, dto.Raza),
            AnoTablaGenetica = dto.AnoTablaGenetica,
            Linea = dto.Linea,
            TipoLinea = dto.TipoLinea,
            CodigoGuiaGenetica = dto.CodigoGuiaGenetica,
            LineaGeneticaId = dto.LineaGeneticaId,
            Tecnico = dto.Tecnico,

            Mixtas = dto.Mixtas,
            PesoMixto = dto.PesoMixto,
            AvesEncasetadas = dto.AvesEncasetadas,
            EdadInicial = dto.EdadInicial,
            LoteErp = dto.LoteErp,
            LotePadreId = dto.LotePadreId,

            // Códigos ERP avícolas (pass-through; visibles solo si la empresa los maneja)
            CodigoCentroCosto = dto.CodigoCentroCosto,
            DescripcionCentroCosto = dto.DescripcionCentroCosto,

            Fase = fase,

            CompanyId = companyId,
            CreatedByUserId = _current.UserId,
            CreatedAt = DateTime.UtcNow,

            PaisId = _current.PaisId,
            PaisNombre = paisNombre,
            EmpresaNombre = _current.ActiveCompanyName
        };

        // Validar que el lote padre existe y pertenece a la misma compañía
        if (dto.LotePadreId.HasValue)
        {
            var lotePadre = await _ctx.Lotes
                .AsNoTracking()
                .SingleOrDefaultAsync(x =>
                    x.LoteId == dto.LotePadreId.Value &&
                    x.CompanyId == companyId &&
                    x.DeletedAt == null);
            
            if (lotePadre is null)
                throw new InvalidOperationException("El lote padre no existe o no pertenece a la compañía.");
            
            // Validar que el lote padre no tenga un padre (evitar jerarquías de más de 2 niveles)
            if (lotePadre.LotePadreId.HasValue)
                throw new InvalidOperationException("El lote seleccionado como padre ya tiene un lote padre asignado. No se permiten jerarquías de más de 2 niveles.");
        }

        _ctx.Lotes.Add(ent);
        await _ctx.SaveChangesAsync();

        var loteIdValue = ent.LoteId ?? 0;
        if (loteIdValue > 0)
        {
            // Historial etapa Levante: aves con que inicia el lote (sin descontar nada)
            var etapaLevante = new LoteEtapaLevante
            {
                LoteId = loteIdValue,
                AvesInicioHembras = ent.HembrasL ?? 0,
                AvesInicioMachos = ent.MachosL ?? 0,
                FechaInicio = ent.FechaEncaset ?? DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            _ctx.LoteEtapaLevante.Add(etapaLevante);

            // Asegurar que LotePosturaLevante exista con la misma granja, núcleo y galpón (por si el trigger DB no creó el registro)
            var existeLpl = await _ctx.LotePosturaLevante.AnyAsync(l => l.LoteId == loteIdValue && l.DeletedAt == null);
            if (!existeLpl)
            {
                var edadInicial = ent.EdadInicial;
                if (!edadInicial.HasValue && ent.FechaEncaset.HasValue)
                {
                    var dias = (DateTime.UtcNow.Date - ent.FechaEncaset.Value.Date).TotalDays;
                    edadInicial = (int)Math.Floor(dias / 7.0);
                }
                var lpl = new LotePosturaLevante
                {
                    LoteNombre = ent.LoteNombre,
                    GranjaId = ent.GranjaId,
                    NucleoId = ent.NucleoId,
                    GalponId = ent.GalponId,
                    Regional = ent.Regional,
                    FechaEncaset = ent.FechaEncaset,
                    HembrasL = ent.HembrasL,
                    MachosL = ent.MachosL,
                    PesoInicialH = ent.PesoInicialH,
                    PesoInicialM = ent.PesoInicialM,
                    UnifH = ent.UnifH,
                    UnifM = ent.UnifM,
                    MortCajaH = ent.MortCajaH,
                    MortCajaM = ent.MortCajaM,
                    Raza = ent.Raza,
                    AnoTablaGenetica = ent.AnoTablaGenetica,
                    Linea = ent.Linea,
                    TipoLinea = ent.TipoLinea,
                    CodigoGuiaGenetica = ent.CodigoGuiaGenetica,
                    LineaGeneticaId = ent.LineaGeneticaId,
                    Tecnico = ent.Tecnico,
                    Mixtas = ent.Mixtas,
                    PesoMixto = ent.PesoMixto,
                    AvesEncasetadas = ent.AvesEncasetadas,
                    EdadInicial = ent.EdadInicial,
                    LoteErp = ent.LoteErp,
                    EstadoTraslado = ent.EstadoTraslado,
                    PaisId = ent.PaisId,
                    PaisNombre = ent.PaisNombre,
                    EmpresaNombre = ent.EmpresaNombre,
                    LoteId = loteIdValue,
                    LotePadreId = ent.LotePadreId,
                    AvesHInicial = ent.HembrasL,
                    AvesMInicial = ent.MachosL,
                    AvesHActual = ent.HembrasL,
                    AvesMActual = ent.MachosL,
                    EmpresaId = companyId,
                    UsuarioId = _current.UserId,
                    Estado = ent.Fase ?? "Levante",
                    Etapa = ent.Fase ?? "Levante",
                    Edad = edadInicial,
                    EstadoCierre = "Abierto",
                    CompanyId = companyId,
                    CreatedByUserId = _current.UserId,
                    CreatedAt = DateTime.UtcNow
                };
                _ctx.LotePosturaLevante.Add(lpl);
            }
            await _ctx.SaveChangesAsync();

            // Plan sanitario de la empresa → cronograma del lote recién encasetado. Se relee el id
            // en vez de usar el de `lpl` porque el registro de levante puede haberlo creado un
            // trigger de la BD (ver la guarda `existeLpl` de arriba).
            // Fail-soft por dentro: MaterializarAlCrearLoteAsync NUNCA lanza. Sin plantilla para el
            // lote no escribe nada, así que una empresa sin plan se comporta igual que siempre.
            var lplId = await _ctx.LotePosturaLevante.AsNoTracking()
                .Where(l => l.LoteId == loteIdValue && l.DeletedAt == null)
                .Select(l => l.LotePosturaLevanteId)
                .FirstOrDefaultAsync();
            if (lplId.HasValue)
                await _vacunacionMaterializador.MaterializarAlCrearLoteAsync("Levante", lplId.Value);
        }

        var result = await GetByIdAsync(loteIdValue);
        return result ?? throw new InvalidOperationException("No fue posible leer el lote recién creado.");
    }

    public async Task<LoteDetailDto?> UpdateAsync(UpdateLoteDto dto)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var ent = await _ctx.Lotes
            .SingleOrDefaultAsync(x =>
                x.LoteId == dto.LoteId &&
                x.CompanyId == companyId &&
                x.DeletedAt == null);

        if (ent is null) return null;

        // Alcance granular (fix QA M1): no editar un lote fuera del cierre del usuario
        if (ent.LoteId is int loteIdActual && !await _scopeResolver.PermiteLoteAsync(loteIdActual))
            throw new InvalidOperationException(
                "Tu acceso a esta granja está restringido: el lote está fuera de tu alcance asignado.");

        // REQ-011a/REQ-009c: anti-encaset-futuro (mismo patrón que ProduccionService.cs:147-150)
        ValidarFechaEncasetNoFutura(dto.FechaEncaset);

        await EnsureFarmExists(dto.GranjaId, companyId);

        // REQ-009c: lote duplicado (mismo nombre en la misma compañía+granja+galpón, entre lotes activos; excluye el propio lote)
        await EnsureLoteNombreNoDuplicadoAsync(companyId, dto.GranjaId, dto.LoteNombre, dto.GalponId, excludeLoteId: dto.LoteId);

        // Guía genética CONDICIONAL (mismo criterio que CreateAsync): sin guía cargada en la
        // empresa (en ninguna de las dos tablas), Raza/Año son opcionales (raza libre) y no se
        // verifica existencia.
        var companyTieneGuia = await GuiaGeneticaLookup.TieneGuiaAsync(_ctx, companyId);

        var errorGuia = GuiaGeneticaRequisitoCalculos.ValidarSeleccion(companyTieneGuia, dto.Raza, dto.AnoTablaGenetica);
        if (errorGuia is not null)
            throw new InvalidOperationException(errorGuia);

        if (GuiaGeneticaRequisitoCalculos.DebeVerificarExistenciaEnGuia(companyTieneGuia, dto.Raza, dto.AnoTablaGenetica))
        {
            // Validar que (Raza, Año tabla) exista en la guía genética de la compañía actual
            var razaNorm = dto.Raza!.Trim().ToLower();
            var anio = dto.AnoTablaGenetica!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var existeGuia = await GuiaGeneticaLookup.ExisteAsync(_ctx, companyId, razaNorm, anio);

            if (!existeGuia)
                throw new InvalidOperationException(
                    GuiaGeneticaRequisitoCalculos.MensajeGuiaInexistente(dto.Raza, dto.AnoTablaGenetica));
        }

        string? nucleoId = string.IsNullOrWhiteSpace(dto.NucleoId) ? null : dto.NucleoId.Trim();
        string? galponId = string.IsNullOrWhiteSpace(dto.GalponId) ? null : dto.GalponId.Trim();

        // Alcance granular (fix QA M1): la ubicación destino del update también debe ser visible
        await EnsureUbicacionEnScopeAsync(dto.GranjaId, nucleoId, galponId);

        if (!string.IsNullOrWhiteSpace(galponId))
        {
            var g = await _ctx.Galpones
                .AsNoTracking()
                .SingleOrDefaultAsync(x =>
                    x.GalponId == galponId &&
                    x.CompanyId == companyId);

            if (g is null)
                throw new InvalidOperationException("Galpón no existe o no pertenece a la compañía.");

            if (g.GranjaId != dto.GranjaId)
                throw new InvalidOperationException("Galpón no pertenece a la granja indicada.");

            if (!string.IsNullOrWhiteSpace(nucleoId) &&
                !string.Equals(g.NucleoId, nucleoId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Galpón no pertenece al núcleo indicado.");
        }

        if (!string.IsNullOrWhiteSpace(nucleoId))
        {
            var n = await _ctx.Nucleos
                .AsNoTracking()
                .SingleOrDefaultAsync(x =>
                    x.NucleoId == nucleoId &&
                    x.GranjaId == dto.GranjaId
                // Si Nucleo tiene CompanyId, añadir filtro CompanyId == _current.CompanyId
                );

            if (n is null)
                throw new InvalidOperationException("Núcleo no existe en la granja (o no pertenece a la compañía).");
        }

        // Mutación (fechas en UTC y decimales directos)
        ent.LoteNombre = (dto.LoteNombre ?? string.Empty).Trim();
        ent.GranjaId = dto.GranjaId;
        ent.NucleoId = nucleoId ?? ent.NucleoId;
        ent.GalponId = galponId ?? ent.GalponId;
        ent.Regional = dto.Regional;
        ent.FechaEncaset = dto.FechaEncaset?.ToUniversalTime();

        // Encasetamiento: escribe hembras_l/machos_l y propaga el DELTA a las copias que la
        // edición dejaba atrás (lote_etapa_levante y lote_postura_produccion). Ver el partial
        // Funciones/LoteService.AjusteEncasetamiento.cs.
        await AplicarAjusteEncasetamientoAsync(ent, dto);

        ent.PesoInicialH = dto.PesoInicialH;
        ent.PesoInicialM = dto.PesoInicialM;
        ent.UnifH = dto.UnifH;
        ent.UnifM = dto.UnifM;

        ent.MortCajaH = dto.MortCajaH;
        ent.MortCajaM = dto.MortCajaM;

        // Sin guía cargada la raza es texto libre (se guarda con trim; vacía → null)
        ent.Raza = GuiaGeneticaRequisitoCalculos.ResolverRazaAGuardar(companyTieneGuia, dto.Raza);
        ent.AnoTablaGenetica = dto.AnoTablaGenetica;
        ent.Linea = dto.Linea;
        ent.TipoLinea = dto.TipoLinea;
        ent.CodigoGuiaGenetica = dto.CodigoGuiaGenetica;
        ent.LineaGeneticaId = dto.LineaGeneticaId;  // ← NUEVO: ID de la línea genética
        ent.Tecnico = dto.Tecnico;

        ent.Mixtas = dto.Mixtas;
        ent.PesoMixto = dto.PesoMixto;
        ent.AvesEncasetadas = dto.AvesEncasetadas;
        ent.EdadInicial = dto.EdadInicial;
        ent.LoteErp = dto.LoteErp;  // ← NUEVO: Código ERP del lote
        ent.LotePadreId = dto.LotePadreId;  // ← NUEVO: ID del lote padre
        ent.LotePosturaBaseId = dto.LotePosturaBaseId;

        // Fase: solo se toca si el DTO la trae. Vacía ⇒ se conserva la que ya tenía el lote
        // (la edición nunca la recalculaba, y seguir sin recalcularla evita que editar el
        // técnico de un lote de levante lo mande a producción al cruzar la semana 26).
        if (FaseLoteCalculos.NormalizarFaseIndicada(dto.Fase) is string faseIndicada)
            ent.Fase = faseIndicada;

        // Códigos ERP avícolas (pass-through)
        ent.CodigoCentroCosto = dto.CodigoCentroCosto;
        ent.DescripcionCentroCosto = dto.DescripcionCentroCosto;

        // Validar que el lote padre existe y pertenece a la misma compañía
        if (dto.LotePadreId.HasValue)
        {
            var lotePadre = await _ctx.Lotes
                .AsNoTracking()
                .SingleOrDefaultAsync(x =>
                    x.LoteId == dto.LotePadreId.Value &&
                    x.CompanyId == companyId &&
                    x.DeletedAt == null);
            
            if (lotePadre is null)
                throw new InvalidOperationException("El lote padre no existe o no pertenece a la compañía.");
            
            // Evitar referencias circulares
            if (dto.LotePadreId.Value == dto.LoteId)
                throw new InvalidOperationException("Un lote no puede ser su propio padre.");
            
            // Validar que el lote padre no tenga un padre (evitar jerarquías de más de 2 niveles)
            if (lotePadre.LotePadreId.HasValue)
                throw new InvalidOperationException("El lote seleccionado como padre ya tiene un lote padre asignado. No se permiten jerarquías de más de 2 niveles.");
            
            // Validar que no se cree un ciclo: verificar que el lote padre no sea descendiente del lote actual
            // Solo validar si estamos actualizando (no creando)
            if (dto.LoteId > 0)
            {
                var esDescendiente = await EsDescendienteAsync(dto.LoteId, dto.LotePadreId.Value);
                if (esDescendiente)
                    throw new InvalidOperationException("No se puede asignar un lote hijo como padre. Esto crearía una referencia circular.");
            }
        }

        // Actualizar datos de sesión (igual que al crear): empresa, país, usuario que actualiza
        ent.CompanyId = companyId;
        ent.PaisId = _current.PaisId;
        ent.EmpresaNombre = _current.ActiveCompanyName;
        if (_current.PaisId.HasValue)
        {
            var pais = await _ctx.Paises.AsNoTracking()
                .Where(p => p.PaisId == _current.PaisId.Value)
                .Select(p => new { p.PaisNombre })
                .FirstOrDefaultAsync();
            ent.PaisNombre = pais?.PaisNombre;
        }
        else
            ent.PaisNombre = null;

        ent.UpdatedByUserId = _current.UserId;
        ent.UpdatedAt = DateTime.UtcNow;

        // El espejo de levante se sincroniza por trigger, pero el de producción no: si el lote
        // recibe núcleo/galpón después de existir su producción (p. ej. lotes sembrados sin
        // ubicación), el filtro por galpón del seguimiento de producción nunca lo encontraría.
        // Relleno solo-si-vacío para no pisar ubicaciones puestas a mano en la producción.
        if (ent.LoteId.HasValue && (ent.NucleoId != null || ent.GalponId != null))
        {
            var lppAbiertos = await _ctx.LotePosturaProduccion
                .Where(p => p.LoteId == ent.LoteId.Value &&
                            p.DeletedAt == null &&
                            p.EstadoCierre == "Abierta" &&
                            (p.NucleoId == null || p.GalponId == null))
                .ToListAsync();
            foreach (var lpp in lppAbiertos)
            {
                lpp.NucleoId ??= ent.NucleoId;
                lpp.GalponId ??= ent.GalponId;
            }
        }

        await _ctx.SaveChangesAsync();
        return await GetByIdAsync(ent.LoteId ?? 0);
    }

    public async Task<bool> DeleteAsync(int loteId)
    {
        // Alcance granular (fix QA M1): no borrar lo que está fuera del cierre (fail-closed → 404)
        if (!await _scopeResolver.PermiteLoteAsync(loteId)) return false;

        var companyId = await GetEffectiveCompanyIdAsync();
        var ent = await _ctx.Lotes
            .SingleOrDefaultAsync(x => x.LoteId == loteId && x.CompanyId == companyId);
        if (ent is null || ent.DeletedAt != null) return false;

        ent.DeletedAt = DateTime.UtcNow;
        ent.UpdatedByUserId = _current.UserId;
        ent.UpdatedAt = DateTime.UtcNow;

        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> HardDeleteAsync(int loteId)
    {
        // Alcance granular (fix QA M1): fail-closed → 404
        if (!await _scopeResolver.PermiteLoteAsync(loteId)) return false;

        var companyId = await GetEffectiveCompanyIdAsync();
        var ent = await _ctx.Lotes
            .SingleOrDefaultAsync(x => x.LoteId == loteId && x.CompanyId == companyId);
        if (ent is null) return false;

        _ctx.Lotes.Remove(ent);
        await _ctx.SaveChangesAsync();
        return true;
    }

    private async Task EnsureFarmExists(int granjaId, int companyId)
    {
        var exists = await _ctx.Farms
            .AsNoTracking()
            .AnyAsync(f => f.Id == granjaId && f.CompanyId == companyId);
        if (!exists) throw new InvalidOperationException("Granja no existe o no pertenece a la compañía.");
    }

    /// <summary>
    /// REQ-011a/REQ-009c: rechaza fecha de encasetamiento futura (mismo patrón que
    /// ProduccionService.cs:147-150, que valida FechaInicio de producción). Previene el bug que
    /// generó el lote duplicado "A374A" (id 116) con encaset 2026-10-14 (un año en el futuro),
    /// que colapsa Semana/Edad y todos los cálculos derivados (indicadores, liquidación).
    /// </summary>
    private static void ValidarFechaEncasetNoFutura(DateTime? fechaEncaset)
    {
        if (fechaEncaset?.Date > DateTime.UtcNow.Date)
            throw new InvalidOperationException("La fecha de encasetamiento no puede ser futura.");
    }

    /// <summary>
    /// REQ-009c: rechaza nombre de lote duplicado entre lotes activos (DeletedAt == null) dentro de
    /// compañía + granja + <b>galpón</b>. <c>excludeLoteId</c> permite que Update no se auto-reporte
    /// como duplicado.
    ///
    /// <para><b>El galpón entró después (ago-2026).</b> La guarda nació acotada a compañía+granja y
    /// eso rechazaba una operación legítima: un mismo nombre de sublote SÍ puede repetirse en
    /// galpones distintos de la misma granja — es el patrón vivo en producción (A374A en G0326 y
    /// G0324 de LA ESMERALDA; A374B en G0325 y G0323; LOTE 235A en dos galpones de la empresa 4).
    /// El selector de letra (<c>LotePosturaLevanteService.GetLetrasDisponiblesAsync</c>) siempre
    /// trabajó por galpón: era esta guarda la que quedó fuera de fase con él y ofrecía una letra
    /// que después el guardado rechazaba.</para>
    ///
    /// <para>La consulta trae los homónimos de la granja (conjunto mínimo: mismo nombre exacto) y la
    /// decisión —incluido el caso «lote sin galpón», que forma su propio grupo— la resuelve
    /// <see cref="LoteNombreDuplicadoCalculos"/>, que es pura y está cubierta por tests.</para>
    /// </summary>
    private async Task EnsureLoteNombreNoDuplicadoAsync(int companyId, int granjaId, string? loteNombre, string? galponId, int? excludeLoteId)
    {
        var nombreNorm = LoteNombreDuplicadoCalculos.NormalizarNombre(loteNombre);
        if (nombreNorm.Length == 0) return;

        var galponesHomonimos = await _ctx.Lotes
            .AsNoTracking()
            .Where(l =>
                l.CompanyId == companyId &&
                l.GranjaId == granjaId &&
                l.DeletedAt == null &&
                (!excludeLoteId.HasValue || l.LoteId != excludeLoteId.Value) &&
                l.LoteNombre != null &&
                l.LoteNombre.ToLower() == nombreNorm.ToLower())
            .Select(l => l.GalponId)
            .ToListAsync();

        if (LoteNombreDuplicadoCalculos.HayDuplicado(galponId, galponesHomonimos))
            throw new InvalidOperationException(LoteNombreDuplicadoCalculos.MensajeDuplicado(nombreNorm, galponId));
    }

    /// <summary>Calcula la semana de edad desde fecha encaset hasta la fecha de referencia. Semana 1 = días 0-6, 2 = 7-13, etc. >= 26 → Producción.</summary>
    private static int CalcularSemanasDesdeEncaset(DateTime? fechaEncaset, DateTime fechaReferencia)
    {
        if (!fechaEncaset.HasValue) return 0;
        var dias = (fechaReferencia.Date - fechaEncaset.Value.Date).Days;
        if (dias < 0) return 0;
        return Math.Max(0, (dias / 7) + 1);
    }
}
