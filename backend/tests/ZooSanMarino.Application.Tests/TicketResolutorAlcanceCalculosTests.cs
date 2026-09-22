using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Alcance del resolutor (EMPRESA / GLOBAL): a qué tickets aplica una fila, cómo se etiqueta y qué hay
/// que cambiar al guardar la plantilla desde UNA empresa sin pisar a las demás.
/// </summary>
/// <remarks>
/// El caso que da nombre a la etiqueta es real: Alexander Mejía atiende Soporte y Dudas solo en
/// Sanmarino y la pantalla lo mostraba como «Global» porque su fila tenía <c>pais_id NULL</c>.
/// </remarks>
public class TicketResolutorAlcanceCalculosTests
{
    private const int Sanmarino = 1;
    private const int SantaReyes = 6;

    // ── Aplica / etiqueta ──

    [Fact]
    public void Una_fila_de_empresa_solo_atiende_su_empresa()
    {
        Assert.True(TicketResolutorAlcanceCalculos.Aplica(TicketAlcance.Empresa, Sanmarino, Sanmarino));
        Assert.False(TicketResolutorAlcanceCalculos.Aplica(TicketAlcance.Empresa, Sanmarino, SantaReyes));
    }

    [Fact]
    public void Una_fila_global_atiende_cualquier_empresa()
        => Assert.True(TicketResolutorAlcanceCalculos.Aplica(TicketAlcance.Global, Sanmarino, SantaReyes));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("basura")]
    public void Un_alcance_ilegible_se_trata_como_empresa(string? alcance)
    {
        Assert.False(TicketResolutorAlcanceCalculos.Aplica(alcance, Sanmarino, SantaReyes));
        Assert.Equal("Agroavicola Sanmarino", TicketResolutorAlcanceCalculos.Etiqueta(alcance, "Agroavicola Sanmarino"));
    }

    [Fact]
    public void La_etiqueta_global_es_exclusiva_del_alcance_global()
    {
        Assert.Equal("Global", TicketResolutorAlcanceCalculos.Etiqueta(TicketAlcance.Global, "Agroavicola Sanmarino"));
        Assert.Equal("Agroavicola Sanmarino", TicketResolutorAlcanceCalculos.Etiqueta(TicketAlcance.Empresa, " Agroavicola Sanmarino "));
        Assert.Equal("Empresa", TicketResolutorAlcanceCalculos.Etiqueta(TicketAlcance.Empresa, null));
    }

    // ── Plan de guardado ──

    private static TicketResolutorAlcanceCalculos.Fila Fila(
        long id, string tipo, int empresa, string alcance = TicketAlcance.Empresa, bool activo = true, int? pais = null)
        => new(id, tipo, pais, empresa, alcance, activo);

    private static TicketResolutorAlcanceCalculos.Pedido Pide(string tipo, string? alcance = null, int? pais = null)
        => new(tipo, pais, alcance);

    [Fact]
    public void Prender_un_tipo_nuevo_crea_una_fila_de_empresa()
    {
        var plan = TicketResolutorAlcanceCalculos.Planificar(
            Array.Empty<TicketResolutorAlcanceCalculos.Fila>(),
            new[] { Pide(TicketTipos.Soporte, TicketAlcance.Empresa) }, Sanmarino);

        Assert.Empty(plan.Cambios);
        var alta = Assert.Single(plan.Altas);
        Assert.Equal(TicketTipos.Soporte, alta.Tipo);
        Assert.Equal(TicketAlcance.Empresa, alta.Alcance);
        Assert.False(plan.TocaGlobal);
    }

    [Fact]
    public void Apagar_un_tipo_desactiva_su_fila()
    {
        var plan = TicketResolutorAlcanceCalculos.Planificar(
            new[] { Fila(10, TicketTipos.Soporte, Sanmarino) },
            Array.Empty<TicketResolutorAlcanceCalculos.Pedido>(), Sanmarino);

        var cambio = Assert.Single(plan.Cambios);
        Assert.Equal(10, cambio.Id);
        Assert.False(cambio.Activo);
        Assert.False(plan.TocaGlobal);
    }

    [Fact]
    public void Pasar_de_empresa_a_global_convierte_la_fila_en_su_lugar()
    {
        // El índice único es (entidad, tipo, país, empresa): no puede haber dos filas para la misma
        // empresa, así que el cambio de alcance se hace sobre la fila que ya está.
        var plan = TicketResolutorAlcanceCalculos.Planificar(
            new[] { Fila(10, TicketTipos.Desarrollo, Sanmarino) },
            new[] { Pide(TicketTipos.Desarrollo, TicketAlcance.Global) }, Sanmarino);

        Assert.Empty(plan.Altas);
        var cambio = Assert.Single(plan.Cambios);
        Assert.Equal(10, cambio.Id);
        Assert.True(cambio.Activo);
        Assert.Equal(TicketAlcance.Global, cambio.Alcance);
        Assert.True(plan.TocaGlobal);
    }

    [Fact]
    public void Bajar_de_global_a_empresa_tambien_toca_global()
    {
        var plan = TicketResolutorAlcanceCalculos.Planificar(
            new[] { Fila(10, TicketTipos.Desarrollo, Sanmarino, TicketAlcance.Global) },
            new[] { Pide(TicketTipos.Desarrollo, TicketAlcance.Empresa) }, Sanmarino);

        var cambio = Assert.Single(plan.Cambios);
        Assert.Equal(TicketAlcance.Empresa, cambio.Alcance);
        Assert.True(plan.TocaGlobal);
    }

    [Fact]
    public void Una_fila_global_de_otra_empresa_se_reusa_no_se_duplica()
    {
        var plan = TicketResolutorAlcanceCalculos.Planificar(
            new[] { Fila(10, TicketTipos.Desarrollo, Sanmarino, TicketAlcance.Global, activo: false) },
            new[] { Pide(TicketTipos.Desarrollo, TicketAlcance.Global) }, SantaReyes);

        Assert.Empty(plan.Altas);
        var cambio = Assert.Single(plan.Cambios);
        Assert.Equal(10, cambio.Id);
        Assert.True(cambio.Activo);
        Assert.True(plan.TocaGlobal);
    }

    [Fact]
    public void Sin_cambios_no_hay_plan_ni_se_toca_global()
    {
        var plan = TicketResolutorAlcanceCalculos.Planificar(
            new[]
            {
                Fila(10, TicketTipos.Soporte, Sanmarino),
                Fila(11, TicketTipos.Desarrollo, Sanmarino, TicketAlcance.Global),
            },
            new[] { Pide(TicketTipos.Soporte), Pide(TicketTipos.Desarrollo) }, Sanmarino);

        Assert.Empty(plan.Cambios);
        Assert.Empty(plan.Altas);
        Assert.False(plan.TocaGlobal);
    }

    [Fact]
    public void Sin_alcance_en_el_pedido_se_conserva_el_de_la_fila()
    {
        // Es lo que manda la pantalla de Usuarios, que no conoce el alcance: reenvía lo que cargó y no
        // tiene que poder bajar una fila global a su empresa sin querer.
        var plan = TicketResolutorAlcanceCalculos.Planificar(
            new[] { Fila(11, TicketTipos.Requerimiento, Sanmarino, TicketAlcance.Global) },
            new[] { Pide(TicketTipos.Requerimiento) }, SantaReyes);

        Assert.Empty(plan.Cambios);
        Assert.Empty(plan.Altas);
        Assert.False(plan.TocaGlobal);
    }

    [Fact]
    public void Un_pedido_nuevo_sin_alcance_nace_de_empresa()
    {
        var plan = TicketResolutorAlcanceCalculos.Planificar(
            Array.Empty<TicketResolutorAlcanceCalculos.Fila>(),
            new[] { Pide(TicketTipos.Dudas) }, SantaReyes);

        Assert.Equal(TicketAlcance.Empresa, Assert.Single(plan.Altas).Alcance);
        Assert.False(plan.TocaGlobal);
    }

    [Fact]
    public void Reactivar_una_fila_apagada_no_crea_otra()
    {
        var plan = TicketResolutorAlcanceCalculos.Planificar(
            new[] { Fila(12, TicketTipos.Dudas, Sanmarino, activo: false) },
            new[] { Pide(TicketTipos.Dudas, TicketAlcance.Empresa) }, Sanmarino);

        Assert.Empty(plan.Altas);
        Assert.True(Assert.Single(plan.Cambios).Activo);
    }

    [Fact]
    public void El_pais_distingue_filas_del_mismo_tipo()
    {
        var plan = TicketResolutorAlcanceCalculos.Planificar(
            new[] { Fila(13, TicketTipos.Soporte, Sanmarino, pais: 1) },
            new[] { Pide(TicketTipos.Soporte, TicketAlcance.Empresa) }, Sanmarino);

        // El pedido es (Soporte, país null): la fila de país 1 queda sin reclamar y se apaga.
        Assert.Single(plan.Altas);
        Assert.False(Assert.Single(plan.Cambios).Activo);
    }

    [Fact]
    public void Un_pedido_repetido_no_duplica_altas()
    {
        var plan = TicketResolutorAlcanceCalculos.Planificar(
            Array.Empty<TicketResolutorAlcanceCalculos.Fila>(),
            new[] { Pide(TicketTipos.Soporte), Pide(TicketTipos.Soporte, TicketAlcance.Global) }, Sanmarino);

        Assert.Single(plan.Altas);
        Assert.False(plan.TocaGlobal);
    }
}
