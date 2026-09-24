// Tests LIVIANOS del motor de flujos de validación, sobre EF Core InMemory (sin Postgres real).
//
// ALCANCE: cubren configuración/publicación/preflight, resolución de modo, ciclo de vida de
// instancias (crear/consultar/cancelar/mis-pendientes) y novedades del Home. NO cubren
// AprobarAsync/DevolverAsync/CorregirYReenviarAsync/MarcarNovedadLeidaAsync: usan
// `FromSqlInterpolated("... FOR UPDATE")` o `ExecuteUpdateAsync`, sintaxis/operaciones exclusivas de
// un proveedor relacional real que InMemory no soporta (lanzan NotSupportedException/
// InvalidOperationException). Probar la finalización atómica y la concurrencia real de la última
// etapa exige Postgres real — decisión tomada con el usuario el 24-sep-2026: diferir esa parte a un
// smoke manual (plan §14, F5) en vez de levantar infraestructura de integración nueva ahora.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Diagnostics.Internal;
using ZooSanMarino.Application.DTOs.FlujosValidacion;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;
using ZooSanMarino.Infrastructure.Services;

namespace ZooSanMarino.Infrastructure.Tests.FlujosValidacion;

public class FlujoValidacionServiceTests
{
    private const string ProcesoKey = "TEST_PROCESO";
    private const int RolTecnicoId = 10;
    private const int RolCostosId = 20;

    private static ZooSanMarinoContext NuevoContexto()
    {
        var options = new DbContextOptionsBuilder<ZooSanMarinoContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            // CancelarInstanciaDelRecursoAsync abre una transacción condicional (patrón del repo,
            // pensado para Postgres); InMemory no las soporta y por defecto ESO es un warning-como-
            // error. Acá se ignora a propósito: no se prueba atomicidad entre statements, solo la
            // lógica de transición de estados.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestZooSanMarinoContext(options);
    }

    /// <summary>Siembra empresa, dos roles (uno por etapa, para que no se pisen como candidatos),
    /// dos usuarios y el proceso de prueba. Devuelve los ids/guids para armar el flujo en cada test.</summary>
    private static async Task<(int CompanyId, Guid Tecnico, Guid Costos, Guid Creador)> SembrarBaseAsync(ZooSanMarinoContext ctx)
    {
        var companyId = 1;
        var tecnico = Guid.NewGuid();
        var costos = Guid.NewGuid();
        var creador = Guid.NewGuid();

        ctx.Companies.Add(new Company { Id = companyId, Name = "Empresa Test", Identifier = "T-1", DocumentType = "NIT" });
        ctx.Roles.AddRange(
            new Role { Id = RolTecnicoId, Name = "Tecnico" },
            new Role { Id = RolCostosId, Name = "Costos" });
        ctx.Users.AddRange(
            new User { Id = tecnico, firstName = "Tecnico", surName = "De Granja", cedula = "1", telefono = "1", ubicacion = "1", IsActive = true },
            new User { Id = costos, firstName = "Costos", surName = "Empresa", cedula = "2", telefono = "2", ubicacion = "2", IsActive = true });
        ctx.UserRoles.AddRange(
            // Roles DISTINTOS a propósito: si compartieran rol, "costos" calificaría como candidato
            // de la etapa del Técnico por rol y falsearía el test de aislamiento de candidatos.
            new UserRole { UserId = tecnico, RoleId = RolTecnicoId, CompanyId = companyId },
            new UserRole { UserId = costos, RoleId = RolCostosId, CompanyId = companyId });
        ctx.ValidacionProcesos.Add(new ValidacionProceso
        {
            Key = ProcesoKey, Nombre = "Proceso de prueba", AdapterKey = ProcesoKey, IsActive = true
        });
        await ctx.SaveChangesAsync();

        return (companyId, tecnico, costos, creador);
    }

    private static FlujoPasoDto Paso(int orden, string nombre, params FlujoAsignadoDto[] asignados) =>
        new(null, orden, nombre, null, 1, asignados);

    private static FlujoAsignadoDto CandidatoRol(int roleId) => new(null, TipoAsignadoFlujo.Rol, roleId, null, "");
    private static FlujoAsignadoDto CandidatoUsuario(Guid userId) => new(null, TipoAsignadoFlujo.Usuario, null, userId, "");

