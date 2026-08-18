using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Lotes;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using FarmLiteDto = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using NucleoLiteDto = ZooSanMarino.Application.DTOs.Shared.NucleoLiteDto;
using GalponLiteDto = ZooSanMarino.Application.DTOs.Shared.GalponLiteDto;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class LotePosturaLevanteService : ILotePosturaLevanteService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;
    private readonly IUserPermissionService _userPermissionService;
    private readonly IUserFarmService _userFarmService;
    private readonly ILocationScopeResolver _scopeResolver;
    private readonly IVacunacionMaterializadorService _vacunacionMaterializador;
    private readonly IArrastreHuevosLevanteService? _arrastreHuevos;

    public LotePosturaLevanteService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        ICompanyResolver companyResolver,
        IUserPermissionService userPermissionService,
        IUserFarmService userFarmService,
        ILocationScopeResolver scopeResolver,
        IVacunacionMaterializadorService vacunacionMaterializador,
        IArrastreHuevosLevanteService? arrastreHuevos = null)
    {
        _ctx = ctx;
        _current = current;
        _companyResolver = companyResolver;
        _userPermissionService = userPermissionService;
        _userFarmService = userFarmService;
        _scopeResolver = scopeResolver;
        _vacunacionMaterializador = vacunacionMaterializador;
        _arrastreHuevos = arrastreHuevos;
    }

    /// <summary>
    /// Filtro de alcance granular (user_farms.restrict_locations + user_farm_scopes), componible en
    /// SQL. Filas con LoteId (FK a lotes) se deciden por lote permitido (precisión de lote); filas
    /// legacy sin LoteId caen al galpón/núcleo visible. Granjas no restringidas pasan intactas.
    /// <paramref name="paraDestino"/> = true lo omite (selección de DESTINO en traslados).
    /// </summary>
    private async Task<IQueryable<LotePosturaLevante>> AplicarScopeUbicacionAsync(
        IQueryable<LotePosturaLevante> q, bool paraDestino = false)
    {
        if (paraDestino) return q;
        var restringidos = await _scopeResolver.GetAllRestrictedScopesAsync();
        if (restringidos.Count == 0) return q;

        var granjasRestringidas = restringidos.Keys.ToList();
        var lotesPermitidos = restringidos.SelectMany(kv => kv.Value.LotesPermitidos).ToList();
        var galponesVisibles = restringidos.SelectMany(kv => kv.Value.GalponesVisibles).Distinct().ToList();
        var clavesNucleo = restringidos
            .SelectMany(kv => kv.Value.NucleosVisibles.Select(n => kv.Key + "|" + n))
            .ToList();

        return q.Where(l => !granjasRestringidas.Contains(l.GranjaId)
            || (l.LoteId != null && lotesPermitidos.Contains(l.LoteId.Value))
            || (l.LoteId == null && l.GalponId != null && l.GalponId != "" && galponesVisibles.Contains(l.GalponId))
            || (l.LoteId == null && (l.GalponId == null || l.GalponId == "") && l.NucleoId != null &&
                clavesNucleo.Contains(l.GranjaId.ToString() + "|" + l.NucleoId)));
    }

    private async Task<int> GetEffectiveCompanyIdAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim());
            if (byName.HasValue) return byName.Value;
        }
        return _current.CompanyId;
    }

    private async Task<bool> IsUserAdminOrAdministratorAsync(CancellationToken ct = default)
    {
        var userIdGuid = _current.UserGuid;
        if (!userIdGuid.HasValue) return false;

        var userRoles = await _ctx.UserRoles
            .AsNoTracking()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userIdGuid.Value)
            .Select(ur => ur.Role!.Name)
            .ToListAsync(ct);

        return userRoles.Any(role =>
            !string.IsNullOrWhiteSpace(role) &&
            (role.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
             role.Equals("administrador", StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<bool> IsSuperAdminAsync(CancellationToken ct = default)
    {
        return await SuperAdminLookup.EsSuperAdminAsync(_ctx, _current.UserGuid, ct);
    }

    private async Task<List<int>?> GetAllowedFarmIdsForCurrentUserAsync(CancellationToken ct = default)
    {
        var userIdGuid = _current.UserGuid;
        if (!userIdGuid.HasValue) return null;

        var accessible = await _userFarmService.GetUserAccessibleFarmsAsync(userIdGuid.Value);
        return accessible.Select(x => x.FarmId).Distinct().ToList();
    }

    private static LotePosturaProduccion CrearLoteProduccion(
        LotePosturaLevante lev, string nombre, int avesH, int avesM, DateTime now, int userId, int? huevosIniciales)
    {
        return new LotePosturaProduccion
        {
            LoteNombre = nombre,
            GranjaId = lev.GranjaId,
            NucleoId = lev.NucleoId,
            GalponId = lev.GalponId,
            Regional = lev.Regional,
            FechaEncaset = lev.FechaEncaset,
            HembrasL = lev.HembrasL,
            MachosL = lev.MachosL,
            PesoInicialH = lev.PesoInicialH,
            PesoInicialM = lev.PesoInicialM,
            UnifH = lev.UnifH,
            UnifM = lev.UnifM,
            MortCajaH = lev.MortCajaH,
            MortCajaM = lev.MortCajaM,
            Raza = lev.Raza,
            AnoTablaGenetica = lev.AnoTablaGenetica,
            Linea = lev.Linea,
            TipoLinea = lev.TipoLinea,
            CodigoGuiaGenetica = lev.CodigoGuiaGenetica,
            LineaGeneticaId = lev.LineaGeneticaId,
            Tecnico = lev.Tecnico,
            Mixtas = lev.Mixtas,
            PesoMixto = lev.PesoMixto,
            AvesEncasetadas = lev.AvesEncasetadas,
            EdadInicial = lev.EdadInicial,
            LoteErp = lev.LoteErp,
            EstadoTraslado = lev.EstadoTraslado,
            PaisId = lev.PaisId,
            PaisNombre = lev.PaisNombre,
            EmpresaNombre = lev.EmpresaNombre,
            FechaInicioProduccion = now,
            HembrasInicialesProd = avesH,
            MachosInicialesProd = avesM,
            HuevosIniciales = huevosIniciales,
            // Heredar el Lote base (padre) del levante — igual que Levante lo tiene desde su creación.
            // Sin esto, lote_postura_produccion.lote_id queda NULL y el seguimiento de producción
            // falla con 400 ("no tiene LoteId asociado", requerido por produccion_diaria).
            LoteId = lev.LoteId,
            LotePadreId = lev.LotePadreId,
            LotePosturaLevanteId = lev.LotePosturaLevanteId,
            AvesHInicial = avesH,
            AvesMInicial = avesM,
            AvesHActual = avesH,
            AvesMActual = avesM,
            EmpresaId = lev.CompanyId,
            UsuarioId = userId,
            Estado = "Produccion",
            Etapa = "Produccion",
            Edad = lev.Edad,
            EstadoCierre = "Abierta",
            CompanyId = lev.CompanyId,
            CreatedByUserId = userId,
            CreatedAt = now
        };
    }

    /// <summary>
    /// Obtiene los lotes postura levante de la empresa en sesión, filtrados por:
    /// - Company (empresa activa)
    /// - Granjas a las que el usuario tiene permiso (UserFarms + granjas por empresa)
    /// - Excluye eliminados (DeletedAt). Muestra abiertos y cerrados.
    /// </summary>
    public async Task<IEnumerable<LotePosturaLevanteDetailDto>> GetAllAsync(CancellationToken ct = default, bool paraDestino = false)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);

        var assignedCountries = await _userPermissionService.GetAssignedCountriesAsync(_current.UserId);
        var allCountriesCount = await _ctx.Set<Pais>().CountAsync(ct);
        var isAdmin = assignedCountries.Count() >= allCountriesCount ||
                     await IsUserAdminOrAdministratorAsync(ct) ||
                     await IsSuperAdminAsync(ct);

        var q = _ctx.LotePosturaLevante
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null);

        if (!isAdmin)
        {
            var allowedFarmIds = await GetAllowedFarmIdsForCurrentUserAsync(ct);
            if (allowedFarmIds != null && allowedFarmIds.Count > 0)
                q = q.Where(l => allowedFarmIds.Contains(l.GranjaId));
            else
                q = q.Where(_ => false); // Sin granjas asignadas → lista vacía
        }

        // Alcance granular núcleo/galpón/lote — aplica incluso a admin (restricción explícita
        // gana al bypass de rol). Omitido al elegir DESTINO de traslados.
        q = await AplicarScopeUbicacionAsync(q, paraDestino);

        q = q.OrderBy(l => l.LotePosturaLevanteId);
        return await ProjectToDetail(q).ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<LotePosturaLevanteDetailDto>> GetByLoteIdAsync(int loteId, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var q = _ctx.LotePosturaLevante
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.LoteId == loteId);

        // Alcance granular: acceso directo por loteId también respeta el scope (fail-closed → vacío)
        q = await AplicarScopeUbicacionAsync(q);

        q = q.OrderBy(l => l.LotePosturaLevanteId);
        return await ProjectToDetail(q).ToListAsync(ct);
    }

    // Instancia (no static) para poder resolver el Regional vía _ctx.MasterListOptions
    // dentro de la proyección (REQ-002b). Los tres callers ya son métodos de instancia.
    private IQueryable<LotePosturaLevanteDetailDto> ProjectToDetail(
        IQueryable<LotePosturaLevante> q)
    {
        return q
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Include(l => l.Galpon)
            .Select(l => new LotePosturaLevanteDetailDto(
                l.LotePosturaLevanteId ?? 0,
                l.LoteNombre,
                l.GranjaId,
                l.NucleoId,
                l.GalponId,
                // REQ-002b: Regional resoluble. En la BD lote_postura_levante.regional
                // viene como CADENA VACÍA (no NULL), por eso un simple `??` no basta:
                // cuando está vacío/NULL se traduce farms.regional_id a nombre vía
                // master_list_options (mismo patrón que FarmService: 60='Oriente',
                // 59='Occidente').
                (l.Regional == null || l.Regional == "")
                    ? _ctx.MasterListOptions
                        .Where(o => o.Id == l.Farm.RegionalId)
                        .Select(o => o.Value)
                        .FirstOrDefault()
                    : l.Regional,
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
                l.LineaGeneticaId,
                l.Tecnico,
                l.Mixtas,
                l.PesoMixto,
                l.AvesEncasetadas,
                l.EdadInicial,
                l.LoteErp,
                l.EstadoTraslado,
                l.PaisId,
                l.PaisNombre,
                l.EmpresaNombre,
                l.CompanyId,
                l.CreatedAt,
                l.LoteId,
                l.LotePadreId,
                l.LotePosturaLevantePadreId,
                l.AvesHInicial,
                l.AvesMInicial,
                l.AvesHActual,
                l.AvesMActual,
                l.EmpresaId,
                l.UsuarioId,
                l.Estado,
                l.Etapa,
                l.Edad,
                l.EstadoCierre,
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
                        l.Nucleo.NucleoNombre ?? l.Nucleo.NucleoId,
                        l.Nucleo.GranjaId
                    ),
                l.Galpon == null
                    ? null
                    : new GalponLiteDto(
                        l.Galpon.GalponId,
                        l.Galpon.GalponNombre ?? l.Galpon.GalponId,
                        l.Galpon.NucleoId ?? "",
                        l.Galpon.GranjaId
                    ),
                (int?)null, // EdadMaximaSeguimiento: solo se calcula en GetByIdAsync
                l.LevanteTrasladoIngresoHembras,
                l.LevanteTrasladoIngresoMachos,
                l.LevanteTrasladoSalidaHembras,
                l.LevanteTrasladoSalidaMachos
            ));
    }

    /// <summary>
    /// Obtiene un lote levante por ID con EdadMaximaSeguimiento (máxima edad en semanas con registros en seguimiento_diario).
    /// </summary>
    public async Task<LotePosturaLevanteDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var q = _ctx.LotePosturaLevante
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.LotePosturaLevanteId == id);
        var list = await ProjectToDetail(q).ToListAsync(ct);
        var dto = list.FirstOrDefault();
        if (dto == null) return null;

        var lpl = await _ctx.LotePosturaLevante
            .AsNoTracking()
            .Where(l => l.LotePosturaLevanteId == id && l.DeletedAt == null)
            .Select(l => new { l.FechaEncaset })
            .FirstOrDefaultAsync(ct);
        if (lpl?.FechaEncaset == null) return dto;

        var maxFecha = await _ctx.SeguimientoDiario
            .Where(s => s.TipoSeguimiento == "levante" && s.LotePosturaLevanteId == id)
            .MaxAsync(s => (DateTime?)s.Fecha, ct);
        if (!maxFecha.HasValue) return dto;

        var dias = (maxFecha.Value.Date - lpl.FechaEncaset.Value.Date).TotalDays;
        var edadMaxSemanas = (int)Math.Floor(dias / 7.0);
        if (edadMaxSemanas < 0) edadMaxSemanas = 0;

        return dto with { EdadMaximaSeguimiento = edadMaxSemanas };
    }

    /// <inheritdoc />
    public async Task<CierreLoteLevanteResumenDto?> GetResumenCierreAsync(int lotePosturaLevanteId, CancellationToken ct = default)
    {
        var lev = await LoadLevanteTrackedOrNullAsync(lotePosturaLevanteId, ct);
        if (lev is null) return null;

        var yaProd = await _ctx.LotePosturaProduccion.AsNoTracking()
            .AnyAsync(p => p.LotePosturaLevanteId == lotePosturaLevanteId && p.DeletedAt == null, ct);

        // Huevos capturados en levante (semana 14+) que se arrastrarán a producción al cerrar.
        // El modal los muestra como readonly: son el dato real, no uno digitado a mano.
        var (huevosTot, huevosInc) = _arrastreHuevos is null
            ? (0, 0)
            : await _arrastreHuevos.ObtenerTotalesParaCierreAsync(lotePosturaLevanteId, lev.LoteId, ct);

        return new CierreLoteLevanteResumenDto(
            lotePosturaLevanteId,
            lev.LoteNombre ?? "",
            lev.AvesHActual ?? 0,
            lev.AvesMActual ?? 0,
            yaProd,
            huevosTot,
            huevosInc);
    }

    /// <inheritdoc />
    public async Task<LotePosturaLevanteDetailDto?> CerrarLoteYCrearProduccionAsync(int lotePosturaLevanteId, CerrarLoteLevanteRequest request, CancellationToken ct = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ClosedByUserId))
            throw new ArgumentException("ClosedByUserId es requerido.");
        if (request.HuevosIniciales < 0)
            throw new ArgumentException("Huevos iniciales no puede ser negativo.");

        var lev = await LoadLevanteTrackedOrNullAsync(lotePosturaLevanteId, ct);
        if (lev is null) return null;

        var estado = (lev.EstadoCierre ?? "").Trim();
        if (string.Equals(estado, "Cerrado", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El lote ya está cerrado.");

        var existeProd = await _ctx.LotePosturaProduccion
            .AnyAsync(p => p.LotePosturaLevanteId == lotePosturaLevanteId && p.DeletedAt == null, ct);
        if (existeProd)
            throw new InvalidOperationException("Ya existe un lote de producción asociado a este lote de levante.");

        var dispH = Math.Max(0, lev.AvesHActual ?? 0);
        var dispM = Math.Max(0, lev.AvesMActual ?? 0);
        var avesH = request.AvesHInicialProd.HasValue ? Math.Max(0, request.AvesHInicialProd.Value) : dispH;
        var avesM = request.AvesMInicialProd.HasValue ? Math.Max(0, request.AvesMInicialProd.Value) : dispM;
        if (avesH > dispH) avesH = dispH;
        if (avesM > dispM) avesM = dispM;

        var now = request.FechaInicioProduccion.HasValue
            ? (request.FechaInicioProduccion.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(request.FechaInicioProduccion.Value, DateTimeKind.Utc)
                : request.FechaInicioProduccion.Value.ToUniversalTime())
            : DateTime.UtcNow;
        var userId = _current.UserId;
        var baseNombre = (lev.LoteNombre ?? "").Trim();
        if (string.IsNullOrEmpty(baseNombre)) baseNombre = $"Lote-{lev.LotePosturaLevanteId}";
        var nombreProduccion = $"P-{baseNombre}";

        var prod = CrearLoteProduccion(lev, nombreProduccion, avesH, avesM, now, userId, request.HuevosIniciales);
        _ctx.LotePosturaProduccion.Add(prod);

        lev.EstadoCierre = "Cerrado";
        lev.UpdatedByUserId = userId;
        lev.UpdatedAt = now;

        // El arrastre de huevos necesita el Id del LPP recién creado ⇒ dos SaveChanges dentro de UNA
        // transacción explícita: o queda el lote de producción CON sus huevos, o no queda nada.
        // (Antes era un único SaveChanges sin transacción; el arrastre no puede quedar a medias.)
        await using var tx = await _ctx.Database.BeginTransactionAsync(ct);
        try
        {
            await _ctx.SaveChangesAsync(ct);

            if (_arrastreHuevos is not null)
                await _arrastreHuevos.ArrastrarAsync(lev, prod, now, userId, ct);

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        // Plan sanitario de PRODUCCIÓN → cronograma del lote que acaba de nacer. Va DESPUÉS del commit
        // a propósito: adentro, un SaveChanges que fallara abortaría la transacción a nivel Postgres y
        // se llevaría puesta la transición entera, que es justo lo que el fail-soft quiere evitar.
        // MaterializarAlCrearLoteAsync nunca lanza y no escribe nada si la empresa no tiene plantilla.
        if (prod.LotePosturaProduccionId is { } prodId)
            await _vacunacionMaterializador.MaterializarAlCrearLoteAsync("Produccion", prodId, ct);

        return await GetByIdAsync(lotePosturaLevanteId, ct);
    }

    /// <inheritdoc />
    public async Task<ReaperturaLoteLevanteResumenDto?> GetResumenReaperturaAsync(int lotePosturaLevanteId, CancellationToken ct = default)
    {
        var lev = await LoadLevanteTrackedOrNullAsync(lotePosturaLevanteId, ct);
        if (lev is null) return null;

        var estaCerrado = CicloVidaPosturaCalculos.EstaCerrado(lev.EstadoCierre);
        var evaluacion = await EvaluarReaperturaAsync(lotePosturaLevanteId, ct);

        return new ReaperturaLoteLevanteResumenDto(
            LotePosturaLevanteId: lotePosturaLevanteId,
            LoteNombre: lev.LoteNombre ?? "",
            EstaCerrado: estaCerrado,
            PuedeReabrir: estaCerrado && evaluacion.MotivoBloqueo is null,
            MotivoBloqueo: !estaCerrado ? "El lote no está cerrado." : evaluacion.MotivoBloqueo,
            Aviso: CicloVidaPosturaCalculos.ConstruirAvisoReaperturaPermitida(evaluacion.LoteProduccionNombre),
            LotePosturaProduccionId: evaluacion.LotePosturaProduccionId,
            LoteProduccionNombre: evaluacion.LoteProduccionNombre,
            LoteProduccionCerrado: evaluacion.LoteProduccionCerrado,
            RegistrosProduccionUsuario: evaluacion.RegistrosUsuario.Count,
            RegistrosProduccionSistema: evaluacion.IdsRegistrosDeSistema.Count,
            PrimerRegistroUsuario: evaluacion.RegistrosUsuario.Count > 0 ? evaluacion.RegistrosUsuario[0].Fecha : null,
            UltimoRegistroUsuario: evaluacion.RegistrosUsuario.Count > 0 ? evaluacion.RegistrosUsuario[^1].Fecha : null);
    }

    /// <inheritdoc />
    public async Task<LotePosturaLevanteDetailDto?> AbrirLoteAsync(int lotePosturaLevanteId, AbrirLoteLevanteRequest request, CancellationToken ct = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.OpenedByUserId))
            throw new ArgumentException("OpenedByUserId es requerido.");
        var motivo = (request.Motivo ?? "").Trim();
        if (motivo.Length < 3)
            throw new ArgumentException("Indique el motivo de reapertura (mínimo 3 caracteres).");

        var lev = await LoadLevanteTrackedOrNullAsync(lotePosturaLevanteId, ct);
        if (lev is null) return null;

        var estado = (lev.EstadoCierre ?? "").Trim();
        if (!string.Equals(estado, "Cerrado", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El lote no está cerrado.");

        // Reabrir elimina el lote de producción: antes hay que asegurarse de que no se lleve por
        // delante captura del usuario. La misma evaluación que alimenta el modal del front, para que
        // la UI y la API no puedan discrepar.
        var evaluacion = await EvaluarReaperturaAsync(lotePosturaLevanteId, ct);
        if (evaluacion.MotivoBloqueo is not null)
            throw new InvalidOperationException(evaluacion.MotivoBloqueo);

        lev.EstadoCierre = "Abierto";
        lev.UpdatedByUserId = _current.UserId;
        lev.UpdatedAt = DateTime.UtcNow;

        var prod = evaluacion.Prod;
        if (prod?.LotePosturaProduccionId is { } pid)
        {
            await using var tx = await _ctx.Database.BeginTransactionAsync(ct);
            try
            {
                await EliminarDependientesLoteProduccionAsync(pid, evaluacion.IdsRegistrosDeSistema, ct);

                // Soft delete en vez de DELETE: el cierre ya filtra por DeletedAt == null (ver
                // CerrarLoteYCrearProduccionAsync), así que el próximo cierre recrea el lote sin
                // conflicto y queda el rastro de que este existió.
                prod.DeletedAt = DateTime.UtcNow;
                prod.UpdatedByUserId = _current.UserId;
                prod.UpdatedAt = DateTime.UtcNow;

                await _ctx.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
        else
        {
            await _ctx.SaveChangesAsync(ct);
        }

        return await GetByIdAsync(lotePosturaLevanteId, ct);
    }

    /// <summary>
    /// Resultado de evaluar si un levante cerrado se puede reabrir.
    /// </summary>
    /// <param name="MotivoBloqueo">null si se puede reabrir; si no, el mensaje para el usuario.</param>
    /// <param name="IdsRegistrosDeSistema">
    /// Filas de <c>seguimiento_diario_produccion</c> que generó el propio cierre y por lo tanto se
    /// pueden borrar (se regeneran al cerrar de nuevo).
    /// </param>
    private sealed record EvaluacionReapertura(
        LotePosturaProduccion? Prod,
        int? LotePosturaProduccionId,
        string? LoteProduccionNombre,
        bool LoteProduccionCerrado,
        IReadOnlyList<RegistroProduccionResumen> RegistrosUsuario,
        IReadOnlyList<int> IdsRegistrosDeSistema,
        string? MotivoBloqueo);

    /// <summary>
    /// Decide si el levante se puede reabrir, clasificando el seguimiento de producción en
    /// «lo generó el cierre» vs «lo capturó el usuario» (<see cref="CicloVidaPosturaCalculos"/>).
    /// La consulta proyecta solo las columnas que participan de la decisión.
    /// </summary>
    private async Task<EvaluacionReapertura> EvaluarReaperturaAsync(int lotePosturaLevanteId, CancellationToken ct)
    {
        var prod = await _ctx.LotePosturaProduccion
            .FirstOrDefaultAsync(p => p.LotePosturaLevanteId == lotePosturaLevanteId && p.DeletedAt == null, ct);

        if (prod?.LotePosturaProduccionId is not { } pid)
            return new EvaluacionReapertura(null, null, null, false,
                Array.Empty<RegistroProduccionResumen>(), Array.Empty<int>(), null);

        var nombre = prod.LoteNombre;

        // El lote de producción cerrado se reabre primero: si no, reabrir el levante lo eliminaría
        // saltándose su propio cierre.
        if (CicloVidaPosturaCalculos.EstaCerrado(prod.EstadoCierre))
            return new EvaluacionReapertura(prod, pid, nombre, true,
                Array.Empty<RegistroProduccionResumen>(), Array.Empty<int>(),
                CicloVidaPosturaCalculos.ConstruirMensajeBloqueoProduccionCerrada(nombre));

        // Se toman las filas del LPP y, además, las legacy que solo quedaron atadas al Lote base
        // (LotePosturaProduccionId nulo). NO se filtra solo por LoteId: otro LPP puede compartir el
        // mismo lote base y sus registros no tienen nada que ver con esta reapertura.
        var loteIdProd = prod.LoteId;
        var crudas = await _ctx.SeguimientoProduccion.AsNoTracking()
            .Where(s => s.LotePosturaProduccionId == pid
                     || (s.LotePosturaProduccionId == null && loteIdProd != null && s.LoteId == loteIdProd))
            .Select(s => new
            {
                s.Id,
                s.Fecha,
                s.TipoAlimento,
                s.ConsKgH,
                s.ConsKgM,
                s.MortalidadH,
                s.SelM,
                s.HuevoTot,
                s.Metadata
            })
            .ToListAsync(ct);

        var filas = crudas.Select(s => new RegistroProduccionResumen(
            Id: s.Id,
            Fecha: s.Fecha,
            TipoAlimento: s.TipoAlimento,
            ConsKgH: s.ConsKgH,
            ConsKgM: s.ConsKgM,
            MortalidadH: s.MortalidadH,
            SelM: s.SelM,
            HuevoTot: s.HuevoTot,
            HuevoTotArrastrado: HuevosLevanteCalculos.LeerArrastreAplicado(s.Metadata).Totales)).ToList();

        var deUsuario = CicloVidaPosturaCalculos.FiltrarRegistrosDeUsuario(filas);
        var idsUsuario = deUsuario.Select(f => f.Id).ToHashSet();
        var idsSistema = filas.Where(f => !idsUsuario.Contains(f.Id)).Select(f => f.Id).ToList();

        // Filas de la tabla unificada atadas al LPP: el cierre no las crea (escribe en
        // seguimiento_diario_produccion), así que si existen son de otro flujo y también bloquean.
        var unificadas = await _ctx.SeguimientoDiario.AsNoTracking()
            .CountAsync(s => s.LotePosturaProduccionId == pid, ct);

        string? motivo = null;
        if (deUsuario.Count > 0)
            motivo = CicloVidaPosturaCalculos.ConstruirMensajeBloqueoReapertura(nombre, deUsuario);
        else if (unificadas > 0)
            motivo =
                $"No se puede reabrir el lote de levante: el lote de producción «{nombre}» tiene " +
                $"{unificadas} registro(s) de seguimiento diario. Elimínelos desde Seguimiento Diario " +
                "de Producción y vuelva a intentarlo.";

        return new EvaluacionReapertura(prod, pid, nombre, false, deUsuario, idsSistema, motivo);
    }

    /// <summary>
    /// Borra lo que generó el cierre (las filas de sistema ya clasificadas, el espejo de huevos) y
    /// desvincula los traslados, para poder dar de baja el lote de producción al reabrir el levante.
    /// <para>
    /// Solo se borran los ids recibidos: nunca se hace un DELETE por LPP a ciegas, porque en esa
    /// tabla también viven los registros del usuario (que, si existen, ya bloquearon la reapertura
    /// antes de llegar acá).
    /// </para>
    /// </summary>
    private async Task EliminarDependientesLoteProduccionAsync(
        int lotePosturaProduccionId, IReadOnlyList<int> idsRegistrosDeSistema, CancellationToken ct)
    {
        if (idsRegistrosDeSistema.Count > 0)
        {
            await _ctx.SeguimientoProduccion
                .Where(s => idsRegistrosDeSistema.Contains(s.Id))
                .ExecuteDeleteAsync(ct);
        }

        await _ctx.EspejoHuevoProduccion
            .Where(e => e.LotePosturaProduccionId == lotePosturaProduccionId)
            .ExecuteDeleteAsync(ct);

        await _ctx.TrasladoHuevos
            .Where(t => t.LotePosturaProduccionId == lotePosturaProduccionId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.LotePosturaProduccionId, (int?)null), ct);
    }

    private async Task<LotePosturaLevante?> LoadLevanteTrackedOrNullAsync(int lotePosturaLevanteId, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var lev = await _ctx.LotePosturaLevante
            .FirstOrDefaultAsync(l =>
                l.LotePosturaLevanteId == lotePosturaLevanteId &&
                l.CompanyId == companyId &&
                l.DeletedAt == null, ct);
        if (lev is null) return null;

        if (!await IsUserAdminOrAdministratorAsync(ct) && !await IsSuperAdminAsync(ct))
        {
            var allowed = await GetAllowedFarmIdsForCurrentUserAsync(ct);
            if (allowed != null && allowed.Count > 0 && !allowed.Contains(lev.GranjaId))
                return null;
        }

        return lev;
    }

    /// <inheritdoc />
    public async Task<LetrasDisponiblesDto> GetLetrasDisponiblesAsync(
        string galponId, string loteBase, CancellationToken ct = default)
    {
        var todas = new[] { "A", "B", "C", "D", "E", "F" };

        var companyId = await GetEffectiveCompanyIdAsync(ct);

        var prefijo = loteBase.Trim().ToUpperInvariant();

        var ocupadas = await _ctx.LotePosturaLevante
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId
                     && l.GalponId == galponId
                     && l.DeletedAt == null
                     && l.LoteNombre.StartsWith(prefijo))
            .Select(l => l.LoteNombre.Substring(prefijo.Length, 1).ToUpper())
            .Distinct()
            .ToListAsync(ct);

        var reales = ocupadas
            .Where(c => todas.Contains(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var disponibles = todas
            .Where(c => !reales.Contains(c))
            .ToList();

        return new LetrasDisponiblesDto(reales, disponibles);
    }
}
