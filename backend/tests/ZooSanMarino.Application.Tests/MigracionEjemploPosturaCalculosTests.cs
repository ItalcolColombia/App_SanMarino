using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Hoja "Ejemplo" de la plantilla de postura. El invariante que estos tests blindan es el que hace
/// que el ejemplo sea confiable: se DERIVA de las columnas que la plantilla emitió, así que no puede
/// enseñar una columna que la empresa tiene apagada por flag.
/// </summary>
public class MigracionEjemploPosturaCalculosTests
{
    private static readonly DateTime Base = new(2026, 3, 15);

    private static FlagsPlantillaPostura SantaReyes => new(
        OcultaMachosEnPostura: true, ConsumoAlimentoSoloHembras: true,
        ClasificacionHuevoPorItems: true, CapturaHuevosEnLevante: false,
        ManejaInventarioPorSilo: true);

    private static FlagsPlantillaPostura Sanmarino => new(
        OcultaMachosEnPostura: false, ConsumoAlimentoSoloHembras: false,
        ClasificacionHuevoPorItems: false, CapturaHuevosEnLevante: true);

    private static DatosEjemploPostura Datos => new(
        FechaBase: Base,
        AlimentoNombre: "ALIMENTO POSTURA 1",
        ItemHuevoNombre: "HUEVO AA PRIMERA",
        ItemHuevoNombre2: "HUEVO PNC",
        LoteContraparte: "LOTE 219A",
        SiloNombre: "Silo 4");

    /// <summary>Las columnas que la plantilla emitiría para esa línea y esos flags.</summary>
    private static List<string> ColumnasEmitidas(bool esLevante, FlagsPlantillaPostura flags)
    {
        var esquema = esLevante ? MigracionEsquemas.SeguimientoLevante : MigracionEsquemas.SeguimientoProduccion;
        var ocultas = PlantillaPosturaCalculos.ColumnasOcultas(esLevante, flags);
        return esquema.Columnas.Where(c => !ocultas.Contains(c.Titulo)).Select(c => c.Titulo).ToList();
    }

    private static IReadOnlyList<BloqueEjemplo> Armar(bool esLevante, FlagsPlantillaPostura flags, bool hojaHuevos)
        => MigracionEjemploPosturaCalculos.Bloques(
            esLevante, flags, ColumnasEmitidas(esLevante, flags), Datos, hojaHuevos);

