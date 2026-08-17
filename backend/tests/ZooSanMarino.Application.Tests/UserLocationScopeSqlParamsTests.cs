using Xunit;
using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.UserLocationScopeCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// W4 — La regla ÚNICA de visibilidad de un registro ubicado (<see cref="PermiteUbicacion"/>) y su
/// aplanado a los 4 arrays que consumen <c>fn_vacunacion_filter_data</c> y
/// <c>fn_vacunacion_pendientes</c>.
///
/// <para>Estos tests son la <b>especificación ejecutable</b> del filtro que corre en SQL: la función
/// sólo prueba pertenencia a los conjuntos que salen de acá. Si el CASE de la fn deja de coincidir
/// con esta tabla, la BD y el backend le muestran cosas distintas al mismo usuario.</para>
/// </summary>
public class UserLocationScopeSqlParamsTests
{
    // Granja 7: N1 { G1: [lote 1], G2: [lote 2] }, N2 { G3: [lote 3] }
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
    };

    private static LocationScope Compute(bool restrict, params ScopeGrant[] grants) =>
        ComputeScope(restrict, grants, Nucleos, Galpones, Lotes);

    // ─────────────────────────────────────────────────────────────────────────
    // PermiteUbicacion — la tabla de decisión
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Global_PermiteCualquierUbicacion_InclusoInexistente()
    {
        var scope = LocationScope.Global;

        Assert.True(PermiteUbicacion(scope, "NX", "GX", 999));
        Assert.True(PermiteUbicacion(scope, null, null, null)); // ni siquiera hace falta ubicación
    }

    [Fact]
    public void ConLote_MandaElNivelLote_YIgnoraGalponYNucleo()
    {
        // Grant de galpón G1 ⇒ el lote 1 (que vive en G1) queda permitido; el 2 no.
        var scope = Compute(restrict: true, new ScopeGrant(null, "G1", null));

        // El lote 1 pasa por su propio id...
        Assert.True(PermiteUbicacion(scope, "N1", "G1", 1));
        // ...y el 2 NO pasa aunque se lo presente con un galpón permitido: con lote, manda el lote.
        Assert.False(PermiteUbicacion(scope, "N1", "G1", 2));
    }

    [Fact]
    public void SinLote_DecideElGalpon()
    {
        var scope = Compute(restrict: true, new ScopeGrant(null, "G1", null));

        Assert.True(PermiteUbicacion(scope, "N1", "G1", null));
        Assert.False(PermiteUbicacion(scope, "N1", "G2", null));
    }

    [Fact]
    public void SinLoteNiGalpon_DecideElNucleo()
    {
        var scope = Compute(restrict: true, new ScopeGrant("N1", null, null));

        Assert.True(PermiteUbicacion(scope, "N1", null, null));
        Assert.False(PermiteUbicacion(scope, "N2", null, null));
        // Cadena vacía cuenta como ausencia (así llega el dato desde SQL).
        Assert.True(PermiteUbicacion(scope, "N1", "", null));
    }

    [Fact]
    public void SinNingunaUbicacion_NoPasa_FailClosed()
    {
        var scope = Compute(restrict: true, new ScopeGrant("N1", null, null));

        Assert.False(PermiteUbicacion(scope, null, null, null));
        Assert.False(PermiteUbicacion(scope, "", "", null));
    }

    [Fact]
    public void GrantDeLote_NoAbreElGalponParaOtrosRegistros()
    {
        // El galpón G1 queda VISIBLE (navegación) por ser ancestro del lote 1 concedido; un registro
        // sin lote propio —engorde— apoyado en ese galpón hereda esa visibilidad. Es el
        // comportamiento vigente de los 3 servicios: se fija acá para que no cambie sin querer.
        var scope = Compute(restrict: true, new ScopeGrant(null, null, 1));

        Assert.True(PermiteUbicacion(scope, "N1", "G1", 1));   // el lote concedido
        Assert.False(PermiteUbicacion(scope, "N1", "G1", 2));  // otro lote del mismo galpón: NO
        Assert.True(PermiteUbicacion(scope, "N1", "G1", null)); // sin lote propio: hereda el galpón
    }

    [Fact]
    public void GranjaAusenteDelDiccionario_NoEstaRestringida()
    {
        var restringidos = new Dictionary<int, LocationScope>
        {
            [7] = Compute(restrict: true, new ScopeGrant(null, "G1", null)),
        };

        Assert.True(PermiteUbicacion(restringidos, granjaId: 99, "NX", "GX", 999)); // otra granja: global
        Assert.True(PermiteUbicacion(restringidos, granjaId: 7, "N1", "G1", null));
        Assert.False(PermiteUbicacion(restringidos, granjaId: 7, "N1", "G2", null));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AplanarParaSql — el cierre como 4 conjuntos
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SinGranjasRestringidas_LosCuatroArraysVanVacios()
    {
        var plano = AplanarParaSql(new Dictionary<int, LocationScope>());

        Assert.Empty(plano.FarmIds);
        Assert.Empty(plano.Nucleos);
        Assert.Empty(plano.Galpones);
        Assert.Empty(plano.Lotes);
    }

    [Fact]
    public void GranjaRestringidaSinGrants_SoloAparecEnFarmIds_FailClosed()
    {
        var restringidos = new Dictionary<int, LocationScope> { [7] = Compute(restrict: true) };

        var plano = AplanarParaSql(restringidos);

        Assert.Equal(new[] { 7 }, plano.FarmIds); // la fn la ve restringida...
        Assert.Empty(plano.Nucleos);              // ...y sin nada visible ⇒ cero filas de esa granja
        Assert.Empty(plano.Galpones);
        Assert.Empty(plano.Lotes);
    }

    [Fact]
    public void ClaveDeNucleoEsCompuesta_ParaNoMezclarGranjasHomonimas()
    {
        var restringidos = new Dictionary<int, LocationScope>
        {
            [7] = Compute(restrict: true, new ScopeGrant("N1", null, null)),
            [9] = Compute(restrict: true, new ScopeGrant("N2", null, null)),
        };

        var plano = AplanarParaSql(restringidos);

        Assert.Equal(new[] { 7, 9 }, plano.FarmIds);
        Assert.Equal(new[] { "7|N1", "9|N2" }, plano.Nucleos);
        // El núcleo N1 de la granja 7 NO habilita el N1 de la 9 (nucleo_id se repite entre granjas).
        Assert.DoesNotContain("9|N1", plano.Nucleos);
    }

    [Fact]
    public void ScopeGlobalColadoEnElDiccionario_NoRestringeLaGranja()
    {
        // El resolver sólo devuelve granjas restringidas; si igual llega una global, no se la filtra
        // (meterla en FarmIds sin nada visible la dejaría a ciegas).
        var restringidos = new Dictionary<int, LocationScope>
        {
            [7] = LocationScope.Global,
            [9] = Compute(restrict: true, new ScopeGrant(null, "G3", null)),
        };

        var plano = AplanarParaSql(restringidos);

        Assert.Equal(new[] { 9 }, plano.FarmIds);
        Assert.DoesNotContain(7, plano.FarmIds);
    }

    [Fact]
    public void ElAplanadoEsDeterministico_YDeduplicaEntreGranjas()
    {
        var restringidos = new Dictionary<int, LocationScope>
        {
            [9] = Compute(restrict: true, new ScopeGrant("N1", null, null)), // G1,G2 + lotes 1,2
            [7] = Compute(restrict: true, new ScopeGrant("N2", null, null)), // G3 + lote 3
        };

        var plano = AplanarParaSql(restringidos);

        Assert.Equal(new[] { 7, 9 }, plano.FarmIds);                    // ordenado, no por inserción
        Assert.Equal(new[] { "G1", "G2", "G3" }, plano.Galpones);
        Assert.Equal(new[] { 1, 2, 3 }, plano.Lotes);
        Assert.Equal(new[] { "7|N2", "9|N1" }, plano.Nucleos);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Contrato: el aplanado y la regla dicen lo mismo (es lo que implementa la SQL)
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    // (nucleo, galpon, loteTabla, esperado) sobre la granja 7 con grant de galpón G1
    [InlineData("N1", "G1", 1, true)]    // lote del galpón concedido
    [InlineData("N1", "G1", 2, false)]   // otro lote presentado en el galpón concedido
    [InlineData("N1", "G1", null, true)] // sin lote ⇒ manda el galpón
    [InlineData("N1", "G2", null, false)]
    [InlineData("N1", null, null, true)] // N1 queda visible por ser ancestro de G1
    [InlineData("N2", null, null, false)]
    [InlineData(null, null, null, false)]
    public void LaPertenenciaALosArrays_CoincideConLaRegla(
        string? nucleoId, string? galponId, int? loteTablaId, bool esperado)
    {
        const int granjaId = 7;
        var scope = Compute(restrict: true, new ScopeGrant(null, "G1", null));
        var restringidos = new Dictionary<int, LocationScope> { [granjaId] = scope };
        var plano = AplanarParaSql(restringidos);

        // Lo que decide el backend...
        Assert.Equal(esperado, PermiteUbicacion(scope, nucleoId, galponId, loteTablaId));

        // ...y lo que decide la SQL con los mismos arrays (réplica literal del CASE de la función).
        var porArrays =
            !plano.FarmIds.Contains(granjaId)
            || (loteTablaId.HasValue
                    ? plano.Lotes.Contains(loteTablaId.Value)
                    : !string.IsNullOrEmpty(galponId)
                        ? plano.Galpones.Contains(galponId)
                        : !string.IsNullOrEmpty(nucleoId)
                            && plano.Nucleos.Contains(ClaveNucleo(granjaId, nucleoId)));

        Assert.Equal(esperado, porArrays);
    }
}
