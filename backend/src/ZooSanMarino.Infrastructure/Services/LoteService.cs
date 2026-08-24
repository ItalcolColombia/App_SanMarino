// file: src/ZooSanMarino.Infrastructure/Services/LoteService.cs
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

using ZooSanMarino.Application.Calculos;       // GuiaGeneticaRequisitoCalculos (lógica pura)
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

namespace ZooSanMarino.Infrastructure.Services
{
    public partial class LoteService : AppInterfaces.ILoteService
    {
        private readonly ZooSanMarinoContext _ctx;
        private readonly AppInterfaces.ICurrentUser _current;
        private readonly AppInterfaces.ICompanyResolver _companyResolver;
        private readonly AppInterfaces.ILocationScopeResolver _scopeResolver;
        private readonly AppInterfaces.IVacunacionMaterializadorService _vacunacionMaterializador;

        public LoteService(
            ZooSanMarinoContext ctx,
            AppInterfaces.ICurrentUser current,
            AppInterfaces.ICompanyResolver companyResolver,
            AppInterfaces.ILocationScopeResolver scopeResolver,
            AppInterfaces.IVacunacionMaterializadorService vacunacionMaterializador)
        {
            _ctx = ctx;
            _current = current;
            _companyResolver = companyResolver;
            _scopeResolver = scopeResolver;
            _vacunacionMaterializador = vacunacionMaterializador;
        }

        /// <summary>
        /// Granjas asignadas DIRECTAMENTE al usuario actual (UserFarms) — mismo criterio que
        /// NucleoService/GalponService.GetAllAsync (tab Granjas). null = sin usuario en contexto.
        /// </summary>
        private async Task<List<int>?> GetAssignedFarmIdsForCurrentUserAsync()
        {
            var userIdGuid = _current.UserGuid;
            if (!userIdGuid.HasValue) return null;

            return await _ctx.UserFarms.AsNoTracking()
                .Where(uf => uf.UserId == userIdGuid.Value)
                .Select(uf => uf.FarmId)
                .Distinct()
                .ToListAsync();
        }

        /// <summary>
        /// Gate de MUTACIÓN (fix QA M1): con granja restringida, el usuario solo puede crear/editar
        /// lotes en núcleos/galpones visibles de su cierre; sin ubicación no se permite (el lote le
        /// quedaría invisible a él mismo y el read-back post-escritura fallaría). Granja no
        /// restringida ⇒ sin cambios.
        /// </summary>
        private async Task EnsureUbicacionEnScopeAsync(int granjaId, string? nucleoId, string? galponId)
        {
            var scope = await _scopeResolver.GetScopeAsync(granjaId);
            if (scope.IsGlobal) return;

            var ok = !string.IsNullOrWhiteSpace(galponId) ? scope.PermiteGalpon(galponId.Trim())
                   : !string.IsNullOrWhiteSpace(nucleoId) ? scope.PermiteNucleo(nucleoId.Trim())
                   : false;
            if (!ok)
                throw new InvalidOperationException(
                    "Tu acceso a esta granja está restringido: solo podés registrar lotes en los núcleos/galpones de tu alcance asignado.");
        }

        /// <summary>
        /// Filtro de alcance granular (user_farms.restrict_locations + user_farm_scopes), componible
        /// en SQL. Granjas no restringidas pasan intactas; en las restringidas solo quedan los lotes
        /// permitidos del cierre (lote_id es PK global ⇒ la unión entre granjas es exacta).
        /// <paramref name="paraDestino"/> = true lo omite (selección de DESTINO en traslados).
        /// </summary>
        private async Task<IQueryable<Lote>> AplicarScopeUbicacionAsync(IQueryable<Lote> q, bool paraDestino = false)
        {
            if (paraDestino) return q;
            var restringidos = await _scopeResolver.GetAllRestrictedScopesAsync();
            if (restringidos.Count == 0) return q;

            var granjasRestringidas = restringidos.Keys.ToList();
            var lotesPermitidos = restringidos.SelectMany(kv => kv.Value.LotesPermitidos).ToList();

            return q.Where(l => !granjasRestringidas.Contains(l.GranjaId) ||
                                (l.LoteId != null && lotesPermitidos.Contains(l.LoteId.Value)));
        }

        private async Task<int> GetEffectiveCompanyIdAsync()
        {
            if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
            {
                var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim());
                if (byName.HasValue) return byName.Value;
            }

