using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// B10 — quién es Super Admin sale de un DATO (<c>users.is_super_admin</c>), nunca de un correo, un
/// nombre de rol ni un nombre de empresa. Hasta ago-2026 la regla estaba copiada a mano en 14 sitios
/// comparando un email hardcodeado, con 4 formas distintas de comparar; conceder o revocar el
/// privilegio más grande del sistema exigía desplegar.
/// </summary>
public class SuperAdminCalculosTests
{
    [Fact]
    public void T1_ConLaMarcaEnTrue_EsSuperAdmin()
    {
        Assert.True(SuperAdminCalculos.EsSuperAdmin(true));
    }

    [Fact]
    public void T2_ConLaMarcaEnFalse_NoEsSuperAdmin()
    {
        Assert.False(SuperAdminCalculos.EsSuperAdmin(false));
    }

    [Fact]
    public void T3_SinDato_FailClosed()
    {
        // null = usuario inexistente, sesión sin Guid o fila no encontrada. Ante la duda, NO se concede.
        Assert.False(SuperAdminCalculos.EsSuperAdmin(null));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    public void T4_LaRespuestaDependeSoloDeLaMarca(bool? marca, bool esperado)
    {
        // Dos usuarios con la misma marca dan el MISMO resultado: la regla no mira nada más.
        Assert.Equal(esperado, SuperAdminCalculos.EsSuperAdmin(marca));
        Assert.Equal(esperado, SuperAdminCalculos.EsSuperAdmin(marca));
    }

    [Fact]
    public void T5_ElDefaultDeUnUsuarioNuevo_NoEsSuperAdmin()
    {
        // `users.is_super_admin` nace en false (DEFAULT neutro, tanto en la entidad como en la
        // columna): dar de alta a alguien no le regala el privilegio. Ese era el riesgo a cerrar.
        var marcaDeUsuarioReciénCreado = default(bool);
        Assert.False(SuperAdminCalculos.EsSuperAdmin(marcaDeUsuarioReciénCreado));
    }
}
