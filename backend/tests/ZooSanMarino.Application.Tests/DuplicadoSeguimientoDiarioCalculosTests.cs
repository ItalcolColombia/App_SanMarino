using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Fija el contrato entre los nombres de índice de la BD y el mensaje que ve el usuario.
///
/// <para>
/// Existe porque el nombre del índice viaja desde una migración hasta un <c>if</c> del controller sin
/// que ningún compilador los relacione: si una migración renombra el índice y nadie toca la lista, el
/// usuario deja de ver «ya existe un registro para ese día» y empieza a recibir el texto crudo de
/// Postgres. Es una regresión que el build no ve y que sólo aparece cuando alguien intenta duplicar
/// un día en producción.
/// </para>
/// </summary>
public class DuplicadoSeguimientoDiarioCalculosTests
{
    /// <summary>
    /// Los dos índices que sostienen «un registro por lote y por día» tienen que dar el mismo
    /// mensaje: para el usuario es el mismo problema, aunque uno cace el instante y el otro el día.
    /// </summary>
    [Theory]
    [InlineData("uq_seg_diario_aves_engorde_lote_fecha")]
    [InlineData("ux_seg_diario_aves_engorde_lote_dia_utc")]
    public void LosDosIndicesDelDiaDanElMensajeClaro(string indice)
    {
        Assert.True(DuplicadoSeguimientoDiarioCalculos.EsUnRegistroPorLotePorDia(indice));
    }

    /// <summary>El nombre llega de Npgsql, así que la comparación no puede depender de la caja.</summary>
    [Fact]
    public void NoDependeDeMayusculas()
    {
        Assert.True(DuplicadoSeguimientoDiarioCalculos.EsUnRegistroPorLotePorDia(
            "UX_SEG_DIARIO_AVES_ENGORDE_LOTE_DIA_UTC"));
    }

    /// <summary>
    /// Un índice ajeno cae al mensaje genérico, que incluye el detalle de Postgres. Afirmar «ya existe
    /// un registro para ese día» sobre una violación que no se verificó manda al usuario a buscar un
    /// duplicado que no existe.
    /// </summary>
    [Theory]
    [InlineData("ux_seg_engorde_cruce_lote_fecha")]
    [InlineData("uq_seg_diario_lrae_lote_fecha")]
    [InlineData("seguimiento_diario_aves_engorde_pkey")]
    [InlineData("")]
    [InlineData(null)]
    public void OtroIndiceNoSeHacePasarPorDuplicadoDeDia(string? indice)
    {
        Assert.False(DuplicadoSeguimientoDiarioCalculos.EsUnRegistroPorLotePorDia(indice));
    }
}
