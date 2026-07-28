using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Decisiones puras de la carga masiva de POSTURA: posición de stock según el nivel de alimento
/// (empresa/granja), cuándo un consumo descuenta inventario, validación de etapa, referencias de los
/// movimientos y resolución de los totales de huevos entre sus tres fuentes.
/// Plan: <c>fase_de_desarrollo/migracion_masiva_postura_alimento_inventario_plan.md</c>.
/// </summary>
public class MigracionPosturaCalculosTests
{
    // ── Etapa ────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(null, true)]   // celda vacía: la fn aplica el default 1 (comportamiento actual)
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(0, false)]
    [InlineData(4, false)]
    [InlineData(-1, false)]
    public void EtapaValida_AceptaSoloVacioO1a3(int? etapa, bool esperado)
        => Assert.Equal(esperado, MigracionPosturaCalculos.EtapaValida(etapa));

    [Theory]
    [InlineData(null, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    public void EtapaEfectiva_VacioODeCeroCaeEn1(int? etapa, int esperado)
        => Assert.Equal(esperado, MigracionPosturaCalculos.EtapaEfectiva(etapa));

    // ── Nivel del stock ──────────────────────────────────────────────────────
    [Fact]
    public void PosicionStockDeLote_NivelGranja_AnulaNucleoYGalpon()
    {
        // Sanmarino / Santa Reyes: el stock vive en (farm, item) con nucleo_id y galpon_id NULL.
        var pos = MigracionPosturaCalculos.PosicionStockDeLote(false, 20, "819014", "G0326");

        Assert.Equal(20, pos.FarmId);
        Assert.Null(pos.NucleoId);
        Assert.Null(pos.GalponId);
    }

    [Fact]
    public void PosicionStockDeLote_NivelGalpon_ConservaLaUbicacionDelLote()
    {
        // Ecuador / Panamá: el alimento vive en el galpón, como en engorde.
        var pos = MigracionPosturaCalculos.PosicionStockDeLote(true, 37, "198400", "G0034");

        Assert.Equal(37, pos.FarmId);
        Assert.Equal("198400", pos.NucleoId);
        Assert.Equal("G0034", pos.GalponId);
    }

    [Fact]
    public void PosicionStockDeLote_NivelGalpon_NormalizaVaciosANull()
    {
        var pos = MigracionPosturaCalculos.PosicionStockDeLote(true, 37, "   ", "");

        Assert.Null(pos.NucleoId);
        Assert.Null(pos.GalponId);
    }

    [Fact]
    public void NormalizarUbicacionSegunNivel_NivelGranja_DescartaElDetalle()
    {
        var escrita = new UbicacionAlimento(5, "N1", "G9");

        var ajustada = MigracionPosturaCalculos.NormalizarUbicacionSegunNivel(escrita, manejaPorGalpon: false);

        Assert.Equal(new UbicacionAlimento(5, null, null), ajustada);
    }

    [Fact]
    public void NormalizarUbicacionSegunNivel_NivelGalpon_NoTocaNada()
    {
        var escrita = new UbicacionAlimento(5, "N1", "G9");

        var ajustada = MigracionPosturaCalculos.NormalizarUbicacionSegunNivel(escrita, manejaPorGalpon: true);

        Assert.Equal(escrita, ajustada);
    }

    [Theory]
    [InlineData(false, "N1", "G9", true)]   // nivel granja + detalle escrito ⇒ avisar
    [InlineData(false, null, "G9", true)]
    [InlineData(false, "N1", null, true)]
    [InlineData(false, null, null, false)]  // nivel granja sin detalle ⇒ nada que avisar
    [InlineData(true, "N1", "G9", false)]   // nivel galpón ⇒ el detalle SÍ se usa
    [InlineData(true, null, null, false)]
    public void UbicacionTraeDetalleIgnorado(bool manejaPorGalpon, string? nucleo, string? galpon, bool esperado)
    {
        var ubicacion = new UbicacionAlimento(1, nucleo, galpon);
        Assert.Equal(esperado, MigracionPosturaCalculos.UbicacionTraeDetalleIgnorado(ubicacion, manejaPorGalpon));
    }

    [Fact]
    public void UbicacionTraeDetalleIgnorado_EspaciosNoCuentanComoDetalle()
    {
        var ubicacion = new UbicacionAlimento(1, "   ", "");
        Assert.False(MigracionPosturaCalculos.UbicacionTraeDetalleIgnorado(ubicacion, manejaPorGalpon: false));
    }

    // ── Consumo: ítems vs directo ────────────────────────────────────────────
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(4, true)]
    public void ConsumoDescuentaInventario_SoloConItems(int items, bool esperado)
        => Assert.Equal(esperado, MigracionPosturaCalculos.ConsumoDescuentaInventario(items));

    [Theory]
    [InlineData(0, 120.5, false)]  // sin ítems: el consumo directo es el de siempre, no se ignora
    [InlineData(1, 120.5, true)]   // con ítems y consumo suelto: se avisa que el suelto se ignora
    [InlineData(1, 0d, false)]
    [InlineData(1, null, false)]
    [InlineData(0, null, false)]
    public void ConsumoDirectoIgnorado(int items, double? consumo, bool esperado)
        => Assert.Equal(esperado,
            MigracionPosturaCalculos.ConsumoDirectoIgnorado(items, consumo is null ? null : (decimal)consumo.Value));

    // ── Referencias (deben coincidir byte a byte con el alta manual) ─────────
    [Fact]
    public void ReferenciaConsumoLevante_EsLaMismaCadenaQueElAltaManual()
    {
        // SeguimientoLoteLevanteService.Crud.cs → $"Seguimiento lote levante #{created.Id} {dto.FechaRegistro:yyyy-MM-dd}"
        var referencia = MigracionPosturaCalculos.ReferenciaConsumoLevante(4321, new DateTime(2026, 3, 9));
        Assert.Equal("Seguimiento lote levante #4321 2026-03-09", referencia);
    }

    [Fact]
    public void ReferenciaConsumoProduccion_EsLaMismaCadenaQueElAltaManual()
    {
        // ProduccionService.cs → $"Seguimiento producción #{entity.Id} {request.FechaRegistro:yyyy-MM-dd}"
        var referencia = MigracionPosturaCalculos.ReferenciaConsumoProduccion(77, new DateTime(2025, 12, 31));
        Assert.Equal("Seguimiento producción #77 2025-12-31", referencia);
    }

    [Fact]
    public void Referencias_NoDependenDeLaCulturaDelProceso()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
            Assert.Equal("Seguimiento lote levante #1 2026-01-05",
                MigracionPosturaCalculos.ReferenciaConsumoLevante(1, new DateTime(2026, 1, 5)));
        }
        finally { Thread.CurrentThread.CurrentCulture = original; }
    }

    // ── Huevos ───────────────────────────────────────────────────────────────
    private static HuevosClasificacion Clasificacion(int limpio = 0, int tratado = 0, int sucio = 0, int roto = 0)
        => new(Limpio: limpio, Tratado: tratado, Sucio: sucio, Roto: roto);

    [Fact]
    public void TotalesHuevoEfectivos_SinCategorias_UsaLosExplicitos()
    {
        // Es el archivo histórico: solo Total e Incubable. El resultado debe ser el de siempre.
        var (total, inc) = MigracionPosturaCalculos.TotalesHuevoEfectivos(HuevosClasificacion.Cero, 1200, 1100);

        Assert.Equal(1200, total);
        Assert.Equal(1100, inc);
    }

    [Fact]
    public void TotalesHuevoEfectivos_SinCategoriasNiExplicitos_EsCero()
    {
        var (total, inc) = MigracionPosturaCalculos.TotalesHuevoEfectivos(HuevosClasificacion.Cero, null, null);

        Assert.Equal(0, total);
        Assert.Equal(0, inc);
    }

    [Fact]
    public void TotalesHuevoEfectivos_ConCategorias_LosDerivaDelDesglose()
    {
        // Igual que el modal: Totales = suma de las 11, Incubables = Limpio + Tratado.
        var c = Clasificacion(limpio: 800, tratado: 150, sucio: 40, roto: 10);

        var (total, inc) = MigracionPosturaCalculos.TotalesHuevoEfectivos(c, totalExplicito: 999, incubablesExplicitos: 999);

        Assert.Equal(1000, total);
        Assert.Equal(950, inc);
    }

    [Theory]
    [InlineData(1000, false)]  // el explícito coincide con la suma
    [InlineData(999, true)]    // discrepa ⇒ advertencia (gana el desglose)
    [InlineData(null, false)]  // sin explícito no hay nada que comparar
    public void TotalDiscrepaDeCategorias(int? explicito, bool esperado)
    {
        var c = Clasificacion(limpio: 800, tratado: 150, sucio: 40, roto: 10); // 1000
        Assert.Equal(esperado, MigracionPosturaCalculos.TotalDiscrepaDeCategorias(explicito, c));
    }

    [Fact]
    public void TotalDiscrepaDeCategorias_SinCategorias_NuncaDiscrepa()
        => Assert.False(MigracionPosturaCalculos.TotalDiscrepaDeCategorias(1200, HuevosClasificacion.Cero));

    [Theory]
    [InlineData(false, 0, null, false)]  // sin ítems: nunca es mezcla
    [InlineData(false, 500, 300, false)]
    [InlineData(true, 0, null, false)]   // solo ítems: correcto
    [InlineData(true, 500, null, true)]  // ítems + categorías ⇒ error
    [InlineData(true, 0, 300, true)]     // ítems + incubables ⇒ error
    public void MezclaFuentesDeHuevos(bool hayItems, int limpio, int? incubables, bool esperado)
    {
        var c = Clasificacion(limpio: limpio);
        Assert.Equal(esperado, MigracionPosturaCalculos.MezclaFuentesDeHuevos(hayItems, c, incubables));
    }

    // ── Merge con el arrastre de huevos del levante (día del cierre) ─────────
    // Es el primer día de producción en toda migración: el cierre deja una fila con los huevos
    // arrastrados y el Excel trae ese mismo día. Contrato: se SUMAN categoría por categoría y los
    // totales se derivan del resultado — idéntico a ProduccionService.AplicarRequestSobreFilaArrastre.

    [Fact]
    public void MergeArrastre_SumaCategoriasYDerivaLosTotales()
    {
        var arrastrado = new HuevosClasificacion(Limpio: 720, Tratado: 60, Sucio: 24);
        var delExcel = new HuevosClasificacion(Limpio: 1500, Tratado: 260, Sucio: 45, Roto: 9);

        var sumado = HuevosLevanteCalculos.Sumar(arrastrado, delExcel);
        // Con merge, los totales explícitos del Excel NO se usan: manda el desglose sumado.
        var (total, inc) = MigracionPosturaCalculos.TotalesHuevoEfectivos(sumado, null, null);

        Assert.Equal(2220, sumado.Limpio);
        Assert.Equal(320, sumado.Tratado);
        Assert.Equal(2618, total);   // 2220 + 320 + 69 + 9
        Assert.Equal(2540, inc);     // Limpio + Tratado
    }

    [Fact]
    public void MergeArrastre_SinHuevosEnElExcel_ConservaLosArrastrados()
    {
        var arrastrado = new HuevosClasificacion(Limpio: 720, Tratado: 60, Sucio: 24);

        var sumado = HuevosLevanteCalculos.Sumar(arrastrado, HuevosClasificacion.Cero);
        var (total, inc) = MigracionPosturaCalculos.TotalesHuevoEfectivos(sumado, null, null);

        Assert.Equal(804, total);
        Assert.Equal(780, inc);
    }

    [Theory]
    [InlineData(0, null, null, null, false)]
    [InlineData(10, null, null, null, true)]
    [InlineData(0, 500, null, null, true)]
    [InlineData(0, null, 400, null, true)]
    [InlineData(0, null, null, 57.8, true)]
    [InlineData(0, 0, 0, 0d, false)]
    public void TraeHuevos(int limpio, int? total, int? inc, double? peso, bool esperado)
    {
        var c = Clasificacion(limpio: limpio);
        Assert.Equal(esperado, MigracionPosturaCalculos.TraeHuevos(c, total, inc, peso));
    }
}
