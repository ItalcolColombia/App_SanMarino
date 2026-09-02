// Dashboard/DashboardService.cs — archivo ANCLA del servicio.
//
// Acá viven usings, campos, ctor, la interfaz y los helpers de ALCANCE que comparten todos los
// paneles. Cada panel agrega su propio `partial` en Funciones/.
//
// Namespace PLANO (ZooSanMarino.Infrastructure.Services) aunque el archivo esté en subcarpeta: es la
// convención del repo y evita romper DI y referencias.

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Dashboard;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Datos del dashboard, recortados por los tres ejes: <b>empresa</b>, <b>usuario</b> y
/// <b>módulo/permiso</b>.
///
/// <para><b>Fail-closed por construcción.</b> Todo arranca en <see cref="ResolverAlcanceAsync"/>: si
/// el usuario no tiene granjas visibles en la empresa activa, cada panel devuelve su forma vacía. No
/// existe un camino que, ante la duda, devuelva la empresa entera — que es exactamente lo que hacía
/// el controller anterior, cuando ignoraba los filtros que el front mandaba.</para>
///
/// <para><b>Por qué SQL parametrizado y no LINQ para las series.</b> Las series diarias se agrupan
/// por día sobre columnas <c>timestamptz</c>, y <c>date_trunc</c> usa la zona de la SESIÓN: en LINQ
/// no se puede fijar. Acá se escribe <c>(fecha AT TIME ZONE 'UTC')::date</c>, que es la fecha
/// intencional (las fechas puras del repo se anclan a mediodía UTC). Además el resultado vuelve como
/// JSON en una sola columna, lo que esquiva los dos traspiés conocidos de <c>SqlQueryRaw</c> — el
/// mapeo snake_case y los dígitos en el nombre de la propiedad.</para>
/// </summary>
public partial class DashboardService : IDashboardService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly ILocationScopeResolver _scope;
    private readonly IRoleCompositeService _roles;

    public DashboardService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        ILocationScopeResolver scope,
        IRoleCompositeService roles)
    {
        _ctx = ctx;
        _current = current;
        _scope = scope;
        _roles = roles;
    }

    private static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Una lista que puede venir nula del JSON, como lista de sólo lectura nunca nula. El
    /// <c>?? Array.Empty&lt;T&gt;()</c> directo no compila: <c>List&lt;T&gt;</c> y <c>T[]</c> no
    /// unifican en la inferencia del operador.
    /// </summary>
    private static IReadOnlyList<T> OVacia<T>(List<T>? lista) => lista ?? (IReadOnlyList<T>)Array.Empty<T>();

    // ─────────────────────────────────────────────────────── alcance compartido

    /// <summary>
    /// Todo lo que hace falta para recortar una consulta: empresa, granjas visibles y el cierre de
    /// ubicación aplanado. <see cref="Vacio"/> ⇒ el usuario no ve nada.
    /// </summary>
    private readonly record struct Alcance(
        int CompanyId,
        Guid UserGuid,
        IReadOnlyList<int> Granjas,
        UserLocationScopeCalculos.ScopeSqlParams Cierre)
    {
        public bool Vacio => Granjas.Count == 0;
        public bool Restringido => Cierre.FarmIds.Length > 0;
    }

    /// <summary>
    /// Resuelve el alcance del usuario actual: granjas de la empresa ACTIVA que tiene asignadas en
    /// <c>user_farms</c>, más su cierre de ubicación.
    ///
    /// <para>Es el mismo criterio de <c>fn_vacunacion_filter_data</c>. La empresa sale de
    /// <see cref="ICurrentUser.CompanyId"/> —nunca de un parámetro del cliente— y el usuario de su
    /// <c>UserGuid</c>: sin guid no hay granjas, porque no hay a quién asignarle ninguna.</para>
    /// </summary>
    private async Task<Alcance> ResolverAlcanceAsync(CancellationToken ct)
    {
        var userGuid = _current.UserGuid;
        var companyId = _current.CompanyId;

        if (userGuid is null || userGuid == Guid.Empty || companyId <= 0)
            return new Alcance(companyId, Guid.Empty, Array.Empty<int>(),
                UserLocationScopeCalculos.ScopeSqlParams.Vacio);

        var granjas = await _ctx.Farms
            .AsNoTracking()
            .Where(f => f.CompanyId == companyId && f.DeletedAt == null)
            .Where(f => _ctx.UserFarms.Any(uf => uf.FarmId == f.Id && uf.UserId == userGuid.Value))
            .Select(f => f.Id)
            .ToListAsync(ct);

        // Diccionario vacío ⇒ ScopeSqlParams.Vacio ⇒ ninguna consulta filtra de más: es el caso
        // común (usuario sin restricción) y cuesta cero. El cierre NO se reimplementa acá — lo
        // calcula UserLocationScopeCalculos.ComputeScope vía el resolver, cacheado por request.
        var restringidos = await _scope.GetRestrictedScopesAsync(granjas);
        var cierre = UserLocationScopeCalculos.AplanarParaSql(restringidos);

        return new Alcance(companyId, userGuid.Value, granjas, cierre);
    }

    /// <summary>
    /// Parámetros Npgsql del alcance, con los nombres que usan los fragmentos SQL de los paneles.
    /// Van SIEMPRE los cinco: un fragmento que se olvide uno debe fallar, no ver toda la empresa.
    /// </summary>
    private static NpgsqlParameter[] ParametrosAlcance(Alcance a) => new[]
    {
        new NpgsqlParameter("p_company_id", NpgsqlDbType.Integer) { Value = a.CompanyId },
        new NpgsqlParameter("p_granjas", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = a.Granjas.ToArray() },
        new NpgsqlParameter("p_scope_farm_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = a.Cierre.FarmIds },
        new NpgsqlParameter("p_scope_nucleos", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = a.Cierre.Nucleos },
        new NpgsqlParameter("p_scope_galpones", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = a.Cierre.Galpones },
        new NpgsqlParameter("p_scope_lotes", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = a.Cierre.Lotes }
    };

    /// <summary>
    /// Predicado SQL del alcance de ubicación, para pegar en un <c>WHERE</c>. El alias de la tabla se
    /// pasa por parámetro; las columnas esperadas son <c>granja_id</c>, <c>nucleo_id</c>,
    /// <c>galpon_id</c> y, si la línea la tiene, la FK a <c>lotes</c>.
    ///
    /// <para>🔴 Es una <b>cascada con prioridad</b>, no un OR, y espeja línea por línea a
    /// <see cref="UserLocationScopeCalculos.PermiteUbicacion"/> (su dueña) y al <c>CASE</c> de
    /// <c>fn_vacunacion_filter_data</c>: con lote de la tabla <c>lotes</c> manda el nivel LOTE
    /// <b>y sólo ese</b> —el grant de lote es más fino que el de su galpón—; sin lote decide el
    /// galpón; sin galpón, el núcleo; sin ninguno, no se ve. Un OR sería MÁS PERMISIVO: dejaría
    /// entrar un lote excluido a propósito del grant fino sólo porque su galpón está concedido. No
    /// hay nada que compensar, porque <c>ComputeScope</c> ya bajó el cierre hacia abajo.</para>
    /// </summary>
    /// <param name="alias">Alias de la tabla en la consulta.</param>
    /// <param name="columnaLoteTabla">
    /// Columna con la FK a <c>lotes</c>, o <c>null</c> si la línea no la tiene. <b>Engorde va con
    /// <c>null</c>:</b> <c>lote_ave_engorde</c> no tiene FK a <c>lotes</c> y se gobierna por
    /// galpón/núcleo — limitación conocida del alcance granular, documentada, no un olvido. Cruzar su
    /// id contra el conjunto de lotes compararía dos espacios de id distintos.
    /// </param>
    private static string PredicadoAlcance(string alias, string? columnaLoteTabla)
    {
        var ramaLote = columnaLoteTabla is null
            ? string.Empty
            : $@"
            WHEN {alias}.{columnaLoteTabla} IS NOT NULL
                THEN {alias}.{columnaLoteTabla} = ANY(@p_scope_lotes)";

        return $@"
    {alias}.granja_id = ANY(@p_granjas)
    AND (
        NOT ({alias}.granja_id = ANY(@p_scope_farm_ids))
        OR CASE{ramaLote}
             WHEN COALESCE({alias}.galpon_id, '') <> ''
                 THEN {alias}.galpon_id = ANY(@p_scope_galpones)
             WHEN COALESCE({alias}.nucleo_id, '') <> ''
                 THEN ({alias}.granja_id::text || '|' || {alias}.nucleo_id)
                      = ANY(@p_scope_nucleos)
             ELSE false
           END
    )";
    }

    /// <summary>
    /// Corre una consulta que devuelve UN jsonb y lo deserializa. Devuelve <c>default</c> si no vino
    /// nada — el llamador decide cuál es su forma vacía.
    ///
    /// <para>🔴 <b>El SQL no puede llevar llaves literales.</b> <c>SqlQueryRaw</c> pasa la cadena por
    /// <c>String.Format</c> (así resuelve los <c>{0}</c> posicionales), de modo que un
    /// <c>'{}'::int[]</c> revienta con <i>«Input string was not in a correct format»</i> antes de
    /// tocar la base. Por eso los <c>COALESCE(p_scope_*, '{}'::int[])</c> del idiom de vacunación
    /// NO están acá: <see cref="ParametrosAlcance"/> manda siempre arrays no nulos —vacíos cuando no
    /// hay cierre— y <c>x = ANY(array_vacío)</c> ya es <c>false</c>. Si alguna vez hiciera falta una
    /// llave literal, hay que duplicarla (<c>{{</c>) para que <c>String.Format</c> la deje pasar.</para>
    /// </summary>
    private async Task<T?> ConsultarJsonAsync<T>(string sql, NpgsqlParameter[] parametros, CancellationToken ct)
    {
        var filas = await _ctx.Database
            .SqlQueryRaw<string>(sql, parametros)
            .ToListAsync(ct);

        var json = filas.FirstOrDefault();
        return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, OpcionesJson);
    }

    /// <summary>
    /// Sanea la ventana de fechas que llega del cliente: sin fechas, los últimos 30 días; invertida,
    /// se da vuelta; y se topea a un año para que nadie pida una serie de 4.000 puntos.
    /// </summary>
    private static PeriodoDashboard SanearPeriodo(DateOnly? desde, DateOnly? hasta)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var fin = hasta ?? hoy;
        var ini = desde ?? fin.AddDays(-29);

        if (ini > fin) (ini, fin) = (fin, ini);
        if (fin.DayNumber - ini.DayNumber > 365) ini = fin.AddDays(-365);

        return new PeriodoDashboard(ini, fin);
    }

    /// <summary>Los dos parámetros de fecha, con los nombres que usan los fragmentos SQL.</summary>
    private static NpgsqlParameter[] ParametrosPeriodo(PeriodoDashboard p) => new[]
    {
        new NpgsqlParameter("p_desde", NpgsqlDbType.Date) { Value = p.Desde },
        new NpgsqlParameter("p_hasta", NpgsqlDbType.Date) { Value = p.Hasta }
    };

    // ─────────────────────────────────────────────────────── menú del usuario

    /// <summary>
    /// ¿El usuario tiene alguno de estos módulos en su menú de la empresa activa?
    ///
    /// <para>El menú lo resuelve <c>fn_menu_usuario</c> a través de
    /// <see cref="IRoleCompositeService.Menus_GetForUserAsync"/> — la MISMA función que arma el menú
    /// del sidebar. No se reimplementa el cruce de <c>role_menus</c> con <c>company_menus</c>: si
    /// hubiera dos versiones de esa regla, el dashboard mostraría datos de un módulo que el usuario
    /// no ve en pantalla.</para>
    /// </summary>
    private async Task<bool> TieneModuloAsync(IEnumerable<string> routesModulo, CancellationToken ct)
    {
        var routes = await GetRoutesMenuAsync(ct);
        return DashboardCalculos.TieneAlgunModulo(routes, routesModulo);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetRoutesMenuAsync(CancellationToken ct = default)
    {
        var userGuid = _current.UserGuid;
        if (userGuid is null || userGuid == Guid.Empty) return Array.Empty<string>();

        var companyId = _current.CompanyId > 0 ? _current.CompanyId : (int?)null;
        var arbol = await _roles.Menus_GetForUserAsync(userGuid.Value, companyId);

        var routes = new List<string>();
        AplanarRoutes(arbol, routes);
        return routes;
    }

    /// <summary>Recorre el árbol y junta las routes normalizadas de todos los niveles.</summary>
    private static void AplanarRoutes(IEnumerable<MenuItemDto>? nodos, List<string> acumulador)
    {
        if (nodos is null) return;

        foreach (var nodo in nodos)
        {
            if (nodo is null) continue;

            // Los nodos de agrupación (Configuración, Reportes…) no tienen route: son contenedores.
            var route = DashboardCalculos.NormalizarRoute(nodo.Route);
            if (route is not null) acumulador.Add(route);

            AplanarRoutes(nodo.Children, acumulador);
        }
    }
}