    // ── El invariante central ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Datos_LosEncabezadosDelEjemploSonExactamenteLosEmitidos(bool esLevante)
    {
        var emitidas = ColumnasEmitidas(esLevante, SantaReyes);
        var datos = Armar(esLevante, SantaReyes, hojaHuevos: !esLevante).Single(b => b.Hoja == "Datos");

        Assert.Equal(emitidas, datos.Encabezados);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TodaFilaTieneExactamenteUnValorPorEncabezado(bool esLevante)
    {
        foreach (var bloque in Armar(esLevante, SantaReyes, hojaHuevos: !esLevante))
            foreach (var fila in bloque.Filas)
                Assert.Equal(bloque.Encabezados.Count, fila.Count);
    }

    [Fact]
    public void SantaReyes_ElEjemploNoNombraNiUnaColumnaDeMachos()
    {
        foreach (var esLevante in new[] { true, false })
        {
            var datos = Armar(esLevante, SantaReyes, hojaHuevos: !esLevante).Single(b => b.Hoja == "Datos");
            Assert.DoesNotContain(datos.Encabezados, h => h.EndsWith(" M", StringComparison.Ordinal));
            Assert.DoesNotContain("Silo Alimento 1 M", datos.Encabezados);
            Assert.DoesNotContain(datos.Encabezados, h => h.EndsWith(" M (g)", StringComparison.Ordinal));
            Assert.DoesNotContain("Consumo M (kg)", datos.Encabezados);
        }
    }

    [Fact]
    public void SantaReyes_Produccion_ElEjemploNoNombraLasCategoriasNiElTotal()
    {
        var datos = Armar(esLevante: false, SantaReyes, hojaHuevos: true).Single(b => b.Hoja == "Datos");

        Assert.DoesNotContain("Huevo Total", datos.Encabezados);
        Assert.DoesNotContain("Huevo Incubable", datos.Encabezados);
        Assert.DoesNotContain("Huevo Limpio", datos.Encabezados);
        Assert.DoesNotContain("Peso Huevo (g)", datos.Encabezados);
    }

    [Fact]
    public void SantaReyes_MovimientosAves_DejaMachosVacio()
    {
        var mov = Armar(esLevante: true, SantaReyes, hojaHuevos: false)
            .Single(b => b.Hoja == MigracionEsquemas.MovimientosAvesLevante.Hoja);

        var iMachos = mov.Encabezados.ToList().IndexOf("Machos");
        Assert.True(iMachos >= 0);
        Assert.All(mov.Filas, f => Assert.Equal("", f[iMachos]));
    }

    [Fact]
    public void Sanmarino_MovimientosAves_SiMuestraMachos()
    {
        var mov = Armar(esLevante: true, Sanmarino, hojaHuevos: false)
            .Single(b => b.Hoja == MigracionEsquemas.MovimientosAvesLevante.Hoja);

        var iMachos = mov.Encabezados.ToList().IndexOf("Machos");
        Assert.Contains(mov.Filas, f => f[iMachos] != "");
    }

    // ── Que el ejemplo enseñe lo correcto ────────────────────────────────────────────────────────

    [Fact]
    public void ConAlimentoDelInventario_ElConsumoDirectoVaVacioYSeExplica()
    {
        // Empresa con los slots de inventario a la vista (Sanmarino): el ejemplo enseña a usarlos.
        var bloque = Armar(esLevante: true, Sanmarino, hojaHuevos: false).Single(b => b.Hoja == "Datos");
        var iConsumo = bloque.Encabezados.ToList().IndexOf("Consumo H (kg)");
        var iAlimento = bloque.Encabezados.ToList().IndexOf("Alimento 1 H");

        Assert.All(bloque.Filas, f => Assert.Equal("", f[iConsumo]));
        Assert.All(bloque.Filas, f => Assert.Equal("ALIMENTO POSTURA 1", f[iAlimento]));
        Assert.Contains(bloque.Notas, n => n.Contains("VACÍO", StringComparison.Ordinal));
    }

    [Fact]
    public void SantaReyes_ElEjemploLlenaElSiloDelSlotDeAlimento()
    {
        // Con inventario por silo el ejemplo tiene que mostrar el trio completo: alimento, consumo y
        // SILO. Sin el silo, el archivo copiado del ejemplo se rechazaria.
        var bloque = Armar(esLevante: true, SantaReyes, hojaHuevos: false).Single(b => b.Hoja == "Datos");
        var h = bloque.Encabezados.ToList();

        Assert.Contains("Silo Alimento 1 H", h);
        Assert.All(bloque.Filas, f => Assert.Equal("Silo 4", f[h.IndexOf("Silo Alimento 1 H")]));
        Assert.All(bloque.Filas, f => Assert.Equal("ALIMENTO POSTURA 1", f[h.IndexOf("Alimento 1 H")]));
        // Y el silo de MACHOS no aparece: esa empresa no digita alimento de machos.
        Assert.DoesNotContain("Silo Alimento 1 M", h);
    }

    [Fact]
    public void SantaReyes_LaHojaAlimentoDelEjemploTraeElSilo()
    {
        var alim = Armar(esLevante: true, SantaReyes, hojaHuevos: false)
            .Single(b => b.Hoja == MigracionEsquemas.AlimentoPostura.Hoja);
        var h = alim.Encabezados.ToList();

        Assert.Contains("Silo", h);
        Assert.All(alim.Filas, f => Assert.Equal("Silo 4", f[h.IndexOf("Silo")]));
    }

    [Fact]
    public void Sanmarino_LaHojaAlimentoDelEjemploNoNombraElSilo()
    {
        var alim = Armar(esLevante: true, Sanmarino, hojaHuevos: false)
            .Single(b => b.Hoja == MigracionEsquemas.AlimentoPostura.Hoja);

        Assert.DoesNotContain("Silo", alim.Encabezados);
        Assert.DoesNotContain("Silo Origen", alim.Encabezados);
    }

    [Fact]
    public void SinAlimentoEnElCatalogo_ElEjemploUsaElConsumoDirecto()
    {
        var flags = Sanmarino;
        var bloque = MigracionEjemploPosturaCalculos.Bloques(
            esLevante: true, flags, ColumnasEmitidas(true, flags),
            new DatosEjemploPostura(Base), incluyeHojaHuevos: false).Single(b => b.Hoja == "Datos");

        var iConsumo = bloque.Encabezados.ToList().IndexOf("Consumo H (kg)");
        Assert.Contains(bloque.Filas, f => f[iConsumo] != "");
    }

    [Fact]
    public void ConCategoriasALaVista_ElTotalYLosIncubablesQuedanVaciosParaQueSeDeriven()
    {
        var bloque = Armar(esLevante: false, Sanmarino, hojaHuevos: false).Single(b => b.Hoja == "Datos");
        var h = bloque.Encabezados.ToList();

        Assert.All(bloque.Filas, f => Assert.Equal("", f[h.IndexOf("Huevo Total")]));
        Assert.All(bloque.Filas, f => Assert.Equal("", f[h.IndexOf("Huevo Incubable")]));
        Assert.Contains(bloque.Filas, f => f[h.IndexOf("Huevo Limpio")] != "");
    }

    [Fact]
    public void LaFechaSiempreSeLlenaYAvanzaUnDiaPorFila()
    {
        var bloque = Armar(esLevante: true, SantaReyes, hojaHuevos: false).Single(b => b.Hoja == "Datos");
        var iFecha = bloque.Encabezados.ToList().IndexOf("Fecha");

        Assert.Equal(MigracionEjemploPosturaCalculos.DiasDeEjemplo, bloque.Filas.Count);
        Assert.Equal("2026-03-15", bloque.Filas[0][iFecha]);
        Assert.Equal("2026-03-16", bloque.Filas[1][iFecha]);
        Assert.Equal("2026-03-17", bloque.Filas[2][iFecha]);
    }

    [Fact]
    public void HojaHuevos_SoloSeIncluyeCuandoLaPlantillaLaEmite()
    {
        var conHoja = Armar(esLevante: false, SantaReyes, hojaHuevos: true);
        var sinHoja = Armar(esLevante: false, SantaReyes, hojaHuevos: false);

        Assert.Contains(conHoja, b => b.Hoja == MigracionEsquemas.HuevosPostura.Hoja);
        Assert.DoesNotContain(sinHoja, b => b.Hoja == MigracionEsquemas.HuevosPostura.Hoja);
    }

    [Fact]
    public void HojaHuevos_AvisaQueLosTiposSeDeclaranEnElLote()
    {
        // Es la precondición que hoy rechaza el archivo entero sin que la plantilla la insinúe.
        var huevos = Armar(esLevante: false, SantaReyes, hojaHuevos: true)
            .Single(b => b.Hoja == MigracionEsquemas.HuevosPostura.Hoja);

        Assert.Contains(huevos.Notas, n => n.Contains("DECLARADOS POR EL LOTE", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TodoBloqueIlustraUnaHojaRealDeLaPlantilla(bool esLevante)
    {
        var hojasReales = new[]
        {
            "Datos",
            MigracionEsquemas.AlimentoPostura.Hoja,
            MigracionEsquemas.MovimientosAvesLevante.Hoja,
            MigracionEsquemas.HuevosPostura.Hoja,
        };

        foreach (var bloque in Armar(esLevante, SantaReyes, hojaHuevos: !esLevante))
            Assert.Contains(bloque.Hoja, hojasReales);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TodoBloqueTraeAlMenosUnaFilaYUnaNota(bool esLevante)
    {
        foreach (var bloque in Armar(esLevante, SantaReyes, hojaHuevos: !esLevante))
        {
            Assert.NotEmpty(bloque.Filas);
            Assert.NotEmpty(bloque.Notas);
        }
    }
}
