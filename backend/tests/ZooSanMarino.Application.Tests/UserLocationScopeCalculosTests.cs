using Xunit;
using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.UserLocationScopeCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests del alcance granular usuario-granja (núcleo/galpón/lote o global).
/// Regla de oro: con restrict_locations = false el comportamiento es GLOBAL (idéntico al previo,
/// sin ningún filtro); con true, solo la unión de grants con cierre hacia abajo (descendientes)
/// y visibilidad hacia arriba (ancestros para navegación). Fail-closed en todos los bordes.
/// </summary>
public class UserLocationScopeCalculosTests
{
    // Granja de prueba: N1 { G1: [lote 1], G2: [lote 2] }, N2 { G3: [lote 3] },
    // lote 4 en N1 sin galpón, lote 5 sin núcleo ni galpón.
    private static readonly string[] Nucleos = { "N1", "N2" };

    private static readonly GalponUbicacion[] Galpones =
    {
        new("G1", "N1"),
        new("G2", "N1"),
        new("G3", "N2"),
    };

    private static readonly LoteUbicacion[] Lotes =
    {
        new(1, "N1", "G1"),
        new(2, "N1", "G2"),
        new(3, "N2", "G3"),
        new(4, "N1", null),
        new(5, null, null),
    };

    private static LocationScope Compute(bool restrict, params ScopeGrant[] grants) =>
        ComputeScope(restrict, grants, Nucleos, Galpones, Lotes);

    // ─── Flag OFF ⇒ global, comportamiento previo intacto ───

    [Fact]
    public void FlagApagado_EsGlobal_YPermiteTodo()
    {
        var scope = Compute(restrict: false, new ScopeGrant("N1", null, null)); // grants ignorados

        Assert.True(scope.IsGlobal);
        Assert.Empty(scope.NucleosVisibles);
        Assert.Empty(scope.GalponesVisibles);
        Assert.Empty(scope.LotesPermitidos);
        Assert.True(scope.PermiteNucleo("N2"));
        Assert.True(scope.PermiteGalpon("G3"));
        Assert.True(scope.PermiteLote(999)); // global: no se filtra nada
    }

    // ─── Flag ON + cero grants ⇒ no ve nada (fail-closed) ───

    [Fact]
    public void FlagEncendido_SinGrants_NoVeNada()
    {
        var scope = Compute(restrict: true);

        Assert.False(scope.IsGlobal);
        Assert.Empty(scope.NucleosVisibles);
        Assert.Empty(scope.GalponesVisibles);
        Assert.Empty(scope.LotesPermitidos);
        Assert.False(scope.PermiteNucleo("N1"));
        Assert.False(scope.PermiteGalpon("G1"));
        Assert.False(scope.PermiteLote(1));
    }

    // ─── Grant de núcleo ⇒ todos sus galpones y lotes ───

    [Fact]
    public void GrantNucleo_CubreGalponesYLotesDelNucleo()
    {
        var scope = Compute(true, new ScopeGrant("N1", null, null));

        Assert.Equal(new HashSet<string> { "N1" }, scope.NucleosVisibles);
        Assert.Equal(new HashSet<string> { "G1", "G2" }, scope.GalponesVisibles);
        Assert.Equal(new HashSet<int> { 1, 2, 4 }, scope.LotesPermitidos); // incluye lote 4 (N1 sin galpón)

        Assert.False(scope.PermiteNucleo("N2"));
        Assert.False(scope.PermiteGalpon("G3"));
        Assert.False(scope.PermiteLote(3));
        Assert.False(scope.PermiteLote(5)); // lote sin ubicación: solo grant directo lo alcanza
    }

    // ─── Grant de galpón ⇒ sus lotes; núcleo padre visible (navegación) ───

    [Fact]
    public void GrantGalpon_CubreSusLotes_YNucleoPadreQuedaVisible()
    {
        var scope = Compute(true, new ScopeGrant(null, "G1", null));

        Assert.Equal(new HashSet<string> { "N1" }, scope.NucleosVisibles);   // navegación
        Assert.Equal(new HashSet<string> { "G1" }, scope.GalponesVisibles);
        Assert.Equal(new HashSet<int> { 1 }, scope.LotesPermitidos);

        Assert.False(scope.PermiteGalpon("G2")); // hermano del mismo núcleo NO
        Assert.False(scope.PermiteLote(2));
        Assert.False(scope.PermiteLote(4)); // lote del núcleo pero fuera del galpón
    }

    // ─── Grant de lote ⇒ ese lote; galpón y núcleo padres visibles ───

