using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests de la lógica PURA del resumen seguro de migraciones de DB Studio (ver plan
/// fase_de_desarrollo/db_studio_resumen_seguro_plan.md): quién conserva la experiencia completa
/// y cómo se proyecta el estado de cada migración sin inventar metadatos.
/// </summary>
public class DbStudioMigrationCalculosTests
{
    // ===================== Acceso completo =====================

    [Fact]
    public void TieneAccesoCompleto_RolAdmin_ConservaLaExperienciaCompleta()
    {
        Assert.True(DbStudioMigrationCalculos.TieneAccesoCompleto(new[] { "admin" }, "operador@ejemplo.com"));
    }

    [Fact]
    public void TieneAccesoCompleto_RolAdminConEspaciosYMayusculas_ConservaLaExperienciaCompleta()
    {
        Assert.True(DbStudioMigrationCalculos.TieneAccesoCompleto(new[] { "  ADMIN " }, null));
    }

    [Fact]
    public void TieneAccesoCompleto_CorreoExcepcional_ConservaLaExperienciaCompleta()
    {
        Assert.True(DbStudioMigrationCalculos.TieneAccesoCompleto(
            Array.Empty<string>(), "MOIESBBUGA@GMAIL.COM"));
    }

    [Fact]
    public void TieneAccesoCompleto_CorreoExcepcionalConEspacios_ConservaLaExperienciaCompleta()
    {
        Assert.True(DbStudioMigrationCalculos.TieneAccesoCompleto(
            Array.Empty<string>(), "  moiesbbuga@gmail.com  "));
    }

    [Fact]
    public void TieneAccesoCompleto_UsuarioNormal_NoObtieneAccesoCompleto()
    {
        Assert.False(DbStudioMigrationCalculos.TieneAccesoCompleto(
            new[] { "operador" }, "operador@ejemplo.com"));
    }

    [Fact]
    public void TieneAccesoCompleto_SinRolesNiCorreo_NoObtieneAccesoCompleto()
    {
        Assert.False(DbStudioMigrationCalculos.TieneAccesoCompleto(Array.Empty<string>(), null));
    }

    [Fact]
    public void TieneAccesoCompleto_RolQueContieneAdmin_NoObtieneAccesoCompleto()
    {
        // "administrador de granja" NO es "admin": la comparación es por igualdad, no por substring.
        Assert.False(DbStudioMigrationCalculos.TieneAccesoCompleto(
            new[] { "administrador de granja", "sub-admin" }, null));
    }

    // ===================== Proyección de estados =====================

    [Fact]
    public void Resumir_SeparaAplicadasPendientes_YNoInventaFecha()
    {
        var result = DbStudioMigrationCalculos.Resumir(
            new[] { "20260101010101_Inicial", "20260202020202_Siguiente" },
            new[] { "20260101010101_Inicial" });

        Assert.Collection(result,
            applied =>
            {
                Assert.Equal("20260101010101_Inicial", applied.MigrationId);
                Assert.Equal("aplicada", applied.Status);
                Assert.Null(applied.AppliedAtUtc);
            },
            pending =>
            {
                Assert.Equal("20260202020202_Siguiente", pending.MigrationId);
                Assert.Equal("pendiente", pending.Status);
                Assert.Null(pending.AppliedAtUtc);
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
        // Solo se proyecta lo que EF conoce por el ensamblado; una fila suelta en el historial no aparece.
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
        Assert.Empty(DbStudioMigrationCalculos.Resumir(Array.Empty<string>(), new[] { "0001_A" }));
    }
}
