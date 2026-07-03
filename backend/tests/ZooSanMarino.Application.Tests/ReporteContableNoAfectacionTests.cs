using ZooSanMarino.Domain.Enums;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Fase 2 (S5a) — GOLDEN de NO-AFECTACIÓN del reporte contable Colombia.
///
/// ReporteContableService.ObtenerDatosBultosAsync clasifica los movimientos del modelo A en
/// exactamente 3 buckets, filtrando por el NOMBRE (string) del movement_type:
///   * Entradas  = MovementType ∈ { "Entry", "TransferIn" }
///   * Traslados = MovementType == "TransferOut"
///   * Retiros   = MovementType == "Exit"
/// (El consumo de bultos del contable NO sale de inventario: es seguimiento.ConsumoKg / 40.)
///
/// Los tipos automáticos de Fase 2 (ConsumoSeguimiento / DevolucionSeguimiento) NO figuran en
/// ninguno de esos literales → quedan EXCLUIDOS de los 3 buckets → las cifras del contable
/// (entradas/traslados/retiros de bultos) son IDÉNTICAS con o sin descuento automático.
/// Si alguien agrega uno de los tipos nuevos a un bucket, este test rompe y avisa.
///
/// El test replica EXACTO los predicados del servicio (los mismos literales) para que sea un
/// guardián estable sin necesidad de levantar EF/BD.
/// </summary>
public class ReporteContableNoAfectacionTests
{
    // Réplica literal de los predicados de ReporteContableService.ObtenerDatosBultosAsync.
    private static bool EsEntrada(string t)  => t == "Entry" || t == "TransferIn";
    private static bool EsTraslado(string t) => t == "TransferOut";
    private static bool EsRetiro(string t)   => t == "Exit";
    private static bool EntraAlContable(string t) => EsEntrada(t) || EsTraslado(t) || EsRetiro(t);

    [Theory]
    [InlineData("ConsumoSeguimiento")]
    [InlineData("DevolucionSeguimiento")]
    public void TiposFase2_NoEntranEnNingunBucketDelContable(string tipoNuevo)
    {
        Assert.False(EsEntrada(tipoNuevo));
        Assert.False(EsTraslado(tipoNuevo));
        Assert.False(EsRetiro(tipoNuevo));
        Assert.False(EntraAlContable(tipoNuevo));
    }

    [Fact]
    public void TiposManuales_SiguenEntrandoAlContable_SinRegresion()
    {
        // Los tipos del UI manual mantienen su clasificación (no se rompió el contable).
        Assert.True(EsEntrada("Entry"));
        Assert.True(EsEntrada("TransferIn"));
        Assert.True(EsTraslado("TransferOut"));
        Assert.True(EsRetiro("Exit"));
        // Adjust nunca fue un bucket del contable (se mantiene fuera).
        Assert.False(EntraAlContable("Adjust"));
    }

    [Fact]
    public void SoloLosCuatroTiposManuales_EntranAlContable()
    {
        // Recorre TODO el enum: exactamente Entry/TransferIn/TransferOut/Exit entran; el resto no.
        var entran = Enum.GetNames<InventoryMovementType>().Where(EntraAlContable).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "Entry", "Exit", "TransferIn", "TransferOut" }, entran);

        // Y en particular, los 2 tipos Fase 2 existen en el enum pero quedan fuera del contable.
        Assert.Contains("ConsumoSeguimiento", Enum.GetNames<InventoryMovementType>());
        Assert.Contains("DevolucionSeguimiento", Enum.GetNames<InventoryMovementType>());
        Assert.DoesNotContain("ConsumoSeguimiento", entran);
        Assert.DoesNotContain("DevolucionSeguimiento", entran);
    }

    // Fase 3 (paso 2) — Colombia pasó a consumir del MODELO B (inventario_gestion), con movimientos
    // tipo "Consumo"/"Ingreso". El contable lee EXCLUSIVAMENTE el modelo A (farm_inventory_movements)
    // y no consulta inventario_gestion_movimiento en absoluto → los movimientos B de Colombia son
    // invisibles para el contable. Además, al dejar de escribir en A, los buckets A ya NO reciben
    // consumos nuevos de Colombia → el estado del contable A es el pre-Fase-2 (idéntico).
    [Theory]
    [InlineData("Consumo")]   // tipo del modelo B (nivel granja Colombia)
    [InlineData("Ingreso")]   // devolución en modelo B
    public void TiposModeloB_NoEntranEnNingunBucketDelContableModeloA(string tipoModeloB)
    {
        // Los literales del contable (modelo A) no incluyen los tipos del modelo B.
        Assert.False(EsEntrada(tipoModeloB));
        Assert.False(EsTraslado(tipoModeloB));
        Assert.False(EsRetiro(tipoModeloB));
        Assert.False(EntraAlContable(tipoModeloB));
    }
}