    // ─── Configuración + publicación (casos 6-10 del plan) ─────────────────────

    [Fact]
    public async Task Preflight_EtapaSinCandidatos_NoEsPublicable()
    {
        await using var ctx = NuevoContexto();
        var (companyId, tecnico, costos, _) = await SembrarBaseAsync(ctx);
        var current = new FakeCurrentUser { CompanyId = companyId, UserGuid = Guid.NewGuid() };
        var service = new FlujoValidacionService(ctx, current, new[] { new FakeProcesoValidacionAdapter(ProcesoKey) });

        var flujo = await service.CrearBorradorAsync(new CrearBorradorFlujoCommand(
            companyId, ProcesoKey, "Flujo de prueba", 24, true, false,
            new[] { Paso(1, "Técnico", CandidatoRol(RolTecnicoId)), Paso(2, "Costos") /* sin candidatos */ }));

        var preflight = await service.PreflightPublicacionAsync(flujo.Id);

        Assert.False(preflight.EsPublicable);
        Assert.Contains(preflight.Problemas, p => p.Contains("etapa 2"));
    }

    [Fact]
    public async Task PublicarAsync_ConTopologiaValida_QuedaPublicadoYRetiraVersionAnterior()
    {
        await using var ctx = NuevoContexto();
        var (companyId, tecnico, costos, _) = await SembrarBaseAsync(ctx);
        var current = new FakeCurrentUser { CompanyId = companyId, UserGuid = Guid.NewGuid() };
        var service = new FlujoValidacionService(ctx, current, new[] { new FakeProcesoValidacionAdapter(ProcesoKey) });

        var v1 = await service.CrearBorradorAsync(new CrearBorradorFlujoCommand(
            companyId, ProcesoKey, "V1", 24, true, false,
            new[] { Paso(1, "Técnico", CandidatoRol(RolTecnicoId)) }));
        var v1Publicado = await service.PublicarAsync(v1.Id);
        Assert.Equal(EstadoFlujoValidacion.Publicado, v1Publicado.Estado);

        // Clonar y publicar V2: V1 debe quedar RETIRADO (solo una PUBLICADO vigente por empresa/proceso).
        var v2 = await service.ClonarAsync(v1.Id);
        Assert.Equal(2, v2.Version);
        var v2Publicado = await service.PublicarAsync(v2.Id);
        Assert.Equal(EstadoFlujoValidacion.Publicado, v2Publicado.Estado);

        var vigente = await service.ObtenerFlujoVigenteAsync(companyId, ProcesoKey);
        Assert.Equal(2, vigente!.Version);

        var v1Recargado = await ctx.ValidacionFlujos.AsNoTracking().FirstAsync(f => f.Id == v1.Id);
        Assert.Equal(EstadoFlujoValidacion.Retirado, v1Recargado.Estado);
    }

    [Fact]
    public async Task ActualizarBorrador_SobrePublicado_Rechaza()
    {
        await using var ctx = NuevoContexto();
        var (companyId, tecnico, _, _) = await SembrarBaseAsync(ctx);
        var current = new FakeCurrentUser { CompanyId = companyId, UserGuid = Guid.NewGuid() };
        var service = new FlujoValidacionService(ctx, current, new[] { new FakeProcesoValidacionAdapter(ProcesoKey) });

        var flujo = await service.CrearBorradorAsync(new CrearBorradorFlujoCommand(
            companyId, ProcesoKey, "V1", 24, true, false,
            new[] { Paso(1, "Técnico", CandidatoRol(RolTecnicoId)) }));
        await service.PublicarAsync(flujo.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ActualizarBorradorAsync(flujo.Id, new ActualizarBorradorFlujoCommand(
                "Cambiado", 24, true, false, new[] { Paso(1, "Técnico", CandidatoRol(RolTecnicoId)) })));
    }