            return _current.CompanyId;
        }

        // Helpers cross-concern: usados por Consulta, Crud y Traslado (partial: visibles entre
        // todos los archivos de Funciones/).

        /// <summary>
        /// Proyección consistente a LoteDetailDto con Lite DTOs.
        /// <para>
        /// Las dos señales de la fase real (levante cerrado / existe producción) se resuelven con
        /// subconsultas correlacionadas escritas <b>en línea</b>: EF Core no traduce una llamada a
        /// método propio dentro del árbol de expresión y la consulta reventaría en runtime. La fase
        /// en sí la deriva <c>LoteDetailDto.FaseActual</c>, para no duplicar la regla.
        /// </para>
        /// </summary>
        private static IQueryable<LoteDetailDto> ProjectToDetail(ZooSanMarinoContext ctx, IQueryable<Lote> q)
        {
            return q
                .Include(l => l.Farm)
                .Include(l => l.Nucleo)
                .Include(l => l.Galpon)
                .Select(l => new LoteDetailDto(
                    l.LoteId ?? 0,
                    l.LoteNombre,
                    l.LotePosturaBaseId,
                    l.GranjaId,
                    l.NucleoId,
                    l.GalponId,
                    l.Regional,
                    l.FechaEncaset,
                    l.HembrasL,
                    l.MachosL,
                    l.PesoInicialH,
                    l.PesoInicialM,
                    l.UnifH,
                    l.UnifM,
                    l.MortCajaH,
                    l.MortCajaM,
                    l.Raza,
                    l.AnoTablaGenetica,
                    l.Linea,
                    l.TipoLinea,
                    l.CodigoGuiaGenetica,
                    l.LineaGeneticaId,  // ← NUEVO: ID de la línea genética
                    l.Tecnico,
                    l.Mixtas,
                    l.PesoMixto,
                    l.AvesEncasetadas,
                    l.EdadInicial,
                    l.LoteErp,  // ← NUEVO: Código ERP del lote
                    l.EstadoTraslado,  // ← Estado de traslado
                    l.LotePadreId,
                    l.PaisId,
                    l.PaisNombre,
                    l.EmpresaNombre,
                    l.CompanyId,
                    l.CreatedByUserId,
                    l.CreatedAt,
                    l.UpdatedByUserId,
                    l.UpdatedAt,
                    new FarmLiteDto(
                        l.Farm.Id,
                        l.Farm.Name,
                        l.Farm.RegionalId,
                        l.Farm.DepartamentoId,
                        l.Farm.MunicipioId,
                        l.Farm.ClienteId,
                        l.Farm.Zona,
                        l.Farm.CertificadoGab,
                        l.Farm.Latitud,
                        l.Farm.Longitud
                    ),
                    l.Nucleo == null
                        ? null
                        : new NucleoLiteDto(
                            l.Nucleo.NucleoId,
                            l.Nucleo.NucleoNombre,
                            l.Nucleo.GranjaId
                        ),
                    l.Galpon == null
                        ? null
                        : new GalponLiteDto(
                            l.Galpon.GalponId,
                            l.Galpon.GalponNombre,
                            l.Galpon.NucleoId,
                            l.Galpon.GranjaId
                        ),
                    l.CodigoCentroCosto,
                    l.DescripcionCentroCosto,
                    ctx.LotePosturaLevante.Any(x => x.LoteId == l.LoteId
                                                 && x.DeletedAt == null
                                                 && x.EstadoCierre != null
                                                 && x.EstadoCierre.ToLower() == "cerrado"),
                    ctx.LotePosturaProduccion.Any(p => p.LoteId == l.LoteId && p.DeletedAt == null)
                ));
        }

        private static IQueryable<Lote> ApplyOrder(IQueryable<Lote> q, string? sortBy, bool desc)
        {
            Expression<Func<Lote, object>> key = (sortBy ?? string.Empty).ToLower() switch
            {
                "lote_nombre" => l => l.LoteNombre ?? string.Empty,
                "lote_id" => l => l.LoteId ?? 0,
                "fecha_encaset" => l => l.FechaEncaset ?? DateTime.MinValue,
                _ => l => l.FechaEncaset ?? DateTime.MinValue
            };
            return desc ? q.OrderByDescending(key) : q.OrderBy(key);
        }

        // Los métodos de generación manual de IDs han sido removidos
        // La base de datos ahora genera automáticamente los IDs
    }
}
