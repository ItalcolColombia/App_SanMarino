using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Plantilla de seguimiento de postura parametrizada por empresa: qué columnas se omiten según los
/// flags. El contrato que estos tests blindan es doble —
/// (1) con los flags en su valor neutro la plantilla sale IDÉNTICA a la histórica (delta cero para
///     Sanmarino / Demo, las empresas que hoy usan el módulo);
/// (2) ninguna columna ocultable puede ser <c>Requerida: true</c>, porque el archivo generado tiene
///     que seguir pasando la validación de encabezados del propio importador.
/// </summary>
public class PlantillaPosturaCalculosTests
{
    /// <summary>Flags de Santa Reyes, medidos en <c>companies</c> (id 6) el 4-sep-2026.</summary>
    private static FlagsPlantillaPostura SantaReyes => new(
        OcultaMachosEnPostura: true,
        ConsumoAlimentoSoloHembras: true,
        ClasificacionHuevoPorItems: true,
        CapturaHuevosEnLevante: false,
        ManejaInventarioPorSilo: true);

    /// <summary>Flags de Agroavicola Sanmarino: todo apagado salvo la captura de huevos en levante.</summary>
    private static FlagsPlantillaPostura Sanmarino => new(
        OcultaMachosEnPostura: false,
        ConsumoAlimentoSoloHembras: false,
        ClasificacionHuevoPorItems: false,
        CapturaHuevosEnLevante: true);

