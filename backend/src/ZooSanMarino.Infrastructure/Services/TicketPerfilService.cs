using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Tickets;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Configuración de tickets: quién puede ABRIR (nivel del usuario y de sus roles) y quién ATIENDE
/// (resolutores por usuario o por rol, con alcance EMPRESA o GLOBAL).
/// </summary>
/// <remarks>
/// <para>
/// Reglas (19-sep-2026, plan <c>fase_de_desarrollo/tickets_crear_vs_atender_empresa_global_plan.md</c>):
/// <list type="bullet">
/// <item>El perfil se guarda en la empresa del USUARIO / ROL, no en la empresa activa del que edita
///   (<see cref="TicketPerfilEmpresaCalculos"/>).</item>
/// <item>Escribir exige ser admin global o <c>tickets.admin</c> de esa empresa; tocar GLOBAL, solo el
///   admin global (<see cref="TicketPerfilAutorizacionCalculos"/>).</item>
/// <item>Abrir y atender no se mezclan: el nivel de apertura no crea resolutores y asignar un rol ya no
///   copia su plantilla al usuario.</item>
/// </list>
/// </para>
/// </remarks>
public class TicketPerfilService : ITicketPerfilService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyResolver _companyResolver;

    public TicketPerfilService(ZooSanMarinoContext ctx, ICurrentUser currentUser, ICompanyResolver companyResolver)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _companyResolver = companyResolver;
    }

    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.ActiveCompanyName))
        {
            var cid = await _companyResolver.GetCompanyIdByNameAsync(_currentUser.ActiveCompanyName);
            if (cid.HasValue) return cid.Value;
        }
        return _currentUser.CompanyId;
    }

    // ─────────────── Tipos permitidos + asignables (para crear ticket) ───────────────

    public async Task<IReadOnlyList<TipoPermitidoDto>> GetTiposPermitidosAsync(CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var paisId = _currentUser.PaisId;
        var userGuid = _currentUser.UserGuid;

        // Nivel efectivo: permiso gestionar/admin, nivel de sus roles o su perfil personal (gana el mayor).
        IReadOnlyList<string?> nivelesDeRoles = Array.Empty<string?>();
        string? nivelPerfil = null;
        if (userGuid.HasValue)
        {
            nivelesDeRoles = await NivelesDeRolesDelUsuarioAsync(userGuid.Value, ct);
            nivelPerfil = await _ctx.TicketPerfilesUsuario.AsNoTracking()
                .Where(p => p.UserId == userGuid.Value && p.CompanyId == companyId && p.Activo)
                .Select(p => p.Nivel)
                .FirstOrDefaultAsync(ct);
        }
        var nivel = TicketNivelEfectivoCalculos.NivelEfectivo(_currentUser.Permissions, nivelesDeRoles, nivelPerfil);

        // Un tipo sin nadie que lo atienda en esta empresa no se ofrece (ver memoria
        // tipo-de-ticket-sin-resolutor-no-existe); el front muestra un aviso cuando la lista queda vacía.
        var result = new List<TipoPermitidoDto>();
        foreach (var tipo in NivelTicket.GetTiposPermitidos(nivel))
        {
            var asignables = await TicketAsignablesConsulta.ListarAsync(_ctx, tipo, paisId, companyId, ct);
            if (asignables.Count > 0)
                result.Add(new TipoPermitidoDto(tipo, TipoLabel(tipo), asignables));
        }
        return result;
    }

    public async Task<IReadOnlyList<AsignableDto>> GetAsignablesAsync(string tipo, int? paisId, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        return await TicketAsignablesConsulta.ListarAsync(_ctx, tipo, paisId, companyId, ct);
    }

    // ─────────────── Perfil de usuario ───────────────

    public async Task<TicketPerfilDto> GetPerfilUsuarioAsync(Guid userId, int? companyId, CancellationToken ct)
    {
        var empresa = await ResolverEmpresaUsuarioAsync(userId, companyId, ct);
        return await ArmarPerfilUsuarioAsync(userId, empresa, ct);
    }

    public async Task<TicketPerfilDto> UpsertPerfilUsuarioAsync(Guid userId, UpsertTicketPerfilRequest req, CancellationToken ct)
    {
        if (!NivelTicket.Todos.Contains(req.Nivel ?? ""))
            throw new InvalidOperationException($"Nivel inválido: {req.Nivel}. Use NORMAL o IMPLEMENTADOR.");

        var empresa = await ResolverEmpresaUsuarioAsync(userId, req.CompanyId, ct);
        var now = DateTime.UtcNow;

        // Plantilla de atención del usuario vista desde esa empresa: sus filas EMPRESA de ahí + las GLOBAL.
        var existentes = await _ctx.TicketResolutores
            .Where(r => r.UserId == userId && (r.CompanyId == empresa || r.Alcance == TicketAlcance.Global))
            .ToListAsync(ct);
        var plan = TicketResolutorAlcanceCalculos.Planificar(
            existentes.Select(r => new TicketResolutorAlcanceCalculos.Fila(r.Id, r.Tipo, r.PaisId, r.CompanyId, r.Alcance, r.Activo)).ToList(),
            PedidoValido(req.Resolutores),
            empresa);

        await ExigirPermisoAsync(empresa, esUnoMismo: _currentUser.UserGuid == userId, plan.TocaGlobal);

        var perfil = await _ctx.TicketPerfilesUsuario
            .FirstOrDefaultAsync(p => p.UserId == userId && p.CompanyId == empresa, ct);
        if (perfil == null)
        {
            _ctx.TicketPerfilesUsuario.Add(new TicketPerfilUsuario { UserId = userId, CompanyId = empresa, Nivel = req.Nivel!.ToUpperInvariant(), CreatedAt = now });
        }
        else
        {
            perfil.Nivel = req.Nivel!.ToUpperInvariant();
            perfil.Activo = true;
            perfil.UpdatedAt = now;
        }

        var porId = existentes.ToDictionary(r => r.Id);
        foreach (var c in plan.Cambios)
        {
            var fila = porId[c.Id];
            fila.Activo = c.Activo;
            fila.Alcance = c.Alcance;
            fila.UpdatedAt = now;
        }
        foreach (var a in plan.Altas)
            _ctx.TicketResolutores.Add(new TicketResolutor
            {
                UserId = userId, Tipo = a.Tipo, PaisId = a.PaisId, CompanyId = empresa,
                Alcance = a.Alcance, CreatedAt = now,
            });

        await _ctx.SaveChangesAsync(ct);
        return await ArmarPerfilUsuarioAsync(userId, empresa, ct);
    }

    private async Task<TicketPerfilDto> ArmarPerfilUsuarioAsync(Guid userId, int empresa, CancellationToken ct)
    {
        var perfil = await _ctx.TicketPerfilesUsuario.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.CompanyId == empresa, ct);

        var resolutores = await _ctx.TicketResolutores.AsNoTracking()
            .Where(r => r.UserId == userId && (r.CompanyId == empresa || r.Alcance == TicketAlcance.Global))
            .OrderBy(r => r.Id)
            .Select(r => new ResolutorItemDto(r.Id, r.Tipo, r.PaisId, r.Activo, r.Alcance, r.CompanyId))
            .ToListAsync(ct);

        var nivelPorRol = TicketNivelEfectivoCalculos.NivelDeRoles(await NivelesDeRolesDelUsuarioAsync(userId, ct));

        return new TicketPerfilDto(userId, perfil?.Nivel ?? NivelTicket.Normal, resolutores, perfil != null,
            empresa, await NombreEmpresaAsync(empresa, ct), nivelPorRol,
            TicketPerfilAutorizacionCalculos.PuedeElegirGlobal(_currentUser.EsAdminEmpresas));
    }

    // ─────────────── Configuración de rol ───────────────

    public async Task<TicketResolutorRolDto> GetPerfilRolAsync(int roleId, int? companyId, CancellationToken ct)
    {
        var empresa = await ResolverEmpresaRolAsync(roleId, companyId, ct);
        return await ArmarPerfilRolAsync(roleId, empresa, ct);
    }

    public async Task<TicketResolutorRolDto> UpsertPerfilRolAsync(int roleId, UpsertTicketResolutorRolRequest req, CancellationToken ct)
    {
        var rol = await _ctx.Roles.FirstOrDefaultAsync(r => r.Id == roleId, ct)
            ?? throw new InvalidOperationException("El rol no existe.");

        // null = no tocar; "" = el rol deja de definir el nivel; si no, tiene que ser un nivel válido.
        string? nivelNuevo = null;
        var tocaNivel = req.NivelCreacion is not null;
        if (tocaNivel)
        {
            nivelNuevo = TicketNivelEfectivoCalculos.NormalizarNivelRol(req.NivelCreacion);
            if (nivelNuevo is null && !string.IsNullOrWhiteSpace(req.NivelCreacion))
                throw new InvalidOperationException($"Nivel de apertura inválido: {req.NivelCreacion}. Use NORMAL o IMPLEMENTADOR.");
        }

        var empresa = await ResolverEmpresaRolAsync(roleId, req.CompanyId, ct);
        var now = DateTime.UtcNow;

        var existentes = await _ctx.TicketResolutorRoles
            .Where(r => r.RoleId == roleId && (r.CompanyId == empresa || r.Alcance == TicketAlcance.Global))
            .ToListAsync(ct);
        var plan = TicketResolutorAlcanceCalculos.Planificar(
            existentes.Select(r => new TicketResolutorAlcanceCalculos.Fila(r.Id, r.Tipo, r.PaisId, r.CompanyId, r.Alcance, r.Activo)).ToList(),
            PedidoValido(req.Resolutores),
            empresa);

        await ExigirPermisoAsync(empresa, esUnoMismo: false, plan.TocaGlobal);

        if (tocaNivel) rol.TicketNivelCreacion = nivelNuevo;

        var porId = existentes.ToDictionary(r => r.Id);
        foreach (var c in plan.Cambios)
        {
            var fila = porId[c.Id];
            fila.Activo = c.Activo;
            fila.Alcance = c.Alcance;
            fila.UpdatedAt = now;
        }
        foreach (var a in plan.Altas)
            _ctx.TicketResolutorRoles.Add(new TicketResolutorRol
            {
                RoleId = roleId, Tipo = a.Tipo, PaisId = a.PaisId, CompanyId = empresa,
                Alcance = a.Alcance, CreatedAt = now,
            });

        await _ctx.SaveChangesAsync(ct);
        return await ArmarPerfilRolAsync(roleId, empresa, ct);
    }

    private async Task<TicketResolutorRolDto> ArmarPerfilRolAsync(int roleId, int empresa, CancellationToken ct)
    {
        var items = await _ctx.TicketResolutorRoles.AsNoTracking()
            .Where(r => r.RoleId == roleId && (r.CompanyId == empresa || r.Alcance == TicketAlcance.Global))
            .OrderBy(r => r.Id)
            .Select(r => new ResolutorItemDto(r.Id, r.Tipo, r.PaisId, r.Activo, r.Alcance, r.CompanyId))
            .ToListAsync(ct);

        var nivel = await _ctx.Roles.AsNoTracking()
            .Where(r => r.Id == roleId)
            .Select(r => r.TicketNivelCreacion)
            .FirstOrDefaultAsync(ct);

        return new TicketResolutorRolDto(roleId, items, TicketNivelEfectivoCalculos.NormalizarNivelRol(nivel),
            empresa, await NombreEmpresaAsync(empresa, ct),
            TicketPerfilAutorizacionCalculos.PuedeElegirGlobal(_currentUser.EsAdminEmpresas));
    }

    // ─────────────── Empresa del destino + permiso ───────────────

    /// <summary>
    /// Empresa donde vive el perfil del usuario: la pedida (solo si es la activa o si la sesión es admin
    /// global) o la del usuario según <see cref="TicketPerfilEmpresaCalculos"/>.
    /// </summary>
    private async Task<int> ResolverEmpresaUsuarioAsync(Guid userId, int? pedida, CancellationToken ct)
    {
        var empresas = await _ctx.UserCompanies.AsNoTracking()
            .Where(uc => uc.UserId == userId)
            .Select(uc => uc.CompanyId)
            .ToListAsync(ct);
        return await ResolverEmpresaAsync("usuario", empresas, pedida);
    }

    /// <summary>Empresa de la plantilla del rol: la pedida (idem) o la del rol (<c>role_companies</c>).</summary>
    private async Task<int> ResolverEmpresaRolAsync(int roleId, int? pedida, CancellationToken ct)
    {
        var empresas = await _ctx.RoleCompanies.AsNoTracking()
            .Where(rc => rc.RoleId == roleId)
            .Select(rc => rc.CompanyId)
            .ToListAsync(ct);
        return await ResolverEmpresaAsync("rol", empresas, pedida);
    }

    private async Task<int> ResolverEmpresaAsync(string destino, IReadOnlyCollection<int> empresasDelDestino, int? pedida)
    {
        var activa = await GetEffectiveCompanyIdAsync();

        if (pedida is int p && p > 0)
        {
            // Otra empresa que no es la activa, o una a la que el destino no pertenece: solo el admin
            // global (así configura, explícito, que alguien atienda los tickets de una empresa ajena).
            if (!_currentUser.EsAdminEmpresas && (p != activa || !empresasDelDestino.Contains(p)))
                throw new UnauthorizedAccessException(
                    $"Solo el administrador global puede configurar los tickets de un {destino} en otra empresa.");
            return p;
        }

        return TicketPerfilEmpresaCalculos.ResolverEmpresa(activa, empresasDelDestino)
            ?? throw new InvalidOperationException(
                TicketPerfilEmpresaCalculos.MensajeSinEmpresa(destino, empresasDelDestino.Distinct().Count()));
    }

    private async Task ExigirPermisoAsync(int empresaDestino, bool esUnoMismo, bool tocaGlobal)
    {
        var decision = TicketPerfilAutorizacionCalculos.PuedeEscribir(
            _currentUser.EsAdminEmpresas, _currentUser.Permissions,
            await GetEffectiveCompanyIdAsync(), empresaDestino, esUnoMismo, tocaGlobal);
        if (!decision.Permitido)
            throw new UnauthorizedAccessException(decision.Motivo);
    }

    // ─────────────── Helpers ───────────────

    /// <summary>
    /// <c>roles.ticket_nivel_creacion</c> de todos los roles del usuario. Mismo conjunto de roles que
    /// arma los permisos de la sesión (todos sus <c>user_roles</c>, cada uno con su empresa).
    /// </summary>
    private async Task<IReadOnlyList<string?>> NivelesDeRolesDelUsuarioAsync(Guid userId, CancellationToken ct) =>
        await _ctx.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.TicketNivelCreacion)
            .ToListAsync(ct);

    private Task<string?> NombreEmpresaAsync(int companyId, CancellationToken ct) =>
        _ctx.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => (string?)c.Name)
            .FirstOrDefaultAsync(ct);

    /// <summary>Pedido de la pantalla sin tipos inválidos (se descartan, como antes).</summary>
    private static IEnumerable<TicketResolutorAlcanceCalculos.Pedido> PedidoValido(IEnumerable<ResolutorItemRequest>? items) =>
        (items ?? Enumerable.Empty<ResolutorItemRequest>())
            .Where(i => i is not null && TicketTipos.EsValido(i.Tipo))
            .Select(i => new TicketResolutorAlcanceCalculos.Pedido(i.Tipo.ToUpperInvariant(), i.PaisId, i.Alcance));

    private static string TipoLabel(string tipo) => tipo switch
    {
        "SOPORTE" => "Soporte", "DESARROLLO" => "Desarrollo",
        "REQUERIMIENTO" => "Requerimiento", "DUDAS" => "Dudas",
        _ => tipo
    };
}
