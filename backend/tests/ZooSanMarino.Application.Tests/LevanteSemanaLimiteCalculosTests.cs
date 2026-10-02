using Xunit;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Límite de semanas del seguimiento diario de levante por empresa (`companies.levante_hasta_semana`).
/// Con el límite en null el comportamiento previo queda intacto: todo registro pasa.
/// </summary>
public class LevanteSemanaLimiteCalculosTests
{
    private static readonly DateTime Encaset = new(2026, 1, 1);

    [Theory]
    [InlineData(0)]
    [InlineData(174)]
    [InlineData(175)]
    [InlineData(1000)]
    public void Sin_limite_todo_pasa_como_antes(int dias)
    {
        Assert.True(LevanteSemanaLimiteCalculos.PermiteRegistro(Encaset, Encaset.AddDays(dias), null));
    }

    [Fact]
    public void Sin_fecha_de_encaset_no_hay_semana_evaluable_y_pasa()
    {
        Assert.True(LevanteSemanaLimiteCalculos.PermiteRegistro(null, new DateTime(2030, 1, 1), 25));
    }

    [Fact]
    public void El_ultimo_dia_de_la_semana_limite_pasa()
    {
        // Día 174 desde el encaset = semana 25 (floor(174/7)+1 = 25), su último día.
        Assert.True(LevanteSemanaLimiteCalculos.PermiteRegistro(Encaset, Encaset.AddDays(174), 25));
        Assert.Equal(Encaset.AddDays(174), LevanteSemanaLimiteCalculos.FechaLimite(Encaset, 25));
    }

    [Fact]
    public void El_primer_dia_de_la_semana_siguiente_se_rechaza()
    {
        Assert.False(LevanteSemanaLimiteCalculos.PermiteRegistro(Encaset, Encaset.AddDays(175), 25));
    }

    [Fact]
    public void La_hora_del_registro_no_corre_el_dia()
    {
        Assert.True(LevanteSemanaLimiteCalculos.PermiteRegistro(
            Encaset.AddHours(23), Encaset.AddDays(174).AddHours(23).AddMinutes(59), 25));
    }

    [Fact]
    public void Semana_1_solo_admite_los_primeros_7_dias()
    {
        Assert.True(LevanteSemanaLimiteCalculos.PermiteRegistro(Encaset, Encaset.AddDays(6), 1));
        Assert.False(LevanteSemanaLimiteCalculos.PermiteRegistro(Encaset, Encaset.AddDays(7), 1));
    }

    [Fact]
    public void El_mensaje_cita_limite_semana_del_registro_y_ultimo_dia()
    {
        var msg = LevanteSemanaLimiteCalculos.Mensaje(25, Encaset, Encaset.AddDays(175));
        Assert.Contains("hasta la semana 25", msg);
        Assert.Contains("último día: 24/06/2026", msg);
        Assert.Contains("es de la semana 26", msg);
    }
}