    // ── Delta cero para las empresas que hoy usan el módulo ──────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Sanmarino_SoloOcultaLasColumnasDeSilo(bool esLevante)
    {
        // Sin inventario por silo, lo UNICO que se omite son las 4 columnas de silo por slot: el
        // parseo las rechaza en modo clasico, asi que ofrecerlas romperia el archivo.
        var ocultas = PlantillaPosturaCalculos.ColumnasOcultas(esLevante, Sanmarino);

        Assert.Equal(
            new[] { "Silo Alimento 1 H", "Silo Alimento 1 M", "Silo Alimento 2 H", "Silo Alimento 2 M" },
            ocultas.OrderBy(t => t, StringComparer.Ordinal).ToArray());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Sanmarino_LaPlantillaConservaLas43ColumnasHistoricas(bool esLevante)
    {
        var esquema = esLevante ? MigracionEsquemas.SeguimientoLevante : MigracionEsquemas.SeguimientoProduccion;
        var ocultas = PlantillaPosturaCalculos.ColumnasOcultas(esLevante, Sanmarino);

        var emitidas = esquema.Columnas.Where(c => !ocultas.Contains(c.Titulo)).ToList();
        // El esquema crecio con las 4 columnas de silo, pero una empresa sin silo sigue emitiendo
        // exactamente las 43 de siempre: delta cero.
        Assert.Equal(43, emitidas.Count);
        Assert.DoesNotContain(emitidas, c => c.Titulo.StartsWith("Silo ", StringComparison.Ordinal));
    }

    // ── Santa Reyes: la lista exacta ─────────────────────────────────────────────────────────────

    [Fact]
    public void SantaReyes_Levante_OcultaMachosYLosHuevosQueNoCaptura()
    {
        var ocultas = PlantillaPosturaCalculos.ColumnasOcultas(esLevante: true, SantaReyes);

        // Machos: las 7 columnas por sexo de levante.
        Assert.Contains("Mort M", ocultas);
        Assert.Contains("Sel M", ocultas);
        Assert.Contains("Error Sexaje M", ocultas);
        Assert.Contains("Consumo M (kg)", ocultas);
        Assert.Contains("Peso M (kg)", ocultas);
        Assert.Contains("Uniformidad M", ocultas);
        Assert.Contains("Coef. Variación M", ocultas);
        // Los 4 slots de alimento del inventario para machos.
        Assert.Contains("Alimento 1 M", ocultas);
        Assert.Contains("Consumo Alimento 1 M", ocultas);
        Assert.Contains("Alimento 2 M", ocultas);
        Assert.Contains("Consumo Alimento 2 M", ocultas);
        // Huevos: no captura en levante ⇒ las 11 categorías + el peso.
        Assert.Contains("Huevo Limpio", ocultas);
        Assert.Contains("Huevo Otro", ocultas);
        Assert.Contains("Peso Huevo (g)", ocultas);

        // El alimento del INVENTARIO si se ofrece, con su silo apareado a cada slot.
        Assert.DoesNotContain("Alimento 1 H", ocultas);
        Assert.DoesNotContain("Consumo Alimento 2 H", ocultas);
        Assert.DoesNotContain("Silo Alimento 1 H", ocultas);
        Assert.DoesNotContain("Silo Alimento 2 H", ocultas);
        // Los silos de los slots de MACHOS se van junto con sus slots.
        Assert.Contains("Silo Alimento 1 M", ocultas);
        Assert.Contains("Silo Alimento 2 M", ocultas);

        // Lo de hembras que SI se digita se conserva entero.
        Assert.DoesNotContain("Mort H", ocultas);
        Assert.DoesNotContain("Consumo H (kg)", ocultas);
        Assert.DoesNotContain("Coef. Variación H", ocultas);
        // Y todo lo que no depende del sexo tampoco se toca.
        Assert.DoesNotContain("Fecha", ocultas);
        Assert.DoesNotContain("Consumo Agua (L)", ocultas);
        Assert.DoesNotContain("Unidad Consumo", ocultas);
    }

    [Fact]
    public void SantaReyes_Produccion_OcultaMachosYElHuevoDeColumnasFijas()
    {
        var ocultas = PlantillaPosturaCalculos.ColumnasOcultas(esLevante: false, SantaReyes);

        Assert.Contains("Mort M", ocultas);
        Assert.Contains("Consumo M (kg)", ocultas);
        Assert.Contains("Peso M (kg)", ocultas);
        Assert.Contains("Consumo Alimento 2 M", ocultas);
        // Espejo exacto del modal: total, incubable, peso promedio y la clasificadora.
        Assert.Contains("Huevo Total", ocultas);
        Assert.Contains("Huevo Incubable", ocultas);
        Assert.Contains("Peso Huevo (g)", ocultas);
        Assert.Contains("Huevo Doble Yema", ocultas);

        // Etapa y observaciones siguen aplicando.
        Assert.DoesNotContain("Etapa", ocultas);
        Assert.DoesNotContain("Observaciones", ocultas);
        // Producción no tiene columnas de uniformidad/CV por sexo: no se pueden ocultar.
        Assert.DoesNotContain("Uniformidad M", ocultas);
        Assert.DoesNotContain("Coef. Variación M", ocultas);
    }

    [Fact]
    public void Produccion_ConUniformidadDeLoteNoSeOcultaAunqueOcultaMachos()
    {
        // "Uniformidad" y "Coef. Variación" de producción son del LOTE, no de un sexo.
        var ocultas = PlantillaPosturaCalculos.ColumnasOcultas(esLevante: false, SantaReyes);
        Assert.DoesNotContain("Uniformidad", ocultas);
        Assert.DoesNotContain("Coef. Variación", ocultas);
    }

    // ── Cada flag por separado ───────────────────────────────────────────────────────────────────

    [Fact]
    public void ConsumoSoloHembras_SinOcultarMachos_QuitaElAlimentoPeroConservaElConteo()
    {
        // Una empresa puede digitar el alimento solo de hembras y seguir contando machos.
        var flags = new FlagsPlantillaPostura(
            OcultaMachosEnPostura: false, ConsumoAlimentoSoloHembras: true,
            ClasificacionHuevoPorItems: false, CapturaHuevosEnLevante: true);
        var ocultas = PlantillaPosturaCalculos.ColumnasOcultas(esLevante: true, flags);

        Assert.Contains("Consumo M (kg)", ocultas);
        Assert.Contains("Alimento 1 M", ocultas);
        Assert.DoesNotContain("Mort M", ocultas);
        Assert.DoesNotContain("Peso M (kg)", ocultas);
        Assert.DoesNotContain("Alimento 1 H", ocultas);
    }

    [Fact]
    public void ClasificacionPorItems_NoAfectaLaPlantillaDeLevante()
    {
        // El gate del backend es `ClasificacionHuevoPorItems && !esLevante`: la hoja "Huevos" solo
        // existe en producción. En levante manda `CapturaHuevosEnLevante`.
        var flags = new FlagsPlantillaPostura(
            OcultaMachosEnPostura: false, ConsumoAlimentoSoloHembras: false,
            ClasificacionHuevoPorItems: true, CapturaHuevosEnLevante: true);
        var ocultas = PlantillaPosturaCalculos.ColumnasOcultas(esLevante: true, flags);

        // Lo unico omitido son las columnas de silo, que esta empresa no maneja.
        Assert.DoesNotContain(ocultas, t => !t.StartsWith("Silo ", StringComparison.Ordinal));
    }

    // ── Hoja "Alimento": solo donde el inventario se puede mover ─────────────────────────────────

    [Fact]
    public void ConInventarioPorSilo_LaHojaAlimentoLlevaSusColumnasDeSilo()
    {
        Assert.Empty(PlantillaPosturaCalculos.ColumnasOcultasHojaAlimento(manejaInventarioPorSilo: true));
    }

    [Fact]
    public void SinInventarioPorSilo_LaHojaAlimentoNoOfreceElSilo()
    {
        // En modo clasico el servicio de inventario RECHAZA un movimiento que traiga silo, asi que
        // emitir la columna seria ofrecer una que hace fallar el archivo.
        var ocultas = PlantillaPosturaCalculos.ColumnasOcultasHojaAlimento(manejaInventarioPorSilo: false);
        Assert.Equal(new[] { "Silo", "Silo Origen" }, ocultas.OrderBy(t => t, StringComparer.Ordinal).ToArray());
    }

    // ── El invariante que protege al importador ──────────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NingunaColumnaOcultableEsRequerida(bool esLevante)
    {
        var esquema = esLevante ? MigracionEsquemas.SeguimientoLevante : MigracionEsquemas.SeguimientoProduccion;
        var ocultables = PlantillaPosturaCalculos.TitulosOcultablesPorEsquema(esLevante);

        var requeridasOcultables = esquema.Columnas
            .Where(c => c.Requerida && ocultables.Contains(c.Titulo))
            .Select(c => c.Titulo)
            .ToList();

        Assert.Empty(requeridasOcultables);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TodaColumnaOcultableExisteEnElEsquema(bool esLevante)
    {
        // Si alguien renombra una columna en MigracionEsquemas y no acá, el título ocultable queda
        // huérfano: la plantilla seguiría emitiendo la columna y nadie se enteraría.
        var esquema = esLevante ? MigracionEsquemas.SeguimientoLevante : MigracionEsquemas.SeguimientoProduccion;
        var titulos = esquema.Columnas.Select(c => c.Titulo).ToHashSet(StringComparer.Ordinal);

        var huerfanos = PlantillaPosturaCalculos.TitulosOcultablesPorEsquema(esLevante)
            .Where(t => !titulos.Contains(t))
            .ToList();

        Assert.Empty(huerfanos);
    }
}
