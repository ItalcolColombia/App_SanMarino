using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato del recorte defensivo de <c>tipo_alimento</c> (incidente 2026-08-06, lote A374A).
/// El objetivo es que ninguna concatenación de nombres de alimento pueda volver a abortar el
/// guardado del seguimiento diario con un 22001.
/// </summary>
public class TipoAlimentoCalculosTests
{
    // El string real que producía el 500: 2 alimentos de hembras + 1 de machos con los nombres
    // del catálogo de reproductora de Sanmarino (35, 33 y 32 caracteres).
    private const string TresAlimentosA374A =
        "H: POLLITA INICIACION REPRODUCTORA PES / H: POLLA LEVANTE REPRODUCTORA PESADA / M: PRODUCCION 1 REPRODUCTORA PESADA";

    [Fact] // T1
    public void Recortar_Null_DevuelveNull()
    {
        Assert.Null(TipoAlimentoCalculos.Recortar(null));
    }

    [Fact] // T2
    public void Recortar_Vacio_DevuelveVacio()
    {
        Assert.Equal("", TipoAlimentoCalculos.Recortar(""));
    }

    [Fact] // T3
    public void Recortar_PorDebajoDelTope_DevuelveLaMismaInstancia()
    {
        var valor = new string('x', TipoAlimentoCalculos.MaxLongitud - 1);
        Assert.Same(valor, TipoAlimentoCalculos.Recortar(valor));
    }

    [Fact] // T4 — borde INCLUSIVO: 500 exactos entran sin tocar
    public void Recortar_ExactamenteElTope_NoRecorta()
    {
        var valor = new string('x', TipoAlimentoCalculos.MaxLongitud);
        Assert.Same(valor, TipoAlimentoCalculos.Recortar(valor));
        Assert.False(TipoAlimentoCalculos.Recorta(valor));
    }

    [Fact] // T5 — un carácter por encima ya recorta, conservando el prefijo exacto
    public void Recortar_UnCaracterDeMas_RecortaAlTopeConservandoPrefijo()
    {
        var valor = new string('a', TipoAlimentoCalculos.MaxLongitud) + "Z";

        var resultado = TipoAlimentoCalculos.Recortar(valor);

        Assert.NotNull(resultado);
        Assert.Equal(TipoAlimentoCalculos.MaxLongitud, resultado!.Length);
        Assert.Equal(valor[..TipoAlimentoCalculos.MaxLongitud], resultado);
        Assert.DoesNotContain("Z", resultado);
        Assert.True(TipoAlimentoCalculos.Recorta(valor));
    }

    [Fact] // T6 — recortar dos veces da lo mismo que recortar una
    public void Recortar_EsIdempotente()
    {
        var valor = new string('b', 900);

        var una = TipoAlimentoCalculos.Recortar(valor);
        var dos = TipoAlimentoCalculos.Recortar(una);

        Assert.Equal(una, dos);
        Assert.Equal(TipoAlimentoCalculos.MaxLongitud, dos!.Length);
    }

    [Theory] // T7 — el tope explícito conserva el contrato viejo de la carga masiva (100)
    [InlineData(100)]
    [InlineData(250)]
    public void Recortar_ConTopeExplicito_RespetaEseTope(int max)
    {
        var valor = new string('c', 600);

        var resultado = TipoAlimentoCalculos.Recortar(valor, max);

        Assert.Equal(max, resultado!.Length);
    }

    [Fact] // T8 — el caso real del incidente: con la columna ampliada ya NO se recorta
    public void Recortar_TresAlimentosDeA374A_EntranCompletosEnLaColumnaAmpliada()
    {
        Assert.True(TresAlimentosA374A.Length > 100, "el string del incidente debe superar el varchar(100) viejo");

        Assert.Same(TresAlimentosA374A, TipoAlimentoCalculos.Recortar(TresAlimentosA374A));
        Assert.False(TipoAlimentoCalculos.Recorta(TresAlimentosA374A));
    }

    [Fact] // T8b — y con el tope viejo SÍ se habría recortado (era el 22001)
    public void Recortar_TresAlimentosDeA374A_ConElTopeViejoDe100_SeRecortaba()
    {
        Assert.True(TipoAlimentoCalculos.Recorta(TresAlimentosA374A, 100));
        Assert.Equal(100, TipoAlimentoCalculos.Recortar(TresAlimentosA374A, 100)!.Length);
    }

    [Fact] // guarda: un tope no positivo no debe recortar (evita vaciar la columna por un config a 0)
    public void Recortar_TopeNoPositivo_NoRecorta()
    {
        var valor = new string('d', 300);

        Assert.Same(valor, TipoAlimentoCalculos.Recortar(valor, 0));
        Assert.Same(valor, TipoAlimentoCalculos.Recortar(valor, -5));
        Assert.False(TipoAlimentoCalculos.Recorta(valor, 0));
    }

    [Fact] // el tope del cálculo y el del DDL de la migración son el mismo número
    public void MaxLongitud_CoincideConElDdlDeLaMigracion()
    {
        Assert.Equal(500, TipoAlimentoCalculos.MaxLongitud);
    }

    [Fact] // engorde sigue en 100 porque su columna no se puede ampliar (vista de Power BI encima)
    public void MaxLongitudEngorde_SigueEn100()
    {
        Assert.Equal(100, TipoAlimentoCalculos.MaxLongitudEngorde);
    }

    [Fact] // en engorde el mismo string del incidente SÍ se recorta — pero no revienta el guardado
    public void Recortar_ConElTopeDeEngorde_RecortaSinRomper()
    {
        var resultado = TipoAlimentoCalculos.Recortar(TresAlimentosA374A, TipoAlimentoCalculos.MaxLongitudEngorde);

        Assert.Equal(100, resultado!.Length);
        Assert.Equal(TresAlimentosA374A[..100], resultado);
    }
}
