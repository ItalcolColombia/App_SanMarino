using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests de la lógica PURA del resumen seguro de migraciones de DB Studio (ver plan
/// fase_de_desarrollo/db_studio_resumen_seguro_plan.md): el acceso completo es DOBLE validación
/// (correo autorizado Y admin), y la proyección de estados es solo nombre + ejecutada sí/no.
/// </summary>
public class DbStudioMigrationCalculosTests
{
    // ===================== Acceso completo = correo autorizado Y admin =====================

    [Fact]
    public void TieneAccesoCompleto_CorreoAutorizadoYRolAdmin_DaAccesoCompleto()
    {
        Assert.True(DbStudioMigrationCalculos.TieneAccesoCompleto(
            new[] { "admin" }, "moiesbbuga@gmail.com"));
    }

    [Fact]
    public void TieneAccesoCompleto_CorreoAutorizadoYRolAdministrador_DaAccesoCompleto()
    {
        Assert.True(DbStudioMigrationCalculos.TieneAccesoCompleto(
            new[] { "administrador" }, "moiesbbuga@gmail.com"));
    }

    [Fact]
    public void TieneAccesoCompleto_CorreoAutorizadoYSuperAdmin_DaAccesoCompleto()
    {
        Assert.True(DbStudioMigrationCalculos.TieneAccesoCompleto(
            Array.Empty<string>(), "moiesbbuga@gmail.com", esSuperAdmin: true));
    }

    [Fact]
    public void TieneAccesoCompleto_CorreoAutorizadoYPermisoDbStudioAdmin_DaAccesoCompleto()
    {
        Assert.True(DbStudioMigrationCalculos.TieneAccesoCompleto(
            Array.Empty<string>(), "moiesbbuga@gmail.com", tienePermisoDbStudioAdmin: true));
    }

    [Fact]
    public void TieneAccesoCompleto_CorreoAutorizadoConEspaciosYMayusculas_DaAccesoCompleto()
    {
        Assert.True(DbStudioMigrationCalculos.TieneAccesoCompleto(
            new[] { "  ADMIN " }, "  MOIESBBUGA@GMAIL.COM  "));
    }

    [Fact]
    public void TieneAccesoCompleto_AdminPeroSinElCorreoAutorizado_NoDaAccesoCompleto()
    {
        // El admin "normal" ya no alcanza: falta el correo.
        Assert.False(DbStudioMigrationCalculos.TieneAccesoCompleto(
            new[] { "admin", "administrador" }, "otro.admin@empresa.com", esSuperAdmin: true));
    }

    [Fact]
    public void TieneAccesoCompleto_CorreoAutorizadoPeroSinSerAdmin_NoDaAccesoCompleto()
    {
        // El correo solo tampoco alcanza: falta ser admin por algún lado.
        Assert.False(DbStudioMigrationCalculos.TieneAccesoCompleto(
            new[] { "operador" }, "moiesbbuga@gmail.com"));
    }

    [Fact]
    public void TieneAccesoCompleto_SinCorreoYSinRoles_NoDaAccesoCompleto()
    {
        Assert.False(DbStudioMigrationCalculos.TieneAccesoCompleto(
            Array.Empty<string>(), null));
    }

    // ===================== Helpers =====================

    [Theory]
    [InlineData("moiesbbuga@gmail.com", true)]
    [InlineData("MOIESBBUGA@GMAIL.COM", true)]
    [InlineData("  moiesbbuga@gmail.com  ", true)]
    [InlineData("moiesbbuga@gmail.com.attacker.com", false)]
    [InlineData("otro@gmail.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EsCorreoAutorizado_ComparaExactoTrimYCaseInsensitive(string? email, bool esperado)
        => Assert.Equal(esperado, DbStudioMigrationCalculos.EsCorreoAutorizado(email));

    [Fact]
    public void EsAdminPorRol_ReconoceAdminYAdministrador_ConTrimYCase()
    {
        Assert.True(DbStudioMigrationCalculos.EsAdminPorRol(new[] { "  Admin " }));
        Assert.True(DbStudioMigrationCalculos.EsAdminPorRol(new[] { "operador", "ADMINISTRADOR" }));
    }

    [Fact]
    public void EsAdminPorRol_NoConfundeRolesQueContienenLaPalabra()
    {
        Assert.False(DbStudioMigrationCalculos.EsAdminPorRol(new[] { "administrador de granja", "sub-admin" }));
        Assert.False(DbStudioMigrationCalculos.EsAdminPorRol(Array.Empty<string>()));
    }

    // ===================== Proyección de estados (nombre + ejecutada) =====================

    [Fact]
    public void Resumir_SeparaAplicadasDePendientes()
    {
        var result = DbStudioMigrationCalculos.Resumir(
            new[] { "20260101010101_Inicial", "20260202020202_Siguiente" },
            new[] { "20260101010101_Inicial" });

        Assert.Collection(result,
            applied =>
            {
                Assert.Equal("20260101010101_Inicial", applied.MigrationId);
                Assert.Equal("aplicada", applied.Status);
            },
            pending =>
            {
                Assert.Equal("20260202020202_Siguiente", pending.MigrationId);
                Assert.Equal("pendiente", pending.Status);
            });
    }

    [Fact]
    public void Resumir_ConservaElOrdenDeLasMigracionesConocidas_YDeduplica()
    {
        var result = DbStudioMigrationCalculos.Resumir(
            new[] { "0003_C", "0001_A", "0001_A", "0002_B" },
            Array.Empty<string>());

        Assert.Equal(new[] { "0003_C", "0001_A", "0002_B" }, result.Select(r => r.MigrationId));
        Assert.All(result, r => Assert.Equal("pendiente", r.Status));
    }

    [Fact]
    public void Resumir_MigracionAplicadaQueYaNoEstaEnElCodigo_NoSeListea()
    {
        var result = DbStudioMigrationCalculos.Resumir(
            new[] { "0001_A" },
            new[] { "0001_A", "0000_Fantasma" });

        Assert.Single(result);
        Assert.Equal("0001_A", result[0].MigrationId);
        Assert.Equal("aplicada", result[0].Status);
    }

    [Fact]
    public void Resumir_SinMigracionesConocidas_DevuelveListaVacia()
    {
        Assert.Empty(DbStudioMigrationCalculos.Resumir(
            Array.Empty<string>(), new[] { "0001_A" }));
    }
}