    [Fact]
    public void GrantLote_SoloEseLote_ConAncestrosVisibles()
    {
        var scope = Compute(true, new ScopeGrant(null, null, 2));

        Assert.Equal(new HashSet<int> { 2 }, scope.LotesPermitidos);
        Assert.Equal(new HashSet<string> { "G2" }, scope.GalponesVisibles);
        Assert.Equal(new HashSet<string> { "N1" }, scope.NucleosVisibles);

        Assert.False(scope.PermiteLote(1));   // hermano del mismo núcleo NO
        Assert.False(scope.PermiteGalpon("G1"));
    }

    [Fact]
    public void GrantLote_SinUbicacion_SoloElLote()
    {
        var scope = Compute(true, new ScopeGrant(null, null, 5));

        Assert.Equal(new HashSet<int> { 5 }, scope.LotesPermitidos);
        Assert.Empty(scope.GalponesVisibles);
        Assert.Empty(scope.NucleosVisibles);
    }

    // ─── Unión de grants mixtos ───

    [Fact]
    public void GrantsMixtos_SeUnen()
    {
        var scope = Compute(true,
            new ScopeGrant("N2", null, null),   // núcleo completo
            new ScopeGrant(null, "G2", null),   // galpón de N1
            new ScopeGrant(null, null, 5));     // lote suelto

        Assert.Equal(new HashSet<string> { "N1", "N2" }, scope.NucleosVisibles);
        Assert.Equal(new HashSet<string> { "G2", "G3" }, scope.GalponesVisibles);
        Assert.Equal(new HashSet<int> { 2, 3, 5 }, scope.LotesPermitidos);
        Assert.False(scope.PermiteLote(1));
        Assert.False(scope.PermiteLote(4));
    }

    // ─── Referencias muertas: un grant que ya no existe en la granja NUNCA otorga acceso ───

    [Fact]
    public void GrantsMuertos_SeIgnoran_FailClosed()
    {
        var scope = Compute(true,
            new ScopeGrant("NX", null, null),   // núcleo inexistente
            new ScopeGrant(null, "GX", null),   // galpón inexistente (o movido de granja)
            new ScopeGrant(null, null, 999));   // lote inexistente (o movido de granja)

        Assert.False(scope.IsGlobal);
        Assert.Empty(scope.NucleosVisibles);
        Assert.Empty(scope.GalponesVisibles);
        Assert.Empty(scope.LotesPermitidos);
    }

    // ─── Helpers multi-granja (diccionario solo con granjas RESTRINGIDAS) ───

    [Fact]
    public void Helpers_GranjaNoRestringida_PasaTodo()
    {
        var restringidos = new Dictionary<int, LocationScope>
        {
            [10] = Compute(true, new ScopeGrant(null, "G1", null))
        };

        // Granja 99 no está en el diccionario ⇒ sin restricción
        Assert.True(NucleoVisible(restringidos, 99, "N2"));
        Assert.True(GalponVisible(restringidos, 99, "G3"));
        Assert.True(LotePermitido(restringidos, 99, 3));

        // Granja 10 restringida ⇒ membresía del cierre
        Assert.True(GalponVisible(restringidos, 10, "G1"));
        Assert.False(GalponVisible(restringidos, 10, "G2"));
        Assert.True(LotePermitido(restringidos, 10, 1));
        Assert.False(LotePermitido(restringidos, 10, 2));
        Assert.False(LotePermitido(restringidos, 10, null)); // sin loteId en granja restringida ⇒ NO
    }

    // ─── Validación de items del API admin ───

    [Theory]
    [InlineData(LevelNucleo, "N1", null, null)]
    [InlineData(LevelGalpon, null, "G1", null)]
    [InlineData(LevelLote, null, null, 7)]
    public void ValidarItem_Validos(string level, string? nucleoId, string? galponId, int? loteId)
    {
        Assert.Null(ValidarItem(level, nucleoId, galponId, loteId));
    }

    [Theory]
    [InlineData("granja", "N1", null, null)]      // nivel inválido
    [InlineData(null, "N1", null, null)]          // nivel nulo
    [InlineData(LevelNucleo, null, null, null)]   // falta nucleoId
    [InlineData(LevelNucleo, "N1", "G1", null)]   // sobra galponId
    [InlineData(LevelGalpon, null, null, null)]   // falta galponId
    [InlineData(LevelGalpon, null, "G1", 7)]      // sobra loteId
    [InlineData(LevelLote, null, null, null)]     // falta loteId
    [InlineData(LevelLote, "N1", null, 7)]        // sobra nucleoId
    public void ValidarItem_Invalidos(string? level, string? nucleoId, string? galponId, int? loteId)
    {
        Assert.NotNull(ValidarItem(level, nucleoId, galponId, loteId));
    }
}