    [Fact]
    public async Task GestionDeOtraEmpresa_SinAdminEmpresas_Rechaza403()
    {
        await using var ctx = NuevoContexto();
        var (companyId, _, _, _) = await SembrarBaseAsync(ctx);
        var current = new FakeCurrentUser { CompanyId = companyId, UserGuid = Guid.NewGuid() };
        var service = new FlujoValidacionService(ctx, current, new[] { new FakeProcesoValidacionAdapter(ProcesoKey) });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CrearBorradorAsync(new CrearBorradorFlujoCommand(
                companyId + 1 /* otra empresa */, ProcesoKey, "V1", 24, true, false,
                new[] { Paso(1, "Técnico", CandidatoRol(RolTecnicoId)) })));
    }

    // ─── Resolución de modo (plan §8.1) ─────────────────────────────────────────

    [Fact]
    public async Task ResolverModo_SinFlujoNiFlag_EsInmediato()
    {
        await using var ctx = NuevoContexto();
        var (companyId, _, _, _) = await SembrarBaseAsync(ctx);
        var service = new FlujoValidacionService(ctx, new FakeCurrentUser { CompanyId = companyId },
            new[] { new FakeProcesoValidacionAdapter(ProcesoKey) });

        Assert.Equal(ModoValidacionProceso.Inmediato, await service.ResolverModoAsync(companyId, ProcesoKey));
    }

    [Fact]
    public async Task ResolverModo_ConFlagLegacyEncendido_EsLegacyUnPaso()
    {
        await using var ctx = NuevoContexto();
        var (companyId, _, _, _) = await SembrarBaseAsync(ctx);
        (await ctx.Companies.FirstAsync(c => c.Id == companyId)).RequiereValidacionSeguimientoDiario = true;
        await ctx.SaveChangesAsync();
        var service = new FlujoValidacionService(ctx, new FakeCurrentUser { CompanyId = companyId },
            new[] { new FakeProcesoValidacionAdapter(ProcesoKey) });

        Assert.Equal(ModoValidacionProceso.LegacyUnPaso, await service.ResolverModoAsync(companyId, ProcesoKey));
    }

    [Fact]
    public async Task ResolverModo_ConFlujoPublicado_EsSecuencial_AunConFlagLegacyEncendido()
    {
        await using var ctx = NuevoContexto();
        var (companyId, tecnico, _, _) = await SembrarBaseAsync(ctx);
        (await ctx.Companies.FirstAsync(c => c.Id == companyId)).RequiereValidacionSeguimientoDiario = true;
        await ctx.SaveChangesAsync();
        var current = new FakeCurrentUser { CompanyId = companyId, UserGuid = Guid.NewGuid() };
        var service = new FlujoValidacionService(ctx, current, new[] { new FakeProcesoValidacionAdapter(ProcesoKey) });
        var flujo = await service.CrearBorradorAsync(new CrearBorradorFlujoCommand(
            companyId, ProcesoKey, "V1", 24, true, false,
            new[] { Paso(1, "Técnico", CandidatoRol(RolTecnicoId)) }));
        await service.PublicarAsync(flujo.Id);

        Assert.Equal(ModoValidacionProceso.Secuencial, await service.ResolverModoAsync(companyId, ProcesoKey));
    }

    // ─── Ciclo de vida de instancias (plan §8.2, §6.16) ────────────────────────

    private static async Task<(FlujoValidacionService Service, FakeProcesoValidacionAdapter Adapter, int CompanyId, Guid Tecnico, Guid Costos, Guid Creador, FakeCurrentUser Current)>
        PrepararFlujoPublicadoDeDosEtapasAsync(ZooSanMarinoContext ctx)
    {
        var (companyId, tecnico, costos, creador) = await SembrarBaseAsync(ctx);
        var current = new FakeCurrentUser { CompanyId = companyId, UserGuid = Guid.NewGuid() };
        var adapter = new FakeProcesoValidacionAdapter(ProcesoKey)
        {
            CompanyIdARetornar = companyId,
            ResumenARetornar = new ResumenRecursoValidacion(companyId, "Granja Test", "N1", "G1", "Lote 1", new DateOnly(2026, 9, 24))
        };
        var service = new FlujoValidacionService(ctx, current, new[] { adapter });

        var flujo = await service.CrearBorradorAsync(new CrearBorradorFlujoCommand(
            companyId, ProcesoKey, "Técnico → Costos", 24, true, false,
            new[]
            {
                Paso(1, "Técnico", CandidatoRol(RolTecnicoId)),
                Paso(2, "Costos", CandidatoUsuario(costos)),
            }));
        await service.PublicarAsync(flujo.Id);

        return (service, adapter, companyId, tecnico, costos, creador, current);
    }

    [Fact]
    public async Task CrearInstancia_DejaElPaso1PendienteYElRestoBloqueado()
    {
        await using var ctx = NuevoContexto();
        var (service, _, companyId, _, _, creador, _) = await PrepararFlujoPublicadoDeDosEtapasAsync(ctx);

        var instanciaId = await service.CrearInstanciaAsync(companyId, ProcesoKey, "R-1", creador);
        var estado = await service.ObtenerEstadoAsync(ProcesoKey, "R-1");

        Assert.NotNull(estado);
        Assert.Equal(instanciaId, estado!.InstanciaId);
        Assert.Equal(EstadoValidacionInstancia.PendienteValidacion, estado.Estado);
        Assert.Equal(1, estado.PasoActualOrden);
        Assert.Equal(2, estado.TotalPasos);
        Assert.Equal(EstadoInstanciaPaso.Pendiente, estado.Pasos.Single(p => p.Orden == 1).Estado);
        Assert.Equal(EstadoInstanciaPaso.Bloqueada, estado.Pasos.Single(p => p.Orden == 2).Estado);
    }

    [Fact]
    public async Task MisPendientes_SoloListaAQuienEsCandidatoDeLaEtapaActual()
    {
        await using var ctx = NuevoContexto();
        var (service, _, companyId, tecnico, costos, creador, current) = await PrepararFlujoPublicadoDeDosEtapasAsync(ctx);
        await service.CrearInstanciaAsync(companyId, ProcesoKey, "R-1", creador);

        current.UserGuid = tecnico; // candidato de la etapa 1 (por rol)
        var pendientesTecnico = await service.ObtenerMisPendientesAsync(companyId, ProcesoKey);
        Assert.Single(pendientesTecnico);
        Assert.Equal("R-1", pendientesTecnico[0].RecursoId);
        Assert.Equal("Técnico", pendientesTecnico[0].PasoActualNombre);

        current.UserGuid = costos; // candidato de la etapa 2, no de la 1 actual
        var pendientesCostos = await service.ObtenerMisPendientesAsync(companyId, ProcesoKey);
        Assert.Empty(pendientesCostos);
    }

    [Fact]
    public async Task CancelarInstancia_LaDejaCanceladaYLiberaViaElAdaptador()
    {
        await using var ctx = NuevoContexto();
        var (service, adapter, companyId, _, _, creador, _) = await PrepararFlujoPublicadoDeDosEtapasAsync(ctx);
        await service.CrearInstanciaAsync(companyId, ProcesoKey, "R-1", creador);

        await service.CancelarInstanciaDelRecursoAsync(ProcesoKey, "R-1");

        Assert.Contains("R-1", adapter.RecursosLiberados);
        var estado = await service.ObtenerEstadoAsync(ProcesoKey, "R-1");
        Assert.Equal(EstadoValidacionInstancia.Cancelada, estado!.Estado);
    }

    [Fact]
    public async Task CancelarInstancia_SinInstanciaActiva_EsNoOpIdempotente()
    {
        await using var ctx = NuevoContexto();
        var (companyId, _, _, _) = await SembrarBaseAsync(ctx);
        var current = new FakeCurrentUser { CompanyId = companyId, UserGuid = Guid.NewGuid() };
        var service = new FlujoValidacionService(ctx, current, new[] { new FakeProcesoValidacionAdapter(ProcesoKey) });

        // No debe lanzar aunque no exista ninguna instancia para ese recurso.
        await service.CancelarInstanciaDelRecursoAsync(ProcesoKey, "no-existe");
    }

    // ─── Novedades del Home (plan §6.8, casos 14-15, 36-39) ────────────────────
    //
    // Se inserta la novedad directamente (no vía DevolverAsync, que necesita Postgres real) para
    // probar solo la lectura y la regla "leer no resuelve".

    [Fact]
    public async Task Novedades_LeerNoLaResuelve_SoloMarcaReadAt()
    {
        await using var ctx = NuevoContexto();
        var (companyId, tecnico, _, creador) = await SembrarBaseAsync(ctx);
        var adapter = new FakeProcesoValidacionAdapter(ProcesoKey) { CompanyIdARetornar = companyId };
        var current = new FakeCurrentUser { CompanyId = companyId, UserGuid = tecnico };
        var service = new FlujoValidacionService(ctx, current, new[] { adapter });

        var (novedadId, _) = await SembrarNovedadActivaAsync(ctx, companyId, destinatario: tecnico, generadaPor: creador);

        var novedades = await service.ObtenerMisNovedadesAsync();
        Assert.Single(novedades);
        Assert.Null(novedades[0].ReadAt);

        // MarcarNovedadLeidaAsync en sí usa ExecuteUpdateAsync, que el proveedor InMemory no
        // soporta (mismo límite que el FOR UPDATE de Aprobar/Devolver): se simula su efecto con una
        // escritura directa para probar lo que sí es de este test — que LEER no resuelve la novedad.
        var novedad = await ctx.ValidacionNovedadesUsuario.SingleAsync(n => n.Id == novedadId);
        novedad.ReadAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var trasLeer = await service.ObtenerMisNovedadesAsync();
        Assert.Single(trasLeer); // sigue activa: leer NO la resuelve (regla §4.17 del plan).
        Assert.NotNull(trasLeer[0].ReadAt);
        Assert.Equal(EstadoNovedadValidacion.Activa, novedad.Estado);
    }

    [Fact]
    public async Task Novedades_SoloVeLasPropiasDeLaEmpresaActiva()
    {
        await using var ctx = NuevoContexto();
        var (companyId, tecnico, costos, creador) = await SembrarBaseAsync(ctx);
        await SembrarNovedadActivaAsync(ctx, companyId, destinatario: tecnico, generadaPor: creador);

        var current = new FakeCurrentUser { CompanyId = companyId, UserGuid = costos }; // no es el destinatario
        var service = new FlujoValidacionService(ctx, current, new[] { new FakeProcesoValidacionAdapter(ProcesoKey) });

        Assert.Empty(await service.ObtenerMisNovedadesAsync());
    }

    private static async Task<(long NovedadId, long AccionId)> SembrarNovedadActivaAsync(
        ZooSanMarinoContext ctx, int companyId, Guid destinatario, Guid generadaPor)
    {
        var proceso = await ctx.ValidacionProcesos.FirstAsync(p => p.Key == ProcesoKey);
        var flujo = new ValidacionFlujo
        {
            CompanyId = companyId, ProcesoId = proceso.Id, Version = 1, Nombre = "F",
            Estado = EstadoFlujoValidacion.Publicado, CreatedByUserId = generadaPor
        };
        ctx.ValidacionFlujos.Add(flujo);
        await ctx.SaveChangesAsync();

        var instancia = new ValidacionInstancia
        {
            CompanyId = companyId, ProcesoId = proceso.Id, FlujoId = flujo.Id,
            RecursoTipo = ProcesoKey, RecursoId = "R-novedad", PasoActualOrden = 1,
            Estado = EstadoValidacionInstancia.DevueltaCorreccion, CreatedByUserId = generadaPor,
        };
        ctx.ValidacionInstancias.Add(instancia);
        await ctx.SaveChangesAsync();

        var paso = new ValidacionInstanciaPaso
        {
            InstanciaId = instancia.Id, PasoDefinicionId = 0, Orden = 1, Nombre = "Técnico",
            Estado = EstadoInstanciaPaso.EsperandoCorreccion,
        };
        ctx.ValidacionInstanciaPasos.Add(paso);
        await ctx.SaveChangesAsync();

        var accion = new ValidacionAccion
        {
            InstanciaId = instancia.Id, InstanciaPasoId = paso.Id, Accion = TipoAccionValidacion.Devolver,
            UserId = generadaPor, Comentario = "Motivo de prueba",
        };
        ctx.ValidacionAcciones.Add(accion);
        await ctx.SaveChangesAsync();

        var novedad = new ValidacionNovedadUsuario
        {
            InstanciaId = instancia.Id, AccionDevolucionId = accion.Id, CompanyId = companyId,
            DestinatarioUserId = destinatario, GeneradaPorUserId = generadaPor,
            Estado = EstadoNovedadValidacion.Activa, Motivo = "Motivo de prueba",
            ContextoResumen = "{}",
        };
        ctx.ValidacionNovedadesUsuario.Add(novedad);
        await ctx.SaveChangesAsync();

        return (novedad.Id, accion.Id);
    }
}
